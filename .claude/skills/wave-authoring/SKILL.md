---
name: wave-authoring
description: How to create, configure, and register enemy waves in Two Knights — BaseWave contract, Spawner API, .asset conventions, WaveManager registration, and the Unity MCP workflow for doing it all from the editor.
---

# Two Knights — Wave Authoring

## Game context (why waves look the way they do)

Two stationary knights at roughly (-2, 0) and (2, 0). Each player rotates an orbiting
shield (radius ~1, `ShieldOrbit.cs`) with a joystick to block incoming threats — there is
NO player movement. Difficulty therefore comes from the **direction, timing, and tempo**
of attacks, never from spatial dodging. Visible field: x ∈ [-12, 12], y ∈ [-7, 7].

### Hard design rules
1. **Projectiles must spawn out of frame** — outside x ∈ [-12,12], y ∈ [-7,7].
2. **A volley aimed at one knight must never cross the other knight's position.**
   Shallow horizontal shots across the middle get absorbed by the wrong knight and
   never arrive. Keep trajectories ≥ ~30° away from horizontal, or originate them on
   the target's own side.

## Creating a new wave type

1. New folder `Assets/Scripts/Waves/<WaveName>/`, script `<WaveName>.cs`:

```csharp
[CreateAssetMenu(fileName = "MyWave", menuName = "Waves/My Wave")]
public class MyWave : BaseWave
{
    [SerializeField] private int someTunable = 1;   // tunables = per-.asset difficulty knobs

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        // ... spawner.Spawn*(...) calls, yield return new WaitForSeconds(...) for pacing ...
        MarkSpawningComplete();   // REQUIRED — tracking never ends the wave without it
        yield return null;
    }
}
```

- Leave `useEnemyTracking = true`: the Spawner runs `SpawnWave`, then waits until every
  registered enemy AND projectile is dead before ending the wave.
- Class names already taken (avoid collisions): `RatMischief` (inside the misnamed
  `TemplateWave.cs`), `RatMischef` [sic], `WolfCircles`, `BatSwarmWave`, `SlimesAndBats`,
  `ChaoticCorners`, `Slimy`, `AboutFace`. `WolfPack.cs` and `Editor/WaveManagerEditor.cs`
  are empty stubs.

## BaseWave gating — how CanPlay actually works (IMPORTANT)

```
if (isUnlocked) return true;                        // SHORT-CIRCUITS — windows ignored!
if (lockedAfterXWaves >= 0 && count >= lockedAfterXWaves) return false;
if (unlockedAfterXWaves >= 0) return count >= unlockedAfterXWaves;
return false;                                       // all defaults => NEVER plays
```

- To use unlock/lock windows, set `isUnlocked: 0` and give `unlockedAfterXWaves` a value
  ≥ 0 (use 0 for "available from the start").
- `isUnlocked: 1` = always playable; any window values on that asset are **inert**
  (several legacy assets, e.g. Stalkers, are in this state — their locks never apply).
- `isUnlocked: 0` with both windows at -1 = never plays.
- `weight` biases the weighted-random pick; ~1000 is the house convention.
- Each asset plays **at most once per run** (WaveManager removes it after selection).

## Spawner API (the toolkit inside SpawnWave)

Prefabs: `projectilePrefab, brownRat, greyRat, blackRat, slimePrefab, bat,
greyWolfPrefab, brownWolfPrefab, blackWolfPrefab, healthOrbPrefab, manaOrbPrefab`.

Positions: `LeftPlayer`/`RightPlayer` (Transforms), `aboveLeftPlayer(-2,7)`,
`aboveRightPlayer(2,7)`, `belowLeftPlayer(-2,-7)`, `belowRightPlayer(2,-7)`,
`leftOfLeftPlayer(-12,0)`, `rightOfRightPlayer(12,0)`, corners `(±12, ±6)`.

