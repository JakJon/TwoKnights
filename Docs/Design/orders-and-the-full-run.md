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
burns accelerate poison ticks), *Bulwark of Thorns* (Tank+Poison: blocks fire back venom
darts). The run's story becomes *the pair*.

## 2. Class roster

- **Serpent (Poison)** — patient, inevitable death; swarm/tank killer, weak burst.
  Disciplines: Venom Tip I–III (chance 30/60/100%), Virulence I–II (stronger/faster
  ticks), Miasma I–II (death-clouds spread poison), capstone **Plaguebringer**
  (requires 4 Serpent picks: poisoned deaths burst onto neighbors and feed the special
  bar). Poison inverts the arrow economy — a poisoned wolf dies eventually even
  unattended, so Serpent knights can afford to switch targets; big-count waves are the
  Serpent showcase.
- **Shadow (Ninja)** — action economy (Shadow 1–5 exist): Shuriken Fan multishot,
  Killing Blow executes, capstone *Thousand Cuts* (no cooldown for 2s after a kill).
- **Bulwark (Tank)** — the shield is the weapon: Thorned Aegis (reflect blocks),
  Stalwart (blocks charge special), Widened Guard (bigger arc), capstone *Unbreakable*
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
- **Map 2 — "The Belfry":** mobs that twist the one player verb (shield rotation):
  Gargoyle (telegraphed dives), Wraith (arrow-immune except on the bell's beat), Belfry
  Spider (webs slow shield rotation). Gate boss *The Bell Warden* — a rhythm boss.
  Map 3+ repeats the pattern (Crypt: armored mobs resist arrows but not DoT — a
  Serpent/Ember showcase). **A map is the unit of content.**
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
| **C** | Map 2 "The Belfry": new mobs, ~15 waves, Bell Warden, camp map select |
| **D** | Bulwark/Ember/Dawn + duo upgrades |
| **E** | Camp skill tree, class/map quest lines, gold sinks |
