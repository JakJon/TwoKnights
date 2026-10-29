using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The plate a quest scene's words sit on: the panel behind the dialogue, the
/// rewards and the objective cards.
///
/// Gold text over the arena was legible against grass and unreadable against a
/// fire field, a lit cave wall or a pile of bodies — the contrast was whatever the
/// backdrop happened to be that wave. The plate makes it the same every time.
///
/// It is drawn the way the cinematic trailer draws its text box (owner,
/// 2026-10-13; Two Knights Trailer/v3/src/ui.js, panel()): a dark fill that gets
/// darker toward the bottom, a gold line round the edge, a thin dark line just
/// inside it, and a gold diamond on each corner.
///
/// The dialogue's plate is the trailer's own shape and has a square for the
/// speaker's portrait; the reward and quest cards keep the taller plate their
/// lists were laid out for. Both are the same width and stand on the same line
/// at the foot of the screen, so one reads as the other grown taller rather than
/// as a second panel.
///
/// Built in code, a pixel at a time and at the art's own 32 pixels to the unit,
/// so its lines sit on the same grid as everything else on the screen. No
/// prefab and no scene object.
/// </summary>
public class QuestBackdrop : MonoBehaviour
{
    // Under the text (600) and over everything in the arena.
    private const string SortingLayer = "Default";
    private const int PanelOrder = 596;

    // ---- the trailer's colours ----

    /// <summary>The fill, top and bottom. Very slightly see-through, as it is there.</summary>
    private static readonly Color32 FillTop = new Color32(33, 26, 16, 245);
    private static readonly Color32 FillBottom = new Color32(18, 14, 9, 247);

    /// <summary>The Gilded Vigil gold, #D4A24A. The diamonds, and anything gold drawn on the plate.</summary>
    public static readonly Color32 Gold = new Color32(212, 162, 74, 255);

    /// <summary>The line round the edge: that gold at nine tenths over the fill.</summary>
    private static readonly Color32 EdgeGold = new Color32(194, 148, 68, 255);

    /// <summary>The thin line inside the edge, #3A3222.</summary>
    private static readonly Color32 Hairline = new Color32(58, 50, 34, 255);

    /// <summary>
    /// Behind the portrait when nobody has said otherwise — the trailer's near
    /// black — and the gold line round it (eight tenths over the fill). Each NPC
    /// has a colour of their own behind their face; see DialogueBox.
    /// </summary>
    private static readonly Color32 PortraitFill = new Color32(13, 10, 6, 255);
    private static readonly Color32 PortraitEdge = new Color32(176, 134, 61, 255);

    // ---- its measurements, in the art's pixels ----

    public const float ArtPixelsPerUnit = 32f;

    /// <summary>One of the art's pixels, in world units.</summary>
    public const float Pixel = 1f / ArtPixelsPerUnit;

    /// <summary>The trailer's box is 1180 wide on a 1920 screen, which is 394 of the art's pixels.</summary>
    private const int MaxWidthPixels = 394;
    /// <summary>How much of a narrow view it may take, so it never runs off the sides.</summary>
    private const float ViewFraction = 0.752f;

    /// <summary>The cards' plate. The dialogue's is shorter — see DialogueBox.</summary>
    public const int HeightPixels = 95;

    /// <summary>How far the plate's bottom edge sits above the bottom of the view. The trailer's 70 screen pixels.</summary>
    private const int LiftPixels = 23;

    /// <summary>How far in from the edge the thin inner line runs.</summary>
    private const int HairlineInset = 2;

    /// <summary>A corner diamond reaches this far out past the plate, so the sprite is that much bigger all round.</summary>
    private const int DiamondReach = 3;

    /// <summary>The portrait square: this far in from the plate's left and top, and as far from its bottom.</summary>
    public const int PortraitInset = 7;

    /// <summary>Breathing room between the plate's edge and the text inside it.</summary>
    public const int PaddingPixels = 8;
    public const float Padding = PaddingPixels * Pixel;

