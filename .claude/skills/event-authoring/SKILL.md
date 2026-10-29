---
name: event-authoring
description: How between-wave events work in Two Knights — the High/Medium/Low priority ladder (quest scenes > Order trials > target range and cart sorting), the trigger for every existing event, and how to add a new one through BetweenWaveEvents and TrialRunner without breaking the one-event-per-gap rule.
---

# Two Knights — Between-Wave Event Authoring

A **between-wave event** is something that can happen in the gap after a wave is
survived, before the upgrade menu: an NPC scene, an Order trial, the forest's target
range, or the mine's cart sorting.
This skill covers how the game decides which one gets a gap, what triggers each
existing event, and how to add a new one.

Code: `Assets/Scripts/Trials/BetweenWaveEvents.cs` (the decision),
`TrialRunner.cs` (the shared framing), `OrderTrials.cs`, `TargetRange.cs` and `CartSorting.cs` (the
schedules). Design notes: `Docs/Design/order-trials.md`, `Docs/Design/target-range.md`.

---

## What happens in a gap, in order

All of this runs from `Spawner.RunWave` after the last enemy dies:

1. `WaveManager.WaveCompleted()` — the wave number moves on, and the wave's stats
   are committed.
2. A boss outcome (gate cleared, true boss killed) ends the run here. No events.
3. The "Wave Survived" panel.
4. **`BetweenWaveEvents.PlayOne`** — at most ONE event plays.
5. `Spawner.PlayQuestScenes` — every quest completion from the wave, then every
   offer owed on this map. This includes anything the event in step 4 just caused:
   a trial that finished an initiation, or the first target clear opening
   Target Practice.
6. The curtain closes, then the upgrade menu, then the next wave.

---

## The priority ladder (owner, 2026-09-26)

| Tier | Events | Rule |
|---|---|---|
| **High** | Quest scenes (offers and completions) | Always win. If one is owed, nothing else rolls for the gap. |
| **Medium** | Order trials — every initiation's trial | Play only when no quest scene is owed. |
| **Low** | The target range (forest), cart sorting (mine) | Plays only when no quest scene is owed AND no Order trial is going to play. |

How `PlayOne` applies it, top to bottom:

1. After a boss wave: nothing, whatever is due.
2. **High:** `QuestSceneQueue.HasPending` → the gap is the NPCs'. Nothing is played
   here; the scenes play at step 5 above.
3. *(Test Mode only)* a forced target pattern or cart phase plays, as practice. With
   nothing forced and a person at the controls, a list of everything due in this gap
   comes up instead and they pick one, or none — see Testing.
4. **Medium:** `OrderTrials.TryPick` → if a trial comes up, it plays and the gap is over.
5. **Low:** `TargetRange.TryPick` → if the range comes up, it plays. Otherwise
   `CartSorting.TryPick` → if the carts come up, they play.
6. Otherwise, a quiet gap.

What the ladder guarantees:

- **One event per gap, ever.** The first tier that plays ends the decision.
- **A tier only rolls its dice after every tier above it has passed.** A low event
  can never take a gap that a higher one would have had.
- "Would have had" means *actually going to play*. A Medium event that is due but
  closed (its quest not offered, or already done) does not block the range. Every
  Order trial is 100% when due today, so a due-and-open trial always blocks it. If a
  trial's chance ever drops below 100%, a missed roll lets the range have the gap.
  Confirm with the owner before changing that reading.

### Inside the Medium tier

Several trials can be due in the same gap. The one that has gone **longest without
playing** wins; one that has never played beats any that has, and an exact tie is
picked at random. The wait is counted in gaps across runs (`trials.clock`,
`trials.<key>.last`). A trial counts as played whether it was won or lost.

### Inside the Low tier

Two events, and they never meet: the target range is forest-only and cart sorting is
mine-only, so at most one of them is ever due in a gap. If a Low event is ever added
that shares a map with one of them, decide with the owner how the two share a gap
(longest-waiting like the trials is the obvious default) — do not just put one above
the other in `PlayOne`.

---

## Every existing event's trigger

The wave number is **the wave just survived** (`WaveManager.CompletedWavesCount`).
"After wave 5" means the gap between waves 5 and 6.

### Medium: Order trials (`OrderTrials.All`)

| Event | Order | Due after | Chance | Phases |
|---|---|---|---|---|
| Fire orbs | Ember | every odd wave from 3 (3, 5, 7…) | 100% | 3 |
| Ice orbs | Frigid | every odd wave from 3 (3, 5, 7…) | 100% | 3 |
| Venom orbs | Serpent | every even wave from 4 (4, 6, 8…) | 100% | 3 |
| Catch the Ninja | Shadow | every even wave from 4 (4, 6, 8…) | 100% | 3 |
| Mirrored bout | Dawn | every 6th wave (6, 12, 18…) | 100% | 1 |
| One-knight bout | Guardian | every 7th wave (7, 14, 21…) | 100% | 1 |

