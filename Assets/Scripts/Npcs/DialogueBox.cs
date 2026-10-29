using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// The text box the NPCs speak through: world space, below the knights, typing one
/// letter at a time in that NPC's voice.
///
/// It is the cinematic trailer's box (owner, 2026-10-13; Two Knights
/// Trailer/v3/src/ui.js, TK.dialog), measured off it and redrawn on the art's
/// pixel grid: the gold-edged plate with a diamond on each corner, the speaker's
/// face in a square at the left, their name on a tab that sits across the top
/// edge, pale words with a shadow under them, and a gold arrow in the corner
/// that bobs once the page is waiting for a press.
///
/// World space rather than a UIDocument overlay, and built in code rather than
/// authored into a scene, for the reasons TutorialText already documents — it should
/// read as part of the arena the player is looking at, and it only exists while
/// somebody is talking.
/// </summary>
public class DialogueBox : MonoBehaviour
{
    // ---- measurements, in the art's pixels (a third of the trailer's 1080p ones) ----

    /// <summary>The trailer's 196 screen pixels.</summary>
    private const int PlateHeight = 66;

    private const int PortraitSide = PlateHeight - QuestBackdrop.PortraitInset * 2;
    private const int PortraitRight = QuestBackdrop.PortraitInset + PortraitSide;

    /// <summary>The art is drawn at three times the size it stands at on the board, as the trailer draws it.</summary>
    private const int PortraitZoom = 3;
    /// <summary>How much of the NPC's sprite the square shows, in the sprite's own pixels.</summary>
    private const int PortraitWindow = 16;
    /// <summary>A pixel of dark between the face and the gold line round it.</summary>
    private const int PortraitMargin = (PortraitSide - PortraitWindow * PortraitZoom) / 2;

    /// <summary>The name tab: this far right of the portrait, this far above the plate's top edge, this tall.</summary>
    private const int TabGap = 9;
    private const int TabRise = 9;
    private const int TabHeight = 17;
    private const int TabPadding = 8;

    /// <summary>The words: this far right of the portrait, this far below the plate's top edge.</summary>
    private const int BodyGap = 11;
    private const int BodyTop = 16;
    private const int BodyRightMargin = 19;
    private const int BodyBottomMargin = 7;

    /// <summary>The arrow's middle, from the plate's right and bottom edges.</summary>
    private const float ArrowFromRight = 14.5f;
    private const float ArrowFromBottom = 13f;

    // ---- colours ----

    /// <summary>The trailer's parchment, #ECE4D3.</summary>
    private static readonly Color Parchment = new Color32(236, 228, 211, 255);

    /// <summary>The name's gold, lighter at the top of each letter than at its foot.</summary>
    private static readonly Color NameTop = new Color32(248, 222, 150, 255);
    private static readonly Color NameBottom = new Color32(180, 128, 55, 255);

    private const float ShadowAlpha = 0.6f;
    /// <summary>The trailer's two screen pixels right and three down.</summary>
    private static readonly Vector3 ShadowOffset = new Vector3(2f / 96f, -3f / 96f, 0f);

    // ---- sizes ----

    /// <summary>
    /// How big one of the font's pixels is, in the art's pixels. A page is set at
    /// the first of these it fits at: two lines pixel for pixel with the art,
    /// which is what nearly every page is, then three smaller, then four.
    /// </summary>
    private static readonly float[] BodyScales = { 1f, 2f / 3f, 0.5f };
    private const float NameScale = 2f / 3f;

    // Above every sprite in the arena. NOT a new sorting layer — adding one leaves
    // every sprite on it unlit black until each Light2D is told about it.
    private const string SortingLayer = "Default";
    private const int PortraitOrder = 597;
    private const int ArrowOrder = 598;
    private const int ShadowOrder = 599;
    private const int TextOrder = 600;

    private const float FadeSeconds = 0.25f;
    /// <summary>The box comes up from this far below where it stands.</summary>
    private const float RisePixels = 8f;

