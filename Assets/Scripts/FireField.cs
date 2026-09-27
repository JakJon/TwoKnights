using UnityEngine;
using System.Collections.Generic;

// The Ember Order's one primitive: burning ground.
//
// Every Ember upgrade places fire zones — fireball craters, sword-hurled
// firebrands, and the trails ignited enemies drip behind them. A zone deals flat
// damage to anything standing in it and NOTHING ELSE.
//
// BURNING GROUND DOES NOT IGNITE (owner's call, 2026-09-06) — not a trail, not a
// placed zone, and not a fireball's crater. Scorched Earth briefly bought the
// exception and no longer does (owner's call, 2026-09-08): the capstone makes zones
// eternal and hotter, and that is all. Each zone still carries an "ignites" bit and
// Sample still reports it, because EmberBoost.GroundFireIgnites can arm it from a
// debug console — but no upgrade sets it, so in a real run it is always false.
//
// Overlapping zones are ONE fire, not many. Sample returns the hottest zone under a
// point; nothing anywhere counts zones. That is the invariant that keeps a Fire Trail
// lane — which is dozens of overlapping drops — from billing dozens of times over.
//
// Blue embers mark ETERNAL zones, so Scorched Earth's fire looks different from fire
// that is going to burn out.
//
// THE VIEW IS A HARD BOUNDARY FOR GROUND FIRE (owner's call, 2026-09-08). Waves spawn
// off screen and walk in, and fire the player cannot see must not be hurting things
// the player cannot see. Three rules, all keyed on IsInsideView:
//
//   1. A zone whose CENTRE is off screen is never placed — Add refuses it, so no
//      call site can forget. That is the "completely ignored" case.
//   2. A zone that straddles the edge is TRIMMED to it, because Sample refuses any
//      point off screen. The half of a crater inside the view burns; the half hanging
//      out of it is inert ground.
//   3. Nothing is DRAWN off screen either, so a trimmed crater reads as the clipped
//      shape it actually is rather than as a full circle half of which does nothing.
//
// This is the FIELD only. Ignition is untouched: an enemy lit by an arrow or a
// fireball goes on burning wherever it walks, on screen or off, because that burn
// rides the body rather than the ground. See EnemyBase's two damage channels.
//
// Igniting zones are also the ones that carry BLUE embers among the orange, so the
// player can read "this ground lights you" off the screen rather than off a tooltip.
//
// The one-way direction is unchanged and still worth keeping: this class holds no
// reference to EnemyBase and cannot reach one. Enemies SAMPLE the field and decide
// for themselves whether to catch (EnemyBase.LateUpdate), rather than the field
// reaching out to damage or ignite them. Zones stay dumb data.
//
// Why one central object instead of PoisonCloud's one-GameObject-per-cloud: Fire
// Trail drops a zone every 0.35s PER burning enemy. Twenty burning rats would be
// ~57 GameObject+ParticleSystem allocations a second. Zones are plain structs in
// one list, drawn by a single pooled particle system.
public class FireField : MonoBehaviour
{
    private struct Zone
    {
        public Vector2 position;
        public float radius;
        public float expiresAt; // +infinity for Scorched Earth zones
        public float bornAt;    // when it was lit, for "one fire that burned a minute"
        public string ownerTag;
        public float dps;
        public bool isTrail; // laid by Fire Trail, as opposed to a crater or placed zone

        // Whether standing in this zone sets a body alight. False in every real run —
        // only EmberBoost.GroundFireIgnites arms it, and no upgrade touches that. A
        // plain bool: it used to be an ignition SOURCE ID, back when catching from the
        // ground minted a burn per distinct fire underfoot, which is what produced
        // 40-70 damage a step. See EnemyBase's fire-AoE flag.
        public bool ignites;

        // The body that dripped this zone, so it can walk its own trail without the
        // trail burning it a second time on top of the burn that laid it.
        public int sourceEnemyId;
    }

