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
3. **A spawn pattern is ALWAYS staggered — two projectiles never leave on the
   same frame** (owner, 2026-09-06). This is a fairness rule, not polish, and it
   falls straight out of the geometry: a knight has ONE guard covering ONE
   direction. Rock spread over time is something a player holds a facing through
   and reads; rock that arrives all at once is a number subtracted from their
   health with a picture attached, and no aim, reaction or upgrade changes the
   outcome. Flight time is identical for every projectile of a group, so the gap
   between RELEASES is exactly the gap between ARRIVALS.

   Enforced at the choke point rather than left to authors:
   `Spawner.MinProjectileStagger` (0.06s) is a floor applied inside
   `SpawnProjectileStraight` and `SpawnProjectileArc`, so a 0 passed to either no
   longer means "all of them, this frame". `RockVolley.salvoStagger` (0.1s house
   value) does the same for salvos, with its own floor. A caller asking for a
   bigger gap always gets it; these only ever raise a floor. **Do not add a spawn
   path that bypasses them** — if you write a new group helper, give it a floor
   too.

4. **Never design a wave where shield-absorbing an ENEMY is the intended play.**
   Any player damage resets the special streak (`PlayerHealth.TakeDamage` →
   `ResetSpecialStreak`), and an enemy body hitting the shield still deals reduced
   damage through it — so eating an enemy on the shield ends the combo. Blocking
   PROJECTILES is damage-free and combo-safe. Therefore: every enemy must arrive
   with enough exposed, in-range time to be shot down first; shield-eating an enemy
   is the player's failure state, never the wave's solution.
5. **Shield facing = shooting direction (one facing resource).** A knight cannot
   block one direction and fire another (`PlayerShooter` fires along
   `shield.Direction`). Projectile streams therefore STEER a knight's aim — great
   for tension — but must release before an enemy arrival so the knight can swing
   and shoot it in time.
6. **Budget kills against the arrow economy.** Base arrow: 10 dmg on a ~1.5s
   cooldown (~6.7 dps per knight, pre-upgrades). TTK in arrows: bat 1, size-1
   slime 1, size-2 slime 2 (+2 for its splits), wolves 3/5/6 (brown/grey/black).
   Rats are prefab-tuned and NOT one-shot kills — keep ≤ ~2 patrolling rats per
   knight; big simultaneous rat counts are only feasible late, fully upgraded.
7. **Every wave carries rock, and the rock is tailored to that wave** (owner,
   2026-09-06). A wave that asks only for arrows and position leaves the guard —
   half of what a knight IS — idle for its whole length, and there is no such
   thing as a wave where that is the right call by accident. So: a new wave is not
   finished until it has a projectile pattern authored FOR it, answering what that
   wave is already doing (Near and Far throws from above while the near track runs
   underneath; Run of the Mine works the shafts while the pack is out on the road).
   The rock is never boilerplate pasted between waves.

   The exception is a wave whose whole body is already one continuous projectile
   problem, and there are two: **The Millstone**, which is a hundred small threats
   arranged into one, and **Crossfire**, which is nothing but bursts. Adding shaft
   volleys to either is adding a second copy of what it already is — and Crossfire
   is not to be given salvos either, for the same reason. If a new wave
   claims the exception, say so in its file header and say why.