```csharp
SpawnRat(Vector2 targetPos, GameObject ratType, float delay, Transform playerTarget)
SpawnSlime(int size, Vector2 spawnPos, float delay, Transform targetPlayer)   // size 1-3, 3 = king, splits
SpawnBat(Vector2 spawnPos, float delay)                                        // no target
SpawnWolf(List<Vector2> waypoints, Transform targetKnight, WolfType type, float delay = 0)
SpawnProjectile(Transform targetPlayer, Vector2 spawnPos, float delay = 0)
SpawnProjectileStraight(Vector2 spawnPos, Transform targetPlayer, float amount, float projectileDelay, float initialDelay = 0)
SpawnProjectileArc(Transform targetPlayer, ArcDirection dir, Vector2 arcStart, float arcDegrees, int count, float delayBetween, int arcCount = 1, float delayBetweenArcs = 0)
SpawnOrb(Vector2 startPos, Vector2 endPos, bool isHealthOrb, float delay = 0)  // false = mana orb
```

`WolfType { Grey=0, Brown=1, Black=2 }`. Waypoint sets in static `WolfMovementPatterns`
(`CircleLeftThenRight`, `ClockwiseCircle`, `RectangleLoopCW`, `HorizontalSweep`, plus
`Offset`/`Scale` helpers). Enums serialize as ints in .asset YAML.

## Existing waves & difficulty windows (keep new waves coherent)

| Wave (class) | Pattern | Effective window |
|---|---|---|
| Bat Cauldron (BatSwarmWave) | concentric bat rings | early (nominal lock @4, inert — isUnlocked:1) |
| Rat Mischief (RatMischef) | staggered rat formations + arc | nominal 4–8 (inert) |
| Stalkers (WolfCircles) | wolves on circle paths | nominal lock @10 (inert) |
| Sticky Situation (Slimy) | side slime streams + volleys | always |
| Chaotic Corners | corner projectile arcs | always |
| Bat Slime Boogie (SlimesAndBats) | escalating mixed sub-waves | always |
| About Face / Whiplash / Vertigo (AboutFace) | opposite-direction flip volleys | 0–4 / 4–9 / 8+ (real windows) |

## Creating instances & registering

1. Instances via Assets → Create → Waves/<name>, or Unity MCP `manage_scriptable_object`
   (`action:"create"`, `type_name`, `folder_path`, `asset_name`; then `action:"modify"`,
   `target:{"path":...}` — target MUST be an object — and
   `patches:[{"op":"set","path":"<field>","value":...}]`).
2. Register: append the asset to `availableWaves` in
   `Assets/Scripts/Waves/WaveManager.asset` (inspector), or right-click the WaveManager
   asset → **Auto-Find All Waves** (repopulates the whole list via `t:BaseWave` search).
3. Playtest tip: temporarily give the new asset a huge `weight` (e.g. 100000000, the
   "Rat Mischief 1" trick) so it's picked first, then revert.

## Unity MCP workflow

Server: MCP for Unity (CoplayDev), streamable HTTP at `http://127.0.0.1:8080/mcp`,
enabled from inside the Unity editor. Registered with Claude Code as `UnityMCP`
(`claude mcp list` to health-check). **Native `mcp__UnityMCP__*` tools only load if the
editor's MCP server is running when the Claude Code session starts** — otherwise talk to
it with curl JSON-RPC:

1. `POST initialize` (protocolVersion 2025-03-26) → capture `mcp-session-id` response
   header; send `notifications/initialized` with that header.
2. `tools/call` with `{"name":..., "arguments":...}`; responses are SSE — parse the last
   `data: ` line. Resource URIs use slashes: `mcpforunity://editor/state`,
   `mcpforunity://instances`.
3. A reusable helper lives at scratchpad `mcp.sh` pattern: `init` prints a session id,
   `call <sid> <method> <params-json>` returns the result JSON.

Script-change loop: `create_script` (args: `path`, `contents`) → `refresh_unity`
(`mode:"force"`, `compile:"request"`) → poll `mcpforunity://editor/state` until
`compilation.is_compiling` is false → `read_console` (`types:["error"]`) → confirm the
new type exists before creating assets of it (e.g. `execute_code` reflection lookup).
`execute_code` uses CodeDom (C# 6 syntax — no `?.` on Unity objects, no string
interpolation guarantees; `return` a string for output).
