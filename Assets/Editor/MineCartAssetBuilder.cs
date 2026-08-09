using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Builds the mine's cart-and-bomb prefabs and wires them into the Choo Choo wave.
//
// This exists because prefabs that reference sprites CANNOT be hand-authored:
// a sprite sub-asset's fileID is an importer-generated hash, so the only way to
// point at a freshly imported .aseprite is to ask the AssetDatabase, in the
// editor, at build time. Everything else about these prefabs is plain data — the
// interesting decisions all live in the components' own defaults.
//
// It runs itself once, on the first domain reload after the scripts land, and
// then never again: the guard is simply whether the three prefabs already exist.
// Re-run it deliberately from Tools > Two Knights > Rebuild Mine Cart Assets,
// which overwrites them. Safe to delete this file once the prefabs are committed.
public static class MineCartAssetBuilder
{
    private const string EmptyCartPath = "Assets/Enemy_MineCart_Empty.prefab";
    private const string GnomeCartPath = "Assets/Enemy_GnomeMineCart.prefab";
    private const string GreenGnomeCartPath = "Assets/Enemy_GnomePickaxeCart.prefab";
    private const string BombPath = "Assets/Enemy_Bomb.prefab";
    private const string PickaxePath = "Assets/Enemy_Pickaxe.prefab";

    private const string KegCartPath = "Assets/Enemy_MineCart_Keg.prefab";
    private const string RatCartPath = "Assets/Enemy_DeliveryCart_Rat.prefab";
    private const string BatCartPath = "Assets/Enemy_DeliveryCart_Bat.prefab";
    private const string DropFlagPath = "Assets/FX_DropFlag.prefab";

    private const string CartArt = "Assets/Graphics/mine_cart.aseprite";
    private const string KegArt = "Assets/Graphics/mine_cart_tnt.aseprite";
    private const string CageArt = "Assets/Graphics/mine_cart_cage.aseprite";
    private const string BombArt = "Assets/Graphics/bomb.aseprite";
    private const string PickaxeArt = "Assets/Graphics/pickaxe.png";
    private const string FlagArt = "Assets/Graphics/dropoff_flag.png";

    private const string MineMap = "Assets/Scripts/Maps/The Mine.asset";
    private const string DeliveryWave = "Assets/Scripts/Waves/Cave/Delivery/Delivery.asset";
    private const string PowderTrainWave = "Assets/Scripts/Waves/Cave/PowderTrain/Powder Train.asset";
    private const string HorseshoeLayout = "Assets/Scripts/Rails/Layouts/Mine Horseshoe.asset";
    private const string RingLayout = "Assets/Scripts/Rails/Layouts/Mine Ring.asset";

    // Colour says WEAPON, not tier: the red gnome is the one who throws bombs and
    // the original gold one throws pickaxes. gnome_mine_cart_green.aseprite is a
    // third palette that nothing uses yet.
    private const string BombGnomeArt = "Assets/Graphics/gnome_mine_cart_red.aseprite";
    private const string PickaxeGnomeArt = "Assets/Graphics/gnome_mine_cart.aseprite";

    private const string RailArtFolder = "Assets/Graphics/";
    private const string RailHorizontalPrefab = "Assets/Rail_Horizontal.prefab";
    private const string PieceSetPath = "Assets/Scripts/Rails/Layouts/Mine Pieces.asset";

    private static readonly string[] Layouts =
    {
        "Assets/Scripts/Rails/Layouts/Mine Circuit.asset",
        "Assets/Scripts/Rails/Layouts/Mine Straight Track.asset",
        HorseshoeLayout,
        RingLayout
    };

