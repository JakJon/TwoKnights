using UnityEngine;

// How thick the cover is over one stage of a map, and what the light that gets
// past it does to the arena. A stage on a MapDefinition points at one of these;
// ArenaLighting turns it into the two overlay quads that actually draw.
//
// Named for its first job but not limited to it. Underneath the foliage wording
// it is just a soft, ragged-edged darkening around the frame with optional leaf
// shadows and light inside it, so a map with no leaves anywhere uses the same
// profile with the foliage and sun terms at zero — which is exactly what The
// Mine does for its breathing vignette.
//
// THE WHOLE THING IS MEANT TO BE ALMOST INVISIBLE — a hint that the fight is on
// a forest floor under cover, not a weather effect and not a picture in its own
// right. Louder versions of this (hard-edged foliage, light beams, a brightened
// clearing) were all tried and all made the arena worse. If in doubt, turn it
// down; the backdrop art is the thing being looked at, and this only leans on it.
//
// Every value is authored in the units you would think in while looking at the
// game — world units for the glade, sRGB colours for shade and sun — and is
// converted on the way to the shader. Colours in particular: the project renders
// in Linear, so a Color picked in the inspector has to be handed over as .linear
// or every shade comes out washed out and every highlight comes out scorching.
[CreateAssetMenu(fileName = "Canopy", menuName = "Maps/Canopy Profile")]
public class CanopyProfile : ScriptableObject
{
    // Cached property ids — this gets pushed on every profile change, and the
    // string lookups are the only expensive part of doing so
    private static readonly int ShadeColorId = Shader.PropertyToID("_ShadeColor");
    private static readonly int ShadeStrengthId = Shader.PropertyToID("_ShadeStrength");
    private static readonly int TopShadeId = Shader.PropertyToID("_TopShade");
    private static readonly int BreathAmountId = Shader.PropertyToID("_BreathAmount");
    private static readonly int BreathPeriodId = Shader.PropertyToID("_BreathPeriod");
    private static readonly int SunColorId = Shader.PropertyToID("_SunColor");

    private static readonly int GladeCenterId = Shader.PropertyToID("_GladeCenter");
    private static readonly int GladeRadiusId = Shader.PropertyToID("_GladeRadius");
    private static readonly int GladeSoftnessId = Shader.PropertyToID("_GladeSoftness");
    private static readonly int EdgeNoiseId = Shader.PropertyToID("_EdgeNoise");
    private static readonly int EdgeRaggedId = Shader.PropertyToID("_EdgeRagged");

    private static readonly int DappleScaleId = Shader.PropertyToID("_DappleScale");
    private static readonly int DappleDetailId = Shader.PropertyToID("_DappleDetail");
    private static readonly int DappleCoverageId = Shader.PropertyToID("_DappleCoverage");
    private static readonly int DappleDepthId = Shader.PropertyToID("_DappleDepth");
    private static readonly int DappleLightId = Shader.PropertyToID("_DappleLight");

    private static readonly int ShaftAngleId = Shader.PropertyToID("_ShaftAngle");
    private static readonly int ShaftScaleId = Shader.PropertyToID("_ShaftScale");
    private static readonly int ShaftSharpId = Shader.PropertyToID("_ShaftSharp");
    private static readonly int ShaftStrengthId = Shader.PropertyToID("_ShaftStrength");

    private static readonly int WindAngleId = Shader.PropertyToID("_WindAngle");
    private static readonly int WindSpeedId = Shader.PropertyToID("_WindSpeed");

    private static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");
    private static readonly int LevelsId = Shader.PropertyToID("_Levels");

    [Tooltip("Custom/CanopyLight. Held as a reference rather than found by name so the shader is " +
             "guaranteed to travel into a build.")]
    [SerializeField] private Shader shader;

    [Header("Where it lands")]
    [Tooltip("Strength of the pass drawn over the BACKDROP only — the dappled forest floor. Nothing you " +
             "need to see is under it, so this one can carry the effect.")]
    [Range(0f, 1f)][SerializeField] private float groundStrength = 1f;

    [Tooltip("Strength of the second pass drawn over EVERYTHING — knights, enemies, projectiles — so they " +
             "sit in the same light as the floor instead of floating over it. Deliberately much weaker " +
             "than the ground pass: this is the number that trades atmosphere against being able to read " +
             "an enemy coming out of the dark edge of the frame.")]
    [Range(0f, 1f)][SerializeField] private float actorStrength = 0.3f;

