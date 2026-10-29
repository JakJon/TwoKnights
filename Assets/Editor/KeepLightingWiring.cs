using UnityEditor;
using UnityEngine;

// One-shot: builds the Forgotten Fortress's lighting profile and points its
// stage at it. Unity runs this the next time it compiles or regains focus; the
// version guard means editing the numbers below and letting Unity recompile is
// enough to re-tune it in place. Tools ▸ Two Knights ▸ Wire Fortress Lighting
// forces a pass.
//
// Kept apart from CanopyLightingWiring and CaveLightingWiring for the same
// reason those two are apart: each map re-tunes without touching the others.
//
// The owner asked for two things already in the game, put together (2026-10-26):
//
//   * the Mine's darkness — the soft, ragged dark edge around the frame and its
//     slow breath. The numbers are the Mine's waves 1-20 profile; only the shade
//     colour is moved from the Mine's purple to the castle's navy, so the dark
//     edge reads as the same floor in shadow rather than a purple tint.
//   * the forest's drifting patches, but as WHITE MIST instead of dark leaf
//     shadows. The leaf shadows are off; the mist term carries it, travelling on
//     the forest's wind.
//
// Previewed in numpy over background_castle.png before wiring: thickest mist
// took the navy floor (25,41,83) to about (55,65,96) at the first strength of
// 0.03, and floor under the dark edge with no mist over it goes down to about
// (17,29,61).
public static class KeepLightingWiring
{
    private const string MapPath = "Assets/Scripts/Maps/Pallid Keep.asset";
    private const string ShaderPath = "Assets/Shaders/CanopyLight.shader";
    private const string LightingFolder = "Assets/Settings/Lighting";
    private const string KeepPath = LightingFolder + "/Lighting_Keep.asset";

    private const string VersionKey = "TwoKnights.KeepLightingWiring.Version";
    private const int Version = 2;

    private const int KeepFromWave = 1;

    [InitializeOnLoadMethod]
    private static void QueueIfStale()
    {
        EditorApplication.delayCall += () =>
        {
            bool built = AssetDatabase.LoadAssetAtPath<CanopyProfile>(KeepPath) != null;
            if (built && EditorPrefs.GetInt(VersionKey, 0) >= Version) return;
            Wire();
        };
    }

