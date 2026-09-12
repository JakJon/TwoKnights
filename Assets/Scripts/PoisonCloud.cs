using UnityEngine;
using System.Collections.Generic;

// A lingering miasma cloud left behind when a poisoned enemy dies (Serpent Order).
// Periodically poisons enemies inside its radius — each enemy only once per cloud —
// crediting the knight whose poison created it. Visuals are a code-built particle
// swirl reusing the poison bubble sprite; no dedicated art required.
public class PoisonCloud : MonoBehaviour
{
    private const float CheckInterval = 0.75f;
    private const int CloudPoisonDamage = 3;
    private const float CloudPoisonDuration = 6f;
    private const float ParticleLifetime = 1.6f;

    // The swirl fills ~0.85 of the cloud radius, so damage/pickup reach 1.15x keeps
    // the hitbox a hair past the drawn edge instead of short of it. Visuals unchanged.
    private const float HitboxScale = 1.15f;

    // Every live, still-emitting cloud, so a passing arrow can be poisoned by the
    // field it flies through (see PlayerProjectile). Registry rather than colliders:
    // clouds are pure particle systems with no physics body.
    private static readonly List<PoisonCloud> _active = new List<PoisonCloud>();

    // A traveling cloud despawns once fully outside the playfield
    private const float OffscreenX = 13f;
    private const float OffscreenY = 8f;

    private float radius;
    private float duration;
    private string ownerTag;
    private Vector2 velocity = Vector2.zero; // zero = stays where it was made

    // Miasma clouds carry on the way the dying enemy was walking, at the same
    // speed a Serpent's Breath cloud drifts
    private const float MiasmaDriftSpeed = 1.5f;

    // Plaguebringer: every cloud made by a knight who owns it hunts. It moves onto the
    // nearest mob within HomingRadius that it has not poisoned yet, and once that mob
    // is poisoned it goes after the next one — so one cloud walks the venom down a
    // line of enemies instead of waiting for them to walk into it. Mobs outside the
    // camera view are never chosen, so a cloud does not wander off the edge after
    // something still walking in.
    //
    // Like Guided Shot the target is sticky: the cloud keeps its mob until that mob is
    // poisoned, dies, or leaves the view, rather than re-picking the nearest every
    // search and drifting between two of them.
    private const float HomingRadius = 3f;
    // The chase runs at the target's own measured pace plus this, so a cloud always
    // gains on whatever it is after — slowly on a wolf, and it closes on a sleeping
    // or frozen mob at just this speed
    private const float HomingSpeedMargin = 0.5f;
    private const float HomingAcceleration = 8f;     // units/s² — how quickly it turns onto a new mob
    private const float HomingArriveDistance = 0.5f; // eases off inside this, so it settles over the mob instead of circling it
    private const float HomingSearchInterval = 0.1f;

    // The puff sprite is white and tinted here. A Plaguebringer cloud is the same
    // green with Guardian gold flecked through it: about one puff in five comes out
    // golden-white, so a hunting cloud reads as the Serpent + Guardian thing it is.
    // Which puffs are gold is random, which is fine — it is colour only, and changes
    // nothing about what the cloud does.
    private static readonly Color VenomTint = new Color(0.5f, 0.8f, 0.35f, 0.55f);
    private static readonly Color PlagueFleckTint = new Color(1f, 0.93f, 0.7f, 0.7f);
    private const float PlagueFleckShare = 0.2f;

    // Shared by every homing cloud's search, rather than allocated per sweep. Enemy
    // colliders are triggers, so the filter has to ask for them (as ShieldSight does).
    private static readonly Collider2D[] HomingHits = new Collider2D[64];
    private static readonly ContactFilter2D HomingFilter = new ContactFilter2D { useTriggers = true };

    private bool homing;
    private EnemyBase homingTarget;
    private Collider2D homingBody;
    private float nextHomingSearch;

    private float elapsed;
    private float sinceLastCheck;
    private bool emissionStopped;
    private ParticleSystem particles;
    private readonly HashSet<EnemyBase> alreadyPoisoned = new HashSet<EnemyBase>();

    // level 1: small, brief puff; level 2: wider cloud that lingers.
    // `heading` is the way the dying enemy was walking; the cloud drifts along it at
    // MiasmaDriftSpeed. Zero (an enemy that never moved) leaves it where it was made.
    public static PoisonCloud Spawn(Vector2 position, int level, string ownerTag,
        Vector2 heading = default)
    {
        var cloudObject = new GameObject("PoisonCloud");
        cloudObject.transform.position = position;
        var cloud = cloudObject.AddComponent<PoisonCloud>();
        cloud.radius = level >= 2 ? 2.0f : 1.4f;
        cloud.duration = level >= 2 ? 10f : 3f;
        cloud.ownerTag = ownerTag;
        cloud.velocity = heading.normalized * MiasmaDriftSpeed;
        cloud.Begin();
        return cloud;
    }

