# Two Knights — Orders & The Full Run (Design Vision)

*Agreed 2026-07-09. Design pillars: every wave handcrafted (no procedural generation);
runs are beatable (Vampire Survivors model); finite, recognizable upgrades (Hades/VS —
no infinite stat soup); organic specialization (random draft that bends toward your
picks); Poison ships first.*

## Why: the wave-16 ceiling

Every wave asset plays at most once per run (`WaveManager.SelectNextWave` removes it),
unlock windows thin the pool as waves climb, and a dry pool silently stalls the run
(`Spawner.StartNextWave` no-ops on null). There is no numeric scaling anywhere — all
difficulty is hand-authored. The fix is not scaling formulas; it is structure: Orders
give builds identity, bosses give runs a finish line, maps give the game more rooms.

## 1. The Class System — "Orders"

Each upgrade belongs to one knightly **Order** (or the Neutral pool). An Order = accent
color + icon, 2–3 named disciplines (chains, via the existing `unlockedBy` DAG + pips
UI), and one **capstone** gated on owning N upgrades of that Order.

**Draft mechanics (organic specialization):**
- **Affinity:** effective weight = base weight × (1 + 0.75 × knight's owned count in
  that Order). Neutral never gets affinity — it's the reliable filler, like VS passives.
- **Variety guarantee:** max 2 of the 3 cards per draft from one Order; one slot always
  draws class-blind.
- **Capstone gating:** capstones require ~4 Order picks; reachable by a committed knight
  around wave 13–15.
- **Recognizability:** every upgrade has a proper name ("Venom Tip III",
  "Plaguebringer"); the whole pool stays curated at ~60–65 assets across 5 Orders +
  Neutral. Cards show Order color/icon at a glance.

**Duo upgrades (the Two Knights signature, later phase):** offered only when the *left*
knight is committed to Order A and the *right* to Order B; upgrades both at once.
*Serpent's Shadow* (Poison+Ninja: shadows always poison), *Witchfire* (Poison+Fire:
burns accelerate poison ticks), *Guardian of Thorns* (Tank+Poison: blocks fire back venom
darts). The run's story becomes *the pair*.

## 2. Class roster

- **Serpent (Poison)** — patient, inevitable death; swarm/tank killer, weak burst.
  Disciplines: Venom Tip I–III (chance 30/60/100%), Virulence I–II (stronger/faster
  ticks), Miasma I–II (death-clouds spread poison), capstone **Plaguebringer**
  (requires 4 Serpent picks: poisoned deaths burst onto neighbors and feed the special
  bar). Poison inverts the arrow economy — a poisoned wolf dies eventually even
  unattended, so Serpent knights can afford to switch targets; big-count waves are the
  Serpent showcase.
- **Shadow (Ninja)** — action economy: Serpent wins by patience, Shadow wins by
  volume. Disciplines (12 upgrades): Shadow Arrows I–V (existing chain; retune
  weights descending 100→30), Shuriken Fan I–II (main shot splits ±12°/±24° at
  35% dmg, rolls poison per shuriken), Killing Blow I–II (execute enemies below
  15%→25% max HP — never bosses), Phantom Blade I–II (sword swings leave a
  shadow clone that repeats the swing at 50% dmg — the sword door, mirroring
  Serpent's Breath), capstone **Thousand Cuts** (requires 4 Shadow: every kill
  erases the firing cooldown for 2s; reuses RapidFire tech). Starting picks:
  Shadow Arrows I + Phantom Blade I; Shuriken/Killing Blow unlock off Shadow
  Arrows I. New `NinjaBoost` knight stat sheet mirrors `PoisonTipBoost`.
- **Guardian (Tank)** — the shield is the weapon: Tower Shield (a longer bar),
  Curved Aegis (bows it around the knight for a wider arc) *(both shipped)*, Thorned
  Aegis (reflect blocks), Stalwart (blocks charge special), capstone *Unbreakable*
  (the damage streak-reset is suppressed once per wave).
