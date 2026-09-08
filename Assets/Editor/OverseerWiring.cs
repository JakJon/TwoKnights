using UnityEditor;
using UnityEngine;

// One-shot: builds the Overseer's prefab, his wave asset, and points The Mine's
// true boss slot at it. Unity runs this the next time it compiles or regains
// focus; it self-disables once the wave asset exists, so it costs nothing after
// the first pass. Tools ▸ Two Knights ▸ Wire The Overseer forces a rebuild.
//
// It lives here rather than being hand-authored as YAML because The Mine's
// MapDefinition is rewritten from Unity's in-memory copy on every refresh — a
// trueBoss guid typed into the .asset by hand is silently dropped the moment the
// editor next saves. Going through AssetDatabase is the only wiring that sticks.
//
// PLACEHOLDER ART: the prefab is cloned off the ordinary pickaxe gnome, so he
// currently wears the green gnome's sprite. The black gnome / gold cart recolour
// replaces the sprite and controller on this prefab; nothing else changes.
public static class OverseerWiring
{
    private const string BossPrefabPath = "Assets/Enemy_DarkGnomeCart.prefab";
    private const string WaveAssetPath = "Assets/Scripts/Waves/Cave/DarkGnome/The Overseer.asset";
    private const string MapPath = "Assets/Scripts/Maps/The Mine.asset";
    private const string SourcePrefabPath = "Assets/Enemy_GnomePickaxeCart.prefab";
    private const string LayoutPath = "Assets/Scripts/Rails/Layouts/Mine Overseer Loop.asset";
    private const string DarkArtPath = "Assets/Graphics/gnome_mine_cart_dark.aseprite";

    // Bumped whenever this file changes what it writes, so an existing set of
    // assets is rebuilt instead of being left at the previous pass's values.
    // Without it the "already exists" guard means edits here never land.
    private const string VersionKey = "TwoKnights.OverseerWiring.Version";
    private const int Version = 4;

