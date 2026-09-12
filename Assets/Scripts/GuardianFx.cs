using UnityEngine;

// The Guardian Order's tells. Code-built, no prefab and no art of its own — the same
// arrangement as FireFx, FrostFx and ShadowFx, and it borrows their mote sprite.
//
// Steel over the shield's gold trim, which are the Order's two existing colours: the
// steel is the swatch the upgrade cards already use (UpgradeMenu.OrderColor) and the
// gold is the band painted down the middle of the shield itself (ShieldShape).
//
// THE STEEL MEANS ONE THING: "this shot is homing". Not "this was reflected" — the
// trails are attached by GuidedShot when a shot commits to a target, and by nothing
// else, so a rebound from a knight without Guided Reflections wears none of this
// (owner's call, 2026-09-08). An arrow that has committed looks exactly like an arrow
// otherwise, and that is the whole job here; neither effect is decoration and neither
// should grow into a light show.
//
// The two arrow/rock trails differ only in scale — see AttachReflectTrail.
public static class GuardianFx
{
    private static readonly Color Steel = new Color(158f / 255f, 168f / 255f, 178f / 255f);
    private static readonly Color Trim = new Color(212f / 255f, 162f / 255f, 74f / 255f);
    private static readonly Color Dim = new Color(0.35f, 0.40f, 0.46f);

    // Shared with the other Fx classes rather than owned here — there is one mote in
    // the game and every Order tints it.
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

    /// <summary>The steel wrapped round a reflected rock that has found a target.
    ///
    /// NOT shown at the moment of the block — the steel means "this is homing", never
    /// "this was reflected" (owner's call, 2026-09-08), so GuidedShot is the only
    /// caller and a rebound with no Guided Reflections wears nothing.
    ///
    /// Sized UP against the host, because the rock prefab draws at a quarter scale and
    /// parented art inherits the shrink — the same correction ProjectileSettings.SizeTrail
    /// has to make for the powder rock's flame.</summary>
    public static ParticleSystem AttachReflectTrail(GameObject rock)
    {
        var host = new GameObject("GuardianReflect");
        host.transform.SetParent(rock.transform);
        host.transform.localPosition = Vector3.zero;
        host.transform.localScale = Vector3.one * 3f;

        var trail = Build(host, 0.75f);

        var main = trail.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.32f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.13f);

        var emission = trail.emission;
        emission.rateOverTime = 34f;

        var shape = trail.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.05f;

        trail.Play();
        return trail;
    }

    /// <summary>The steel on an arrow that has committed to a target. Quieter than the
    /// reflect trail on purpose: a guided knight's every shot wears this, so it has to
    /// sit under the fight rather than announce itself once a second.</summary>
    public static ParticleSystem AttachGuidedTrail(GameObject arrow)
    {
        var host = new GameObject("GuardianGuided");
        host.transform.SetParent(arrow.transform);
        host.transform.localPosition = Vector3.zero;

        var trail = Build(host, 0.5f);

        var main = trail.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f);

        var emission = trail.emission;
        emission.rateOverTime = 22f;

        var shape = trail.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.05f;

        trail.Play();
        return trail;
    }

    /// <summary>A body thrown off the guard by Bulwark. One short outward puff at the
    /// point of contact — it marks the shove, and then it is gone; the knockback itself
    /// is the thing the player is meant to be watching.</summary>
    public static void ShoveBurst(Vector2 position, Vector2 direction)
    {
        var host = new GameObject("GuardianShove");
        host.transform.position = position;
        float degrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        host.transform.rotation = Quaternion.AngleAxis(degrees, Vector3.forward);

        var burst = Build(host, 0.9f);

        var main = burst.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.36f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);

        var emission = burst.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });

        // A cone along the shove, not a sphere: the puff should say which way the
        // body went, which is the one piece of information a knockback has to carry.
        var shape = burst.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 28f;
        shape.radius = 0.08f;
        shape.rotation = new Vector3(0f, 90f, 0f); // cone down local +X

        burst.Play();
        Object.Destroy(host, 1f);
    }

    // Shared builder: the mote on the default sprite shader, world space, steel
    // warming through the shield's trim and fading out. Deliberately the same shape
    // as FrostFx.Build and FireFx.Build so all three stay comparable when any is tuned.
    private static ParticleSystem Build(GameObject host, float startAlpha)
    {
        var system = host.AddComponent<ParticleSystem>();

        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(Steel.r, Steel.g, Steel.b, startAlpha);
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
                new GradientColorKey(Trim, 0f),
                new GradientColorKey(Steel, 0.45f),
                new GradientColorKey(Dim, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.7f, 0.5f),
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
