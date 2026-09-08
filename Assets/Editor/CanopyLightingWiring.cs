using UnityEditor;
using UnityEngine;

// One-shot: builds the two canopy profiles the Camp Fields fights under and
// points the map's stages at them. Unity runs this the next time it compiles or
// regains focus; the version guard means editing the numbers below and letting
// Unity recompile is enough to re-tune the lighting in place.
// Tools ▸ Two Knights ▸ Wire Forest Canopy forces a pass.
//
// It lives here rather than being hand-authored as YAML for the reason the rest
// of Assets/Editor exists: a MapDefinition is rewritten from Unity's in-memory
// copy on the next refresh, so a profile reference typed into Camp Fields.asset
// by hand is silently dropped. Going through AssetDatabase is the only wiring
// that sticks.
//
// The two profiles are the same forest seen from two depths, and they differ by
// very little on purpose: the Deep Forest has a slightly tighter opening and
// slightly heavier cover. Both are pitched to be almost invisible.
public static class CanopyLightingWiring
{
    private const string MapPath = "Assets/Scripts/Maps/Camp Fields.asset";
    private const string ShaderPath = "Assets/Shaders/CanopyLight.shader";
    private const string LightingFolder = "Assets/Settings/Lighting";
    private const string ForestPath = LightingFolder + "/Canopy_Forest.asset";
    private const string DeepForestPath = LightingFolder + "/Canopy_DeepForest.asset";

    // Bumped whenever the numbers below change, so an existing pair of profiles is
    // re-tuned instead of being left at the previous pass's values. Without it the
    // "already exists" guard means edits here never land.
    private const string VersionKey = "TwoKnights.CanopyLightingWiring.Version";
    private const int Version = 5;

    // The stage each profile belongs to, matched on the wave it starts at rather
    // than on its label — the label is prose and may be reworded
    private const int ForestFromWave = 1;
    private const int DeepForestFromWave = 11;

    // The arena is 20 x 11.25 world units. Radii near half of that put full cover
    // on the frame's edge and leave the knights standing in the open.
    private struct Settings
    {
        public float groundStrength;
        public float actorStrength;
        public Color shadeColor;
        public float shadeStrength;
        public float topShade;
        public Vector2 gladeCenter;
        public Vector2 gladeRadius;
        public float gladeSoftness;
        public float edgeNoiseScale;
        public float edgeRaggedness;
        public float dappleScale;
        public float dappleDetail;
        public float leafCoverage;
        public float leafShadowDepth;
        public Color sunColor;
        public float sunThroughGaps;
        public float shaftAngle;
        public float shaftScale;
        public float shaftSharpness;
        public float shaftStrength;
        public float windAngle;
        public float windSpeed;
        public float ditherLevels;
    }

    // Waves 1-10. Cover overhead and a thinning of it over the knights, pitched
    // so you would have to go looking for it. It is a lean on the backdrop art,
    // not a layer competing with it.
    private static Settings Forest => new Settings
    {
        groundStrength = 1f,
        actorStrength = 0.30f,
        shadeColor = new Color(0.02f, 0.035f, 0.032f, 1f),
        shadeStrength = 0.26f,
        topShade = 0.04f,
        gladeCenter = new Vector2(0f, -0.2f),
        gladeRadius = new Vector2(7.5f, 4.2f),
        gladeSoftness = 0.55f,
        edgeNoiseScale = 0.22f,
        edgeRaggedness = 0.30f,
        dappleScale = 0.9f,
        dappleDetail = 0.40f,
        leafCoverage = 0.46f,
        leafShadowDepth = 0.11f,
        sunColor = new Color(1f, 0.93f, 0.68f, 1f),
        sunThroughGaps = 0.005f,
        shaftAngle = -1.05f,
        shaftScale = 0.35f,
        shaftSharpness = 0.72f,
        shaftStrength = 0.002f,
        windAngle = -0.2f,
        windSpeed = 0.12f,
        ditherLevels = 40f
    };

    // Waves 11+. The same forest a little further in: a slightly tighter opening
    // and slightly heavier cover. Deliberately a small step, not a different
    // place — the backdrop art already says "deeper" on its own.
    private static Settings DeepForest => new Settings
    {
        groundStrength = 1f,
        actorStrength = 0.34f,
        shadeColor = new Color(0.018f, 0.030f, 0.034f, 1f),
        shadeStrength = 0.34f,
        topShade = 0.05f,
        gladeCenter = new Vector2(0f, -0.2f),
        gladeRadius = new Vector2(6.5f, 3.7f),
        gladeSoftness = 0.5f,
        edgeNoiseScale = 0.26f,
        edgeRaggedness = 0.34f,
        dappleScale = 1f,
        dappleDetail = 0.45f,
        leafCoverage = 0.50f,
        leafShadowDepth = 0.13f,
        sunColor = new Color(0.92f, 0.94f, 0.72f, 1f),
        sunThroughGaps = 0.003f,
        shaftAngle = -1.05f,
        shaftScale = 0.35f,
        shaftSharpness = 0.72f,
        shaftStrength = 0.0015f,
        windAngle = -0.2f,
        windSpeed = 0.10f,
        ditherLevels = 40f
    };