8. **A wave gets at most ONE health orb, unless it is a boss** (owner, 2026-09-06).
   Mana orbs are unconstrained — two is fine, and one of each is fine — but health
   is the resource a run is actually spending, and a wave that hands back two of
   them stops being a cost. Bosses are exempt because their fights are long enough
   to need a recovery on their own clock: see The Overseer, whose orbs are
   released on the BOSS's health rather than on a stopwatch (owner, 2026-10-19:
   health at 3000, two mana at 2000, health at 1000 — `orbDrops` on the asset),
   so a fast kill is not paid the same medicine a slow one earns.

   **The Overseer (rebuilt 2026-10-19/20)** is the one wave whose rock and boss
   projectiles share a clock: per-stage attack lists (`OverseerStage.attacks`,
   rock volleys AND his axe throws) placed by landing time, with gnomes holding
   their throws via `IncomingLedger` (`EnemyGnomeCart.WaitForClearLanding`).
   Built by Tools ▸ Two Knights ▸ Wire The Overseer (OverseerWiring.cs, a first
   draft the owner tunes); check a list on paper with Tools ▸ Two Knights ▸ Check
   Overseer Attack Lists before playing it. `RockVolley.Plan` gives any wave a
   volley's exact landings without firing it.

   2026-10-14: two forest waves broke this in code, not in an asset, and were
   fixed: Sheep's Clothing paid a health orb on each of its two walls (now the
   second wall only), and Belfry and Cellar IV–VI paid one on every odd rat pair
   that carried an orb (now only the last of them). Loop-driven orb code needs
   the same care as `count`.

   In practice: `OrbRun.healthOrb: 1` with `count` above 1 is the bug, and it is
   easy to author by accident because the field pair reads as one orb run. If a
   wave has several `OrbRun` fields (Bits and Pieces, Cart Loads), only one of them
   may be a health run.

   **Every Mine wave has at least one orb** (owner, 2026-09-23). Never author a
   Mine tier with `count: 0`. And because most Mine waves end when the players kill
   what is out, `OrbRun`'s `firstAt` timer alone is not enough: a quick clear used
   to finish the wave before the timer fired, and the `StopCoroutine` at the end
   took the orb with it. So every Mine wave calls
   `orbs.SendFirstIfWaiting(spawner)` as its LAST group goes out (last ambush
   released, last rider out, last crate out). That sends the first orb right then
   if the timer has not already, while there is still something on the board. A
   new Mine wave must make the same call.

9. **Tier names carry Roman numerals; the first tier carries nothing** (owner,
   2026-09-06). "Choo Choo", then "Choo Choo II", "Choo Choo III", "Choo Choo IV".
   Waves whose tiers all shared one name (Neighbours, Run of the Mine, Up the
   Middle) were the reason: three assets called "Neighbours" told the player
   nothing about which one had just started.

   **THE FILE NAME IS THE IN-GAME NAME, CHARACTER FOR CHARACTER** (owner,
   2026-09-09; this replaces the old "the file name keeps its arabic digit"). So
   `Bat Cauldron.asset`, `Bat Cauldron II.asset`, `Delivery!.asset`, `The Crimson
   Twins.asset`, `A Sticky Situation.asset`. No arabic digits and no zero-indexed
   files — the whole tree was swept onto this on 2026-09-09 and `m_Name` was
   brought along with it. Two consequences worth knowing before you rename
   anything: guids live in the `.meta`, so moving the pair keeps every reference
   intact; but `WaveExploration` slugs its per-wave stat key off the ASSET NAME
   (`waves.seen.<map>.<slug>`), so a rename makes an already-seen wave count as
   new and inflates `waves.distinct.<map>` in an existing save.

10. **No randomness inside a wave (owner's design pillar, stated 2026-07-19).**
   Given a wave, its content must play out identically every run — players
   learn exact spawn timings/positions. WHICH wave is picked stays weighted-
   random (WaveManager pool), but wave scripts must not roll dice for enemy
   composition, positions, or timings. For "varied but fixed" sequences use a
   golden-ratio stride over the legal range with a per-wave step counter that
   RESETS in SpawnWave (SO state persists between runs) — see AboutFace.cs,
   refactored to this pattern 2026-07-19.

11. **A wolf is on screen for at least 6 seconds before it can strike** (owner,
   2026-10-14: "wolves should never strike before 6 seconds on screen").
   "Strike" is touching a shield or a knight: count to about 1.5 units from the
   knight's feet (the shield sits 1 unit out and the wolf's box reaches ~0.45
   ahead of its middle). Measure at the FASTEST the wolf can be in the tier's
   window — before the forest's wave 11 every black wolf comes out grey (3.0, not
   2.5) — and check its path against BOTH knights, not just its target.
   Tiers fixed to it that day: Sheep's Clothing II–VI (`secondsBeforeWolfAttack`
   4.75 / 5.5 / 4.5 / 4.5 / 4.5 — pacing only changes in whole zigzag legs, so
   pick a value inside a leg, not on its edge) and Neighbours III–VII
   (`courseDistance` 25 / 24 / 24 / 24 / 24). On 2026-10-20 the owner said
   Neighbours IV "feels fast": at 24 a grey wolf on the LOWER road struck at
   6.1s (the lower road is nearer the shields' centre, which sits half a unit
   above the knights' feet, so it is always the tighter one). IV–VII went to 25,
   matching III: grey 6.7s lower / 6.9s upper, black 8.0 / 8.2. Every other wolf wave already passed;
   the closest are Hold the Line (~8.9s) and Sheep's Clothing I (~6.4s). Run of
   the Mine II and III were then given 13s or more on the owner's ask (roads
   26 -> 42 and 22 -> 40); its tiers now run 19.6 / 14.3 / 13.8 / 13.4 / 12.6 /
   12.1 / 11.3s.
   In the editor and dev builds `EnemyWolf` logs a `[Wolves]` warning naming the
   wave whenever one strikes sooner (`EnemyWolf.MinSecondsOnScreen`), so play the
   tier once after authoring and read the log.

