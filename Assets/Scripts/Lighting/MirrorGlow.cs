using UnityEngine;

// The light a castle mirror gives off: its own colour, cast on the floor round
// the pane.
//
// Sits on the Mirror_Pane prefab beside MirrorPane and reads everything it needs
// off it — the colour, which way the pane stands, and how big it is — so a pane
// laid by any layout glows the right colour with no wiring per layout. Drawn by
// the torch's shader (Custom/TorchGlow) with a rectangle in place of a point, so
// the light hugs a tall pane's sides as evenly as its ends, one order under the
// mirror so the frame art keeps its own colours. TorchGlow.BuildGlowRenderer has
// the layering.
//
// It breathes, slowly, rather than flickering — glass, not fire. The breath's
// timing comes from the COLOUR, not from where the pane stands, so the two
// panes of a pair rise and fall together: colour is how a player reads a pair
// (MirrorColor), and the light should say the same thing. A shot going in or
// out flares it for a moment, alongside the pane's own swell (MirrorPane.Pulse).
[DisallowMultipleComponent]
[RequireComponent(typeof(MirrorPane))]
public class MirrorGlow : MonoBehaviour
{
    private const float WorldUnitsPerArtPixel = 1f / 32f;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int CenterId = Shader.PropertyToID("_Center");
    private static readonly int RadiusId = Shader.PropertyToID("_Radius");
    private static readonly int HalfSizeId = Shader.PropertyToID("_HalfSize");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int RingsId = Shader.PropertyToID("_Rings");
    private static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");

    [Tooltip("Custom/TorchGlow. Held as a reference so the shader travels into a build.")]
    [SerializeField] private Shader shader;

    [Header("Colours — the brightest pixel of each pane's glass")]
    [SerializeField] private Color green = new Color32(126, 246, 152, 255);
    [SerializeField] private Color cyan = new Color32(122, 236, 250, 255);
    [SerializeField] private Color vermilion = new Color32(250, 112, 80, 255);
    [SerializeField] private Color magenta = new Color32(240, 112, 214, 255);

    [Tooltip("How far the light reaches past the pane's edge, in world units. The pane is 1 x 2.")]
    [SerializeField] private float reach = 1f;

    [Tooltip("How much light the ring against the pane adds. ADDITIVE in linear light, so the useful " +
             "range is small.")]
    [Range(0f, 0.5f)][SerializeField] private float intensity = 0.08f;

    [Tooltip("Steps the falloff is cut into. Fewer = chunkier rings.")]
    [Range(2f, 32f)][SerializeField] private float rings = 7f;

    [Tooltip("How far the light swells and settles with its breath, as a fraction of Intensity.")]
    [Range(0f, 0.5f)][SerializeField] private float breath = 0.15f;

    [Tooltip("Seconds for one breath.")]
    [Range(0.5f, 20f)][SerializeField] private float breathPeriod = 3.5f;

    [Tooltip("Extra light, as a multiple of Intensity, the moment a shot goes in or out. Fades over Flare Seconds.")]
    [Range(0f, 3f)][SerializeField] private float flare = 1f;

    [SerializeField] private float flareSeconds = 0.35f;

    private MirrorPane _pane;
    private SpriteRenderer _glow;
    private MaterialPropertyBlock _block;

    void Awake()
    {
        _pane = GetComponent<MirrorPane>();

        Shader s = shader != null ? shader : Shader.Find("Custom/TorchGlow");
        if (s == null)
        {
            Debug.LogWarning($"[MirrorGlow] {name} has no shader, so it cannot glow. " +
                             "Assign Custom/TorchGlow to its Shader field.");
            enabled = false;
            return;
        }

        // On the pane's centre, which is where its transform sits (see MirrorPane)
        _glow = TorchGlow.BuildGlowRenderer(transform, s, "MirrorGlow");
        _block = new MaterialPropertyBlock();
    }

    void LateUpdate()
    {
        if (_glow == null || _pane == null) return;

        // The pane's size comes from its art, set in MirrorPane.Configure; a tall
        // pane stands its span up the screen, a wide one lays it across
        float halfSpan = _pane.Span * 0.5f;
        float halfThick = _pane.HalfThickness;
        Vector2 half = _pane.Facing.IsTall()
            ? new Vector2(halfThick, halfSpan)
            : new Vector2(halfSpan, halfThick);

        // The quad covers the rectangle plus the reach on every side
        Vector3 parentScale = transform.lossyScale;
        _glow.transform.localScale = new Vector3(
            (half.x + reach) * 2f / Mathf.Max(Mathf.Abs(parentScale.x), 0.0001f),
            (half.y + reach) * 2f / Mathf.Max(Mathf.Abs(parentScale.y), 0.0001f),
            1f);

        // Timed off the colour so a pair breathes together. Scaled time, like
        // the pane's own shimmer and swell.
        float phase = (int)_pane.PaneColor * 1.7f;
        float swell = 1f + Mathf.Sin(Time.time * (2f * Mathf.PI / breathPeriod) + phase) * breath;

        float sinceShot = (Time.time - _pane.LastPulseTime) / Mathf.Max(flareSeconds, 0.01f);
        float flash = sinceShot < 1f ? (1f - sinceShot) * flare : 0f;

        float dim = ArenaLighting.Instance != null ? ArenaLighting.Instance.GlobalLightScale() : 1f;

        Vector3 centre = _glow.transform.position;
        _glow.GetPropertyBlock(_block);
        _block.SetColor(ColorId, TorchGlow.ToRenderSpace(ColorFor(_pane.PaneColor)));
        _block.SetVector(CenterId, new Vector4(centre.x, centre.y, 0f, 0f));
        _block.SetVector(HalfSizeId, new Vector4(half.x, half.y, 0f, 0f));
        _block.SetFloat(RadiusId, reach);
        _block.SetFloat(IntensityId, intensity * (swell + flash) * dim);
        _block.SetFloat(RingsId, rings);
        _block.SetFloat(PixelSizeId, WorldUnitsPerArtPixel);
        _glow.SetPropertyBlock(_block);
    }

    private Color ColorFor(MirrorColor color)
    {
        switch (color)
        {
            case MirrorColor.Cyan: return cyan;
            case MirrorColor.Vermilion: return vermilion;
            case MirrorColor.Magenta: return magenta;
            default: return green;
        }
    }
}
