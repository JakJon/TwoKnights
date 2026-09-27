using UnityEngine;

/// <summary>
/// The plate a quest scene's words sit on: one fixed panel behind the dialogue,
/// the rewards and the objective cards.
///
/// Gold text over the arena was legible against grass and unreadable against a
/// fire field, a lit cave wall or a pile of bodies — the contrast was whatever the
/// backdrop happened to be that wave. The plate makes it the same every time.
///
/// FIXED SIZE, deliberately. A panel that hugs its text is a speech bubble, and it
/// makes every line a different shape in a different place; one plate that never
/// moves reads as part of the game rather than as something that appeared. The
/// width is clamped to the camera, and only so a narrow window cannot push it off
/// the sides of the view.
///
/// It wears the camp menus' own colours — the same fill, the same border, the same
/// rounded corners — so the game has one panel rather than a menu panel and an
/// arena panel that merely resemble each other.
///
/// Built in code from a generated nine-slice, like every other visual in this
/// project that is not authored art. No prefab and no scene object.
/// </summary>
public class QuestBackdrop : MonoBehaviour
{
    // Under the text (600) and over everything in the arena.
    private const string SortingLayer = "Default";
    private const int PanelOrder = 596;

    // Straight off CampMenu.uss, where every card in the camp is drawn as a
    // rgb(28,22,14) fill inside a 2px rgb(58,50,34) border with a 5px radius.
    private static readonly Color32 FillColor = new Color32(28, 22, 14, 255);
    private static readonly Color32 BorderColor = new Color32(58, 50, 34, 255);

    // Measured in the game's OWN pixels — the art is 32 pixels to the world unit,
    // and the plate is built at the same scale so its corners and border sit on
    // the same grid as everything else on the screen. A border quoted in UI pixels
    // would come out a third as thick as the menus' and land between art pixels.
    private const float ArtPixelsPerUnit = 32f;
    private const int BorderPixels = 2;
    private const int RadiusPixels = 5;

    /// <summary>Breathing room between the plate's edge and the text inside it.</summary>
    public const float Padding = 8f / ArtPixelsPerUnit;

    /// <summary>The widest the plate is ever allowed to be, in world units.</summary>
    private const float MaxWidth = 12f;
    /// <summary>How much of a narrow view it may take, so it never runs off the sides.</summary>
    private const float ViewFraction = 0.752f;

    /// <summary>How tall the plate stands.</summary>
    public const float Height = 2.97f;
    /// <summary>How far the plate's bottom edge sits above the bottom of the view.</summary>
    private const float Lift = 0.30f;

    private static Sprite _panel;

    private SpriteRenderer _renderer;

    /// <summary>
    /// The one width every panel in a quest scene uses. Constant for a given view,
    /// so the dialogue plate and the reward plate are the same object standing
    /// still rather than two panels of different sizes.
    /// </summary>
    public static float Width
    {
        get
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return MaxWidth;
            return Mathf.Min(MaxWidth, cam.orthographicSize * cam.aspect * 2f * ViewFraction);
        }
    }

    /// <summary>How wide the text inside the plate may run.</summary>
    public static float TextWidth => Width - Padding * 2f;
    /// <summary>How tall the text inside the plate may run.</summary>
    public static float TextHeight => Height - Padding * 2f;

    /// <summary>Where the plate's centre goes, in world space. Everything else hangs off it.</summary>
    public static float CenterY => ViewBottom + Lift + Height * 0.5f;

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
    /// Where a text block of <paramref name="size"/> goes to sit in the plate's
    /// top-left corner, one padding in from both edges. In the plate's own
    /// coordinates, and a block is centred on its transform, hence the halves.
    /// </summary>
    public static Vector3 TopLeft(Vector2 size)
    {
        return new Vector3(-Width * 0.5f + Padding + size.x * 0.5f,
                           Height * 0.5f - Padding - size.y * 0.5f,
                           0f);
    }

    /// <summary>The mirror of <see cref="TopLeft"/>, for the "press to continue" line.</summary>
    public static Vector3 BottomRight(Vector2 size)
    {
        return new Vector3(Width * 0.5f - Padding - size.x * 0.5f,
                           -Height * 0.5f + Padding + size.y * 0.5f,
                           0f);
    }

    public static QuestBackdrop Create(Transform parent)
    {
        var go = new GameObject("Backdrop");
        go.transform.SetParent(parent, false);

        var backdrop = go.AddComponent<QuestBackdrop>();
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = Panel;
        // Nine-sliced, so the corners keep their radius at any size rather than
        // stretching into ovals. This is also why the plate is sized through
        // SpriteRenderer.size and never through localScale.
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = new Vector2(Width, Height);
        renderer.sortingLayerName = SortingLayer;
        renderer.sortingOrder = PanelOrder;

        backdrop._renderer = renderer;
        backdrop.SetAlpha(0f);
        return backdrop;
    }

    /// <summary>Fades with the text it stands behind, rather than on its own schedule.</summary>
    public void SetAlpha(float alpha)
    {
        // White tint: the fill and border colours are baked into the sprite, so the
        // tint carries nothing but the fade.
        _renderer.color = new Color(1f, 1f, 1f, alpha);
    }

    /// <summary>
    /// The nine-slice: a rounded rectangle with the border drawn inside it, shared
    /// by every plate in the process.
    ///
    /// Hard-edged on purpose — no antialiasing on the curve. The game is pixel art
    /// and a smoothed corner would be the only soft edge on the screen.
    /// </summary>
    private static Sprite Panel
    {
        get
        {
            if (_panel != null) return _panel;

            // Big enough that the two corner regions and a centre pixel fit: the
            // corner has to hold the radius AND the border outside it.
            const int corner = RadiusPixels + BorderPixels;
            const int size = corner * 2 + 2;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var clear = new Color32(0, 0, 0, 0);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inOuter = InRoundedRect(x, y, 0, size, corner);
                    bool inInner = InRoundedRect(x, y, BorderPixels, size, RadiusPixels);
                    pixels[y * size + x] = inInner ? FillColor : (inOuter ? BorderColor : clear);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            _panel = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                                   ArtPixelsPerUnit, 0, SpriteMeshType.FullRect,
                                   new Vector4(corner, corner, corner, corner));
            _panel.name = "Quest Backdrop Panel";
            _panel.hideFlags = HideFlags.HideAndDontSave;
            return _panel;
        }
    }

    /// <summary>
    /// Whether a pixel falls inside a rounded rectangle inset by
    /// <paramref name="inset"/> from a square of <paramref name="extent"/>.
    /// The usual trick: measure only from the corner circles' centres, and clamp
    /// to zero along the flat edges so they test as straight.
    /// </summary>
    private static bool InRoundedRect(int x, int y, int inset, int extent, int radius)
    {
        float px = x + 0.5f - inset;
        float py = y + 0.5f - inset;
        float span = extent - inset * 2;
        if (px < 0f || py < 0f || px > span || py > span) return false;

        float dx = Mathf.Max(radius - px, px - (span - radius), 0f);
        float dy = Mathf.Max(radius - py, py - (span - radius), 0f);
        return dx * dx + dy * dy <= radius * radius;
    }
}