    // Serpent's Breath: a cloud exhaled by a sword swing that drifts along the
    // shield facing until its time runs out or it leaves the playfield
    public static PoisonCloud SpawnTraveling(Vector2 position, Vector2 direction, float duration,
        bool large, string ownerTag, float speed = 1.5f)
    {
        var cloudObject = new GameObject("PoisonCloud");
        cloudObject.transform.position = position;
        var cloud = cloudObject.AddComponent<PoisonCloud>();
        cloud.radius = large ? 1.6f : 0.9f;
        cloud.duration = duration;
        cloud.ownerTag = ownerTag;
        cloud.velocity = direction.normalized * speed;
        cloud.Begin();
        return cloud;
    }

    // A cloud whose size and lifetime the CALLER states, rather than picking one of
    // the two Miasma levels. Vial Throw wants a cloud that is neither: a Miasma
    // level 1 puff is gone in three seconds and a level 2 lingers ten and covers
    // two units, and a vial arrives every four or five shots, so it wants
    // something in between that it can state itself.
    //
    // `drift` is world units per second the puff carries on at, and which way. Zero
    // leaves it where it was made. A vial passes its own heading, so the venom keeps
    // going the way it was thrown rather than stopping dead where the glass broke.
    public static PoisonCloud SpawnPuff(Vector2 position, float radius, float duration,
        string ownerTag, Vector2 drift = default)
    {
        var cloudObject = new GameObject("PoisonCloud");
        cloudObject.transform.position = position;
        var cloud = cloudObject.AddComponent<PoisonCloud>();
        cloud.radius = Mathf.Max(0.2f, radius);
        cloud.duration = Mathf.Max(0.2f, duration);
        cloud.ownerTag = ownerTag;
        cloud.velocity = drift;
        cloud.Begin();
        return cloud;
    }

    // The last step of every Spawn method, once ownerTag is set. Whether the cloud
    // hunts has to be known before the particles are built, because a hunting cloud
    // is drawn in the Plaguebringer tint from its very first puff.
    private void Begin()
    {
        homing = OwnerHasPlaguebringer(ownerTag);
        BuildCloudParticles();
    }

    private static bool OwnerHasPlaguebringer(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return false;
        GameObject knight = GameObject.FindWithTag(tag);
        PoisonTipBoost boost = knight != null ? knight.GetComponent<PoisonTipBoost>() : null;
        return boost != null && boost.Plaguebringer;
    }

    private void OnEnable()
    {
        if (!_active.Contains(this)) _active.Add(this);
    }

    private void OnDisable()
    {
        _active.Remove(this);
    }

    // True if the point sits inside any live cloud — an arrow flying through picks
    // up poison and becomes a poisoned arrow (its own owner is credited on hit, so
    // the cloud's tag is irrelevant here). Stopped/fading clouds no longer count.
    public static bool SampleAt(Vector2 position)
    {
        for (int i = 0; i < _active.Count; i++)
        {
            PoisonCloud cloud = _active[i];
            if (cloud == null || cloud.emissionStopped) continue;
            float dx = cloud.transform.position.x - position.x;
            float dy = cloud.transform.position.y - position.y;
            float hitRadius = cloud.radius * HitboxScale;
            if (dx * dx + dy * dy <= hitRadius * hitRadius) return true;
        }
        return false;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        sinceLastCheck += Time.deltaTime;

        if (homing && !emissionStopped)
        {
            Home();
        }

        if (velocity != Vector2.zero && !emissionStopped)
        {
            transform.position += (Vector3)(velocity * Time.deltaTime);
        }

        if (sinceLastCheck >= CheckInterval)
        {
            sinceLastCheck = 0f;
            PoisonEnemiesInside();
        }

        bool offscreen = Mathf.Abs(transform.position.x) > OffscreenX
            || Mathf.Abs(transform.position.y) > OffscreenY;

        if ((elapsed >= duration || offscreen) && !emissionStopped)
        {
            emissionStopped = true;
            if (particles != null)
            {
                var emission = particles.emission;
                emission.enabled = false;
            }
            Destroy(gameObject, ParticleLifetime + 0.5f);
        }
    }

    // Steers `velocity` toward the current mob. With no mob in reach the cloud keeps
    // whatever velocity it has, so it carries on exactly as a plain cloud would.
    private void Home()
    {
        if (!IsWorthChasing(homingTarget, homingBody))
        {
            homingTarget = null;
            homingBody = null;
            FindHomingTarget();
        }

        if (homingTarget == null) return;

        // The body's middle, not its transform — enemy pivots sit at the feet
        Vector2 toTarget = (Vector2)homingBody.bounds.center - (Vector2)transform.position;
        float distance = toTarget.magnitude;
        float chaseSpeed = homingTarget.MeasuredSpeed + HomingSpeedMargin;
        Vector2 desired = distance > 1e-4f
            ? toTarget / distance * (chaseSpeed * Mathf.Clamp01(distance / HomingArriveDistance))
            : Vector2.zero;
        velocity = Vector2.MoveTowards(velocity, desired, HomingAcceleration * Time.deltaTime);
    }

