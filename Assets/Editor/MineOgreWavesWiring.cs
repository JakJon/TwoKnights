using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// One-shot: brings The Mine's setlist up to date with its wave folders, and moves
// the Overseer to wave 25 without ending the run there, so a Mine run carries on
// to wave 30 (and past it) the way the forest does after the Twins. Unity runs
// this the next time it compiles; it records its version once it has succeeded
// and does nothing after that. Tools ▸ Two Knights ▸ Wire Mine Ogre Waves forces it.
//
//   v1  Hot Potato and Rush Hour, tiers I-IV.
//   v2  Every tiered Mine wave goes to seven tiers (owner, 2026-09-25): V, VI and
//       VII for all twelve, so 84 tiers plus the Millstone.
//
// It goes through the AssetDatabase rather than hand-edited YAML because The
// Mine's MapDefinition is rewritten from Unity's in-memory copy on refresh, and a
// wave reference typed into the .asset by hand is dropped the next time the
// editor saves it (see OverseerWiring).
public static class MineOgreWavesWiring
{
    private const string MapPath = "Assets/Scripts/Maps/The Mine.asset";

    // Folders this wiring adds to the map's list if they are missing
    private static readonly string[] NewFolders =
    {
        "Assets/Scripts/Waves/Cave/HotPotato",
        "Assets/Scripts/Waves/Cave/RushHour"
    };

    // Twelve waves of seven tiers, plus the Millstone. Fewer found means some
    // wave assets were imported before their scripts compiled; the pass is left
    // unrecorded so it tries again on the next compile.
    private const int ExpectedWaves = 12 * 7 + 1;

    private const int OverseerWave = 25;

    private const string VersionKey = "TwoKnights.MineOgreWavesWiring.Version";
    private const int Version = 2;

    [InitializeOnLoadMethod]
    private static void QueueIfNeeded()
    {
        if (EditorPrefs.GetInt(VersionKey, 0) >= Version) return;
        EditorApplication.delayCall += () => Wire();
    }

    [MenuItem("Tools/Two Knights/Wire Mine Ogre Waves")]
    public static void Wire()
    {
        var map = AssetDatabase.LoadAssetAtPath<MapDefinition>(MapPath);
        if (map == null)
        {
            Debug.LogError($"[MineOgreWavesWiring] No map at {MapPath}.");
            return;
        }

        var so = new SerializedObject(map);
        SerializedProperty folderList = so.FindProperty("waveFolders");
        foreach (string folder in NewFolders)
        {
            if (Contains(folderList, folder)) continue;
            folderList.arraySize++;
            folderList.GetArrayElementAtIndex(folderList.arraySize - 1).stringValue = folder;
        }

        var folders = new List<string>();
        for (int i = 0; i < folderList.arraySize; i++)
        {
            string folder = folderList.GetArrayElementAtIndex(i).stringValue;
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            folders.Add(folder);
            AssetDatabase.ImportAsset(folder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        }

        int found = AssetDatabase.FindAssets("t:BaseWave", folders.ToArray()).Length;
        if (found < ExpectedWaves)
        {
            Debug.LogWarning($"[MineOgreWavesWiring] Found {found} of {ExpectedWaves} Mine wave assets; " +
                             "leaving The Mine alone and trying again on the next compile.");
            return;
        }

        so.FindProperty("trueBossWaveNumber").intValue = OverseerWave;
        so.FindProperty("trueBossEndsRun").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();

        // The map's own "Find Waves In Folders": adds anything in its folders that
        // is not in the setlist yet, and leaves the rest alone
        MethodInfo find = typeof(MapDefinition).GetMethod("FindWavesInFolders",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (find == null)
        {
            Debug.LogError("[MineOgreWavesWiring] MapDefinition.FindWavesInFolders is gone; add the waves by hand.");
            return;
        }
        find.Invoke(map, null);

        EditorUtility.SetDirty(map);
        AssetDatabase.SaveAssets();
        EditorPrefs.SetInt(VersionKey, Version);

        Debug.Log($"[MineOgreWavesWiring] The Mine: {map.Waves.Count} waves in the setlist " +
                  $"(expected {ExpectedWaves}), Overseer at wave {map.TrueBossWaveNumber}, " +
                  $"run ends there: {map.TrueBossEndsRun}.");
    }

    private static bool Contains(SerializedProperty list, string value)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).stringValue == value) return true;
        }
        return false;
    }
}