12. **Mine wolf counts climb steadily tier by tier** (owner, 2026-10-17: tiers I–III
   had too many wolves; halved, and the counts "should gradually scale up"; then
   the same day "reduce mobs on all Run of the Mine and Neighbours tiers by 33%",
   which cut every enemy, not just wolves). Only two Mine waves have wolves. A
   retune or a new wolf wave in the Mine should keep these ladders smooth, with no
   tier jumping far past the one below it. Tiers I–VII:

   | | I | II | III | IV | V | VI | VII |
   |---|---|---|---|---|---|---|---|
   | Neighbours crates | 3 | 3 | 4 | 5 | 5 | 6 | 7 |
   | Neighbours wolves | 1 | 2 | 3 | 4 | 5 | 7 | 9 |
   | Neighbours rats | 0 | 1 | 1 | 2 | 3 | 4 | 5 |
   | Neighbours ogres | 1 | 1 | 2 | 3 | 3 | 4 | 5 |
   | Run of the Mine wolves | 3 | 3 | 7 | 16 | 24 | 32 | 40 |
   | Run of the Mine relays | 2 | 2 | 2 | 4 | 6 | 8 | 8 |
   | Run of the Mine riders | 1 | 1 | 2 | 2 | 2 | 2 | 2 |
   | Run of the Mine ogres | 1 | 1 | 2 | 3 | 3 | 4 | 5 |

   Run of the Mine I was then given a second relay of two brown wolves (owner,
   2026-10-19), so tier I now matches tier II's three.

   **Rat ladders in Delivery and Neighbours** (owner, 2026-10-20: "too many rats
   in Delivery IV", halve it, then make the tiers below step down and the tiers
   above step up with no big jumps; the same for Neighbours IV). These count the
   FLANK rats only — the groups dropped in off the top and bottom edges. Rats in
   delivery carts and crates only exist if the cart reaches its flag, and the
   cart lists were left alone.

   | | I | II | III | IV | V | VI | VII |
   |---|---|---|---|---|---|---|---|
   | Delivery flank rats | 4 | 4 | 5 | 6 | 7 | 8 | 9 |
   | Neighbours flank rats | 0 | 1 | 1 | 2 | 3 | 4 | 5 |

   Delivery's groups keep their old arrival times (5 / 15 / 25 / 35s on IV–VII)
   and are pairs, with a three as the last group where the count is odd. A
   flank rat at x = 0 (the middle of an odd group, or a group of one) is given
   to the RIGHT knight, so pairs keep the knights even.

   **Up the Middle has a second ogre band** (`secondOgres`, owner, 2026-10-20):
   it sets off once every ogre of the first band is dead, its `firstAt` counted
   from that moment. Tier IV uses it — two plain ogres from the top corners,
   (±11.5, 6.5), each for the knight on its side — and lost its fifth rat/bat
   shift in the same change, because the wave went quiet once the first band
   was down. Tiers V–VII have no second band yet.

   **Bits and Pieces shifts per tier are 1 / 2 / 2 / 3 / 3 / 4 / 4** (owner,
   2026-10-20: one fewer gnome shift on every tier; the last, heaviest shift
   was the one taken). Ogres come with every shift, so each tier also lost one
   shift's worth of ogres.

   When a count was cut, the gap between releases (`crateInterval`,
   `wolfInterval`, `ogres.interval`) was widened ×1.5 so what is left is spread
   over the same stretch of the wave rather than finishing early. Run of the
   Mine's `projectileWindow` (20 / 26 / 21 / 51 / 74 / 96 / 96) was cut in
   step with its relays, because that wave does not end until the window runs
   out. Riders were cut by spelling the ring out cart by cart in
   `trafficPattern` and swapping the last riders for empties, so the kegs and
   the ring's spacing stay as they were.

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
  `NightHunt`, `ChooChoo`, `Delivery`, `PowderTrain`, `HotPotato`, `RushHour`,
  `BehindThePillars`, `WolfInvaders`, `BetweenTheMirrors`, `BackToBack`. `WolfPack.cs` and
  `Editor/WaveManagerEditor.cs` are empty stubs.

