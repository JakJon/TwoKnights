using System.Collections.Generic;
using UnityEngine;

// Code-built Frigid visuals, mirroring FireFx / ShadowFx / DawnFx: no prefabs,
// and no new art. Everything here is the shared white EmberMote alpha mask tinted
// cyan - that sprite was authored as a code-tintable mask, so this is its intended
// reuse rather than a restyle of Ember's art.
//
// Fire licks upward and cools to soot; Dawn rises and fades to white. Frost does
// neither - it DRIFTS and settles, barely moving, and dies pale. That contrast is
// the whole reason the three Orders do not read alike on screen.
public static class FrostFx
{
    private static readonly Color Cyan = new Color(0.47f, 0.80f, 0.92f);
    private static readonly Color PaleIce = new Color(0.85f, 0.96f, 1f);

    // The ice shell riding a frozen body, kept per-enemy so a second freeze does
    // not stack a second shell on the same thing.
    private static readonly Dictionary<GameObject, GameObject> Shells =
        new Dictionary<GameObject, GameObject>();

    private static Sprite MoteSprite
    {
        get
        {
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

    /// <summary>The cold trail on an arrow carrying frost, so the player can read
    /// which shots are live before they land - the role PoisonProjectile's bubbles
    /// play for Serpent and the ember trail plays for Ember.</summary>
    public static ParticleSystem AttachArrowTrail(GameObject arrow)
    {
        var host = new GameObject("FrostTrail");
        host.transform.SetParent(arrow.transform);
        host.transform.localPosition = Vector3.zero;

        var trail = Build(host, 0.5f);

        var main = trail.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.45f);
        // Barely any speed of its own: frost hangs in the air where it was left
        // rather than streaming off the way embers do.
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.18f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);

        var emission = trail.emission;
        emission.rateOverTime = 30f;

        var shape = trail.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.06f;

        trail.Play();
        return trail;
    }

    /// <summary>Slow motes drifting off a chilled body. EnemyBase enables and
    /// disables emission as the chill comes and goes, exactly as it does with
    /// Ember's flame, and it dies with the enemy.</summary>
    public static ParticleSystem AttachEnemyChill(GameObject enemy)
    {
        var host = new GameObject("FrostChill");
        host.transform.SetParent(enemy.transform);

        float width = 1f;
        float height = 1f;
        Vector3 center = enemy.transform.position;
        var sr = enemy.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            Bounds b = sr.bounds; // world-space AABB - see scalingMode below
            width = b.size.x;
            height = b.size.y;
            center = b.center;
        }
        host.transform.position = center;

        var chill = Build(host, 0.7f);

