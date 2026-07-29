---
name: enemy-prefab-authoring
description: How to build a new enemy (or boss) prefab in Two Knights end to end — the EnemyBase contract, the exact component stack, the .aseprite recolour→Animator pipeline, how bosses differ from mobs, and the traps that cost real time (pivot at the feet, DoT bypassing TakeDamage, stale fields in old prefab YAML).
---

# Two Knights — Enemy Prefab Authoring

General prefab rules (naming, when to make one, how references travel, the MCP
`execute_code` bridge) live in the **prefab-authoring** skill — read that first.
This one covers only what is specific to ENEMIES.

## 1. The EnemyBase contract

Every enemy derives from `EnemyBase` (`Assets/Scripts/EnemyBase.cs`), which
already provides health, stagger, poison/fire, damage text, gold, special
award, wave tracking, sprite flipping and collision. A new enemy supplies:

- **`Start()`** — set `attributes` (`EnemyType.Ground|Flying|Splitting`),
  `specialOnHit`, and the sounds off `AudioManager.Instance`. Do NOT set health
  here if a wave config drives it (see §5).
- **`Update()`** — movement. `EnemyBase` does not move anything.
- Optional overrides: `OnDeath()`, `OnAfterDamageApplied()`,
  `GetShieldCollisionDamage()`, `GetPlayerCollisionDamage()`,
  `OnTriggerEnter2D()`, `GetMaxHealth()`, `StatKey`.

`Awake()` is `protected virtual` and registers the enemy for wave tracking —
if you override it you MUST call `base.Awake()` or the wave never completes.

### Traps

- **Poison and fire DoT never go through `TakeDamage`.** `PoisonRoutine` and
  `ApplyFireDamage` decrement `health` directly. So an enemy that gates damage
  by overriding `TakeDamage` (an invulnerability phase, a shield/ward) still
  takes Serpent and Ember damage. That is usually what you want — a fully sealed
  boss blanks two whole Orders — but it must be a decision, not a surprise.
- **`IsStaggered` stops movement only if YOUR `Update` checks it.** `EnemySlime`
  returns early while staggered; `EnemyRatKing` and `EnemyGiantSlime` deliberately
  do not, because their approach is the fight's clock and chip damage must not
  stall it.
- **`StatKey`** defaults to the class name minus "Enemy", lowercased, and becomes
  the `kills.<key>` quest stat. Override it if you want a stable key.

## 2. The component stack

Copy the shape of `Enemy_Slime_Purple.prefab` / `Enemy_Boss_GiantSlime_Left.prefab`:

| Component | Notes |
|---|---|
| `Transform` | `localScale` is how enemies resize (slime sizes 1–3, the giant at 6) |
| `SpriteRenderer` | sprite = the .aseprite's **main** sprite sub-asset |
| `Animator` | `runtimeAnimatorController` = the .aseprite's generated controller |
| `Rigidbody2D` | `gravityScale = 0`. Needed for trigger events to fire at all |
| `Collider2D` | **`isTrigger = true`** — all combat here is trigger-based |
| `GlowManager` | hit flash + telegraphs; `glowManager?.StartGlow(...)` |
| `Enemy<Name>` | your script |

Serialized `EnemyBase` fields worth setting on the prefab: `health`,
`specialOnHit`, `specialOnDeath`, `goldOnDeath`, `shieldDamage`,
`staggerDuration`, `staggerAnimation` (the "Damage" clip), `defaultAnimation`
(the "Walking" clip), `damageTextPrefab` (`Assets/FX_DamageText.prefab`),
`damageTextOffset`, `displayName`.

- **`displayName` needs its article** — the death screen reads
  `"{knight} died from {displayName}"`, so `"a Giant Red Slime"`, not `"Slime"`.
- **`poisonBubblePrefab` does NOT exist on `EnemyBase`.** It is still present in
  older prefab YAML (`Enemy_Slime_Purple`) as a stale leftover; bubbles now come
  from `PoisonResourceManager`. `FindProperty("poisonBubblePrefab")` returns null
  and will NRE. Enumerate the real property list before assuming a field exists:

  ```csharp
  var so = new UnityEditor.SerializedObject(component);
  var p = so.GetIterator();
  while (p.NextVisible(true)) sb.Append(p.propertyPath).Append(" ");
  ```

- **Big enemies need `damageTextOffset` pulled DOWN.** `ShowDamageText` places
  text at `spriteRenderer.bounds.size.y` above the pivot; on a scale-6 body
  that is ~5.4 units up, near the top of the frame. The giant slime uses
  `y = -2.5`. (Damage text does not inherit the parent's scale —
  `SetParent(transform)` preserves world scale — so numbers stay readable.)

## 3. Sprite → Animator pipeline

Art comes from `.aseprite` files in `Assets/Graphics/`, imported by the Aseprite
importer which generates the Texture, per-frame Sprites, one `AnimationClip` per
tag, and an `AnimatorController` — all as sub-assets.

For a **recoloured variant** (the cheapest new enemy), see the sprite-authoring
skill's headless-Lua recipe. Two things that specifically matter here:

1. **Pre-author the `.meta` by copying the source's with a fresh guid.** A new
   `.aseprite` imports at **PPU 100** by default, which silently makes the enemy
   ~3x too big; copying the source meta preserves PPU 32, point filter and pivot,
   and makes the generated sprite/clip fileIDs line up with the original.
2. **Then fix the sprite NAME in the copied meta.** The meta carries
   `name: <source>` in the sprite rect table and the import data, so
   `slime_red.aseprite` will contain a main sprite called `slime_basic` until you
   replace those two lines. Clip names (`Walking`, `Damage`) are tag-derived and
   come out correct on their own.