## Rock: RockVolley, and how a tier makes it harder (updated 2026-09-06)

`Assets/Scripts/Waves/RockVolley.cs` is the shared authoring type for shaft rock,
used by every Mine wave except the two exceptions above and by Night Hunt in the
forest. A wave holds a `List<RockVolley> volleys` and cycles it, either for a fixed
window (`RockVolley.WorkTheShafts(spawner, volleys, window, rockSpeed)`) or for as
long as an ambush is alive (see ChooChoo.WorkTheShafts).

One volley is a `shape` at a `knight`:

Worked example, Near and Far IV's flip (`salvo: 8`, `salvoStagger: 0.1`,
`spacing: 0.1`): eight rocks rake in from BELOW over 0.00–0.70s, a tenth of a
second of window, then eight from ABOVE over 0.80–1.50s.

| shape | int | what arrives |
|---|---|---|
| `Single` | 0 | one shot |
| `Pair` | 1 | two down the same shaft, `spacing` apart |
| `Split` | 2 | both knights at once |
| `Flip` | 3 | same knight, one side then the other. `fromBelow` picks which side it OPENS on — false is over-then-under, true is under-then-over |
| `Fan` | 4 | `count` shots walking outward around the knight's own half, capped at 80° |
| `Mirror` | 5 | both knights at once from opposite sides |
| `Scissors` | 6 | Mirror, then both knights flip |
| `Weave` | 7 | one knight, each shot from the far side of the dial from the last (1.2s floor) |
| `Spiral` | 8 | one knight, `count` shots per pass stepping steadily round his own side: from 30° past straight up toward the partner (straight down with `fromBelow`), outward, `spiralDegrees` in all (cap 240). `spiralPasses` 2+ comes back the same way. Steps never exceed 45°, gaps floor at 0.3s |
| `TwinSpiral` | 9 | a Spiral on both knights at once, mirror images, so the guards turn in opposite directions |

**`salvo` is the tier dial, and it is the answer to "this wave's rock does not get
harder at later tiers"** (owner, 2026-09-06). It is how many rocks go out TOGETHER
on every shot of the volley: the shape stays the shape and the windows stay the
windows, but a late-tier window has to be answered with the shield actually
covering it rather than nearly covering it.

**House ladder across a wave's first four tiers: `salvo: 1 / 3 / 5 / 8`.** Tier I stays a
single rock — the opening tier is where a player learns the shape, and a wall
teaches nothing. Mine tiers V / VI / VII go on to `8 / 9 / 10` (see "Seven tiers" below).

**A salvo is RAKED, never fired as a block.** `salvoStagger` (0.1s) is the gap
between one rock of the wall and the next, and it is released in order along the
spread so the wall sweeps across the knight from one end to the other. Five rocks
therefore take 0.4s to arrive and eight take 0.7s, which is something a guard can
be asked to hold a facing through. Fired on one frame they land on one frame — see
rule 3; the floor refuses 0 anyway.

The rocks are fanned `salvoSpread` (0.45 default) ACROSS the line of flight and all
aimed at the same knight, so they converge: eight rocks at seven units out arrive
inside about 25°, and the stock shield covers 60° (a one-unit bar on a one-unit
orbit). **One correct facing must always answer the whole salvo.** Widen the spread
until the ends fall outside the bar and the wave stops being a wave and becomes a
toll. Do not spread a salvo around the dial either — that is the same mistake the
Fan's 80° cap exists to prevent, and it walks rock into the other knight's shield.

**Every gap a shape authors is measured from the END of the wall before it**, not
from its start — `Fire` works in `beat = salvoBusy + spacing`. Measured the other
way, raising a tier's salvo count would quietly eat its own windows: eight rocks at
a tenth apiece is 0.7s of rock, so a 0.1s flip would have had the answer from above
starting while the shots from below were still leaving.

**Crossfire does not use any of this and must not be given it** (owner,
2026-09-06). Its mirrored files of rock are the wave, complete as authored, and a
salvo on top is a second copy of what it already does. Same reason it is exempt
from rule 7.

