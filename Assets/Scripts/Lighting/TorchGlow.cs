using UnityEngine;

// The light a torch throws: a small, flickering warm pool round its flame.
//
// Sits on the object that carries the torch art (the castle pillar) and builds
// its own child renderer on Awake, so the only wiring a prefab needs is this
// component and its shader. The pool is drawn by Custom/TorchGlow — additive,
// stepped into rings and dithered on the art's pixel grid.
//
// It draws on the Background layer one order UNDER the castle pillar
// (Background/11) and the mirrors (Background/12), so it lights the floor round the pillar and the pillar keeps
// its own colours — drawn over the pillar, the warm light turned its near-black
// top brown. Being floor, it takes ArenaLighting's ground shade (Background/50)
// like the rest of the floor does, so a torch out in the dark edge glows less
// than one in the middle. Every knight, enemy and projectile is on Default, above it.
//
// The flicker is the campfire's recipe (CampfireScene): a slow swell under a
// quick shiver, each from Perlin noise. Every torch reads the noise from its own
// place, so a row of pillars does not flicker in step.
[DisallowMultipleComponent]
public class TorchGlow : MonoBehaviour
{
    private const string GlowSortingLayer = "Background";
    private const int GlowSortingOrder = 10;

    // Matches the project's import default (spritePixelsToUnits: 32)
    private const float WorldUnitsPerArtPixel = 1f / 32f;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int CenterId = Shader.PropertyToID("_Center");
    private static readonly int RadiusId = Shader.PropertyToID("_Radius");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int RingsId = Shader.PropertyToID("_Rings");
    private static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");

    // Shared by every torch: one material, the per-torch values on property blocks
    private static Material _sharedMaterial;
    private static Sprite _quad;
    private static Texture2D _quadTexture;

    [Tooltip("Custom/TorchGlow. Held as a reference so the shader travels into a build.")]
    [SerializeField] private Shader shader;

    [Tooltip("Where the flame is, in local units from this object's position. The castle pillar's torch " +
             "is the little flame at the bottom middle of its art.")]
    [SerializeField] private Vector2 flameOffset = new Vector2(0f, -0.42f);

    [Tooltip("Colour of the light. Inspector colours are sRGB; it is converted on the way to the shader. " +
             "Kept amber with almost no blue: added over the navy floor, an orange with more blue in it " +
             "came out pink.")]
    [SerializeField] private Color glowColor = new Color(1f, 0.72f, 0.18f, 1f);

    [Tooltip("How far the light reaches, in world units. The pillar is 1 unit across.")]
    [SerializeField] private float radius = 1.4f;

    [Tooltip("How much light the brightest ring adds. ADDITIVE in linear light, so the useful range is " +
             "small — a dark navy floor takes a lot from a little.")]
    [Range(0f, 0.5f)][SerializeField] private float intensity = 0.07f;

    [Tooltip("Steps the falloff is cut into. Fewer = chunkier rings.")]
    [Range(2f, 32f)][SerializeField] private float rings = 7f;

    [Tooltip("How far the light swells and shrinks with the flicker, as a fraction of Intensity.")]
    [Range(0f, 0.5f)][SerializeField] private float flicker = 0.18f;

    private SpriteRenderer _glow;
    private MaterialPropertyBlock _block;
    private float _noiseSeed;

    void Awake()
    {
        Shader s = shader != null ? shader : Shader.Find("Custom/TorchGlow");
        if (s == null)
        {
            Debug.LogWarning($"[TorchGlow] {name} has no shader, so its torch cannot glow. " +
                             "Assign Custom/TorchGlow to its Shader field.");
            enabled = false;
            return;
        }

        _glow = BuildGlowRenderer(transform, s, "TorchGlow");
        _glow.transform.localPosition = new Vector3(flameOffset.x, flameOffset.y, 0f);

        _block = new MaterialPropertyBlock();

        // Where this torch reads the flicker noise from: its own spot in the
        // arena, so neighbours are out of step and a given pillar always
        // flickers the same way
        Vector3 p = transform.position;
        _noiseSeed = p.x * 1.73f + p.y * 3.11f;
    }

    void LateUpdate()
    {
        if (_glow == null) return;

        // The quad is the whole pool; lossyScale undoes any scale on the parent
        // so the reach stays in world units
        Vector3 parentScale = transform.lossyScale;
        float size = radius * 2f;
        _glow.transform.localScale = new Vector3(
            size / Mathf.Max(Mathf.Abs(parentScale.x), 0.0001f),
            size / Mathf.Max(Mathf.Abs(parentScale.y), 0.0001f),
            1f);

        // Two speeds laid over each other, as at the campfire. Unscaled so the
        // flame keeps moving while the game is paused under a menu.
        float now = Time.unscaledTime;
        float slow = Mathf.PerlinNoise(now * 2.3f + _noiseSeed, 0.37f) - 0.5f;
        float quick = Mathf.PerlinNoise(now * 8.9f + _noiseSeed, 4.2f) - 0.5f;
        float swell = 1f + (slow * 1.2f + quick * 0.6f) * flicker * 2f;

        // Rides the global light down with the venture curtain, like the canopy:
        // an unlit glow would otherwise hang in the frame while the arena goes black
        float dim = ArenaLighting.Instance != null ? ArenaLighting.Instance.GlobalLightScale() : 1f;

        Vector3 centre = _glow.transform.position;
        _glow.GetPropertyBlock(_block);
        _block.SetColor(ColorId, ToRenderSpace(glowColor));
        _block.SetVector(CenterId, new Vector4(centre.x, centre.y, 0f, 0f));
        _block.SetFloat(RadiusId, radius * (1f + (swell - 1f) * 0.35f));
        _block.SetFloat(IntensityId, intensity * swell * dim);
        _block.SetFloat(RingsId, rings);
        _block.SetFloat(PixelSizeId, WorldUnitsPerArtPixel);
        _glow.SetPropertyBlock(_block);
    }

    /// <summary>
    /// A child renderer that draws Custom/TorchGlow on the floor, one order under
    /// the castle's pillars and mirrors. Shared with MirrorGlow: every glow uses one
    /// material, and the per-object values ride a property block.
    /// </summary>
    public static SpriteRenderer BuildGlowRenderer(Transform parent, Shader s, string name)
    {
        EnsureShared(s);

        var holder = new GameObject(name);
        holder.transform.SetParent(parent, false);

        var renderer = holder.AddComponent<SpriteRenderer>();
        renderer.sprite = _quad;
        renderer.sharedMaterial = _sharedMaterial;
        renderer.sortingLayerName = GlowSortingLayer;
        renderer.sortingOrder = GlowSortingOrder;
        return renderer;
    }

    private static void EnsureShared(Shader s)
    {
        if (_sharedMaterial == null || _sharedMaterial.shader != s)
            _sharedMaterial = new Material(s) { name = "TorchGlow", hideFlags = HideFlags.HideAndDontSave };

        if (_quad != null) return;

        _quadTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "TorchGlowQuad",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point
        };
        _quadTexture.SetPixel(0, 0, Color.white);
        _quadTexture.Apply();

        // A one-unit quad the shader never samples; the pool comes from world position
        _quad = Sprite.Create(_quadTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        _quad.name = "TorchGlowQuad";
        _quad.hideFlags = HideFlags.HideAndDontSave;
    }

    public static Color ToRenderSpace(Color c) =>
        QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
}
