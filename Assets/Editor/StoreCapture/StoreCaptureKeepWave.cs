#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// A wave that exists only for the camera. The Pallid Keep ships one wave so far,
// Looking Good, and it is a proving ground: eight panes placed to test the maths,
// nothing on the board, one pane sitting over the health bars. Right for checking
// a redirect, wrong for showing anybody what the mirrors are FOR.
//
// So this lays its own board and sends something across it. Four pairs, each
// facing its twin, so a shot keeps its heading and simply arrives somewhere else:
//
//   * CYAN and GREEN, on the side walls — out through one side, in through the
//     other. A knight shooting away from the fight has the arrow come back across
//     the hall from the far wall.
//   * MAGENTA and VERMILION, top and bottom — up through the ceiling, back in off
//     the floor, still climbing.
//
// The pairs sit off the knights' own row on purpose: a shot through a side pair
// comes back in well above or below the other knight instead of straight at him.
//
// It is never saved as an asset and never joins a setlist: StoreCapture builds one
// in memory and hands it to the test run as the wave to play. Editor-only, like
// the rest of this folder. And it keeps the waves' rule all the same — nothing
// here is rolled; the same bats and slimes arrive on the same seconds every take.
public class StoreCaptureKeepWave : BaseWave
{
    private const string BaseLayoutPath = "Assets/Scripts/Mirrors/Layouts/Keep Looking Good.asset";
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

    // Where each knight is told to shoot, in StoreCapture.SetAims form.
    public const string LeftAims = "-8.6:2.2;-5.5:4.4";
    public const string RightAims = "8.6:-1.8;5.5:4.4";

    public static StoreCaptureKeepWave Build()
    {
        var wave = CreateInstance<StoreCaptureKeepWave>();
        wave.name = "Hall of Mirrors";
        Set(typeof(BaseWave), wave, "waveName", "Hall of Mirrors");
        Set(typeof(BaseWave), wave, "weight", 1f);
        Set(typeof(BaseWave), wave, "isUnlocked", true);
        Set(typeof(BaseWave), wave, "useEnemyTracking", true);
        return wave;
    }

    // The same wave with the board left bare: no mirrors, nothing sent. A quiet
    // arena to stage a quest scene in. It holds itself open, because a wave whose
    // spawning is done and whose field is empty ends on the spot.
    private bool _quiet;

    public static StoreCaptureKeepWave BuildQuiet()
    {
        StoreCaptureKeepWave wave = Build();
        wave.name = "Between Waves";
        Set(typeof(BaseWave), wave, "waveName", "Between Waves");
        wave._quiet = true;
        return wave;
    }

    private static void Set(System.Type type, object target, string field, object value)
    {
        FieldInfo info = type.GetField(field, Hidden);
        if (info != null) info.SetValue(target, value);
        else Debug.LogWarning("[StoreCaptureKeepWave] " + type.Name + " has no field '" + field + "'");
    }

    private static MirrorPaneEntry Pane(string label, MirrorColor color, MirrorFacing facing, float x, float y)
    {
        return new MirrorPaneEntry { label = label, color = color, facing = facing, position = new Vector2(x, y) };
    }

    // The shipped layout is copied for its pane art and its redirect rule; only
    // the list of panes is this wave's own.
    private static MirrorLayout BuildBoard()
    {
        MirrorLayout shipped = AssetDatabase.LoadAssetAtPath<MirrorLayout>(BaseLayoutPath);
        if (shipped == null) return null;
        MirrorLayout board = Instantiate(shipped);
        board.name = "Hall of Mirrors (capture)";
        var panes = new List<MirrorPaneEntry>
        {
            Pane("side wrap high, left", MirrorColor.Cyan, MirrorFacing.Right, -8.6f, 2.2f),
            Pane("side wrap high, right", MirrorColor.Cyan, MirrorFacing.Left, 8.6f, 2.2f),
            Pane("side wrap low, left", MirrorColor.Green, MirrorFacing.Right, -8.6f, -1.8f),
            Pane("side wrap low, right", MirrorColor.Green, MirrorFacing.Left, 8.6f, -1.8f),
            Pane("rise, left top", MirrorColor.Magenta, MirrorFacing.Down, -5.5f, 4.4f),
            Pane("rise, left bottom", MirrorColor.Magenta, MirrorFacing.Up, -5.5f, -3.4f),
            Pane("rise, right top", MirrorColor.Vermilion, MirrorFacing.Down, 5.5f, 4.4f),
            Pane("rise, right bottom", MirrorColor.Vermilion, MirrorFacing.Up, 5.5f, -3.4f),
        };
        Set(typeof(MirrorLayout), board, "panes", panes);
        return board;
    }

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        if (_quiet)
        {
            yield return new WaitForSeconds(150f);
            MarkSpawningComplete();
            yield break;
        }

        MirrorNetwork mirrors = spawner.Mirrors;
        MirrorLayout board = BuildBoard();
        if (mirrors != null && board != null) mirrors.Lay(board);
        else Debug.LogWarning("[StoreCaptureKeepWave] No mirror board could be laid.");

        Transform left = spawner.LeftPlayer;
        Transform right = spawner.RightPlayer;

        // Slimes in off both walls on the knights' own row, where the shots that
        // went up through the ceiling come back in off the floor.
        for (int i = 0; i < 5; i++)
        {
            float at = 3f + i * 7f;
            spawner.SpawnSlime(2, new Vector2(-13f, 1f), at, left);
            spawner.SpawnSlime(2, new Vector2(13f, 1f), at + 1.5f, right);
        }

        // Bats off the corners in turn — top right first, across the path of the
        // arrows coming back in through the right-hand wall.
        float[] xs = { 11f, -11f, 9f, -9f, 11f, -11f };
        float[] ys = { 7f, -7f, 8f, -8f, -7f, 7f };
        for (int i = 0; i < 36; i++)
        {
            spawner.SpawnBat(new Vector2(xs[i % xs.Length], ys[i % ys.Length]), 2.5f + i * 1.05f);
        }

        yield return new WaitForSeconds(44f);
        MarkSpawningComplete();
        yield return new WaitForSeconds(60f);
    }
}
#endif