**`spacing` on a Flip is the other tier dial, and it can go very low.** THE SHIELD
TURNS INSTANTLY. `ShieldOrbit.rotationSpeed` is not a rate despite its name — it is
handed to `Quaternion.Slerp` as the interpolation fraction, which clamps to 1, so
at 180 and 60fps the guard is on the stick's angle the same frame the stick moved.
Crossing the whole dial costs the player's reaction and their thumb, nothing else.

So the Mine's tier III and IV flips run at **`spacing: 0.1`** (owner, 2026-09-06):
the salvo lands from one side, a tenth of a second to get the stick over, then the
answer from the other side. It is a fair ask because it is *telegraphed* — volley
order is fixed (rule 10), so the player learns the flip is coming and is turning
before the first salvo lands, rather than reacting to it cold.

Do NOT copy AboutFace's `MinimumFlipWindow` into a new wave. It reads
`RotationSpeed` as if it were degrees per second and derives 1.35s from it; that
figure is far more generous than the geometry needs, and it stands only because
those forest waves are playtested at it.

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

**Rock that runs per ambush must keep ONE clock across ambushes** (2026-10-19). Killing
a group stops its shafts coroutine but not the rock already scheduled or falling, so a
next group that fires the moment it starts lands its first volley on top of the last
one — Choo Choo III put a flip from above and a pair from below on the right knight
together, and every tier had the same hole. ChooChoo now carries `_shaftsRestUntil`
with `_shot` and waits it out. It also waits for the last volley to LAND: Weave and
the spirals leave from `WeaveRadius` (13) rather than the shaft mouths (7), so a shaft
volley timed off a weave's release lands on it (Choo Choo VII). Use
`RockVolley.FlightDistance` and `Spawner.RockFlightSpeed` for the difference.
`RockVolley.WorkTheShafts` got the same landing check on 2026-10-19, after Delivery
VII hit the right knight with a weave shot from below and the fan from above at the
same instant. A replay of every shared-loop asset found the same overlap in Cart
Loads VII, Up the Middle VII, Run of the Mine VII, Bat Cauldron VI–VII, Bat Slime
Boogie V–VI, Stalkers V and Rat Mischief V–VI; all cleared by the one change. A wave
that runs its own volley loop (not `WorkTheShafts`) still has to do this itself.

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
design — see rule 10. **In the Mine this is live from wave 1**, so budget for it in
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
waves above never play there, and these never play in the forest:

| Wave (class) | Pattern | Layout |
|---|---|---|
| Choo Choo | gnome carts in ambush shifts on a loop | Mine Circuit (40-unit lap) |
| Delivery | cargo carts racing to a drop flag | Mine Horseshoe (open route) |
| Powder Train | keg ring with gaps + enemies outside it | Mine Ring (32-unit lap) |
| Hot Potato | fire ogres only, each group sent together (4/5/6/7/7/8/9 per group); spiral rock | none |
| Rush Hour | gangs of plain ogres (8/12/16/22/26/30/36); straight shaft rock at I–II, fans/spirals/weaves from III | none |

**The Keep / Forgotten Fortress** (`Assets/Scripts/Maps/Pallid Keep.asset`, waves under
`Assets/Scripts/Waves/Castle/`). Its two gimmicks (owner, 2026-10-26): **mirrors** are portals
that KEEP a shot's heading (`MirrorRedirect.PreserveDirection`; going right in, it comes out the
right side of the twin at the same speed and angle), and **pillars** are solid blocks that stop
every projectile EXCEPT enemy rocks, which fly over them (owner, 2026-10-26). Pillars exist to make the mirrors worth using. Every castle wave should put its
targets where a straight shot cannot reach and give a pane pair that carries a shot there.

- A wave lays its board at the start: `spawner.Mirrors.Lay(layout)` and
  `spawner.Pillars.Lay(Castle_Pillar.prefab, rows)`. Both are cleared when the next wave begins.
- Knights stand at (±2, **-0.5**); the shield centre and a level shot are at y = 0. Rat body
  centre = position + 0.22, wolf + 0.38. Hang a pane at the height of the BODY you want hit.
