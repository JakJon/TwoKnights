using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// Builds the Overseer fight as the owner sketched it on 2026-10-19: his own
// straight track across the top, three loops of crimson carts round the knights,
// the blue gnome that opens them, and the four stages of attacks. Unity runs it
// the next time it compiles or regains focus whenever Version below is ahead of
// what this machine last built; Tools ▸ Two Knights ▸ Wire The Overseer forces a
// rebuild.
//
// It lives here rather than being hand-authored as YAML for two reasons. The
// Mine's MapDefinition is rewritten from Unity's in-memory copy on every refresh,
// so a trueBoss guid typed in by hand is dropped the next time the editor saves.
// And the new prefabs point at sprites inside .aseprite files, whose ids only the
// importer knows. Going through AssetDatabase is the only wiring that sticks.
//
// The attack lists written below are a FIRST DRAFT for the owner to tune in the
// inspector. Running this again puts them back to the draft — so after tuning,
// leave Version alone. Tools ▸ Two Knights ▸ Check Overseer Attack Lists prints
// every landing at each knight, so a change can be checked without playing it.
public static class OverseerWiring
{
    private const string BossPrefabPath = "Assets/Enemy_DarkGnomeCart.prefab";
    private const string CrimsonPrefabPath = "Assets/Enemy_MineCart_Crimson.prefab";
    private const string BluePrefabPath = "Assets/Enemy_GnomeCart_Blue.prefab";
    private const string WaveAssetPath = "Assets/Scripts/Waves/Cave/DarkGnome/The Overseer.asset";
    private const string MapPath = "Assets/Scripts/Maps/The Mine.asset";
    private const string LayoutPath = "Assets/Scripts/Rails/Layouts/Mine Overseer Rings.asset";
    private const string PieceSetPath = "Assets/Scripts/Rails/Layouts/Mine Pieces.asset";

    private const string EmptyCartPath = "Assets/Enemy_MineCart_Empty.prefab";
    private const string PickaxeGnomePath = "Assets/Enemy_GnomePickaxeCart.prefab";
    private const string GreyCartArt = "Assets/Graphics/mine_cart.aseprite";
    private const string CrimsonCartArt = "Assets/Graphics/mine_cart_crimson.aseprite";
    private const string GnomeArt = "Assets/Graphics/gnome_mine_cart.aseprite";
    private const string BlueGnomeArt = "Assets/Graphics/gnome_mine_cart_blue.aseprite";
    private const string DeathCryPath = "Assets/Sounds/overseer_death.wav";

    // Bumped whenever this file changes what it writes, so an existing set of
    // assets is rebuilt instead of being left at the previous pass's values.
    // Without it the "already exists" guard means edits here never land.
    private const string VersionKey = "TwoKnights.OverseerWiring.Version";
    private const int Version = 5;

