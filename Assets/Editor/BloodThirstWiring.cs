using System.Linq;
using UnityEditor;
using UnityEngine;

// One-shot: points each knight's KnightFacing at the "blood thirst" frame of his
// front poses, which Blood Tithe swaps in while its window is open. Unity runs this
// the next time it compiles or regains focus; the version guard means bumping
// Version below re-applies it. Tools > Two Knights > Wire Blood Thirst Faces forces
// a pass.
//
// Only the front poses have the frame - the back poses hide the visor. It is frame 2
// of each two-frame file, picked as "the sprite in that file which is not the pose
// already wired", so it does not depend on what the importer names its frames.
//
// Those four files import with Generate Animation Clips OFF. With it on, PlayerMan's
// import would mint "PlayerMan_Controller" again, and both knights in Main.unity still
// carry an Animator pointing at exactly that id from when the art was animated - it
// would come back to life and pin both knights to the red blood thirst frame.
public static class BloodThirstWiring
{
    private const string LeftKnightPrefab = "Assets/Player_Knight_Left.prefab";
    private const string RightKnightPrefab = "Assets/Player_Knight_Right.prefab";

    // Normal pose field -> its blood thirst twin, on KnightFacing
    private static readonly string[,] Fields =
    {
        { "frontLeft", "bloodFrontLeft" },
        { "frontRight", "bloodFrontRight" },
    };

    private const string VersionKey = "TwoKnights.BloodThirstWiring.Version";
    private const int Version = 1;

    [InitializeOnLoadMethod]
    private static void QueueIfStale()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetInt(VersionKey, 0) >= Version) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Wire();
        };
    }

    [MenuItem("Tools/Two Knights/Wire Blood Thirst Faces")]
    public static void Wire()
    {
        int wired = WirePrefab(LeftKnightPrefab) + WirePrefab(RightKnightPrefab);
        int expected = 2 * Fields.GetLength(0);

        AssetDatabase.SaveAssets();

        if (wired == expected)
        {
            EditorPrefs.SetInt(VersionKey, Version);
            Debug.Log($"[BloodThirstWiring] Done: {wired}/{expected} blood thirst faces wired.");
        }
        else
        {
            // Unstamped, so the next domain reload tries again once the art has imported
            Debug.LogWarning($"[BloodThirstWiring] Only {wired}/{expected} blood thirst faces wired; " +
                             "will retry on the next reload.");
        }
    }

    // Returns how many blood fields hold the right sprite afterwards
    private static int WirePrefab(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            Debug.LogWarning($"[BloodThirstWiring] No prefab at {prefabPath}.");
            return 0;
        }

        try
        {
            var facing = root.GetComponent<KnightFacing>();
            if (facing == null)
            {
                Debug.LogWarning($"[BloodThirstWiring] {prefabPath} has no KnightFacing.");
                return 0;
            }

            var so = new SerializedObject(facing);
            int ok = 0;
            bool changed = false;

            for (int i = 0; i < Fields.GetLength(0); i++)
            {
                var pose = so.FindProperty(Fields[i, 0]);
                var blood = so.FindProperty(Fields[i, 1]);
                var poseSprite = pose != null ? pose.objectReferenceValue as Sprite : null;
                if (poseSprite == null || blood == null)
                {
                    Debug.LogWarning($"[BloodThirstWiring] {prefabPath}: {Fields[i, 0]} is empty or " +
                                     $"{Fields[i, 1]} is missing.");
                    continue;
                }

                string artPath = AssetDatabase.GetAssetPath(poseSprite);
                var sprites = AssetDatabase.LoadAllAssetsAtPath(artPath).OfType<Sprite>().ToList();
                if (sprites.Count != 2)
                {
                    Debug.LogWarning($"[BloodThirstWiring] {artPath} has {sprites.Count} sprites; expected " +
                                     "2 (the pose and its blood thirst frame).");
                    continue;
                }

                Sprite bloodSprite = sprites.First(s => s != poseSprite);
                if (blood.objectReferenceValue != bloodSprite)
                {
                    blood.objectReferenceValue = bloodSprite;
                    changed = true;
                }
                ok++;
            }

            if (changed)
            {
                so.ApplyModifiedProperties();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            return ok;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
