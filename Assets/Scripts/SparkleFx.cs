using UnityEngine;

// Code-built gold sparkles: little four-point stars that pop in and out over a
// knight's body. Iron Vigil wears them for its whole window so the special reads
// as "untouchable" at a glance, on top of the shared Dawn halo every
// invulnerability gets.
//
// The star is drawn here in the knights' own gold trim (fff0a0 / e6bd3a / a8791f)
// rather than tinted from a white mote, so the centre stays bright while the arms
// stay gold — a tint can only darken, and a sparkle needs a hot middle.
public static class SparkleFx
{
    private const string ChildName = "GoldSparkles";

    // Same as the knight sprites, so a star at size 1 is drawn at the game's pixel scale
    private const float PixelsPerUnit = 32f;

    private static readonly Color Core = new Color32(0xff, 0xf0, 0xa0, 0xff);
    private static readonly Color Arm = new Color32(0xe6, 0xbd, 0x3a, 0xff);
    private static readonly Color Tip = new Color32(0xa8, 0x79, 0x1f, 0xff);

    // The Overseer's blue gnome (owner, 2026-10-19: "a new blue gnome that
    // sparkles"). Ice-white core, sky arms, deep blue tips — my pick of blues.
    private static readonly Color BlueCore = new Color32(0xe8, 0xf8, 0xff, 0xff);
    private static readonly Color BlueArm = new Color32(0x7c, 0xc8, 0xff, 0xff);
    private static readonly Color BlueTip = new Color32(0x2f, 0x6f, 0xc0, 0xff);

    private const string BlueChildName = "BlueSparkles";

    private static Texture2D _starTexture;
    private static Texture2D _blueStarTexture;

    private static Texture2D StarTexture
    {
        get
        {
            if (_starTexture == null) _starTexture = BuildStar(Core, Arm, Tip);
            return _starTexture;
        }
    }

    private static Texture2D BlueStarTexture
    {
        get
        {
            if (_blueStarTexture == null) _blueStarTexture = BuildStar(BlueCore, BlueArm, BlueTip);
            return _blueStarTexture;
        }
    }

    private static Texture2D BuildStar(Color core, Color arm, Color tip)
    {
        // 7x7 four-point star: hot core, coloured arms, darker tips
        //   . . . t . . .
        //   . . . a . . .
        //   . . . a . . .
        //   t a a C a a t
        //   . . . a . . .
        //   . . . a . . .
        //   . . . t . . .
        const int size = 7;
        const int mid = size / 2;
        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;
        for (int d = -mid; d <= mid; d++)
        {
            Color c = d == 0 ? core : (Mathf.Abs(d) == mid ? tip : arm);
            pixels[mid * size + (mid + d)] = c;
            pixels[(mid + d) * size + mid] = c;
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    /// <summary>
    /// Gold sparkles over <paramref name="knight"/> for <paramref name="seconds"/>.
    /// Replaces any already running rather than stacking, so firing again mid-window
    /// gives one set of sparkles that lasts the new window.
    /// </summary>
    public static void ShowGoldSparkles(GameObject knight, float seconds)
    {
        if (knight == null || seconds <= 0f) return;
        Build(knight, ChildName, StarTexture, seconds, false, 14f);
    }

    /// <summary>
    /// Blue sparkles over <paramref name="target"/> for as long as it lives — the
    /// Overseer's blue gnome wears them so it can be picked out of an ambush at a
    /// glance. Go with the object, so they need no clean-up.
    /// </summary>
    public static void ShowBlueSparkles(GameObject target)
    {
        if (target == null) return;
        Build(target, BlueChildName, BlueStarTexture, 1f, true, 10f);
    }

    private static void Build(GameObject target, string childName, Texture2D star,
                              float seconds, bool loop, float rate)
    {
        Transform existing = target.transform.Find(childName);
        if (existing != null) Object.Destroy(existing.gameObject);

        var body = target.GetComponent<SpriteRenderer>();
        if (body == null) body = target.GetComponentInChildren<SpriteRenderer>();
        Bounds bounds = body != null ? body.bounds : new Bounds(target.transform.position, Vector3.one);

        var host = new GameObject(childName);
        host.transform.SetParent(target.transform);
        host.transform.position = bounds.center;

        var system = host.AddComponent<ParticleSystem>();
        // A fresh ParticleSystem starts playing on its first frame; stop it so the
        // duration below is set on a system that is not yet running
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        const float maxLifetime = 0.6f;
        float starSize = 7f / PixelsPerUnit;

        var main = system.main;
        main.duration = seconds;
        main.loop = loop;
        main.playOnAwake = false;
        // Ignore any parent scale, so the stars are always drawn at pixel scale
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, maxLifetime);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(starSize, starSize * 1.6f);
        main.startColor = Color.white;
        main.maxParticles = 40;

        var emission = system.emission;
        emission.rateOverTime = rate;

        // Anywhere over the body and a touch beyond it, not a ring — sparkles
        // should look like they are coming off the armour
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(bounds.size.x * 1.1f, bounds.size.y * 0.95f, 0f);

        // A slow lift so they feel alive without turning into a stream
        var velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // The twinkle: each star grows from a point, holds, and shrinks away
        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0f);
        sizeCurve.AddKey(0.3f, 1f);
        sizeCurve.AddKey(0.6f, 0.9f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var renderer = system.GetComponent<ParticleSystemRenderer>();
        var material = new Material(Shader.Find("Sprites/Default"));
        material.mainTexture = star;
        renderer.material = material;
        if (body != null) renderer.sortingLayerID = body.sortingLayerID;
        // Over the knight, and over the Dawn halo (DawnFx draws at 20)
        renderer.sortingOrder = 21;

        system.Play();

        // A looping set lives and dies with what it is on; the material goes with
        // the host either way
        host.AddComponent<SparkleMaterialOwner>().Material = material;
        if (!loop) Object.Destroy(host, seconds + maxLifetime + 0.1f);
    }

    // Frees the per-sparkle material when its host goes, however it goes
    private sealed class SparkleMaterialOwner : MonoBehaviour
    {
        public Material Material;

        private void OnDestroy()
        {
            if (Material != null) Object.Destroy(Material);
        }
    }
}
