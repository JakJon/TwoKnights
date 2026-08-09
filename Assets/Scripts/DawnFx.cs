using UnityEngine;

// Code-built Dawn visuals: the holy light. Mirrors FireFx / ShadowFx — no
// prefab and no new art, just the shared white ember mote tinted to the Order's
// gold, so Dawn reads as light rather than as fire.
//
// Three surfaces, one look:
//   Blessing  — a one-shot ring of motes ASCENDING around a knight, fired
//               whenever a Dawn knight receives healing.
//   Aura      — a sustained halo for as long as a knight is untouchable.
//   OrbGlow   — a slowed orb emanating light, so Sunwell III is visible on the
//               object it changed rather than only in the numbers.
//
// Everything rises. Fire licks up and dies dark (FireFx cools to ash); Dawn
// rises and dies BRIGHT, fading to white rather than to soot. That contrast is
// the whole reason the two Orders don't look alike despite sharing a sprite.
public static class DawnFx
{
    // The Order's accent, matching order--dawn in UpgradeMenu.uss
    private static readonly Color Gold = new Color(240f / 255f, 200f / 255f, 170f / 255f);
    private static readonly Color HolyWhite = new Color(1f, 0.98f, 0.90f);

    private const string AuraChildName = "DawnAura";
    private const string OrbGlowChildName = "DawnOrbGlow";

    private static Sprite MoteSprite
    {
        get
        {
            // The ember mote is a white alpha mask authored for exactly this —
            // code-tinting, not a fire-specific sprite. Poison puff is the
            // fallback, same as FireFx uses.
            Sprite mote = FireResourceManager.Instance != null
                ? FireResourceManager.Instance.GetEmberMoteSprite()
                : null;
            if (mote == null && PoisonResourceManager.Instance != null)
            {
                mote = PoisonResourceManager.Instance.GetPoisonPuffSprite();
            }
            return mote;
        }
    }

    /// <summary>
    /// One-shot ring of rising motes around a knight — the Dawn "something good
    /// just happened to you" beat. Fired from PlayerHealth whenever a knight
    /// carrying any Dawn upgrade is healed, so no individual Dawn effect has to
    /// remember to ask for it.
    /// </summary>
    public static void Blessing(GameObject knight)
    {
        if (knight == null) return;

        var host = new GameObject("DawnBlessing");
        host.transform.position = CenterOf(knight);

        var system = Build(host, 0.95f);

        var main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
        // Barely any outward push — the rise below is what carries them
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.30f);

        // Emitted from the RIM of a circle, so the light reads as a halo forming
        // around the knight rather than a puff at their middle
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = RadiusOf(knight);
        shape.radiusThickness = 0f;

        var velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        system.Play();
        system.Emit(22);
        Object.Destroy(host, 1.5f);
    }

    /// <summary>
    /// A halo held for as long as the knight is untouchable. Hooked into
    /// PlayerHealth.SetInvulnerable, which is the game's single untouchable
    /// funnel — so Second Wind, Benediction II, Last Light and Iron Vigil all
    /// look the same, because to the player they ARE the same thing.
    ///
    /// Replaces any aura already running rather than stacking: overlapping
    /// windows EXTEND the deadline, so the caller passes the new remaining time
    /// and gets one halo of the right length.
    /// </summary>
    public static void ShowInvulnerableAura(GameObject knight, float seconds)
    {
        if (knight == null || seconds <= 0f) return;

        ClearChild(knight, AuraChildName);

        var host = new GameObject(AuraChildName);
        host.transform.SetParent(knight.transform);
        host.transform.position = CenterOf(knight);

        var system = Build(host, 0.7f);

        var main = system.main;
        // Local space so the halo travels with the knight; Hierarchy scaling so
        // it stays world-sized whatever the knight's transform scale is (the
        // lesson FireFx learned on scaled slimes)
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        // Emit for exactly the window, then let the last motes live out and stop
        main.duration = seconds;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.22f);

        var emission = system.emission;
        emission.rateOverTime = 30f;

        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = RadiusOf(knight);
        shape.radiusThickness = 0f;

        var velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        system.Play();
        Object.Destroy(host, seconds + 1.2f);
    }

    /// <summary>
    /// Sunwell III: a slowed orb emanates light. Parented to the orb, so it dies
    /// with it on collection or exit — the orb is the thing that changed, so the
    /// orb is what has to look different.
    /// </summary>
    public static void AttachOrbGlow(GameObject orb)
    {
        if (orb == null) return;

        ClearChild(orb, OrbGlowChildName);

        var host = new GameObject(OrbGlowChildName);
        host.transform.SetParent(orb.transform);
        host.transform.localPosition = Vector3.zero;

        var system = Build(host, 0.8f);

        var main = system.main;
        // World space: the motes should hang in the air as the orb drifts past,
        // leaving a wake of light rather than a rigid halo travelling with it
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.20f);

        var emission = system.emission;
        emission.rateOverTime = 26f;

        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.22f;
        shape.radiusThickness = 1f; // fill, not rim — the orb IS the light source

        var velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        system.Play();
    }

    // ---- shared ----

    private static Vector3 CenterOf(GameObject target)
    {
        var sr = target.GetComponentInChildren<SpriteRenderer>();
        return sr != null ? sr.bounds.center : target.transform.position;
    }

    // Halo radius from the sprite, so a knight and anything larger both get a
    // ring that sits just off the body instead of buried inside it
    private static float RadiusOf(GameObject target)
    {
        var sr = target.GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return 0.5f;
        Bounds b = sr.bounds;
        return Mathf.Clamp(Mathf.Max(b.size.x, b.size.y) * 0.55f, 0.35f, 2f);
    }

    private static void ClearChild(GameObject parent, string childName)
    {
        Transform existing = parent.transform.Find(childName);
        if (existing != null) Object.Destroy(existing.gameObject);
    }

    // Shared builder: white mote on the default sprite shader, holy-white core
    // warming to the Order's gold and fading out BRIGHT — the deliberate inverse
    // of FireFx, which cools to ash.
    private static ParticleSystem Build(GameObject host, float startAlpha)
    {
        var system = host.AddComponent<ParticleSystem>();

        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(Gold.r, Gold.g, Gold.b, startAlpha);
        main.maxParticles = 120;
        main.playOnAwake = false;

        var emission = system.emission;
        emission.rateOverTime = 0f;

        // Motes swell slightly then thin away, so the light feels like it is
        // being given off rather than thrown
        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.6f);
        sizeCurve.AddKey(0.35f, 1f);
        sizeCurve.AddKey(1f, 0.15f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(HolyWhite, 0f),
                new GradientColorKey(Gold, 0.55f),
                new GradientColorKey(HolyWhite, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.75f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        var renderer = system.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            var material = new Material(Shader.Find("Sprites/Default"));
            Sprite mote = MoteSprite;
            if (mote != null)
            {
                material.mainTexture = mote.texture;
            }
            renderer.material = material;
            renderer.sortingOrder = 20; // over the knight / orb sprite
        }

        return system;
    }
}
