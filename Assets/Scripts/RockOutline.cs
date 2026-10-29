using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The glowing rim round an enemy rock, so a rock can be picked out of a busy
/// or half-lit arena at a glance — and which rock it is, from the colour alone:
/// pale for a plain stone, green for poison, orange for powder.
///
/// Drawn in Unity rather than painted into the sprite. A child quad a few
/// texels bigger than the rock carries Custom/RockOutline one sorting order
/// under it; the shader draws a round ring about the middle of the sprite,
/// so the rock itself is untouched and still takes the arena's light.
///
/// Lives on the rock's Art child, beside the SpriteRenderer it rims, so it turns
/// with the art when RockSpin spins it. The ring is round about the sprite's middle
/// and RockSpin turns about that same point, so the rim stays put round the stone.
///
/// Watches the rock rather than waiting to be told: ProjectileSettings.Become
/// swaps the sprite on a poison or powder rock, and the tutorial, the trials
/// and the Reflector all handle rocks along their own paths. Whatever sprite
/// and variant the rock has on a frame, the rim matches it on that frame.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class RockOutline : MonoBehaviour
{
    [Tooltip("Custom/RockOutline. Without it the rock simply has no rim.")]
    [SerializeField] private Shader shader;

    [Header("Colours")]
    [SerializeField] private Color plainColour = new Color32(0xE6, 0xE9, 0xEE, 0xFF);
    [SerializeField] private Color poisonColour = new Color32(0x46, 0xFF, 0x3C, 0xFF);
    [SerializeField] private Color explosiveColour = new Color32(0xFF, 0x6A, 0x00, 0xFF);

    [Header("Shape")]
    [Tooltip("Width of the solid rim past the circle the rock fits in, in texels of the rock's art. The rock draws at a quarter scale, so 4 is one arena pixel.")]
    [Range(0.5f, 8f)]
    [SerializeField] private float rimWidth = 1.6f;

    [Tooltip("How far the glow reaches past the rim, in the same texels.")]
    [Range(0f, 10f)]
    [SerializeField] private float glowWidth = 3f;

    [Tooltip("How bright the glow is right against the rim, fading out from there. The player's Rock Glow setting (RockGlowSetting) scales it.")]
    [Range(0f, 1f)]
    [SerializeField] private float glowStrength = 0.6f;

    [Tooltip("Flat bands the glow fades in. Bands rather than a smooth ramp, so it reads as pixel art.")]
    [Range(1, 6)]
    [SerializeField] private int glowBands = 3;

    [Header("Pulse")]
    [Tooltip("How far the glow dims at the bottom of each swell. The rim itself never pulses.")]
    [Range(0f, 1f)]
    [SerializeField] private float pulseAmount = 0.35f;

    [Range(0.1f, 5f)]
    [SerializeField] private float pulsePeriod = 0.8f;

    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int WidthId = Shader.PropertyToID("_Width");
    private static readonly int GlowWidthId = Shader.PropertyToID("_GlowWidth");
    private static readonly int GlowStrengthId = Shader.PropertyToID("_GlowStrength");
    private static readonly int GlowStepsId = Shader.PropertyToID("_GlowSteps");
    private static readonly int PulseAmountId = Shader.PropertyToID("_PulseAmount");
    private static readonly int PulsePeriodId = Shader.PropertyToID("_PulsePeriod");
    private static readonly int SpriteTexelsId = Shader.PropertyToID("_SpriteTexels");
    private static readonly int CircleId = Shader.PropertyToID("_Circle");
    private static readonly int TexSizeId = Shader.PropertyToID("_TexSize");
    private static readonly int PhaseId = Shader.PropertyToID("_Phase");
    private static readonly int MasterId = Shader.PropertyToID("_Master");

    // One material per shader and one quad per sprite, shared by every rock in
    // the air: everything that differs between rocks rides a property block
    private static readonly Dictionary<Shader, Material> Materials = new Dictionary<Shader, Material>();
    private static readonly Dictionary<(Sprite, int), Mesh> Quads = new Dictionary<(Sprite, int), Mesh>();

    private SpriteRenderer _rock;
    private ProjectileSettings _settings;
    private MeshFilter _filter;
    private MeshRenderer _renderer;
    private MaterialPropertyBlock _block;
    private Sprite _shownSprite;
    private int _shownMargin = -1;
    private float _phase;

    void Awake()
    {
        _rock = GetComponent<SpriteRenderer>();
        _settings = GetComponentInParent<ProjectileSettings>();
        if (shader == null) return;

        var holder = new GameObject("RockOutline");
        holder.transform.SetParent(transform, false);

        _filter = holder.AddComponent<MeshFilter>();
        _renderer = holder.AddComponent<MeshRenderer>();
        _renderer.sharedMaterial = MaterialFor(shader);
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        _renderer.enabled = false;

        _block = new MaterialPropertyBlock();

        // Each rock swells from the moment it is thrown, so a volley shimmers
        // down the line instead of pulsing as one
        _phase = Time.time;
    }

    void LateUpdate()
    {
        if (_renderer == null) return;

        Sprite sprite = _rock.sprite;
        // The player's Rock Glow setting at 0 takes the whole rim away, ring and glow
        bool show = _rock.enabled && sprite != null && RockGlowSetting.Intensity > 0f;
        _renderer.enabled = show;
        if (!show) return;

        Texture texture = sprite.texture;
        Rect rect = sprite.textureRect;

        // The circle the rock fits in: the middle of the sprite, reaching its
        // longer side. The quad has to hold that circle plus the rim and glow
        float radius = Mathf.Max(rect.width, rect.height) * 0.5f;
        float overhang = radius - Mathf.Min(rect.width, rect.height) * 0.5f;
        int margin = Mathf.CeilToInt(overhang + rimWidth + glowWidth) + 1;
        if (sprite != _shownSprite || margin != _shownMargin)
        {
            _filter.sharedMesh = QuadFor(sprite, margin);
            _shownSprite = sprite;
            _shownMargin = margin;
        }

        // Just under the rock, wherever the rock is sorted
        _renderer.sortingLayerID = _rock.sortingLayerID;
        _renderer.sortingOrder = _rock.sortingOrder - 1;

        _renderer.GetPropertyBlock(_block);
        _block.SetTexture(MainTexId, texture);
        _block.SetColor(ColorId, ColourFor(_settings != null ? _settings.Variant : RockVariant.Plain));
        _block.SetFloat(WidthId, rimWidth);
        _block.SetFloat(GlowWidthId, glowWidth);
        _block.SetFloat(GlowStrengthId, glowStrength * RockGlowSetting.Intensity);
        _block.SetFloat(GlowStepsId, glowBands);
        _block.SetFloat(PulseAmountId, pulseAmount);
        _block.SetFloat(PulsePeriodId, pulsePeriod);
        _block.SetVector(SpriteTexelsId, new Vector4(rect.xMin, rect.yMin, rect.xMax, rect.yMax));
        _block.SetVector(CircleId, new Vector4(rect.center.x, rect.center.y, radius, 0f));
        _block.SetVector(TexSizeId, new Vector4(texture.width, texture.height,
            1f / texture.width, 1f / texture.height));
        _block.SetFloat(PhaseId, _phase);

        // Unlit, so it would otherwise hang in the frame glowing while the
        // curtain takes the arena to black — the same ride GemGlow takes
        _block.SetFloat(MasterId, ArenaLighting.Instance != null ? ArenaLighting.Instance.GlobalLightScale() : 1f);
        _renderer.SetPropertyBlock(_block);
    }

    private Color ColourFor(RockVariant variant)
    {
        switch (variant)
        {
            case RockVariant.Poison: return poisonColour;
            case RockVariant.Explosive: return explosiveColour;
            default: return plainColour;
        }
    }

    private static Material MaterialFor(Shader shader)
    {
        if (Materials.TryGetValue(shader, out Material material) && material != null) return material;

        material = new Material(shader) { name = "RockOutline", hideFlags = HideFlags.HideAndDontSave };
        Materials[shader] = material;
        return material;
    }

    /// <summary>
    /// A quad over the sprite plus <paramref name="margin"/> texels all round,
    /// laid out exactly as a SpriteRenderer lays out the sprite — same pivot,
    /// same pixels per unit — so every texel of the quad sits on its texel of
    /// the rock. The UVs run past the sprite's rectangle; the shader treats
    /// anything out there as empty.
    /// </summary>
    private static Mesh QuadFor(Sprite sprite, int margin)
    {
        var key = (sprite, margin);
        if (Quads.TryGetValue(key, out Mesh mesh) && mesh != null) return mesh;

        Rect rect = sprite.textureRect;
        Vector2 offset = sprite.textureRectOffset;
        Vector2 pivot = sprite.pivot;
        float ppu = sprite.pixelsPerUnit;
        float texW = sprite.texture.width;
        float texH = sprite.texture.height;

        float x0 = (offset.x - margin - pivot.x) / ppu;
        float x1 = (offset.x + rect.width + margin - pivot.x) / ppu;
        float y0 = (offset.y - margin - pivot.y) / ppu;
        float y1 = (offset.y + rect.height + margin - pivot.y) / ppu;

        float u0 = (rect.xMin - margin) / texW;
        float u1 = (rect.xMax + margin) / texW;
        float v0 = (rect.yMin - margin) / texH;
        float v1 = (rect.yMax + margin) / texH;

        mesh = new Mesh { name = "RockOutline_" + sprite.name, hideFlags = HideFlags.HideAndDontSave };
        mesh.vertices = new[]
        {
            new Vector3(x0, y0, 0f), new Vector3(x1, y0, 0f),
            new Vector3(x0, y1, 0f), new Vector3(x1, y1, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(u0, v0), new Vector2(u1, v0),
            new Vector2(u0, v1), new Vector2(u1, v1)
        };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();

        Quads[key] = mesh;
        return mesh;
    }
}
