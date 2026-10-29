using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

// Fills in Assets/Resources/CampfireKit.asset — the art the campfire camp is
// built from.
//
// Like CartSortingAssetBuilder, it exists because a sprite inside an .aseprite
// cannot be pointed at by hand: its id is generated on import. It also sets the
// camp art's import settings, since a new .aseprite lands at 100 pixels to the
// unit and everything in this game is drawn at 32.
//
// It runs by itself once, the first time the editor loads with the art present
// and the kit missing or unfinished. Tools > Two Knights > Build Campfire Kit
// runs it again — do that after redrawing or replacing any of the camp art. It
// is safe to repeat: it overwrites what it made and keeps every guid.
public static class CampfireKitBuilder
{
    private const string KitPath = "Assets/Resources/CampfireKit.asset";
    private const string CampArt = "Assets/Graphics/Camp/";
    private const string LogPath = "Library/CampfireKitBuilder.log";

    private const string VentureArt = CampArt + "camp_venture.aseprite";
    private const string JournalArt = CampArt + "camp_journal.aseprite";
    private const string ChestArt = CampArt + "camp_chest.aseprite";
    private const string ShopArt = CampArt + "camp_shop.aseprite";
    private const string SettingsArt = CampArt + "camp_settings.aseprite";
    private const string FireArt = CampArt + "camp_fire.aseprite";

    // Art that is already in the game
    private const string CartographerArt = "Assets/Graphics/npc_cartographer.aseprite";
    private const string LeftKnightArt = "Assets/Graphics/PlayerMan.aseprite";                    // red, faces right
    private const string RightKnightArt = "Assets/Graphics/PlayerMan_Front_Left_Blue.aseprite";   // blue, faces left

    private const string WheelShader = "Assets/Shaders/CampWheel.shader";
    private const string FirelightShader = "Assets/Shaders/CampfireLight.shader";

    // The cartographer's file is his walk (frames 0-3) and then the map: "open
    // map" is frames 4-5 and "close map" is 6-7. The camp loops those four.
    private const int MapFirstFrame = 4;
    private const int MapFrameCount = 4;

    private static readonly string[] NewArt = { VentureArt, JournalArt, ChestArt, ShopArt, SettingsArt, FireArt };

    [InitializeOnLoadMethod]
    private static void AutoBuildOnce()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var kit = AssetDatabase.LoadAssetAtPath<CampfireKit>(KitPath);
            if (kit != null && kit.IsComplete && kit.CartographerFrames != null && kit.CartographerFrames.Length == MapFrameCount)
                return;

            // Nothing to build from yet — the art has not been imported
            if (AssetDatabase.LoadMainAssetAtPath(FireArt) == null) return;

            Build();
        };
    }

    [MenuItem("Tools/Two Knights/Build Campfire Kit")]
    public static void Build()
    {
        var report = new List<string>();

        foreach (string path in NewArt) FixImport(path, report);

        var kit = AssetDatabase.LoadAssetAtPath<CampfireKit>(KitPath);
        if (kit == null)
        {
            kit = ScriptableObject.CreateInstance<CampfireKit>();
            AssetDatabase.CreateAsset(kit, KitPath);
            report.Add("created " + KitPath);
        }

        var so = new SerializedObject(kit);
        so.FindProperty("ventureForth").objectReferenceValue = One(VentureArt, report);
        so.FindProperty("journal").objectReferenceValue = One(JournalArt, report);
        so.FindProperty("equipment").objectReferenceValue = One(ChestArt, report);
        so.FindProperty("shopGoods").objectReferenceValue = One(ShopArt, report);
        so.FindProperty("settings").objectReferenceValue = One(SettingsArt, report);
        so.FindProperty("leftKnight").objectReferenceValue = One(LeftKnightArt, report);
        so.FindProperty("rightKnight").objectReferenceValue = One(RightKnightArt, report);

        SetFrames(so.FindProperty("fireFrames"), Frames(FireArt, report), 0, int.MaxValue);
        SetFrames(so.FindProperty("cartographerFrames"), Frames(CartographerArt, report), MapFirstFrame, MapFrameCount);

        so.FindProperty("wheelShader").objectReferenceValue = LoadShader(WheelShader, report);
        so.FindProperty("firelightShader").objectReferenceValue = LoadShader(FirelightShader, report);

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(kit);
        AssetDatabase.SaveAssets();

        report.Add(kit.IsComplete
            ? "DONE: the kit is complete. The camp will draw as the campfire."
            : "UNFINISHED: something above is missing, so the camp will fall back to the list menu.");

        foreach (string line in report) Debug.Log("[CampfireKitBuilder] " + line);
        try { File.WriteAllLines(LogPath, report.ToArray()); }
        catch (IOException) { /* the log is a convenience, never a reason to fail */ }
    }

    // New camp art only. The knights and the cartographer already have their
    // settings and are left exactly as they are.
    private static void FixImport(string path, List<string> report)
    {
        var importer = AssetImporter.GetAtPath(path) as AsepriteImporter;
        if (importer == null)
        {
            report.Add("MISSING ART " + path);
            return;
        }

        bool right = Mathf.Approximately(importer.spritePixelsPerUnit, 32f)
                     && importer.filterMode == FilterMode.Point
                     && !importer.generateModelPrefab
                     && !importer.generateAnimationClips;
        if (right) return;

        importer.spritePixelsPerUnit = 32f;
        importer.filterMode = FilterMode.Point;
        // These are pictures stood on the ground by code. A model prefab and
        // animation clips beside each one would be clutter nothing uses.
        importer.generateModelPrefab = false;
        importer.generateAnimationClips = false;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        report.Add("import settings set on " + path);
    }

    private static Sprite One(string path, List<string> report)
    {
        Sprite[] frames = Frames(path, report);
        return frames.Length > 0 ? frames[0] : null;
    }

    // Every sprite the importer made from one .aseprite, in frame order. A file
    // with several frames names them Frame_0, Frame_1...; a single frame is named
    // after the file.
    private static Sprite[] Frames(string path, List<string> report)
    {
        var frames = new List<Sprite>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            var sprite = o as Sprite;
            if (sprite != null) frames.Add(sprite);
        }
        frames.Sort((a, b) => FrameNumber(a.name).CompareTo(FrameNumber(b.name)));

        if (frames.Count == 0) report.Add("NO SPRITES in " + path);
        else if (!Mathf.Approximately(frames[0].pixelsPerUnit, 32f))
            report.Add("WRONG PPU in " + path + ": " + frames[0].pixelsPerUnit + " (want 32)");
        else report.Add(path + ": " + frames.Count + " sprite(s), first is " + frames[0].rect.width + "x" + frames[0].rect.height);
        return frames.ToArray();
    }

    // By number, not by name: as text, Frame_10 sorts ahead of Frame_2
    private static int FrameNumber(string spriteName)
    {
        int underscore = spriteName.LastIndexOf('_');
        int number;
        return underscore >= 0 && int.TryParse(spriteName.Substring(underscore + 1), out number) ? number : 0;
    }

    private static void SetFrames(SerializedProperty list, Sprite[] frames, int first, int count)
    {
        int available = Mathf.Max(0, frames.Length - first);
        int take = Mathf.Min(count, available);
        list.arraySize = take;
        for (int i = 0; i < take; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = frames[first + i];
    }

    private static Shader LoadShader(string path, List<string> report)
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
        if (shader == null) report.Add("MISSING SHADER " + path);
        return shader;
    }
}
