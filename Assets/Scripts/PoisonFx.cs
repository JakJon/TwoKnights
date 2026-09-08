using UnityEngine;

// The Serpent's shield tell, and deliberately the same promise the Ember and
// Shadow Orders already make: while the loaded shot is the vial, the shield
// steams. Fireball smoulders, the sleeping dart drifts Zs, and this bubbles.
//
// The tell exists because the cadence is a COUNTER, not a roll. "Every fifth
// shot" is only worth building a fight around if the player can act on it, and
// asking them to count arrows through a wave is asking them to look away from
// the wave. The shield says so instead.
public static class PoisonFx
{
    private const string ChargeName = "VialCharge";

    /// <summary>
    /// Hangs the vial tell on the shield and hands back the system, so the caller
    /// can turn its emission on and off with the cadence rather than rebuilding it
    /// every shot. Mirrors FireFx.AttachShieldCharge exactly — same ring, same
    /// sizing off the shield sprite, different colour and a lazier rise, because
    /// venom seeps where fire climbs.
    /// </summary>
    public static ParticleSystem AttachShieldCharge(GameObject shield)
    {
        if (shield == null) return null;

        Transform existing = shield.transform.Find(ChargeName);
        if (existing != null) return existing.GetComponent<ParticleSystem>();

        var host = new GameObject(ChargeName);
        host.transform.SetParent(shield.transform);
        host.transform.localPosition = Vector3.zero;
        host.transform.localRotation = Quaternion.identity;

        // Ring sized to the shield's own sprite, in WORLD units — the shield grows
        // with Shield Shape upgrades and the tell has to grow with it
        float radius = 0.35f;
        var sr = shield.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            radius = Mathf.Max(sr.bounds.extents.x, sr.bounds.extents.y) * 0.9f;
        }

        var system = host.AddComponent<ParticleSystem>();

        var main = system.main;
        // Local so the motes ride the shield around its orbit instead of smearing a
        // trail across the arena; SetParent kept world scale at one, so Hierarchy
        // scaling is what makes the ring below world-sized.
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startColor = new Color(0.55f, 0.85f, 0.35f, 0.85f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
        main.maxParticles = 120;
        main.playOnAwake = false;

        var emission = system.emission;
        emission.rateOverTime = 22f;

        // Hollow-ish circle so the motes hug the rim and leave the shield face readable
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 0.4f;

        // Rises in WORLD up, so the tell reads the same whichever way the shield is
        // pointing — local up would spray sideways at 3 o'clock
        var velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

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
                new GradientColorKey(new Color(0.75f, 1f, 0.5f), 0f),
                new GradientColorKey(new Color(0.4f, 0.7f, 0.25f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        var renderer = system.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            var material = new Material(Shader.Find("Sprites/Default"));
            Sprite mote = PoisonResourceManager.Instance != null
                ? PoisonResourceManager.Instance.GetPoisonPuffSprite()
                : null;
            if (mote == null && PoisonResourceManager.Instance != null)
            {
                mote = PoisonResourceManager.Instance.GetPoisonBubbleSprite();
            }
            if (mote != null) material.mainTexture = mote.texture;
            renderer.material = material;
            renderer.sortingOrder = 20; // over the shield sprite
        }

        system.Play();
        return system;
    }
}
