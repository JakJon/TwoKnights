using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-shot: dresses the RIGHT knight in the blue palette swap and leaves the left
// one red. Unity runs this the next time it compiles or regains focus; the version
// guard means bumping Version below and letting Unity recompile re-applies it.
// Tools > Two Knights > Wire Blue Right Knight forces a pass.
//
// The two knights share one set of art, so telling them apart meant a second copy of
// each piece. Four pairs are wired here - body, shield, arrow, sword - and each blue
// file is pixel-for-pixel its red original with only the red accents moved: the plume,
// the fletching on both the held and the fired arrow, the shield band, the sword grip.
// Gold is deliberately untouched everywhere it appears, so the right knight reads as
// blue-and-gold against the left one's red-and-gold rather than as a different kit.
//
// The generated shield in ShieldShape.cs paints the same band from its own constants,
// because a reshaped shield is drawn in code rather than blitted from shield.aseprite.
// Change a band colour here and change it there too.
//
// Two things have to agree, and a third is worth watching:
//   1. the prefab's SpriteRenderer, which is what you see in the project browser,
//   2. the scene instance's SpriteRenderer, which overrides the prefab. The Animator
//      lives on the scene instance rather than the prefab, which is why this has to
//      touch the scene at all.
//   3. the scene instance's Animator. When PlayerMan.aseprite had four frames the
//      importer generated an AnimatorController whose clip keyed the sprite, and that
//      controller would have reasserted red on the first frame of play, quietly
//      undoing 1 and 2. The art is one frame now, so the importer generates no
//      controller at all and both knights' Animators point at a fileID that no
//      current import produces - a dead reference that resolves to null and does
//      nothing. The swap below is kept anyway: put frames back in the .aseprite and
//      a controller reappears, at which point the right knight needs the blue one
//      rather than the red. If it reports no controller, that is the expected state
//      for single-frame art, not a failure.
public static class BlueKnightWiring
{
    private const string BlueKnightArt = "Assets/Graphics/PlayerMan_Blue.aseprite";
    private const string BlueShieldArt = "Assets/Graphics/shield_blue.aseprite";
    private const string RedKnightArt = "Assets/Graphics/PlayerMan.aseprite";
    private const string RedShieldArt = "Assets/Graphics/shield.aseprite";

    private const string BlueArrowArt = "Assets/Graphics/arrow_blue.aseprite";
    private const string RedArrowArt = "Assets/Graphics/arrow_red.aseprite";
    private const string BlueSwordArt = "Assets/Graphics/sword_blue.aseprite";
    private const string RedSwordArt = "Assets/Graphics/sword_red.aseprite";

    // The four poses each knight turns between, in KnightFacing.Facing order:
    // back right, back left, front left, front right. The front-right slot is the
    // original PlayerMan art, which is also what the prefab wears at rest.
    private static readonly string[] RedPoseArt =
    {
        "Assets/Graphics/PlayerMan_Back_Red.aseprite",
        "Assets/Graphics/PlayerMan_Back_left_red.aseprite",
        "Assets/Graphics/PlayerMan_Front_Left_Red.aseprite",
        RedKnightArt,
    };

    private static readonly string[] BluePoseArt =
    {
        "Assets/Graphics/PlayerMan_Back_Blue.aseprite",
        "Assets/Graphics/PlayerMan_Back_left_blue.aseprite",
        "Assets/Graphics/PlayerMan_Front_Left_Blue.aseprite",
        BlueKnightArt,
    };

    private static readonly string[] PoseFields =
    {
        "backRight", "backLeft", "frontLeft", "frontRight",
    };

    private const string RightKnightPrefab = "Assets/Player_Knight_Right.prefab";
    private const string LeftKnightPrefab = "Assets/Player_Knight_Left.prefab";
    private const string RightSwordPrefab = "Assets/Player_Sword_Right.prefab";
    private const string LeftSwordPrefab = "Assets/Player_Sword_Left.prefab";

    // The knights shared one arrow prefab, so the blue one has to exist before it can
    // be pointed at. It is generated from the red prefab rather than authored, which
    // keeps the two identical in everything except the sprite.
    private const string RedArrowPrefab = "Assets/Player_Projectile.prefab";
    private const string BlueArrowPrefab = "Assets/Player_Projectile_Blue.prefab";

    private const string MainScene = "Assets/Scenes/Main.unity";

    private const string ShieldTag = "Shield";
    private const string RightKnightTag = "PlayerRight";
    private const string LeftKnightTag = "PlayerLeft";

    // The blade child inside the sword prefabs; its sibling "Slash" is the swing VFX
    // and is deliberately left alone - it reads as motion, not as a knight's colour.
    private const string SwordChild = "Sword";