- Tall panes all face Right in castle layouts, so a shot's height on the pane carries over.
- Pillar rows sit on odd x so there is a gap over each knight. Rocks ignore pillars AND mirror
  panes (owner, 2026-10-27: mirrors carry only the knights' shots), so any volley shape works.
- A volley's `origin` (RockOrigin) says where the shaft shapes (Single, Pair, Split, Flip, Mirror,
  Scissors) come from: Column (straight down/up his column, (±2, ±7)), Corner (the screen corner on
  his own side, (±12, ±7), ~12.2 units of flight) or Centre (top/bottom middle, (0, ±7), ~7.3 units,
  ~16° off vertical from the inside). Per volley, never per map: every wave type's rock is its own
  (owner, 2026-10-27). It replaced the bool `fromCorners` (FormerlySerializedAs keeps old assets).
  Each castle wave's file header carries its rock idea and a tier I–V ladder.
- Rats walk to the nearest gap when they chase (`PillarField.Route`); wolves do not route yet,
  so do not put wolves and pillars on the same board until they do.

| Wave (class) | Pattern | Tiers |
|---|---|---|
| Behind the Pillars | pillar rows at y ±2; rats patrol above/below; knight-height panes wrap to the rat rows | I (0–6), II (4–10) |
| Wolf Invaders | four wrap pairs down the side edges; a pack zigzags in from top-left and from bottom-right, held in order by `SpawnWolf(..., pathSpeed)` | I (0–6), II (4–10) |

The Mine has 13 wave types (2026-09-25; the forest has 12). The Overseer is at wave 20
(owner, 2026-10-20; it was 25) and does NOT end the run (`trueBossEndsRun: 0`), so a Mine run goes on to wave 30 and
past it like the forest does. `finalWaveNumber: 30` is only the level-select star.

**Seven tiers per wave (owner, 2026-09-25).** Every tiered wave has tiers I–VII, not
four. In the Mine they are spread evenly over the run: tier k (0-based) has
`unlockedAfterXWaves: 4k`, `lockedAfterXWaves: 4k+6`, and VII has no lock, i.e.
waves 1–6 / 5–10 / 9–14 / 13–18 / 17–22 / 21–26 / 25+. Tier I may start a wave or
two late when the wave is too heavy for wave 1 (Run of the Mine, Hot Potato, Rush
Hour). The Millstone is a single asset because it is the gate boss. With this
spread a simulated run reaches wave 30 without a repeat.

Tiers V–VII of the ten older Mine waves were generated from their tier IV by a
script. The per-wave settings grow (more riders, crates, wolves, relays, ogres,
bats, shorter gaps), and the rock goes: rests ×0.9 / ×0.8 / ×0.7 (never under
1.8s), salvo 8 / 9 / 10 with tighter rakes, Split → Mirror from V, the first Flip
→ Scissors from VI, the first Pair → a four-shot Weave (salvo 3) at VII.
Cart counts on LOOPING layouts were left at tier IV's, because a loop's spacing
is fixed by its length: Near and Far swaps empties for riders instead of adding
carts. All of this is a first pass, not playtested.

**Fire ogres wait for a gap in the rock (2026-09-25; replaces hand-timing the
rock between the fireballs).** Before each throw a fire ogre works out when its
fireball would land and holds the throw while anything else is due at that knight
within `clearance` (0.9s) either side. The check reads `IncomingLedger`: rocks and
straight fireballs register while they fly, `Spawner.SpawnProjectile` books a
delayed rock's landing the moment it is scheduled, and a decided throw is booked
before its wind-up. Rock is never held. The cost is that a wave's rock sets how
much fire gets through: a knight under a long spiral takes no fire at all. So
leave every knight long quiet stretches (Hot Potato's tiers keep each knight's
rock under ~45% of the cycle, with ~5s rests); simulated, 79–100% of throws still
go out. `OgreBand.interval: 0` sends a group on one frame, and
`OgreBand.throwStagger` (10 / group size) spreads their first throws round the
clock so the fire alternates knights instead of arriving as a volley.

**Guarded riders** (`GnomeWardRing`, added 2026-09-06): a gnome can come out
wearing a spinning ring of stone that eats arrows — one stone per arrow, and the
stone is back in its own slot a few seconds later. It is a TIMING problem, not a
durability one: the ring turns at a fixed rate, so the gaps between its stones
sweep past on a clock the player can read, and a shot released into a gap goes
through untouched. The tier dial is the stone count, because that is a dial over
the GAPS: on a 0.9-unit ring four stones leave gaps over a unit wide and eleven
leave a quarter of one. Choo Choo's ladder is 4 / 6 / 8 / 11, on the first gnome
of every shift. The ring never touches the knights — a barrier that dealt contact
damage would make eating it on the shield the way through, which is the one thing
rule 4 forbids.

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