    // Scorched Earth makes zones immortal, so the list needs a hard ceiling or a
    // long wave becomes a memory leak. Oldest retires first; invisible in play.
    private const int MaxZones = 200;

    // Particle budget for the whole field, not per zone
    private const float EmitInterval = 0.06f;
    private const float ParticleLifetime = 0.85f;
    private const int MaxParticles = 400;

    // Damage reaches slightly past the drawn flames: particles fill ~0.85 of the
    // zone radius, so sampling at 1.15x keeps the hitbox a hair larger than the
    // graphic rather than falling short of its visible edge. Visuals are untouched.
    public const float HitboxScale = 1.15f;

    private static readonly Color FireTint = new Color(1f, 0.48f, 0.16f, 0.75f);

    // Blue embers ride along in ETERNAL zones — Scorched Earth's tell, and the one
    // thing that tells a lane which is about to go out from one which never will. Kept
    // at roughly one particle in five and three-quarters the size of an orange one, so
    // it reads as a colder core inside the same fire rather than as a second effect
    // competing with it. Deep blue rather than sky blue (owner's call, 2026-09-08):
    // against an orange fire a pale blue reads as white and washes out, while a dark
    // one reads as the hole in the middle of a flame.
    private static readonly Color BlueFireTint = new Color(0.16f, 0.28f, 0.72f, 0.8f);
    private const float BlueEmberShare = 0.22f;
    private const float BlueEmberScale = 0.75f;

    private static FireField _instance;
    private static bool _quitting;

    private readonly List<Zone> _zones = new List<Zone>();
    private ParticleSystem _particles;
    private ParticleSystem _blueParticles;
    private float _sinceLastEmit;