No trial before the gap after wave 3 (`OrderTrials.EarliestWave`, owner 2026-10-14):
fire and ice used to start after wave 1, venom and the ninja after wave 2.

Any map. A trial is only a candidate while it is **open**
(`OrderTrials.IsOpen`):

- its initiation quest is unlocked, which also means not the tutorial run, where
  every Order quest is frozen;
- the NPC has offered it (`QuestProgress.IsAnnounced`) — a trial never turns up
  before the offer that explains it;
- the quest is not complete, and a phase is left to play.

Passing a phase increments `trials.<key>`, which the initiation's one objective
reads. Losing replays the same phase next time.

### Low: the target range (`TargetRange`)

| | |
|---|---|
| Map | The forest only (`camp_fields`) |
| Due after | any wave from 5 on, odd or even (owner, 2026-10-13; was odd waves only) |
| Chance | 1 in 4, or 1 in 2 after a wait — see "The Low tier's roll" below (owner, 2026-10-13; it had no roll from 2026-09-26, and was 1 in 3 before that). It stays after Target Practice is done. |
| Needs | nothing else — no quest gate, so it also turns up in the tutorial run (after waves 5 to 9). There it still moves on through the patterns, because `TutorialStatGate` lets `targets.last_played` and `targets.batch` be written (owner, 2026-10-13), but a clear does not count toward Target Practice. |
| Which pattern | batches of five (owner, 2026-10-05): from the pattern last played, the next one not yet cleared in this batch, wrapping 5→1. Pass or fail, it moves on, so a failed pattern comes back later in the batch, not next time. When all 5 are cleared the batch resets and starts at 1. Never rolled. |

The five patterns (all targets appear at once; the clock starts when they can be hit):

| # | Targets | Time |
|---|---|---|
| 1 | one in each corner | 8 s |
| 2 | five along the top (not the corners), five along the bottom | 13.5 s |
| 3 | four corners, plus three along the top and three along the bottom | 12.5 s |
| 4 | four corners, each sliding clockwise to the next corner, arriving as time runs out | 8 s |
| 5 | twenty round the edge: seven along the top, seven along the bottom, three down each side | 28 s |

Targets pop in and out in white smoke (`NpcFx.SmokeCentredOn(..., white: true)`) and
blink for the last 3 seconds. Clear them all and the fanfare plays, then a health orb
crosses just above the knights (left to right) and a mana orb just below them (right
to left), inside sword reach. They are ordinary orbs with the ordinary payouts. The
gap waits until both have been taken or have flown off.

Progress stats: `targets.patterns` (different patterns ever cleared — the
Cartographer's **Target Practice** quest reads it) and `targets.cleared` (every
clear). `targets.pattern.<n>` (ever cleared), `targets.batch` (a bit per pattern
cleared in the current batch) and `targets.last_played` are bookkeeping written
with `PlayerStats.Set`, and are left out of `StatsDatabase` on purpose.

### The Low tier's roll (`LowEventRoll`)

The range and the carts roll the same way (owner, 2026-10-13):

- In a gap the event is due in, **1 in 4**.
- Once **4 or more waves** have gone by in this run without it, **1 in 2** until it
  plays. The wait is counted from the wave it last played after, or from the wave
  before its first wave (wave 4) while it has not played yet in the run.
- A new run, either event: 1 in 4 after waves 5, 6 and 7, then 1 in 2 from wave 8.
- Playing it, won or lost, starts the wait again.

The count is one static for both events, cleared by `LowEventRoll.BeginRun()` from
`Spawner.Start`. That works because a run has only one of them (forest or mine). A
Low event that shares a map with another needs a count of its own.

The roll is still made last, after the event has checked its map, its wave and its
art. Test Mode's event list skips it (`PickByHand`).

### Low: cart sorting (`CartSorting`)

| | |
|---|---|
| Map | The mine only (`mine`) |
| Due after | any wave from 5 on (owner, 2026-10-13; was from 3) |
| Chance | 1 in 4, or 1 in 2 after a wait — see "The Low tier's roll" below (owner, 2026-10-13; was 1 in 3) |
| Needs | `Resources/CartSortingKit.asset` with a track for the phase — built by Tools > Two Knights > Build Cart Sorting Assets |
| Which phase | batches, the same rule as the range's patterns. Five phases, each on its own track (`Mine Sorting 1`-`5`). |

