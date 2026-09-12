using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// One-shot repair for upgrades authored while the editor was closed (Sleeping
// Dart I/II, Long Sword I/II, Acid Dagger). Their .asset files and the guid lines
// in UpgradeManager.asset were both written by hand, which is fine on disk but
// is silently undone if Unity already had UpgradeManager loaded and rewrites it
// from memory. This checks the registry on the next refresh and puts back only
// what is missing, so running it twice does nothing the second time.
public static class NewUpgradeRegistration
{
    private static readonly string[] AssetPaths =
    {
        "Assets/Upgrades/Shadow Ups/Sleeping Dart 1.asset",
        "Assets/Upgrades/Shadow Ups/Sleeping Dart 2.asset",
        "Assets/Upgrades/Sword Ups/Long Sword 1.asset",
        "Assets/Upgrades/Sword Ups/Long Sword 2.asset",
        AcidDaggerAssetPath,
    };

    [InitializeOnLoadMethod]
    private static void Schedule()
    {
        EditorApplication.delayCall += () =>
        {
            Register(verbose: false);
            WireSprites(verbose: false);
        };
    }

    [MenuItem("Tools/Two Knights/Register New Upgrades")]
    private static void RegisterFromMenu()
    {
        Register(verbose: true);
        WireSprites(verbose: true);
    }

    private static void Register(bool verbose)
    {
        var manager = Resources.Load<UpgradeManager>("UpgradeManager");
        if (manager == null)
        {
            if (verbose) Debug.LogError("[NewUpgradeRegistration] Resources/UpgradeManager not found.");
            return;
        }

        var serialized = new SerializedObject(manager);
        SerializedProperty list = serialized.FindProperty("allUpgrades");
        if (list == null)
        {
            if (verbose) Debug.LogError("[NewUpgradeRegistration] UpgradeManager has no allUpgrades field.");
            return;
        }

        var present = new HashSet<Object>();
        for (int i = 0; i < list.arraySize; i++)
        {
            present.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);
        }

        var added = new List<string>();
        foreach (string path in AssetPaths)
        {
            var upgrade = AssetDatabase.LoadAssetAtPath<BaseUpgrade>(path);
            if (upgrade == null)
            {
                Debug.LogWarning($"[NewUpgradeRegistration] Missing upgrade asset at {path}.");
                continue;
            }
            if (present.Contains(upgrade)) continue;

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = upgrade;
            added.Add(upgrade.name);
        }

