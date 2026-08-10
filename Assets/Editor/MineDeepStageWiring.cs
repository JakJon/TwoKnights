using System;
using UnityEditor;
using UnityEngine;

// A one-shot wiring pass for the Mine's second stage: the deep cave the knights
// walk into once the Millstone is behind them.
//
// The stage itself is ordinary MapDefinition data and would happily have been
// authored as YAML, except for the two sprites it points at. Both are LAYERS of
// BackGround_Cave_2.aseprite, and the Aseprite importer mints a fresh GUID for
// every layer sprite it generates (GUID.Generate, per import) — so there is no
// id to write into The Mine.asset from outside the editor. The references have
// to be made here, against sprites the AssetDatabase has actually imported.
//
// The two layers are told apart by the sorting order the importer stamps on the
// generated model prefab (layer index, bottom-up), NOT by name: the bottom layer
// is the cave the fight happens in, the top layer is the rock lip the knights
// stand behind. Renaming the layers in Aseprite must not silently swap them.
//
// It is SELF-DISABLING: it checks the map first and returns silently once the
// stage is present with both sprites resolving. Delete it once the deep cave is
// settled.
public static class MineDeepStageWiring
{
    private const string MapPath = "Assets/Scripts/Maps/The Mine.asset";
    private const string BackdropPath = "Assets/Graphics/BackGround_Cave_2.aseprite";

    private const string StageLabel = "Deep Mine";
    private const int StageFromWave = 11;
    private const string StageVentureLine = "The knights wander further into the cave.";

    [InitializeOnLoadMethod]
    private static void Schedule()
    {
        // delayCall rather than straight away: on the refresh that first imports
        // the aseprite with its new layer settings the AssetDatabase is still
        // mid-import, and the sprites we need would not be there to find yet.
        EditorApplication.delayCall += () => Run(false);
    }

    [MenuItem("Tools/Two Knights/Wire Deep Mine Stage")]
    private static void RunFromMenu()
    {
        Run(true);
    }

    private static void Run(bool verbose)
    {
        var map = AssetDatabase.LoadAssetAtPath<MapDefinition>(MapPath);
        if (map == null)
        {
            if (verbose) Debug.LogError("[MineDeepStageWiring] Could not load " + MapPath);
            return;
        }

        if (IsWired(map))
        {
            if (verbose)
            {
                Debug.Log("[MineDeepStageWiring] The Mine already enters the deep cave at wave " +
                          StageFromWave + " with both layers resolving. Nothing to do; this file " +
                          "can be deleted.");
            }
            return;
        }

        if (!TryReadLayers(out Sprite backdrop, out Sprite foreground, out string problem))
        {
            Debug.LogError("[MineDeepStageWiring] " + problem);
            return;
        }

        WriteStage(map, backdrop, foreground);

        if (IsWired(map))
        {
            Debug.Log("[MineDeepStageWiring] The Mine enters '" + StageLabel + "' at wave " +
                      StageFromWave + ": backdrop '" + backdrop.name + "' behind the arena, '" +
                      foreground.name + "' in front of every sprite in it.");
        }
        else
        {
            Debug.LogError("[MineDeepStageWiring] Wrote the stage but it still does not read back " +
                           "whole. Reimport " + MapPath + " and run " +
                           "Tools > Two Knights > Wire Deep Mine Stage again.");
        }
    }

    // Wired means the stage exists at its wave number AND both sprites resolve.
    // A stage whose sprites read as null looks perfectly healthy in YAML and puts
    // an unchanged forest-green backdrop on the deep cave.
    private static bool IsWired(MapDefinition map)
    {
        var stages = map.Stages;
        if (stages == null) return false;

        for (int i = 0; i < stages.Count; i++)
        {
            MapDefinition.MapStage stage = stages[i];
            if (stage == null || stage.fromWaveNumber != StageFromWave) continue;
            return stage.backdrop != null && stage.foreground != null;
        }
        return false;
    }

    // The generated model prefab carries one child SpriteRenderer per imported
    // layer, each stamped with that layer's index as its sorting order. Reading
    // the pair off there gets the back/front question right by construction.
    private static bool TryReadLayers(out Sprite backdrop, out Sprite foreground, out string problem)
    {
        backdrop = null;
        foreground = null;

        AssetDatabase.ImportAsset(BackdropPath, ImportAssetOptions.ForceUpdate);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BackdropPath);
        if (prefab == null)
        {
            problem = BackdropPath + " did not import as a model prefab. Select it and turn " +
                      "Generate Model Prefab on.";
            return false;
        }

        SpriteRenderer[] renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers.Length < 2)
        {
            problem = BackdropPath + " imported as " + renderers.Length + " layer(s), not 2. Select " +
                      "it and set Layer Import Mode to 'Individual Layers' with 'Import Hidden " +
                      "Layers' on — Layer 1 is hidden in the .aseprite and is skipped without it.";
            return false;
        }

        Array.Sort(renderers, (a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
        backdrop = renderers[0].sprite;
        foreground = renderers[renderers.Length - 1].sprite;

        if (backdrop == null || foreground == null)
        {
            problem = BackdropPath + " generated layer objects with no sprite on them. Reimport it.";
            return false;
        }

        problem = null;
        return true;
    }

    // Through SerializedObject because the stage list is private and this is a
    // wiring pass, not an authoring API anyone should be reaching for. An existing
    // entry at the same wave number is repaired in place rather than duplicated —
    // two stages starting on one wave is a coin toss over which backdrop wins.
    private static void WriteStage(MapDefinition map, Sprite backdrop, Sprite foreground)
    {
        var so = new SerializedObject(map);
        SerializedProperty stages = so.FindProperty("stages");
        if (stages == null || !stages.isArray) return;

        SerializedProperty entry = null;
        for (int i = 0; i < stages.arraySize; i++)
        {
            SerializedProperty candidate = stages.GetArrayElementAtIndex(i);
            if (candidate.FindPropertyRelative("fromWaveNumber").intValue == StageFromWave)
            {
                entry = candidate;
                break;
            }
        }

        if (entry == null)
        {
            stages.InsertArrayElementAtIndex(stages.arraySize);
            entry = stages.GetArrayElementAtIndex(stages.arraySize - 1);
        }

        entry.FindPropertyRelative("label").stringValue = StageLabel;
        entry.FindPropertyRelative("fromWaveNumber").intValue = StageFromWave;
        entry.FindPropertyRelative("backdrop").objectReferenceValue = backdrop;
        entry.FindPropertyRelative("foreground").objectReferenceValue = foreground;
        entry.FindPropertyRelative("ventureLine").stringValue = StageVentureLine;

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(map);
        AssetDatabase.SaveAssetIfDirty(map);
    }
}