    // kind -> prefab. THE NAMING INVERTS between the two columns: the art is named
    // for where a tile sits in a circuit, RailPieceKind for the two directions the
    // tile CONNECTS. A tile at the top-left of a loop joins the track running east
    // away from it to the track running south below it, so rail_top_left is a
    // CornerSouthEast. Read this twice before changing a row.
    private static readonly KeyValuePair<RailPieceKind, string>[] PieceTable =
    {
        Piece(RailPieceKind.Horizontal, RailHorizontalPrefab),
        Piece(RailPieceKind.Vertical, "Assets/Rail_Vertical.prefab"),          // rail_vertical
        Piece(RailPieceKind.CornerNorthEast, "Assets/Rail_CornerNorthEast.prefab"),  // rail_bottom_left
        Piece(RailPieceKind.CornerNorthWest, "Assets/Rail_CornerNorthWest.prefab"),  // rail_bottom_right
        Piece(RailPieceKind.CornerSouthEast, "Assets/Rail_CornerSouthEast.prefab"),  // rail_top_left
        Piece(RailPieceKind.CornerSouthWest, "Assets/Rail_CornerSouthWest.prefab")   // rail_top_right
    };

    private static KeyValuePair<RailPieceKind, string> Piece(RailPieceKind kind, string path)
    {
        return new KeyValuePair<RailPieceKind, string>(kind, path);
    }

    private const string ReferenceCart = "Assets/Rail_Cart.prefab";
    private const string DamageText = "Assets/FX_DamageText.prefab";
    private const string ChooChooAsset = "Assets/Scripts/Waves/Cave/ChooChoo/Choo Choo.asset";

    private const int CartHealth = 20;

    [InitializeOnLoadMethod]
    private static void AutoBuildOnce()
    {
        EditorApplication.delayCall += () =>
        {
            if (Has(EmptyCartPath) && Has(GnomeCartPath) && Has(GreenGnomeCartPath)
                && Has(BombPath) && Has(PickaxePath) && Has(KegCartPath)
                && Has("Assets/Rail_Vertical.prefab")
                && Has("Assets/Rail_CornerSouthEast.prefab")
                && Has(RatCartPath) && Has(DropFlagPath))
            {
                RepairPieceSetIfMissing();
                RepairSoundsIfMissing();
                return;
            }
            Build();
        };
    }

    [MenuItem("Tools/Two Knights/Rebuild Mine Cart Assets")]
    public static void Build()
    {
        // The reference cart is where the sorting layer, material and cart art all
        // come from — matching it is what keeps a new cart sitting on the rails the
        // same way the old decorative one did
        GameObject reference = AssetDatabase.LoadAssetAtPath<GameObject>(ReferenceCart);
        if (reference == null)
        {
            Debug.LogError($"[MineCartAssetBuilder] Missing {ReferenceCart}; nothing built.");
            return;
        }

        SpriteRenderer referenceRenderer = reference.GetComponent<SpriteRenderer>();
        GameObject damageText = AssetDatabase.LoadAssetAtPath<GameObject>(DamageText);

        GameObject emptyCart = BuildCart(
            EmptyCartPath, "Enemy_MineCart_Empty", CartArt, referenceRenderer, damageText,
            "EnemyMineCart", "a Mine Cart");

        GameObject bomb = BuildBomb(referenceRenderer);
        GameObject pickaxe = BuildPickaxe(referenceRenderer);

        BuildRailPieces();

        GameObject gnomeCart = BuildCart(
            GnomeCartPath, "Enemy_GnomeMineCart", BombGnomeArt, referenceRenderer, damageText,
            "EnemyGnomeMineCart", "a Bomb Gnome");

        if (gnomeCart != null)
        {
            var gnome = gnomeCart.GetComponent(TypeOf("EnemyGnomeMineCart"));
            var so = new SerializedObject(gnome);
            SetRef(so, "bombPrefab", bomb);
            SetRef(so, "wreckPrefab", emptyCart);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gnomeCart);
        }

        // The pickaxe rider wears the original gold palette, and is on a shorter
        // fuse than the bomber — five seconds to the first throw
        GameObject greenCart = BuildCart(
            GreenGnomeCartPath, "Enemy_GnomePickaxeCart", PickaxeGnomeArt, referenceRenderer,
            damageText, "EnemyGnomePickaxeCart", "a Pickaxe Gnome");

        if (greenCart != null)
        {
            var green = greenCart.GetComponent(TypeOf("EnemyGnomePickaxeCart"));
            var so = new SerializedObject(green);
            SetRef(so, "pickaxePrefab", pickaxe);
            SetRef(so, "wreckPrefab", emptyCart);
            SerializedProperty arm = so.FindProperty("armDelay");
            if (arm != null) arm.floatValue = 5f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(greenCart);
        }

