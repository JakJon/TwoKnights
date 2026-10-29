using UnityEngine;
using UnityEngine.UIElements;

// One of a knight's two bars, as pixel art painted in code (owner, 2026-10-13).
// A slanted box, both ends leaning the same way, 72 x 8 of the game's own
// pixels: a one-pixel black outline like every sprite has, and inside it seven
// flat colour steps. The steps stay where they are along the bar, so a low bar
// shows only the first colours and a full one shows them all. The steps and
// the leading edge of the fill slant the way the ends do.
//
// It paints a small texture and gives it to an element in PlayerHUD.uxml as
// its background. PlayerHUD.uss sizes that element so one texture pixel is one
// art pixel. The texture is repainted only when what it shows has changed.
//
// The Bar Width setting (HudLayout.BarWidth) makes the bar LONGER, never
// bigger (owner, 2026-10-14: "doesn't effect the font size or height of the
// bars"). It is painted with more art pixels rather than stretched, so the
// pixels stay square, the height stays 8, and the seven steps spread out over
// the extra length.
//
// The boss's bar is one of these too (owner, 2026-10-20: "the same style as
// the knights"), at a fixed length of its own that the knights' setting does
// not touch.
public class PixelBar
{
    // The bar as built, and the 100% of the Bar Width setting
    public const int Width = 72;
    public const int Height = 8;

    // HUD pixels per art pixel: .pixel-bar in PlayerHUD.uss is 135 x 15 for 72 x 8
    public const float ArtPixel = 1.875f;

    // Light red to deep crimson. The last three are the red palette strip's.
    // The first two were paler (255,138,118 and 242,96,79) and a low bar
    // read as pink (owner, 2026-10-13).
    public static readonly Color32[] HealthRamp =
    {
        new Color32(248, 112, 92, 255), new Color32(234, 85, 69, 255), new Color32(217, 66, 58, 255),
        new Color32(185, 48, 40, 255), new Color32(159, 33, 37, 255), new Color32(140, 26, 34, 255),
        new Color32(110, 17, 25, 255),
    };

    // Deep purple to deep blue. The first two are the purple palette strip's.
    public static readonly Color32[] SpecialRamp =
    {
        new Color32(76, 25, 108, 255), new Color32(87, 32, 121, 255), new Color32(88, 42, 156, 255),
        new Color32(74, 49, 176, 255), new Color32(60, 56, 190, 255), new Color32(48, 64, 200, 255),
        new Color32(42, 72, 208, 255),
    };

    // The outline never changes colour. Low health and a full special bar are
    // both shown by a halo behind the bar instead (owner, 2026-10-13: "I don't
    // like the flashing for when it is low"). See .health-glow in PlayerHUD.uss.
    private static readonly Color32 Outline = new Color32(0, 0, 0, 255);
    private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

    // The rows inside the outline: one light row on top, one dark row at the
    // foot, the plain colour between them
    private const int Top = 0, Middle = 1, Foot = 2;

    // The empty part of the bar, by row
    private static readonly Color32[] Track =
    {
        new Color32(32, 27, 42, 255), new Color32(23, 19, 29, 255), new Color32(15, 12, 20, 255),
    };

    private static readonly Color32 TopTint = new Color32(255, 244, 230, 255);
    private const float TopAmount = 0.34f;
    private static readonly Color32 FootTint = new Color32(10, 4, 20, 255);
    private const float FootAmount = 0.36f;

    // Pixels along the inside of one row, between the two outline pixels
    private static int InsideOf(int width) => width - (Height - 1) - 2;

    private readonly VisualElement _target;
    private readonly Color32[][] _steps = new Color32[3][];
    private readonly bool _mirrored;
    private readonly int _fixedWidth; // 0 = follow the Bar Width setting
    private int _width;
    private Color32[] _pixels;
    private Texture2D _texture;
    private float _fraction = 1f;
    private int _filled = -1;

    // mirrored: the right knight's bar, which leans and fills the other way
    public PixelBar(VisualElement target, Color32[] ramp, bool mirrored)
        : this(target, ramp, mirrored, 0)
    {
    }