Two forks of track drop in, one each side of the knights. Each is ordinary track with
one coloured corner piece where it splits (red on the left fork, blue on the right),
and a red and a blue flag at the ends of its two branches. A red lever and a blue
lever stand between the knights; hitting one turns the corner of its colour to the
other branch. Red and blue carts roll in and each must reach the flag of its own
colour. The first cart at the wrong flag loses it. Clearing it pays the same two orbs
as the range (`TargetRange.SendRewardOrbs`).

Unlike every other event, its track is laid by the mine's `RailNetwork` rather than
handed to `runner.Track`, so `TrialRunner.RunCarts` clears the rails itself when the
body returns. The switch is rail data: a fork's corner tile and the link off the end
of its feeder are each authored twice in the `RailLayout`, marked with a
`RailSwitchColor` and `whenThrown`; `RailNetwork.Throw(color)` swaps them.

Progress stats: `carts.phases` (different phases ever cleared — the Cartographer's
**Sorting Carts** quest reads it, out of `CartSorting.TotalPhases`) and
`carts.sorted` (every clear). `carts.phase.<n>`, `carts.batch` and
`carts.last_played` are bookkeeping, as for the range. Full design:
`Docs/Design/cart-sorting.md`.

### High: quest scenes

Out of scope here — see the `quest-and-equipment-authoring` skill for what opens
and completes quests. The only fact this ladder needs is what counts as "owed":
`QuestSceneQueue.HasPending` is true when the wave completed a quest, **or** when any
quest is unlocked but never offered and belongs to this map or to the camp. An owed
offer is read from the save, so it blocks the gap even if it opened runs ago.

---

## What every event shares (TrialRunner)

Order trials, the target range and cart sorting all play inside `TrialRunner`, which gives them
the same framing through `Open()` and `Close()`:

- **Before:** `Spawner.ClearFixturesForTrial()` clears the wave's leftovers (carts,
  rails, the castle's mirrors, stray orbs). Both knights' specials are locked, and
  the HUD fades out.
- **During:** the body runs on scaled time, so the pause menu stops it like a wave.
  An NPC intro, if any, runs frozen (`IsSpeaking`). `TrialRunner.IsRunning` is up.
  `EchoShotsHeld` is set for aim tests (the orb trials and the range), so knights
  fire only their own arrow: no shadow arrows, no shurikens. Cart sorting also
  sets `UpgradeHold.Held`, which takes every upgrade off the bow and the sword
  until its body ends (owner, 2026-10-13). A new upgrade is covered as long as
  the bow or sword looks it up with `UpgradeHold.Of<T>()` and not `GetComponent`.
- **After:** everything the body passed to `runner.Track` is destroyed. A loss plays
  `trial_lost` after a short beat; a pass plays `trial_won` and runs the event's
  `onPassed` coroutine (record the pass, pay the reward). Then specials unlock and
  the HUD comes back.
- **Abandoned** (pause → camp mid-event): everything just stops; the Spawner takes
  the same exit it takes for a quit mid-wave.

A body is an `IEnumerator` that takes the runner:

- Spawn things, and `runner.Track(go)` every one of them, or they survive a loss.
- Schedule later spawns with `runner.After(seconds, action)`; they are skipped once
  the event has stopped.
- Poll `runner.Stopped` every frame and `yield break` when it is true.
- Call `runner.Lose()` to fail. Returning without calling it is a **pass**, so a
  body that bails early on missing art must call `Lose()` first.

For something hit by a knight's shot, copy `NinjaTarget` / `RangeTarget`: a trigger
collider, `CollectibleOrb.CollectorFor(other)` to ask "was that a knight's arrow or
blade?", and `TrialOrb.SpendShot(other)` so one arrow counts once.

---

## Adding a new event

1. **Get the tier from the owner.** Do not guess it — it decides which gaps the event
   can ever have.
2. **Write a schedule class** next to `OrderTrials` / `TargetRange`:
   `IsDueAfter(wave)`, and a `TryPick(survivedWave, mapId, out …)` that returns false
   for anything not due, rolls the chance last, and logs with its own `[Tag]`. A
   Low event rolls with `LowEventRoll.Roll` and calls `LowEventRoll.NotePlayed`.
3. **Keep the content fixed.** Design pillar: the dice may decide *whether* an event
   happens, never what is in it. Choose variants by a rule (the target range's
   "lowest uncleared, then rotate"), never by `Random`.
4. **Write the body** as above, plus a `TrialRunner` entry point modelled on
   `PlayTargets`: `Create` → `Open` → body → `Close(logLabel, onPassed)` → `Finish`.
