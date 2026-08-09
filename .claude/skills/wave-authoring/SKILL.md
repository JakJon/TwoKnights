---
name: wave-authoring
description: How to create, configure, and register enemy waves in Two Knights — BaseWave contract, Spawner API, .asset conventions, WaveManager registration, and the Unity MCP workflow for doing it all from the editor.
---

# Two Knights — Wave Authoring

## Game context (why waves look the way they do)

Two stationary knights at roughly (-2, 0) and (2, 0). Each player rotates an orbiting
shield (radius ~1, `ShieldOrbit.cs`) with a joystick to block incoming threats — there is
NO player movement. Difficulty therefore comes from the **direction, timing, and tempo**
of attacks, never from spatial dodging. Spawn bounds: x ∈ [-12, 12], y ∈ [-7, 7].
NOTE (2026-07-09): the PixelPerfectCamera shows 20×11.25 units (x ±10, y ±5.625). The
±12/±7 spawn bounds are playtested and confirmed fine with this view — spawns sit ~2 units
deeper off-screen and just travel a beat longer before entering frame. No retune needed.
For NEW waves: keep spawns at/beyond ±12/±7 as before, but put the meaningful on-screen
action inside x ±10 / y ±5.6 (e.g. wolf circle paths, rat formation targets).

### Hard design rules
1. **Projectiles must spawn out of frame** — outside x ∈ [-12,12], y ∈ [-7,7].
2. **A volley aimed at one knight must never cross the other knight's position.**
   Shallow horizontal shots across the middle get absorbed by the wrong knight and
   never arrive. Keep trajectories ≥ ~30° away from horizontal, or originate them on
   the target's own side.
3. **Never design a wave where shield-absorbing an ENEMY is the intended play.**
   Any player damage resets the special streak (`PlayerHealth.TakeDamage` →
   `ResetSpecialStreak`), and an enemy body hitting the shield still deals reduced
   damage through it — so eating an enemy on the shield ends the combo. Blocking
   PROJECTILES is damage-free and combo-safe. Therefore: every enemy must arrive
   with enough exposed, in-range time to be shot down first; shield-eating an enemy
   is the player's failure state, never the wave's solution.
4. **Shield facing = shooting direction (one facing resource).** A knight cannot
   block one direction and fire another (`PlayerShooter` fires along
   `shield.Direction`). Projectile streams therefore STEER a knight's aim — great
   for tension — but must release before an enemy arrival so the knight can swing
   and shoot it in time.
5. **Budget kills against the arrow economy.** Base arrow: 10 dmg on a ~1.5s
   cooldown (~6.7 dps per knight, pre-upgrades). TTK in arrows: bat 1, size-1
   slime 1, size-2 slime 2 (+2 for its splits), wolves 3/5/6 (brown/grey/black).
   Rats are prefab-tuned and NOT one-shot kills — keep ≤ ~2 patrolling rats per
   knight; big simultaneous rat counts are only feasible late, fully upgraded.
