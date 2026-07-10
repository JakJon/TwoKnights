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

    private float radius;
    private float duration;
    private string ownerTag;

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
        cloud.duration = level >= 2 ? 5f : 3f;
        cloud.ownerTag = ownerTag;
        cloud.BuildCloudParticles();
        return cloud;
    }

    // One-shot radial pop for Plaguebringer bursts; cleans itself up
    public static void SpawnBurstEffect(Vector2 position)
    {
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

    private void Update()
    {
        elapsed += Time.deltaTime;
        sinceLastCheck += Time.deltaTime;

        if (sinceLastCheck >= CheckInterval)
        {
            sinceLastCheck = 0f;
            PoisonEnemiesInside();
        }

        if (elapsed >= duration && !emissionStopped)
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
        foreach (var col in Physics2D.OverlapCircleAll(transform.position, radius))
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
            Sprite bubbleSprite = PoisonResourceManager.Instance != null
                ? PoisonResourceManager.Instance.GetPoisonBubbleSprite()
                : null;
            if (bubbleSprite != null)
            {
                material.mainTexture = bubbleSprite.texture;
            }
            renderer.material = material;
        }

        return system;
    }
}