    public static FireField Instance
    {
        get
        {
            if (_quitting) return null;
            if (_instance == null)
            {
                var host = new GameObject("FireField");
                _instance = host.AddComponent<FireField>();
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        BuildParticles();
    }

    private void OnApplicationQuit()
    {
        _quitting = true;
    }

    // ---- Where fire is allowed to be ----

    // The playfield the waves are written against, used when there is no orthographic
    // camera to ask. Same numbers as RailNetwork.GetViewBounds.
    private static readonly Vector2 FallbackHalfExtents = new Vector2(10f, 5.625f);

    // Camera.main walks the scene, so it is held. A destroyed camera compares equal to
    // null through Unity's operator, which re-acquires it on the next call — that is
    // what carries this across a scene load.
    private static Camera _viewCamera;

    /// <summary>
    /// Whether a point is inside what the player can actually see.
    ///
    /// Waves spawn OFF screen and walk in, so fire laid outside the view is fire
    /// nobody can see burning things nobody can see. A burning enemy on its way in
    /// would paint the approach, and the next thing to spawn would arrive already
    /// hurt — see EnemyBase.DropFireTrail, which is what asks.
    /// </summary>
    public static bool IsInsideView(Vector2 point)
    {
        if (_viewCamera == null) _viewCamera = Camera.main;

        Vector2 center;
        Vector2 halfExtents;
        if (_viewCamera != null && _viewCamera.orthographic)
        {
            center = _viewCamera.transform.position;
            halfExtents = new Vector2(_viewCamera.orthographicSize * _viewCamera.aspect,
                _viewCamera.orthographicSize);
        }
        else
        {
            center = Vector2.zero;
            halfExtents = FallbackHalfExtents;
        }

        return Mathf.Abs(point.x - center.x) <= halfExtents.x
            && Mathf.Abs(point.y - center.y) <= halfExtents.y;
    }

    // ---- Placing fire ----

    // isTrail only distinguishes Fire Trail drops from craters and zones, for the
    // Ember quest that asks for several burning at once. It changes no behaviour.
    public static void AddZone(Vector2 position, float radius, float duration,
        string ownerTag, float dps, bool eternal, bool isTrail = false,
        bool ignites = false, int sourceEnemyId = 0)
    {
        var field = Instance;
        if (field == null) return;
        field.Add(position, radius, duration, ownerTag, dps, eternal, isTrail,
            ignites, sourceEnemyId);
    }

    private void Add(Vector2 position, float radius, float duration,
        string ownerTag, float dps, bool eternal, bool isTrail,
        bool ignites, int sourceEnemyId)
    {
        // Rule 1. Refused here rather than at the call sites so a fireball crater, a
        // Fire Trail drop and anything added later all obey it without being asked.
        if (!IsInsideView(position)) return;

        var zone = new Zone
        {
            position = position,
            radius = radius,
            expiresAt = eternal ? float.PositiveInfinity : Time.time + duration,
            bornAt = Time.time,
            ownerTag = ownerTag,
            dps = dps,
            isTrail = isTrail,
            ignites = ignites,
            sourceEnemyId = sourceEnemyId
        };

        _zones.Add(zone);

        // Oldest-first retirement once the ceiling is reached
        if (_zones.Count > MaxZones)
        {
            _zones.RemoveRange(0, _zones.Count - MaxZones);
        }

        if (isTrail && ActiveTrailCount >= 4) Feats.Record(Feats.FireTrailsFour);
    }

    /// <summary>Fire Trail zones currently burning, ignoring craters and placed zones.</summary>
    public static int ActiveTrailCount
    {
        get
        {
            if (_instance == null) return 0;
            int n = 0;
            var zones = _instance._zones;
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i].isTrail && Time.time < zones[i].expiresAt) n++;
            }
            return n;
        }
    }

    // Zones die with the wave, not the run — otherwise a Scorched Earth wave 11
    // hands wave 12 a field that is already an inferno and the curve inverts.
    public static void ClearAll()
    {
        if (_instance == null) return;
        _instance._zones.Clear();
        if (_instance._particles != null)
        {
            _instance._particles.Clear();
        }
        if (_instance._blueParticles != null)
        {
            _instance._blueParticles.Clear();
        }
    }

    public static int ActiveZoneCount
    {
        get { return _instance == null ? 0 : _instance._zones.Count; }
    }

    // ---- Sampling (how enemies take fire damage) ----

    // Returns the HOTTEST overlapping zone, never the sum, and never one result per
    // zone. Fire Trail lays zones every 0.35s along a path so they heavily overlap;
    // summing would make a trail deal many times its stated dps and turn "3 dps" into
    // a lie. This is the ONLY way a body learns what the ground is doing to it —
    // damage, kill credit and ignition all come out of this one call.
    //
    // excludeEnemyId skips zones that body dripped itself. Without it a burning enemy
    // is billed twice for one fire — its burn, and the lane that burn is laying — and
    // on any build where ground fire ignites it would keep re-lighting itself off its
    // own trail and never stop burning.
    public static bool Sample(Vector2 position, int excludeEnemyId,
        out float dps, out string ownerTag, out bool ignites)
    {
        dps = 0f;
        ownerTag = null;
        ignites = false;
        if (_instance == null) return false;

        // Rule 2, and the whole of what "trimmed at the view edge" means: a zone is a
        // circle, but the ground it actually burns is that circle intersected with the
        // screen. Asked of the SAMPLING POINT rather than of the zone, so one test
        // clips every zone at once and a crater dropped at the boundary burns exactly
        // the part of itself the player can see.
        if (!IsInsideView(position)) return false;

        return _instance.SampleInternal(position, excludeEnemyId, out dps, out ownerTag, out ignites);
    }

    private bool SampleInternal(Vector2 position, int excludeEnemyId,
        out float dps, out string ownerTag, out bool ignites)
    {
        dps = 0f;
        ownerTag = null;
        ignites = false;
        bool hit = false;

        for (int i = 0; i < _zones.Count; i++)
        {
            Zone zone = _zones[i];
            if (zone.sourceEnemyId != 0 && zone.sourceEnemyId == excludeEnemyId) continue;

            float dx = zone.position.x - position.x;
            float dy = zone.position.y - position.y;
            float hitRadius = zone.radius * HitboxScale;
            if (dx * dx + dy * dy > hitRadius * hitRadius) continue;

            hit = true;

            // Ignition is a property of the GROUND, not of the hottest patch of it:
            // one igniting zone underfoot lights you even if a hotter ordinary zone
            // overlaps it.
            if (zone.ignites) ignites = true;

            if (zone.dps > dps)
            {
                dps = zone.dps;
                ownerTag = zone.ownerTag;
            }
        }

        return hit;
    }

    // ---- Lifetime + rendering ----

    private void Update()
    {
        float now = Time.time;

        for (int i = _zones.Count - 1; i >= 0; i--)
        {
            if (_zones[i].expiresAt <= now)
            {
                _zones.RemoveAt(i);
            }
        }

        _sinceLastEmit += Time.deltaTime;
        if (_sinceLastEmit >= EmitInterval)
        {
            _sinceLastEmit = 0f;
            EmitIntoZones();
        }

        _sinceLastSurvey += Time.deltaTime;
        if (_sinceLastSurvey >= SurveyInterval)
        {
            _sinceLastSurvey = 0f;
            SurveyForQuests(now);
        }
    }

    // How often the two Ember quest records are measured. Deliberately slow: both
    // are "the best it ever got" questions, and a quarter-second sampling cannot
    // miss a fire that was ever big enough or old enough to matter, while a
    // per-frame grid sweep would be a cost a fire build could feel.
    private const float SurveyInterval = 0.25f;
    private float _sinceLastSurvey;

    // Grid resolution for the coverage estimate. The zones overlap constantly —
    // that is the whole design, "overlapping zones are ONE fire" — so their areas
    // cannot simply be summed. Sampling answers the union honestly instead.
    private const int SurveyCols = 40;
    private const int SurveyRows = 24;

    /// <summary>
    /// Records the two things the Ember line asks about the field itself: how long
    /// the oldest single fire has burned, and how much of the arena is alight at
    /// once.
    /// </summary>
    private void SurveyForQuests(float now)
    {
        if (_zones.Count == 0) return;

        // Longest-burning single zone. Measured as it burns rather than on expiry,
        // so a Scorched Earth zone — which never expires — still counts.
        float oldest = 0f;
        for (int i = 0; i < _zones.Count; i++)
        {
            float age = now - _zones[i].bornAt;
            if (age > oldest) oldest = age;
        }
        QuestTally.Peak(OrderStats.FireFieldSecondsMax, Mathf.FloorToInt(oldest));

        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector2 centre = cam.transform.position;

        int lit = 0;
        for (int row = 0; row < SurveyRows; row++)
        {
            float y = centre.y - halfH + (row + 0.5f) * (2f * halfH / SurveyRows);
            for (int col = 0; col < SurveyCols; col++)
            {
                float x = centre.x - halfW + (col + 0.5f) * (2f * halfW / SurveyCols);
                var point = new Vector2(x, y);
                for (int i = 0; i < _zones.Count; i++)
                {
                    float r = _zones[i].radius * HitboxScale;
                    if ((point - _zones[i].position).sqrMagnitude <= r * r) { lit++; break; }
                }
            }
        }

        QuestTally.Peak(OrderStats.FireCoveragePercentMax,
                        Mathf.RoundToInt(100f * lit / (SurveyCols * SurveyRows)));
    }

    // One pooled system emitting into every zone, rather than a system per zone.
    // Two systems now — orange, and a blue one that only igniting zones draw from, so
    // the tell costs nothing on a field of ordinary fire.
    private void EmitIntoZones()
    {
        if (_particles == null || _zones.Count == 0) return;

        // Spread a fixed budget across the field so a hundred zones cost the same
        // as five — density per zone falls off, which reads fine at a glance
        int budget = Mathf.Clamp(_zones.Count, 1, 24);

        var emitParams = new ParticleSystem.EmitParams();

        for (int i = 0; i < budget; i++)
        {
            Zone zone = _zones[Random.Range(0, _zones.Count)];

            // Blue rides in the same budget rather than adding to it: an eternal
            // zone is not denser than an ordinary one, some of its embers are just
            // the wrong colour for a fire, which is the whole read.
            bool blue = float.IsPositiveInfinity(zone.expiresAt)
                && _blueParticles != null
                && Random.value < BlueEmberShare;

            Vector2 offset = Random.insideUnitCircle * zone.radius * 0.85f;
            Vector2 spot = zone.position + offset;

            // Rule 3. Ground that cannot burn must not look like it can, so the part of
            // a straddling zone hanging past the edge is simply not drawn. Costs the
            // occasional emission from the budget, which is the right trade: the
            // alternative is a crater that looks whole and only bites on one side.
            if (!IsInsideView(spot)) continue;

            emitParams.position = new Vector3(spot.x, spot.y, 0f);
            emitParams.applyShapeToPosition = false;
            emitParams.startSize = Random.Range(0.18f, 0.34f) * Mathf.Clamp(zone.radius, 0.5f, 2f);
            if (blue) emitParams.startSize *= BlueEmberScale;
            emitParams.startLifetime = ParticleLifetime * Random.Range(0.7f, 1.1f);

            if (blue) _blueParticles.Emit(emitParams, 1);
            else _particles.Emit(emitParams, 1);
        }
    }

    // Code-built, matching PoisonCloud's approach: sprite texture on the default
    // sprite shader, world space, shrink + fade over lifetime.
    //
    // Two systems, because Unity allows one ParticleSystem per GameObject and the
    // blue embers need their own colour ramp — tinting the orange gradient by a blue
    // start colour multiplies down to mud rather than reading as blue flame. The
    // second lives on a child and is otherwise identical.
    private void BuildParticles()
    {
        _particles = BuildSystem(gameObject, FireTint, OrangeGradient());

        var blueHost = new GameObject("BlueEmbers");
        blueHost.transform.SetParent(transform, false);
        _blueParticles = BuildSystem(blueHost, BlueFireTint, BlueGradient());
    }

    // Hot core to cooling ash
    private static Gradient OrangeGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.92f, 0.55f), 0f),
                new GradientColorKey(new Color(1f, 0.45f, 0.12f), 0.45f),
                new GradientColorKey(new Color(0.45f, 0.12f, 0.05f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.75f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        return gradient;
    }

    // The same shape several bands colder and much darker: no white core at all, a
    // deep cobalt body, and near-black navy ash. Alpha runs under the orange ramp so
    // blue sits INSIDE the fire rather than on top of it — noticeable, not the thing
    // you look at first.
    private static Gradient BlueGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.30f, 0.48f, 0.90f), 0f),
                new GradientColorKey(new Color(0.10f, 0.20f, 0.66f), 0.45f),
                new GradientColorKey(new Color(0.02f, 0.03f, 0.20f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.85f, 0f),
                new GradientAlphaKey(0.6f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        return gradient;
    }

    private ParticleSystem BuildSystem(GameObject host, Color tint, Gradient ramp)
    {
        var system = host.AddComponent<ParticleSystem>();

        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = tint;
        main.maxParticles = MaxParticles;
        main.playOnAwake = false;
        main.startSpeed = 0f;
        main.startLifetime = ParticleLifetime;

        var emission = system.emission;
        emission.rateOverTime = 0f; // everything is emitted manually

        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0.25f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Embers rise as they fade
        var velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = ramp;

        var renderer = system.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            var material = new Material(Shader.Find("Sprites/Default"));
            Sprite mote = FireResourceManager.Instance != null
                ? FireResourceManager.Instance.GetEmberMoteSprite()
                : null;
            // Fall back to the poison puff (also white and code-tinted) so fire
            // still renders before the dedicated art is wired
            if (mote == null && PoisonResourceManager.Instance != null)
            {
                mote = PoisonResourceManager.Instance.GetPoisonPuffSprite();
            }
            if (mote != null)
            {
                material.mainTexture = mote.texture;
            }
            renderer.material = material;
            renderer.sortingLayerName = "Default";
        }

        system.Play();
        return system;
    }
}