    // fixedWidth: art pixels long whatever the Bar Width setting says — the
    // boss's bar. 0 follows the setting, as the knights' bars do.
    public PixelBar(VisualElement target, Color32[] ramp, bool mirrored, int fixedWidth)
    {
        _target = target;
        _mirrored = mirrored;
        _fixedWidth = Mathf.Max(0, fixedWidth);

        _steps[Top] = new Color32[ramp.Length];
        _steps[Middle] = new Color32[ramp.Length];
        _steps[Foot] = new Color32[ramp.Length];
        for (int i = 0; i < ramp.Length; i++)
        {
            _steps[Top][i] = Mix(ramp[i], TopTint, TopAmount);
            _steps[Middle][i] = ramp[i];
            _steps[Foot][i] = Mix(ramp[i], FootTint, FootAmount);
        }

        // Generated, not an asset: it goes when the HUD does
        _target.RegisterCallback<DetachFromPanelEvent>(_ => Release());
        _target.RegisterCallback<AttachToPanelEvent>(_ =>
        {
            Listen();
            Paint();
        });

        Listen();
        Resize(_fixedWidth > 0 ? _fixedWidth : HudLayout.BarPixels);
        SetFill(1f);
    }

    public void SetFill(float fraction)
    {
        _fraction = fraction;
        int filled = FilledPixels(fraction, InsideOf(_width));
        if (filled == _filled) return;
        _filled = filled;
        Paint();
    }

    // Any health at all shows at least one pixel, and only a full bar shows
    // them all: the last pixel is the one the player reads "full" and "dead" off.
    public static int FilledPixels(float fraction, int inside)
    {
        if (fraction <= 0f) return 0;
        if (fraction >= 1f) return inside;
        return Mathf.Clamp(Mathf.RoundToInt(fraction * inside), 1, inside - 1);
    }

    // Unhooked first, so a bar put back on the panel never listens twice
    private void Listen()
    {
        HudLayout.BarWidthChanged -= OnBarWidthChanged;
        if (_fixedWidth > 0) return;
        HudLayout.BarWidthChanged += OnBarWidthChanged;
    }

    private void OnBarWidthChanged()
    {
        if (HudLayout.BarPixels == _width) return;
        Resize(HudLayout.BarPixels);
        _filled = FilledPixels(_fraction, InsideOf(_width));
        Paint();
    }

    // A new length, in art pixels. The element is sized to it here, overriding
    // the stylesheet's 135px, so one texture pixel stays one art pixel; its
    // height is the stylesheet's and never changes.
    private void Resize(int width)
    {
        _width = Mathf.Max(Width, width);
        _pixels = new Color32[_width * Height];
        _target.style.width = _width * ArtPixel;

        // Made again at the new size on the next paint
        if (_texture != null)
        {
            Object.Destroy(_texture);
            _texture = null;
        }
    }

    private void Paint()
    {
        if (_texture == null)
        {
            _texture = new Texture2D(_width, Height, TextureFormat.RGBA32, false)
            {
                name = "PixelBar",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            _target.style.backgroundImage = new StyleBackground(_texture);
        }

        Fill(_pixels, _steps, _filled, _mirrored, _width);
        _texture.SetPixels32(_pixels);
        _texture.Apply(false);
    }

    // The drawing itself. Rows are counted from the top here and written
    // bottom-up, the way a texture stores them.
    private static void Fill(Color32[] pixels, Color32[][] steps, int filled, bool mirrored, int width)
    {
        int stepCount = steps[Middle].Length;
        int inside = InsideOf(width);
        for (int y = 0; y < Height; y++)
        {
            // Each row sits one pixel further left than the row above it
            int start = Height - 1 - y;
            int end = width - y;
            int row = y == 1 ? Top : y == Height - 2 ? Foot : Middle;

            for (int x = 0; x < width; x++)
            {
                Color32 colour;
                if (x < start || x >= end)
                {
                    colour = Clear;
                }
                else if (y == 0 || y == Height - 1 || x == start || x == end - 1)
                {
                    colour = Outline;
                }
                else
                {
                    int along = x - start - 1;
                    if (along < filled)
                    {
                        int step = Mathf.Min(along * stepCount / (inside - 1), stepCount - 1);
                        colour = steps[row][step];
                    }
                    else
                    {
                        colour = Track[row];
                    }
                }

                int column = mirrored ? width - 1 - x : x;
                pixels[(Height - 1 - y) * width + column] = colour;
            }
        }
    }

    private void Release()
    {
        HudLayout.BarWidthChanged -= OnBarWidthChanged;
        if (_texture == null) return;
        _target.style.backgroundImage = StyleKeyword.Null;
        Object.Destroy(_texture);
        _texture = null;
    }

    private static Color32 Mix(Color32 from, Color32 to, float amount)
    {
        return new Color32(
            (byte)Mathf.RoundToInt(from.r + (to.r - from.r) * amount),
            (byte)Mathf.RoundToInt(from.g + (to.g - from.g) * amount),
            (byte)Mathf.RoundToInt(from.b + (to.b - from.b) * amount),
            255);
    }
}
