using UnityEngine;
using UnityEngine.UIElements;

// The picture behind the upgrade draft: a dark stone wall lit by two torches, with three
// frames drawn in big blocky pixels around the three columns, so the knights and the
// cards sit inside something instead of floating on flat black. The two knight columns
// are gothic windows, a stepped pointed arch over each portrait; the card column is a box
// with cut corners whose top line breaks around the wave caption: [ WAVE 22 ].
//
// It is one small pixel-art texture, 320 blocks across, drawn with point filtering.
// That makes every block 6 screen pixels at 1080p - twice the size of an arena pixel -
// and the frames are blocks too, not smooth lines.
//
// Nothing here is a fixed image. The frames are measured off the live layout each time
// it moves, then snapped to the block grid, so they always sit around the panels and
// never cross them, whatever the shape of the screen.
//
// Two small Aseprite tiles fill it, one tile pixel per block: the big slabs outside the
// frames (UpgradeGranite.aseprite) and a finer, quieter lattice inside them
// (UpgradePanel.aseprite). Both are painted with steps of one fixed ramp of seven browns.
//
// All the light comes from the torches hanging in the gaps between the frames. Every
// block of stone, frame, plinth and the torches' own iron starts from its painted step
// and is moved up or down its ramp by how much torch light reaches it: a bright pool
// round each flame, a long fall-off beyond it, and near-black at the bottom of the screen,
// the furthest from both. The panels' lattice is the one thing left out: it sits behind
// the text, so it keeps its painted steps everywhere and only its frame catches the
// light. Light is never blended in; it only steps along the ramps, and
// each step edge is a thin checkerboard band, the way the cave floor shades. Each torch's
// light sways slowly and smoothly, so the pool of light round it swells and ebbs a block at
// a time and the flame grows and shrinks with it. The two torches never sway together.
//
// Depth comes from the frames themselves: their edges facing up and left catch light,
// they throw a one-block shadow down and right onto the stone, the panel inside sits sunk
// below its frame with a shadow along its own top and left, and each frame stands on a
// low stone plinth.
//
// Shape distances are in UI units: the panel is 1200 wide, so 1 unit is 1.6 pixels at
// 1080p, and one block is 3.75 units. Light distances are in blocks.
public class UpgradeBackdrop : VisualElement
{
    // ---------- colours (Gilded Vigil) ----------

    // The stone's ramp, darkest first. The same seven colours are the palette of both
    // Aseprite tiles; paint them with these and the light here knows how to step them.
    // Index 3 is .upgrade-menu's background-color.
    private static readonly Color32[] Ramp =
    {
        new Color32(6, 5, 3, 255), new Color32(11, 9, 6, 255), new Color32(16, 13, 8, 255),
        new Color32(22, 18, 12, 255), new Color32(29, 23, 15, 255), new Color32(37, 29, 18, 255),
        new Color32(46, 36, 22, 255)
    };
    private const int GroundStep = 3;   // plain ground, when a tile is missing

    // The frames' own ramp, for the outlines, plinth tops and the torches' iron. Step 3 is
    // the outline in torchlight of nothing in particular; step 4 its lit edge. A muted
    // brown even at its brightest, well short of the menu gold, which on this screen
    // means "you can pick this".
    private static readonly Color32[] FrameRamp =
    {
        new Color32(18, 14, 9, 255), new Color32(30, 24, 15, 255), new Color32(46, 37, 22, 255),
        new Color32(66, 53, 32, 255), new Color32(92, 74, 44, 255), new Color32(118, 95, 56, 255),
        new Color32(140, 113, 66, 255)
    };
    private const int LineStep = 3;
    private const int LitLineStep = 4;

    private static readonly Color32 FlameOuter = new Color32(178, 78, 34, 255);
    private static readonly Color32 FlameCore = new Color32(236, 164, 72, 255);

    // ---------- light, in steps along the ramps ----------

    // Where every block starts before any torch reaches it: this far down its ramp.
    private const float Darkness = -3.2f;
    // Each torch: a long fall-off reaching this many blocks, worth this many steps at the
    // flame, plus a bright pool right round it.
    private const float GlowSteps = 4.2f;
    private const float GlowReach = 150f;
    private const float PoolSteps = 2.2f;
    private const float PoolReach = 24f;
    // Light travels this much further up and down than sideways.
    private const float LightSquash = 1.15f;
    // Half the width of the checkerboard band at each step edge, in steps.
    private const float DitherBand = 0.12f;
    // How bright each kind of block may get. (The panels take no torch light at all.)
    private const int StoneBrightest = 6;
    private const int FrameBrightest = 5;

