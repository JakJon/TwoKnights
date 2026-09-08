using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;

// Fills in the two references the upgrade menu cannot carry as hand-authored YAML:
// the card-entrance sound clips on the scene's AudioManager, and the knight portrait
// sprites on UpgradeMenu. The clips are wired but no longer played — the upgrade menu
// is silent until a pick is confirmed — and are kept assigned so the knock can be put
// back by uncommenting one call.
//
// It exists because both live on scene objects. A scene Unity already has open is
// served from memory, so a guid typed into the .unity file by hand is invisible to
// the editor and is overwritten the next time the scene is saved — the same trap
// OverseerWiring works around for The Mine's MapDefinition. Going through
// AssetDatabase against the live objects is the only wiring that sticks.
//
// It only ever writes fields that are currently empty, so it costs nothing once the
// scene has been saved with the references in place, and it never overwrites a
// deliberate choice made in the inspector.
//
// It sweeps EVERY scene in the build settings rather than just the open one, and saves
// them. That matters because AudioManager is a DontDestroyOnLoad singleton that destroys
// duplicates: Camp is build index 0, so the AudioManager that survives the whole session
// is Camp's, and Main's is destroyed the moment Main loads. Wiring only the scene that
// happens to be open leaves the clips silent for the entire run.
//
// Tools ▸ Two Knights ▸ Wire Upgrade Menu forces a pass.
public static class UpgradeMenuWiring
{
    private const string ClickPath = "Assets/Sounds/card_click.wav";
    private const string SettlePath = "Assets/Sounds/card_settle.wav";

    // Left knight wears the red plume, right knight the blue — matching the sprites
    // on Player_Knight_Left / Player_Knight_Right.
    private const string LeftPortraitPath = "Assets/Graphics/PlayerMan.aseprite";
    private const string RightPortraitPath = "Assets/Graphics/PlayerMan_Blue.aseprite";

    // Bumped whenever this file changes what it writes, so an already-wired project is
    // swept again instead of being skipped by the "nothing is null" check.
    private const string VersionKey = "TwoKnights.UpgradeMenuWiring.Version";
    private const int Version = 2;

    [InitializeOnLoadMethod]
    private static void Hook()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetInt(VersionKey, 0) >= Version) return;
            if (SweepAllScenes(false)) EditorPrefs.SetInt(VersionKey, Version);
        };
        EditorSceneManager.sceneOpened += (_, __) => Wire(false);
    }

    [MenuItem("Tools/Two Knights/Wire Upgrade Menu")]
    public static void WireFromMenu() => SweepAllScenes(true);

    // Opens each build scene in turn, wires it, saves it, then puts the editor back on
    // the scene it started from. Returns false if the user declined to park their
    // unsaved work, in which case nothing was touched and the sweep runs again next time.
    private static bool SweepAllScenes(bool verbose)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[UpgradeMenuWiring] Skipped — unsaved scene changes were kept.");
            return false;
        }

        string returnTo = SceneManager.GetActiveScene().path;
        int wired = 0;

        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (string.IsNullOrEmpty(entry.path)) continue;

            Scene scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
            if (!scene.IsValid()) continue;

            if (WireAudio(verbose) | WirePortraits(verbose))
            {
                EditorSceneManager.SaveScene(scene);
                wired++;
                Debug.Log($"[UpgradeMenuWiring] Wired and saved '{scene.name}'.");
            }
        }

        if (!string.IsNullOrEmpty(returnTo)) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);

        if (wired == 0 && verbose)
            Debug.Log("[UpgradeMenuWiring] Nothing to do — every build scene is already wired.");
        return true;
    }

    private static void Wire(bool verbose)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        // Both objects can be in different scenes when more than one is loaded, so each
        // reports the scene it actually changed rather than assuming the active one.
        bool changed = false;
        changed |= WireAudio(verbose);
        changed |= WirePortraits(verbose);

        if (!changed && verbose)
            Debug.Log("[UpgradeMenuWiring] Nothing to do — everything already wired.");
    }

    // Marks the scene that actually owns the object, so a pass over a multi-scene setup
    // cannot silently dirty the wrong file. The sweep saves; the sceneOpened pass leaves
    // the scene dirty for the user to look at.
    private static void Announce(Component owner)
    {
        Scene scene = owner.gameObject.scene;
        if (!scene.IsValid()) return;
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static bool WireAudio(bool verbose)
    {
        // Include inactive: the menu and the audio rig are both routinely parked
        // disabled in a scene, and the default search would skip them entirely.
        var manager = Object.FindFirstObjectByType<AudioManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            if (verbose) Debug.LogWarning("[UpgradeMenuWiring] No AudioManager in the open scene.");
            return false;
        }

        bool changed = false;
        changed |= Assign(manager.cardClick, ClickPath, "cardClick");
        changed |= Assign(manager.cardSettle, SettlePath, "cardSettle");

        if (changed)
        {
            EditorUtility.SetDirty(manager);
            Announce(manager);
        }
        return changed;
    }

    // The wavs are authored to peak near 0.9, so the per-entry volume stays at 1.
    private static bool Assign(SoundEffect slot, string path, string label)
    {
        if (slot == null || slot.clip != null) return false;

        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null)
        {
            Debug.LogWarning($"[UpgradeMenuWiring] Missing clip for {label} at {path}.");
            return false;
        }

        slot.clip = clip;
        slot.volume = 1f;
        return true;
    }

    private static bool WirePortraits(bool verbose)
    {
        var menu = Object.FindFirstObjectByType<UpgradeMenu>(FindObjectsInactive.Include);
        if (menu == null)
        {
            if (verbose) Debug.LogWarning("[UpgradeMenuWiring] No UpgradeMenu in the open scene.");
            return false;
        }

        var so = new SerializedObject(menu);
        bool changed = false;
        changed |= AssignSprite(so, "leftKnightPortrait", LeftPortraitPath);
        changed |= AssignSprite(so, "rightKnightPortrait", RightPortraitPath);

        if (changed)
        {
            so.ApplyModifiedProperties();
            Announce(menu);
        }
        return changed;
    }

    // An .aseprite imports as a texture plus one sprite per frame. Frame zero is the
    // knight standing still, which is what the portrait wants.
    private static bool AssignSprite(SerializedObject so, string fieldName, string assetPath)
    {
        var property = so.FindProperty(fieldName);
        if (property == null || property.objectReferenceValue != null) return false;

        Sprite sprite = null;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (asset is Sprite candidate) { sprite = candidate; break; }
        }

        if (sprite == null)
        {
            Debug.LogWarning($"[UpgradeMenuWiring] No sprite found in {assetPath} for {fieldName}.");
            return false;
        }

        property.objectReferenceValue = sprite;
        return true;
    }
}