    [InitializeOnLoadMethod]
    private static void QueueIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            bool built = AssetDatabase.LoadAssetAtPath<DarkGnome>(WaveAssetPath) != null
                         && AssetDatabase.LoadAssetAtPath<RailLayout>(LayoutPath) != null;
            if (built && EditorPrefs.GetInt(VersionKey, 0) >= Version) return;
            Wire();
        };
    }

    [MenuItem("Tools/Two Knights/Wire The Overseer")]
    public static void Wire()
    {
        // The two recolours have to have imported first: both new prefabs point at
        // sprites inside them. Asked for here so a first run straight after the
        // files land does not build prefabs with holes in them.
        AssetDatabase.ImportAsset(CrimsonCartArt, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(BlueGnomeArt, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(DeathCryPath, ImportAssetOptions.ForceSynchronousImport);

        RailLayout layout = BuildLayout();
        GameObject crimson = BuildCrimsonCart();
        GameObject blue = BuildBlueGnome();
        GameObject boss = UpdateBossPrefab();
        if (layout == null || crimson == null || blue == null || boss == null)
        {
            Debug.LogError("[OverseerWiring] Stopped before the wave asset — see the errors above. Nothing is half-wired: run it again once they are fixed.");
            return;
        }

        DarkGnome wave = BuildWave(layout, boss, crimson, blue);
        if (wave == null) return;

        PointTheMineAtIt(wave);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorPrefs.SetInt(VersionKey, Version);
        Debug.Log("[OverseerWiring] Done: track, crimson cart, blue gnome, boss prefab, wave asset, and The Mine's true boss.");
    }

    // ---- the track ----

    // The board, in cells of one unit. A rail piece pivots at its bottom centre,
    // so a run at offset y fills y..y+1 — every offset below is the BOTTOM of its
    // row, and the rows are stacked so the shield, the three loops, the two orb
    // lanes and his track all fit between the top and bottom of the screen:
    //
    //   his track     4.625 .. 5.625   (the screen's top edge)
    //   orb lane      4.375 .. 4.625   health orb at 1000
    //   outer top     3.375 .. 4.375
    //   orb lane      3.125 .. 3.375   mana orb at 2000
    //   middle top    2.125 .. 3.125
    //   inner top     1.125 .. 2.125   (the shield reaches about y = 1.1)
    //   inner bottom -2.875 .. -1.875
    //   middle bottom-3.875 .. -2.875
    //   orb lane     -4.625 .. -3.875  mana orb at 2000
    //   outer bottom -5.625 .. -4.625  (the screen's bottom edge)
    //
    // Across: inner x = ±4, middle ±6, outer ±9, and the health orb at 3000 rises
    // up x = -5, between the inner and middle loops.
    private static RailLayout BuildLayout()
    {
        var pieceSet = AssetDatabase.LoadAssetAtPath<RailPieceSet>(PieceSetPath);
        if (pieceSet == null)
        {
            Debug.LogError($"[OverseerWiring] No rail piece set at {PieceSetPath}.");
            return null;
        }

        RailLayout layout = AssetDatabase.LoadAssetAtPath<RailLayout>(LayoutPath);
        bool fresh = layout == null;
        if (fresh) layout = ScriptableObject.CreateInstance<RailLayout>();

        const RailPieceKind H = RailPieceKind.Horizontal;
        const RailPieceKind V = RailPieceKind.Vertical;
        const RailFlow East = RailFlow.Forward;  // and north, on a vertical run
        const RailFlow West = RailFlow.Reverse;  // and south

        var runs = new List<RailRun>
        {
            // 0, 1: his track, both ways. The second lane is the same rails.
            Run("HIS TRACK (east) - he enters here", H, RailAxis.Horizontal, 4.625f, -11f, 23, East),
            Run("HIS TRACK (west) - the way back after a U-turn; no pieces of its own", H, RailAxis.Horizontal, 4.625f, -11f, 23, West, noPieces: true),

            // 2-5: inner loop, clockwise
            Run("INNER top (east)", H, RailAxis.Horizontal, 1.125f, -3f, 7, East),
            Run("INNER right (south)", V, RailAxis.Vertical, 4f, -1.875f, 3, West),
            Run("INNER bottom (west)", H, RailAxis.Horizontal, -2.875f, -3f, 7, West, layFromFarEnd: true),
            Run("INNER left (north)", V, RailAxis.Vertical, -4f, -1.875f, 3, East),

            // 6-9: middle loop, counterclockwise
            Run("MIDDLE top (west)", H, RailAxis.Horizontal, 2.125f, -5f, 11, West),
            Run("MIDDLE left (south)", V, RailAxis.Vertical, -6f, -2.875f, 5, West),
            Run("MIDDLE bottom (east)", H, RailAxis.Horizontal, -3.875f, -5f, 11, East, layFromFarEnd: true),
            Run("MIDDLE right (north)", V, RailAxis.Vertical, 6f, -2.875f, 5, East),

            // 10-13: outer loop, clockwise
            Run("OUTER top (east)", H, RailAxis.Horizontal, 3.375f, -8f, 17, East),
            Run("OUTER right (south)", V, RailAxis.Vertical, 9f, -4.625f, 8, West),
            Run("OUTER bottom (west)", H, RailAxis.Horizontal, -5.625f, -8f, 17, West, layFromFarEnd: true),
            Run("OUTER left (north)", V, RailAxis.Vertical, -9f, -4.625f, 8, East),

            // 14, 15: the two ways onto the outer loop from off-screen
            Run("WAY IN, top-left (east) - joins the outer top", H, RailAxis.Horizontal, 3.375f, -13f, 4, East),
            Run("WAY IN, top-right (west) - turns down the outer right side", H, RailAxis.Horizontal, 3.375f, 10f, 4, West),
        };

        // Elbows are named for the two directions they join (see RailTile): a
        // loop's top-left corner joins south and east, top-right west and south,
        // bottom-right north and west, bottom-left east and north.
        var tiles = new List<RailTile>();
        AddCorners(tiles, "INNER", 4f, 1.125f, -2.875f);
        AddCorners(tiles, "MIDDLE", 6f, 2.125f, -3.875f);
        AddCorners(tiles, "OUTER", 9f, 3.375f, -5.625f);
        // The two junctions, each a second piece laid over the outer corner
        tiles.Add(Tile("OUTER top-left junction: the left way in runs straight through", H, -9f, 3.375f));
        tiles.Add(Tile("OUTER top-right junction: the right way in turns down", RailPieceKind.CornerSouthEast, 9f, 3.375f));

        var teleports = new List<RailTeleport>
        {
            Corner("INNER top into the right side", 2, 3),
            Corner("INNER right side into the bottom", 3, 4),
            Corner("INNER bottom into the left side", 4, 5),
            Corner("INNER left side into the top", 5, 2),

            Corner("MIDDLE top into the left side", 6, 7),
            Corner("MIDDLE left side into the bottom", 7, 8),
            Corner("MIDDLE bottom into the right side", 8, 9),
            Corner("MIDDLE right side into the top", 9, 6),

            Corner("OUTER top into the right side", 10, 11),
            Corner("OUTER right side into the bottom", 11, 12),
            Corner("OUTER bottom into the left side", 12, 13),
            Corner("OUTER left side into the top", 13, 10),

            Corner("WAY IN top-left onto the outer top", 14, 10),
            Corner("WAY IN top-right onto the outer right side", 15, 11),
        };

        SetField(layout, "pieceSet", pieceSet);
        SetField(layout, "cellSize", 1f);
        SetField(layout, "fallStagger", 0.015f);
        SetField(layout, "runs", runs);
        SetField(layout, "tiles", tiles);
        SetField(layout, "teleports", teleports);
        SetField(layout, "drops", new List<RailDrop>());

        if (fresh) AssetDatabase.CreateAsset(layout, LayoutPath);
        else EditorUtility.SetDirty(layout);

        Debug.Log($"[OverseerWiring] Track -> {LayoutPath} ({runs.Count} runs, {tiles.Count} loose tiles, {teleports.Count} links).");
        return layout;
    }

    private static RailRun Run(string label, RailPieceKind piece, RailAxis axis, float offset, float start,
                               int count, RailFlow flow, bool layFromFarEnd = false, bool noPieces = false)
    {
        return new RailRun
        {
            label = label,
            piece = piece,
            axis = axis,
            offset = offset,
            start = start,
            count = count,
            flow = flow,
            layFromFarEnd = layFromFarEnd,
            noPieces = noPieces
        };
    }

    private static RailTile Tile(string label, RailPieceKind piece, float x, float y)
    {
        return new RailTile { label = label, piece = piece, position = new Vector2(x, y) };
    }

    private static void AddCorners(List<RailTile> tiles, string loop, float halfWidth, float top, float bottom)
    {
        tiles.Add(Tile(loop + " top-left elbow", RailPieceKind.CornerSouthEast, -halfWidth, top));
        tiles.Add(Tile(loop + " top-right elbow", RailPieceKind.CornerSouthWest, halfWidth, top));
        tiles.Add(Tile(loop + " bottom-right elbow", RailPieceKind.CornerNorthWest, halfWidth, bottom));
        tiles.Add(Tile(loop + " bottom-left elbow", RailPieceKind.CornerNorthEast, -halfWidth, bottom));
    }

    // One cell past the end of a run is the corner it turns at, and one cell
    // before the next run's mouth is that same corner, so the hand-off happens
    // in the middle of the elbow
    private static RailTeleport Corner(string label, int from, int to)
    {
        return new RailTeleport { label = label, fromRun = from, toRun = to, exitOvershoot = 1f, entryLead = 1f };
    }

    // ---- the crimson cart ----

    private static GameObject BuildCrimsonCart()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(EmptyCartPath);
        List<Sprite> grey = Frames(GreyCartArt);
        List<Sprite> red = Frames(CrimsonCartArt);
        if (source == null || grey.Count == 0 || red.Count != grey.Count)
        {
            Debug.LogError($"[OverseerWiring] Crimson cart needs {EmptyCartPath}, {GreyCartArt} and an imported " +
                           $"{CrimsonCartArt} with the same frames (got {grey.Count} grey, {red.Count} crimson).");
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.name = "Enemy_MineCart_Crimson";

        var cart = Swap<EnemyMineCart, EnemyCrimsonCart>(instance);

        var so = new SerializedObject(cart);
        SetString(so, "displayName", "Crimson Cart");
        SetSprites(so, "greyFrames", grey);
        SetSprites(so, "crimsonFrames", red);
        so.ApplyModifiedProperties();

        // What it looks like before the first frame plays — the prefab preview
        var body = instance.GetComponent<SpriteRenderer>();
        if (body != null) body.sprite = red[0];

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, CrimsonPrefabPath);
        Object.DestroyImmediate(instance);
        Debug.Log($"[OverseerWiring] Crimson cart -> {CrimsonPrefabPath} ({red.Count} frames).");
        return saved;
    }

    // ---- the blue gnome ----

    private static GameObject BuildBlueGnome()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(PickaxeGnomePath);
        Object[] blueArt = AssetDatabase.LoadAllAssetsAtPath(BlueGnomeArt);
        if (source == null || blueArt == null || blueArt.Length == 0)
        {
            Debug.LogError($"[OverseerWiring] Blue gnome needs {PickaxeGnomePath} and an imported {BlueGnomeArt}.");
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.name = "Enemy_GnomeCart_Blue";

        var gnome = Swap<EnemyGnomePickaxeCart, EnemyBlueGnomeCart>(instance);

        var so = new SerializedObject(gnome);
        SetString(so, "displayName", "Blue Gnome");
        Set(so, "windowSeconds", 15f);
        Set(so, "blinkSeconds", 5f);
        so.ApplyModifiedProperties();

        // Every reference into the plain gnome's art — the sprite, the Animator's
        // controller, any clip a component holds — moved to the same-named piece
        // of the blue file. Swapping only the sprite gives a gnome that turns back
        // to lavender the moment the wheels start.
        int moved = Repoint(instance, GnomeArt, blueArt);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, BluePrefabPath);
        Object.DestroyImmediate(instance);
        Debug.Log($"[OverseerWiring] Blue gnome -> {BluePrefabPath} ({moved} art reference(s) moved to the blue file).");
        return saved;
    }

    // ---- the boss ----

    // Edited in place: the prefab already carries his art, sounds and wiring, and
    // only what the new fight changes is touched
    private static GameObject UpdateBossPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath) == null)
        {
            Debug.LogError($"[OverseerWiring] No boss prefab at {BossPrefabPath}.");
            return null;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
        try
        {
            var boss = root.GetComponent<EnemyDarkGnomeCart>();
            var cart = root.GetComponent<MineCart>();
            if (boss == null || cart == null)
            {
                Debug.LogError($"[OverseerWiring] {BossPrefabPath} has no EnemyDarkGnomeCart or MineCart.");
                return null;
            }

            var so = new SerializedObject(boss);
            Set(so, "health", 1500f);
            SetString(so, "bossTitle", "The Overseer");
            SetRef(so, "pickaxePrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy_Pickaxe.prefab"));
            SetRef(so, "bombPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy_Bomb.prefab"));
            Set(so, "throwRange", 9f);
            Set(so, "axePace", 0.5f);
            Set(so, "roarSeconds", 2f);
            SerializedProperty cry = Find(so, "deathCry");
            if (cry != null)
            {
                cry.FindPropertyRelative("clip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(DeathCryPath);
                cry.FindPropertyRelative("volume").floatValue = 1f;
            }
            so.ApplyModifiedProperties();

            // Short lead-in and run-out: he turns and wraps just past the screen
            // edge rather than six units out, so his time off-screen is about a second
            var rails = new SerializedObject(cart);
            Set(rails, "leadIn", 1.5f);
            Set(rails, "runOut", 1.5f);
            rails.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log($"[OverseerWiring] Boss prefab updated: 1500 HP, death cry, lead-in/run-out 1.5.");
        return AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
    }

    // ---- the wave ----

    private static DarkGnome BuildWave(RailLayout layout, GameObject boss, GameObject crimson, GameObject blue)
    {
        DarkGnome wave = AssetDatabase.LoadAssetAtPath<DarkGnome>(WaveAssetPath);
        bool fresh = wave == null;
        if (fresh) wave = ScriptableObject.CreateInstance<DarkGnome>();

        var so = new SerializedObject(wave);
        SetString(so, "waveName", "The Overseer");
        Set(so, "weight", 1000f);
        SetBool(so, "isUnlocked", false);
        SetInt(so, "unlockedAfterXWaves", 19);
        SetInt(so, "lockedAfterXWaves", -1);
        SetBool(so, "useEnemyTracking", true);
        so.ApplyModifiedProperties();

        SetField(wave, "railLayout", layout);
        SetField(wave, "bossLine", 0);
        SetField(wave, "bossReturnLine", 1);
        SetField(wave, "innerLine", 2);
        SetField(wave, "middleLine", 6);
        SetField(wave, "outerLine", 10);
        SetField(wave, "leftEntranceLine", 14);
        SetField(wave, "rightEntranceLine", 15);

        SetField(wave, "bossPrefab", boss);
        SetField(wave, "entryDelay", 0.5f);
        SetField(wave, "firstAttackDelay", 3.5f);
        SetField(wave, "roarAtHealth", 450f);
        SetField(wave, "rockSpeed", 0f);
        // A second blue gnome before each bomb run in every stage but the last
        // (owner, 2026-10-20)
        List<OverseerStage> stages = Stages();
        for (int i = 0; i < stages.Count - 1; i++) stages[i].secondBlueGnome = true;
        SetField(wave, "stages", stages);

        SetField(wave, "crimsonCartPrefab", crimson);
        SetField(wave, "innerGroups", new List<int> { 2, 2, 2 });
        SetField(wave, "middleGroups", new List<int> { 2, 3, 2, 3, 2 });
        SetField(wave, "outerSingles", 10);
        SetField(wave, "dropHeight", 7f);
        SetField(wave, "dropSeconds", 0.55f);
        SetField(wave, "dropStagger", 0.12f);
        SetField(wave, "landingDust", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FX_RailDust.prefab"));
        SetField(wave, "outerCap", 24);
        SetField(wave, "mergeGap", 1.6f);

        SetField(wave, "firstAmbushAfter", 0f);
        SetField(wave, "nextAmbushAfterWindow", 25f);
        SetField(wave, "bombRunAfterBlue", 15f);
        SetField(wave, "bombRunRest", 2.5f);
        SetField(wave, "secondBlueLead", 5f);
        SetField(wave, "gnomeRotation", new List<GameObject>
        {
            Load("Assets/Enemy_GnomeMineCart.prefab"),          // powder bomb
            Load("Assets/Enemy_GnomeMineCart_Poison.prefab"),   // green bomb
            Load("Assets/Enemy_GnomePickaxeCart.prefab"),       // pickaxe
            Load("Assets/Enemy_GnomePickaxeCart_Purple.prefab") // pickaxe at both
        });
        SetField(wave, "blueGnomePrefab", blue);

        SetField(wave, "orbDrops", new List<OverseerOrbDrop>
        {
            Orb("health, just outside the inner loop, rising", 1150f, true, new Vector2(-5f, -7f), new Vector2(-5f, 7f)),
            Orb("mana, above the middle loop", 800f, false, new Vector2(-12f, 3.25f), new Vector2(12f, 3.25f)),
            Orb("mana, below the middle loop", 800f, false, new Vector2(12f, -4.25f), new Vector2(-12f, -4.25f)),
            Orb("health, just under his track", 450f, true, new Vector2(-12f, 4.5f), new Vector2(12f, 4.5f)),
        });

        if (fresh) AssetDatabase.CreateAsset(wave, WaveAssetPath);
        else EditorUtility.SetDirty(wave);

        Debug.Log($"[OverseerWiring] Wave asset {(fresh ? "created" : "rebuilt")} at {WaveAssetPath}.");
        return wave;
    }

    private static OverseerOrbDrop Orb(string label, float atHealth, bool health, Vector2 from, Vector2 to)
    {
        return new OverseerOrbDrop { label = label, atHealth = atHealth, healthOrb = health, from = from, to = to };
    }

    // ---- the first draft of the four attack lists ----
    //
    // Three moods, mixed in every stage (owner, 2026-10-19): both knights at once
    // (Split / Mirror / Scissors / TwinSpiral, axes split), one knight carrying it
    // while the other gets a long quiet to shoot (runs of entries at one knight),
    // and back and forth. Rests are measured landing to landing.

    private static List<OverseerStage> Stages()
    {
        // Stages of 350, 350, 350 and 450 of his health (owner, 2026-10-20).
        //
        // Second draft, the same day: the first was "too simple and similar" —
        // its rock was mostly pairs and flips from above, which is what his axes
        // already are. So the rock here is the shapes axes cannot be (spirals,
        // weaves, fans and flips from BELOW, scissors), each phase has a character
        // of its own, and "Together" entries put rock on one knight while he
        // throws at the other, so the two knights are answering different things
        // at once.
        //   phase 1  curves     spirals and fans
        //   phase 2  crossings  weaves and scissors
        //   phase 3  turns      two-pass spirals and tight flips, as he starts turning round
        //   phase 4  all of it, tighter
        return new List<OverseerStage>
        {
            Stage("1500: curves — wraps, six axes", 1500f, 1.5f, OverseerTurns.Wrap, 6, 4,
                Together("spiral on the right, axes at the left",
                    Spiral("spiral on the right", right: true, count: 6, degrees: 180f, spacing: 0.4f, rest: 0f),
                    AxeTarget.Left, 0.14f, 0f, 2.5f),
                Fan("fan up from below on the left", right: false, below: true, count: 4, degrees: 70f, spacing: 0.45f, salvo: 3, rest: 2.5f),
                Axes("axes at both", AxeTarget.Split, 0.15f, 2.5f),
                Weave("weave on the right", right: true, count: 4, salvo: 2, rest: 2.5f),
                Together("flip on the left from below, axes at the right",
                    Rock("flip on the left from below", VolleyShape.Flip, right: false, below: true, salvo: 3, spacing: 0.4f, rest: 0f),
                    AxeTarget.Right, 0.12f, 0.5f, 3f),
                TwinSpiral("twin spiral", count: 5, degrees: 160f, spacing: 0.45f, rest: 3.5f, passes: 1)),

            Stage("1150: crossings — faster, eight axes", 1150f, 1.8f, OverseerTurns.Wrap, 8, 6,
                Together("weave on the left, axes at the right",
                    Weave("weave on the left", right: false, count: 5, salvo: 2, rest: 0f),
                    AxeTarget.Right, 0.1f, 0.5f, 2.5f),
                Rock("scissors", VolleyShape.Scissors, right: false, below: false, salvo: 4, spacing: 0.35f, rest: 2f),
                BackAndForth("axes back and forth, two at a time", 2, 0.12f, 0.35f, 2f),
                Spiral("spiral on the right, there and back", right: true, count: 5, degrees: 200f, spacing: 0.35f, rest: 2f, passes: 2),
                Together("fan down on the right, axes at the left",
                    Fan("fan down on the right", right: true, below: false, count: 4, degrees: 75f, spacing: 0.45f, salvo: 3, rest: 0f),
                    AxeTarget.Left, 0.1f, 0f, 2.5f),
                Rock("mirror, left from below", VolleyShape.Mirror, right: false, below: true, salvo: 5, rest: 2.5f),
                Weave("weave on the left, opening underneath", right: false, count: 4, salvo: 3, rest: 3.5f, below: true)),

            Stage("800: turns — faster, turns round, ten axes, walls refilled", 800f, 2.1f, OverseerTurns.UTurn, 10, 8, refill: true,
                Together("spiral on the left there and back, axes at the right",
                    Spiral("spiral on the left, there and back", right: false, count: 6, degrees: 240f, spacing: 0.3f, rest: 0f, passes: 2),
                    AxeTarget.Right, 0.09f, 0f, 2.5f),
                Weave("weave on the right", right: true, count: 6, salvo: 3, rest: 2f),
                Axes("axes at both", AxeTarget.Split, 0.1f, 2f),
                Rock("scissors", VolleyShape.Scissors, right: false, below: true, salvo: 5, spacing: 0.25f, rest: 2f),
                Together("flip on the right, axes at the left",
                    Rock("flip on the right", VolleyShape.Flip, right: true, below: false, salvo: 5, spacing: 0.2f, rest: 0f),
                    AxeTarget.Left, 0.09f, 0.5f, 2.5f),
                Fan("fan up from below on the left", right: false, below: true, count: 5, degrees: 80f, spacing: 0.4f, salvo: 3, rest: 2f),
                TwinSpiral("twin spiral, there and back", count: 6, degrees: 200f, spacing: 0.35f, rest: 3.5f, passes: 2)),

            Stage("450: everything — roars, wraps twice then turns, twelve axes", 450f, 2.4f, OverseerTurns.WrapWrapUTurn, 12, 10,
                Together("weave on the left, axes at the right",
                    Weave("weave on the left", right: false, count: 6, salvo: 3, rest: 0f),
                    AxeTarget.Right, 0.08f, 0f, 2f),
                Together("weave on the right, axes at the left",
                    Weave("weave on the right", right: true, count: 6, salvo: 3, rest: 0f),
                    AxeTarget.Left, 0.08f, 0f, 2.5f),
                Rock("scissors", VolleyShape.Scissors, right: false, below: false, salvo: 6, spacing: 0.2f, rest: 1.8f),
                BackAndForth("axes three and three", 3, 0.08f, 0.25f, 1.8f),
                TwinSpiral("twin spiral, there and back", count: 7, degrees: 240f, spacing: 0.3f, rest: 2.5f, passes: 2),
                Together("spiral on the right there and back, axes at the left",
                    Spiral("spiral on the right, there and back", right: true, count: 7, degrees: 240f, spacing: 0.3f, rest: 0f, passes: 2),
                    AxeTarget.Left, 0.08f, 0f, 2.5f),
                Rock("mirror, left from below", VolleyShape.Mirror, right: false, below: true, salvo: 6, rest: 2f),
                Fan("fan up from below on the right", right: true, below: true, count: 5, degrees: 80f, spacing: 0.35f, salvo: 4, rest: 3.5f)),
        };
    }

    private static OverseerStage Stage(string label, float fromHealth, float speed, OverseerTurns turns,
                                       int axes, int gnomes, params OverseerAttack[] attacks)
    {
        return Stage(label, fromHealth, speed, turns, axes, gnomes, false, attacks);
    }

    private static OverseerStage Stage(string label, float fromHealth, float speed, OverseerTurns turns,
                                       int axes, int gnomes, bool refill, params OverseerAttack[] attacks)
    {
        return new OverseerStage
        {
            label = label,
            fromHealth = fromHealth,
            speedScale = speed,
            turns = turns,
            axesPerThrow = axes,
            gnomesPerAmbush = gnomes,
            refillInnerLoops = refill,
            attacks = new List<OverseerAttack>(attacks),
            ogres = OgresFor(fromHealth)
        };
    }

    // Green ogres as each stage starts (owner, 2026-10-20): one from each bottom
    // corner; from the second stage one more from the bottom centre a little
    // later, from the third two. Centre ogres have to be told their knight — the
    // centre line is the same distance from both — so they take the left, then
    // the right (my pick).
    private const float CentreDelay = 6f;
    private const float SecondCentreDelay = 9f;

    private static List<OverseerOgre> OgresFor(float fromHealth)
    {
        var ogres = new List<OverseerOgre>
        {
            Ogre("bottom-left corner", new Vector2(-11.5f, -6.5f), OgreMark.Nearest, 0f),
            Ogre("bottom-right corner", new Vector2(11.5f, -6.5f), OgreMark.Nearest, 0f),
        };
        if (fromHealth <= 1150f)
        {
            ogres.Add(Ogre("bottom centre, for the left knight", new Vector2(-0.5f, -7.5f), OgreMark.LeftKnight, CentreDelay));
        }
        if (fromHealth <= 800f)
        {
            ogres.Add(Ogre("bottom centre, for the right knight", new Vector2(0.5f, -7.5f), OgreMark.RightKnight, SecondCentreDelay));
        }
        return ogres;
    }

    private static OverseerOgre Ogre(string label, Vector2 at, OgreMark mark, float after)
    {
        return new OverseerOgre { label = label, at = at, mark = mark, after = after };
    }

    private static OverseerAttack Axes(string label, AxeTarget target, float stagger, float rest)
    {
        return new OverseerAttack
        {
            label = label,
            kind = OverseerAttackKind.Axes,
            axeTarget = target,
            axeStagger = stagger,
            restAfter = rest
        };
    }

    // Rock on one knight and axes on the other, at once. `rock` supplies the
    // volley; the axes go at `target`, which must be the OTHER knight.
    private static OverseerAttack Together(string label, OverseerAttack rock, AxeTarget target, float stagger,
                                           float rockOffset, float rest)
    {
        return new OverseerAttack
        {
            label = label,
            kind = OverseerAttackKind.Together,
            rock = rock.rock,
            rockOffset = rockOffset,
            axeTarget = target,
            axeStagger = stagger,
            restAfter = rest
        };
    }

    private static OverseerAttack BackAndForth(string label, int perTurn, float stagger, float turnGap, float rest)
    {
        OverseerAttack attack = Axes(label, AxeTarget.BackAndForth, stagger, rest);
        attack.axesPerTurn = perTurn;
        attack.turnGap = turnGap;
        return attack;
    }

    private static OverseerAttack Rock(string label, VolleyShape shape, bool right, bool below, int salvo,
                                       float spacing = 1f, float rest = 3f)
    {
        return new OverseerAttack
        {
            label = label,
            kind = OverseerAttackKind.Rock,
            restAfter = rest,
            rock = new RockVolley
            {
                label = label,
                shape = shape,
                rightKnight = right,
                fromBelow = below,
                spacing = spacing,
                salvo = salvo,
                salvoStagger = 0.12f,
                restAfter = rest
            }
        };
    }

    private static OverseerAttack Fan(string label, bool right, bool below, int count, float degrees,
                                      float spacing, int salvo, float rest)
    {
        OverseerAttack attack = Rock(label, VolleyShape.Fan, right, below, salvo, spacing, rest);
        attack.rock.count = count;
        attack.rock.fanDegrees = degrees;
        return attack;
    }

    private static OverseerAttack Spiral(string label, bool right, int count, float degrees, float spacing, float rest,
                                         int passes = 1)
    {
        OverseerAttack attack = Rock(label, VolleyShape.Spiral, right, false, 1, spacing, rest);
        attack.rock.count = count;
        attack.rock.spiralDegrees = degrees;
        attack.rock.spiralPasses = passes;
        return attack;
    }

    private static OverseerAttack TwinSpiral(string label, int count, float degrees, float spacing, float rest,
                                             int passes = 1)
    {
        OverseerAttack attack = Rock(label, VolleyShape.TwinSpiral, false, false, 1, spacing, rest);
        attack.rock.count = count;
        attack.rock.spiralDegrees = degrees;
        attack.rock.spiralPasses = passes;
        return attack;
    }

    private static OverseerAttack Weave(string label, bool right, int count, int salvo, float rest, bool below = false)
    {
        OverseerAttack attack = Rock(label, VolleyShape.Weave, right, below, salvo, 1.2f, rest);
        attack.rock.count = count;
        attack.rock.weaveDegrees = 90f;
        return attack;
    }

    private static void PointTheMineAtIt(DarkGnome wave)
    {
        var map = AssetDatabase.LoadAssetAtPath<MapDefinition>(MapPath);
        if (map == null)
        {
            Debug.LogError($"[OverseerWiring] No map at {MapPath}; the boss exists but nothing schedules him.");
            return;
        }

        var so = new SerializedObject(map);
        SetRef(so, "trueBoss", wave);
        SetInt(so, "trueBossWaveNumber", 20);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(map);
    }

    // ---- the check ----

    /// <summary>
    /// Plays each stage's list twice on paper — every rock and every axe, at the
    /// moment it would land on its knight — and prints, per knight: how many
    /// things land in one time round the list, the longest quiet stretch, how many
    /// quiet stretches of three seconds or more there are, and any two landings
    /// from different entries closer together than half a second (which should
    /// never happen). Assumes he is always on screen and never put to sleep; in
    /// play the list only ever waits for him, which adds quiet and never removes it.
    /// </summary>
    [MenuItem("Tools/Two Knights/Check Overseer Attack Lists")]
    public static void CheckAttackLists()
    {
        DarkGnome wave = AssetDatabase.LoadAssetAtPath<DarkGnome>(WaveAssetPath);
        var stages = wave != null ? GetField<List<OverseerStage>>(wave, "stages") : null;
        if (stages == null || stages.Count == 0)
        {
            Debug.LogError($"[OverseerWiring] {WaveAssetPath} has no stages to check.");
            return;
        }

        // His own flight time, pace and all, off the boss prefab
        var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
        var boss = bossPrefab != null ? bossPrefab.GetComponent<EnemyDarkGnomeCart>() : null;
        float axeFlight = boss != null ? boss.AxeFlightTime : 1.1f;
        float throwRange = boss != null ? boss.ThrowRange : 9f;
        float rockSpeed = GetField<float>(wave, "rockSpeed");

        // A stand-in spawner with the knights where they stand in Main, so the
        // rock's own geometry (RockVolley.Plan) can be asked without a scene open
        var host = new GameObject("OverseerCheck") { hideFlags = HideFlags.HideAndDontSave };
        var left = new GameObject("Left") { hideFlags = HideFlags.HideAndDontSave };
        var right = new GameObject("Right") { hideFlags = HideFlags.HideAndDontSave };
        left.transform.position = new Vector3(-2f, -0.5f, 0f);
        right.transform.position = new Vector3(2f, -0.5f, 0f);

        var report = new StringBuilder();
        try
        {
            Spawner spawner = host.AddComponent<Spawner>();
            spawner.projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Projectile_Rock.prefab");
            SetField(spawner, "_leftPlayer", left.transform);
            SetField(spawner, "_rightPlayer", right.transform);

            report.AppendLine($"Overseer attack lists — axes fly {axeFlight:F2}s, rock at {spawner.RockFlightSpeed(rockSpeed):F2} u/s.");
            foreach (OverseerStage stage in stages) CheckStage(report, spawner, stage, axeFlight, rockSpeed, throwRange);
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(left);
            Object.DestroyImmediate(right);
        }

        string text = report.ToString();
        Debug.Log(text);
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Library/OverseerAttackCheck.log"), text);
    }

    private struct Landing
    {
        public bool Right;
        public float At;
        public int Entry;
    }

    // The same clock DarkGnome.RunAttacks runs, on paper, with him always on
    // screen: rock committed up to CommitAhead early, and an axe entry committing
    // the rock behind it before it throws. Keep the two in step.
    private static void CheckStage(StringBuilder report, Spawner spawner, OverseerStage stage, float axeFlight,
                                   float rockSpeed, float throwRange)
    {
        report.AppendLine();
        report.AppendLine($"== {stage.label} — {stage.axesPerThrow} axes a throw, {stage.attacks.Count} entries");

        var landings = new List<Landing>();
        var axes = new List<OverseerAttack.AxeShot>();
        int count = stage.attacks.Count;

        float now = 0f;
        float lastLanding = 0f;
        float rest = 0f;
        float cycle = -1f;
        int n = 0;

        while (n < count * 2)
        {
            int entry = n++;
            OverseerAttack attack = stage.attacks[entry % count];

            if (attack.kind == OverseerAttackKind.Rock)
            {
                float first, last;
                List<Landing> rock = PlanRock(spawner, attack, rockSpeed, entry, out first, out last);
                float release = lastLanding + rest - first;
                now = Mathf.Max(now, release - DarkGnome.CommitAhead);
                release = Mathf.Max(release, now);
                Place(report, landings, rock, release, attack, entry, count, ref cycle);
                lastLanding = release + last;
                rest = Mathf.Max(0f, attack.restAfter);
                continue;
            }

            float busy = attack.PlanAxes(stage.axesPerThrow, axes);
            bool together = attack.kind == OverseerAttackKind.Together && attack.rock != null;
            float offset = together ? attack.rockOffset : 0f;
            float due = lastLanding + rest - axeFlight - Mathf.Max(0f, -offset);
            now = Mathf.Max(now, due - DarkGnome.CommitAhead);
            due = Mathf.Max(due, now);

            // The rock that goes with them, which may push the whole entry back
            float companionEnd = 0f;
            if (together)
            {
                float first, last;
                List<Landing> rock = PlanRock(spawner, attack, rockSpeed, entry, out first, out last);
                float late = now - (due + axeFlight + offset - first);
                if (late > 0f) due += late;
                float release = Mathf.Max(now, due + axeFlight + offset - first);
                Place(report, landings, rock, release, attack, entry, count, ref cycle, " (rock)");
                companionEnd = release + last;
            }

            var thrown = new List<Landing>();
            foreach (var axe in axes) thrown.Add(new Landing { Right = axe.Right, At = axe.Release + axeFlight, Entry = entry });
            Place(report, landings, thrown, due, attack, entry, count, ref cycle, together ? " (axes)" : "");
            float axesLast = Mathf.Max(due + busy + axeFlight, companionEnd);

            float speed = MineCart.TrackSpeed * Mathf.Max(0.1f, stage.speedScale);
            if (entry < count && !FitsOnePass(axes, speed, throwRange))
            {
                report.AppendLine($"          ^ too long for one pass at {speed:F2} u/s with every axe in range of " +
                                  "its knight: the rest wait for his next pass");
            }

            lastLanding = axesLast;
            rest = Mathf.Max(0f, attack.restAfter);

            // The look ahead: rock straight after an axe entry that has to be in
            // the air before the last axe leaves is committed now
            while (n < count * 2 && stage.attacks[n % count].kind == OverseerAttackKind.Rock)
            {
                OverseerAttack following = stage.attacks[n % count];
                float first, last;
                List<Landing> rock = PlanRock(spawner, following, rockSpeed, n, out first, out last);
                float release = lastLanding + rest - first;
                if (release >= due + busy) break;

                release = Mathf.Max(release, now);
                Place(report, landings, rock, release, following, n, count, ref cycle);
                n++;
                lastLanding = release + last;
                rest = Mathf.Max(0f, following.restAfter);
            }

            now = due + busy;
        }

        float roundOne = float.PositiveInfinity;
        foreach (var l in landings) if (l.Entry == 0) roundOne = Mathf.Min(roundOne, l.At);
        float period = cycle - roundOne;
        report.AppendLine($"  one time round the list: {period:F1}s");

        // Judged on the SECOND time round, which has the first one behind it —
        // the join between the end of the list and its start is where a list
        // goes wrong without anyone noticing
        Knight(report, "left ", landings, false, cycle, cycle + period);
        Knight(report, "right", landings, true, cycle, cycle + period);
    }

    // Could a whole axe entry go out in one pass, every axe leaving with its own
    // knight inside his throw range (the rule EnemyDarkGnomeCart.ReadyAt plans
    // by), in either direction along his track? Knights' bodies taken at (±2, 0),
    // his track at y = 4.625 (Mine Overseer Rings, run 0).
    private static bool FitsOnePass(List<OverseerAttack.AxeShot> axes, float speed, float range)
    {
        const float trackY = 4.625f;
        var leftAim = new Vector2(-2f, 0f);
        var rightAim = new Vector2(2f, 0f);

        for (int direction = -1; direction <= 1; direction += 2)
        {
            for (int step = 0; step <= 480; step++)
            {
                float start = -12f + step * 0.05f;
                bool fits = true;
                for (int i = 0; i < axes.Count && fits; i++)
                {
                    float x = start + direction * speed * axes[i].Release;
                    Vector2 aim = axes[i].Right ? rightAim : leftAim;
                    float dx = x - aim.x;
                    float dy = trackY - aim.y;
                    fits = dx * dx + dy * dy <= range * range;
                }
                if (fits) return true;
            }
        }
        return false;
    }

    private static List<Landing> PlanRock(Spawner spawner, OverseerAttack attack, float rockSpeed, int entry,
                                          out float first, out float last)
    {
        var shots = new List<RockVolley.PlannedShot>();
        RockVolley.Plan(spawner, attack.rock, rockSpeed, shots);

        var rock = new List<Landing>();
        first = float.PositiveInfinity;
        last = 0f;
        foreach (var shot in shots)
        {
            float at = RockVolley.LandingOffset(spawner, shot, rockSpeed);
            first = Mathf.Min(first, at);
            last = Mathf.Max(last, at);
            rock.Add(new Landing { Right = shot.Knight == spawner.RightPlayer, At = at, Entry = entry });
        }
        if (float.IsPositiveInfinity(first)) first = 0f;
        return rock;
    }

    // Records an entry released at `release` and prints it the first time round
    private static void Place(StringBuilder report, List<Landing> landings, List<Landing> mine, float release,
                              OverseerAttack attack, int entry, int count, ref float cycle, string part = "")
    {
        float first = float.PositiveInfinity, last = 0f;
        for (int i = 0; i < mine.Count; i++)
        {
            Landing l = mine[i];
            first = Mathf.Min(first, l.At);
            last = Mathf.Max(last, l.At);
            l.At += release;
            landings.Add(l);
        }
        if (mine.Count == 0) return;

        if (entry < count)
        {
            string name = attack.label + part;
            report.AppendLine($"  {release,6:F1}s  {name,-52} lands {release + first,6:F1}–{release + last,6:F1}s  {Who(mine)}");
        }
        else if (entry == count)
        {
            // The second time round starts where the list's first entry lands again
            cycle = cycle < 0f ? release + first : Mathf.Min(cycle, release + first);
        }
    }

    private static string Who(List<Landing> mine)
    {
        bool l = false, r = false;
        foreach (var x in mine) { if (x.Right) r = true; else l = true; }
        return l && r ? "both" : (r ? "right" : "left");
    }

    private static void Knight(StringBuilder report, string name, List<Landing> all, bool right, float from, float to)
    {
        var mine = new List<Landing>();
        foreach (var l in all) if (l.Right == right) mine.Add(l);
        mine.Sort((a, b) => a.At.CompareTo(b.At));

        float longest = 0f;
        int quiet = 0;
        int inCycle = 0;
        var clashes = new StringBuilder();

        for (int i = 0; i < mine.Count; i++)
        {
            bool second = mine[i].At >= from && mine[i].At < to;
            if (second) inCycle++;
            if (i == 0) continue;

            float gap = mine[i].At - mine[i - 1].At;
            if (second)
            {
                longest = Mathf.Max(longest, gap);
                if (gap >= 3f) quiet++;
            }
            if (mine[i].Entry != mine[i - 1].Entry && gap < 0.5f)
            {
                clashes.Append($" {mine[i - 1].At:F1}/{mine[i].At:F1}");
            }
        }

        report.AppendLine($"  {name}: {inCycle} landings a time round, longest quiet {longest:F1}s, " +
                          $"{quiet} quiet stretch(es) of 3s+" +
                          (clashes.Length > 0 ? $", CLASHES at{clashes}" : ", no clashes"));
    }

    // ---- helpers ----

    /// <summary>
    /// Replace <typeparamref name="TFrom"/> with <typeparamref name="TTo"/> on
    /// <paramref name="go"/>, carrying every serialized field the two share
    /// across by property path — hurt and death sounds, damage text, health,
    /// animator wiring — so the new component is the same enemy underneath.
    /// </summary>
    private static TTo Swap<TFrom, TTo>(GameObject go) where TFrom : Component where TTo : Component
    {
        var from = go.GetComponent<TFrom>();
        var to = go.AddComponent<TTo>();
        if (from == null) return to;

        var source = new SerializedObject(from);
        var target = new SerializedObject(to);
        SerializedProperty walk = source.GetIterator();
        bool enterChildren = true;
        while (walk.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (walk.propertyPath == "m_Script") continue;
            SerializedProperty match = target.FindProperty(walk.propertyPath);
            if (match != null && match.propertyType == walk.propertyType) target.CopyFromSerializedProperty(walk);
        }
        target.ApplyModifiedProperties();
        Object.DestroyImmediate(from, true);
        return to;
    }

    // Every frame sprite in an .aseprite, in frame order
    private static List<Sprite> Frames(string path)
    {
        var frames = new List<Sprite>();
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            var sprite = asset as Sprite;
            if (sprite != null) frames.Add(sprite);
        }
        frames.Sort((a, b) => FrameIndex(a.name).CompareTo(FrameIndex(b.name)));
        return frames;
    }

    private static int FrameIndex(string name)
    {
        int underscore = name.LastIndexOf('_');
        int index;
        return underscore >= 0 && int.TryParse(name.Substring(underscore + 1), out index) ? index : 0;
    }

    private static void SetSprites(SerializedObject so, string name, List<Sprite> sprites)
    {
        SerializedProperty p = Find(so, name);
        if (p == null) return;
        p.arraySize = sprites.Count;
        for (int i = 0; i < sprites.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }

    // Every object reference on every component that points into `fromPath`,
    // moved to the same-named, same-typed object in `toAssets`
    private static int Repoint(GameObject go, string fromPath, Object[] toAssets)
    {
        int moved = 0;
        foreach (Component component in go.GetComponentsInChildren<Component>(true))
        {
            if (component == null) continue;
            var so = new SerializedObject(component);
            SerializedProperty p = so.GetIterator();
            bool changed = false;
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                Object current = p.objectReferenceValue;
                if (current == null || AssetDatabase.GetAssetPath(current) != fromPath) continue;

                string wanted = current.name.Replace("gnome_mine_cart", "gnome_mine_cart_blue");
                foreach (Object candidate in toAssets)
                {
                    if (candidate == null || candidate.GetType() != current.GetType()) continue;
                    if (candidate.name == current.name || candidate.name == wanted)
                    {
                        p.objectReferenceValue = candidate;
                        changed = true;
                        moved++;
                        break;
                    }
                }
            }
            if (changed) so.ApplyModifiedProperties();
        }
        return moved;
    }

    private static GameObject Load(string path)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null) Debug.LogWarning($"[OverseerWiring] Missing prefab: {path}");
        return go;
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = FindField(target.GetType(), name);
        if (field == null)
        {
            Debug.LogWarning($"[OverseerWiring] No field '{name}' on {target.GetType().Name}.");
            return;
        }
        field.SetValue(target, value);
    }

    private static T GetField<T>(object target, string name)
    {
        FieldInfo field = FindField(target.GetType(), name);
        return field != null ? (T)field.GetValue(target) : default(T);
    }

    private static FieldInfo FindField(System.Type type, string name)
    {
        for (System.Type t = type; t != null; t = t.BaseType)
        {
            FieldInfo field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) return field;
        }
        return null;
    }

    private static void Set(SerializedObject so, string name, float value)
    {
        SerializedProperty p = Find(so, name);
        if (p != null) p.floatValue = value;
    }

    private static void SetInt(SerializedObject so, string name, int value)
    {
        SerializedProperty p = Find(so, name);
        if (p != null) p.intValue = value;
    }

    private static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = Find(so, name);
        if (p != null) p.boolValue = value;
    }

    private static void SetString(SerializedObject so, string name, string value)
    {
        SerializedProperty p = Find(so, name);
        if (p != null) p.stringValue = value;
    }

    private static void SetRef(SerializedObject so, string name, Object value)
    {
        SerializedProperty p = Find(so, name);
        if (p != null) p.objectReferenceValue = value;
    }

    private static SerializedProperty Find(SerializedObject so, string name)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null) Debug.LogWarning($"[OverseerWiring] No serialized field '{name}' on {so.targetObject}.");
        return p;
    }
}
