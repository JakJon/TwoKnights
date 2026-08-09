using UnityEditor;
using UnityEngine;

// TEMPORARY DIAGNOSTIC — delete once the Near and Far assets are showing up.
//
// A map's wave list is a list of asset references. If the map asset was imported
// at a moment when a referenced wave asset did not yet exist in the database, that
// entry loads as null, and WaveManager.BeginRunPoolOnly quietly strips it
// (RemoveAll(w => w == null)). The symptom is a wave that is present in the .asset
// on disk, compiles fine, and still never appears in the pool.
//
// This forces the map to be reimported now that everything it points at exists,
// then reports what it actually resolves to.
public static class MinePoolProbe
{
    private const string MapPath = "Assets/Scripts/Maps/The Mine.asset";

    [InitializeOnLoadMethod]
    private static void RunOnLoad()
    {
        EditorApplication.delayCall += Probe;
    }

    [MenuItem("Tools/Two Knights/Probe The Mine wave pool")]
    public static void Probe()
    {
        AssetDatabase.ImportAsset(MapPath, ImportAssetOptions.ForceUpdate);

        var map = AssetDatabase.LoadAssetAtPath<MapDefinition>(MapPath);
        if (map == null)
        {
            Debug.LogError($"[MinePoolProbe] Could not load {MapPath}");
            return;
        }

        int nulls = 0;
        var names = new System.Text.StringBuilder();
        for (int i = 0; i < map.Waves.Count; i++)
        {
            BaseWave wave = map.Waves[i];
            if (wave == null)
            {
                nulls++;
                names.Append("\n  [").Append(i).Append("] <MISSING>");
                continue;
            }
            names.Append("\n  [").Append(i).Append("] ").Append(wave.name)
                 .Append("  (").Append(wave.WaveName).Append(")")
                 .Append(wave.CanPlay(0) ? "  playable@wave1" : "  locked@wave1");
        }

        Debug.Log($"[MinePoolProbe] The Mine lists {map.Waves.Count} waves, {nulls} of them unresolved:{names}");
    }
}
