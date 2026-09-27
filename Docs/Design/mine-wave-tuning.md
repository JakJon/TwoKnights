# The Mine — Wave Tuning & Difficulty Tiers

*Drafted 2026-08-04, and BUILT the same day — all twelve assets exist and are in The
Mine's pool. Covers Choo Choo, Delivery and Powder Train; the Near and Far family is
authored separately and only referenced here for convention.*

**Naming and windows follow Near and Far**: `<Family> N` for the asset, a shared
unnumbered `waveName` so the tier never shows to the player, `weight` 1000, and the
sliding windows `isUnlocked: 0` with `unlockedAfterXWaves` / `lockedAfterXWaves` of
**0/7 → 4/11 → 8/15 → 12/-1**. Tier 1 was renamed (`Choo Choo` → `Choo Choo 1`, etc.,
guids preserved) and given the 0/7 window it was missing — left unbounded it would have
kept surfacing at wave 20 and defeated the whole tiering.

## 1. Ambushes (shipped)

An **ambush** is a group of enemies inside a wave that comes out at roughly the same
time. It is the wave's pacing unit: release a group, wait until the players have killed
it, release the next. Wave length is then set by how fast the knights shoot rather than
by a stopwatch — which matters enormously on a rail map, because on a looping track
nothing ever leaves, so a timed wave either releases into a ring that is already full or
leaves the knights standing in an empty one.

`BaseWave` now carries the tracker, deliberately separate from wave completion:

| | `AreAllEnemiesDead()` | `IsAmbushClear()` |
|---|---|---|
| Scope | everything the wave ever spawned | just the current group |
| Projectiles | counted | ignored |
| Gate | `MarkSpawningComplete()` | `MarkAmbushReleased()` |
| Means | the wave ends now | send the next group |

```csharp
BeginAmbush();                       // enemies registering from here belong to this group
/* ...release the group... */
MarkAmbushReleased();                // REQUIRED, or the group can never read as clear
yield return WaitForAmbushClear();   // holds until every member is dead
```

**No timeout, by decision.** The next ambush is a reward for clearing the last one; a
wave that sends it anyway after N seconds is a schedule in disguise. The corollary is a
standing obligation on every wave that uses ambushes: **keep pressure running while the
group is alive.** Choo Choo keeps the shafts firing for exactly this reason — a shift
left alive is a shift that keeps hitting you, so stalling costs health rather than
buying a rest.

Three things fall out of the design that the tier tables below depend on:

- Enemies with `TracksWaveCompletion => false` — empty carts, kegs, gnome wrecks — never
  join a group. A ring still full of rolling iron reads as clear. **Only gnomes gate a
  Choo Choo shift.**
- A group of nothing but scenery clears the instant it is released. Intentional: it
  cannot stall.
- Whatever registers while a group is open joins it, summons and delivered cargo
  included. On Delivery this is free and exactly right — see §4.

## 2. The arithmetic every rail tier obeys

**Lap lengths** (measured, not guessed — `RailNetwork.TryMeasureLoop` derives these):

| Layout | Route | At 2.4 u/s |
|---|---|---|
| Mine Circuit (Choo Choo) | 40u loop — 12u straights ×2, 4u shafts ×2, 2u per elbow ×4 | 16.7 s/lap |
| Mine Ring (Powder Train) | 32u loop, 32 one-cell slots | 13.3 s/lap |
| Mine Horseshoe (Delivery) | 27u open route, mouth → drop flag | 11.3 s race window |
| Mine Millstone, outer (The Millstone) | 52u loop, corners (±9, ±4), 52 one-cell slots | 21.7 s/lap |
| Mine Millstone, inner (The Millstone) | 44u loop, corners (±8, ±3), 44 one-cell slots | 18.3 s/lap |

**Release interval on a loop is a permanent spacing.** Every cart runs at one speed, so
whatever gap the interval opens is the gap they keep forever. Even spacing = `lap ÷
carts ÷ speed`. Cluster spacing (what an ambush wants) = 3–4 units apart, i.e.
`interval ≈ 3.8 ÷ speed`.

**Releasing into a busy ring is safe by construction.** Carts are released a lead-in
behind the mouth (`MineCart.leadIn` = 6u) while returning traffic re-enters 1u behind
it, so a new cart is always ≥5u clear of the traffic ahead and — same speed — stays
that way. No overlap is possible; only density.

