using UnityEngine;

// Tiny code-built effects for the Shadow Order — no dedicated art required.
public static class ShadowFx
{
    // Killing Blow: a quick dark-violet slash burst where the execute landed
    public static void ExecuteFlash(Vector2 position)
    {
        var burstObject = new GameObject("ShadowExecuteFlash");
        burstObject.transform.position = position;

        var system = burstObject.AddComponent<ParticleSystem>();

        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(0.42f, 0.36f, 0.75f, 0.9f);
        main.startLifetime = 0.35f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.maxParticles = 40;
        main.playOnAwake = false;

        var emission = system.emission;
        emission.rateOverTime = 0f;

        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.1f;

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
            renderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        system.Play();
        system.Emit(18);
        Object.Destroy(burstObject, 1f);
    }
}