6. **No randomness inside a wave (owner's design pillar, stated 2026-07-19).**
   Given a wave, its content must play out identically every run — players
   learn exact spawn timings/positions. WHICH wave is picked stays weighted-
   random (WaveManager pool), but wave scripts must not roll dice for enemy
   composition, positions, or timings. For "varied but fixed" sequences use a
   golden-ratio stride over the legal range with a per-wave step counter that
   RESETS in SpawnWave (SO state persists between runs) — see AboutFace.cs,
   refactored to this pattern 2026-07-19.

## Creating a new wave type

1. New folder under the map's bucket — `Assets/Scripts/Waves/Forest/<WaveName>/` for the
   Camp Fields, `Assets/Scripts/Waves/Cave/<WaveName>/` for the Mine — script `<WaveName>.cs`:

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
  `ChaoticCorners`, `Slimy`, `AboutFace`, `SheepsClothing`, `BelfryAndCellar`,
  `NightHunt`, `ChooChoo`, `Delivery`, `PowderTrain`. `WolfPack.cs` and
  `Editor/WaveManagerEditor.cs` are empty stubs.

## Ambushes — player-paced groups inside a wave (added 2026-08-04)

An **ambush** is a group of enemies inside a wave that comes out at roughly the same
time. It is the wave's pacing unit: release a group, wait until the players have
killed it, release the next. The wave's length is then set by how fast the knights
shoot rather than by a stopwatch, which is what a fixed schedule can never do.

"Ambush clear" and "wave over" are DIFFERENT QUESTIONS — do not reach for the wrong one:

| | `AreAllEnemiesDead()` | `IsAmbushClear()` |
|---|---|---|
| Scope | everything the wave ever spawned | just the current group |
| Projectiles | counted | ignored |
| Gate | `MarkSpawningComplete()` | `MarkAmbushReleased()` |
| Means | the wave ends now | send the next group |

```csharp
for (int i = 0; i < ambushes.Count; i++)
{
    BeginAmbush();                       // enemies registering from here belong to this group
    /* ...spawner.Spawn*(...) the group... */
    MarkAmbushReleased();                // REQUIRED — an unreleased group can never read as clear
    yield return WaitForAmbushClear();   // holds until every member is dead
}
MarkSpawningComplete();
```

Rules that cost real time if you miss them:

- **`MarkAmbushReleased()` is as load-bearing as `MarkSpawningComplete()`.** Forget it
  and the wave hangs forever. Call it on every exit path out of the release loop
  (`break`, not `yield break`).
- **Delayed spawns need a declared count.** `Spawner.SpawnRat/Bat/Slime/Wolf(delay)`
  instantiate AFTER the wait, so they register late — a group whose first member dies
  before its second exists reads as clear and the wave runs over the top of it. Either
  pace the group with the wave's own `yield return new WaitForSeconds`, or declare it:
  `BeginAmbush(expectedMembers: 4)`.
- **Enemies that don't gate wave completion don't join groups either.** Anything with
  `TracksWaveCompletion => false` (empty mine carts, kegs, gnome wrecks) never registers,
  so a track still full of rolling iron reads as clear. A group of nothing but scenery
  clears the instant it is released — intentional, so it can't stall.
- **There is NO timeout** (owner's call, 2026-08-04): the next ambush is a reward for
  clearing the last one, and a wave that sends it anyway after N seconds is just a
  schedule in disguise. The corollary is a design obligation — **keep standing pressure
  running while the group is alive** (ChooChoo keeps the shafts firing), or a group left
  alive is a free rest instead of a fight.
- Whatever spawns while a group is open joins it, summons included — so a boss's brood
  gates its own ambush without any extra bookkeeping.
- Debug: `AmbushIndex`, `AmbushEnemiesRemaining`, and an editor-only
  `[Ambush] <asset> cleared ambush N at t=…` log per clear.

Worked example: `ChooChoo.cs` (shifts of gnome carts on the Mine Circuit, one shift per
ambush, shafts firing throughout).

## BaseWave gating — how CanPlay actually works (IMPORTANT)

```
if (lockedAfterXWaves >= 0 && count >= lockedAfterXWaves) return false;  // lock beats EVERYTHING
if (isUnlocked) return true;                        // skips the unlock window only
if (unlockedAfterXWaves >= 0) return count >= unlockedAfterXWaves;
return false;                                       // all defaults => NEVER plays
```

(Precedence changed 2026-07-19: the lock window used to be inert on `isUnlocked: 1`
assets; now it is a hard cutoff that overrides `isUnlocked`.)

- To use an unlock window, set `isUnlocked: 0` and give `unlockedAfterXWaves` a value
  ≥ 0 (use 0 for "available from the start").
- `isUnlocked: 1` = available from wave 1, but `lockedAfterXWaves` still applies;
  only the `unlockedAfterXWaves` value is inert on such assets.
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
SpawnRat(Vector2 targetPos, GameObject ratType, float delay, Transform playerTarget, bool bypassStrengthGate = false)
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

NOTE (2026-07-18, reworked 2026-08-04) — **staggered enemies, per map**: a map may hold
its stronger enemies back and let them in partway through the run. That is a property of
the MAP, not of the game — `MapDefinition.strongerEnemiesFromWave` (-1 = derive it as
gate boss + 1). Do NOT call these "elites" (owner, 2026-08-04): they are ordinary
enemies that a given map staggers in, and are perfectly normal residents elsewhere.

- **Camp Fields**: `-1` → from wave 11, behind the rat king.
- **The Mine**: `1` → dark bats, black wolves and brown/black rats from wave one. The
  Mine will stagger in its own additions later, on its own schedule.

Below the threshold the Spawner silently downgrades at spawn time: brown/black rats →
`greyRat`, `WolfType.Black` → `Grey` (brown wolves are the weakest and stay). So a wave
asset may *request* them in any window — early plays get the grey stand-in, later plays
get the real thing (this is why straddling windows like Night Hunt 1 / Belfry 2 need no
per-asset splits). `bypassStrengthGate: true` on SpawnRat exempts boss summons
(EnemyRatKing's brood).

At or past the threshold, every 4th `SpawnBat` call of a wave
(`Spawner.darkBatInterval`, counter resets each wave, decided at CALL time in wave-script
order) substitutes Enemy_Bat_Dark.prefab — a sonar-firing bat that Confuses a knight
(reversed shield controls, 5s) unless the sonar is blocked/slashed/shot. DETERMINISTIC by
design — see rule 6. **In the Mine this is live from wave 1**, so budget for it in
tier-0 mine waves.

Camp Fields' arena backdrop swaps in step: `BackgroundController` on the
BackGround_Forest scene object switches to the deep-forest sprite at wave 11 (stage list
is serialized on the component — add entries there for future areas).

## Existing waves & difficulty windows (keep new waves coherent)

| Wave (class) | Pattern | Effective window |
|---|---|---|
| Bat Cauldron (BatSwarmWave) | concentric bat rings | 0–4 (lock real since 2026-07-19; unlock inert — isUnlocked:1) |
| Rat Mischief (RatMischef) | staggered rat formations + arc | 0–8 (lock real; nominal unlock @4 inert) |
| Stalkers (WolfCircles) | wolves on circle paths | 0–10 (lock real) |
| Sticky Situation (Slimy) | side slime streams + volleys | always |
| Chaotic Corners | corner projectile arcs | always |
| Bat Slime Boogie (SlimesAndBats) | escalating mixed sub-waves | always |
| About Face / Whiplash / Vertigo (AboutFace) | opposite-direction flip volleys | 0–4 / 4–9 / 8+ (real windows) |
| Sheep's Clothing 0–3 (SheepsClothing) | slime wall as arrow cover + stalking wolf | 1–7 / 5–11 / 9–15 / 13+ |
| Belfry and Cellar 0–3 (BelfryAndCellar) | bat beat high/low + rat 15s-fuse pairs | 0–5 / 3–9 / 7–13 / 11+ |
| Night Hunt 0–3 (NightHunt) | telegraphed wolf+bat pincer strikes | 3–9 / 7–13 / 11–17 / 15+ |

**The Mine** (`Assets/Scripts/Maps/The Mine.asset`) is a map-scoped pool — the forest
waves above never play there, and these three never play in the forest:

| Wave (class) | Pattern | Layout |
|---|---|---|
| Choo Choo | gnome carts in ambush shifts on a loop | Mine Circuit (40-unit lap) |
| Delivery | cargo carts racing to a drop flag | Mine Horseshoe (open route) |
| Powder Train | keg ring with gaps + enemies outside it | Mine Ring (32-unit lap) |

Rail waves have their own arithmetic: on a LOOPING layout the release interval is a
permanent spacing (every cart runs at one speed), so derive it as `lap ÷ carts ÷ speed`
rather than by feel — `RailNetwork.TryMeasureLoop` will measure the lap for you.
Carts enter a lead-in behind the mouth (`MineCart.leadIn`, 6u) while returning traffic
re-enters 1u behind it, so releasing into a busy ring is safe by construction — a new
cart is always ≥5u clear of the traffic ahead.

NOTE (2026-07-09): the 12 assets above currently carry weight **100000000** (the
guarantee-pick trick) for playtesting. Revert each to the house ~1000 once playtested.

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
interpolation guarantees; `return` a string for output). It REQUIRES
`action:"execute"` alongside `code` (missing_argument error otherwise).

More session-tested tool facts:
- `manage_camera` `action:"screenshot"` (omit `camera`) captures the Game view
  including UI Toolkit overlay UI → saves to `Assets/Screenshots/<name>.png`
  (async, ~1-2s). Great for visually verifying UI changes in play mode; delete
  the folder afterwards so it doesn't pollute Assets.
- Calling `refresh_unity` while in play mode can disconnect/stop the editor
  session mid-call. Stop play mode first, refresh, then re-enter play.
- USS/UXML changes need a `refresh_unity` to reimport, and a menu re-show
  (e.g. `SetMenuVisible(false)` then `(true)`) to re-resolve styles.
- `manage_editor` `action:"play"/"stop"`, `manage_scene` `action:"load"` with
  `path` work in edit mode; set `Time.timeScale = 0` via `execute_code` if you
  need a stable play-mode screenshot without the game killing the knights.
- **Play mode FREEZES while the editor app is unfocused** — this project has
  `Application.runInBackground = false`, so `Time.time` stops advancing the
  moment the editor loses OS focus (coroutines, waves, bosses all stall at
  their current frame). Headless play-mode tests only advance if the user has
  the editor focused; prefer edit-mode logic tests (call the ScriptableObject
  methods directly, reflection-set private state) for flow verification.
- **Screenshots are STALE FRAMES when the editor app is unfocused** (OS-level;
  `InternalEditorUtility.isApplicationActive == false`): the Game view stops
  repainting, so `manage_camera` screenshots return the last rendered frame no
  matter what you changed — and `resolvedStyle` on UI Toolkit elements reads
  defaults/stale values until a panel update runs. QueuePlayerLoopUpdate +
  `EditorWindow.GetWindow(GameView).Repaint()` via `execute_code` forces ONE
  style/layout pass (enough for `resolvedStyle` verification a call later), but
  pixels still may not refresh — verify UI programmatically (class lists +
  resolvedStyle after a forced repaint) instead of trusting screenshots.
- `manage_camera` screenshot takes NO `name` argument — call with only
  `{"action":"screenshot"}`; it names the file itself under Assets/Screenshots.
- CodeDom `execute_code` can't resolve UI Toolkit extension methods like
  `element.Q(...)` — call them as statics:
  `UnityEngine.UIElements.UQueryExtensions.Q(root, "item-one", (string)null)`.
- New .asset files can be authored as plain YAML text with hand-written .meta
  files (guid = `openssl rand -hex 16`) — Unity adopts the pre-made guids on
  refresh, which lets you write cross-referencing `unlockedBy`-style chains in
  one pass without MCP patch calls. `m_Script` guid comes from the target
  script's .cs.meta.