5. **Slot it into `BetweenWaveEvents.PlayOne`** at its tier's position. A new Medium
   event that is not an Order trial also needs a decision (from the owner) on how it
   competes with the trials' "longest waiting wins".
6. **Stats:** `PlayerStats.Increment` for anything a quest reads, `PlayerStats.Set`
   for bookkeeping, then `PlayerStats.Flush()` — gaps are outside the wave journal,
   so writes land at once. Name player-facing counters in `StatsDatabase`.
7. **Quests that the event opens:** gate on the event's own stat, and if the first
   occurrence is what opens the quest, make the objective `lifetime: true`, or that
   first occurrence will not count.
8. **Test Mode:** give the schedule class a `PeekDue` (what would play in this gap,
   with no roll and nothing written) and add a row for it in
   `BetweenWaveEvents.TestChoices`, so it shows in the tester's event list. Then
   add options to `TestModePanel.BuildTrialOptions` using
   `TestRunConfig.ForcedTrial` (a key) and `ForcedTrialPhase` (a number), and check
   for it in `PlayOne` **below** the High check, so a forced event still gives way to
   an NPC the way a real one would.
9. **Update the trigger table in this skill** and the event's design doc.

---

## Traps

- **Rewards paid in `onPassed` are not tracked.** `Close` has already cleared the
  board by then. Hold the gap until they are gone (the range waits on both orbs), or
  the curtain falls on them.
- **The three-orbs-per-wave cap does not cover gaps.** `WaveOrbBudget` is per wave;
  a between-wave reward is outside it by design.
- **Orb lanes are the orb's visual centre, not its transform.** The orb prefab's
  pivot is at the bottom of the art. Subtract the collider offset (see
  `TargetRange.SendOrb`, `TrialOrb.Spawn`) or the lane rides a quarter unit high.
- **`CollectibleOrb.Initialize(from, to)` with `from == to` destroys the orb on its
  first frame** — it thinks it has arrived. Orbs always travel.
- **NPC smoke is lifted to cover a standing figure.** `NpcFx.Smoke` takes FEET;
  anything hanging in the air uses `NpcFx.SmokeCentredOn`.
- **Art for code-built objects goes in `Resources/Trials/`** and is loaded with
  `Resources.LoadAll<Sprite>`, because an `.aseprite` sprite reference cannot be
  written by hand. A new `.aseprite` imports at PPU 100 unless its `.meta` says 32.
- **Map ids, not names.** The forest is `camp_fields` (`QuestBuild.Forest`).

## Reading the log

Every gap writes at least one line:

- `[Events] after wave N: …` — boss gap, an NPC scene took the gap, or nothing played.
- `[Trials] after wave N: …` — which trials were due, closed, missed their roll, or won
  the gap; then `[Trials] <key> phase n: PASSED / lost`.
- `[Targets] after wave N: …` — missed its roll, or which pattern plays, with the
  chance it had (25% or 50%); then `[Targets] pattern n: PASSED / lost`.
- `[Carts] after wave N: …` — missed its roll, or which phase plays, with the chance
  it had; then `[Carts] phase n: PASSED / lost`.

## Testing

**The event list** (2026-10-09). In a test run with nothing forced, after "Wave
Survived" a list comes up of every event that is due in that gap — each open Order
trial at its next phase, the target range, the carts — plus "No event" (also B). The
dice and the ladder are both set aside: the range and the carts need no roll, and
either can be picked in a gap a trial was also due in. What is picked plays for real and is
recorded like any run's. The list does not come up when nothing is due, after a boss,
when an NPC scene is owed, when an event is forced (below), or when
`TestRunConfig.AutoPickWave` is set — that last one is what keeps a run driven over
the bridge from hanging on it. The pause menu's **Skip Wave** is the quick way to
reach a gap. Code: `BetweenWaveEvents.TestChoices`, `TestWavePicker.ShowEvents`.

Test Mode row **Force event between waves**: Off / each trial phase / Targets 1–5 /
Carts 1-5.
Every gap of that test run plays the choice as practice (it records nothing, and the
range still pays its orbs) — this is the way to play an event that is NOT due.
Over the Unity bridge:
`TestRunConfig.ForcedTrial = "targets"; TestRunConfig.ForcedTrialPhase = 4;`
See the `test-mode-validation` skill for driving a run.

---

Related skills: `quest-and-equipment-authoring` (the High tier's quests),
`wave-authoring` (what the gap sits between), `test-mode-validation`,
`sprite-authoring` (art for event objects), `sfx-authoring`.