    private const int DropShadowSteps = 2;    // the shadow a frame throws onto the stone
    private const int SunkShadowSteps = 1;    // the shadow along a panel's top and left

    // ---------- the torches ----------

    // How often the light is worked out again. Small, so the light drifts a block at a time
    // instead of jumping; 0 holds the flames, and their light, still.
    private const int LightTickMs = 66;

    // Each torch's light sways smoothly, never in jumps: two slow waves laid over each
    // other (periods in seconds, and how much each counts), started at a different point
    // for each torch so they never breathe together. The sway swells and shrinks the
    // bright pool by up to this share, and the long fall-off by much less, so the far
    // stone barely moves and the motion stays round the flames.
    private static readonly float[] SwayPeriods = { 2.3f, 0.9f };
    private static readonly float[] SwayWeights = { 0.65f, 0.35f };
    private static readonly float[] TorchOffset = { 0f, 1.7f };
    private const float PoolSway = 0.10f;
    private const float GlowSway = 0.02f;
    // The flame's shape follows the sway - tall when it swells, short when it ebbs - and
    // holds each shape at least this long, so it never twitches between two.
    private const float FlameHold = 0.3f;
    private const float FlameTall = 0.35f;

    // X = iron, L = iron catching the light. The flame sits in the four rows above.
    private static readonly string[] TorchBody =
    {
        "XLLLX",
        ".XLX.",
        "..X..",
        "..X..",
        ".XXX.",
    };

    // o = outer flame, F = core. Tall, middling (leaning) and short, picked by the sway.
    private static readonly string[][] Flames =
    {
        new[] { "..o..", ".oFo.", ".oFo.", "..o.." },
        new[] { ".o...", ".oFo.", ".oFo.", "..o.." },
        new[] { ".....", "..o..", ".oFo.", "..o.." },
    };
    private const int FlameRows = 4;
    private const int TorchWidth = 5;

    // ---------- shape ----------

    private const int BlocksAcross = 320;
    private const float Chamfer = 8f;         // the cut corners
    private const float CardInset = 8f;       // card frame sits this far inside its column
    private const float ArchRise = 1.27f;     // arch height over half the window's width
    private const float CaptionGap = 12f;     // clear space either side of "WAVE 22"

    // What a block is made of, which decides the ramp it steps along.
    private enum Kind : byte { Stone, Panel, Frame, Fixed }

    private readonly VisualElement _leftPanel, _rightPanel, _cardColumn;
    private readonly Label _caption;

    // The slabs outside the frames and the lattice inside them. Either may be null.
    private readonly Tile _stone, _panel;

    private bool _measured;
    private Rect _leftWindow, _rightWindow, _cardFrame;
    private bool _hasGap;
    private float _gapFrom, _gapTo;

    private Texture2D _texture;
    private Color32[] _pixels;
    private bool[] _inside, _line, _corner;

    // Per block, worked out once per layout: what it is, the step it is painted at, the
    // colour of anything off the ramps, and how far it is from each torch.
    private Kind[] _kind;
    private int[] _step;
    private Color32[] _fixed;
    private readonly float[][] _torchDistance = new float[2][];

    // Where the torches hang: the block at the top-left of each flame.
    private readonly Vector2Int[] _torches = new Vector2Int[2];
    private int _torchCount;
    // Each torch's current flame shape, and when it took it.
    private readonly int[] _flameShape = new int[2];
    private readonly float[] _flameSince = { float.MinValue, float.MinValue };

