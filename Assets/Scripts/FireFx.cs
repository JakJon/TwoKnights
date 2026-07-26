using UnityEngine;

// Code-built Ember visuals that aren't the persistent fire field itself:
// the fireball detonation pop, and the trailing embers on an ignited arrow.
// Mirrors ShadowFx / PoisonCloud.SpawnBurstEffect — no prefab, no art beyond the
// shared white ember mote.
public static class FireFx
{
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

    // One-shot radial detonation for fireball impacts
    public static void Burst(Vector2 position, float radius)
    {
        var host = new GameObject("FireBurst");
        host.transform.position = position;

        var burst = Build(host, 0.55f);

        var main = burst.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 2.2f, radius * 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.42f);

        var shape = burst.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * 0.2f;

        burst.Play();
        burst.Emit(Mathf.RoundToInt(18f + radius * 10f));
        Object.Destroy(host, 1.5f);
    }

    // Ember trail on an arrow that rolled ignite, so the player can read which
    // shots are live before they land — the role PoisonProjectile's bubbles play
    // for Serpent. Parented to the arrow; detaches and fades when it's destroyed.
    public static ParticleSystem AttachArrowTrail(GameObject arrow)
    {
        var host = new GameObject("EmberTrail");
        host.transform.SetParent(arrow.transform);
        host.transform.localPosition = Vector3.zero;

        var trail = Build(host, 0.5f);

        var main = trail.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.36f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);

        var emission = trail.emission;
        emission.rateOverTime = 34f;

        var shape = trail.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.06f;

        trail.Play();
        return trail;
    }

    // Persistent flame riding an ignited enemy, so a burning enemy reads as on
    // fire rather than merely tinted. Parented to the enemy; EnemyBase enables and
    // disables emission as ignition comes and goes, and it dies with the enemy.
    // Local simulation space so the flame tracks the enemy as it runs.
    public static ParticleSystem AttachEnemyFlame(GameObject enemy)
    {
        var host = new GameObject("EmberFlame");
        host.transform.SetParent(enemy.transform);

        // Size the flame to the enemy's sprite, in WORLD units. The flame stays
        // anchored at the sprite's BASE — the same spot the original point flame
        // sat — and only grows: the emission band stretches across the full sprite
        // width and rises a modest slice into the body; the upward velocity below
        // does the climbing, not the emitter position.
        float width = 1f;
        float height = 1f;
        Vector3 baseCenter = enemy.transform.position;
        var sr = enemy.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            Bounds b = sr.bounds; // world-space AABB — no scale back-out; see scalingMode below
            width = b.size.x;
            height = b.size.y;
            baseCenter = new Vector3(b.center.x, b.min.y, 0f);
        }
        // The band's bottom sits exactly on the sprite's base, so nothing shifts
        // vertically — the host center is just half the band's own height up
        float bandHeight = height * 0.25f;
        host.transform.position = baseCenter + new Vector3(0f, bandHeight * 0.5f, 0f);

        var flame = Build(host, 0.85f);

        var main = flame.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        // SetParent kept the host's WORLD scale at one, so Hierarchy scaling makes
        // the box below world-sized even on scaled enemies (slime size variants).
        // The default Local mode used the host's compensated localScale and shrank
        // the band on exactly those enemies — the width never covered their base.
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.55f);
        // Low start speed: a Box shape would otherwise fling particles into screen
        // depth. The upward velocityOverLifetime below is what makes flames rise.
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.15f);
        // Particle size scales gently with the enemy so a big body gets big flames
        float sizeScale = Mathf.Clamp(Mathf.Max(width, height), 0.6f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f * sizeScale, 0.4f * sizeScale);

        var emission = flame.emission;
        emission.rateOverTime = 26f * Mathf.Clamp(width, 0.6f, 2.5f);

        // A flat box spanning the full sprite width, a shallow band tall, so flames
        // rise from across the whole base rather than from a single point
        var shape = flame.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(width, bandHeight, 0.01f);
        shape.radius = 0.01f;

        // Flames lick upward
        var velocity = flame.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var renderer = flame.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 20; // draw over the enemy sprite
        }

        flame.Play();
        return flame;
    }

    // Shared builder: white mote on the default sprite shader, world space,
    // hot core cooling to ash, shrinking as it fades
    private static ParticleSystem Build(GameObject host, float startAlpha)
    {
        var system = host.AddComponent<ParticleSystem>();

        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(1f, 0.55f, 0.18f, startAlpha);
        main.maxParticles = 120;
        main.playOnAwake = false;

        var emission = system.emission;
        emission.rateOverTime = 0f;

        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0.2f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.93f, 0.6f), 0f),
                new GradientColorKey(new Color(1f, 0.45f, 0.12f), 0.5f),
                new GradientColorKey(new Color(0.4f, 0.1f, 0.04f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.8f, 0.5f),
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
        }

        return system;
    }
}