    [InitializeOnLoadMethod]
    private static void QueueIfStale()
    {
        EditorApplication.delayCall += () =>
        {
            bool built = AssetDatabase.LoadAssetAtPath<CanopyProfile>(ForestPath) != null;
            if (built && EditorPrefs.GetInt(VersionKey, 0) >= Version) return;
            Wire();
        };
    }

    [MenuItem("Tools/Two Knights/Wire Forest Canopy")]
    public static void Wire()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null) shader = Shader.Find("Custom/CanopyLight");
        if (shader == null)
        {
            Debug.LogError($"[CanopyLightingWiring] No shader at {ShaderPath}. Reimport it and run " +
                           "Tools > Two Knights > Wire Forest Canopy.");
            return;
        }

        EnsureFolder();

        CanopyProfile forest = WriteProfile(ForestPath, shader, Forest);
        CanopyProfile deepForest = WriteProfile(DeepForestPath, shader, DeepForest);
        if (forest == null || deepForest == null) return;

        var map = AssetDatabase.LoadAssetAtPath<MapDefinition>(MapPath);
        if (map == null)
        {
            Debug.LogError($"[CanopyLightingWiring] No map at {MapPath}; the profiles exist but nothing " +
                           "puts them over the arena.");
            return;
        }

        int wired = 0;
        wired += PointStageAt(map, ForestFromWave, forest) ? 1 : 0;
        wired += PointStageAt(map, DeepForestFromWave, deepForest) ? 1 : 0;

        AssetDatabase.SaveAssets();
        EditorPrefs.SetInt(VersionKey, Version);
        Debug.Log($"[CanopyLightingWiring] Done: 2 canopy profiles, {wired} of 2 Camp Fields stages under them.");
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            AssetDatabase.CreateFolder("Assets", "Settings");
        if (!AssetDatabase.IsValidFolder(LightingFolder))
            AssetDatabase.CreateFolder("Assets/Settings", "Lighting");
    }

    // Written through SerializedObject because every field on the profile is
    // private: it is authoring data with an inspector, not an API for code to set.
    // An existing asset is re-tuned in place so the map's references survive.
    private static CanopyProfile WriteProfile(string path, Shader shader, Settings s)
    {
        var profile = AssetDatabase.LoadAssetAtPath<CanopyProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<CanopyProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        var so = new SerializedObject(profile);
        so.FindProperty("shader").objectReferenceValue = shader;
        so.FindProperty("groundStrength").floatValue = s.groundStrength;
        so.FindProperty("actorStrength").floatValue = s.actorStrength;
        so.FindProperty("shadeColor").colorValue = s.shadeColor;
        so.FindProperty("shadeStrength").floatValue = s.shadeStrength;
        so.FindProperty("topShade").floatValue = s.topShade;
        // Written explicitly rather than left to the field default: the forest is
        // dead still, and that is a decision, not an omission. The Mine is the map
        // that breathes.
        so.FindProperty("breathAmount").floatValue = 0f;
        so.FindProperty("breathPeriod").floatValue = 11f;
        so.FindProperty("gladeCenter").vector2Value = s.gladeCenter;
        so.FindProperty("gladeRadius").vector2Value = s.gladeRadius;
        so.FindProperty("gladeSoftness").floatValue = s.gladeSoftness;
        so.FindProperty("edgeNoiseScale").floatValue = s.edgeNoiseScale;
        so.FindProperty("edgeRaggedness").floatValue = s.edgeRaggedness;
        so.FindProperty("dappleScale").floatValue = s.dappleScale;
        so.FindProperty("dappleDetail").floatValue = s.dappleDetail;
        so.FindProperty("leafCoverage").floatValue = s.leafCoverage;
        so.FindProperty("leafShadowDepth").floatValue = s.leafShadowDepth;
        so.FindProperty("sunColor").colorValue = s.sunColor;
        so.FindProperty("sunThroughGaps").floatValue = s.sunThroughGaps;
        so.FindProperty("shaftAngle").floatValue = s.shaftAngle;
        so.FindProperty("shaftScale").floatValue = s.shaftScale;
        so.FindProperty("shaftSharpness").floatValue = s.shaftSharpness;
        so.FindProperty("shaftStrength").floatValue = s.shaftStrength;
        so.FindProperty("windAngle").floatValue = s.windAngle;
        so.FindProperty("windSpeed").floatValue = s.windSpeed;
        so.FindProperty("ditherLevels").floatValue = s.ditherLevels;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(profile);
        return profile;
    }

    // Matched on the wave the stage starts at. A stage that isn't there is
    // reported rather than created: the backdrop is what defines a stage, and a
    // stage invented here would be one with lighting over no scenery.
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

        Debug.LogWarning($"[CanopyLightingWiring] Camp Fields has no stage starting at wave {fromWaveNumber}, " +
                         $"so {profile.name} is built but unused.");
        return false;
    }
}
