# The Ember Order — Design & Build Plan

*Drafted 2026-07-20. Phase D of `orders-and-the-full-run.md`.*

## The concept: you don't burn enemies, you burn the arena

Serpent poisons a body and walks away. Shadow kills faster. **Ember sets the field on
fire, and enemies are just how the fire gets around.**

An ignited enemy is on fire: it takes the **standardized fire dps** directly (base 2.0/s,
scaling with Ember investment — see below), it drips fire onto the ground behind it as it
moves, and it panics. So a lone Ignited-Tips hit deals damage on its own; the trails and
craters then add *more* fire the enemy — and everything behind it — has to stand in.

**Burns stack.** Each ignite that lands on an already-burning enemy adds an *independent*
burn — its own dps (that knight's zone dps) on its own 8s timer. Five ignited arrows in a
body burn it at 5× until, one by one, each timer runs out and the dps steps back down,
the way a real fire dies in stages. Ground fire is still taken as the *hotter* of
"sum of my burns" vs "the zone under me," never both, so walking your own trail isn't
double-billed.

That still separates Ember from Serpent at the concept level. Poison is a slow timer you
apply and forget on one body; Ember is a short, hot burn that turns the enemy into a
moving brush painting damage across the whole approach lane. Poison is a timer on a body.
Ember is terrain — plus the torch that draws it.

## THE PILLAR: only your shots can ignite

**An enemy can be ignited by exactly two things — a fireball, or an arrow that carries
ignite.** Nothing else. An *enemy* walking through fire does *not* ignite; it takes flat
fire damage and nothing more.

An **arrow** flying through fire is the one nuance: it *becomes* an ignited arrow (and an
arrow through a poison cloud becomes a poisoned arrow), picking up that carrier state just
as if it had rolled it — so it's still one of the two sanctioned sources doing the
igniting, not the ground. It stays player-gated: you still have to aim it and land it, so
the runaway loop below never closes. Shadow arrows and shurikens do **not** pick effects
up this way — only a knight's main arrow does.

This is the rule the whole Order is built on, and it must not be relaxed anywhere:

- It kills the runaway loop. Without it, fire ignites enemy → enemy lays trail → trail
  ignites next enemy → the arena self-immolates and the player stops mattering.
- It keeps ignition **scarce and authored**. Every burning enemy on the field is one the
  player chose to light. Trails become terrain *you* drew, not weather.
- It gives fire zones one honest job: flat damage over an area. No status, no bookkeeping,
  no per-source accounting.

### Fire zones

The single primitive: a circle of burning ground dealing **2 damage/second** to anything
inside (tuning value — its damage hitbox reaches ~1.15× the drawn flame radius so the edge
isn't a dead zone). Zones never ignite.

Placed by fireball craters, and by ignited enemies as they run.

### Zone damage scaling

Bellows is gone, so the dps knob rides on the other upgrades instead of a dedicated
multiplier chain:

| | dps |
|---|---|
| Base | 2.0 |
| Fire Trail I / II | +0.5 each |
| Searing Panic I / II | +0.5 each |
| **Full Ember build** | **4.0** |

This is the dps of a single zone *and* of a single burn stack. Two ignited arrows from a
full build burn a body at 8.0/s combined until their timers stagger out.

## The roster (13 upgrades)

Rarity is weight-derived in `BaseUpgrade` (≥100 Common / ≥50 Rare / ≥20 Epic / else Legendary).

| Upgrade | Weight | Rarity | Unlocked by |
|---|---|---|---|
| Ignited Tips I | 110 | Common | — (starting pick) |
| Ignited Tips II | 70 | Rare | Ignited Tips I |
| Ignited Tips III | 30 | Epic | Ignited Tips II |
| Fireball I | 100 | Common | — (starting pick) |
| Fireball II | 55 | Rare | Fireball I |
| Fireball III | 26 | Epic | Fireball II |
| Firebrand I | 55 | Rare | Fireball I |
| Firebrand II | 24 | Epic | Firebrand I |
| Fire Trail I | 55 | Rare | Ignited Tips I |
| Fire Trail II | 26 | Epic | Fire Trail I |
| Searing Panic I | 35 | Epic | Fire Trail I |
| Searing Panic II | 22 | Epic | Searing Panic I |
| Scorched Earth | 10 | Legendary | `requiresOrderCount: 4` |

Two Common doors instead of one, because the Order has two independent ignition sources
and either should be able to start a build.

### Ignited Tips I–III — the arrow door

Every arrow has a chance to ignite: **30% / 60% / 100%**.

Unlike the previous draft, this is not a damage chain — it is an *access* chain. It buys
the right to set things on fire; the fire itself is what deals damage. Rolls independently
per projectile, so shadow arrows and shurikens each get their own chance, matching how
poison already works in `PlayerShooter`.

**Ignited arrows carry a visible ember trail** so the player can read which shots are live
before they land — the same role `PoisonProjectile`'s bubble trail plays for Serpent.

### Fireball I–III — the rhythm door

**Every 5th → 4th → 3rd arrow leaves the shield as a fireball**: slower, fatter, orange. It
explodes on impact, igniting everything caught and leaving a fire zone in the crater.

A deterministic counter, not a roll, so the player can count to five and time the big one
into a cluster. It's the only upgrade in the game that gives the shoot button a rhythm, it
multiplies against the Reload line, and it turns Rapid Fire into a barrage.

- **I** — every 5th shot. Direct 1.5× arrow damage, blast 1.0× in 1.5u, crater zone 1.0u / 3s.
- **II** — every 4th shot.
- **III** — every 3rd shot, blast radius 2.0u.

### Firebrand I–II — the sword door

A sword swing has a **low chance to hurl a spread of fireballs** along the shield facing.
Rank II does not improve the odds — it adds a third fireball.

That distinction is the whole design of the chain. Most second ranks make a thing happen
more often; this one makes the same rare moment hit harder, so Firebrand stays a payoff
you can't fish for. It also keeps the sword honest as a close-range panic button rather
than becoming a primary fire delivery system.

- **I** — 20% chance per swing, **2** fireballs fanned ±15°.
- **II** — still 20%, **3** fireballs at −20° / 0° / +20°.

These are real fireballs: they explode, ignite, and leave craters like any other. That
makes the sword Ember's third ignition source, and the only one that works at melee range
when something has already closed the distance.

Serpent and Shadow both hang a discipline off the sword too (Serpent's Breath, Phantom
Blade), and all three do something different with it — Serpent exhales a drifting cloud,
Shadow echoes the swing, Ember throws ordnance.

### Fire Trail I–II — the enemy is the brush

An ignited enemy **drips fire behind it as it moves**, laying zones along its own approach
path. Also **+0.5 dps** to every fire zone you own, per rank.

This inverts the incentive of every other Order: Shadow wants enemies dead immediately,
Serpent doesn't care when — **Ember wants them to live a while burning**, because a wolf
that runs four seconds on fire paints an entire lane. Igniting something far away and
early becomes correct play.

- **I** — a 0.5u zone every 0.35s of movement, each lasting 2s.
- **II** — 0.75u zones lasting 3.5s.

It also solves Ember's boss problem for free. The Rat King circles the arena on a waypoint
rail — light him and he lays a burning racetrack he then has to keep lapping.

### Searing Panic I–II — the interesting one

**Ignited enemies move faster** (+35%, then +60%), and each rank adds **+0.5 dps** to
your fire zones.

It reads as a downside — you are making the things running at you run faster — and that
tension is the point. With Fire Trail it's a large gain: a panicking wolf covers more
ground while burning, paints more field, and reaches its own trail sooner. Gated behind
Fire Trail I, because without trails it is purely a drawback.

### Scorched Earth — capstone (requires 4 Ember picks)

**The fire does not go out.** Your fire zones stop expiring — every zone you place burns
for the rest of the wave.

Ember stops being a hazard you re-apply and becomes a map you are drawing. A knight who
has been laying trail all wave ends it standing behind an impassable field, and the last
enemies of the wave have to cross everything the first ones painted. It is the literal end
state of "you burn the arena," which is why it beat the alternatives.

Two things it demands from the implementation:

- **A zone cap.** Non-expiring zones accumulate without bound. `FireField` keeps a hard
  ceiling (~200 zones) and retires the oldest when it's hit — invisible in practice,
  but the difference between a capstone and a memory leak.
- **A wave-end clear.** Zones die with the wave, not the run. Otherwise wave 12 begins
  inside wave 11's inferno and the difficulty curve inverts.

**Balance watch:** this is the one upgrade in the Order that could make late waves
*easier* than mid waves. If it over-performs, the lever is zone dps, not the mechanic —
the fantasy is worth protecting.

---

# Build Plan

## Tooling reality for this session

Native `mcp__UnityMCP__*` and `mcp__aseprite__*` tools did **not** load, so both go
through their documented fallbacks (verified live at plan time — Unity bridge answered
HTTP 200, `pixel-mcp.exe` present):

| Need | Path | Source |
|---|---|---|
| Unity editor ops | raw JSON-RPC to `127.0.0.1:8080/mcp` (initialize → session header → `execute_code`) | `unity-mcp-direct-http` memory, prefab-authoring skill |
| Sprites | stdio pipe into `pixel-mcp.exe`, LF-only JSONL, forward slashes | sprite-authoring skill |
| Fast C# checks | offline `csc` compile-check, no editor round trip | `unity-offline-editing-recipes` memory |
| Runtime validation | Test Mode via `TestRunConfig` + `execute_code` | test-mode-validation skill |

`execute_code` compiles under **CodeDom, C# 6 max** — no `?.`, no pattern matching, no
string interpolation. Payloads get built with python `json.dumps`, never inline bash
quoting.

## Phase 1 — `FireField`, the core (build first, it's the risk)

Everything routes through one system, and it is the only genuine engineering risk in the
Order. `PoisonCloud` spawns a GameObject with its own `ParticleSystem` per cloud — fine at
one per poisoned death, fatal at one every 0.35s *per burning enemy*. Twenty burning rats
would be ~57 allocations a second.

So: **`FireField` singleton** — zones as plain structs (position, radius, expiry, owner
tag) in one list, ticked centrally on a fixed interval, drawn by a **single pooled
particle system** emitting into every active zone. One overlap query per tick, not one per
zone.

Also in this phase: the `ignited` carrier state on `EnemyBase` (much simpler than the
poison block — no stacking, no per-source accounting, just a flag + expiry + owner tag),
the trail-emission hook, and the Searing Panic speed modifier.

The ignition pillar is enforced structurally, not by convention: `FireField` has **no
code path that calls `Ignite`**. Only `PlayerProjectile` and the fireball can.

Scorched Earth's two requirements land here as well, since both are `FireField`'s job: the
hard zone cap with oldest-first retirement, and a wave-end clear hooked to the same signal
the Spawner already uses for wave transitions.

Verified by offline `csc` compile-check, then a forced editor refresh + `read_console`
(0 errors) via the HTTP bridge.

## Phase 2 — Art: fireball sprite + ember trail

Per the sprite-authoring skill's procedural workflow — distance field in python,
posterized alpha, one big `draw_pixels` call, **export in a separate server invocation**
(same-batch export races the save and yields a blank PNG), then the mandatory PIL preview
loop: tint, composite on dark field-green, ×10 nearest-neighbour, and actually look at it
before shipping.

Two sprites:
1. **Fireball** — the projectile body, ~16px.
2. **Ember mote** — the particle for ignited-arrow trails, fire zones, and craters. Drawn
   **white with alpha** so `ParticleSystem.startColor` can tint it everywhere, exactly like
   `PoisonPuff.png`.

Both import via `execute_code` TextureImporter: point filter, uncompressed, no mipmaps.

Standing rule respected: **no existing art is touched.** If either sprite comes out
placeholder-grade, it gets flagged for repainting rather than quietly shipped.

## Phase 3 — Prefab

`Projectile_Fireball` as a **prefab variant** of the existing player projectile, built via
the bridge (`PrefabUtility.InstantiatePrefab` → mutate → `SaveAsPrefabAsset` →
`DestroyImmediate`). It must be built with the editor open: sprite sub-asset fileIDs are
importer-generated hashes and cannot be hand-authored offline.

The reference travels by house convention #1 — serialized field on `FireballUpgrade` →
`EmberBoost` at apply time, with a fallback to the arrow prefab at the consumer.

## Phase 4 — Upgrade SOs and assets

Six SO classes: `IgnitedTipsUpgrade`, `FireballUpgrade`, `FirebrandUpgrade`,
`FireTrailUpgrade`, `SearingPanicUpgrade`, `ScorchedEarthUpgrade`.

Thirteen `.asset` files under `Assets/Upgrades/Ember Ups/`, hand-authored as YAML offline
(plain MonoBehaviour YAML against a script guid — no new-sprite fileIDs involved, and the
one prefab reference uses the constant root fileID `100100000` once the prefab guid
exists, so it's authorable after Phase 3). Registered in `UpgradeManager.asset`.

Every asset needs its `stats` badge list hand-authored alongside a short, number-free
description — house rule since the badge pass. Ignited Tips gets `+30% IGNITE CHANCE`,
Fireball `EVERY 5TH SHOT`, Fire Trail `+0.5 FIRE DPS`, Searing Panic `+35% ENEMY SPEED`
marked as a bane plus its dps buff.

Compile **before** wiring any serialized field added this session, or the property won't
exist to find.

**Zero existing assets change** — Ember is entirely additive, so there's no repeat of the
Serpent retag risk.

## Phase 5 — Hooks

- `PlayerShooter` — shot counter + fireball spawn; ignite roll at all three existing spawn
  sites (main arrow, shurikens, shadow arrows), alongside the poison roll.
- `PlayerProjectile` — ignite on hit.
- `SwordSwing` — the Firebrand roll, mirroring where `TryExhaleSerpentsBreath` already
  hooks in. Note the knight is resolved via `GetComponentInParent<PlayerHealth>()` there,
  because sword attack objects are untagged children — the same lookup Firebrand needs to
  credit fireballs to the right knight.
- `EnemyBase` — ignited state, trail emission, speed modifier.
- `PlayerStats.Increment("kills.burned")` in the fire-zone kill path, mirroring
  `kills.poisoned`, for a free quest hook.

UI needs nothing: `order--ember` is already styled orange (`rgb(226,120,63)`) in
`UpgradeMenu.uss` and the enum slot exists.

## Phase 6 — Validation

Test Mode, driven programmatically: enter play mode, `execute_code` sets `TestRunConfig`
with an Ember loadout **from inside play mode** (domain reload wipes an edit-mode config),
set `AutoPickWave` so the run doesn't stall on the picker, load Main, confirm via
`GetAppliedUpgradeNames`, observe, stop, and reset `AutoPickWave` to null before handing
the editor back.

Three things to actually watch:

1. **A dense swarm wave** (Rat Mischief / Bat Swarm) — do trails paint readable lanes, or
   does the screen turn into orange soup?
2. **The Rat King** — does the racetrack effect materialize the way the design claims?
3. **Zone count and allocation rate** under the worst case, read programmatically.

Known gotcha from prior sessions: play mode and screenshots **freeze while the editor app
is unfocused**. Anything timing-dependent needs Jake focused on the editor; everything
else gets verified by reading state programmatically.

Balance knobs (base dps, per-rank dps, trail cadence, zone radii/durations, panic speed)
all live as consts in one place so the tuning pass is fast.

## Open interaction

`EnemyBase` drives its poison tint through the shared `glowManager`. An enemy both
poisoned and ignited would have two systems fighting for the glow. Proposal: ignition owns
the glow while active (it's much shorter), poison's resumes when it expires.
