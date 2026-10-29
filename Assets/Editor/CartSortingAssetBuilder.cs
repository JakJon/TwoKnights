using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Builds everything the mine's cart sorting challenge needs that cannot be written
// by hand: the coloured corner pieces, carts, flags and levers, the five tracks,
// and the kit asset the challenge loads them through.
//
// Like MineCartAssetBuilder, it exists because a prefab that points at a sprite
// inside an .aseprite has to be made in the editor — the sprite's id is generated
// on import. Every coloured prefab is a VARIANT of the ordinary one, so the fall,
// the dust, the land sound and the sorting all stay the ordinary piece's and only
// the art differs.
//
// Corner pieces come in red and blue only, one colour per lever. A fork is ordinary
// track with one coloured corner where it splits (owner, 2026-10-09). Carts and
// flags come in four colours: red, blue, green and yellow.
//
// Tools > Two Knights > Build Cart Sorting Assets makes everything. It is safe to
// run again: it overwrites what it made and keeps every guid.
// Tools > Two Knights > Check Cart Sorting Phases then plays every phase through on
// paper — no play mode — and says whether each cart can get home and how tight
// the lever hits are.
public static class CartSortingAssetBuilder
{
    private const string Art = "Assets/Graphics/";
    private const string PieceSetPath = "Assets/Scripts/Rails/Layouts/Mine Pieces.asset";
    private const string KitPath = "Assets/Resources/CartSortingKit.asset";
    private const string LayoutFolder = "Assets/Scripts/Rails/Layouts/";

    private const string PlainCart = "Assets/Rail_Cart.prefab";
    private const string PlainFlag = "Assets/FX_DropFlag.prefab";
    private const float SortingFlagScale = 1.5f;

    private const RailPieceKind NE = RailPieceKind.CornerNorthEast;
    private const RailPieceKind NW = RailPieceKind.CornerNorthWest;
    private const RailPieceKind SE = RailPieceKind.CornerSouthEast;
    private const RailPieceKind SW = RailPieceKind.CornerSouthWest;

    private const RailSwitchColor RedLever = RailSwitchColor.Red;
    private const RailSwitchColor BlueLever = RailSwitchColor.Blue;

    // kind, the ordinary prefab, and the art it is drawn from. The art is named for
    // where a tile sits in a circuit and the kind for the two directions it joins,
    // which read as opposites — see MineCartAssetBuilder.PieceTable.
    private struct Piece
    {
        public RailPieceKind Kind;
        public string Prefab;
        public string ArtName;
        public Piece(RailPieceKind kind, string prefab, string artName)
        {
            Kind = kind;
            Prefab = prefab;
            ArtName = artName;
        }
    }

    private static readonly Piece[] Corners =
    {
        new Piece(NE, "Assets/Rail_CornerNorthEast.prefab", "rail_bottom_left"),
        new Piece(NW, "Assets/Rail_CornerNorthWest.prefab", "rail_bottom_right"),
        new Piece(SE, "Assets/Rail_CornerSouthEast.prefab", "rail_top_left"),
        new Piece(SW, "Assets/Rail_CornerSouthWest.prefab", "rail_top_right"),
    };

    private static readonly string[] LeverColors = { "Red", "Blue" };
    private static readonly string[] CartColors = { "Red", "Blue", "Green", "Yellow" };

    // Made by the first version of this builder, before the fork was cut down to
    // one coloured corner and the single lever became two. Nothing uses them.
    private static readonly string[] Retired =
    {
        "Assets/Rail_Horizontal_Red.prefab",
        "Assets/Rail_Horizontal_Blue.prefab",
        "Assets/Rail_Vertical_Red.prefab",
        "Assets/Rail_Vertical_Blue.prefab",
        "Assets/Rail_Switch.prefab",
        "Assets/Graphics/rail_switch.aseprite",
    };