    [MenuItem("Tools/Two Knights/Wire Fortress Lighting")]
    public static void Wire()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null) shader = Shader.Find("Custom/CanopyLight");
        if (shader == null)
        {
            Debug.LogError($"[KeepLightingWiring] No shader at {ShaderPath}. Reimport it and run " +
                           "Tools > Two Knights > Wire Fortress Lighting.");
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            AssetDatabase.CreateFolder("Assets", "Settings");
        if (!AssetDatabase.IsValidFolder(LightingFolder))
            AssetDatabase.CreateFolder("Assets/Settings", "Lighting");

        CanopyProfile keep = WriteProfile(KeepPath, shader);
        if (keep == null) return;

        var map = AssetDatabase.LoadAssetAtPath<MapDefinition>(MapPath);
        if (map == null)
        {
            Debug.LogError($"[KeepLightingWiring] No map at {MapPath}; the profile exists but nothing " +
                           "puts it over the arena.");
            return;
        }

        bool wired = PointStageAt(map, KeepFromWave, keep);

        AssetDatabase.SaveAssets();
        EditorPrefs.SetInt(VersionKey, Version);
        Debug.Log($"[KeepLightingWiring] Done: Lighting_Keep built, {(wired ? 1 : 0)} of 1 Fortress stages under it.");
    }

    // Written through SerializedObject because every field on the profile is
    // private. An existing asset is re-tuned in place so the map's reference survives.
    private static CanopyProfile WriteProfile(string path, Shader shader)
    {
        var profile = AssetDatabase.LoadAssetAtPath<CanopyProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<CanopyProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        var so = new SerializedObject(profile);
        so.FindProperty("shader").objectReferenceValue = shader;

        // The Mine's darkness (CaveLightingWiring, waves 1-20), shade colour aside
        so.FindProperty("groundStrength").floatValue = 1f;
        so.FindProperty("actorStrength").floatValue = 0.25f;
        so.FindProperty("shadeColor").colorValue = new Color(0.012f, 0.02f, 0.05f, 1f);
        so.FindProperty("shadeStrength").floatValue = 0.50f;
        so.FindProperty("topShade").floatValue = 0f;
        so.FindProperty("breathAmount").floatValue = 0.18f;
        so.FindProperty("breathPeriod").floatValue = 11f;
        so.FindProperty("gladeCenter").vector2Value = Vector2.zero;
        so.FindProperty("gladeRadius").vector2Value = new Vector2(6.4f, 3.6f);
        so.FindProperty("gladeSoftness").floatValue = 0.55f;
        so.FindProperty("edgeNoiseScale").floatValue = 0.16f;
        so.FindProperty("edgeRaggedness").floatValue = 0.10f;

        // No leaf shadows and no sun. Same sane-not-zero dapple numbers the Mine
        // writes, because they divide inside the shader.
        so.FindProperty("dappleScale").floatValue = 0.9f;
        so.FindProperty("dappleDetail").floatValue = 0.4f;
        so.FindProperty("leafCoverage").floatValue = 0.46f;
        so.FindProperty("leafShadowDepth").floatValue = 0f;
        so.FindProperty("sunColor").colorValue = Color.white;
        so.FindProperty("sunThroughGaps").floatValue = 0f;
        so.FindProperty("shaftAngle").floatValue = -1.05f;
        so.FindProperty("shaftScale").floatValue = 0.35f;
        so.FindProperty("shaftSharpness").floatValue = 0.72f;
        so.FindProperty("shaftStrength").floatValue = 0f;

        // White mist in place of the forest's dark patches. Faint on purpose: the
        // project renders Linear, and a lerp toward white lifts a navy floor fast.
        // Stretched along the wind into banks — left round it read as clouds
        // painted on the floor. Was 0.03; the owner asked for a third less
        // (2026-10-26).
        so.FindProperty("mistColor").colorValue = new Color(0.86f, 0.9f, 0.96f, 1f);
        so.FindProperty("mistStrength").floatValue = 0.02f;
        so.FindProperty("mistScale").floatValue = 0.3f;
        so.FindProperty("mistDetail").floatValue = 0.45f;
        so.FindProperty("mistStretch").floatValue = 0.5f;
        so.FindProperty("mistCoverage").floatValue = 0.6f;
        so.FindProperty("mistInDark").floatValue = 0.5f;

        // The forest's wind, so the mist drifts the way the forest's patches do
        so.FindProperty("windAngle").floatValue = -0.2f;
        so.FindProperty("windSpeed").floatValue = 0.12f;

        // The Mine's fine dither: the breath and the faint mist both live in a few
        // steps at the forest's 40
        so.FindProperty("ditherLevels").floatValue = 96f;

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(profile);
        return profile;
    }

    // Matched on the wave the stage starts at. A stage that isn't there is
    // reported rather than created.
    private static bool PointStageAt(MapDefinition map, int fromWaveNumber, CanopyProfile profile)
    {
        var so = new SerializedObject(map);
        SerializedProperty stages = so.FindProperty("stages");
        if (stages == null || !stages.isArray) return false;

        for (int i = 0; i < stages.arraySize; i++)
        {
            SerializedProperty stage = stages.GetArrayElementAtIndex(i);
            if (stage.FindPropertyRelative("fromWaveNumber").intValue != fromWaveNumber) continue;

            stage.FindPropertyRelative("canopy").objectReferenceValue = profile;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssetIfDirty(map);
            return true;
        }

        Debug.LogWarning($"[KeepLightingWiring] The Forgotten Fortress has no stage starting at wave " +
                         $"{fromWaveNumber}, so it is left unlit.");
        return false;
    }
}
