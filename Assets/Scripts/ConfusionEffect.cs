using UnityEngine;

// Confusion status (dark bat sonar): reverses the knight's shield-orbit input
// for a few seconds, spins a swirl icon over their head, and puffs a small
// violet particle swirl. Re-applying refreshes the timer. The component
// removes itself when the timer runs out.
public class ConfusionEffect : MonoBehaviour
{
    private const float IconSpinDegreesPerSecond = 200f;
    private const float IconHeight = 1.6f;
    private static readonly Color ParticleTint = new Color(0.58f, 0.53f, 1f, 0.8f); // shadow violet

    private float _remaining;
    private ShieldOrbit _shield;
    private GameObject _icon;
    private ParticleSystem _particles;

    public static void Apply(GameObject knight, float duration, Sprite iconSprite)
    {
        if (knight == null) return;
        ConfusionEffect effect = knight.GetComponent<ConfusionEffect>();
        if (effect == null)
        {
            effect = knight.AddComponent<ConfusionEffect>();
        }
        effect.Begin(duration, iconSprite);
    }

    private void Begin(float duration, Sprite iconSprite)
    {
        _remaining = duration;

        if (_shield == null)
        {
            _shield = GetComponentInChildren<ShieldOrbit>();
        }
        if (_shield != null)
        {
            _shield.InvertControls = true;
        }

        if (_icon == null && iconSprite != null)
        {
            CreateIcon(iconSprite);
        }
        if (_particles == null)
        {
            CreateParticles();
        }
    }

    private void CreateIcon(Sprite iconSprite)
    {
        _icon = new GameObject("ConfusionIcon");
        _icon.transform.SetParent(transform);
        _icon.transform.localPosition = new Vector3(0f, IconHeight, 0f);
        var renderer = _icon.AddComponent<SpriteRenderer>();
        renderer.sprite = iconSprite;
        renderer.sortingOrder = 20; // above the knight and shield
    }

    // Follows the PoisonCloud pattern: particle system built in code with the
    // default sprite material, tinted at the system level
    private void CreateParticles()
    {
        GameObject host = new GameObject("ConfusionParticles");
        host.transform.SetParent(transform);
        host.transform.localPosition = new Vector3(0f, IconHeight - 0.3f, 0f);
        _particles = host.AddComponent<ParticleSystem>();

        var main = _particles.main;
        main.startLifetime = 0.9f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
        main.startColor = ParticleTint;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 30;

        var emission = _particles.emission;
        emission.rateOverTime = 10f;

        var shape = _particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.35f;

        // Slow orbit around the head sells the dizziness
        var velocity = _particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.orbitalZ = new ParticleSystem.MinMaxCurve(2.2f);

        var renderer = host.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.sortingOrder = 19;
    }

    private void Update()
    {
        if (_icon != null)
        {
            _icon.transform.Rotate(0f, 0f, IconSpinDegreesPerSecond * Time.deltaTime);
        }

        _remaining -= Time.deltaTime;
        if (_remaining <= 0f)
        {
            Destroy(this);
        }
    }

    private void OnDestroy()
    {
        if (_shield != null)
        {
            _shield.InvertControls = false;
        }
        if (_icon != null)
        {
            Destroy(_icon);
        }
        if (_particles != null)
        {
            Destroy(_particles.gameObject);
        }
    }
}
