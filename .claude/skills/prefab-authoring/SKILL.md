---
name: prefab-authoring
description: When to make a Unity prefab in Two Knights vs mutating instances in code, how prefab references travel (upgrade SO > scene field > Resources.Load), and how to build/wire prefabs via the Unity MCP HTTP bridge — including why new-sprite references cannot be hand-authored offline.
---

# Two Knights — Prefab Authoring

## Naming convention (established 2026-07-19, owner-approved)

Prefabs live FLAT at the Assets root, grouped by prefix so alphabetical sorting
clusters families: `Enemy_<Family>_<Variant>` (Enemy_Bat_Dark, Enemy_Rat_Black,
Enemy_Boss_RatKing), `Player_*` (Player_Knight_Left, Player_Sword_Right,
Player_ShadowArrow), `Projectile_*` for enemy-fired shots (Projectile_Rock,
Projectile_Sonar), `Orb_*`, `FX_*`, `UI_*`. Name by the prefab's TRUE in-game
role (check the Spawner's serialized slots — the old `basic_rat` was actually
the black rat). Rename only via `AssetDatabase.RenameAsset` (guid-safe;
case-only renames need a two-step via a temp name on Windows).

## When to make a prefab (the decision rule)

Make a prefab whenever a spawned thing has its **own identity** — its own sprite,
collider shape, components, or tuning that differ from an existing prefab.

The smell that demands one: **spawn-site surgery on someone else's prefab** —
`Instantiate(otherPrefab)` followed by swapping the sprite, resizing the
collider, or bolting on components to make it "become" the new thing. That was
tried for the Shuriken Fan (runtime reskin of the arrow + `Resources.Load` by
magic string) and rejected by the owner as "messy and terrible." The
`Player_Shuriken.prefab` variant replaced ~20 lines of spawn-site mutation with
one `Instantiate(fanPrefab)`.

Runtime mutation IS still right for **per-instance state**: damage numbers,
velocity/spin, tags, temporary tints (GlowManager), added effect components
(PoisonProjectile). Identity lives in the prefab; instance state lives in code.

Prefer a **prefab variant** when the new thing is a reskin/retune of an
existing prefab (Player_Shuriken is a variant of Player_Projectile — arrow
physics/damage changes flow through; only sprite + collider are overridden).

## How prefab references travel (house conventions, strongest first)

1. **Serialized field on the upgrade ScriptableObject**, passed into a runtime
   boost component at apply time — the pattern for anything upgrade-granted.
   See `ShadowArrowUpgrade.shadowArrowPrefab` → `ShadowArrowBoost`, and
   `ShurikenFanUpgrade.shurikenPrefab` → `NinjaBoost.SetShurikenFan`.
   Keep a fallback at the consumer (`?? playerProjectilePrefab`-style).
2. **Serialized field on a scene component** for things that exist per-knight
   (`PlayerShooter.playerProjectilePrefab`).
3. **`Resources.Load`** ONLY for the true singleton pattern
   (`PoisonResourceManager`). Never for individual sprites/prefabs — it couples
   asset location to a magic string and hides the dependency from the editor.

## Building prefabs: editor required, MCP does it

**You cannot hand-author a prefab that references a NEW imported sprite.**
Sprite sub-asset fileIDs are importer-generated hashes (e.g. shuriken's sprite
is `6482666809865804992` inside guid `dd8514d0...`) — underivable offline.
Hand-authoring YAML only works for constant fileIDs (UXML `9197481963319205126`,
USS `7433441132597879392`, prefab root `100100000` — see the
unity-offline-editing-recipes memory).

With the editor open, use the Unity MCP HTTP bridge (mcp-for-unity, port 8080):

```bash
# initialize (grab mcp-session-id response header), then:
curl -s -X POST http://127.0.0.1:8080/mcp \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json, text/event-stream' \
  -H "mcp-session-id: $SID" -d @params.json | sed -n 's/^data: //p'
```

- Responses are SSE (`data:` lines). Send `notifications/initialized` after init.
- `execute_code` needs `{"action":"execute","code":...}` and compiles with
  **CodeDom = C# 6 max** by default: no `?.`, no pattern matching. Build the
  JSON payload with python (`json.dumps`) — inline bash quoting will burn you.
- The proven build snippet: `PrefabUtility.InstantiatePrefab(basePrefab)` →
  mutate (`LoadAllAssetsAtPath(...)` to fish the Sprite sub-asset out of an
  .aseprite) → `SaveAsPrefabAsset(inst, path)` (this makes a VARIANT) →
  `DestroyImmediate(inst)` → wire consumers via
  `new SerializedObject(asset).FindProperty(...)` +
  `ApplyModifiedPropertiesWithoutUndo()` + `SetDirty` + `SaveAssets`.
- After editing .cs files from outside: `refresh_unity`
  `{"mode":"force","scope":"all","compile":"request","wait_for_ready":true}`,
  poll resource `mcpforunity://editor/state` until `data.advice.ready_for_tools`
  is true, then `read_console` (types `["error"]`) — 0 entries = clean. Compile
  BEFORE wiring assets whose serialized fields you just added, or the property
  won't exist to find.
- `manage_asset` move works but may report a bogus failure after succeeding —
  verify on disk, don't retry blindly.

## Post-build checklist

- Prefab on disk + `.meta`; consumers reference `{fileID: 100100000-or-hash, guid: <prefab guid>}`.
- `read_console` clean after a forced refresh.
- Play-mode sanity check the spawn path (upgrade → fire) when possible.
