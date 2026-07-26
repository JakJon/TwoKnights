using UnityEngine;
using System.Collections.Generic;

// The Ember Order's one primitive: burning ground.
//
// Every Ember upgrade places fire zones — fireball craters, sword-hurled
// firebrands, and the trails ignited enemies drip behind them. A zone deals flat
// damage to anything standing in it and NOTHING ELSE.
//
// THE IGNITION PILLAR, ENFORCED BY CONSTRUCTION: this class holds no reference to
// EnemyBase and has no way to reach one. Enemies SAMPLE the field (see
// EnemyBase.LateUpdate) rather than the field damaging enemies, so there is
// physically no code path from burning ground to Ignite(). Only a fireball or an
// arrow that rolled ignite can light something. Keep it that way — the whole
// Order falls apart if fire can start fire (see Docs/Design/ember-order.md).
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
        public string ownerTag;
        public float dps;
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

    private static FireField _instance;
    private static bool _quitting;

    private readonly List<Zone> _zones = new List<Zone>();
    private ParticleSystem _particles;
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

    // ---- Placing fire ----

    public static void AddZone(Vector2 position, float radius, float duration,
        string ownerTag, float dps, bool eternal)
    {
        var field = Instance;
        if (field == null) return;
        field.Add(position, radius, duration, ownerTag, dps, eternal);
    }

    private void Add(Vector2 position, float radius, float duration,
        string ownerTag, float dps, bool eternal)
    {
        var zone = new Zone
        {
            position = position,
            radius = radius,
            expiresAt = eternal ? float.PositiveInfinity : Time.time + duration,
            ownerTag = ownerTag,
            dps = dps
        };

        _zones.Add(zone);

        // Oldest-first retirement once the ceiling is reached
        if (_zones.Count > MaxZones)
        {
            _zones.RemoveRange(0, _zones.Count - MaxZones);
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
    }

    public static int ActiveZoneCount
    {
        get { return _instance == null ? 0 : _instance._zones.Count; }
    }

    // ---- Sampling (how enemies take fire damage) ----

    // Returns the HOTTEST overlapping zone, not the sum. Fire Trail lays zones
    // every 0.35s along a path so they heavily overlap; summing would make a
    // trail deal many times its stated dps and turn "1 dps" into a lie.
    public static bool Sample(Vector2 position, out float dps, out string ownerTag)
    {
        dps = 0f;
        ownerTag = null;
        if (_instance == null) return false;
        return _instance.SampleInternal(position, out dps, out ownerTag);
    }

    private bool SampleInternal(Vector2 position, out float dps, out string ownerTag)
    {
        dps = 0f;
        ownerTag = null;
        bool hit = false;

        for (int i = 0; i < _zones.Count; i++)
        {
            Zone zone = _zones[i];
            float dx = zone.position.x - position.x;
            float dy = zone.position.y - position.y;
            float hitRadius = zone.radius * HitboxScale;
            if (dx * dx + dy * dy > hitRadius * hitRadius) continue;

            hit = true;
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
    }

    // One pooled system emitting into every zone, rather than a system per zone
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

            Vector2 offset = Random.insideUnitCircle * zone.radius * 0.85f;
            emitParams.position = new Vector3(
                zone.position.x + offset.x,
                zone.position.y + offset.y,
                0f);
            emitParams.applyShapeToPosition = false;
            emitParams.startSize = Random.Range(0.18f, 0.34f) * Mathf.Clamp(zone.radius, 0.5f, 2f);
            emitParams.startLifetime = ParticleLifetime * Random.Range(0.7f, 1.1f);

            _particles.Emit(emitParams, 1);
        }
    }

    // Code-built, matching PoisonCloud's approach: sprite texture on the default
    // sprite shader, world space, shrink + fade over lifetime
    private void BuildParticles()
    {
        _particles = gameObject.AddComponent<ParticleSystem>();

        var main = _particles.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = FireTint;
        main.maxParticles = MaxParticles;
        main.playOnAwake = false;
        main.startSpeed = 0f;
        main.startLifetime = ParticleLifetime;

        var emission = _particles.emission;
        emission.rateOverTime = 0f; // everything is emitted manually

        var sizeOverLifetime = _particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0.25f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Embers rise as they fade
        var velocity = _particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // Hot core to cooling ash
        var colorOverLifetime = _particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
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
        colorOverLifetime.color = gradient;

        var renderer = _particles.GetComponent<ParticleSystemRenderer>();
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

        _particles.Play();
    }
}
