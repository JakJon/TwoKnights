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
    private Vector2 velocity = Vector2.zero; // Serpent's Breath clouds drift

    private float elapsed;
    private float sinceLastCheck;
    private bool emissionStopped;
    private ParticleSystem particles;
    private readonly HashSet<EnemyBase> alreadyPoisoned = new HashSet<EnemyBase>();

    // level 1: small, brief puff; level 2: wider cloud that lingers
    public static PoisonCloud Spawn(Vector2 position, int level, string ownerTag)
    {
        var cloudObject = new GameObject("PoisonCloud");
        cloudObject.transform.position = position;
        var cloud = cloudObject.AddComponent<PoisonCloud>();
        cloud.radius = level >= 2 ? 2.0f : 1.4f;
        cloud.duration = level >= 2 ? 10f : 3f;
        cloud.ownerTag = ownerTag;
        cloud.BuildCloudParticles();
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
        cloud.BuildCloudParticles();
        return cloud;
    }

    // One-shot radial pop for Plaguebringer bursts; cleans itself up
    public static void SpawnBurstEffect(Vector2 position)
    {
        AudioManager.Instance.PlaySFX(AudioManager.Instance.poisonBurst);

        var burstObject = new GameObject("PlagueBurst");
        burstObject.transform.position = position;
        var burst = CreateParticleSystem(burstObject, new Color(0.55f, 0.85f, 0.35f, 0.9f));

        var main = burst.main;
        main.startLifetime = 0.5f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);

        var emission = burst.emission;
        emission.rateOverTime = 0f;

        var shape = burst.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.15f;

        burst.Play();
        burst.Emit(28);
        Destroy(burstObject, 1.5f);
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

    private void PoisonEnemiesInside()
    {
        if (emissionStopped) return;
        foreach (var col in Physics2D.OverlapCircleAll(transform.position, radius * HitboxScale))
        {
            EnemyBase enemy = col.GetComponent<EnemyBase>();
            if (enemy == null || enemy.IsDead || alreadyPoisoned.Contains(enemy)) continue;
            alreadyPoisoned.Add(enemy);
            enemy.ApplyPoisonFromTag(CloudPoisonDamage, CloudPoisonDuration, 1f, ownerTag);
        }
    }

    private void BuildCloudParticles()
    {
        particles = CreateParticleSystem(gameObject, new Color(0.5f, 0.8f, 0.35f, 0.55f));

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

    // Shared builder matching PoisonBubbleEffect's approach: sprite texture on the
    // default sprite shader, world space, shrink + fade over lifetime
    private static ParticleSystem CreateParticleSystem(GameObject host, Color tint)
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
