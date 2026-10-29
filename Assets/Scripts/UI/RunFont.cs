using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// The pixel typeface the text printed during a run is set in (owner,
/// 2026-10-13): damage numbers, the tutorial's lines, the NPC text box and the
/// quest cards. The menus keep their serif, and so does the wave banner — the
/// owner tried it in pixels and put it back the same day.
///
/// It is Jersey 10 (SIL Open Font Licence — the licence sits beside the .ttf in
/// Assets/Resources/Fonts). Its letters are drawn on a grid: capitals ten pixels
/// tall, strokes two pixels wide. <see cref="WorldSize"/> turns "how big is one of
/// the font's pixels" into a font size, so text can be set with its pixels the
/// same size as the art's.
///
/// To change the typeface, replace the .ttf, point <see cref="ResourcePath"/> and
/// the two style sheets at the new file, and set <see cref="EmPixels"/> to the new
/// font's grid. Nothing else names it.
///
/// The font asset is built from the .ttf as the game starts, the same kind of
/// asset Alegreya-Bold SDF is (letters are added to its atlas as they are first
/// printed). The HUD is UI Toolkit and names the same file in its own style
/// sheets — PlayerHUD.uss and QuestComplete.uss.
/// </summary>
public static class RunFont
{
    /// <summary>Under a Resources folder, without the extension.</summary>
    private const string ResourcePath = "Fonts/Jersey10-Regular";

    /// <summary>How many of the font's own pixels tall its em square is: 1400 font units, 75 to the pixel.</summary>
    private const float EmPixels = 1400f / 75f;

    /// <summary>The art is 32 pixels to the world unit.</summary>
    private const float ArtPixelsPerUnit = 32f;

    /// <summary>
    /// Multiply a size that was chosen for Liberation Sans by this and the digits
    /// stay the height they were. Jersey's fill less of its em square.
    /// </summary>
    public const float VersusLiberation = 1.3f;

    /// <summary>
    /// Six atlas pixels to each of the font's pixels. The letters are stored as a
    /// distance field, which rounds a corner by a fraction of an atlas pixel; at
    /// this size that is too little to see on the biggest damage number.
    /// </summary>
    private const int SamplingSize = 112;
    private const int AtlasPadding = 11;
    private const int AtlasSize = 1024;

    /// <summary>
    /// How much is shaved off the strokes of a damage number (owner, 2026-10-13:
    /// "slightly too thick", looking at a 10). TMP's Face Dilate, where 0 is the
    /// font as drawn and -1 is as thin as it goes. This takes about a sixth off:
    /// a stroke two of the font's pixels wide prints about one and two thirds.
    /// </summary>
    private const float DamageNumberDilate = -0.18f;

    /// <summary>
    /// What a damage number is made of. Put into the atlas when the font is built,
    /// so the first hit of a run does not stop to draw them.
    /// </summary>
    private const string Numerals = "0123456789+-";

    private static TMP_FontAsset _asset;
    private static Material _damageMaterial;
    private static bool _tried;

    /// <summary>
    /// The font size, for text standing in the arena, at which one pixel of the
    /// font covers <paramref name="artPixels"/> pixels of the art. 1 is pixel for
    /// pixel with the sprites; a half is the smallest that still reads.
    /// </summary>
    public static float WorldSize(float artPixels)
    {
        // A world-space TextMeshPro em is a tenth of a unit per point of font size.
        return artPixels * EmPixels / ArtPixelsPerUnit * 10f;
    }

    /// <summary>
    /// Builds the font before anything needs it. Without this the first thing to
    /// ask would be the first damage number of the run, in the middle of a fight.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BuildEarly()
    {
        _ = Asset;
    }

    /// <summary>Null only if the .ttf is missing, in which case text keeps the font it had.</summary>
    public static TMP_FontAsset Asset
    {
        get
        {
            if (_asset != null || _tried) return _asset;
            _tried = true;

            var font = Resources.Load<Font>(ResourcePath);
            if (font == null)
            {
                Debug.LogWarning("[RunFont] Resources/" + ResourcePath + ".ttf is missing — run text keeps its old font.");
                return null;
            }

            _asset = TMP_FontAsset.CreateFontAsset(font, SamplingSize, AtlasPadding, GlyphRenderMode.SDFAA,
                                                   AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
            if (_asset == null) return null;

            _asset.name = "Jersey 10 (run font)";
            _asset.hideFlags = HideFlags.HideAndDontSave;

            // A pixel font has no bold and no italic, and faking them is what TMP
            // does by default: it fattens the strokes and leans the letters, and
            // both smear the grid. With these at zero a <b> or an <i> in a line is
            // drawn exactly like the words around it.
            _asset.boldStyle = 0f;
            _asset.boldSpacing = 0f;
            _asset.italicStyle = 0;
            if (_asset.material != null) _asset.material.SetFloat(ShaderUtilities.ID_WeightBold, 0f);

            _asset.TryAddCharacters(Numerals);

            return _asset;
        }
    }

    /// <summary>Sets <paramref name="text"/> in the run font, plain. Its size is the caller's to choose.</summary>
    public static void Apply(TMP_Text text)
    {
        var asset = Asset;
        if (text == null || asset == null) return;

        text.font = asset;
        text.fontStyle = FontStyles.Normal;
    }

    /// <summary>
    /// The run font for a damage number: the same letters with a little taken off
    /// their strokes — see <see cref="DamageNumberDilate"/>.
    ///
    /// One material for every number, made the first time it is asked for. It is
    /// a copy of the font's own, so it draws from the same atlas; the numerals
    /// are the first things put into that atlas, so they are always on the one
    /// sheet this copy points at.
    /// </summary>
    public static void ApplyToDamageNumber(TMP_Text text)
    {
        var asset = Asset;
        if (text == null || asset == null) return;

        Apply(text);

        if (_damageMaterial == null && asset.material != null)
        {
            _damageMaterial = new Material(asset.material)
            {
                name = "Jersey 10 (damage numbers)",
                hideFlags = HideFlags.HideAndDontSave,
            };
            _damageMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, DamageNumberDilate);
        }
        if (_damageMaterial != null) text.fontSharedMaterial = _damageMaterial;
    }
}
