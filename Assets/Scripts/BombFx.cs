using UnityEngine;

// The gnome bomb's detonation. Code-built, no prefab and no art of its own —
// the same arrangement as FireFx and ShadowFx, reusing the shared white mote.
//
// It is deliberately not a fireball pop. Ember's burst is a warm orange bloom
// that means "this is on fire and will keep burning"; a bomb is a hard white
// flash that is over instantly, followed by grey smoke that drifts and dies. The
// player has to be able to tell at a glance which one just went off, because one
// of them leaves a burning crater and the other leaves nothing.
public static class BombFx
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

    /// <summary>
    /// One-shot blast at <paramref name="position"/>. <paramref name="radius"/> is
    /// the bomb's real damage radius, so what the player sees is what actually
    /// hurt — the flash reaches the edge of the kill zone and no further.
    /// </summary>
    public static void Explode(Vector2 position, float radius)
    {
        var host = new GameObject("BombBlast");
        host.transform.position = position;

        SpawnFlash(host.transform, radius);
        SpawnSmoke(host.transform, radius);

        Object.Destroy(host, 2.5f);
    }

    // The hit itself: a dense sphere of fast white-hot shrapnel that reaches the
    // blast edge inside a fifth of a second and is gone
    private static void SpawnFlash(Transform parent, float radius)
    {
        var host = new GameObject("Flash");
        host.transform.SetParent(parent, false);

        var flash = Build(host);

        var main = flash.main;
        main.startColor = new Color(1f, 0.95f, 0.75f, 0.95f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
        // Sized off the radius so a retuned blast stays honest: the fastest motes
        // reach the damage edge just as they expire
        main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 4f, radius * 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.34f);

        var colorOverLifetime = flash.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 0.92f), 0f),
                new GradientColorKey(new Color(1f, 0.78f, 0.35f), 0.35f),
                new GradientColorKey(new Color(0.55f, 0.35f, 0.22f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.85f, 0.4f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        var shape = flash.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * 0.12f;

        flash.Play();
        flash.Emit(Mathf.RoundToInt(26f + radius * 12f));
    }

    // The aftermath: fewer, fatter, slower motes that swell as they grey out and
    // lift, so the blast leaves a mark on the frame for a beat afterwards
    private static void SpawnSmoke(Transform parent, float radius)
    {
        var host = new GameObject("Smoke");
        host.transform.SetParent(parent, false);

        var smoke = Build(host);

        var main = smoke.main;
        main.startColor = new Color(0.62f, 0.6f, 0.58f, 0.7f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.4f, radius * 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);

        // Smoke grows instead of shrinking — the opposite of the flash, and of
        // every Ember effect, which is most of what sells it as smoke
        var sizeOverLifetime = smoke.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.6f);
        sizeCurve.AddKey(1f, 1.5f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = smoke.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.5f, 0.48f, 0.46f), 0f),
                new GradientColorKey(new Color(0.34f, 0.33f, 0.33f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.65f, 0.15f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        // All three axes have to be set to the same curve MODE or the particle
        // system logs "Velocity curves must all be in the same mode" every blast —
        // setting y alone leaves x and z as plain constants
        var velocity = smoke.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var shape = smoke.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * 0.3f;

        smoke.Play();
        smoke.Emit(Mathf.RoundToInt(10f + radius * 5f));
    }

    // Shared shell: world space so the blast stays where it went off, emission
    // driven entirely by the explicit Emit calls above
    private static ParticleSystem Build(GameObject host)
    {
        var system = host.AddComponent<ParticleSystem>();

        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
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
            renderer.sortingOrder = 25; // over the knights and the track alike
        }

        return system;
    }
}