        var main = chill.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        // SetParent kept the host's WORLD scale at one, so Hierarchy scaling keeps
        // the box world-sized on scaled enemies (the slime size variants). This is
        // the lesson FireFx learned the hard way; do not switch it to Local.
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.08f);
        float sizeScale = Mathf.Clamp(Mathf.Max(width, height), 0.6f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f * sizeScale, 0.2f * sizeScale);

        var emission = chill.emission;
        emission.rateOverTime = 12f * Mathf.Clamp(width, 0.6f, 2.5f);

        var shape = chill.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(width * 0.9f, height * 0.8f, 0.01f);
        shape.radius = 0.01f;

        // Cold SINKS. The one line that stops this reading as blue fire.
        var velocity = chill.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.35f, -0.1f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var renderer = chill.GetComponent<ParticleSystemRenderer>();
        if (renderer != null) renderer.sortingOrder = 20;

        chill.Play();
        return chill;
    }

    /// <summary>The shell on a body held in ice. Separate from the chill motes
    /// because the two states have to be told apart across a crowded board at a
    /// glance: chilled drifts, frozen is still and bright.</summary>
    public static void ShowIce(GameObject enemy)
    {
        if (enemy == null) return;
        GameObject existing;
        if (Shells.TryGetValue(enemy, out existing) && existing != null) return;

        var host = new GameObject("FrostShell");
        host.transform.SetParent(enemy.transform);

        float width = 1f;
        float height = 1f;
        Vector3 center = enemy.transform.position;
        var sr = enemy.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            Bounds b = sr.bounds;
            width = b.size.x;
            height = b.size.y;
            center = b.center;
        }
        host.transform.position = center;

        var shell = Build(host, 0.95f);

        var main = shell.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startColor = new Color(PaleIce.r, PaleIce.g, PaleIce.b, 0.95f);
        // Long-lived and motionless: the shell is meant to sit there looking solid.
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.02f);
        float sizeScale = Mathf.Clamp(Mathf.Max(width, height), 0.6f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f * sizeScale, 0.3f * sizeScale);

        var emission = shell.emission;
        emission.rateOverTime = 20f * Mathf.Clamp(width, 0.6f, 2.5f);

        // Rim only, so the body underneath stays readable inside its own ice
        var shape = shell.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = Mathf.Max(width, height) * 0.5f;
        shape.radiusThickness = 0.25f;

        var renderer = shell.GetComponent<ParticleSystemRenderer>();
        if (renderer != null) renderer.sortingOrder = 21;

        shell.Play();
        Shells[enemy] = host;
    }

    public static void HideIce(GameObject enemy)
    {
        if (enemy == null) return;
        GameObject host;
        if (!Shells.TryGetValue(enemy, out host)) return;
        Shells.Remove(enemy);
        if (host == null) return;

        // Detach and let the last particles die rather than popping the shell out
        // of existence on the frame the ice broke.
        host.transform.SetParent(null);
        var system = host.GetComponent<ParticleSystem>();
        if (system != null)
        {
            var emission = system.emission;
            emission.enabled = false;
            system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        Object.Destroy(host, 1.4f);
    }

    /// <summary>Ice going everywhere: a shatter, or the burst a Rimeblade swing
    /// throws off. One-shot, self-destructing. Returns the system so a caller
    /// running under a frozen timeScale can put it on unscaled time; every
    /// gameplay caller ignores it.</summary>
    public static ParticleSystem Burst(Vector2 position, float radius)
    {
        var host = new GameObject("FrostBurst");
        host.transform.position = position;

        var burst = Build(host, 0.7f);

        var main = burst.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 1.6f, radius * 3.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.32f);

        var shape = burst.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * 0.15f;

        burst.Play();
        burst.Emit(Mathf.RoundToInt(16f + radius * 9f));
        Object.Destroy(host, 1.5f);
        return burst;
    }

    // The ring's far end: Cyan one step colder, so the edge reads plainly blue
    // against the motes rather than as more of the same pale spray
    private static readonly Color RingBlue = new Color(0.35f, 0.62f, 1f);

    /// <summary>The edge of a Rimeblade burst, drawn exactly like the circle every
    /// explosion draws (see BlastRing) but in ice. The motes from Burst fly well
    /// past the damage radius; this is the line that says where it actually
    /// stopped. Self-destructing.</summary>
    public static BlastRing BurstRing(Vector2 position, float radius)
    {
        return BlastRing.Spawn(position, radius, PaleIce, RingBlue);
    }

    /// <summary>The standing ring of cold around a knight running Glacial Ward.
    /// Parented to the knight, so it rides them and dies with them.</summary>
    public static ParticleSystem AttachWardRing(GameObject knight, float radius, Vector3 localCenter)
    {
        var host = new GameObject("GlacialWard");
        host.transform.SetParent(knight.transform);
        // NOT the knight's transform, which sits at their FEET. A ring hung there
        // reads as sagging below them - most of it is under the boots and only the
        // top arc crosses the body. Lifted to the middle of the knight instead, and
        // GlacialWard sweeps from the same point so the cold is where it looks.
        host.transform.localPosition = localCenter;

        var ring = Build(host, 0.4f);

        var main = ring.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.05f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
        main.maxParticles = 200;

        var emission = ring.emission;
        emission.rateOverTime = 26f;

        // Rim only. The ward's whole job is telling the player where its EDGE is,
        // and a filled disc of motes would bury the knight and the enemies both.
        var shape = ring.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 0.08f;

        var velocity = ring.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.2f, -0.05f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var renderer = ring.GetComponent<ParticleSystemRenderer>();
        if (renderer != null) renderer.sortingOrder = 5; // under the actors

        ring.Play();
        return ring;
    }

    /// <summary>Resize a ward already on the field, for the knight who takes rank
    /// two mid-run. Cheaper and less fiddly than tearing the emitter down.</summary>
    public static void SetWardRadius(ParticleSystem ring, float radius)
    {
        if (ring == null) return;
        var shape = ring.shape;
        shape.radius = radius;
    }

    // Shared builder: white mote on the default sprite shader, world space,
    // pale core cooling to a colder blue and fading out. Deliberately the same
    // shape as FireFx.Build so the two stay comparable when either is tuned.
    private static ParticleSystem Build(GameObject host, float startAlpha)
    {
        var system = host.AddComponent<ParticleSystem>();

        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(Cyan.r, Cyan.g, Cyan.b, startAlpha);
        main.maxParticles = 120;
        main.playOnAwake = false;

        var emission = system.emission;
        emission.rateOverTime = 0f;

        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0.25f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(PaleIce, 0f),
                new GradientColorKey(Cyan, 0.5f),
                new GradientColorKey(new Color(0.24f, 0.45f, 0.62f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
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
            if (mote != null) material.mainTexture = mote.texture;
            renderer.material = material;
        }

        return system;
    }
}