        if (added.Count == 0)
        {
            if (verbose) Debug.Log("[NewUpgradeRegistration] All already registered - nothing to do.");
            return;
        }

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(manager);
        AssetDatabase.SaveAssets();
        Debug.Log($"[NewUpgradeRegistration] Registered {added.Count}: {string.Join(", ", added)}");
    }

    // ---- sprite wiring ----

    private const string DartSpritePath = "Assets/Graphics/sleeping_dart.aseprite";
    private const string ZSpritePath = "Assets/Graphics/sleep_z.aseprite";
    private const string AcidDaggerAssetPath = "Assets/Upgrades/Poison Ups/Acid Dagger.asset";
    private const string AcidDaggerSpritePath = "Assets/Graphics/acid_dagger.aseprite";

    // An .aseprite's sprite sub-asset id is minted per import, so the reference
    // cannot be written into the .asset YAML from outside the editor - it has to
    // come from AssetDatabase. Both Sleeping Dart ranks carry their own copy so
    // neither depends on the other having been taken.
    private static void WireSprites(bool verbose)
    {
        WireAcidDagger();

        Sprite dart = AssetDatabase.LoadAssetAtPath<Sprite>(DartSpritePath);
        Sprite z = AssetDatabase.LoadAssetAtPath<Sprite>(ZSpritePath);

        if (dart == null || z == null)
        {
            Debug.LogWarning($"[NewUpgradeRegistration] Sprite missing - dart: {dart != null}, z: {z != null}. " +
                             "If the .aseprite files are present, the importer may not have run yet.");
            return;
        }

        // The importer tight-crops to non-transparent bounds; the alpha-guarded
        // corners are supposed to stop that. Assert rather than trust it.
        AssertFullRect(dart, DartSpritePath, 32, 32);
        AssertFullRect(z, ZSpritePath, 16, 16);

        foreach (string path in new[]
                 {
                     "Assets/Upgrades/Shadow Ups/Sleeping Dart 1.asset",
                     "Assets/Upgrades/Shadow Ups/Sleeping Dart 2.asset",
                 })
        {
            var upgrade = AssetDatabase.LoadAssetAtPath<BaseUpgrade>(path);
            if (upgrade == null) continue;

            var so = new SerializedObject(upgrade);
            SerializedProperty dartProp = so.FindProperty("dartSprite");
            SerializedProperty zProp = so.FindProperty("sleepZSprite");
            if (dartProp == null || zProp == null) continue;

            bool changed = false;
            if (dartProp.objectReferenceValue != dart) { dartProp.objectReferenceValue = dart; changed = true; }
            if (zProp.objectReferenceValue != z) { zProp.objectReferenceValue = z; changed = true; }
            if (!changed) continue;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(upgrade);
            Debug.Log($"[NewUpgradeRegistration] Wired sprites onto {upgrade.name}.");
        }

        AssetDatabase.SaveAssets();
        if (verbose) Debug.Log("[NewUpgradeRegistration] Sprite wiring pass complete.");
    }

    // Its own pass so a missing dart sprite cannot hold up the dagger, or the
    // other way round. SwordSwing measures the blade's hitbox off this 9x17
    // canvas, so the full-rect assert matters here more than anywhere.
    private static void WireAcidDagger()
    {
        Sprite dagger = AssetDatabase.LoadAssetAtPath<Sprite>(AcidDaggerSpritePath);
        if (dagger == null)
        {
            Debug.LogWarning($"[NewUpgradeRegistration] {AcidDaggerSpritePath} has no sprite yet - " +
                             "the importer may not have run.");
            return;
        }

        AssertFullRect(dagger, AcidDaggerSpritePath, 9, 17);

        // Loaded again in case the assert had to reimport it
        dagger = AssetDatabase.LoadAssetAtPath<Sprite>(AcidDaggerSpritePath);
        if (dagger == null) return;

        var upgrade = AssetDatabase.LoadAssetAtPath<BaseUpgrade>(AcidDaggerAssetPath);
        if (upgrade == null) return;

        var so = new SerializedObject(upgrade);
        SerializedProperty prop = so.FindProperty("daggerSprite");
        if (prop == null || prop.objectReferenceValue == dagger) return;

        prop.objectReferenceValue = dagger;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(upgrade);
        AssetDatabase.SaveAssets();
        Debug.Log($"[NewUpgradeRegistration] Wired {dagger.name} onto {upgrade.name}.");
    }

    private static void AssertFullRect(Sprite sprite, string path, int width, int height)
    {
        if (Mathf.RoundToInt(sprite.rect.width) == width && Mathf.RoundToInt(sprite.rect.height) == height)
            return;

        // The cached rect in the .meta goes stale and survives a plain reimport;
        // clearing it is the only thing that reliably shakes it loose.
        Debug.LogWarning($"[NewUpgradeRegistration] {path} imported at {sprite.rect.width}x{sprite.rect.height}, " +
                         $"expected {width}x{height} - clearing the cached rect and reimporting.");

        var importer = AssetImporter.GetAtPath(path);
        if (importer == null) return;

        var so = new SerializedObject(importer);
        SerializedProperty cached = so.FindProperty("m_AnimatedSpriteImportData");
        if (cached != null && cached.isArray)
        {
            cached.ClearArray();
            so.ApplyModifiedProperties();
        }
        importer.SaveAndReimport();
    }
}