- **Ember (Fire)** — burst + area denial: Ignited Tips (short/hot DoT vs poison's
  long/slow), Fireburst (kill explosions), Scorched Ground, capstone *Immolation*
  (special = firestorm).
- **Dawn (Healing)** — sustain + partnership, the co-op Order: better orbs, lifesteal,
  *Shared Light* (heals spill to the other knight), capstone *Guardian's Vigil* (save
  the other knight at 1 HP once per map).

## 3. The full run — maps, gate bosses, true bosses

- A **Map** = tileset + mob palette + a handcrafted wave setlist (today's window
  system, scoped per map) + bosses. Implementation sketch: a `MapDefinition`
  ScriptableObject the WaveManager loads instead of its single global list; `SaveData`
  gains map unlock/clear state. This also fixes the silent stall — a boss is the
  terminal wave, never an empty pool.
- **Map lengths vary and grow:** Map 1 ≈ 10 waves, Map 2 ≈ 15, later maps 20–30.
- **Gate boss → true boss:** the first time you reach a map's nominal end (Map 1:
  wave 10) the **gate boss** appears (Map 1: **The Rat King** — circles the arena on
  wolf waypoint paths, telegraphed projectile fans that steer both knights' aim,
  fuse-rat adds, authored phases). Beating a gate boss unlocks **(a)** that map's
  extended waves beyond the gate, leading to the map's **true final boss** deeper in
  (~wave 20+ for Map 1), and **(b)** the **next map** — the camp map-select becomes a
  fan of open areas the player chooses between, not a strict ladder.
- **Map 2 — "The Mine"** (replaced the planned Belfry, 2026-07-25): a dark cave map
  whose signature is **rails and mine carts**. Track is scenery that reconfigures per
  wave — a straight horizontal line, vertical shafts, a full loop — and carts ride it
  as moving hazards the knights must read and shoot around. The rail network is the
  map's structural verb the way the bell was going to be the Belfry's rhythm one.
  - *Shipped:* `RailLayout` (a track shape as data), `RailNetwork` (builds/clears it),
    `RailSegment` + `SpriteFlipbook` (600ms fall, then a dust puff), and the first
    wave **Choo Choo** — a full-width track laid on a 0.15s cascade over a slow
    four-position projectile cycle. Only the horizontal rail piece has art;
    `RailPieceKind` already names the vertical and corner pieces so loops and shafts
    are a sprite away, not a rewrite.
  - *Open:* cart prefab and cart movement along a layout, mine mobs, ~15 waves,
    gate boss. `The Mine.asset` is `unlockedByDefault` while it's being built out —
    gate it behind the Rat King once it has a real setlist.
  - The Belfry's ideas (Gargoyle dives, Wraith, rhythm boss) are unspent and can move
    to a later map. Map 3+ repeats the pattern (Crypt: armored mobs resist arrows but
    not DoT — a Serpent/Ember showcase). **A map is the unit of content.**
- **Post-victory chase:** handcrafted per-map **Trials** (modifier runs), not endless
  scaling. Far-future.

## 4. Quests, gold, camp

Quest hooks are already generic (`QuestProgress` keys off any stat): add keys per class
(`kills.poisoned`), per map (`boss.ratking.defeated`), per feat. Quest lines narrate the
roadmap. Honor/rank stays the achievement track and can co-gate map unlocks. Gold
becomes the camp economy via the camp skill tree: unlock later Orders into the pool
(meta progression = *variety*, not raw power), starting boons, camp buildout — a tent
per Order, lit when unlocked.

## 5. Roadmap

| Phase | Delivers |
|---|---|
| **A** | Orders framework + full Serpent class + card styling + particles |
| **B** | `MapDefinition`, gate-boss/true-boss flow, Rat King, victory screen, stall fixed, map unlocks |
| **C** | Map 2 "The Mine": camp level select + rail system + Choo Choo *(done)*; carts, mine mobs, ~15 waves, gate boss *(open)* |
| **D** | Guardian/Ember/Dawn + duo upgrades |
| **E** | Camp skill tree, class/map quest lines, gold sinks |