        // Ten hit points, not twenty: one arrow should set it off, so that the
        // moment it goes up is the player's decision rather than a chip-damage
        // accident
        GameObject kegCart = BuildCart(
            KegCartPath, "Enemy_MineCart_Keg", KegArt, referenceRenderer, damageText,
            "EnemyKegCart", "a Powder Keg", 10);

        // Cargo lives on the PREFAB, not the wave, so a delivery route is authored
        // by listing which carts roll out in which order — the same way the rat
        // tiers are separate prefabs rather than a field on the spawner.
        GameObject ratCart = BuildCart(
            RatCartPath, "Enemy_DeliveryCart_Rat", CageArt, referenceRenderer, damageText,
            "EnemyDeliveryCart", "a Delivery Cart");
        GameObject batCart = BuildCart(
            BatCartPath, "Enemy_DeliveryCart_Bat", CageArt, referenceRenderer, damageText,
            "EnemyDeliveryCart", "a Delivery Cart");

        WireDeliveryCart(ratCart, emptyCart, 1); // Cargo.GreyRat
        WireDeliveryCart(batCart, emptyCart, 3); // Cargo.Bat

        GameObject flag = BuildDropFlag();

        // Sound lives on the prefabs that own the moment, not on AudioManager.
        // AudioManager's clips are assigned on a SCENE object and have to be wired
        // in Main AND Camp (first-loaded wins), which is exactly the step that
        // keeps silently not happening. A SoundEffect field on the prefab is
        // asset-to-asset and cannot be missed.
        Sound(gnomeCart, "EnemyGnomeMineCart", "throwSound", "bomb_roll", 0.8f);
        Sound(greenCart, "EnemyGnomePickaxeCart", "throwSound", "pickaxe_throw", 0.85f);
        Sound(ratCart, "EnemyDeliveryCart", "dropSound", "cargo_drop", 0.9f);
        Sound(batCart, "EnemyDeliveryCart", "dropSound", "cargo_drop", 0.9f);
        Sound(ratCart, "EnemyDeliveryCart", "haulLoop", "cargo_cart_loop", 0.55f);
        Sound(batCart, "EnemyDeliveryCart", "haulLoop", "cargo_cart_loop", 0.55f);
        WireRailLandSound();