**The ring budget.** A dead gnome leaves a 20 HP wreck circling forever, so the ring a
late shift arrives into is carrying every gnome the earlier shifts lost:

```
peak ring  =  gnomes killed so far  +  kegs still unshot  +  carts in the arriving shift
```

At 40u, 8 carts sit 5u apart (a spaced chain), 10 sit 4u apart, 13 sit 3u apart (a
wall). Budget: **≤8 for tiers 1–2, ≤10 for tiers 3–4** as a deliberate late-game
texture. This is the binding constraint on Choo Choo — see the finding in §3.

**TTK reference.** Base arrow 10 dmg / 1.5s = 6.7 dps per knight. Gnome cart 20 HP =
2 arrows. Keg 10 HP = 1 arrow. Empty cart / wreck 20 HP. Delivery cart 20 HP.

**Orbs** (added 2026-08-04). Every Mine wave now carries one, alternating by tier:
**mana on tiers 1 and 3, health on tiers 2 and 4** (owner's schedule). An orb is
**collected by shooting it**, not by touching it — so it costs an arrow and a shield
facing at a moment the wave has already made busy. It travels at 5 u/s and destroys
itself at the far point, so a full-width crossing is 24 units ≈ 4.8 seconds of
availability, and where the line runs decides how real the trade is:

| Wave | Line | Why there |
|---|---|---|
| Delivery | across the **bottom of the view**, y = -4.5 | the horseshoe is overhead, so taking one means turning a knight all the way down and off the route he is meant to be watching |
| Choo Choo | **inside** the loop, y = -1.5 | outside the ring an orb is only shootable through a gap in the traffic — a harsher trade than this wave is asking |
| Powder Train | **inside** the ring, y = -1.5 | same, and more so: outside the fence the orb becomes a second puzzle competing with the one the wave already is |

Orbs never register with wave tracking, so one nobody shot cannot hold a wave open — it
simply leaves.

**The stronger enemies are ungated in the Mine** (owner, 2026-08-04; fixed in code the
same day). Dark bats, black wolves and brown/black rats are not "elites" — they are
ordinary enemies that the Camp Fields chooses to stagger in behind the rat king. In the
Mine they are simply what lives down there, from wave one, and the Mine will stagger in
its own additions later on its own schedule.

The threshold now lives on the map (`MapDefinition.strongerEnemiesFromWave`: `-1`
derives it from the gate boss, the Camp Fields rule; The Mine sets `1`). Previously it
was derived from `gateBossWaveNumber` for every map, which silently pushed the Mine's
stronger enemies out to wave 16 because the Mine's gate sits later. Tier tables below
assume they are real from wave 1.

**Dark bats are now live in tier 1.** Every 4th `SpawnBat` call of a wave substitutes
`Enemy_Bat_Dark` (`Spawner.darkBatInterval`, counter resets per wave), so Mine waves get
sonar-and-Confuse from wave 1 where the forest's tier-1 waves never did. Counts fall out
of the rotations: Powder Train alternates rat/bat, so T1's 12 arrivals give 6 bats = 1
dark bat, rising to 2 at T3/T4. Delivery gets **none** at any tier — its bat cargo tops
out at 2 carts, short of the 4th call. If dark bats belong in Delivery, T4 needs 4+ bat
cargo carts.

## 3. Choo Choo — gnome shifts on the circuit

### Tunable surface (as reworked)

| Field | Scope | What it really controls |
|---|---|---|
| `railLayout` | wave | which loop; changes every number in §2 |
| `entryRun` | wave | the single mouth all carts enter from |
| `cartSpeed` | wave | lap time, and therefore how often a shift comes back round. One speed for the whole wave on purpose — mixed speeds close the gaps the interval opened |
| `ambushes[]` | wave | the shift list; each entry is one gated group |
| ↳ `label` | ambush | authoring/dev-log only |
| ↳ `carts[]` | ambush | composition **and** the gate — only gnomes hold the shift open |
| ↳ `leadIn` | ambush | the breath after the last shift cleared (opening shift: measured from the track landing) |
| ↳ `releaseInterval` | ambush | permanent spacing within the cluster |
| ↳ `projectileInterval` | ambush | shaft tempo while the shift is alive. **0 makes stalling free — use sparingly** |
| `orbs` | wave | one mana/health orb run, crossing inside the loop |

### Finding: the wave's ceiling is its own wrecks

Because every gnome killed leaves a permanent 20 HP cart on the loop, the ring budget
caps a Choo Choo asset at roughly **8 gnomes total**, whatever tier it is. Difficulty
therefore scales through speed, cluster density, shaft tempo and composition — not
through body count. Tiers 2 and 3 below are within 2 gnomes of tier 1 and are still
markedly harder.

### Finding: gnome throw rate is a prefab constant, not a wave knob

`EnemyGnomeCart` gates throws on a **wave-wide shared cooldown per kind** (10s on both
prefabs), so putting two bomb gnomes in a shift does *not* double the bomb rate — it
doubles the HP and the blocking, and nothing else. Composition changes *which* threats
appear, never how often.

> **Recommended addition (~10 lines, not built):** `throwCooldownOverride` and
> `armDelayOverride` on the ambush entry, stamped onto each gnome as it is released
> (0 = leave the prefab alone). Without it there is no way to make late tiers throw
> harder short of authoring per-tier gnome prefabs. Tables below assume the prefab
> defaults (10s cooldown; bomb arms at 8s, pickaxe at 5s).

### The four assets

Composition shorthand: **P** = pickaxe gnome, **B** = bomb gnome, **K** = keg.

| | **Choo Choo 1** | **Choo Choo 2** | **Choo Choo 3** | **Choo Choo 4** |
|---|---|---|---|---|
| Window (wave numbers) | 1–7 | 5–11 | 9–15 | 13+ |
| `isUnlocked` / `unlockedAfter` / `lockedAfter` | 0 / 0 / 7 | 0 / 4 / 11 | 0 / 8 / 15 | 0 / 12 / -1 |
| `cartSpeed` | 2.4 | 2.6 | 2.9 | 3.1 |
| lap time | 16.7s | 15.4s | 13.8s | 12.9s |
| Shift 1 | P, K | P, B | P, B | P, B |
| Shift 2 | B, K, P | B, K, P | B, P | B, K, P |
| Shift 3 | P, B | P, B | P, K, B | P, B |
| Shift 4 | — | — | B, P | B, K, P |
| gnomes total | 5 | 6 | 8 | 8 |
| `leadIn` per shift | 0.75 / 1.5 / 1.5 | 0.75 / 1.2 / 1.2 | 0.6 / 1.0 / 1.0 / 1.0 | 0.5 / 0.7 / 0.7 / 0.7 |
| `releaseInterval` | 1.6 / 1.6 / 1.4 | 1.5 / 1.5 / 1.4 | 1.3 all | 1.2 all |
| cluster spacing | 3.8u | 3.9u | 3.8u | 3.7u |
| `projectileInterval` | 6 / 5.5 / 5 | 5 / 4.5 / 4 | 4.5 / 4 / 3.5 / 3.5 | 3.5 / 3 / 3 / 2.5 |
| peak ring | 7 | 7 | 9 | 10 |
| orb (inside the loop, y -1.5) | mana ×1 @10s | health ×1 @10s | mana ×2 @9s/+12s | health ×2 @8s/+12s |

Reading the curve: cluster spacing is held near-constant on purpose — a shift stays
readable as one group at every tier — while the ring turns faster (16.7s → 12.9s per
lap), the gap between shifts shrinks (1.5s → 0.7s), and the shafts roughly double their
tempo (6s → 2.5s). T1 opens with a single gnome so the loop can be learned on one
target; T4 opens with both kinds at once.

Kegs are the pressure valve and are placed mid-shift at every tier: they are the only
thing that removes accumulated iron (15 blast damage clips a 20 HP wreck to 5), and the
only cart whose timing the player owns.

## 4. Delivery — the race to the flag

### Tunable surface (current)

| Field | What it really controls |
|---|---|
| `railLayout` | route length = the race window; and where the flags are |
| `cartOrder[]` | which cargo, in what order. Empty entries leave a gap |
| `entryRun` | mouth of the horseshoe |
| `firstCartDelay` | quiet before the first cart |
| `cartInterval` | how much of the route is occupied at once — the real density knob |
| `cartSpeed` | **the core tension**: race window = 27u ÷ speed |
| `volleys[]` | **the shaft programme** — see below. Cycled in order until the window elapses |
| `projectileWindow` | how long volleys keep being issued. Must cover the wave or the back half is free |
| `orbs` | one mana/health orb run across the bottom of the view |

### The shaft programme (reworked 2026-08-04 — the old pattern was too easy)

The shafts used to alternate one rock onto each knight on a fixed interval: a
metronome, learnable in two beats. They are now an authored cycle of **volleys**, each
with its own shape and its own trailing window.

The rocks travel at **1 u/s** and a shaft is 7 units up, so every shot is *seven seconds
in the air and visible for all of it*. That is what makes intricate patterns safe here —
nothing is a reflex test. The pattern is not deciding whether a knight can react; it is
deciding **when a knight is allowed to look away from the track.** Flight time is
identical for every shape (all shots sit at the same 7-unit radius), so the arrival
order is the firing order, shifted by a constant.

| Shape | What it does | What it costs the player |
|---|---|---|
| `Single` | one rock, one knight | a beat of guard |
| `Pair` | two down the same shaft | the second lands while the first is still being answered |
| `Split` | both knights at once | nobody is shooting anything during it |
| `Flip` | same knight, above then below | the guard crosses the whole dial |
| `Fan` | a spread walking outward around the knight's own half | sustained tracking, one knight pinned |

**The fairness knob is `restAfter`** — quiet after the volley's last shot, taken from
when the volley *finishes* spawning rather than when it starts, so a five-rock fan
cannot silently eat its own window. A delivery cart is 20 HP: two arrows, 1.5s apart.
**So 3.0s is the floor** — under that, the window is mathematically too short for anyone
to kill a cart at base fire rate. Windows below the floor are legitimate as an
occasional chip-only beat, never as the whole cycle.

Programmes (shape/knight, `spacing`, then the window in brackets):

| | Cycle | Length |
|---|---|---|
| **T1** | Single L (3.5) → Single R (3.5) → Pair L 1.2 (4.0) → Flip R 1.4 (4.0) | 17.6s |
| **T2** | Pair L 1.1 (3.2) → Flip R 1.3 (3.5) → Split (3.0) → Fan L ×3 @55° 0.9 (3.5) → Single R *below* (3.2) | 20.6s |
| **T3** | Fan L ×4 @65° 0.8 (3.2) → Split *below* (3.0) → Flip R 1.1 (3.2) → Pair R *below* 1.0 (3.0) → Fan R ×4 @65° 0.8 (3.2) → Single L (2.8) | 25.3s |
| **T4** | Fan L ×5 @75° 0.7 (3.0) → Flip L 0.9 (2.8) → Split (2.8) → Fan R ×5 @75° 0.7 (3.0) → Pair L *below* 0.8 (2.8) → Flip R 0.9 (3.0) | 25.6s |

T1 teaches the five shapes on wide windows. T2 — the tier that was too easy — goes from
4 rocks per cycle to 8, brings in `Split` and `Fan` and the first shots from below, and
tightens windows to 3.0–3.5s: still a clean cart kill in every gap, but no gap with
anything spare in it. T3 and T4 widen the fans and push most windows to the 2.8–3.0
floor, so the knights are choosing which cart to give up rather than clearing them all.

### Notes and limits

- **Cargo is a prefab constant** (`EnemyDeliveryCart.cargo`), and only two cart prefabs
  exist: `Enemy_DeliveryCart_Rat` (grey) and `_Bat`. Slime cargo, brown/black rats, and
  `passengers > 1` all need new prefab variants — trivial YAML clones with one changed
  field, but they must exist before T3/T4 can be authored as written.
- **The "two flags" plan did not survive authoring, and the fix is better.** A cart
  carries `passengers: 1`, and it retires at the FIRST flag it reaches — so a second
  flag further down the route would never be served by anything. Two flags only means
  something once multi-passenger cart prefabs exist. What tiers 3 and 4 use instead is
  **Mine Horseshoe Short**: the same horseshoe with its single flag moved up the line to
  run 1 / distanceAlong 9 — directly over the RIGHT knight, at the far end of the
  crossing. That is the same lever (an earlier deadline) with none of the dead wiring,
  and the drop lands on a knight's head, which reads as failure far better than a flag
  in the corner did.

| Layout | Mouth → flag | At 2.5 u/s | At 2.8 | At 3.1 |
|---|---|---|---|---|
| Mine Horseshoe | 27u | 10.8s | 9.6s | 8.7s |
| Mine Horseshoe Short | 16u | 6.4s | 5.7s | 5.2s |

  A cart is 20 HP — two arrows, 3.0s of one knight's fire — so tier 4's 5.2s is roughly
  *one knight, committed, with nothing to spare*. That is the intended squeeze: at a
  2.4s cart interval they arrive faster than one bow can clear, so both knights have to
  work the route or start choosing which delivery to concede.
- **Ambush candidate.** Delivery converts to convoys almost for free, and the semantics
  are perfect: a delivery cart unregisters when it retires empty, and cargo it dropped
  registers into the same open group — so "the convoy is dealt with" means *the carts
  are gone AND whatever got through is dead*. Recommended, not built. The tables below
  are authored against the current fields and work either way.

### The four assets

| | **Delivery 1** | **Delivery 2** | **Delivery 3** | **Delivery 4** |
|---|---|---|---|---|
| Window (wave numbers) | 1–7 | 5–11 | 9–15 | 13+ |
| `isUnlocked` / `unlockedAfter` / `lockedAfter` | 0 / 0 / 7 | 0 / 4 / 11 | 0 / 8 / 15 | 0 / 12 / -1 |
| `railLayout` | Horseshoe | Horseshoe | Horseshoe **Short** | Horseshoe **Short** |
| `cartOrder[]` | Rat, Bat, Rat, Rat | Rat, Bat, Rat, Bat, Rat | Bat, Rat, Rat, Bat, Rat, Rat | Rat, Rat, Bat, Rat, Bat, Rat, Rat |
| carts | 4 | 5 | 6 | 7 |
| `firstCartDelay` | 0.5 | 0.5 | 0.5 | 0.4 |
| `cartInterval` | 4.0 | 3.5 | 3.0 | 2.4 |
| `cartSpeed` | 2.4 | 2.5 | 2.8 | 3.1 |
| race window | 11.3s | 10.8s | **5.7s** | **5.2s** |
| carts in transit at once | ~2 | ~3 | ~3 | ~4 |
| `projectileInterval` | 6 | 5 | 4.5 | 4 |
| `projectileWindow` | 26 | 30 | 32 | 36 |
| volley programme | T1 above | T2 above | T3 above | T4 above |
| orb (bottom of view, y -4.5) | mana ×1 @9s | health ×1 @9s | mana ×2 @8s/+12s | health ×2 @8s/+11s |

The curve is the window closing: 12.3s to stop a cart at T1, 8.7s at T4 — and at T3/T4
the first flag arrives at roughly *half* the route, so a cart that gets past the
crossing has already delivered something. Cart count rises gently; what actually breaks
the wave open is that at T4 four carts are in transit at once against 4-second shaft
pressure, and one knight cannot cover both.

## 5. Powder Train — the ring with gaps

### Tunable surface (current)

| Field | What it really controls |
|---|---|
| `railLayout` | the ring; its circumference sets the slot count (32 on Mine Ring) |
| `cartPattern[]` | **the signature knob.** One entry per slot, repeated around the loop. `{fileID: 0}` entries are the gaps you shoot through. Length must divide 32 — use 8 or 16 |
| `repeats` | fallback only; on a real loop the count is measured off the track |
| `firstCartDelay` | quiet before the train sets off |
| `slotInterval` | leave 0 — derived as `cell ÷ speed`, which is the only way the ring closes |
| `cartSpeed` | how fast gaps sweep past a firing line |
| `enemyInterval` / `enemyCount` | the fight outside the fence. `enemyCount` is what gates the wave |
| `enemyStartDelay` | held until the ring has closed, so the fence is met before the fight |
| `ratType` | grey / brown / black — all real from wave 1 in the Mine |
| `orbs` | one mana/health orb run, crossing inside the ring |

### Notes

- **Gap fraction is the difficulty.** 2 gaps per 8 slots = 25% of the ring shootable;
  2 per 16 = 12.5%. The tiers below keep gaps **paired** — a 2-unit window that arrives
  less and less often as the pattern lengthens.
- A second lever, unused below and available if the curve needs another notch:
  *splitting* a pair. Two adjacent gaps are one 2u window every pattern length; the same
  two spread apart are two 1u windows arriving twice as often. That trades patience for
  precision rather than raising difficulty outright, so it is a flavour change — worth
  reaching for only if paired gaps start feeling like a memorised metronome.
- Keg vs empty is the other composition axis. Kegs are payoff (one arrow clears a
  segment, at the risk of 15 damage to a knight standing under it); empties are pure
  wall. Shifting the mix toward empties raises difficulty without touching the gaps.

### The four assets

Pattern shorthand: **E** = empty cart, **K** = keg, **·** = gap.

| | **Powder Train 1** | **Powder Train 2** | **Powder Train 3** | **Powder Train 4** |
|---|---|---|---|---|
| Window (wave numbers) | 1–7 | 5–11 | 9–15 | 13+ |
| `isUnlocked` / `unlockedAfter` / `lockedAfter` | 0 / 0 / 7 | 0 / 4 / 11 | 0 / 8 / 15 | 0 / 12 / -1 |
| `cartPattern[]` | `E K K · · · · ·` | `E K K E K E · · E K E · · · · ·` | `E K K E K E K E K E K K E E · ·` | `E K E E K E E K E E K E E E · ·` |
| pattern length | 8 (×4) | 16 (×2) | 16 (×2) | 16 (×2) |
| gaps on the ring | 20 of 32 (62.5%) | 14 of 32 (44%) | 4 of 32 (12.5%, paired) | 4 of 32 (12.5%, paired) |
| kegs : empties | 8 : 4 | 8 : 10 | 14 : 14 | 8 : 20 |
| `cartSpeed` | 2.4 | 2.4 | 2.6 | 2.9 |
| gap comes round every | 3.3s | 3.3s | 6.2s | 5.5s |
| `enemyCount` | 12 | 14 | 17 | 20 |
| `enemyInterval` | 3.0 | 2.8 | 2.4 | 2.1 |
| `enemyStartDelay` | 6 | 5.5 | 5 | 4 |
| `ratType` | grey | grey | brown | black |
| `firstCartDelay` / `slotInterval` | 0.5 / 0 | 0.5 / 0 | 0.5 / 0 | 0.4 / 0 |
| orb (inside the ring, y -1.5) | mana ×1 @12s | health ×1 @12s | mana ×2 @11s/+12s | health ×2 @10s/+12s |

**Thinned 2026-09-25 (owner's call):** T1 runs half its old carts (24 → 12 a lap) and T2 a quarter fewer (24 → 18). T2's 4.5 carts per 8 slots needed the 16-slot pattern. The old patterns were `E K K E K K · ·` and `E K K E K E · ·`, both 8 gaps a lap, so the step into T3 (4 gaps) is now much steeper than the paragraph below describes.

The step change is at T3, where the gap count halves: the same 2-unit window, but it now
comes round every 6.2 seconds instead of every 3.3, so a knight who misses it waits
twice as long with the fence still up. T4 then thins the powder as well
— 18 empties to 10 kegs means most of the fence gives nothing back — while the fight
outside doubles from T1 (10 arrivals at 3.2s to 20 at 2.1s) and goes black-rat
throughout.

## 6. The gate — The Millstone (added 2026-08-09)

Wave **10**, and the map's first finish line. Two closed rings on one layout (`Mine
Millstone` — a RailNetwork lays one layout at a time, so both rings are eight runs in a
single asset), turning **opposite ways**, both packed nose to tail with no gap anywhere:

| | Inner ring | Outer ring |
|---|---|---|
| Lap | 44 slots, corners (±8, ±3) | 52 slots, corners (±9, ±4) |
| Turns | anticlockwise (top run west) | clockwise (top run east) |
| Pattern | 1 keg + 8 empties | 1 empty + 4 gnomes (pickaxe/bomb alternating) |
| Content | 5 kegs, 39 empties | 41 riders, 11 empties |
| Laid | first, and closed before the shift sets off | second |

The rings are one unit apart, which is exactly a cart: a cart's collider is 1u tall and
sits above its rail, so inner carts fill y 3→4 and outer carts 4→5 with no overlap and
no daylight. **Every shot the knights own stops in the inner ring** until they make a
hole, and holes cost arrows spent on iron worth nothing (20 HP an empty).

**The powder is the way through.** A keg is 10 HP — one arrow — takes its own slot out of
the wall, and puts 15 into everything within 2.8u. At one unit of ring separation that is
five or six riders going past on the outside: the best trade in the mine, and there are
exactly five kegs. It is also the worst trade taken carelessly, because the inner ring
runs at Mine Circuit's height and a keg popped over a knight's head catches the knight
for the same 15 (`EnemyKegCart`'s radius is measured for precisely this).

Counter-rotation is what stops a hole from being a permanent answer: a firing line the
knights burn open sweeps one way while the shift it uncovered sweeps the other.

Numbers that matter: **820 HP of riders** (41 × 20, against the rat king's 750), one
shift-wide throw gate per gnome kind so 41 riders throw no faster than six do, shafts
working a four-position rotation every 6s from the moment the track lands, and two health
orbs across the inner track at y -1.5. Fill time is ~44s end to end (18s inner, 21s
outer), which the shafts and the arming riders cover.

**The wheel spins up as the shift thins.** Three gears, authored on the asset as a ladder
and never wound back: **1.5×** once half the riders that came out are dead, **2×** at three
left, **3×** at the last one. "Half" is resolved once against the shift that actually
rolled — counted at release, not read off the pattern — while three and one are absolute.
A blast of the mine whistle (`cart_whistle.wav`, `AudioManager.cartWhistle`) marks each
change, and only that: it is the one warning that the ring the players just learned to
read has stopped being the ring they learned.

The gear goes to the **whole track**, not to the riders alone, and that is not a shortcut.
Both rings are packed nose to tail, so a subset running faster would not be faster — it
would shoulder the iron in front of it, and what the player would see is a ring grinding
against itself. `RailNetwork.SpeedScale` is therefore a property of the track: it changes
gear on every cart riding, puts carts released afterwards straight into that gear, and
resets to 1 with `ClearAll`. Uniform speed is also what keeps the spacing exact — every
cart scales together, so a packed ring at 3× is still packed.

The ladder only starts watching once the shift is fully out. Reading the rider count
during the fill would see the first cart of a forty-strong shift as "one gnome left" and
put the wheel into top gear before the fight began.

Neither ring divides its pattern exactly (52 ÷ 5, 44 ÷ 9), so one block round the back of
each runs short — a lone rider outside, a keg gap of 8 instead of 9 inside. Deliberate;
the alternative is contorting the rectangle off-centre to make the arithmetic close.

**The Mine's gate is a door, not a finish line**, and that is two flags rather than one
because they are two questions. Both are new on `MapDefinition` and both default to the
Camp Fields' answer, so the forest is untouched:

| Flag | Camp Fields | The Mine | What it decides |
|---|---|---|---|
| `gateBossRepeats` | true | **0** | whether the gate owns its wave number again on later runs |
| `gateBossEndsRun` | true | **0** | whether the first kill ends the run in victory |

So the Millstone is forced at wave 10 on every run until it is beaten once; the run then
**walks straight through it and carries on**, and from that point it is an ordinary
setlist wave — weight 1000, `unlockedAfterXWaves: 9` — that can come up any time from wave
10 on. The clear is recorded and paid for either way: `MarkGateCleared` saves on the spot,
so `maps.mine.gate_cleared` reaches the quest that was waiting on it even if the run goes
on and ends in a death forty waves later.

The forest keeps the old rule — the rat king is a finish line that stands at wave 10
forever, and beating him again is how a later run reaches the deep waves.

Consequence worth knowing: nothing on screen currently marks the moment. The victory
banner was the acknowledgement, and a door does not raise one. If the first clear wants a
beat of its own it needs its own cue, not the run ending.

## 7. Open items

1. **The Mine has no true boss.** The gate landed at wave 10 (§6); `trueBoss` (wave 25)
   is still null on `The Mine.asset`, so a run past the gate still has no finish line.
2. **These three are the Mine's first families, not its final set** (owner, 2026-08-04).
   Near and Far has since landed, so the pool is 4 families × 4 tiers = 16 assets, and
   waves 17+ draw from the four tier-4s before refilling. That is thin but no longer
   pathological, and it keeps closing as families are added — do NOT paper over it by
   widening the tier-3 window.

   Worth revisiting once the roster settles: four tiers per family is the right shape at
   four families, but at six or eight it is a lot of assets to keep playtested, and 2–3
   tiers each may cover the same curve.
3. **Orb schedule: Near and Far 2 is out of step.** The owner's schedule is mana / health
   / mana / health by tier; Near and Far runs mana / **mana** / mana / health. The twelve
   assets here follow the schedule. Worth aligning one way or the other so the reward
   cadence is a rule rather than a per-family accident.
4. **Weights** stay at the house 1000 for all twelve. (The forest assets are still
   carrying the 100000000 playtest weight — unrelated, but still outstanding.)
5. The two "recommended additions" above — gnome cooldown overrides (§3) and Delivery
   convoys (§4) — are each small, and each unlocks a tier lever that does not currently
   exist. Neither is built.