    private static readonly Dictionary<long, Sprite> Plates = new Dictionary<long, Sprite>();
    private static readonly Dictionary<long, Sprite> Frames = new Dictionary<long, Sprite>();
    private static readonly Dictionary<long, Sprite> Outlines = new Dictionary<long, Sprite>();
    private static readonly Dictionary<int, Material> UnlitMaterials = new Dictionary<int, Material>();
    private static Shader _unlitShader;
    private static bool _unlitLooked;

    private SpriteRenderer _renderer;
    private int _widthPixels;
    private int _heightPixels;
    private bool _portrait;

    /// <summary>
    /// The one width every panel in a quest scene uses, in the art's pixels. Even,
    /// so a plate centred on the screen has both edges on the pixel grid.
    /// </summary>
    public static int WidthPixels
    {
        get
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return MaxWidthPixels;
            int fits = Mathf.FloorToInt(cam.orthographicSize * cam.aspect * 2f * ViewFraction * ArtPixelsPerUnit);
            return Mathf.Min(MaxWidthPixels, fits - (fits & 1));
        }
    }

    /// <summary>The same width in world units.</summary>
    public static float Width => WidthPixels * Pixel;

    /// <summary>How tall the cards' plate stands.</summary>
    public const float Height = HeightPixels * Pixel;

    /// <summary>How wide the text inside the plate may run.</summary>
    public static float TextWidth => Width - Padding * 2f;
    /// <summary>How tall the text inside the cards' plate may run.</summary>
    public static float TextHeight => Height - Padding * 2f;

    /// <summary>Where the centre of the cards' plate goes, in world space.</summary>
    public static float CenterY => CenterYFor(HeightPixels);

    /// <summary>Where the centre of a plate <paramref name="heightPixels"/> tall goes: every plate stands on the same line.</summary>
    public static float CenterYFor(int heightPixels)
    {
        return ViewBottom + (LiftPixels + heightPixels * 0.5f) * Pixel;
    }

    /// <summary>
    /// The bottom edge of what the player can see. Everything in a quest scene is
    /// placed up from here rather than from a hardcoded y, because the anchors that
    /// were hardcoded put the "press to continue" line off the bottom of the screen.
    /// </summary>
    public static float ViewBottom
    {
        get
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return -5.625f;
            return cam.transform.position.y - cam.orthographicSize;
        }
    }

    /// <summary>
    /// Where a text block of <paramref name="size"/> goes to sit in the top-left
    /// corner of the cards' plate, one padding in from both edges. In the plate's
    /// own coordinates, and a block is centred on its transform, hence the halves.
    /// </summary>
    public static Vector3 TopLeft(Vector2 size)
    {
        return new Vector3(-Width * 0.5f + Padding + size.x * 0.5f,
                           Height * 0.5f - Padding - size.y * 0.5f,
                           0f);
    }

    /// <summary>The mirror of <see cref="TopLeft"/>.</summary>
    public static Vector3 BottomRight(Vector2 size)
    {
        return new Vector3(Width * 0.5f - Padding - size.x * 0.5f,
                           -Height * 0.5f + Padding + size.y * 0.5f,
                           0f);
    }

    /// <summary>The cards' plate.</summary>
    public static QuestBackdrop Create(Transform parent)
    {
        return Create(parent, HeightPixels, false);
    }

    /// <summary>
    /// A plate <paramref name="heightPixels"/> tall, centred on
    /// <paramref name="parent"/>. With <paramref name="portrait"/> it has the dark
    /// square at its left end that the speaker's face is drawn in.
    /// </summary>
    public static QuestBackdrop Create(Transform parent, int heightPixels, bool portrait)
    {
        var go = new GameObject("Backdrop");
        go.transform.SetParent(parent, false);

        var backdrop = go.AddComponent<QuestBackdrop>();
        backdrop._widthPixels = WidthPixels;
        backdrop._heightPixels = heightPixels;
        backdrop._portrait = portrait;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = Plate(backdrop._widthPixels, heightPixels, portrait, PortraitFill);
        renderer.sortingLayerName = SortingLayer;
        renderer.sortingOrder = PanelOrder;
        Unlight(renderer);

        backdrop._renderer = renderer;
        backdrop.SetAlpha(0f);
        return backdrop;
    }

    /// <summary>
    /// Changes the colour behind the portrait. The square is part of the plate's
    /// own picture, so this swaps the plate for one drawn with that colour — made
    /// the first time each colour is asked for and kept. Does nothing on a plate
    /// that has no portrait square.
    /// </summary>
    public void SetPortraitFill(Color32 fill)
    {
        if (!_portrait) return;

        fill.a = 255;
        _renderer.sprite = Plate(_widthPixels, _heightPixels, true, fill);
        Unlight(_renderer);
    }

    /// <summary>Fades with the text it stands behind, rather than on its own schedule.</summary>
    public void SetAlpha(float alpha)
    {
        // White tint: the colours are baked into the sprite, so the tint carries
        // nothing but the fade.
        _renderer.color = new Color(1f, 1f, 1f, alpha);

        // A plate that cannot be seen is not drawn at all. The dialogue's plate
        // and the cards' plate stand in the same place, one of them always
        // invisible, and this keeps the invisible one out of the other's way.
        _renderer.enabled = alpha > 0f;
    }

    /// <summary>
    /// Draws a renderer's sprite at its own colours whatever the map's lights are
    /// doing. The plate is a piece of the interface: its gold has to be the same
    /// gold in the mine as it is in the forest. Sprites/Default is in the
    /// project's always-included shaders (ShieldSight leans on the same); if it
    /// somehow is not, the renderer keeps its ordinary lit material.
    ///
    /// Call it AFTER the renderer has its sprite, and again whenever the sprite
    /// changes, because the material goes with the sprite's texture.
    ///
    /// ONE MATERIAL FOR EACH TEXTURE, never one shared by everything here. The
    /// first version shared one, and the quest cards came up with the dialogue
    /// box's portrait square behind their text (owner, 2026-10-13): the cards'
    /// plate was being drawn with the dialogue plate's picture stretched over
    /// it. Two sprites cut from different textures, drawn through one material
    /// at one sorting order, went out as a single batch with the first one's
    /// texture. A material that belongs to one texture, and has that texture set
    /// on it, cannot be batched with another texture's.
    /// </summary>
    public static void Unlight(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null) return;

        if (!_unlitLooked)
        {
            _unlitLooked = true;
            _unlitShader = Shader.Find("Sprites/Default");
        }
        if (_unlitShader == null) return;

        Texture2D texture = renderer.sprite.texture;
        if (texture == null) return;

        int id = texture.GetInstanceID();
        if (!UnlitMaterials.TryGetValue(id, out Material material) || material == null)
        {
            material = new Material(_unlitShader)
            {
                name = "Quest plate, unlit (" + texture.name + ")",
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = texture,
            };
            UnlitMaterials[id] = material;
        }
        renderer.sharedMaterial = material;
    }

    /// <summary>
    /// The plate, made once for each size asked for. Hard-edged on purpose — the
    /// game is pixel art and a smoothed line would be the only soft edge on the
    /// screen.
    /// </summary>
    private static Sprite Plate(int width, int height, bool portrait, Color32 portraitFill)
    {
        // Size, whether it has the square, and the square's colour.
        long tint = portrait ? ((long)portraitFill.r << 16) | ((long)portraitFill.g << 8) | portraitFill.b : 0L;
        long key = ((long)width << 44) | ((long)height << 32) | (portrait ? 1L << 31 : 0L) | tint;
        if (Plates.TryGetValue(key, out Sprite made) && made != null) return made;

        int reach = DiamondReach;
        int textureWidth = width + reach * 2;
        int textureHeight = height + reach * 2;
        var pixels = new Color32[textureWidth * textureHeight];

        // Texture rows run bottom to top; `down` below counts from the plate's top.
        for (int y = 0; y < height; y++)
        {
            int down = height - 1 - y;
            Color32 fill = Color32.Lerp(FillTop, FillBottom, height > 1 ? down / (float)(height - 1) : 0f);

            for (int x = 0; x < width; x++)
            {
                int edge = Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(y, height - 1 - y));
                Color32 colour = edge == 0 ? EdgeGold : (edge == HairlineInset ? Hairline : fill);

                if (portrait)
                {
                    // A square as tall as the plate allows, the same distance in
                    // from the left as from the top and the bottom.
                    int side = height - PortraitInset * 2;
                    int px = x - PortraitInset;
                    int py = down - PortraitInset;
                    if (px >= 0 && py >= 0 && px < side && py < side)
                    {
                        bool rim = px == 0 || py == 0 || px == side - 1 || py == side - 1;
                        colour = rim ? PortraitEdge : portraitFill;
                    }
                }

                pixels[(y + reach) * textureWidth + x + reach] = colour;
            }
        }

        // A diamond on each corner, centred on the corner itself, so half of it
        // hangs outside the plate: rows two, four, six, six, four and two wide.
        for (int corner = 0; corner < 4; corner++)
        {
            float cx = reach + ((corner & 1) == 0 ? 0f : width);
            float cy = reach + ((corner & 2) == 0 ? 0f : height);
            for (int dy = -reach; dy < reach; dy++)
            {
                for (int dx = -reach; dx < reach; dx++)
                {
                    if (Mathf.Abs(dx + 0.5f) + Mathf.Abs(dy + 0.5f) > reach) continue;
                    int x = Mathf.RoundToInt(cx) + dx;
                    int y = Mathf.RoundToInt(cy) + dy;
                    pixels[y * textureWidth + x] = Gold;
                }
            }
        }

        made = MakeSprite(pixels, textureWidth, textureHeight,
                          portrait ? "Quest Backdrop Plate " + tint.ToString("X6") : "Quest Backdrop Plate");
        Plates[key] = made;
        return made;
    }

    /// <summary>
    /// A plain box in the plate's colours: one flat fill inside one line of gold.
    /// The speaker's name stands in one of these on the dialogue plate's top edge.
    /// </summary>
    public static Sprite Frame(int width, int height)
    {
        long key = ((long)width << 21) | (long)height;
        if (Frames.TryGetValue(key, out Sprite made) && made != null) return made;

        var fill = new Color32(22, 18, 12, 250);
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool rim = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                pixels[y * width + x] = rim ? EdgeGold : fill;
            }
        }

        made = MakeSprite(pixels, width, height, "Quest Backdrop Frame");
        Frames[key] = made;
        return made;
    }

    /// <summary>
    /// A box that is only its line: one pixel of the portrait square's gold all
    /// round and nothing inside, so the plate shows through it. Each reward on
    /// a card stands in one of these.
    ///
    /// The four corner pixels are left out, which is as round as a corner gets
    /// at this size. The top edge has a gap <paramref name="gapWidth"/> pixels
    /// wide, starting <paramref name="gapFrom"/> pixels in from the left, where
    /// the box's label is written across the line.
    /// </summary>
    public static Sprite Outline(int width, int height, int gapFrom, int gapWidth)
    {
        long key = ((long)width << 42) | ((long)height << 28) | ((long)gapFrom << 14) | (long)gapWidth;
        if (Outlines.TryGetValue(key, out Sprite made) && made != null) return made;

        var clear = new Color32(0, 0, 0, 0);
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool side = x == 0 || x == width - 1;
                bool end = y == 0 || y == height - 1;
                bool line = side != end;

                // Texture rows run bottom to top, so the last row is the top edge.
                if (y == height - 1 && x >= gapFrom && x < gapFrom + gapWidth) line = false;

                pixels[y * width + x] = line ? PortraitEdge : clear;
            }
        }

        made = MakeSprite(pixels, width, height, "Quest Backdrop Outline " + gapFrom + "+" + gapWidth);
        Outlines[key] = made;
        return made;
    }

    private static Sprite MakeSprite(Color32[] pixels, int width, int height, string name)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        texture.name = name + " " + width + "x" + height;
        texture.SetPixels32(pixels);
        texture.Apply();

        var sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f),
                                   ArtPixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