        WireChooChoo(kegCart, gnomeCart, greenCart);
        WirePieceSet(flag);
        BuildWaves(ratCart, batCart, emptyCart, kegCart);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MineCartAssetBuilder] Built Enemy_MineCart_Empty, Enemy_GnomeMineCart, " +
                  "Enemy_Bomb and dealt them into Choo Choo.");
    }

    // Empty cart and gnome cart differ only in their art and which script rides
    // on top, so they are the same build
    private static GameObject BuildCart(string path, string name, string artPath,
        SpriteRenderer reference, GameObject damageText, string scriptName, string displayName,
        int health = CartHealth)
    {
        Sprite sprite;
        RuntimeAnimatorController controller;
        if (!LoadArt(artPath, out sprite, out controller)) return null;

        var go = new GameObject(name);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        CopyPresentation(reference, renderer);

        var animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;

        var body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;

        // Trigger-shaped, like every other combat collider here. Read off the
        // sprite so the pivot (bottom-centre, wheels on the rail) is respected.
        var collider = go.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = sprite.bounds.size;
        collider.offset = sprite.bounds.center;

        go.AddComponent<GlowManager>();

        // Adding the enemy script pulls in MineCart via [RequireComponent]
        var enemy = go.AddComponent(TypeOf(scriptName));

        var cart = go.GetComponent<MineCart>();
        var cartSo = new SerializedObject(cart);
        cartSo.FindProperty("body").objectReferenceValue = renderer;
        cartSo.ApplyModifiedPropertiesWithoutUndo();

        var enemySo = new SerializedObject(enemy);
        enemySo.FindProperty("health").floatValue = health;
        enemySo.FindProperty("displayName").stringValue = displayName;
        // A cart on rails cannot flinch — MineCart owns its position, and the art
        // has no Damage tag to play, so a long stagger would only mute the hit
        enemySo.FindProperty("staggerDuration").floatValue = 0.1f;
        if (damageText != null) SetRef(enemySo, "damageTextPrefab", damageText);
        enemySo.ApplyModifiedPropertiesWithoutUndo();

        return Save(go, path);
    }

    private static GameObject BuildBomb(SpriteRenderer reference)
    {
        Sprite sprite;
        RuntimeAnimatorController controller;
        if (!LoadArt(BombArt, out sprite, out controller)) return null;

        var go = new GameObject("Enemy_Bomb");

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = reference != null ? reference.sharedMaterial : null;
        // Not on the track's Background layer: a bomb leaves the rails and falls
        // through the knights, so it belongs in the play area's draw order
        renderer.sortingLayerID = 0;
        renderer.sortingOrder = 2;

        var animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;

        var body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;

        var collider = go.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.offset = sprite.bounds.center;
        collider.radius = Mathf.Max(sprite.bounds.extents.x, sprite.bounds.extents.y);

        go.AddComponent(TypeOf("EnemyBomb"));

        return Save(go, BombPath);
    }

    // The track's remaining directions. Each is a clone of Rail_Horizontal with
    // its own art, so RailSegment, the dust puff and the sorting all stay
    // identical and only the sprite differs.
    //
    // THE NAMING INVERTS. The art is named for where a tile sits in a circuit;
    // RailPieceKind is named for the two directions the tile CONNECTS. A tile at
    // the top-left of a loop joins the track running east away from it to the
    // track running south below it, so rail_top_left is a CornerSouthEast. Every
    // row below is that same inversion — read it twice before changing one.
    private static void BuildRailPieces()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(RailHorizontalPrefab);
        if (source == null)
        {
            Debug.LogError($"[MineCartAssetBuilder] Missing {RailHorizontalPrefab}; no rail pieces built.");
            return;
        }

        BuildRailPiece(source, "rail_vertical", "Assets/Rail_Vertical.prefab");
        BuildRailPiece(source, "rail_top_left", "Assets/Rail_CornerSouthEast.prefab");
        BuildRailPiece(source, "rail_top_right", "Assets/Rail_CornerSouthWest.prefab");
        BuildRailPiece(source, "rail_bottom_left", "Assets/Rail_CornerNorthEast.prefab");
        BuildRailPiece(source, "rail_bottom_right", "Assets/Rail_CornerNorthWest.prefab");
    }

    private static void BuildRailPiece(GameObject source, string artName, string path)
    {
        string artPath = RailArtFolder + artName + ".aseprite";

        var frames = new List<Sprite>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(artPath))
        {
            var s = o as Sprite;
            if (s != null) frames.Add(s);
        }
        if (frames.Count == 0)
        {
            Debug.LogError($"[MineCartAssetBuilder] {artPath} produced no sprites; {path} not built.");
            return;
        }
        frames.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.name = System.IO.Path.GetFileNameWithoutExtension(path);

        var renderer = instance.GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.sprite = frames[frames.Count - 1]; // settled look

        // These tiles are drawn as a single settled frame, unlike the horizontal
        // piece's six-frame slam. One frame still animates — it just holds for one
        // beat and then fires Completed, which is what spawns the dust — so the
        // cascade timing and the puff both survive with no special-casing.
        var flipbook = instance.GetComponent<SpriteFlipbook>();
        if (flipbook != null) flipbook.EditorSetFrames(frames.ToArray());

        GameObject asset = PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);

        if (asset == null) Debug.LogError($"[MineCartAssetBuilder] Failed to save {path}.");
    }

    // A plain PNG rather than an .aseprite: the pickaxe never animates, it only
    // tumbles, and EnemyPickaxe spins the transform. So no Animator here.
    private static GameObject BuildPickaxe(SpriteRenderer reference)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PickaxeArt);
        if (sprite == null)
        {
            Debug.LogError($"[MineCartAssetBuilder] No sprite at {PickaxeArt}.");
            return null;
        }

        var go = new GameObject("Enemy_Pickaxe");

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = reference != null ? reference.sharedMaterial : null;
        renderer.sortingLayerID = 0;
        renderer.sortingOrder = 2;

        var body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;

        // Circular so the hitbox does not swing around with the tumble — a
        // spinning box collider would make the axe alternately easy and hard to
        // shoot for no reason the player could see
        var collider = go.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.offset = sprite.bounds.center;
        collider.radius = Mathf.Max(sprite.bounds.extents.x, sprite.bounds.extents.y);

        go.AddComponent(TypeOf("EnemyPickaxe"));

        return Save(go, PickaxePath);
    }

    // The piece table lives on the RailNetwork component in the SCENE, not on an
    // asset, so it cannot be filled the way everything else here is. This edits
    // the open scene and marks it dirty rather than saving: clobbering whatever
    // else the scene has in flight would be a much worse trade than making the
    // author press Ctrl+S.
    // Sound fields arrive AFTER the prefabs that carry them — a field added to a
    // script cannot be filled on a prefab that was written by the previous
    // compile, and by then the build guard is satisfied and will not run again.
    // So the wiring is re-checked on reload rather than only at build time.
    [MenuItem("Tools/Two Knights/Wire Mine Sounds")]
    public static void WireSounds()
    {
        Sound(Load(GnomeCartPath), "EnemyGnomeMineCart", "throwSound", "bomb_roll", 0.8f);
        Sound(Load(GreenGnomeCartPath), "EnemyGnomePickaxeCart", "throwSound", "pickaxe_throw", 0.85f);
        Sound(Load(RatCartPath), "EnemyDeliveryCart", "dropSound", "cargo_drop", 0.9f);
        Sound(Load(BatCartPath), "EnemyDeliveryCart", "dropSound", "cargo_drop", 0.9f);
        Sound(Load(RatCartPath), "EnemyDeliveryCart", "haulLoop", "cargo_cart_loop", 0.55f);
        Sound(Load(BatCartPath), "EnemyDeliveryCart", "haulLoop", "cargo_cart_loop", 0.55f);
        WireRailLandSound();
        AssetDatabase.SaveAssets();
    }

    private static void RepairSoundsIfMissing()
    {
        var piece = Load("Assets/Rail_Vertical.prefab");
        if (piece == null) return;

        var segment = piece.GetComponent<RailSegment>();
        if (segment == null) return;

        SerializedProperty effect = new SerializedObject(segment).FindProperty("landSound");
        if (effect == null) return; // not recompiled yet
        if (effect.FindPropertyRelative("clip").objectReferenceValue != null) return;

        WireSounds();
        Debug.Log("[MineCartAssetBuilder] Wired the mine's sounds onto the cart and rail prefabs.");
    }

    private static GameObject Load(string path)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    // Wavs are written by an external tool, and Unity's file watcher can catch one
    // MID-WRITE and cache the partial file as a broken clip that stays broken. So
    // force the import before reading it, and check it actually has samples —
    // a silent clip wired into a prefab looks exactly like no bug at all.
    private static AudioClip LoadClip(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null)
        {
            Debug.LogWarning($"[MineCartAssetBuilder] No clip at {path}.");
            return null;
        }
        if (clip.samples == 0)
        {
            Debug.LogWarning($"[MineCartAssetBuilder] {path} imported empty — re-render it.");
            return null;
        }
        return clip;
    }

    // A SoundEffect is a clip plus a volume, both nested under one serialized
    // field, so it has to be written through the relative properties
    private static void Sound(GameObject prefab, string scriptName, string field,
                              string clipName, float volume)
    {
        if (prefab == null) return;

        var component = prefab.GetComponent(TypeOf(scriptName));
        if (component == null) return;

        AudioClip clip = LoadClip($"Assets/Sounds/{clipName}.wav");
        if (clip == null) return;

        var so = new SerializedObject(component);
        SerializedProperty effect = so.FindProperty(field);
        if (effect == null)
        {
            Debug.LogWarning($"[MineCartAssetBuilder] No field '{field}' on {scriptName}.");
            return;
        }

        effect.FindPropertyRelative("clip").objectReferenceValue = clip;
        effect.FindPropertyRelative("volume").floatValue = volume;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(prefab);
    }

    // RailSegment has carried an empty landSound field since the rails were
    // built; every piece gets the same thud, and at a low volume, because a
    // cascade lands forty of them in a couple of seconds.
    private static void WireRailLandSound()
    {
        AudioClip clip = LoadClip("Assets/Sounds/rail_land.wav");
        if (clip == null) return;

        foreach (var entry in PieceTable)
        {
            var piece = AssetDatabase.LoadAssetAtPath<GameObject>(entry.Value);
            if (piece == null) continue;

            var segment = piece.GetComponent<RailSegment>();
            if (segment == null) continue;

            var so = new SerializedObject(segment);
            SerializedProperty effect = so.FindProperty("landSound");
            if (effect == null) continue;

            effect.FindPropertyRelative("clip").objectReferenceValue = clip;
            effect.FindPropertyRelative("volume").floatValue = 0.35f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(piece);
        }
    }

    private static void WireDeliveryCart(GameObject cart, GameObject wreck, int cargo)
    {
        if (cart == null) return;

        var component = cart.GetComponent(TypeOf("EnemyDeliveryCart"));
        if (component == null) return;

        var so = new SerializedObject(component);
        SerializedProperty c = so.FindProperty("cargo");
        if (c != null) c.enumValueIndex = cargo;
        SetRef(so, "wreckPrefab", wreck);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cart);
    }

    // The flag is pure signage: no collider, no body, nothing that can be shot.
    // Killing the marker would tell the player the delivery was cancelled when it
    // very much was not.
    private static GameObject BuildDropFlag()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FlagArt);
        if (sprite == null)
        {
            Debug.LogError($"[MineCartAssetBuilder] No sprite at {FlagArt}.");
            return null;
        }

        var go = new GameObject("FX_DropFlag");
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;

        var reference = AssetDatabase.LoadAssetAtPath<GameObject>(RailHorizontalPrefab);
        SpriteRenderer railRenderer = reference != null ? reference.GetComponent<SpriteRenderer>() : null;
        if (railRenderer != null)
        {
            renderer.sharedMaterial = railRenderer.sharedMaterial;
            renderer.sortingLayerID = railRenderer.sortingLayerID;
        }
        renderer.sortingOrder = 12; // over the rails and the carts riding them

        return Save(go, DropFlagPath);
    }

    // Both new waves are created here rather than by hand because a wave asset's
    // m_Script guid is generated when Unity first imports the .cs — there is no
    // way to write one offline that points at a script that does not exist yet.
    private static void BuildWaves(GameObject ratCart, GameObject batCart,
                                   GameObject emptyCart, GameObject kegCart)
    {
        var delivery = LoadOrCreateWave(DeliveryWave, "Delivery");
        if (delivery != null)
        {
            var so = new SerializedObject(delivery);
            SetWaveHeader(so, "Delivery!", 1000f,
                "Something is being hauled up the long way round. Stop it before the flag.");
            SetRef(so, "railLayout", AssetDatabase.LoadAssetAtPath<RailLayout>(HorseshoeLayout));
            FillList(so, "cartOrder", new List<GameObject> { ratCart, batCart, ratCart, ratCart });
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(delivery);
        }

        var train = LoadOrCreateWave(PowderTrainWave, "PowderTrain");
        if (train != null)
        {
            var so = new SerializedObject(train);
            SetWaveHeader(so, "Powder Train", 1000f,
                "A ring of powder turns tight around you. Shoot through the gaps.");
            SetRef(so, "railLayout", AssetDatabase.LoadAssetAtPath<RailLayout>(RingLayout));
            // Two nulls per eight slots ARE the gaps — a null costs an interval
            // and spawns nothing, which is what cuts the holes in the wall
            FillList(so, "cartPattern", new List<GameObject>
            {
                emptyCart, kegCart, kegCart, emptyCart, kegCart, kegCart, null, null
            });
            SetRef(so, "ratType", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy_Rat_Grey.prefab"));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(train);
        }

        RegisterWithMineMap(delivery, train);
    }

    private static ScriptableObject LoadOrCreateWave(string path, string typeName)
    {
        var existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
        if (existing != null) return existing;

        System.Type type = TypeOf(typeName);
        if (type == null)
        {
            Debug.LogWarning($"[MineCartAssetBuilder] No type '{typeName}' yet — recompile, then rebuild.");
            return null;
        }

        var created = ScriptableObject.CreateInstance(type);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        AssetDatabase.CreateAsset(created, path);
        return created;
    }

    private static void SetWaveHeader(SerializedObject so, string name, float weight, string description)
    {
        SerializedProperty p = so.FindProperty("waveName");
        if (p != null) p.stringValue = name;
        p = so.FindProperty("weight");
        if (p != null) p.floatValue = weight;
        p = so.FindProperty("isUnlocked");
        if (p != null) p.boolValue = true;
        p = so.FindProperty("waveDescription");
        if (p != null) p.stringValue = description;
        p = so.FindProperty("useEnemyTracking");
        if (p != null) p.boolValue = true;
    }

    private static void FillList(SerializedObject so, string property, List<GameObject> values)
    {
        SerializedProperty list = so.FindProperty(property);
        if (list == null)
        {
            Debug.LogWarning($"[MineCartAssetBuilder] No list '{property}' on {so.targetObject}.");
            return;
        }
        list.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static void RegisterWithMineMap(params ScriptableObject[] waves)
    {
        var map = AssetDatabase.LoadAssetAtPath<ScriptableObject>(MineMap);
        if (map == null)
        {
            Debug.LogWarning($"[MineCartAssetBuilder] Could not load {MineMap}; the new waves are unreachable.");
            return;
        }

        var so = new SerializedObject(map);
        SerializedProperty list = so.FindProperty("waves");
        if (list == null) return;

        foreach (var wave in waves)
        {
            if (wave == null) continue;

            bool present = false;
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == wave) { present = true; break; }
            }
            if (present) continue;

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = wave;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(map);
    }

    // Cheap enough to run on every domain reload, and it has to be: a tileset that
    // silently isn't wired shows up as a track built entirely out of straights,
    // which reads as odd art rather than a broken reference.
    private static void RepairPieceSetIfMissing()
    {
        var set = AssetDatabase.LoadAssetAtPath<RailPieceSet>(PieceSetPath);
        if (set == null || set.For(RailPieceKind.CornerSouthEast) == null)
        {
            WirePieceSet();
            return;
        }

        foreach (string layoutPath in Layouts)
        {
            var layout = AssetDatabase.LoadAssetAtPath<RailLayout>(layoutPath);
            if (layout != null && layout.PieceSet == null)
            {
                WirePieceSet();
                return;
            }
        }
    }

    // Assets only — no scene is opened, loaded or dirtied. That is the entire
    // reason the tileset moved off the RailNetwork component.
    [MenuItem("Tools/Two Knights/Wire Rail Piece Set")]
    public static void WirePieceSet(GameObject dropFlag = null)
    {
        var set = AssetDatabase.LoadAssetAtPath<RailPieceSet>(PieceSetPath);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<RailPieceSet>();
            AssetDatabase.CreateAsset(set, PieceSetPath);
        }

        var so = new SerializedObject(set);
        SerializedProperty list = so.FindProperty("pieces");
        if (list == null)
        {
            Debug.LogWarning("[MineCartAssetBuilder] RailPieceSet has no pieces field — recompile first.");
            return;
        }

        list.arraySize = PieceTable.Length;
        int missing = 0;
        for (int i = 0; i < PieceTable.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PieceTable[i].Value);
            if (prefab == null) missing++;

            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("kind").enumValueIndex = (int)PieceTable[i].Key;
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        }
        // Only overwrite when we have one — a repair pass triggered from a domain
        // reload has no flag in hand and must not clear the one already wired
        if (dropFlag == null) dropFlag = AssetDatabase.LoadAssetAtPath<GameObject>(DropFlagPath);
        SetRef(so, "dropFlagPrefab", dropFlag);

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(set);

        foreach (string layoutPath in Layouts)
        {
            var layout = AssetDatabase.LoadAssetAtPath<RailLayout>(layoutPath);
            if (layout == null) continue;

            var layoutSo = new SerializedObject(layout);
            SetRef(layoutSo, "pieceSet", set);
            layoutSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(layout);
        }

        AssetDatabase.SaveAssets();
        if (missing > 0)
        {
            Debug.LogWarning($"[MineCartAssetBuilder] {missing} rail piece prefab(s) missing; " +
                             "those kinds will fall back to the horizontal tile.");
        }
        else
        {
            Debug.Log($"[MineCartAssetBuilder] Wired all {PieceTable.Length} rail pieces into " +
                      $"{PieceSetPath} and pointed the layouts at it.");
        }
    }

    private static void WireChooChoo(GameObject kegCart, GameObject gnomeCart, GameObject greenCart)
    {
        var wave = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ChooChooAsset);
        if (wave == null)
        {
            Debug.LogWarning($"[MineCartAssetBuilder] Could not load {ChooChooAsset}; carts not dealt.");
            return;
        }

        // Two of each, interleaved rather than grouped. Six carts released evenly
        // around a loop land one every sixth of it, so this ordering puts each
        // PAIR exactly half a loop apart: the two bomb gnomes never reach the
        // midpoint together, and the two pickaxe gnomes never come off cooldown
        // together. Group them (keg, keg, bomb, bomb, ...) and each pair arrives
        // back to back with a long dead stretch behind it.
        //
        // The kegs take the plain carts' slots rather than adding to the count —
        // the wave is already dense, and dead empties still turn up on their own
        // every time a gnome is killed out of one.
        var order = new List<GameObject>
        {
            kegCart, gnomeCart, greenCart,
            kegCart, gnomeCart, greenCart
        };

        var so = new SerializedObject(wave);
        SerializedProperty list = so.FindProperty("cartOrder");
        if (list == null)
        {
            Debug.LogWarning("[MineCartAssetBuilder] Choo Choo has no cartOrder field — recompile first.");
            return;
        }

        list.arraySize = order.Count;
        for (int i = 0; i < order.Count; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = order[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(wave);
    }

    // The .aseprite importer emits per-frame Sprites, a clip per tag and one
    // controller, all as sub-assets. Untagged files name their frames Frame_N, so
    // there is no sprite named after the file to look for — take Frame_0, or
    // whatever the first sprite is if the art is ever retagged.
    private static bool LoadArt(string path, out Sprite sprite, out RuntimeAnimatorController controller)
    {
        sprite = null;
        controller = null;

        Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
        if (all == null || all.Length == 0)
        {
            Debug.LogError($"[MineCartAssetBuilder] Nothing imported at {path}.");
            return false;
        }

        Sprite first = null;
        foreach (Object o in all)
        {
            var asSprite = o as Sprite;
            if (asSprite != null)
            {
                if (first == null) first = asSprite;
                if (asSprite.name == "Frame_0" || asSprite.name == System.IO.Path.GetFileNameWithoutExtension(path))
                {
                    sprite = asSprite;
                }
            }

            var asController = o as RuntimeAnimatorController;
            if (asController != null) controller = asController;
        }

        if (sprite == null) sprite = first;

        if (sprite == null)
        {
            Debug.LogError($"[MineCartAssetBuilder] {path} produced no sprites.");
            return false;
        }
        return true;
    }

    private static void CopyPresentation(SpriteRenderer from, SpriteRenderer to)
    {
        if (from == null) return;
        to.sharedMaterial = from.sharedMaterial;
        to.sortingLayerID = from.sortingLayerID;
        to.sortingOrder = from.sortingOrder;
    }

    private static GameObject Save(GameObject go, string path)
    {
        GameObject asset = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);

        foreach (Component c in asset.GetComponents<Component>())
        {
            if (c == null)
            {
                Debug.LogError($"[MineCartAssetBuilder] {path} saved with a missing component.");
                break;
            }
        }
        return asset;
    }

    private static void SetRef(SerializedObject so, string property, Object value)
    {
        SerializedProperty p = so.FindProperty(property);
        if (p == null)
        {
            Debug.LogWarning($"[MineCartAssetBuilder] No field '{property}' on {so.targetObject}.");
            return;
        }
        p.objectReferenceValue = value;
    }

    private static System.Type TypeOf(string name)
    {
        return System.Type.GetType(name + ", Assembly-CSharp");
    }

    private static bool Has(string path)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
    }
}