    [Header("Shade")]
    [Tooltip("What deep shade is made of. The frame is lerped TOWARD this rather than multiplied toward " +
             "black, which is what makes the dark edges read as cool forest shade instead of as a dimmer. " +
             "Keep it DARKER than the darkest pixel of the backdrop: a shade colour lighter than what it " +
             "falls on lifts the corners instead of deepening them and the whole arena goes milky.")]
    [SerializeField] private Color shadeColor = new Color(0.02f, 0.035f, 0.032f, 1f);

    [Tooltip("How far toward the shade colour the thickest canopy takes the frame. The headline number " +
             "for 'how strong is this at all' — the first thing to turn DOWN if the arena starts looking " +
             "like it has a picture painted over it.")]
    [Range(0f, 1f)][SerializeField] private float shadeStrength = 0.26f;

    [Tooltip("A little extra shade toward the top of the frame, so the arena reads as a floor with depth " +
             "rather than a flat plate.")]
    [Range(0f, 0.5f)][SerializeField] private float topShade = 0.04f;

    [Header("Breath — a slow swell of the shade")]
    [Tooltip("How far the shade swells and settles, as a fraction of Shade Strength. Only the big overhead " +
             "shape breathes; the leaf shadows hold still, because a floor that throbs in time with it is " +
             "the one thing that would give the effect away. Zero is a dead-still canopy — what the forest " +
             "wants. The Mine uses it to make its dark close on the frame and let go again.")]
    [Range(0f, 1f)][SerializeField] private float breathAmount = 0f;

    [Tooltip("Seconds for one full swell and release. Slow is the whole point: ten seconds and up reads as " +
             "the room being alive, three seconds reads as a pulsing light and is immediately obvious.")]
    [Range(0.5f, 60f)][SerializeField] private float breathPeriod = 11f;

    [Header("The glade — where the canopy thins")]
    [Tooltip("World position the trees open up over. The knights stand near the origin, so this is " +
             "usually close to it.")]
    [SerializeField] private Vector2 gladeCenter = new Vector2(0f, -0.2f);

    [Tooltip("World-unit radii of the opening. The arena is 20 x 11.25 world units, so radii near half of " +
             "that put the shade on the frame's edge and leave the fight in the light.")]
    [SerializeField] private Vector2 gladeRadius = new Vector2(7.5f, 4.2f);

    [Tooltip("How long the fall-off from open sky to full cover takes, as a fraction of the radius.")]
    [Range(0.05f, 1.5f)][SerializeField] private float gladeSoftness = 0.55f;

    [Tooltip("Size of the lobes chewed out of the glade's edge. Larger = whole branches, smaller = fussy.")]
    [Range(0.02f, 2f)][SerializeField] private float edgeNoiseScale = 0.22f;

    [Tooltip("How ragged the edge of the opening is. Zero is a clean ellipse and looks like a camera " +
             "vignette; this is what stops it looking like one.")]
    [Range(0f, 1f)][SerializeField] private float edgeRaggedness = 0.3f;

    [Header("Dapple — leaf shadows on the floor")]
    [Tooltip("Size of the leaf blobs, in cycles per world unit. HIGHER means SMALLER shadows.")]
    [Range(0.05f, 4f)][SerializeField] private float dappleScale = 0.9f;

    [Tooltip("How much a finer second octave breaks up the leaf blobs.")]
    [Range(0f, 1f)][SerializeField] private float dappleDetail = 0.4f;

    [Tooltip("Fraction of the canopy that is leaf rather than gap. Higher = more shadow, fewer sun spots.")]
    [Range(0.05f, 0.95f)][SerializeField] private float leafCoverage = 0.46f;

    [Tooltip("How dark a leaf shadow gets, in the same units as Shade Strength. Low on purpose — the " +
             "dapple should be felt as texture on the floor, never seen as a pattern moving overhead.")]
    [Range(0f, 1f)][SerializeField] private float leafShadowDepth = 0.11f;

    [Header("Sun")]
    [Tooltip("Colour of the light that gets through. Warm against the cool shade is most of why the " +
             "effect reads at all.")]
    [SerializeField] private Color sunColor = new Color(1f, 0.93f, 0.68f, 1f);

    [Tooltip("How much light a gap in the leaves adds. ADDITIVE in linear light, so the useful range is " +
             "tiny — past about 0.02 the glade hazes over instead of catching the sun.")]
    [Range(0f, 0.2f)][SerializeField] private float sunThroughGaps = 0.005f;

