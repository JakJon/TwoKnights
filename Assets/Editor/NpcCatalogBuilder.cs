using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds and wires Assets/Resources/NpcCatalog.asset.
///
/// This cannot be hand-authored as YAML. The NPC art imports with Layer Import Mode
/// = Individual Layers, and the Aseprite importer mints a fresh GUID per layer
/// sprite on EVERY reimport — the sub-asset file ids are not derivable from outside
/// the editor by any hash. So the references have to be made through the real
/// AssetDatabase, which means in here.
///
/// Runs itself once when Unity next loads, and self-disables afterwards by checking
/// whether every entry already resolves. Force a rebuild from
/// Tools &gt; Two Knights &gt; Rebuild NPC Catalog after changing the art.
/// </summary>
public static class NpcCatalogBuilder
{
    private const string CatalogPath = "Assets/Resources/NpcCatalog.asset";
    private const string GraphicsDir = "Assets/Graphics";

    private class Spec
    {
        public NpcId Id;
        public string Aseprite;
        public NpcCatalog.Arrival Arrival;
        public float Pitch;
        public string Walk, Open, Close;
        public float Scale;
    }

    // Pitch is what actually separates the five voices — they share a blip. The King
    // sits lowest, the cartographer bright and busy, the wizard high and odd.
    private static readonly Spec[] Specs =
    {
        new Spec { Id = NpcId.King, Scale = 1.9f,         Aseprite = "npc_king",         Arrival = NpcCatalog.Arrival.Walker,
                   Pitch = 0.72f, Walk = "" },
        new Spec { Id = NpcId.Cartographer, Scale = 1.6f, Aseprite = "npc_cartographer", Arrival = NpcCatalog.Arrival.Walker,
                   Pitch = 1.25f, Walk = "Walk", Open = "open map", Close = "close map" },
        new Spec { Id = NpcId.Ninja, Scale = 1.6f,        Aseprite = "npc_ninja",        Arrival = NpcCatalog.Arrival.Smoke,
                   Pitch = 0.95f, Walk = "" },
        new Spec { Id = NpcId.Wizard, Scale = 1.6f,       Aseprite = "npc_wizard",       Arrival = NpcCatalog.Arrival.Elemental,
                   Pitch = 1.45f, Walk = "walking" },
        new Spec { Id = NpcId.Paladin, Scale = 1.7f,      Aseprite = "npc_paladin",      Arrival = NpcCatalog.Arrival.Radiant,
                   Pitch = 0.85f, Walk = "" },
    };

    [InitializeOnLoadMethod]
    private static void AutoBuild()
    {
        EditorApplication.delayCall += () =>
        {
            if (IsComplete()) return;
            Build(false);
        };
    }

    [MenuItem("Tools/Two Knights/Rebuild NPC Catalog")]
    private static void RebuildFromMenu()
    {
        Build(true);
    }

    /// <summary>
    /// Rebuilds the catalog whenever one of the NPC .aseprite files is reimported.
    ///
    /// Without this the wiring is only as fresh as the last time somebody
    /// remembered the menu item, and a reimport mints new sub-asset GUIDs — so
    /// changing an import setting (the King's Layer Import Mode, say) silently left
    /// the catalog pointing at sprites that no longer exist.
    /// </summary>
    private class ArtWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
                                                   string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
            {
                if (!path.StartsWith(GraphicsDir + "/npc_", System.StringComparison.Ordinal)) continue;
                if (!path.EndsWith(".aseprite", System.StringComparison.Ordinal)) continue;

                // Deferred: rebuilding writes an asset, and writing one from inside
                // an import callback is how an import loop starts.
                EditorApplication.delayCall += () => Build(false);
                return;
            }
        }
    }

    private static bool IsComplete()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<NpcCatalog>(CatalogPath);
        if (catalog == null) return false;
        foreach (var spec in Specs)
        {
            var entry = catalog.Find(spec.Id);
            // The portrait counts. A catalog wired before the portrait rule changed
            // resolves its prefabs perfectly well and still shows the wrong face.
            if (entry == null || entry.prefab == null || entry.portrait == null) return false;
            if (entry.worldScale <= 0f) return false;
        }
        return true;
    }

    private static void Build(bool verbose)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<NpcCatalog>(CatalogPath);
        if (catalog == null)
        {
            Directory.CreateDirectory("Assets/Resources");
            catalog = ScriptableObject.CreateInstance<NpcCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            Debug.Log("[NpcCatalog] Created " + CatalogPath);
        }

        var entries = new List<NpcCatalog.Entry>();
        int wired = 0;

        foreach (var spec in Specs)
        {
            string path = GraphicsDir + "/" + spec.Aseprite + ".aseprite";
            var entry = new NpcCatalog.Entry
            {
                id = spec.Id,
                arrival = spec.Arrival,
                voicePitch = spec.Pitch,
                worldScale = spec.Scale > 0f ? spec.Scale : 1.6f,
                walkState = spec.Walk ?? "",
                openState = spec.Open ?? "",
                closeState = spec.Close ?? "",
            };

            // The importer's generated model prefab is the MAIN asset of the
            // .aseprite when Generate Model Prefab is on, which it is for all five.
            entry.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            // The portrait is the BIGGEST sprite the importer produced, not the
            // first. Sub-assets are scanned rather than named — the importer names
            // them per layer and per frame in a scheme not worth relying on — and
            // for art that imports as individual layers "first" is whichever layer
            // happened to come out first, which for the King could be his crown on
            // its own. The largest is the body, which is the figure.
            //
            // That does mean the King's portrait is his body layer alone, without
            // the crown or cape drawn over it. Compositing the layers back together
            // would mean generating a texture and storing it on this catalog as a
            // sub-asset; worth doing if the portrait ever looks wrong, not before.
            float best = 0f;
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(sub is Sprite sprite)) continue;
                float area = sprite.rect.width * sprite.rect.height;
                if (area <= best) continue;
                best = area;
                entry.portrait = sprite;
            }

            if (entry.prefab == null)
            {
                Debug.LogError($"[NpcCatalog] No model prefab at {path}. Is the .aseprite imported " +
                               "with Generate Model Prefab on?");
            }
            else
            {
                wired++;
            }

            entries.Add(entry);
        }

        var serialized = new SerializedObject(catalog);
        var list = serialized.FindProperty("entries");
        list.arraySize = entries.Count;
        for (int i = 0; i < entries.Count; i++)
        {
            var element = list.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("id").enumValueIndex = (int)entries[i].id;
            element.FindPropertyRelative("prefab").objectReferenceValue = entries[i].prefab;
            element.FindPropertyRelative("portrait").objectReferenceValue = entries[i].portrait;
            element.FindPropertyRelative("arrival").enumValueIndex = (int)entries[i].arrival;
            element.FindPropertyRelative("voicePitch").floatValue = entries[i].voicePitch;
            element.FindPropertyRelative("worldScale").floatValue = entries[i].worldScale;
            element.FindPropertyRelative("walkState").stringValue = entries[i].walkState;
            element.FindPropertyRelative("openState").stringValue = entries[i].openState;
            element.FindPropertyRelative("closeState").stringValue = entries[i].closeState;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        Debug.Log($"[NpcCatalog] Wired {wired}/{Specs.Length} NPCs.");
        if (verbose) EditorGUIUtility.PingObject(catalog);
    }
}
