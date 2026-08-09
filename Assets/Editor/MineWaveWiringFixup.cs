using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// A one-shot repair for the disappearing-wave failure WaveManager warns about:
// a map entry reads as NULL when the map asset was imported before the wave asset
// it points at existed. The .asset on disk is correct, the script compiles, and
// the wave simply never turns up in the pool.
//
// That is exactly what happens to a wave authored as text while the editor is
// open — the map's YAML names a guid the AssetDatabase has never heard of, so the
// reference resolves to nothing on import and stays that way. Reimporting the
// waves first and the map second is the fix, and it has to happen in that order,
// which is the whole reason this file exists rather than a note telling someone to
// right-click things in the correct sequence.
//
// It is SELF-DISABLING: it checks the map first and returns silently when the
// setlist is already whole, so it costs one asset load per domain reload and says
// nothing once the wiring is good. Delete it once the Mine waves are settled.
public static class MineWaveWiringFixup
{
    private const string MapPath = "Assets/Scripts/Maps/The Mine.asset";

    // Everything authored offline for the twin-shaft waves. Reimported before the
    // map, so the map's references have something to resolve against.
    private static readonly string[] Dependencies =
    {
        "Assets/Scripts/Rails/Layouts/Mine Twin Shafts.asset",
        "Assets/Scripts/Rails/Layouts/Mine Twin Shafts Low Drop.asset",
        "Assets/Scripts/Rails/Layouts/Mine Twin Shafts High Drop.asset",
        "Assets/Enemy_GnomePickaxeCart.prefab",
    };

    private static readonly string[] WavePaths =
    {
        "Assets/Scripts/Waves/Cave/BitsAndPieces/Bits and Pieces 1.asset",
        "Assets/Scripts/Waves/Cave/BitsAndPieces/Bits and Pieces 2.asset",
        "Assets/Scripts/Waves/Cave/BitsAndPieces/Bits and Pieces 3.asset",
        "Assets/Scripts/Waves/Cave/BitsAndPieces/Bits and Pieces 4.asset",
        "Assets/Scripts/Waves/Cave/CartLoads/Cart Loads 1.asset",
        "Assets/Scripts/Waves/Cave/CartLoads/Cart Loads 2.asset",
        "Assets/Scripts/Waves/Cave/CartLoads/Cart Loads 3.asset",
        "Assets/Scripts/Waves/Cave/CartLoads/Cart Loads 4.asset",
    };

    [InitializeOnLoadMethod]
    private static void Schedule()
    {
        // delayCall rather than straight away: on the refresh that first imports
        // these files the AssetDatabase is still mid-import, and asking it to load
        // the map here would hand back exactly the half-resolved asset we are
        // trying to repair.
        EditorApplication.delayCall += () => Run(false);
    }

    [MenuItem("Tools/Two Knights/Fix Mine Wave Wiring")]
    private static void RunFromMenu()
    {
        Run(true);
    }

    private static void Run(bool verbose)
    {
        var map = AssetDatabase.LoadAssetAtPath<MapDefinition>(MapPath);
        if (map == null)
        {
            if (verbose) Debug.LogError("[MineWaveWiringFixup] Could not load " + MapPath);
            return;
        }

        if (IsWhole(map))
        {
            if (verbose)
            {
                Debug.Log("[MineWaveWiringFixup] The Mine's setlist is already whole — " +
                          "all 8 twin-shaft waves are wired and there are no null entries. " +
                          "Nothing to do; this file can be deleted.");
            }
            return;
        }

        // Waves and their dependencies FIRST, the map second. The order is the
        // entire point: reimporting the map against assets the database has not
        // seen yet just re-null-resolves the same references.
        for (int i = 0; i < Dependencies.Length; i++) ForceImport(Dependencies[i]);
        for (int i = 0; i < WavePaths.Length; i++) ForceImport(WavePaths[i]);
        ForceImport(MapPath);

        map = AssetDatabase.LoadAssetAtPath<MapDefinition>(MapPath);
        if (map == null) return;

        int added = Repair(map);

        if (IsWhole(map))
        {
            Debug.Log("[MineWaveWiringFixup] The Mine's setlist repaired: " + map.Waves.Count +
                      " waves, all resolving" + (added > 0 ? " (" + added + " re-added)" : "") +
                      ". Bits and Pieces 1-4 and Cart Loads 1-4 are in the pool.");
        }
        else
        {
            Debug.LogError("[MineWaveWiringFixup] The Mine's setlist is STILL incomplete after a forced " +
                           "reimport. Check that every wave .asset and its .cs.meta guid exist on disk, " +
                           "then run Tools > Two Knights > Fix Mine Wave Wiring again.");
        }
    }

    // Whole means two things, and both matter: nothing in the list failed to load,
    // and every wave we authored is actually referenced. A list with no nulls that
    // is simply missing entries looks perfectly healthy and still never plays.
    private static bool IsWhole(MapDefinition map)
    {
        var setlist = map.Waves;
        if (setlist == null) return false;

        var present = new HashSet<Object>();
        for (int i = 0; i < setlist.Count; i++)
        {
            if (setlist[i] == null) return false;
            present.Add(setlist[i]);
        }

        for (int i = 0; i < WavePaths.Length; i++)
        {
            var wave = AssetDatabase.LoadAssetAtPath<BaseWave>(WavePaths[i]);
            if (wave == null || !present.Contains(wave)) return false;
        }
        return true;
    }

    // Strips entries that still will not load and adds back any of ours that went
    // missing. Done through SerializedObject because the setlist is private and
    // this is a repair, not an authoring API anyone should be reaching for.
    private static int Repair(MapDefinition map)
    {
        var so = new SerializedObject(map);
        SerializedProperty waves = so.FindProperty("waves");
        if (waves == null || !waves.isArray) return 0;

        for (int i = waves.arraySize - 1; i >= 0; i--)
        {
            if (waves.GetArrayElementAtIndex(i).objectReferenceValue == null)
            {
                waves.DeleteArrayElementAtIndex(i);
            }
        }

        var present = new HashSet<Object>();
        for (int i = 0; i < waves.arraySize; i++)
        {
            present.Add(waves.GetArrayElementAtIndex(i).objectReferenceValue);
        }

        int added = 0;
        for (int i = 0; i < WavePaths.Length; i++)
        {
            var wave = AssetDatabase.LoadAssetAtPath<BaseWave>(WavePaths[i]);
            if (wave == null || present.Contains(wave)) continue;

            waves.InsertArrayElementAtIndex(waves.arraySize);
            waves.GetArrayElementAtIndex(waves.arraySize - 1).objectReferenceValue = wave;
            present.Add(wave);
            added++;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(map);
        AssetDatabase.SaveAssetIfDirty(map);
        return added;
    }

    private static void ForceImport(string path)
    {
        if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }
}
