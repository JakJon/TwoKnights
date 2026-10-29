using UnityEditor;
using UnityEngine;

// One-shot: builds the Mine's lighting profiles and points its stages at
// them. Unity runs this the next time it compiles or regains focus; the version
// guard means editing the numbers below and letting Unity recompile is enough to
// re-tune them in place. Tools ▸ Two Knights ▸ Wire Mine Lighting forces a pass.
//
// Kept apart from CanopyLightingWiring, which owns the Camp Fields, so the two
// maps can be re-tuned without touching each other: bumping the version here
// rewrites the Mine's profiles and nothing else. They share the shader and the
// profile TYPE, and that is the only overlap.
//
// The Mine's effect is a vignette rather than a canopy — the same soft, ragged
// darkening around the frame, with every foliage and sun term at zero, plus the
// slow breath. The cave backdrops are dead flat (a single purple with a lighter
// patch in the middle and no falloff at all), so the frame's edges are doing no
// work until something puts them in shadow.
//
// Waves 1-20 fight under one profile; the deep cave's backdrop arrives at wave
// eleven but the lighting does not change with it. From wave twenty-one the same
// cave gets darker: the Mine profile with the shade turned up from 0.50 to 0.65
// and nothing else changed (owner, 2026-10-19).
//
// Lighting_DeepMine is no longer under the Mine at all. It is still written here
// because the Camp Fields' Moonlit Wood stage fights under it, so it must not be
// re-tuned for the Mine's sake.
public static class CaveLightingWiring
{
    private const string MapPath = "Assets/Scripts/Maps/The Mine.asset";
    private const string ShaderPath = "Assets/Shaders/CanopyLight.shader";
    private const string LightingFolder = "Assets/Settings/Lighting";
    private const string MinePath = LightingFolder + "/Lighting_Mine.asset";
    private const string DeepMinePath = LightingFolder + "/Lighting_DeepMine.asset";
    private const string DeepMineDarkerPath = LightingFolder + "/Lighting_DeepMineDarker.asset";

    private const string VersionKey = "TwoKnights.CaveLightingWiring.Version";
    private const int Version = 3;

    private const int MineFromWave = 1;
    private const int DeepMineFromWave = 11;
    private const int DeepMineDarkFromWave = 21;

    // Only what the two depths disagree about. Everything a cave has in common —
    // no leaves, no sun, a nearly-clean elliptical edge — is written once in
    // WriteProfile, because it is a fact about being underground rather than a
    // choice about this stretch of tunnel.
    private struct Settings
    {
        public float actorStrength;
        public Color shadeColor;
        public float shadeStrength;
        public Vector2 gladeRadius;
        public float gladeSoftness;
        public float edgeNoiseScale;
        public float edgeRaggedness;
        public float breathAmount;
        public float breathPeriod;
        public float windSpeed;
    }

    // Waves 1-20. Tuned by the owner on 2026-10-16 (first too light, then 0.75 was
    // too dark, settled on a third less): the corners go from about RGB 39,29,66
    // to 27,19,48 and the clear middle is pulled in so the shade reaches where
    // enemies come on.
    private static Settings Mine => new Settings
    {
        actorStrength = 0.25f,
        shadeColor = new Color(0.02f, 0.015f, 0.045f, 1f),
        shadeStrength = 0.50f,
        gladeRadius = new Vector2(6.4f, 3.6f),
        gladeSoftness = 0.55f,
        edgeNoiseScale = 0.16f,
        edgeRaggedness = 0.10f,
        breathAmount = 0.18f,
        breathPeriod = 11f,
        windSpeed = 0.02f
    };

    // Waves 21+. The waves 1-20 profile with only the shade turned up, so the
    // step reads as the same cave getting darker. Corners land around RGB
    // 22,16,40 — still plainly purple, which matters because the Mine's enemies
    // are purple too.
    private static Settings DeepMineDarker
    {
        get
        {
            Settings s = Mine;
            s.shadeStrength = 0.65f;
            return s;
        }
    }

    // The Mine's old waves 11+ profile, kept as it was because the Camp Fields'
    // Moonlit Wood fights under it (see the header). A smaller clear middle than
    // the Mine's old waves 1-10, a colder shade colour and a bigger breath.
    private static Settings DeepMine => new Settings
    {
        actorStrength = 0.32f,
        shadeColor = new Color(0.014f, 0.010f, 0.034f, 1f),
        shadeStrength = 0.50f,
        gladeRadius = new Vector2(6.8f, 3.9f),
        gladeSoftness = 0.5f,
        edgeNoiseScale = 0.20f,
        edgeRaggedness = 0.16f,
        breathAmount = 0.22f,
        breathPeriod = 10f,
        windSpeed = 0.025f
    };