Fish the sub-assets out by type + name when building the prefab:

```csharp
foreach (var o in AssetDatabase.LoadAllAssetsAtPath("Assets/Graphics/slime_red.aseprite")) {
    if (o is Sprite && o.name == "slime_red") mainSprite = o as Sprite;
    if (o is AnimationClip && o.name == "Walking") walking = o as AnimationClip;
    if (o is AnimatorController) controller = o as AnimatorController;
}
```

Because the clips keep their tag names, `StaggerRoutine`'s `animator.Play(clip.name)`
works on any variant without rewiring.

## 4. Colliders, pivots and scale

- Author the collider **once at scale 1**; `transform.localScale` does the
  resizing. `EnemySlime.baseColliderPoints` is the canonical 15-point outline —
  reuse it verbatim for any slime-shaped enemy.
- **The slime pivot is at its FEET.** The collider spans y `-0.025 → 0.93` in
  local space, so `transform.position` is the bottom of the body and the middle
  sits `~0.45 * scale` above it. At scale 6 that is 2.7 units. Anything that
  should emanate from the creature (projectile spawn points, orbiting objects,
  auras) must use a body-centre helper, not `transform.position`:

  ```csharp
  public Vector2 BodyCenter =>
      _collider != null ? (Vector2)_collider.bounds.center : (Vector2)transform.position;
  ```

  Read it off the **collider**, not the SpriteRenderer — the walk cycle squashes
  the sprite every frame and a SpriteRenderer-derived centre jitters.
- Sanity numbers: visible frame is x ±10, y ±5.625; PPU 32; knights sit at
  (±2, −0.5) and never move. A scale-6 slime is ~6.1 wide and ~5.7 tall — half
  the screen height.

## 5. Mobs vs bosses

**Mob**: add a prefab slot to `Spawner` (`public GameObject foo;`), a
`SpawnFoo(...)` method following `SpawnSlime`/`SpawnBat`, and wire the prefab in
the scene's Spawner inspector. Wave scripts then call `spawner.SpawnFoo(...)`.
See the wave-authoring skill.

**Boss**: the prefab is referenced by the **wave asset** instead (as
`RatKingWave.ratKingPrefab` / `GiantSlimeWave.leftSlimePrefab`), and the wave
hands it a `[System.Serializable] Config` at spawn via an `Initialize(...)`
method. That keeps every tuning knob on the `.asset`, editable without recompiling.
Bosses additionally:

- **Must not be popped by contact.** `EnemyBase.OnTriggerEnter2D` destroys the
  enemy outright on shield or player contact — correct for a mob, fatal for a
  boss. Override it (see `EnemyRatKing` / `EnemyGiantSlime`) and handle only the
  cases you want.
- **Drive `BossHealthBar`** — `Show(title)` on spawn, `SetHealth(current, max)`
  each frame (drives bar + numeric readout together), `Hide()` in `OnDestroy`.
  The bar is a single static in the PlayerHUD document; a multi-body boss sums
  its parts and pushes one combined pool (`GiantSlimeDuel`).
- **Override `GetMaxHealth()`** to return the config value, or Killing Blow
  thresholds read the mutated current value.
- Reveal the bar ~3.5s after spawn so it fades in as the wave banner fades out.

> **Config defaults vs the asset:** editing a default in the `Config` class only
> affects NEWLY created assets. Existing `.asset` files keep their serialized
> values. Retune the asset, not the C# default, or nothing changes in game.

## 6. Audio

New enemy sounds follow the sfx-authoring skill (roll → audition → pin into
`Assets/Sounds/<snake_case>.wav`, add a `SoundEffect` field to `AudioManager`).
The enemy-specific part:

- Assign in `Start()`: `hurtSound = AudioManager.Instance.giantSlimeHurt;`
- **Wire the clip in BOTH `Main.unity` and `Camp.unity`.** `AudioManager` is a
  `DontDestroyOnLoad` singleton that exists as a separate object in each scene,
  and the FIRST one loaded wins — on a normal Camp→Main run that is *Camp's*
  instance. Wiring only Main leaves the enemy silent in real play.
- Respect the noise budget: no per-enemy spawn sounds.

## 7. Build + verify checklist

Build via the MCP bridge (`execute_code`, CodeDom = C# 6, no `using` directives —
it runs as a method body, so fully qualify or it will not compile):

```csharp
var go = new UnityEngine.GameObject("Enemy_Foo");
// ... AddComponent, SetPath, SerializedObject field writes ...
UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, "Assets/Enemy_Foo.prefab");
UnityEngine.Object.DestroyImmediate(go);
```

Add your own types with the non-generic `AddComponent(System.Type.GetType("Foo, Assembly-CSharp"))` —
the generic form needs a compile-time reference the dynamic assembly lacks.

Then verify:

- [ ] `read_console` types `["error"]` is empty after `refresh_unity`.
- [ ] Prefab has 0 missing components:
      `foreach (var c in go.GetComponents<Component>()) if (c == null) ...`
- [ ] Sprite PPU is 32 and the prefab's on-screen size is what you intended.
- [ ] Spawn it in play mode (test-mode-validation skill) and confirm it moves,
      takes damage, dies, and awards gold/special.
- [ ] Sounds actually assigned at runtime (`hurtSound.clip != null`) — a null
      `SoundEffect` fails silently.
- [ ] Wave completes after it dies (i.e. `base.Awake()` ran and tracking works).

`Object.Destroy` is deferred to end-of-frame: a "was it destroyed?" assertion in
the same `execute_code` call always reads false. Check a synchronous flag, or
re-query in a later call.