    /// <summary>The arrow moves a pixel up and a pixel down, about a bob and a quarter a second.</summary>
    private const float ArrowBobSpeed = 8f;
    private const float ArrowBobPixels = 1.33f;

    /// <summary>A blip every Nth visible character, not every one — a per-letter
    /// chatter is a drone rather than a voice.</summary>
    private const int LettersPerBlip = 3;

    /// <summary>
    /// An NPC's face, as the layers of their sprite it is drawn in. All but the
    /// King are one layer; he is three, and the top one is his crown.
    /// </summary>
    private sealed class PortraitArt
    {
        /// <summary>Bottom layer first. Null where a layer has nothing inside the square.</summary>
        public Sprite[] Layers = new Sprite[0];
        /// <summary>Which layer is the King's crown, or -1.</summary>
        public int Crown = -1;
    }

    private static readonly Dictionary<NpcId, PortraitArt> Portraits = new Dictionary<NpcId, PortraitArt>();
    private static Sprite _arrowSprite;

    private Transform _content;
    private QuestBackdrop _backdrop;
    private readonly List<SpriteRenderer> _portraitLayers = new List<SpriteRenderer>();
    private int _crownLayer = -1;
    private bool _crownVisible;
    private SpriteRenderer _tab;
    private SpriteRenderer _arrow;
    private TextMeshPro _name;
    private TextMeshPro _label;
    private TextMeshPro _shadow;

    private int _width;
    private Vector2 _bodySize;
    private float _alpha;
    private bool _waiting;
    private NpcId _speaker = NpcId.None;
    private bool _speakerSet;

    public static DialogueBox Create(WaveName banner)
    {
        var go = new GameObject("Dialogue Box");
        go.transform.position = new Vector3(0f, QuestBackdrop.CenterYFor(PlateHeight), 0f);
        var box = go.AddComponent<DialogueBox>();
        box.Build();
        return box;
    }

    private void Build()
    {
        _width = QuestBackdrop.WidthPixels;

        // Everything hangs off one object so the whole box can rise as it fades in.
        _content = new GameObject("Dialogue Content").transform;
        _content.SetParent(transform, false);

        _backdrop = QuestBackdrop.Create(_content, PlateHeight, true);

        // The face is put in when somebody speaks — see SetSpeaker.

        _tab = MakeRenderer("Dialogue Name Tab", PortraitOrder);
        _name = MakeLabel("Dialogue Name", TextOrder, TextAlignmentOptions.Capline);
        _name.fontSize = RunFont.WorldSize(NameScale);
        _name.textWrappingMode = TextWrappingModes.NoWrap;
        _name.color = Color.white;
        _name.enableVertexGradient = true;
        _name.colorGradient = new VertexGradient(NameTop, NameTop, NameBottom, NameBottom);

        // The words, top-left of the space to the right of the portrait. Prose
        // reads from the top-left corner; centring it made a one-line page and a
        // two-line page start in different places.
        int bodyLeft = PortraitRight + BodyGap;
        int bodyWidth = _width - bodyLeft - BodyRightMargin;
        int bodyHeight = PlateHeight - BodyTop - BodyBottomMargin;
        _bodySize = new Vector2(bodyWidth, bodyHeight) * QuestBackdrop.Pixel;
        Vector3 bodyCentre = At(bodyLeft + bodyWidth * 0.5f, BodyTop + bodyHeight * 0.5f);

        // The same words again in black, a little down and to the right, under
        // the real ones. A second label rather than a shadow on the font's
        // material, which would need a shader variant nothing else in the game uses.
        _shadow = MakeLabel("Dialogue Shadow", ShadowOrder, TextAlignmentOptions.TopLeft);
        _shadow.rectTransform.sizeDelta = _bodySize;
        _shadow.transform.localPosition = bodyCentre + ShadowOffset;
        _shadow.color = Color.black;

        _label = MakeLabel("Dialogue Body", TextOrder, TextAlignmentOptions.TopLeft);
        _label.rectTransform.sizeDelta = _bodySize;
        _label.transform.localPosition = bodyCentre;
        _label.color = Parchment;

        _arrow = MakeRenderer("Dialogue Arrow", ArrowOrder);
        _arrow.sprite = ArrowSprite;
        QuestBackdrop.Unlight(_arrow);
        _arrow.transform.localPosition = ArrowRest;
        _arrow.enabled = false;

        SetAlpha(0f);
    }