    [InitializeOnLoadMethod]
    private static void QueueIfStale()
    {
        EditorApplication.delayCall += () =>
        {
            bool built = AssetDatabase.LoadAssetAtPath<CanopyProfile>(DeepMineDarkerPath) != null;
            if (built && EditorPrefs.GetInt(VersionKey, 0) >= Version) return;
            Wire();
        };
    }

    [MenuItem("Tools/Two Knights/Wire Mine Lighting")]
    public static void Wire()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null) shader = Shader.Find("Custom/CanopyLight");
        if (shader == null)
        {
            Debug.LogError($"[CaveLightingWiring] No shader at {ShaderPath}. Reimport it and run " +
                           "Tools > Two Knights > Wire Mine Lighting.");
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            AssetDatabase.CreateFolder("Assets", "Settings");
        if (!AssetDatabase.IsValidFolder(LightingFolder))
            AssetDatabase.CreateFolder("Assets/Settings", "Lighting");

        CanopyProfile mine = WriteProfile(MinePath, shader, Mine);
        CanopyProfile deepMine = WriteProfile(DeepMinePath, shader, DeepMine);
        CanopyProfile deepMineDarker = WriteProfile(DeepMineDarkerPath, shader, DeepMineDarker);
        if (mine == null || deepMine == null || deepMineDarker == null) return;

        var map = AssetDatabase.LoadAssetAtPath<MapDefinition>(MapPath);
        if (map == null)
        {
            Debug.LogError($"[CaveLightingWiring] No map at {MapPath}; the profiles exist but nothing " +
                           "puts them over the arena.");
            return;
        }

        int wired = 0;
        wired += PointStageAt(map, MineFromWave, mine) ? 1 : 0;
        wired += PointStageAt(map, DeepMineFromWave, mine) ? 1 : 0;
        wired += PointStageAt(map, DeepMineDarkFromWave, deepMineDarker) ? 1 : 0;

        AssetDatabase.SaveAssets();
        EditorPrefs.SetInt(VersionKey, Version);
        Debug.Log($"[CaveLightingWiring] Done: 3 lighting profiles, {wired} of 3 Mine stages under them.");
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

        so.FindProperty("groundStrength").floatValue = 1f;
        so.FindProperty("actorStrength").floatValue = s.actorStrength;
        so.FindProperty("shadeColor").colorValue = s.shadeColor;
        so.FindProperty("shadeStrength").floatValue = s.shadeStrength;

        // Flat zero: a vignette is symmetric, and the top-heavy bias that gives the
        // forest its sense of a floor would read here as the ceiling being lopsided
        so.FindProperty("topShade").floatValue = 0f;

        so.FindProperty("breathAmount").floatValue = s.breathAmount;
        so.FindProperty("breathPeriod").floatValue = s.breathPeriod;

        so.FindProperty("gladeCenter").vector2Value = Vector2.zero;
        so.FindProperty("gladeRadius").vector2Value = s.gladeRadius;
        so.FindProperty("gladeSoftness").floatValue = s.gladeSoftness;

        // Barely ragged, and on big slow lobes. Rock is not foliage: the edge wants
        // to be almost a clean ellipse, just not quite clean enough to look like a
        // camera effect
        so.FindProperty("edgeNoiseScale").floatValue = s.edgeNoiseScale;
        so.FindProperty("edgeRaggedness").floatValue = s.edgeRaggedness;

        // No leaves and no sun underground. The dapple numbers still have to be
        // sane values rather than zeroes — they divide inside the shader — but at
        // zero depth none of them reach the frame.
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

        // Almost still. The breath is the motion; this only keeps the ragged edge
        // from being a frozen shape.
        so.FindProperty("windAngle").floatValue = -0.2f;
        so.FindProperty("windSpeed").floatValue = s.windSpeed;

        // Much finer than the forest's 40. With no foliage to carry the pixel-art
        // character this is a single smooth gradient, and at 40 steps the breath
        // would swing it barely more than one step — the swell would show up as
        // bands of dither flickering rather than as light moving.
        so.FindProperty("ditherLevels").floatValue = 96f;

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

        Debug.LogWarning($"[CaveLightingWiring] The Mine has no stage starting at wave {fromWaveNumber}, " +
                         "so that stage is left unlit.");
        return false;
    }
}