    public UpgradeBackdrop(VisualElement menu, Sprite stone = null, Sprite panel = null)
    {
        name = "upgrade-backdrop";
        pickingMode = PickingMode.Ignore;
        style.position = Position.Absolute;
        style.left = 0;
        style.top = 0;
        style.right = 0;
        style.bottom = 0;
        style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));

        _leftPanel = menu.Q("left-status");
        _rightPanel = menu.Q("right-status");
        _cardColumn = menu.Q("center-content");
        _caption = menu.Q<Label>("wave-number");

        _stone = Tile.Read(stone);
        _panel = Tile.Read(panel);

        // Any of these moving moves a frame. The caption is in the list because its
        // width changes with the wave number, and the top line breaks around it.
        RegisterCallback<GeometryChangedEvent>(_ => Remeasure());
        foreach (var watched in new[] { _leftPanel, _rightPanel, _cardColumn, _caption })
            watched?.RegisterCallback<GeometryChangedEvent>(_ => Remeasure());

        RegisterCallback<DetachFromPanelEvent>(_ => DropTexture());

        // Runs on real time, so the flames keep moving while the draft pauses the game.
        if (LightTickMs > 0) schedule.Execute(Tick).Every(LightTickMs);
    }

    // ---------- measuring ----------

    private void Remeasure()
    {
        if (!Laid(this) || !Laid(_leftPanel) || !Laid(_rightPanel) || !Laid(_cardColumn))
        {
            _measured = false;
            return;
        }

        Rect left = Local(_leftPanel);
        Rect right = Local(_rightPanel);
        Rect cards = Local(_cardColumn);

        // All three frames share one top and one bottom so they read as a set. The top
        // runs through the middle of the wave caption, so its line can break around it.
        _hasGap = Laid(_caption) && _caption.resolvedStyle.display != DisplayStyle.None
                  && !string.IsNullOrEmpty(_caption.text);
        Rect caption = _hasGap ? Local(_caption) : default;
        float top = _hasGap ? caption.center.y : left.yMin;
        float bottom = left.yMax;

        _leftWindow = Rect.MinMaxRect(left.xMin, top, left.xMax, bottom);
        _rightWindow = Rect.MinMaxRect(right.xMin, top, right.xMax, bottom);
        _cardFrame = Rect.MinMaxRect(cards.xMin + CardInset, top, cards.xMax - CardInset, bottom);
        _gapFrom = caption.xMin - CaptionGap;
        _gapTo = caption.xMax + CaptionGap;

        _measured = true;
        // 320 x 180 blocks: cheap enough to work out again whenever anything moves.
        Bake();
    }

    // worldBound includes transforms, but none of the elements measured here are ever
    // slid or scaled - only the cards inside them are - so the frames hold still while
    // the cards animate.
    private Rect Local(VisualElement element) => this.WorldToLocal(element.worldBound);

    private static bool Laid(VisualElement element)
    {
        if (element == null) return false;
        Rect box = element.worldBound;
        return box.width > 1f && box.height > 1f;
    }

    // ---------- working out what every block is ----------

    // Blocks down the screen: the 320 across, scaled to the screen's shape.
    private int BlocksDown()
    {
        return Mathf.Max(2, Mathf.RoundToInt(BlocksAcross * layout.height / Mathf.Max(layout.width, 1f)));
    }

    private void Bake()
    {
        if (!_measured || layout.width < 2f || layout.height < 2f) return;
        int width = BlocksAcross;
        int height = BlocksDown();
        int count = width * height;

        if (_texture == null || _texture.width != width || _texture.height != height)
        {
            DropTexture();
            // sRGB (linear: false), so the colours above come out on screen as written.
            // Point filtering keeps every block a hard-edged square.
            _texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                name = "UpgradeBackdrop",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };
            _pixels = new Color32[count];
            _inside = new bool[count];
            _line = new bool[count];
            _corner = new bool[count];
            _kind = new Kind[count];
            _step = new int[count];
            _fixed = new Color32[count];
            _torchDistance[0] = new float[count];
            _torchDistance[1] = new float[count];
        }

        // UI units per block. The frames are snapped to whole blocks first, so their
        // straight sides run down a single column and the arches meet them cleanly.
        float unitX = layout.width / width;
        float unitY = layout.height / height;
        Rect left = Snap(_leftWindow, unitX, unitY);
        Rect right = Snap(_rightWindow, unitX, unitY);
        Rect cards = Snap(_cardFrame, unitX, unitY);

        // 1. Which blocks fall inside a frame (tested at each block's centre).
        for (int y = 0; y < height; y++)
        {
            float py = (y + 0.5f) * unitY;
            for (int x = 0; x < width; x++)
            {
                float px = (x + 0.5f) * unitX;
                _inside[y * width + x] = InWindow(left, px, py) || InWindow(right, px, py) || InCutRect(cards, px, py);
            }
        }

        // 2. The outline: inside blocks with outside directly beside, above or below them.
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                _line[i] = _inside[i] && (!Inside(x - 1, y, width, height) || !Inside(x + 1, y, width, height)
                                          || !Inside(x, y - 1, width, height) || !Inside(x, y + 1, width, height));
            }

        // 3. Clean the steps: where the outline turns an L-shaped corner on a slope, the
        // corner block makes the line look two blocks thick. Drop it to the outside, as
        // you would by hand, so every slope is a clean one-block staircase.
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                _corner[i] = false;
                if (!_line[i]) continue;
                bool up = Line(x, y - 1, width, height), down = Line(x, y + 1, width, height);
                bool leftOf = Line(x - 1, y, width, height), rightOf = Line(x + 1, y, width, height);
                int neighbours = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if ((dx != 0 || dy != 0) && Line(x + dx, y + dy, width, height)) neighbours++;
                _corner[i] = neighbours == 2 && (up || down) && (leftOf || rightOf);
            }
        for (int i = 0; i < count; i++)
        {
            if (!_corner[i]) continue;
            _line[i] = false;
            _inside[i] = false;
        }

        // 4. Break the card frame's top line around the wave caption.
        int topRow = Mathf.RoundToInt(cards.yMin / unitY);
        if (_hasGap && topRow >= 0 && topRow < height)
            for (int x = 0; x < width; x++)
                if (InGap((x + 0.5f) * unitX)) _line[topRow * width + x] = false;

        // 5. What each block is made of, and the step it is painted at.
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                // A frame's outline above, to the left or up-left of this block: the blocks
                // that sit in its shadow, on whichever side of it they are.
                bool shadowed = Line(x, y - 1, width, height) || Line(x - 1, y, width, height)
                                || Line(x - 1, y - 1, width, height);
                if (_line[i])
                {
                    bool facesLight = !Inside(x, y - 1, width, height) || !Inside(x - 1, y, width, height);
                    Set(i, Kind.Frame, facesLight ? LitLineStep : LineStep);
                }
                else if (_inside[i])
                {
                    int step = Tile.Sample(_panel, x, y, width, height, out Color32 colour);
                    bool gapBlock = _hasGap && y == topRow && InGap((x + 0.5f) * unitX);
                    if (step < 0) SetFixed(i, colour);
                    else Set(i, Kind.Panel, shadowed && !gapBlock ? step - SunkShadowSteps : step);
                }
                else
                {
                    int step = Tile.Sample(_stone, x, y, width, height, out Color32 colour);
                    if (step < 0) SetFixed(i, colour);
                    else Set(i, Kind.Stone, shadowed ? step - DropShadowSteps : step);
                }
            }

        // 6. The plinths, the caption's brackets and the torches' iron, over the top.
        DrawPlinth(left, unitX, unitY, width, height);
        DrawPlinth(right, unitX, unitY, width, height);
        DrawPlinth(cards, unitX, unitY, width, height);
        if (_hasGap)
        {
            DrawBracket(Mathf.FloorToInt(_gapFrom / unitX) - 1, topRow, width, height);
            DrawBracket(Mathf.FloorToInt(_gapTo / unitX) + 1, topRow, width, height);
        }

        // Torches hang in the middle of each gap between a window and the card frame,
        // forty blocks below the frames' top - about level with the knights' names.
        _torchCount = 0;
        AddTorch(Mathf.RoundToInt(left.xMax / unitX), Mathf.RoundToInt(cards.xMin / unitX), topRow + 40);
        AddTorch(Mathf.RoundToInt(cards.xMax / unitX), Mathf.RoundToInt(right.xMin / unitX), topRow + 40);
        for (int t = 0; t < _torchCount; t++)
        {
            DrawTorchIron(t, width, height);
            MeasureTorchDistances(t, width, height);
        }

        Relight();
        style.backgroundImage = new StyleBackground(_texture);
    }

    private void Set(int i, Kind kind, int step)
    {
        _kind[i] = kind;
        _step[i] = Mathf.Max(0, step);
    }

    private void SetFixed(int i, Color32 colour)
    {
        _kind[i] = Kind.Fixed;
        _fixed[i] = colour;
    }

    // Textures count rows from the bottom, the UI counts from the top; the arrays here
    // follow the UI, and only Relight turns them over.
    private void Set(int x, int y, Kind kind, int step, int width, int height)
    {
        if (x < 0 || y < 0 || x >= width || y >= height) return;
        Set(y * width + x, kind, step);
    }

    // A low ledge under a frame: one row of frame on top, two of pale stone, and the same
    // shadow down and right that the frames throw.
    private void DrawPlinth(Rect frame, float unitX, float unitY, int width, int height)
    {
        int bottom = Mathf.RoundToInt(frame.yMax / unitY) - 1;
        int from = Mathf.RoundToInt(frame.xMin / unitX) - 2;
        int to = Mathf.RoundToInt(frame.xMax / unitX) + 1;
        for (int x = from; x <= to; x++)
        {
            Set(x, bottom - 1, Kind.Frame, LineStep, width, height);
            Set(x, bottom, Kind.Stone, 5, width, height);
            Set(x, bottom + 1, Kind.Stone, 5, width, height);
            Set(x + 1, bottom + 2, Kind.Stone, 1, width, height);
        }
        for (int y = bottom - 1; y <= bottom + 1; y++) Set(to + 1, y + 1, Kind.Stone, 1, width, height);
    }

    // One end of the caption's break: an upright bar three blocks tall.
    private void DrawBracket(int x, int row, int width, int height)
    {
        for (int dy = -1; dy <= 1; dy++) Set(x, row + dy, Kind.Frame, dy < 1 ? LitLineStep : LineStep, width, height);
    }

    // Records a torch in the middle of a gap, if the gap is wide enough to hold one.
    private void AddTorch(int gapFrom, int gapTo, int row)
    {
        if (gapTo - gapFrom < TorchWidth + 4 || _torchCount >= _torches.Length) return;
        _torches[_torchCount++] = new Vector2Int((gapFrom + gapTo) / 2 - TorchWidth / 2, row - FlameRows);
    }

    private void DrawTorchIron(int t, int width, int height)
    {
        Vector2Int at = _torches[t];
        for (int row = 0; row < TorchBody.Length; row++)
            for (int column = 0; column < TorchWidth; column++)
            {
                char c = TorchBody[row][column];
                if (c != '.') Set(at.x + column, at.y + FlameRows + row, Kind.Frame, c == 'L' ? LitLineStep : LineStep, width, height);
            }
    }

    // Distance from the middle of the flame to every block, stretched so light reaches a
    // little further up and down than sideways.
    private void MeasureTorchDistances(int t, int width, int height)
    {
        float fx = _torches[t].x + TorchWidth / 2;
        float fy = _torches[t].y + FlameRows * 0.375f;
        float[] distance = _torchDistance[t];
        for (int y = 0; y < height; y++)
        {
            float dy = (y - fy) / LightSquash;
            for (int x = 0; x < width; x++)
            {
                float dx = x - fx;
                distance[y * width + x] = Mathf.Sqrt(dx * dx + dy * dy);
            }
        }
    }

    // ---------- lighting ----------

    private void Tick()
    {
        // Nothing to do while the menu is hidden or before the first paint.
        if (_texture == null || _torchCount == 0 || !Shown()) return;
        Relight();
        MarkDirtyRepaint();
    }

    // How far a torch's light has swayed at a moment, between -1 and 1. Real time, so it
    // runs on while the draft pauses the game.
    private static float Sway(float time, int torch)
    {
        float sway = 0f;
        for (int k = 0; k < SwayPeriods.Length; k++)
            sway += SwayWeights[k] * Mathf.Sin(2f * Mathf.PI * (time + TorchOffset[torch] * (k + 1)) / SwayPeriods[k]);
        return sway;
    }

    // Paints every block from what it is and how much torch light reaches it right now,
    // then the flames over the top.
    private void Relight()
    {
        int width = _texture.width;
        int height = _texture.height;
        float now = Time.unscaledTime;

        // This moment's light for each torch, and the flame shape that goes with it.
        var pool = new float[_torchCount];
        var glow = new float[_torchCount];
        for (int t = 0; t < _torchCount; t++)
        {
            float sway = Sway(now, t);
            pool[t] = 1f + PoolSway * sway;
            glow[t] = 1f + GlowSway * sway;
            int shape = sway > FlameTall ? 0 : sway < -FlameTall ? 2 : 1;
            if (shape != _flameShape[t] && now - _flameSince[t] >= FlameHold)
            {
                _flameShape[t] = shape;
                _flameSince[t] = now;
            }
        }

        for (int y = 0; y < height; y++)
        {
            int row = (height - 1 - y) * width;
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                Kind kind = _kind[i];
                if (kind == Kind.Fixed)
                {
                    _pixels[row + x] = _fixed[i];
                    continue;
                }
                // The lattice behind the text takes no torch light: it stays at the steps it
                // was painted, so the crossed pattern reads the same from top to bottom. Only
                // its frame, and the stone around it, catch the light.
                if (kind == Kind.Panel)
                {
                    _pixels[row + x] = Ramp[Mathf.Clamp(_step[i], 0, Ramp.Length - 1)];
                    continue;
                }

                float light = Darkness;
                for (int t = 0; t < _torchCount; t++)
                {
                    float d = _torchDistance[t][i];
                    light += GlowSteps * glow[t] * Mathf.Clamp01(1f - d / (GlowReach * glow[t]))
                           + PoolSteps * pool[t] * Mathf.Clamp01(1f - d / (PoolReach * pool[t]));
                }

                int step = _step[i] + Steps(light, ((x + y) & 1) == 1);
                _pixels[row + x] = kind == Kind.Frame
                    ? FrameRamp[Mathf.Clamp(step, 0, FrameBrightest)]
                    : Ramp[Mathf.Clamp(step, 0, StoneBrightest)];
            }
        }

        for (int t = 0; t < _torchCount; t++) DrawFlame(_torches[t], Flames[_flameShape[t]], width, height);

        _texture.SetPixels32(_pixels);
        _texture.Apply(false, false);
    }

    private void DrawFlame(Vector2Int at, string[] flame, int width, int height)
    {
        for (int row = 0; row < FlameRows; row++)
            for (int column = 0; column < TorchWidth; column++)
            {
                char c = flame[row][column];
                int x = at.x + column, y = at.y + row;
                if (c == '.' || x < 0 || y < 0 || x >= width || y >= height) continue;
                _pixels[(height - 1 - y) * width + x] = c == 'F' ? FlameCore : FlameOuter;
            }
    }

    // Light in steps, to a whole number of steps. Mostly it simply rounds; only right at
    // each step's edge does it land on every other block, which is the thin checkerboard
    // band between two flat tones.
    private static int Steps(float light, bool checker)
    {
        int whole = Mathf.FloorToInt(light);
        float part = light - whole;
        if (part >= 0.5f + DitherBand) return whole + 1;
        if (part > 0.5f - DitherBand && checker) return whole + 1;
        return whole;
    }

    // ---------- helpers ----------

    // The menu hides by setting display: none on its root, and a hidden element can keep
    // its last size, so ask every ancestor rather than trusting the layout.
    private bool Shown()
    {
        if (panel == null) return false;
        for (VisualElement e = this; e != null; e = e.hierarchy.parent)
            if (e.resolvedStyle.display == DisplayStyle.None) return false;
        return true;
    }

    private bool InGap(float px) => px > _gapFrom && px < _gapTo;

    private bool Inside(int x, int y, int width, int height)
    {
        return x >= 0 && y >= 0 && x < width && y < height && _inside[y * width + x];
    }

    private bool Line(int x, int y, int width, int height)
    {
        return x >= 0 && y >= 0 && x < width && y < height && _line[y * width + x];
    }

    private static Rect Snap(Rect rect, float unitX, float unitY)
    {
        return Rect.MinMaxRect(
            Mathf.Round(rect.xMin / unitX) * unitX, Mathf.Round(rect.yMin / unitY) * unitY,
            Mathf.Round(rect.xMax / unitX) * unitX, Mathf.Round(rect.yMax / unitY) * unitY);
    }

    private void DropTexture()
    {
        if (_texture == null) return;
        style.backgroundImage = StyleKeyword.None;
        if (Application.isPlaying) Object.Destroy(_texture);
        else Object.DestroyImmediate(_texture);
        _texture = null;
    }

    // ---------- the tiles ----------

    // One Aseprite tile, top row first: each pixel's colour, and where that colour sits on
    // the ramp (-1 for anything off it, which is drawn exactly as painted and never lit).
    private sealed class Tile
    {
        private Color32[] _colours;
        private int[] _steps;
        private int _width, _height;

        // The ramp step at screen block (x, y), with the repeat centred on the screen so
        // the pattern falls the same on both sides. The colour comes back too, for
        // pixels that are off the ramp. A missing tile is plain ground.
        public static int Sample(Tile tile, int x, int y, int screenWidth, int screenHeight, out Color32 colour)
        {
            if (tile == null)
            {
                colour = Ramp[GroundStep];
                return GroundStep;
            }
            int offsetX = (tile._width - screenWidth % tile._width) % tile._width / 2;
            int offsetY = (tile._height - screenHeight % tile._height) % tile._height / 2;
            int index = (y + offsetY) % tile._height * tile._width + (x + offsetX) % tile._width;
            colour = tile._colours[index];
            return tile._steps[index];
        }

        // Copies the pixels out once. The importer keeps both tiles Read/Write; if that
        // ever gets switched off, the pixels come back through the GPU instead, so the
        // backdrop never silently falls back to flat ground.
        public static Tile Read(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return null;

            Rect rect = sprite.textureRect;
            int x = Mathf.RoundToInt(rect.x), y = Mathf.RoundToInt(rect.y);
            int width = Mathf.RoundToInt(rect.width), height = Mathf.RoundToInt(rect.height);
            if (width < 1 || height < 1) return null;

            Color32[] bottomUp = sprite.texture.isReadable
                ? ToColor32(sprite.texture.GetPixels(x, y, width, height))
                : ReadThroughGpu(sprite.texture, x, y, width, height);
            if (bottomUp == null || bottomUp.Length != width * height) return null;

            var tile = new Tile
            {
                _colours = new Color32[width * height],
                _steps = new int[width * height],
                _width = width,
                _height = height
            };
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    Color32 c = bottomUp[(height - 1 - row) * width + column];
                    tile._colours[row * width + column] = c;
                    tile._steps[row * width + column] = StepOf(c);
                }
            }
            return tile;
        }

        private static int StepOf(Color32 colour)
        {
            for (int i = 0; i < Ramp.Length; i++)
            {
                Color32 r = Ramp[i];
                if (r.r == colour.r && r.g == colour.g && r.b == colour.b) return i;
            }
            return -1;
        }

        private static Color32[] ToColor32(Color[] colours)
        {
            var result = new Color32[colours.Length];
            for (int i = 0; i < colours.Length; i++) result[i] = colours[i];
            return result;
        }

        // An sRGB render target, so the bytes read back are the bytes that were drawn.
        private static Color32[] ReadThroughGpu(Texture2D source, int x, int y, int width, int height)
        {
            RenderTexture target = RenderTexture.GetTemporary(source.width, source.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            var copy = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                copy.ReadPixels(new Rect(x, y, width, height), 0, 0, false);
                return copy.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                if (Application.isPlaying) Object.Destroy(copy);
                else Object.DestroyImmediate(copy);
            }
        }
    }

    // ---------- shape tests ----------

    // A gothic window: straight sides with cut feet, topped by a pointed arch made of
    // two circles that cross in the middle. Its point sits on the rect's top edge.
    private static void ArchOf(Rect window, out float spring, out float radius, out float offset)
    {
        float half = window.width * 0.5f;
        float rise = half * ArchRise;
        spring = window.yMin + rise;
        // The circle through both the springing point and the apex. ArchRise above 1 keeps
        // the radius wider than half the window, which is what makes the top pointed.
        radius = (half * half + rise * rise) / (2f * half);
        offset = radius - half;
    }

    private static bool InWindow(Rect window, float px, float py)
    {
        if (px < window.xMin || px > window.xMax || py < window.yMin || py > window.yMax) return false;

        if (window.yMax - py + (px - window.xMin) < Chamfer) return false;
        if (window.yMax - py + (window.xMax - px) < Chamfer) return false;

        ArchOf(window, out float spring, out float radius, out float offset);
        if (py >= spring) return true;

        float cx = window.center.x;
        float dy = py - spring;
        float r2 = radius * radius;
        float toLeftArc = px - (cx + offset);
        float toRightArc = px - (cx - offset);
        return toLeftArc * toLeftArc + dy * dy <= r2 && toRightArc * toRightArc + dy * dy <= r2;
    }

    private static bool InCutRect(Rect frame, float px, float py)
    {
        if (px < frame.xMin || px > frame.xMax || py < frame.yMin || py > frame.yMax) return false;
        float left = px - frame.xMin, right = frame.xMax - px;
        float top = py - frame.yMin, bottom = frame.yMax - py;
        return left + top >= Chamfer && right + top >= Chamfer
            && left + bottom >= Chamfer && right + bottom >= Chamfer;
    }
}