    private const string VersionKey = "TwoKnights.BlueKnightWiring.Version";
    private const int Version = 4;

    [InitializeOnLoadMethod]
    private static void QueueIfStale()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetInt(VersionKey, 0) >= Version) return;

            // This saves Main.unity, which is not a thing to do underneath a running
            // game. Leave the version unstamped so the next domain reload retries.
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Wire();
        };
    }

    [MenuItem("Tools/Two Knights/Wire Blue Right Knight")]
    public static void Wire()
    {
        Sprite blueKnight = FindSprite(BlueKnightArt);
        Sprite blueShield = FindSprite(BlueShieldArt);
        Sprite redKnight = FindSprite(RedKnightArt);
        Sprite redShield = FindSprite(RedShieldArt);
        RuntimeAnimatorController blueController = FindController(BlueKnightArt);

        if (blueKnight == null || blueShield == null)
        {
            Debug.LogError("[BlueKnightWiring] The blue art is missing or has not imported yet. " +
                           $"Expected sprites in {BlueKnightArt} and {BlueShieldArt}. Reimport them " +
                           "and run Tools > Two Knights > Wire Blue Right Knight.");
            return;
        }

        // The prefabs are the fallback the scene overrides sit on top of. The left
        // one's body sprite is a dangling reference that predates this change; it is
        // repointed at the red art here so "left stays red" is true of the prefab
        // too, not only of the copy standing in the scene.
        int prefabEdits = 0;
        prefabEdits += DressPrefab(RightKnightPrefab, blueKnight, blueShield) ? 1 : 0;
        prefabEdits += DressPrefab(LeftKnightPrefab, redKnight, redShield) ? 1 : 0;

        // Swords already had a prefab each, so they only need the right sprite in each.
        prefabEdits += DressChildSprite(RightSwordPrefab, SwordChild, FindSprite(BlueSwordArt)) ? 1 : 0;
        prefabEdits += DressChildSprite(LeftSwordPrefab, SwordChild, FindSprite(RedSwordArt)) ? 1 : 0;

        // Arrows did not. The red prefab keeps its path and its guid so the left
        // knight's existing reference survives untouched; the blue one is cloned off
        // it, so the two stay identical in everything except the sprite.
        prefabEdits += DressChildSprite(RedArrowPrefab, null, FindSprite(RedArrowArt)) ? 1 : 0;
        GameObject blueArrow = EnsureBlueArrowPrefab(FindSprite(BlueArrowArt));
        prefabEdits += PointShooterAt(RightKnightPrefab, blueArrow) ? 1 : 0;

        // Four poses each, so the body turns with the shield.
        prefabEdits += GivePoses(LeftKnightPrefab, RedPoseArt) ? 1 : 0;
        prefabEdits += GivePoses(RightKnightPrefab, BluePoseArt) ? 1 : 0;

        bool sceneWired = DressScene(blueKnight, blueShield, blueController, blueArrow);

        AssetDatabase.SaveAssets();
        EditorPrefs.SetInt(VersionKey, Version);

        string controllerNote = blueController != null
            ? "animator on blue"
            : "no animator controller in the blue art, which is expected while it is "
              + "single-frame - the SpriteRenderer is what renders";
        Debug.Log($"[BlueKnightWiring] Done: {prefabEdits} prefab edits, " +
                  $"scene {(sceneWired ? "wired" : "not changed")}, {controllerNote}.\n" +
                  Inventory(RedKnightArt) + "\n" + Inventory(BlueKnightArt) + "\n" +
                  SceneAnimatorReport());
    }

    // What the Aseprite importer actually produced. The red art is listed next to
    // the blue so the two can be compared: if neither carries a controller, the
    // scene's Animators are pointing at something that no longer exists and the
    // SpriteRenderer is what renders - which would make the controller swap moot.
    private static string Inventory(string assetPath)
    {
        var objects = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        string listed = objects.Length == 0
            ? "(nothing)"
            : string.Join(", ", objects.Select(o => $"{o.GetType().Name}:{o.name}"));
        return $"  {assetPath} -> {listed}";
    }

    private static string SceneAnimatorReport()
    {
        var scene = EditorSceneManager.GetSceneByPath(MainScene);
        if (!scene.isLoaded) return "  (Main.unity not open; animators not inspected)";

        var lines = scene.GetRootGameObjects()
            .Where(go => go.CompareTag(LeftKnightTag) || go.CompareTag(RightKnightTag))
            .SelectMany(go => go.GetComponentsInChildren<Animator>(true)
                                .Select(a => $"  {go.name}/{a.name} animator -> " +
                                             (a.runtimeAnimatorController == null
                                                 ? "NONE (missing or never set)"
                                                 : a.runtimeAnimatorController.name)));
        return string.Join("\n", lines);
    }

    // The Aseprite importer mints its own sub-assets, so nothing here can be
    // addressed by a hand-written file id - everything is looked up by type.
    private static Sprite FindSprite(string assetPath)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().ToList();
        if (sprites.Count == 0)
        {
            Debug.LogWarning($"[BlueKnightWiring] No sprite inside {assetPath}.");
            return null;
        }

        // A merged single-frame file yields exactly one. If a later edit adds frames,
        // take the first by name so at least the choice stays stable.
        return sprites.OrderBy(s => s.name, System.StringComparer.Ordinal).First();
    }

    private static RuntimeAnimatorController FindController(string assetPath)
    {
        return AssetDatabase.LoadAllAssetsAtPath(assetPath)
                            .OfType<RuntimeAnimatorController>()
                            .FirstOrDefault();
    }

    private static bool DressPrefab(string prefabPath, Sprite body, Sprite shield)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            Debug.LogWarning($"[BlueKnightWiring] No prefab at {prefabPath}.");
            return false;
        }

        try
        {
            bool changed = false;
            changed |= SetSprite(root.GetComponent<SpriteRenderer>(), body);

            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.CompareTag(ShieldTag)) changed |= SetSprite(renderer, shield);
            }

            if (changed) PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            return changed;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Sets one sprite inside a prefab: on the named child, or on the root when
    // childName is null. Used for the swords (child "Sword") and the arrow (root).
    private static bool DressChildSprite(string prefabPath, string childName, Sprite sprite)
    {
        if (sprite == null)
        {
            Debug.LogWarning($"[BlueKnightWiring] No sprite to put in {prefabPath}; skipped.");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            Debug.LogWarning($"[BlueKnightWiring] No prefab at {prefabPath}.");
            return false;
        }

        try
        {
            SpriteRenderer target = childName == null
                ? root.GetComponent<SpriteRenderer>()
                : root.GetComponentsInChildren<SpriteRenderer>(true)
                      .FirstOrDefault(r => r.gameObject.name == childName);

            if (target == null)
            {
                Debug.LogWarning($"[BlueKnightWiring] {prefabPath} has no " +
                                 $"{(childName ?? "root")} SpriteRenderer.");
                return false;
            }

            if (!SetSprite(target, sprite)) return false;

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Clones the red arrow into a blue one the first time, then keeps its sprite in
    // step on later passes. Cloning rather than authoring means the collider, rigidbody
    // and ProjectileMovement settings can only ever differ by someone editing one and
    // not the other, and the log below says so when they do.
    private static GameObject EnsureBlueArrowPrefab(Sprite blueArrowSprite)
    {
        if (blueArrowSprite == null)
        {
            Debug.LogWarning($"[BlueKnightWiring] {BlueArrowArt} has not imported; the right " +
                             "knight will keep firing the red arrow.");
            return AssetDatabase.LoadAssetAtPath<GameObject>(BlueArrowPrefab);
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(BlueArrowPrefab) == null)
        {
            if (!AssetDatabase.CopyAsset(RedArrowPrefab, BlueArrowPrefab))
            {
                Debug.LogError($"[BlueKnightWiring] Could not copy {RedArrowPrefab} to {BlueArrowPrefab}.");
                return null;
            }

            AssetDatabase.ImportAsset(BlueArrowPrefab);
            Debug.Log($"[BlueKnightWiring] Created {BlueArrowPrefab} from {RedArrowPrefab}.");
        }

        DressChildSprite(BlueArrowPrefab, null, blueArrowSprite);
        return AssetDatabase.LoadAssetAtPath<GameObject>(BlueArrowPrefab);
    }

    // Adds KnightFacing if the prefab has not got one yet and fills its four pose
    // slots. Every slot is checked before anything is written, so a half-imported set
    // of art leaves the knight on his single pose rather than blanking him mid-turn.
    private static bool GivePoses(string knightPrefabPath, string[] poseArt)
    {
        var poses = poseArt.Select(FindSprite).ToArray();
        if (poses.Any(p => p == null))
        {
            string missing = string.Join(", ", poseArt.Where((_, i) => poses[i] == null));
            Debug.LogWarning($"[BlueKnightWiring] Not posing {knightPrefabPath}; no sprite in: {missing}. " +
                             "Let the art import, then run Tools > Two Knights > Wire Blue Right Knight.");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(knightPrefabPath);
        if (root == null) return false;

        try
        {
            var facing = root.GetComponent<KnightFacing>();
            if (facing == null) facing = root.AddComponent<KnightFacing>();

            var so = new SerializedObject(facing);
            bool changed = false;
            for (int i = 0; i < PoseFields.Length; i++)
            {
                var property = so.FindProperty(PoseFields[i]);
                if (property == null || property.objectReferenceValue == poses[i]) continue;
                property.objectReferenceValue = poses[i];
                changed = true;
            }

            // Left null on purpose: KnightFacing finds the shield among its own
            // children on the first frame, which survives the shield being replaced.
            if (!changed) return false;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(facing);
            PrefabUtility.SaveAsPrefabAsset(root, knightPrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // playerProjectilePrefab is private and serialized - authoring data with an
    // inspector, not an API - so it is written the way the inspector would.
    private static bool PointShooterAt(string knightPrefabPath, GameObject arrowPrefab)
    {
        if (arrowPrefab == null) return false;

        GameObject root = PrefabUtility.LoadPrefabContents(knightPrefabPath);
        if (root == null) return false;

        try
        {
            var shooter = root.GetComponentInChildren<PlayerShooter>(true);
            if (shooter == null)
            {
                Debug.LogWarning($"[BlueKnightWiring] {knightPrefabPath} has no PlayerShooter.");
                return false;
            }

            if (!SetShooterArrow(shooter, arrowPrefab)) return false;

            PrefabUtility.SaveAsPrefabAsset(root, knightPrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool SetShooterArrow(PlayerShooter shooter, GameObject arrowPrefab)
    {
        var so = new SerializedObject(shooter);
        var property = so.FindProperty("playerProjectilePrefab");
        if (property == null)
        {
            Debug.LogWarning("[BlueKnightWiring] PlayerShooter has no playerProjectilePrefab field.");
            return false;
        }

        if (property.objectReferenceValue == arrowPrefab) return false;

        property.objectReferenceValue = arrowPrefab;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(shooter);
        return true;
    }

    private static bool DressScene(Sprite blueKnight, Sprite blueShield,
                                   RuntimeAnimatorController blueController,
                                   GameObject blueArrow)
    {
        var scene = EditorSceneManager.GetSceneByPath(MainScene);
        bool openedHere = !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Additive);

        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning($"[BlueKnightWiring] Could not open {MainScene}; the prefabs are dressed " +
                             "but the knight standing in the scene still overrides them.");
            return false;
        }

        try
        {
            GameObject right = scene.GetRootGameObjects()
                                    .FirstOrDefault(go => go.CompareTag(RightKnightTag));
            if (right == null)
            {
                Debug.LogWarning($"[BlueKnightWiring] Nothing tagged {RightKnightTag} in {MainScene}.");
                return false;
            }

            bool changed = false;
            changed |= SetSprite(right.GetComponent<SpriteRenderer>(), blueKnight);

            foreach (var renderer in right.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.CompareTag(ShieldTag)) changed |= SetSprite(renderer, blueShield);
            }

            // Only bites if the scene instance overrides the field; if it inherits, the
            // prefab edit above already carried and this compares equal.
            var shooter = right.GetComponentInChildren<PlayerShooter>(true);
            if (shooter != null && blueArrow != null) changed |= SetShooterArrow(shooter, blueArrow);

            var animator = right.GetComponentInChildren<Animator>(true);
            if (animator != null && blueController != null &&
                animator.runtimeAnimatorController != blueController)
            {
                Undo.RecordObject(animator, "Blue knight animator");
                animator.runtimeAnimatorController = blueController;
                EditorUtility.SetDirty(animator);
                changed = true;
            }

            // Nothing to do to the left knight beyond confirming it is still red.
            // Saying so out loud is cheaper than wondering about it later.
            GameObject left = scene.GetRootGameObjects()
                                   .FirstOrDefault(go => go.CompareTag(LeftKnightTag));
            if (left != null)
            {
                var leftRenderer = left.GetComponent<SpriteRenderer>();
                if (leftRenderer != null && leftRenderer.sprite == blueKnight)
                {
                    Debug.LogWarning("[BlueKnightWiring] The LEFT knight is wearing the blue art. " +
                                     "That is not what this wiring intends.");
                }
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            return changed;
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static bool SetSprite(SpriteRenderer renderer, Sprite sprite)
    {
        if (renderer == null || sprite == null || renderer.sprite == sprite) return false;

        Undo.RecordObject(renderer, "Knight palette");
        renderer.sprite = sprite;
        EditorUtility.SetDirty(renderer);
        return true;
    }
}