    [Header("Shafts")]
    [Tooltip("Direction the light falls, in radians.")]
    [Range(-3.15f, 3.15f)][SerializeField] private float shaftAngle = -1.05f;

    [Tooltip("Width of the shafts, in cycles per world unit.")]
    [Range(0.02f, 2f)][SerializeField] private float shaftScale = 0.35f;

    [Tooltip("How much of the noise counts as a shaft. Higher = fewer, cleaner streaks.")]
    [Range(0f, 0.95f)][SerializeField] private float shaftSharpness = 0.72f;

    [Tooltip("Brightness of the shafts. This is texture, not sunbeams — turning it up to where you can " +
             "point at a beam is exactly what made an earlier pass look wrong.")]
    [Range(0f, 0.2f)][SerializeField] private float shaftStrength = 0.002f;

    [Header("Wind")]
    [Tooltip("Direction the whole canopy travels, in radians. It travels this way and ONLY this way: an " +
             "oscillating sway gives itself away the moment it turns around, and the arena stops reading " +
             "as weather and starts reading as a texture being slid about overhead.")]
    [Range(-3.15f, 3.15f)][SerializeField] private float windAngle = -0.2f;

    [Tooltip("World units per second the canopy travels. 0.12 is about 4 art pixels a second. Nothing " +
             "loops: the drift is unbounded and the shader's noise is exact for far longer than any " +
             "session.")]
    [Range(0f, 2f)][SerializeField] private float windSpeed = 0.12f;

    [Header("Pixel grid")]
    [Tooltip("Steps the shade is dithered to. Higher is smoother; low values give a chunky, hand-placed " +
             "look, but a heavy dither spread over the whole frame reads as dust.")]
    [Range(4f, 256f)][SerializeField] private float ditherLevels = 40f;

    public Shader Shader => shader;
    public float GroundStrength => groundStrength;
    public float ActorStrength => actorStrength;

    /// <summary>
    /// Writes every authored value onto the material ArenaLighting draws with.
    /// Master strength is NOT written here — that rides a per-renderer property
    /// block, because the two passes differ only by it.
    /// </summary>
    /// <param name="worldUnitsPerArtPixel">
    /// Size of one pixel of the game's art in world units (1/32 at this project's
    /// 32 pixels-per-unit). Everything the shader computes is snapped to this
    /// grid, so a wrong value here is the difference between pixel art and a
    /// smooth gradient sliding under it.
    /// </param>
    public void PushTo(Material material, float worldUnitsPerArtPixel)
    {
        if (material == null) return;

        material.SetColor(ShadeColorId, ToRenderSpace(shadeColor));
        material.SetFloat(ShadeStrengthId, shadeStrength);
        material.SetFloat(TopShadeId, topShade);
        material.SetFloat(BreathAmountId, breathAmount);
        material.SetFloat(BreathPeriodId, breathPeriod);
        material.SetColor(SunColorId, ToRenderSpace(sunColor));

        material.SetVector(GladeCenterId, gladeCenter);
        material.SetVector(GladeRadiusId, new Vector4(Mathf.Max(gladeRadius.x, 0.01f), Mathf.Max(gladeRadius.y, 0.01f), 0f, 0f));
        material.SetFloat(GladeSoftnessId, gladeSoftness);
        material.SetFloat(EdgeNoiseId, edgeNoiseScale);
        material.SetFloat(EdgeRaggedId, edgeRaggedness);

        material.SetFloat(DappleScaleId, dappleScale);
        material.SetFloat(DappleDetailId, dappleDetail);
        material.SetFloat(DappleCoverageId, leafCoverage);
        material.SetFloat(DappleDepthId, leafShadowDepth);
        material.SetFloat(DappleLightId, sunThroughGaps);

        material.SetFloat(ShaftAngleId, shaftAngle);
        material.SetFloat(ShaftScaleId, shaftScale);
        material.SetFloat(ShaftSharpId, shaftSharpness);
        material.SetFloat(ShaftStrengthId, shaftStrength);

        material.SetFloat(WindAngleId, windAngle);
        material.SetFloat(WindSpeedId, windSpeed);

        material.SetFloat(PixelSizeId, Mathf.Max(worldUnitsPerArtPixel, 0.0001f));
        material.SetFloat(LevelsId, Mathf.Max(ditherLevels, 1f));
    }

    // Inspector colours are sRGB. The project renders Linear, and the shader does
    // real arithmetic with these — lerping toward the shade colour and adding the
    // sun colour — so they have to arrive in the space that arithmetic happens in.
    private static Color ToRenderSpace(Color c) =>
        QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
}