    [InitializeOnLoadMethod]
    private static void QueueIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            bool built = AssetDatabase.LoadAssetAtPath<DarkGnome>(WaveAssetPath) != null;
            if (built && EditorPrefs.GetInt(VersionKey, 0) >= Version) return;
            Wire();
        };
    }

    [MenuItem("Tools/Two Knights/Wire The Overseer")]
    public static void Wire()
    {
        GameObject bossPrefab = BuildBossPrefab();
        if (bossPrefab == null) return;

        DarkGnome wave = BuildWave(bossPrefab);
        if (wave == null) return;

        PointTheMineAtIt(wave);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorPrefs.SetInt(VersionKey, Version);
        Debug.Log("[OverseerWiring] Done: prefab, wave asset, and The Mine's true boss at wave 20.");
    }

    private static GameObject BuildBossPrefab()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
        if (source == null)
        {
            Debug.LogError($"[OverseerWiring] No source prefab at {SourcePrefabPath}.");
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.name = "Enemy_DarkGnomeCart";

        var rider = instance.GetComponent<EnemyGnomePickaxeCart>();
        var boss = instance.AddComponent<EnemyDarkGnomeCart>();

        // Everything the two riders share — health bar wiring, hurt and death
        // sounds, damage text, attributes, animator state names, throw offset —
        // carried across by property path, so the boss is the same rider
        // underneath and only the fields set below actually differ. Copying is
        // what stops him arriving with the silent, textless, soundless defaults
        // a fresh AddComponent gives you.
        if (rider != null)
        {
            var from = new SerializedObject(rider);
            var to = new SerializedObject(boss);
            SerializedProperty walk = from.GetIterator();
            bool enterChildren = true;
            while (walk.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (walk.propertyPath == "m_Script") continue;
                SerializedProperty target = to.FindProperty(walk.propertyPath);
                if (target != null && target.propertyType == walk.propertyType)
                {
                    to.CopyFromSerializedProperty(walk);
                }
            }
            to.ApplyModifiedProperties();
            Object.DestroyImmediate(rider, true);
        }

        var so = new SerializedObject(boss);
        Set(so, "health", 2500f);
        SetString(so, "displayName", "The Overseer");
        SetString(so, "bossTitle", "The Overseer");
        SetRef(so, "wreckPrefab", null);            // a boss does not leave a wreck rolling
        Set(so, "armDelay", 3f);
        Set(so, "throwCooldown", 10f);
        Set(so, "throwCooldownTwoLeft", 5f);
        Set(so, "throwCooldownOneLeft", 3.5f);
        SetRef(so, "pickaxePrefab", Load("Assets/Enemy_Pickaxe.prefab"));
        SetInt(so, "volleyEveryNthThrow", 3);
        SetInt(so, "volleyPairs", 3);
        Set(so, "volleySpacing", 0.15f);
        Set(so, "volleyRecovery", 1.5f);
        SetRef(so, "bombPrefab", Load("Assets/Enemy_Bomb.prefab"));
        SetRef(so, "poisonBombPrefab", Load("Assets/Enemy_Bomb_Poison.prefab"));
        SetInt(so, "bombsBetweenPoison", 4);
        Set(so, "minVerticalClearance", 1.5f);
        Set(so, "roarSeconds", 2f);
        Set(so, "lastStandSpeedScale", 1.5f);
        Set(so, "lastStandThrowScale", 0.5f);
        so.ApplyModifiedProperties();

        ApplyDarkArt(instance);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, BossPrefabPath);
        Object.DestroyImmediate(instance);

        Debug.Log($"[OverseerWiring] Boss prefab -> {BossPrefabPath} (2500 HP, black gnome / gold cart).");
        return saved;
    }

    // The recolour: black gnome, gold cart, off gnome_mine_cart_dark.aseprite.
    // Both halves have to move together — the SpriteRenderer's sprite is only
    // what he looks like standing still, and the Animator's controller is what he
    // looks like rolling, so swapping one and not the other gives a gold cart
    // that turns green the moment the wheels start.
    private static void ApplyDarkArt(GameObject instance)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(DarkArtPath);
        if (assets == null || assets.Length == 0)
        {
            Debug.LogWarning($"[OverseerWiring] {DarkArtPath} has not imported yet — the boss keeps the green art. " +
                             "Run Tools ▸ Two Knights ▸ Wire The Overseer once it has.");
            return;
        }

        Sprite frame = null;
        RuntimeAnimatorController controller = null;
        foreach (Object asset in assets)
        {
            var sprite = asset as Sprite;
            // Alphabetically first is frame 0 — "..._0" sorts under "..._1"
            if (sprite != null && (frame == null || string.CompareOrdinal(sprite.name, frame.name) < 0))
            {
                frame = sprite;
            }

            var candidate = asset as RuntimeAnimatorController;
            if (candidate != null) controller = candidate;
        }

        var renderer = instance.GetComponent<SpriteRenderer>();
        if (renderer != null && frame != null) renderer.sprite = frame;

        var animator = instance.GetComponent<Animator>();
        if (animator != null && controller != null) animator.runtimeAnimatorController = controller;

        Debug.Log($"[OverseerWiring] Art: sprite={(frame != null ? frame.name : "MISSING")}, " +
                  $"controller={(controller != null ? controller.name : "MISSING")}");
    }

    private static DarkGnome BuildWave(GameObject bossPrefab)
    {
        DarkGnome wave = AssetDatabase.LoadAssetAtPath<DarkGnome>(WaveAssetPath);
        bool fresh = wave == null;
        if (fresh) wave = ScriptableObject.CreateInstance<DarkGnome>();

        var pickaxeGnome = Load("Assets/Enemy_GnomePickaxeCart.prefab");
        var bombGnome = Load("Assets/Enemy_GnomeMineCart.prefab");
        var empty = Load("Assets/Enemy_MineCart_Empty.prefab");
        var keg = Load("Assets/Enemy_MineCart_Keg.prefab");
        var layout = AssetDatabase.LoadAssetAtPath<RailLayout>(LayoutPath);

        var so = new SerializedObject(wave);
        SetString(so, "waveName", "The Overseer");
        Set(so, "weight", 1000f);
        SetBool(so, "isUnlocked", false);
        SetString(so, "waveDescription",
            "True boss of the Mine. Whatever was turning the millstone rides the loop itself, "
            + "and it brings the shift out with it.");
        SetInt(so, "unlockedAfterXWaves", 19);
        SetInt(so, "lockedAfterXWaves", -1);
        SetBool(so, "useEnemyTracking", true);

        SetRef(so, "railLayout", layout);
        SetInt(so, "entryRun", 0);
        SetRef(so, "bossPrefab", bossPrefab);
        Set(so, "entryDelay", 1f);
        Set(so, "roarAtHealth", 500f);

        // Keg on the outside of each haul, an empty cart between every gnome:
        // the empties are cover, so they are real carts rather than gaps
        GameObject[] first = Train(10, pickaxeGnome, bombGnome, empty, keg);
        GameObject[] second = Train(20, pickaxeGnome, bombGnome, empty, keg);

        SerializedProperty hauls = so.FindProperty("hauls");
        hauls.arraySize = 2;
        WriteHaul(hauls.GetArrayElementAtIndex(0), "ten wide", 2000f, 1.5f, first);
        WriteHaul(hauls.GetArrayElementAtIndex(1), "twenty wide", 1000f, 2.25f, second);

        SerializedProperty orbs = so.FindProperty("orbs");
        if (orbs != null)
        {
            SetBoolRel(orbs, "healthOrb", true);
            SetIntRel(orbs, "count", 6);
            SetRel(orbs, "firstAt", 15f);
            SetRel(orbs, "interval", 20f);
        }
        so.ApplyModifiedProperties();

        if (fresh)
        {
            AssetDatabase.CreateAsset(wave, WaveAssetPath);
            Debug.Log($"[OverseerWiring] Wave asset -> {WaveAssetPath}");
        }
        else
        {
            EditorUtility.SetDirty(wave);
            Debug.Log($"[OverseerWiring] Wave asset refreshed at {WaveAssetPath}");
        }
        return wave;
    }

    /// <summary>
    /// A train of <paramref name="gnomes"/> riders: a keg at each end, an empty
    /// cart between every gnome, and the two kinds alternating three pickaxe to
    /// two bomb so a train always opens and closes with the thrower that has to
    /// be answered by aim rather than by position.
    ///
    /// Built rather than typed out: the second train is twenty riders — forty
    /// one slots — and a hand-written list that long is a list nobody will ever
    /// check again.
    /// </summary>
    private static GameObject[] Train(int gnomes, GameObject pickaxe, GameObject bomb,
                                      GameObject empty, GameObject keg)
    {
        var slots = new System.Collections.Generic.List<GameObject> { keg };
        for (int i = 0; i < gnomes; i++)
        {
            slots.Add(i % 5 == 1 || i % 5 == 3 ? bomb : pickaxe);
            if (i < gnomes - 1) slots.Add(empty);
        }
        slots.Add(keg);
        return slots.ToArray();
    }

    private static void WriteHaul(SerializedProperty haul, string label, float atHealth,
                                  float speedScale, GameObject[] pattern)
    {
        haul.FindPropertyRelative("label").stringValue = label;
        haul.FindPropertyRelative("atBossHealth").floatValue = atHealth;
        haul.FindPropertyRelative("speedScale").floatValue = speedScale;
        haul.FindPropertyRelative("whistle").boolValue = true;

        SerializedProperty slots = haul.FindPropertyRelative("pattern");
        slots.arraySize = pattern.Length;
        for (int i = 0; i < pattern.Length; i++)
        {
            slots.GetArrayElementAtIndex(i).objectReferenceValue = pattern[i];
        }
    }

    private static void PointTheMineAtIt(DarkGnome wave)
    {
        var map = AssetDatabase.LoadAssetAtPath<MapDefinition>(MapPath);
        if (map == null)
        {
            Debug.LogError($"[OverseerWiring] No map at {MapPath}; the boss exists but nothing schedules him.");
            return;
        }

        var so = new SerializedObject(map);
        SetRef(so, "trueBoss", wave);
        SetInt(so, "trueBossWaveNumber", 20);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(map);

        Debug.Log("[OverseerWiring] The Mine: trueBoss = The Overseer, trueBossWaveNumber = 20.");
    }

    // ---- tiny setters that say so out loud when a field has been renamed ----

    private static GameObject Load(string path)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null) Debug.LogWarning($"[OverseerWiring] Missing prefab: {path}");
        return go;
    }

    private static void Set(SerializedObject so, string name, float value)
    {
        SerializedProperty p = Find(so, name);
        if (p != null) p.floatValue = value;
    }

    private static void SetInt(SerializedObject so, string name, int value)
    {
        SerializedProperty p = Find(so, name);
        if (p != null) p.intValue = value;
    }

    private static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = Find(so, name);
        if (p != null) p.boolValue = value;
    }

    private static void SetString(SerializedObject so, string name, string value)
    {
        SerializedProperty p = Find(so, name);
        if (p != null) p.stringValue = value;
    }

    private static void SetRef(SerializedObject so, string name, Object value)
    {
        SerializedProperty p = Find(so, name);
        if (p != null) p.objectReferenceValue = value;
    }

    private static SerializedProperty Find(SerializedObject so, string name)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null) Debug.LogWarning($"[OverseerWiring] No serialized field '{name}' on {so.targetObject}.");
        return p;
    }

    private static void SetRel(SerializedProperty parent, string name, float value)
    {
        SerializedProperty p = parent.FindPropertyRelative(name);
        if (p != null) p.floatValue = value;
    }

    private static void SetIntRel(SerializedProperty parent, string name, int value)
    {
        SerializedProperty p = parent.FindPropertyRelative(name);
        if (p != null) p.intValue = value;
    }

    private static void SetBoolRel(SerializedProperty parent, string name, bool value)
    {
        SerializedProperty p = parent.FindPropertyRelative(name);
        if (p != null) p.boolValue = value;
    }
}