    // A mob this cloud has already poisoned is not worth chasing — it would only ever
    // be poisoned once by this cloud anyway — so that is what moves it on to the next
    private bool IsWorthChasing(EnemyBase enemy, Collider2D body)
    {
        return enemy != null && !enemy.IsDead
            && body != null && body.enabled
            && !alreadyPoisoned.Contains(enemy)
            && FireField.IsInsideView(body.bounds.center);
    }

    private void FindHomingTarget()
    {
        if (Time.time < nextHomingSearch) return;
        nextHomingSearch = Time.time + HomingSearchInterval;

        Vector2 here = transform.position;
        int count = Physics2D.OverlapCircle(here, HomingRadius, HomingFilter, HomingHits);

        float bestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = HomingHits[i];
            if (hit == null) continue;

            EnemyBase enemy = hit.GetComponent<EnemyBase>();
            if (!IsWorthChasing(enemy, hit)) continue;

            float distance = ((Vector2)hit.bounds.center - here).sqrMagnitude;
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            homingTarget = enemy;
            homingBody = hit;
        }
    }

    private bool _recordedFour;

    private void PoisonEnemiesInside()
    {
        if (emissionStopped) return;
        foreach (var col in Physics2D.OverlapCircleAll(transform.position, radius * HitboxScale))
        {
            EnemyBase enemy = col.GetComponent<EnemyBase>();
            if (enemy == null || enemy.IsDead || alreadyPoisoned.Contains(enemy)) continue;
            alreadyPoisoned.Add(enemy);
            enemy.ApplyPoisonFromTag(CloudPoisonDamage, CloudPoisonDuration, 1f, ownerTag);

            // Four caught by ONE cloud — the Serpent quest asks for a good placement,
            // not a lifetime total, so it counts per cloud and only once
            if (alreadyPoisoned.Count >= 4 && !_recordedFour)
            {
                _recordedFour = true;
                Feats.Record(Feats.PoisonCloudFour);
            }
        }
    }

    private void BuildCloudParticles()
    {
        particles = CreateParticleSystem(gameObject,
            homing ? PlagueFleckedTint() : new ParticleSystem.MinMaxGradient(VenomTint));

        var main = particles.main;
        main.startLifetime = ParticleLifetime;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        main.prewarm = false;

        // Emission scaled to area so both cloud sizes read equally dense
        var emission = particles.emission;
        emission.rateOverTime = 10f * radius;

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * 0.85f;

        // Lazy upward drift with sideways waft
        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        particles.Play();
        particles.Emit(Mathf.RoundToInt(6f * radius)); // visible immediately
    }

    // Each puff picks its colour at random from this gradient. It is a step, not a
    // blend (GradientMode.Fixed), so a puff is either plainly green or plainly gold —
    // never a muddy in-between. The green and gold keys are doubled up either side of
    // the split so the share comes out the same whichever side of a key Unity's
    // Fixed mode reads a colour from.
    private static ParticleSystem.MinMaxGradient PlagueFleckedTint()
    {
        float split = 1f - PlagueFleckShare;
        var gradient = new Gradient { mode = GradientMode.Fixed };
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(VenomTint, 0f),
                new GradientColorKey(VenomTint, split),
                new GradientColorKey(PlagueFleckTint, split + 0.001f),
                new GradientColorKey(PlagueFleckTint, 1f),
            },
            new[]
            {
                new GradientAlphaKey(VenomTint.a, 0f),
                new GradientAlphaKey(VenomTint.a, split),
                new GradientAlphaKey(PlagueFleckTint.a, split + 0.001f),
                new GradientAlphaKey(PlagueFleckTint.a, 1f),
            });
        return new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };
    }

    // Shared builder matching PoisonBubbleEffect's approach: sprite texture on the
    // default sprite shader, world space, shrink + fade over lifetime
    private static ParticleSystem CreateParticleSystem(GameObject host, ParticleSystem.MinMaxGradient tint)
    {
        var system = host.AddComponent<ParticleSystem>();

        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = tint;
        main.maxParticles = 150;
        main.playOnAwake = false;

        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0.3f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = gradient;

        var renderer = system.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            var material = new Material(Shader.Find("Sprites/Default"));
            // Prefer the dedicated puff sprite (white, code-tinted); fall back
            // to the bubble sprite so clouds still render without it
            Sprite cloudSprite = null;
            if (PoisonResourceManager.Instance != null)
            {
                cloudSprite = PoisonResourceManager.Instance.GetPoisonPuffSprite();
                if (cloudSprite == null)
                {
                    cloudSprite = PoisonResourceManager.Instance.GetPoisonBubbleSprite();
                }
            }
            if (cloudSprite != null)
            {
                material.mainTexture = cloudSprite.texture;
            }
            renderer.material = material;
        }

        return system;
    }
}