    [MenuItem("Tools/Two Knights/Build Cart Sorting Assets")]
    public static void Build()
    {
        var report = new List<string>();

        var set = AssetDatabase.LoadAssetAtPath<RailPieceSet>(PieceSetPath);
        if (set == null)
        {
            Debug.LogError($"[CartSortingAssetBuilder] Missing {PieceSetPath}; nothing built.");
            return;
        }

        var setSo = new SerializedObject(set);
        if (setSo.FindProperty("yellowFlagPrefab") == null)
        {
            Debug.LogError("[CartSortingAssetBuilder] RailPieceSet has no yellowFlagPrefab field — recompile first.");
            return;
        }

        // ---- corner pieces: red and blue ----
        foreach (string color in LeverColors)
        {
            SerializedProperty table = setSo.FindProperty(color == "Red" ? "redPieces" : "bluePieces");
            table.arraySize = Corners.Length;

            for (int i = 0; i < Corners.Length; i++)
            {
                string path = Corners[i].Prefab.Replace(".prefab", "_" + color + ".prefab");
                GameObject piece = BuildRailPiece(Corners[i], color, path, report);

                SerializedProperty entry = table.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("kind").intValue = (int)Corners[i].Kind;
                entry.FindPropertyRelative("prefab").objectReferenceValue = piece;
            }
        }

        // ---- flags and carts: all four colours ----
        var carts = new Dictionary<string, GameObject>();
        foreach (string color in CartColors)
        {
            setSo.FindProperty(color.ToLowerInvariant() + "FlagPrefab").objectReferenceValue = BuildFlag(color, report);
            carts[color] = BuildCart(color, report);
        }
        setSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(set);

        // ---- levers ----
        GameObject redLever = BuildLever("Red", RedLever, report);
        GameObject blueLever = BuildLever("Blue", BlueLever, report);

        // ---- the five tracks ----
        RailLayout[] tracks =
        {
            WriteLayout("Mine Sorting 1", set, TwoForks()),
            WriteLayout("Mine Sorting 2", set, PassItOn()),
            WriteLayout("Mine Sorting 3", set, OverAndUnder()),
            WriteLayout("Mine Sorting 4", set, Fountain()),
            WriteLayout("Mine Sorting 5", set, TwinWheels()),
        };

        // ---- the kit ----
        var kit = AssetDatabase.LoadAssetAtPath<CartSortingKit>(KitPath);
        if (kit == null)
        {
            kit = ScriptableObject.CreateInstance<CartSortingKit>();
            AssetDatabase.CreateAsset(kit, KitPath);
        }

        var kitSo = new SerializedObject(kit);
        SerializedProperty layouts = kitSo.FindProperty("layouts");
        layouts.arraySize = tracks.Length;
        for (int i = 0; i < tracks.Length; i++) layouts.GetArrayElementAtIndex(i).objectReferenceValue = tracks[i];
        foreach (string color in CartColors)
        {
            kitSo.FindProperty(color.ToLowerInvariant() + "Cart").objectReferenceValue = carts[color];
        }
        kitSo.FindProperty("redLever").objectReferenceValue = redLever;
        kitSo.FindProperty("blueLever").objectReferenceValue = blueLever;
        SetSound(kitSo.FindProperty("deliveredSound"), 0.8f, report, "cart_sorted", "cargo_drop");
        kitSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(kit);

        AssetDatabase.SaveAssets();

        foreach (string path in Retired)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) == null) continue;
            report.Add((AssetDatabase.DeleteAsset(path) ? "removed " : "COULD NOT REMOVE ") + path);
        }

        AssetDatabase.Refresh();

        foreach (string line in report) Debug.Log("[CartSortingAssetBuilder] " + line);
        Debug.Log("[CartSortingAssetBuilder] Done.");
    }

    // ------------------------------------------------------------------
    // The tracks. Positions are a rail piece's pivot, the bottom middle of its
    // cell, so everything draws half a unit above the y written here. The frame
    // is x -10..10, y -5.6..5.6; the knights stand at x -2 and 2, y -0.5.
    //
    // The first runs of every track are the ones carts roll in on, because
    // CartSorting names them by number.
    // ------------------------------------------------------------------

    // 1. Two forks (the owner's sketch). Feeders up from the bottom at x -6 and 6;
    //    each splits at y -1 into branches two cells either side.
    //
    //   x=-8    -6    -4                      4     6     8
    //    |red         |blue                   |red        |blue    branches, flags at 3.5
    //    +-----R------+       knights         +-----B-----+        y=-1 (R, B: the coloured corner)
    //          |                                    |              feeders
    private static Track TwoForks()
    {
        var t = new Track();
        int leftIn = t.Up("LEFT fork: feeder", -6, -7, -2);                       // run 0
        int rightIn = t.Up("RIGHT fork: feeder", 6, -7, -2);                      // run 1
        int lw = t.West("left fork: across to its left branch", -1, -7, -7);
        int le = t.East("left fork: across to its right branch", -1, -5, -5);
        int ll = t.Up("left fork: left branch, red flag", -8, 0, 4);
        int lr = t.Up("left fork: right branch, blue flag", -4, 0, 4);
        int rw = t.West("right fork: across to its left branch", -1, 5, 5);
        int re = t.East("right fork: across to its right branch", -1, 7, 7);
        int rl = t.Up("right fork: left branch, red flag", 4, 0, 4);
        int rr = t.Up("right fork: right branch, blue flag", 8, 0, 4);

        t.Corner(NE, -8, -1); t.Corner(NW, -4, -1); t.Corner(NE, 4, -1); t.Corner(NW, 8, -1);
        t.Fork(RedLever, -6, -1, SW, SE);     // at rest: to the left branch
        t.Fork(BlueLever, 6, -1, SE, SW);     // at rest: to the right branch

        t.Link(leftIn, lw, RedLever, false); t.Link(leftIn, le, RedLever, true);
        t.Link(lw, ll); t.Link(le, lr);
        t.Link(rightIn, re, BlueLever, false); t.Link(rightIn, rw, BlueLever, true);
        t.Link(rw, rl); t.Link(re, rr);

        t.Flag(ll, 3.5f, CartColor.Red, RailFlagSide.WestOrSouth);
        t.Flag(lr, 3.5f, CartColor.Blue, RailFlagSide.EastOrNorth);
        t.Flag(rl, 3.5f, CartColor.Red, RailFlagSide.WestOrSouth);
        t.Flag(rr, 3.5f, CartColor.Blue, RailFlagSide.EastOrNorth);
        return t;
    }

    // 2. Pass it on. Up the left to the red fork at the top left: left and down to
    //    the red flag, or right along the top, over the knights, down to the blue
    //    fork, which splits to the blue flag and the green flag.
    //
    //    +--R-----------------------+        y= 2
    //    |  |                       |
    //    |  |        knights        |
    //   red |                  +----B----+   y=-2
    //       |                  |blue     |green
    private static Track PassItOn()
    {
        var t = new Track();
        int feeder = t.Up("feeder, up the left", -6, -7, 1);                      // run 0
        int keep = t.West("red fork: keeps it, across to the red branch", 2, -7, -7);
        int redBranch = t.Down("down to the red flag", -8, 1, -3);
        int pass = t.East("red fork: passes it on, over the knights", 2, -5, 5);
        int drop = t.Down("down to the blue fork", 6, 1, -1);
        int bl = t.West("blue fork: across to its left prong", -2, 5, 5);
        int br = t.East("blue fork: across to its right prong", -2, 7, 7);
        int leftProng = t.Down("left prong, blue flag", 4, -3, -6);
        int rightProng = t.Down("right prong, green flag", 8, -3, -6);

        t.Corner(SE, -8, 2); t.Corner(SW, 6, 2); t.Corner(SE, 4, -2); t.Corner(SW, 8, -2);
        t.Fork(RedLever, -6, 2, SW, SE);      // at rest: keeps it
        t.Fork(BlueLever, 6, -2, NW, NE);     // at rest: to the left prong

        t.Link(feeder, keep, RedLever, false); t.Link(feeder, pass, RedLever, true);
        t.Link(keep, redBranch); t.Link(pass, drop);
        t.Link(drop, bl, BlueLever, false); t.Link(drop, br, BlueLever, true);
        t.Link(bl, leftProng); t.Link(br, rightProng);

        t.Flag(redBranch, 3.5f, CartColor.Red, RailFlagSide.WestOrSouth);
        t.Flag(leftProng, 1f, CartColor.Blue, RailFlagSide.WestOrSouth);
        t.Flag(rightProng, 1f, CartColor.Green, RailFlagSide.EastOrNorth);
        return t;
    }

    // 3. Over and under. The top lane runs left to right over the knights and drops
    //    into the right fork; the bottom lane runs right to left under them and
    //    climbs into the left fork.
    //
    //   ===========================+          y= 3   top lane, blue and green
    //    |red      |yellow         |
    //    +----R----+   knights  +--B--+       y=-1 / 0
    //         |                 |green|blue
    //         +=========================      y=-4   bottom lane, red and yellow
    private static Track OverAndUnder()
    {
        var t = new Track();
        int top = t.East("TOP lane, left to right", 3, -11, 5);                   // run 0
        int bottom = t.West("BOTTOM lane, right to left", -4, 11, -5);            // run 1
        int down = t.Down("top lane: down into the right fork", 6, 2, 1);
        int rl = t.West("right fork: across to its left prong", 0, 5, 5);
        int rr = t.East("right fork: across to its right prong", 0, 7, 7);
        int rlp = t.Down("right fork: left prong, green flag", 4, -1, -2);
        int rrp = t.Down("right fork: right prong, blue flag", 8, -1, -2);
        int up = t.Up("bottom lane: up into the left fork", -6, -3, -2);
        int ll = t.West("left fork: across to its left prong", -1, -7, -7);
        int lr = t.East("left fork: across to its right prong", -1, -5, -5);
        int llp = t.Up("left fork: left prong, red flag", -8, 0, 1);
        int lrp = t.Up("left fork: right prong, yellow flag", -4, 0, 1);

        t.Corner(SW, 6, 3); t.Corner(SE, 4, 0); t.Corner(SW, 8, 0);
        t.Corner(NE, -6, -4); t.Corner(NE, -8, -1); t.Corner(NW, -4, -1);
        t.Fork(BlueLever, 6, 0, NE, NW);      // at rest: to the right prong (blue flag)
        t.Fork(RedLever, -6, -1, SW, SE);     // at rest: to the left prong (red flag)

        t.Link(top, down);
        t.Link(down, rr, BlueLever, false); t.Link(down, rl, BlueLever, true);
        t.Link(rl, rlp); t.Link(rr, rrp);
        t.Link(bottom, up);
        t.Link(up, ll, RedLever, false); t.Link(up, lr, RedLever, true);
        t.Link(ll, llp); t.Link(lr, lrp);

        t.Flag(rlp, 0.5f, CartColor.Green, RailFlagSide.WestOrSouth);
        t.Flag(rrp, 0.5f, CartColor.Blue, RailFlagSide.EastOrNorth);
        t.Flag(llp, 1f, CartColor.Red, RailFlagSide.WestOrSouth, new Vector2(0f, 0.5f));
        t.Flag(lrp, 1f, CartColor.Yellow, RailFlagSide.EastOrNorth, new Vector2(0f, 0.5f));
        return t;
    }

    // 4. The fountain. Up the middle between the knights to the red fork, out
    //    along the top both ways, down both sides to a blue fork each.
    //
    //    +-----------R-----------+            y= 3
    //    |           |           |
    //    |    knight | knight    |
    //  +-B-+         |         +-B-+          y=-2
    //  |   |         |         |   |
    // red yellow              green blue
    private static Track Fountain()
    {
        var t = new Track();
        int trunk = t.Up("trunk, up between the knights", 0, -7, 2);              // run 0
        int la = t.West("red fork: left arm", 3, -1, -5);
        int ra = t.East("red fork: right arm", 3, 1, 5);
        int ls = t.Down("left side, down to its blue fork", -6, 2, -1);
        int rs = t.Down("right side, down to its blue fork", 6, 2, -1);
        int lsl = t.West("left blue fork: across to its left prong", -2, -7, -7);
        int lsr = t.East("left blue fork: across to its right prong", -2, -5, -5);
        int rsl = t.West("right blue fork: across to its left prong", -2, 5, 5);
        int rsr = t.East("right blue fork: across to its right prong", -2, 7, 7);
        int red = t.Down("prong, red flag", -8, -3, -6);
        int yellow = t.Down("prong, yellow flag", -4, -3, -6);
        int green = t.Down("prong, green flag", 4, -3, -6);
        int blue = t.Down("prong, blue flag", 8, -3, -6);

        t.Corner(SE, -6, 3); t.Corner(SW, 6, 3);
        t.Corner(SE, -8, -2); t.Corner(SW, -4, -2); t.Corner(SE, 4, -2); t.Corner(SW, 8, -2);
        t.Fork(RedLever, 0, 3, SW, SE);       // at rest: to the left side
        t.Fork(BlueLever, -6, -2, NW, NE);    // at rest: to the left prong of the pair
        t.Fork(BlueLever, 6, -2, NW, NE);     // the same lever turns this one too

        t.Link(trunk, la, RedLever, false); t.Link(trunk, ra, RedLever, true);
        t.Link(la, ls); t.Link(ra, rs);
        t.Link(ls, lsl, BlueLever, false); t.Link(ls, lsr, BlueLever, true);
        t.Link(rs, rsl, BlueLever, false); t.Link(rs, rsr, BlueLever, true);
        t.Link(lsl, red); t.Link(lsr, yellow); t.Link(rsl, green); t.Link(rsr, blue);

        t.Flag(red, 1f, CartColor.Red, RailFlagSide.WestOrSouth);
        t.Flag(yellow, 1f, CartColor.Yellow, RailFlagSide.EastOrNorth);
        t.Flag(green, 1f, CartColor.Green, RailFlagSide.WestOrSouth);
        t.Flag(blue, 1f, CartColor.Blue, RailFlagSide.EastOrNorth);
        return t;
    }

    // 5. Twin wheels. A one-way ring each side, 18 units round. A track comes up
    //    from the bottom and runs straight onto each. Each wheel has two doors half
    //    a lap apart — the outer top corner and the inner bottom corner — and both
    //    are the same lever's. At rest a door is shut: the corner carries on round.
    //
    //  red<-R----+                +----B->blue     y= 2
    //       |    |    knights     |    |
    //       +----R->yel    green<-B----+           y=-3
    //       |                          |
    //       ^ in                    in ^
    private static Track TwinWheels()
    {
        var t = new Track();
        int leftIn = t.Up("LEFT wheel: the way in", -8, -7, -4);                  // run 0
        int rightIn = t.Up("RIGHT wheel: the way in", 8, -7, -4);                 // run 1

        // left wheel, clockwise
        int lUp = t.Up("left wheel: up its outer side", -8, -2, 1);
        int lTop = t.East("left wheel: along the top", 2, -7, -5);
        int lDown = t.Down("left wheel: down its inner side", -4, 1, -2);
        int lBack = t.West("left wheel: back along the bottom", -3, -5, -7);
        int lRed = t.West("left wheel: out to the red flag", 2, -9, -9);
        int lYellow = t.East("left wheel: out to the yellow flag", -3, -3, -2);

        // right wheel, the mirror image, so it turns the other way
        int rUp = t.Up("right wheel: up its outer side", 8, -2, 1);
        int rTop = t.West("right wheel: along the top", 2, 7, 5);
        int rDown = t.Down("right wheel: down its inner side", 4, 1, -2);
        int rBack = t.East("right wheel: back along the bottom", -3, 5, 7);
        int rBlue = t.East("right wheel: out to the blue flag", 2, 9, 9);
        int rGreen = t.West("right wheel: out to the green flag", -3, 3, 2);

        t.Corner(SW, -4, 2); t.Corner(NE, -8, -3);
        t.Corner(SE, 4, 2); t.Corner(NW, 8, -3);
        t.Fork(RedLever, -8, 2, SE, SW);      // shut: round the top. Open: out to the red flag
        t.Fork(RedLever, -4, -3, NW, NE);     // shut: round the bottom. Open: out to the yellow flag
        t.Fork(BlueLever, 8, 2, SW, SE);
        t.Fork(BlueLever, 4, -3, NE, NW);

        t.Link(leftIn, lUp);
        t.Link(lUp, lTop, RedLever, false); t.Link(lUp, lRed, RedLever, true);
        t.Link(lTop, lDown);
        t.Link(lDown, lBack, RedLever, false); t.Link(lDown, lYellow, RedLever, true);
        t.Link(lBack, lUp);

        t.Link(rightIn, rUp);
        t.Link(rUp, rTop, BlueLever, false); t.Link(rUp, rBlue, BlueLever, true);
        t.Link(rTop, rDown);
        t.Link(rDown, rBack, BlueLever, false); t.Link(rDown, rGreen, BlueLever, true);
        t.Link(rBack, rUp);

        // Each flag stands just past the end of its short way out
        t.Flag(lRed, 0f, CartColor.Red, RailFlagSide.EastOrNorth, new Vector2(-0.2f, 0.6f));
        t.Flag(lYellow, 1f, CartColor.Yellow, RailFlagSide.EastOrNorth, new Vector2(0.8f, -0.4f));
        t.Flag(rBlue, 0f, CartColor.Blue, RailFlagSide.EastOrNorth, new Vector2(0.2f, 0.6f));
        t.Flag(rGreen, 1f, CartColor.Green, RailFlagSide.EastOrNorth, new Vector2(-0.8f, -0.4f));
        return t;
    }

    // A track being written down: runs named by the way a cart travels them, the
    // corner tiles, the links from one run to the next, and the flags. Every link
    // hands over on a corner tile, one cell past the end of the run it leaves and
    // one cell before the run it joins.
    private sealed class Track
    {
        private struct RunSpec
        {
            public string Label; public RailPieceKind Piece; public RailAxis Axis;
            public float Offset; public float Start; public int Count; public RailFlow Flow;
        }

        private struct TileSpec
        {
            public string Label; public RailPieceKind Piece; public Vector2 At;
            public RailSwitchColor Color; public bool WhenThrown;
        }

        private struct LinkSpec
        {
            public int From; public int To; public RailSwitchColor Color; public bool WhenThrown;
        }

        private struct FlagSpec
        {
            public int Run; public float Along; public CartColor Flag; public RailFlagSide Side; public Vector2 Shift;
        }

        private readonly List<RunSpec> _runs = new List<RunSpec>();
        private readonly List<TileSpec> _tiles = new List<TileSpec>();
        private readonly List<LinkSpec> _links = new List<LinkSpec>();
        private readonly List<FlagSpec> _flags = new List<FlagSpec>();

        /// <summary>A run travelled north, from cell <paramref name="from"/> up to cell <paramref name="to"/>.</summary>
        public int Up(string label, float x, int from, int to)
        {
            return Add(label, RailPieceKind.Vertical, RailAxis.Vertical, x, from, to - from + 1, RailFlow.Forward);
        }

        /// <summary>A run travelled south, from cell <paramref name="from"/> down to cell <paramref name="to"/>.</summary>
        public int Down(string label, float x, int from, int to)
        {
            return Add(label, RailPieceKind.Vertical, RailAxis.Vertical, x, to, from - to + 1, RailFlow.Reverse);
        }

        /// <summary>A run travelled east, from cell <paramref name="from"/> to cell <paramref name="to"/>.</summary>
        public int East(string label, float y, int from, int to)
        {
            return Add(label, RailPieceKind.Horizontal, RailAxis.Horizontal, y, from, to - from + 1, RailFlow.Forward);
        }

        /// <summary>A run travelled west, from cell <paramref name="from"/> to cell <paramref name="to"/>.</summary>
        public int West(string label, float y, int from, int to)
        {
            return Add(label, RailPieceKind.Horizontal, RailAxis.Horizontal, y, to, from - to + 1, RailFlow.Reverse);
        }

        private int Add(string label, RailPieceKind piece, RailAxis axis, float offset, float start, int count, RailFlow flow)
        {
            _runs.Add(new RunSpec { Label = label, Piece = piece, Axis = axis, Offset = offset, Start = start, Count = count, Flow = flow });
            return _runs.Count - 1;
        }

        /// <summary>An ordinary corner tile.</summary>
        public void Corner(RailPieceKind kind, float x, float y)
        {
            _tiles.Add(new TileSpec { Label = "corner", Piece = kind, At = new Vector2(x, y) });
        }

        /// <summary>The one coloured corner of a fork, written twice: how it lies at rest and how it lies once its lever is thrown.</summary>
        public void Fork(RailSwitchColor color, float x, float y, RailPieceKind atRest, RailPieceKind thrown)
        {
            string name = color.ToString().ToUpperInvariant();
            _tiles.Add(new TileSpec { Label = name + " corner, at rest", Piece = atRest, At = new Vector2(x, y), Color = color, WhenThrown = false });
            _tiles.Add(new TileSpec { Label = name + " corner, thrown", Piece = thrown, At = new Vector2(x, y), Color = color, WhenThrown = true });
        }

        public void Link(int from, int to)
        {
            _links.Add(new LinkSpec { From = from, To = to });
        }

        /// <summary>One of the two ways out of a fork.</summary>
        public void Link(int from, int to, RailSwitchColor color, bool whenThrown)
        {
            _links.Add(new LinkSpec { From = from, To = to, Color = color, WhenThrown = whenThrown });
        }

        public void Flag(int run, float along, CartColor flag, RailFlagSide side, Vector2 shift = default(Vector2))
        {
            _flags.Add(new FlagSpec { Run = run, Along = along, Flag = flag, Side = side, Shift = shift });
        }

        public void Write(SerializedObject so)
        {
            SerializedProperty runs = so.FindProperty("runs");
            runs.arraySize = _runs.Count;
            for (int i = 0; i < _runs.Count; i++)
            {
                SerializedProperty p = runs.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("label").stringValue = _runs[i].Label;
                p.FindPropertyRelative("piece").intValue = (int)_runs[i].Piece;
                p.FindPropertyRelative("axis").intValue = (int)_runs[i].Axis;
                p.FindPropertyRelative("offset").floatValue = _runs[i].Offset;
                p.FindPropertyRelative("spanViewport").boolValue = false;
                p.FindPropertyRelative("overhangCells").intValue = 0;
                p.FindPropertyRelative("start").floatValue = _runs[i].Start;
                p.FindPropertyRelative("count").intValue = _runs[i].Count;
                p.FindPropertyRelative("flow").intValue = (int)_runs[i].Flow;
                p.FindPropertyRelative("layFromFarEnd").boolValue = false;
            }

            SerializedProperty tiles = so.FindProperty("tiles");
            tiles.arraySize = _tiles.Count;
            for (int i = 0; i < _tiles.Count; i++)
            {
                SerializedProperty p = tiles.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("label").stringValue = _tiles[i].Label;
                p.FindPropertyRelative("piece").intValue = (int)_tiles[i].Piece;
                p.FindPropertyRelative("position").vector2Value = _tiles[i].At;
                p.FindPropertyRelative("switchColor").intValue = (int)_tiles[i].Color;
                p.FindPropertyRelative("whenThrown").boolValue = _tiles[i].WhenThrown;
            }

            SerializedProperty links = so.FindProperty("teleports");
            links.arraySize = _links.Count;
            for (int i = 0; i < _links.Count; i++)
            {
                SerializedProperty p = links.GetArrayElementAtIndex(i);
                string gate = _links[i].Color == RailSwitchColor.None ? ""
                    : " (" + _links[i].Color.ToString().ToUpperInvariant() + (_links[i].WhenThrown ? " thrown)" : " at rest)");
                p.FindPropertyRelative("label").stringValue = _runs[_links[i].From].Label + "  ->  " + _runs[_links[i].To].Label + gate;
                p.FindPropertyRelative("fromRun").intValue = _links[i].From;
                p.FindPropertyRelative("toRun").intValue = _links[i].To;
                p.FindPropertyRelative("exitOvershoot").floatValue = 1f;
                p.FindPropertyRelative("entryLead").floatValue = 1f;
                p.FindPropertyRelative("switchColor").intValue = (int)_links[i].Color;
                p.FindPropertyRelative("whenThrown").boolValue = _links[i].WhenThrown;
            }

            SerializedProperty drops = so.FindProperty("drops");
            drops.arraySize = _flags.Count;
            for (int i = 0; i < _flags.Count; i++)
            {
                SerializedProperty p = drops.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("label").stringValue = _flags[i].Flag + " flag: " + _runs[_flags[i].Run].Label;
                p.FindPropertyRelative("run").intValue = _flags[i].Run;
                p.FindPropertyRelative("distanceAlong").floatValue = _flags[i].Along;
                p.FindPropertyRelative("flag").intValue = (int)_flags[i].Flag;
                p.FindPropertyRelative("flagSide").intValue = (int)_flags[i].Side;
                p.FindPropertyRelative("flagShift").vector2Value = _flags[i].Shift;
            }
        }
    }

    private static RailLayout WriteLayout(string name, RailPieceSet set, Track track)
    {
        string path = LayoutFolder + name + ".asset";
        var layout = AssetDatabase.LoadAssetAtPath<RailLayout>(path);
        if (layout == null)
        {
            layout = ScriptableObject.CreateInstance<RailLayout>();
            AssetDatabase.CreateAsset(layout, path);
        }

        var so = new SerializedObject(layout);
        so.FindProperty("pieceSet").objectReferenceValue = set;
        so.FindProperty("cellSize").floatValue = 1f;
        so.FindProperty("fallStagger").floatValue = 0.03f;
        track.Write(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(layout);
        return layout;
    }

    // ------------------------------------------------------------------
    // Prefabs
    // ------------------------------------------------------------------

    private static GameObject BuildRailPiece(Piece piece, string color, string path, List<string> report)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(piece.Prefab);
        Sprite[] frames = Frames(Art + piece.ArtName + "_" + color.ToLowerInvariant() + ".aseprite", report);
        if (source == null || frames.Length == 0)
        {
            report.Add($"FAILED {path}: source prefab or art missing");
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.name = System.IO.Path.GetFileNameWithoutExtension(path);

        var renderer = instance.GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.sprite = frames[frames.Length - 1]; // settled look

        var flipbook = instance.GetComponent<SpriteFlipbook>();
        if (flipbook != null) flipbook.EditorSetFrames(frames);

        GameObject asset = PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        return asset;
    }

    private static GameObject BuildFlag(string color, List<string> report)
    {
        string path = PlainFlag.Replace(".prefab", "_" + color + ".prefab");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(PlainFlag);
        Sprite[] frames = Frames(Art + "dropoff_flag_" + color.ToLowerInvariant() + ".aseprite", report);
        if (source == null || frames.Length == 0)
        {
            report.Add($"FAILED {path}: source prefab or art missing");
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.name = System.IO.Path.GetFileNameWithoutExtension(path);
        instance.GetComponent<SpriteRenderer>().sprite = frames[0];
        // One and a half times the delivery flag's size (owner, 2026-10-09; twice was too big): these are goals the
        // player has to find at a glance from the middle of the screen.
        instance.transform.localScale = new Vector3(SortingFlagScale, SortingFlagScale, 1f);

        GameObject asset = PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        return asset;
    }

    // The plain cart is the decorative one: a sprite, its rolling animation and
    // MineCart. No health and no collider, which is right here — a cart being
    // sorted is not something to shoot.
    private static GameObject BuildCart(string color, List<string> report)
    {
        string path = PlainCart.Replace(".prefab", "_" + color + ".prefab");
        string artPath = Art + "mine_cart_" + color.ToLowerInvariant() + ".aseprite";
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(PlainCart);
        Sprite[] frames = Frames(artPath, report);

        RuntimeAnimatorController controller = null;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(artPath))
        {
            var asController = o as RuntimeAnimatorController;
            if (asController != null) controller = asController;
        }

        if (source == null || frames.Length == 0 || controller == null)
        {
            report.Add($"FAILED {path}: source prefab, art or its animator controller missing");
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.name = System.IO.Path.GetFileNameWithoutExtension(path);
        instance.GetComponent<SpriteRenderer>().sprite = frames[0];
        instance.GetComponent<Animator>().runtimeAnimatorController = controller;

        GameObject asset = PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        report.Add($"{path}: {frames.Length} frame(s), controller {controller.name}");
        return asset;
    }

    // Frame 0 of the lever art leans left and frame 1 leans right. Which of the
    // two is "at rest" is the phase's to say — see CartSorting.Phase.
    private static GameObject BuildLever(string color, RailSwitchColor switchColor, List<string> report)
    {
        string path = "Assets/Rail_Switch_" + color + ".prefab";
        string artPath = Art + "rail_switch_" + color.ToLowerInvariant() + ".aseprite";
        Sprite[] frames = Frames(artPath, report);
        if (frames.Length < 2)
        {
            report.Add($"FAILED {path}: {artPath} needs two frames, found {frames.Length}");
            return null;
        }

        var go = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = frames[0];

        // The rails' own material, so the mine's lights fall on it the same way
        var rail = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rail_Horizontal.prefab");
        SpriteRenderer railRenderer = rail != null ? rail.GetComponent<SpriteRenderer>() : null;
        if (railRenderer != null) renderer.sharedMaterial = railRenderer.sharedMaterial;

        // The art's width and a knight's height. Standing half a unit either side
        // of the middle, the two boxes meet without overlapping: a sword's point
        // just reaches its own lever's box, and an arrow stops at the first lever
        // it meets.
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(1f, 1.1f);
        box.offset = Vector2.zero;

        var lever = go.AddComponent<RailSwitchLever>();
        var so = new SerializedObject(lever);
        so.FindProperty("color").intValue = (int)switchColor;
        so.FindProperty("body").objectReferenceValue = renderer;
        so.FindProperty("leanLeftSprite").objectReferenceValue = frames[0];
        so.FindProperty("leanRightSprite").objectReferenceValue = frames[1];
        SetSound(so.FindProperty("throwSound"), 0.9f, report, "switch_throw", "rail_land");
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject asset = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        report.Add($"{path}: left {frames[0].name}, right {frames[1].name}, rect {frames[0].rect.size}");
        return asset;
    }

    // Every sprite the importer made from one .aseprite, in frame order. A file
    // with several frames names them Frame_0, Frame_1...; a single frame is named
    // after the file.
    private static Sprite[] Frames(string artPath, List<string> report)
    {
        var frames = new List<Sprite>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(artPath))
        {
            var sprite = o as Sprite;
            if (sprite != null) frames.Add(sprite);
        }
        frames.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        if (frames.Count == 0)
        {
            report.Add($"NO SPRITES in {artPath}");
        }
        else if (!Mathf.Approximately(frames[0].pixelsPerUnit, 32f))
        {
            report.Add($"WRONG PPU in {artPath}: {frames[0].pixelsPerUnit} (want 32)");
        }
        return frames.ToArray();
    }

    // The first of <paramref name="clipNames"/> that exists in Assets/Sounds, so a
    // dedicated sound can replace a borrowed one by landing the file and
    // rebuilding.
    private static void SetSound(SerializedProperty effect, float volume, List<string> report,
                                 params string[] clipNames)
    {
        if (effect == null) return;

        foreach (string name in clipNames)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Sounds/{name}.wav");
            if (clip == null) continue;

            effect.FindPropertyRelative("clip").objectReferenceValue = clip;
            effect.FindPropertyRelative("volume").floatValue = volume;
            return;
        }
        report.Add($"NO SOUND for {effect.name}: none of [{string.Join(", ", clipNames)}] found");
    }

    // ------------------------------------------------------------------
    // The check: every phase played through on paper.
    //
    // It walks each cart along the track the way MineCart does — along a run,
    // across a link, onto the next — with a player who always sets each lever
    // just in time. It reports whether every cart can reach the flag of its own
    // colour, how many lever hits that takes, and the shortest time the player
    // ever has between one cart needing a lever one way and the next needing it
    // the other. It also checks the track itself: that every link meets on a
    // corner tile of the right shape, and that every flag is in the frame.
    //
    // It does not prove the phase is fun or fair. It proves it can be done.
    // ------------------------------------------------------------------

    [MenuItem("Tools/Two Knights/Check Cart Sorting Phases")]
    public static void Check()
    {
        var kit = AssetDatabase.LoadAssetAtPath<CartSortingKit>(KitPath);
        if (kit == null)
        {
            Debug.LogError("[CartSortingCheck] No kit asset — build the assets first.");
            return;
        }

        foreach (CartSorting.Phase phase in CartSorting.Phases)
        {
            RailLayout layout = kit.LayoutFor(phase.Track);
            if (layout == null)
            {
                Debug.LogError($"[CartSortingCheck] phase {phase.Number}: no track in the kit.");
                continue;
            }

            var problems = new List<string>();
            string summary = Simulate(layout, phase, problems);
            CheckTrack(layout, problems);
            foreach (CartSorting.Cart cart in phase.Carts)
            {
                if (kit.CartFor(cart.Color) == null) { problems.Add("no " + cart.Color + " cart prefab in the kit"); break; }
            }

            if (problems.Count == 0) Debug.Log($"[CartSortingCheck] phase {phase.Number} \"{phase.Name}\": OK. {summary}");
            else Debug.LogError($"[CartSortingCheck] phase {phase.Number} \"{phase.Name}\": {problems.Count} PROBLEM(S). {summary}\n - "
                                + string.Join("\n - ", problems));
        }
    }

    private sealed class SimCart
    {
        public CartColor Color; public int Line; public float Along; public float At; public bool Out; public bool Done;
    }

    private static string Simulate(RailLayout layout, CartSorting.Phase phase, List<string> problems)
    {
        var lines = new List<RailLine>();
        layout.BuildLines(lines, Vector2.zero, new Vector2(10f, 5.625f));
        IReadOnlyList<RailTeleport> links = layout.Teleports;
        IReadOnlyList<RailDrop> drops = layout.Drops;

        var carts = new List<SimCart>();
        foreach (CartSorting.Cart c in phase.Carts) carts.Add(new SimCart { Color = c.Color, Line = c.Line, At = c.At });

        bool[] thrown = new bool[3];
        int[] hits = new int[3];
        float[] lastUse = new float[3];
        float[] tightest = { 999f, 999f, 999f };

        const float Step = 1f / 240f;
        int home = 0;
        float finished = 0f;

        for (float t = 0f; t < 180f && home < carts.Count; t += Step)
        {
            foreach (SimCart cart in carts)
            {
                if (cart.Done) continue;
                if (!cart.Out)
                {
                    if (t < cart.At) continue;
                    cart.Out = true;
                    cart.Along = 0f;
                    if (cart.Line < 0 || cart.Line >= lines.Count)
                    {
                        problems.Add($"a cart is let go on run {cart.Line}, which the track does not have");
                        cart.Done = true; home++;
                        continue;
                    }
                }

                cart.Along += phase.Speed * Step;

                // at a flag?
                bool arrived = false;
                for (int d = 0; d < drops.Count && !arrived; d++)
                {
                    if (drops[d].run != cart.Line || cart.Along < drops[d].distanceAlong) continue;
                    arrived = true;
                    if (drops[d].flag != cart.Color)
                        problems.Add($"a {cart.Color} cart ends at the {drops[d].flag} flag at {t:F1}s");
                }
                if (arrived) { cart.Done = true; home++; finished = t; continue; }

                // at the end of its run?
                var ways = new List<RailTeleport>();
                for (int i = 0; i < links.Count; i++) if (links[i].fromRun == cart.Line) ways.Add(links[i]);

                if (ways.Count == 0)
                {
                    if (cart.Along > lines[cart.Line].Length + 2f)
                    {
                        problems.Add($"a {cart.Color} cart rolls off the end of \"{lines[cart.Line].Label}\" with no flag");
                        cart.Done = true; home++;
                    }
                    continue;
                }

                float exitAlong = lines[cart.Line].Length + ways[0].exitOvershoot;
                if (cart.Along < exitAlong) continue;

                RailTeleport way = ways[0];
                if (ways[0].switchColor != RailSwitchColor.None)
                {
                    // the way that gets this cart to its own flag soonest
                    float best = float.MaxValue;
                    foreach (RailTeleport option in ways)
                    {
                        float far = Distance(option.toRun, cart.Color, lines, links, drops, new HashSet<int>());
                        if (far < best) { best = far; way = option; }
                    }
                    if (best == float.MaxValue)
                    {
                        problems.Add($"a {cart.Color} cart has no way to a {cart.Color} flag from \"{lines[cart.Line].Label}\"");
                        cart.Done = true; home++;
                        continue;
                    }

                    int lever = (int)way.switchColor;
                    if (thrown[lever] != way.whenThrown)
                    {
                        thrown[lever] = way.whenThrown;
                        hits[lever]++;
                        tightest[lever] = Mathf.Min(tightest[lever], t - lastUse[lever]);
                    }
                    lastUse[lever] = t;
                }

                cart.Along = -way.entryLead + (cart.Along - exitAlong);
                cart.Line = way.toRun;
            }
        }

        if (home < carts.Count) problems.Add($"only {home} of {carts.Count} carts were dealt with in three minutes");
        if (phase.Seconds > 0f && finished > phase.Seconds)
            problems.Add($"a clean run takes {finished:F1}s but the clock is {phase.Seconds:F0}s");

        string Lever(int i, string name) => hits[i] == 0 ? $"{name} lever 0 hits"
            : $"{name} lever {hits[i]} hits (tightest {tightest[i]:F2}s)";
        string clock = phase.Seconds > 0f ? $", clock {phase.Seconds:F0}s" : "";
        return $"{carts.Count} carts, clean run {finished:F1}s{clock}; {Lever(1, "red")}; {Lever(2, "blue")}.";
    }

    // How far a cart has to roll from the mouth of `run` to reach a flag of its
    // colour, taking whichever way is shortest at every fork.
    private static float Distance(int run, CartColor color, List<RailLine> lines, IReadOnlyList<RailTeleport> links,
                                  IReadOnlyList<RailDrop> drops, HashSet<int> seen)
    {
        if (!seen.Add(run)) return float.MaxValue;

        float best = float.MaxValue;
        for (int d = 0; d < drops.Count; d++)
        {
            if (drops[d].run == run && drops[d].flag == color) best = Mathf.Min(best, drops[d].distanceAlong + 1f);
        }
        if (best < float.MaxValue) { seen.Remove(run); return best; }

        // a run with any flag on it ends there, right colour or not
        for (int d = 0; d < drops.Count; d++)
        {
            if (drops[d].run == run) { seen.Remove(run); return float.MaxValue; }
        }

        for (int i = 0; i < links.Count; i++)
        {
            if (links[i].fromRun != run) continue;
            float onward = Distance(links[i].toRun, color, lines, links, drops, seen);
            if (onward < float.MaxValue) best = Mathf.Min(best, lines[run].Length + 2f + onward);
        }
        seen.Remove(run);
        return best;
    }

    private static void CheckTrack(RailLayout layout, List<string> problems)
    {
        var lines = new List<RailLine>();
        layout.BuildLines(lines, Vector2.zero, new Vector2(10f, 5.625f));
        IReadOnlyList<RailTeleport> links = layout.Teleports;
        IReadOnlyList<RailTile> tiles = layout.Tiles;

        for (int i = 0; i < links.Count; i++)
        {
            RailTeleport link = links[i];
            if (link.fromRun < 0 || link.fromRun >= lines.Count || link.toRun < 0 || link.toRun >= lines.Count)
            {
                problems.Add($"link {i} names a run the track does not have");
                continue;
            }

            RailLine from = lines[link.fromRun];
            RailLine to = lines[link.toRun];
            Vector2 leaves = from.PointAt(from.Length + link.exitOvershoot);
            Vector2 joins = to.PointAt(-link.entryLead);
            if (Vector2.Distance(leaves, joins) > 0.01f)
            {
                problems.Add($"\"{from.Label}\" leaves at {leaves} but \"{to.Label}\" is joined at {joins}");
                continue;
            }

            // straight on needs no corner; a turn needs a tile that joins the side
            // the cart comes from to the side it leaves by
            if (from.Axis == to.Axis) continue;

            RailPieceKind wanted = CornerJoining(-from.Direction, to.Direction);
            bool found = false;
            for (int k = 0; k < tiles.Count; k++)
            {
                if (Vector2.Distance(tiles[k].position, leaves) > 0.01f) continue;
                if (tiles[k].piece != wanted) continue;
                if (tiles[k].switchColor != link.switchColor) continue;
                if (link.switchColor != RailSwitchColor.None && tiles[k].whenThrown != link.whenThrown) continue;
                found = true;
                break;
            }
            // A plain link may also turn on a fork's tile: the way IN to a wheel
            // passes straight through one. Only a missing shape is a problem.
            if (!found)
            {
                for (int k = 0; k < tiles.Count; k++)
                {
                    if (Vector2.Distance(tiles[k].position, leaves) <= 0.01f && tiles[k].piece == wanted
                        && link.switchColor == RailSwitchColor.None) { found = true; break; }
                }
            }
            if (!found) problems.Add($"no {wanted} tile at {leaves} for \"{from.Label}\" -> \"{to.Label}\"");
        }

        IReadOnlyList<RailDrop> drops = layout.Drops;
        for (int d = 0; d < drops.Count; d++)
        {
            if (drops[d].run < 0 || drops[d].run >= lines.Count) { problems.Add($"flag {d} is on a run the track does not have"); continue; }
            Vector2 at = lines[drops[d].run].PointAt(drops[d].distanceAlong);
            if (Mathf.Abs(at.x) > 9.6f || at.y > 4.6f || at.y < -5.1f)
                problems.Add($"the {drops[d].flag} flag's stopping point {at} is at or past the edge of the frame");
        }
    }

    // The corner tile that joins two sides of a cell. One is north or south, the
    // other east or west.
    private static RailPieceKind CornerJoining(Vector2 sideA, Vector2 sideB)
    {
        Vector2 vertical = Mathf.Abs(sideA.y) > 0.5f ? sideA : sideB;
        Vector2 horizontal = Mathf.Abs(sideA.x) > 0.5f ? sideA : sideB;
        bool north = vertical.y > 0f;
        bool east = horizontal.x > 0f;
        return north ? (east ? NE : NW) : (east ? SE : SW);
    }
}