    /// <summary>
    /// A point on the plate, counted in the art's pixels from its left edge and
    /// down from its top edge, in the box's own coordinates.
    /// </summary>
    private Vector3 At(float fromLeft, float fromTop)
    {
        return new Vector3((-_width * 0.5f + fromLeft) * QuestBackdrop.Pixel,
                           (PlateHeight * 0.5f - fromTop) * QuestBackdrop.Pixel,
                           0f);
    }

    private Vector3 ArrowRest => At(_width - ArrowFromRight, PlateHeight - ArrowFromBottom);

    private SpriteRenderer MakeRenderer(string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_content, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sortingLayerName = SortingLayer;
        renderer.sortingOrder = order;
        return renderer;
    }

    private TextMeshPro MakeLabel(string name, int order, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_content, false);
        var label = go.AddComponent<TextMeshPro>();

        // The run's pixel font. Failing to find it is not worth stopping for — TMP
        // falls back to its default and the scene still reads.
        RunFont.Apply(label);

        label.alignment = align;
        label.enableAutoSizing = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.fontSize = RunFont.WorldSize(BodyScales[0]);
        label.text = string.Empty;
        label.alpha = 0f;

        var mesh = go.GetComponent<MeshRenderer>();
        if (mesh != null)
        {
            mesh.sortingLayerName = SortingLayer;
            mesh.sortingOrder = order;
        }
        return label;
    }

    // ---------- who is speaking ----------

    /// <summary>
    /// Puts the speaker's face in the square and their name on the tab. The name
    /// is not something the NPC says, so it is not typed out with the line.
    /// </summary>
    private void SetSpeaker(NpcId speaker)
    {
        if (_speakerSet && speaker == _speaker) return;
        _speaker = speaker;
        _speakerSet = true;

        // The face, a renderer for each layer of the NPC's sprite. Each layer is
        // cut with the bottom-left corner of what the square shows as its pivot,
        // so every one of them is placed on that same corner.
        PortraitArt art = PortraitFor(speaker);
        int layers = art != null ? art.Layers.Length : 0;
        while (_portraitLayers.Count < layers)
        {
            int index = _portraitLayers.Count;
            // One order apart, bottom layer first, and never as high as the words.
            var layer = MakeRenderer("Dialogue Portrait " + index, Mathf.Min(PortraitOrder + index, ShadowOrder));
            layer.transform.localPosition = At(QuestBackdrop.PortraitInset + PortraitMargin,
                                               PlateHeight - QuestBackdrop.PortraitInset - PortraitMargin);
            _portraitLayers.Add(layer);
        }
        for (int i = 0; i < _portraitLayers.Count; i++)
        {
            _portraitLayers[i].sprite = i < layers ? art.Layers[i] : null;
            QuestBackdrop.Unlight(_portraitLayers[i]);
        }
        _crownLayer = art != null ? art.Crown : -1;

        // And the colour behind it, which is each NPC's own.
        _backdrop.SetPortraitFill(PortraitBacking(speaker));

        // In capitals, as the trailer's small capitals nearly are. The pixel font
        // has no small capitals of its own.
        string name = QuestLog.NpcName(speaker);
        bool named = !string.IsNullOrEmpty(name);
        if (named) name = name.ToUpperInvariant();
        _name.text = named ? name : string.Empty;
        _tab.sprite = null;

        if (named)
        {
            // The tab is as wide as the name and a little more, so it is drawn for
            // this name and no other.
            float nameWidth = _name.GetPreferredValues(name).x;
            int tabWidth = Mathf.CeilToInt(nameWidth / QuestBackdrop.Pixel) + TabPadding * 2;
            _tab.sprite = QuestBackdrop.Frame(tabWidth, TabHeight);
            QuestBackdrop.Unlight(_tab);

            // A frame's pivot is its middle, and the tab is an odd number of pixels
            // tall, so its middle is placed on a half pixel to keep its edges whole.
            Vector3 tabCentre = At(PortraitRight + TabGap + tabWidth * 0.5f, -TabRise + TabHeight * 0.5f);
            _tab.transform.localPosition = tabCentre;
            _name.rectTransform.sizeDelta = new Vector2(tabWidth, TabHeight) * QuestBackdrop.Pixel;
            _name.transform.localPosition = tabCentre;
        }

        ShowParts();
    }

    /// <summary>
    /// Whether the King's portrait wears his crown. The scene says so whenever it
    /// puts the crown on him or takes it off, so the face in the box and the
    /// figure on the board always agree (owner, 2026-10-13).
    /// </summary>
    public void SetCrownVisible(bool visible)
    {
        _crownVisible = visible;
        ShowParts();
    }

    /// <summary>
    /// Switches each piece of the box on or off. Nothing is drawn while the box
    /// is fully faded out, a layer with nothing in the square is never drawn, and
    /// the crown is drawn only while the King has it.
    /// </summary>
    private void ShowParts()
    {
        bool shown = _alpha > 0f;
        for (int i = 0; i < _portraitLayers.Count; i++)
        {
            var layer = _portraitLayers[i];
            layer.enabled = shown && layer.sprite != null && (i != _crownLayer || _crownVisible);
        }
        if (_tab != null) _tab.enabled = shown && _tab.sprite != null;
    }

    /// <summary>
    /// The colour behind each NPC's face (owner, 2026-10-13). The words are his;
    /// the exact shades are picked to sit under each sprite without swallowing it.
    ///
    ///   The King          royal blue, a little pale (first a soft grey-blue; changed the same
    ///                     day). Royal blue's own hue with some of the colour taken out. The
    ///                     grey-blue was as light as his crown, which is what stands against
    ///                     it, so the crown sank in; this is darker than the gold and lighter
    ///                     than his hair and cape. Going darker still takes it from the cape.
    ///   The Cartographer  jungle green (first light tan like a map; changed the same day)
    ///   The Wizard        pale yellow, like a moon
    ///   The Ninja         dark crimson red, like blood
    ///   The Paladin       near black
    ///
    /// Anyone else gets the trailer's near black.
    /// </summary>
    private static Color32 PortraitBacking(NpcId npc)
    {
        switch (npc)
        {
            case NpcId.King: return new Color32(88, 118, 208, 255);
            case NpcId.Cartographer: return new Color32(44, 106, 64, 255);
            case NpcId.Wizard: return new Color32(240, 232, 178, 255);
            case NpcId.Ninja: return new Color32(112, 18, 26, 255);
            case NpcId.Paladin: return new Color32(14, 14, 18, 255);
            default: return new Color32(13, 10, 6, 255);
        }
    }

    /// <summary>
    /// How far above the feet the middle of each NPC's face is, in the art's
    /// pixels. The trailer's own numbers: its portraits are centred on the same
    /// spot of the same 32 by 32 sprites.
    /// </summary>
    private static int FaceHeight(NpcId npc)
    {
        switch (npc)
        {
            case NpcId.Cartographer:
            case NpcId.Paladin:
                return 21;
            case NpcId.Ninja:
                return 19;
            default:
                return 20;
        }
    }

    /// <summary>
    /// The square of the NPC's standing sprite around their face, one cut for
    /// each layer the sprite is drawn in. Read off the NPC's own prefab, bottom
    /// layer first, so the King comes with his cape and his crown as separate
    /// pieces and the crown can be left off. Made once for each NPC. Null if the
    /// catalog has nobody by that name.
    /// </summary>
    private static PortraitArt PortraitFor(NpcId npc)
    {
        if (Portraits.TryGetValue(npc, out PortraitArt made) && made != null) return made;

        var catalog = NpcCatalog.Instance;
        var entry = catalog != null ? catalog.Find(npc) : null;
        if (entry == null) return null;

        made = new PortraitArt();
        bool any = false;

        SpriteRenderer[] layers = entry.prefab != null
            ? entry.prefab.GetComponentsInChildren<SpriteRenderer>(true)
            : new SpriteRenderer[0];
        if (layers.Length > 0)
        {
            // The importer writes each layer's place in the stack into its
            // sortingOrder, counted from the bottom. NpcActor finds the crown
            // the same way: the top layer of a figure with three or more.
            Array.Sort(layers, (a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
            Vector3 root = entry.prefab.transform.position;

            made.Layers = new Sprite[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                made.Layers[i] = Cut(layers[i].sprite, layers[i].transform.position - root,
                                     FaceHeight(npc), layers.Length == 1, npc + " Portrait " + i);
                any |= made.Layers[i] != null;
            }
            if (npc == NpcId.King && layers.Length >= 3) made.Crown = layers.Length - 1;
        }

        // No prefab, or nothing usable in it: the catalog's own standing sprite.
        if (!any)
        {
            made.Layers = new[] { Cut(entry.portrait, Vector2.zero, FaceHeight(npc), true, npc + " Portrait") };
            made.Crown = -1;
        }

        Portraits[npc] = made;
        return made;
    }

    /// <summary>
    /// The part of <paramref name="source"/> that falls inside the portrait's
    /// square, as a sprite of its own three times the size. Cut from the same
    /// texture, so nothing is copied. Null if none of it is inside.
    ///
    /// <paramref name="offset"/> is where this layer's renderer sits relative to
    /// the figure's own origin, which is the point it stands on.
    /// </summary>
    private static Sprite Cut(Sprite source, Vector2 offset, int faceHeight, bool alone, string name)
    {
        if (source == null) return null;

        try
        {
            // Where the sprite sits in its texture, and the point in it the
            // figure stands on. A sprite is drawn with its pivot on its renderer.
            Rect rect = source.textureRect;
            Vector2 origin = rect.position - source.textureRectOffset;
            Vector2 feet = origin + source.pivot - offset * source.pixelsPerUnit;

            // A single sprite whose pivot is somewhere other than its feet:
            // stand it on the middle of its bottom edge instead.
            if (alone && source.pivot.y > 2f) feet = new Vector2(rect.center.x, rect.yMin);

            float half = PortraitWindow * 0.5f;
            var window = new Rect(Mathf.Round(feet.x - half), Mathf.Round(feet.y + faceHeight - half),
                                  PortraitWindow, PortraitWindow);

            // Only the part of the window that is inside this sprite. The texture
            // holds other frames beside it, and they must not show at the edges.
            float xMin = Mathf.Max(window.xMin, rect.xMin);
            float yMin = Mathf.Max(window.yMin, rect.yMin);
            float xMax = Mathf.Min(window.xMax, rect.xMax);
            float yMax = Mathf.Min(window.yMax, rect.yMax);
            if (xMax <= xMin || yMax <= yMin) return null;

            var part = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
            // The pivot is the WINDOW's bottom-left corner, which may lie outside
            // the part kept, so the face lands in the same place however much was
            // trimmed, and every layer of it lines up with the others.
            var pivot = new Vector2((window.xMin - part.xMin) / part.width, (window.yMin - part.yMin) / part.height);

            Sprite made = Sprite.Create(source.texture, part, pivot, source.pixelsPerUnit / PortraitZoom,
                                        0, SpriteMeshType.FullRect);
            made.name = name;
            made.hideFlags = HideFlags.HideAndDontSave;
            return made;
        }
        catch (UnityException)
        {
            // A sprite packed in a way that hides its place in the texture. The
            // square stays empty rather than the scene stopping.
            return null;
        }
    }

    /// <summary>The arrow: seven of the art's pixels wide at the top, coming to a point four rows down.</summary>
    private static Sprite ArrowSprite
    {
        get
        {
            if (_arrowSprite != null) return _arrowSprite;

            const int width = 7;
            const int height = 4;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                // Rows run bottom to top: one pixel at the point, seven at the top.
                int inset = height - 1 - y;
                for (int x = 0; x < width; x++)
                {
                    bool on = x >= inset && x < width - inset;
                    pixels[y * width + x] = on ? QuestBackdrop.Gold : new Color32(0, 0, 0, 0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            _arrowSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f),
                                         QuestBackdrop.ArtPixelsPerUnit, 0, SpriteMeshType.FullRect);
            _arrowSprite.name = "Dialogue Arrow";
            _arrowSprite.hideFlags = HideFlags.HideAndDontSave;
            return _arrowSprite;
        }
    }

    // ---------- speaking ----------

    /// <summary>
    /// Types one page out and waits for the player to acknowledge it.
    ///
    /// <paramref name="onDirective"/> runs before the first character, for the
    /// staging beats that sit between pages — the King's crown, his walk away.
    /// Returned as a coroutine so a directive can take as long as it needs.
    /// </summary>
    public IEnumerator Speak(DialogueScript.Page page, NpcId speaker, float pitch,
                             Func<DialogueScript.Directive, IEnumerator> onDirective)
    {
        if (page.Before != DialogueScript.Directive.None && onDirective != null)
        {
            yield return onDirective(page.Before);
        }
        if (page.IsEmpty) yield break;

        SetSpeaker(speaker);

        // The whole page is laid out before any of it is shown, and the letters
        // are then uncovered one at a time. Typing them into the label instead
        // let a word start on one line and jump to the next when it outgrew it.
        SetPage(page.PlainText);
        Reveal(0);

        if (_alpha < 1f) yield return FadeBody(1f);

        // The press that acknowledged the PREVIOUS page is still down on the frame
        // this one starts, and wasPressedThisFrame is true for all of it. Without
        // this grace every page after the first read as already-rushed and appeared
        // whole — which is exactly what "only the first box types" was.
        float armedAt = Time.unscaledTime + 0.12f;
        while (Time.unscaledTime < armedAt) yield return null;

        var voice = NpcCatalog.VoiceFor(speaker);
        int sinceBlip = 0;

        for (int i = 0; i < page.Letters.Count; i++)
        {
            var letter = page.Letters[i];
            Reveal(i + 1);

            // Whitespace is not a sound. Without this the voice keeps ticking
            // through the gaps and the rhythm stops matching the words.
            if (!char.IsWhiteSpace(letter.Character) && ++sinceBlip >= LettersPerBlip)
            {
                sinceBlip = 0;
                if (voice != null) AudioManager.Instance?.PlayVoice(voice, pitch);
            }

            // A press mid-page fills the rest of it in immediately rather than
            // advancing — the player asked to read it, not to skip it. The second
            // press then moves on, which is the convention everywhere else.
            //
            // LEAVING THE LOOP is what makes that true, and it is an audio fix as
            // much as a text one. This used to set a "rushed" flag and keep
            // iterating, which ran every remaining letter of the page inside a
            // single frame — and fired every remaining letter's BLIP with it. Fifty
            // one-shots of the same clip starting on the same frame do not read as
            // a voice talking quickly, they sum into one clipped bang, which is
            // what "all the voice sfx play at once really loud" is. Nothing is lost
            // by stopping here: the line below shows the finished page outright,
            // so those extra iterations only ever added the noise.
            if (QuestSceneInput.AdvancePressed()) break;

            float wait = DialogueScript.SecondsFor(letter);
            float until = Time.unscaledTime + wait;
            bool rushed = false;
            while (Time.unscaledTime < until)
            {
                if (QuestSceneInput.AdvancePressed()) { rushed = true; break; }
                yield return null;
            }
            if (rushed) break;
        }

        Reveal(int.MaxValue);
        yield return WaitForAcknowledgement();
    }

    /// <summary>
    /// Sets the page's words at the biggest size they fit at. Stepped rather than
    /// squeezed to fit, so the font's pixels are always a whole number of the
    /// screen's at 1080p.
    /// </summary>
    private void SetPage(string text)
    {
        _label.enableAutoSizing = false;

        bool fits = false;
        for (int i = 0; i < BodyScales.Length && !fits; i++)
        {
            _label.fontSize = RunFont.WorldSize(BodyScales[i]);
            float needed = _label.GetPreferredValues(text, _bodySize.x, 1000f).y;
            fits = needed <= _bodySize.y + QuestBackdrop.Pixel * 0.5f;
        }

        if (!fits)
        {
            // Longer than four small lines. Nothing written for the game is, but
            // if a page ever is, it is squeezed in rather than running off the plate.
            _label.fontSizeMax = RunFont.WorldSize(BodyScales[BodyScales.Length - 1]);
            _label.fontSizeMin = _label.fontSizeMax * 0.5f;
            _label.enableAutoSizing = true;
        }

        _label.text = text;
        _label.ForceMeshUpdate();

        _shadow.fontSize = _label.fontSize;
        _shadow.text = text;
    }

    /// <summary>Shows the first <paramref name="letters"/> letters of the page, in the words and in their shadow.</summary>
    private void Reveal(int letters)
    {
        _label.maxVisibleCharacters = letters;
        _shadow.maxVisibleCharacters = letters;
    }

    private IEnumerator WaitForAcknowledgement()
    {
        _waiting = true;

        // The grace exists because the press that finished the typewriter is often
        // still down. Same reasoning as WaveSurvivedPanel's input guard.
        float readyAt = Time.unscaledTime + 0.18f;
        while (Time.unscaledTime < readyAt) yield return null;
        while (!QuestSceneInput.AdvancePressed()) yield return null;

        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiConfirm);
        _waiting = false;
        _arrow.enabled = false;
    }

    /// <summary>
    /// The arrow in the corner: up while the page is waiting for a press, and
    /// bobbing. Unscaled time, because the game is frozen for the whole scene.
    /// </summary>
    private void Update()
    {
        if (_arrow == null) return;

        _arrow.enabled = _waiting;
        if (!_waiting) return;

        // Whole pixels, so the arrow steps rather than slides.
        float bob = Mathf.Round(Mathf.Sin(Time.unscaledTime * ArrowBobSpeed) * ArrowBobPixels);
        _arrow.transform.localPosition = ArrowRest + Vector3.down * (bob * QuestBackdrop.Pixel);
    }

    public IEnumerator Hide()
    {
        _waiting = false;
        _arrow.enabled = false;
        yield return FadeBody(0f);
        _label.text = "";
        _shadow.text = "";
    }

    /// <summary>
    /// The whole box, up or down. On the way up it also rises the last few pixels
    /// into place, quickly at first and settling, as the trailer's does.
    /// </summary>
    private IEnumerator FadeBody(float to)
    {
        float from = _alpha;
        bool rising = to > from;

        if (!Mathf.Approximately(from, to))
        {
            float elapsed = 0f;
            while (elapsed < FadeSeconds)
            {
                // Unscaled: the whole scene runs with the game frozen behind it.
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / FadeSeconds);
                SetAlpha(Mathf.Lerp(from, to, t));
                if (rising)
                {
                    float settled = 1f - (1f - t) * (1f - t) * (1f - t);
                    _content.localPosition = Vector3.down * ((1f - settled) * RisePixels * QuestBackdrop.Pixel);
                }
                yield return null;
            }
        }

        SetAlpha(to);
        _content.localPosition = Vector3.zero;
    }

    private void SetAlpha(float alpha)
    {
        _alpha = alpha;
        _backdrop.SetAlpha(alpha);

        var tint = new Color(1f, 1f, 1f, alpha);
        for (int i = 0; i < _portraitLayers.Count; i++) _portraitLayers[i].color = tint;
        _tab.color = tint;
        _arrow.color = tint;
        ShowParts();

        _name.alpha = alpha;
        _label.alpha = alpha;
        _shadow.alpha = alpha * ShadowAlpha;
    }
}
