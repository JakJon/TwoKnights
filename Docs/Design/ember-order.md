# The Ember Order — Design & Build Plan

*Drafted 2026-07-20. Phase D of `orders-and-the-full-run.md`.*

## The concept: you don't burn enemies, you burn the arena

Serpent poisons a body and walks away. Shadow kills faster. **Ember sets the field on
fire, and enemies are just how the fire gets around.**

An ignited enemy is on fire: it takes the **standardized fire dps** directly (base 3.0/s,
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

**There are no exceptions.** Scorched Earth briefly bought one — the capstone opened a
third door and let burning ground light what walked into it — and it was taken back out
the same day (owner's call, 2026-09-08). Burning ground *cooks* what stands in it and a
burning body *cooks* what it touches; neither lights anything. The pillar holds
everywhere, for every upgrade.

The plumbing for the other behaviour survives behind `EmberBoost.GroundFireIgnites`, off,
reachable from a debug console. No upgrade sets it.

An **arrow** flying through fire is the one nuance: it *becomes* an ignited arrow (and an
arrow through a poison cloud becomes a poisoned arrow), picking up that carrier state just
as if it had rolled it — so it's still one of the two sanctioned sources doing the
igniting, not the ground. It stays player-gated: you still have to aim it and land it, so
the runaway loop below never closes. Shadow arrows and shurikens do **not** pick effects
up this way — only a knight's main arrow does.

This is the rule the whole Order is built on, and it must not be relaxed anywhere:

- It kills the runaway loop. Without it, fire ignites enemy → enemy lays trail → trail
  ignites next enemy → the arena self-immolates and the player stops mattering. Selling
  that loop as a capstone was tried and rejected: even at the top of the Order, a board
  that lights itself is a board the player has stopped playing.
- It keeps ignition **scarce and authored**. Every burning enemy on the field is one the
  player chose to light. Trails become terrain *you* drew, not weather.
- It gives fire zones one honest job: flat damage over an area. No status, no bookkeeping,
  no per-source accounting.

### Fire zones

The single primitive: a circle of burning ground dealing **3 damage/second** to anything
inside (tuning value — its damage hitbox reaches ~1.15× the drawn flame radius so the edge
isn't a dead zone). Zones never ignite until Scorched Earth.

Placed by fireball craters, and by ignited enemies as they run.

**Eternal zones look different.** `FireField` mixes deep-blue embers (~1 particle in 5,
three-quarters the size of an orange one) into any zone that never expires, so a lane
about to burn out and one that never will are two different-looking fires. The tell is
free: before Scorched Earth no zone draws them.

### Zone damage scaling

Bellows is gone, so the dps knob rides on the other upgrades instead of a dedicated
multiplier chain. Retuned down twice on 2026-09-08 (ceiling 7.5 → 6.0 → **4.5**):

| | dps |
|---|---|
| Base | 3.00 |
| Fire Trail I | **+0.00** |
| Fire Trail II | +0.25 |
| Searing Panic I | +0.25 |
| Searing Panic II | +0.50 |
| Scorched Earth | +0.50 |
| **Full Ember build** | **4.50** |

**Two thirds of the ceiling is the base.** Ember's upgrades buy reach — wider lanes, more
of them, fire that stays — and only trim the rate. That is what keeps a fully-built field
from deleting anything that touches it.

Fire Trail I is the one pick in the Order worth no dps at all. It buys the lane, which is
the whole of what it is for.

Searing Panic is the only chain whose two ranks are worth different amounts. It is the
pick that reads as a downside, so rank I has to pay something the frame it lands — the
frame the enemies get faster — and rank II is where it actually gets paid.

This is the dps of a single zone *and* of a single burn stack. Two ignited arrows from a
full build burn a body at 12.0/s combined until their timers stagger out — and if that
body is also standing in fire, the field channel bills another 6.0 on its own clock.

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
| Firebrand I | 55 | Rare | — (starting pick) |
| Firebrand II | 24 | Epic | Firebrand I |
| Firebrand III | 14 | Legendary | Firebrand II |
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

### Firebrand I–III — the sword door

**Every** sword swing lobs fire along the shield facing (owner's call, 2026-09-25). The
catch is reach: the fireball is lobbed, not thrown, and it comes down and bursts a short
step in front of the blade whether or not it met anything.

- **I** — 1 fireball, lands **0.5u** out.
- **II** — the same fireball lobbed three times as far, **1.5u**.
- **III** — a second fireball; the pair fan ±15°, both 1.5u.

The lob moves at 1.75 u/s (a quarter of the old 7 u/s) so the arc can be followed. The
arc is drawn rather than simulated: the fireball travels flat, its sprite swells to 1.5x
at the middle of the flight and shrinks back, and a near-black shadow drops below it and
meets it again at the landing.

*Before 2026-09-25:* a 20% chance per swing for 1 / 2 / 3 fireballs that flew 7 u/s for
four seconds and fizzled if they hit nothing.

These are real fireballs: they explode, ignite, and leave craters — but smaller ones
(owner's call, 2026-09-25). A lob's blast is **half** the Fireball chain's current blast
radius (0.75u, or 1.0u with Fireball III), and its crater has a **0.5u** radius and
burns **6s**, half of a shot fireball's 1.0u / 12s. Firebrand I is a starting pick of its
own; it used to need Fireball I. That makes the sword Ember's third ignition source, and the only one that works at melee range
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

- **I** — a 0.5u zone every 0.35s of movement, each lasting 4s. Worth no dps: the lane
  *is* the pick.
- **II** — 0.75u zones lasting 7s, and +0.25 dps.

### The view is a hard boundary for ground fire

Waves spawn off screen and walk in, and fire the player cannot see must not be hurting
things the player cannot see. Three rules (2026-09-08), all keyed on
`FireField.IsInsideView` — the camera's world rect, falling back to the playfield rect the
waves are written against when there is no orthographic camera:

1. **A zone whose centre is off screen is never placed.** `FireField.Add` refuses it, so
   no call site can forget — Fire Trail drops, fireball craters and anything added later
   all obey it. This is the "completely ignored" case.
2. **A zone straddling the edge is trimmed to it.** `Sample` refuses any point off screen,
   so the half of a crater inside the view burns and the half hanging out of it is inert
   ground. The test is on the *sampling point*, not the zone, which is what lets one check
   clip every zone at once.
3. **Nothing is drawn off screen either**, so a trimmed crater reads as the clipped shape
   it actually is rather than as a full circle half of which does nothing.

A trail drop refused this way is *skipped, not banked*: the cadence has already reset, so a
body crossing the edge mid-interval starts painting on the normal beat rather than dumping
a held zone the instant it becomes visible.

**This is the field channel only.** Ignition is untouched — an enemy lit by an arrow or a
fireball goes on burning wherever it walks, on screen or off, because that fire rides the
body rather than the ground. Off screen you can be *set alight*; you cannot be *stood in
fire*.

Searing Panic and Scorched Earth widen these further (see the ladder below), so lane size
is the sum of four separate picks rather than Fire Trail's own.

It also solves Ember's boss problem for free. The Rat King circles the arena on a waypoint
rail — light him and he lays a burning racetrack he then has to keep lapping.

### Searing Panic I–II — the interesting one

**Ignited enemies move faster** (+35%, then +60%). Rank I adds **+0.25 dps**, rank II adds
**+0.5**, and each rank adds **+0.25u** of lane.

It reads as a downside — you are making the things running at you run faster — and that
tension is the point. With Fire Trail it's a large gain three times over: a panicking wolf
covers more ground while burning, each drop it leaves is wider, and every zone on the
field hits harder. Gated behind Fire Trail I, because without trails the speed is purely a
drawback.

### The lane-width ladder

Four picks widen a trail. Retuned down twice on 2026-09-08 (ceiling 2.25u → 1.75u →
**1.5u**):

| pick | width |
|---|---|
| Fire Trail I | 0.50u — the base lane |
| Fire Trail II | +0.25u |
| Searing Panic I | +0.25u |
| Searing Panic II | +0.25u |
| Scorched Earth | +0.25u |
| **Top of the Order** | **1.50u** |

By combination (add **+0.25u** to any cell for Scorched Earth):

| | Panic 0 | Panic I | Panic II |
|---|---|---|---|
| Fire Trail I | 0.50u | 0.75u | 1.00u |
| Fire Trail II | 0.75u | 1.00u | 1.25u |

Every step is a quarter, so the ladder is the base lane plus four equal rungs. **Fire Trail
I owns two thirds of the ceiling by itself** — the lane is the pick, and everything after
it is a trim.

Additive rather than multiplied so a step is worth the same wherever it is bought, and so
the table above *is* the implementation.

> **Known rough edge.** `FireField` spreads a fixed particle budget across the whole
> field, so lanes this size draw noticeably thinner than small ones. The damage is
> unaffected — the hitbox is the radius — but the fire looks sparser than it bites.

### Scorched Earth — capstone (requires 4 Ember picks)

Three things (2026-09-08):

1. **The fire does not go out.** Your fire zones stop expiring — every zone you place
   burns for the rest of the wave.
2. **+0.25u to every Fire Trail lane**, on top of Fire Trail's and Searing Panic's own —
   the last rung of the width ladder.
3. **+0.5 zone dps** — joint-biggest single damage step, level with Searing Panic II, and
   still only an eighth of a fully-built field's rate. The capstone is bought for
   permanence; the number is a garnish.

Ember stops being a hazard you re-apply and becomes a map you are drawing. A knight who
has been laying trail all wave ends it standing behind an impassable field, and the last
enemies of the wave have to cross everything the first ones painted. It is the literal end
state of "you burn the arena," which is why it beat the alternatives.

**It does not ignite.** For one day it did, gated behind four Ember picks and a Legendary
roll, on the theory that a runaway board is a reward rather than a bug when it costs that
much. It isn't. The capstone keeps the eternal zones and the damage and gives up the
ignition — burning ground lights nothing, however long it burns and whoever laid it.

The two things eternal zones still demand of `FireField` are unchanged: a hard zone cap
with oldest-first retirement, and the wave-end clear.

### The fire-AoE flag — standing in fire is a flag, not an ignition

**Ground fire never adds a burn stack.** It sets one flag on the body, and while that flag
holds the body burns at the rate of the ground under it — the zone dps, once — and counts
as on fire for the flame, the trail and Searing Panic.

This is the single most important rule in the Order's damage model, and it exists because
the first version of Scorched Earth got it wrong. Catching from the ground minted one burn
stack per *distinct fire* underfoot, and burn stacks sum. A Fire Trail lane is dozens of
overlapping drops from many different burns, so one step into a painted patch handed a
body five or six stacks at once: **40–70 damage on the next flush**, from a build whose
stated zone dps was 11. The damage a body took depended on how many fires happened to meet
under its feet, which is not a number anyone can reason about.

The two halves are split by how *scarce* the fire is:

| | stacks? | why |
|---|---|---|
| Arrow that rolled ignite, fireball | **yes** | aimed *and spent* — a shot each, so stacking is the reward for landing several |
| Fire Sight (the beam) | **no** | aimed but not spent — the beam runs every frame, so it lights only what is not already on fire |
| Burning ground | **no** | one flag; overlap cannot multiply a flag |
| A burning body in contact | **no** | a rate with an expiry, hottest-wins |

**Fire Sight is a one-time ignition per crossing.** It lights a body that is not alight
and then leaves it alone, however long it is held in the light. The test is `IsOnFire`
rather than `IsIgnited`, because "alight" and "carries a burn stack" are different
questions: a body alight from the ground has no stack. Nothing lights a body that way
today, so the two read the same — but the beam should refuse anything visibly already
burning, whatever lit it.

### Two channels, two ticks, two numbers

Fire bills a body from **two independent sources**, each accruing and flushing on its own
clock:

| channel | what feeds it | how it combines | colour |
|---|---|---|---|
| **Ignition** | burn stacks from arrows and fireballs (and one from Fire Sight) | **summed** — every live stack | ember orange |
| **Field** | the ground under it, or a burning body in contact | **hottest-wins** — one zone's worth | gold |

These are **additive against each other**. An enemy carrying your arrow's burn *and*
standing in your burning ground pays for both, which roughly doubles what a full Ember
build does to a body that is both lit and in a lane. That is the intended shape — Ember's
pitch is that the two halves compose — and it is why the numbers land as two separate
figures rather than one.

Hottest-wins survives only *inside* the field channel, where it is doing real work:
overlapping zones are one fire, and a zone next to a burning neighbour is still one fire.

The flushes run **half a second out of phase** so the two figures alternate on screen
instead of landing on top of each other, and they are different colours so you can read
which half of your build is doing the work.

An ignited enemy walking its *own* trail is still not billed twice for one fire — but that
is `FireField.Sample`'s doing (it skips zones that body dripped), not the damage model's.
Standing in somebody else's fire is genuinely a second fire and costs a second fire.

**The flag lingers 2 seconds after the body leaves the fire — but the linger does not
bill.** Damage is charged only for the beats a body is actually standing in fire; the
linger exists so the *state* does not chatter on and off as a body clips the edge of a
lane.

> Charging the linger was a bug, fixed 2026-09-08. A dark bat (30 HP) that grazed a lane
> for a fifth of a second was billed for 2.2 seconds — 13 damage, 44% of its health, most
> of it while flying nowhere near the fire. Anything fast enough to cross a lane paid
> almost as much as something that stopped in it, which is the opposite of what a burning
> floor should do. The same graze now costs 0.9.

A body cannot refresh its flag from its own drippings (`FireField.Sample` skips zones that
body laid), so it cannot burn off its own trail.

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

The ignition pillar is still enforced structurally, not by convention: `FireField` has
**no code path that calls `Ignite`**. Zones are dumb data that report what is burning
underfoot; the *enemy* polls the field and decides for itself whether to catch
(`EnemyBase.CatchFireFromGround`). Scorched Earth changes what a zone reports, not who is
allowed to call `Ignite` — which is why the exception could be added without touching the
one-way direction that makes the pillar hold.

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
- `SwordSwing` — the Firebrand roll, mirroring where `ExhaleSerpentsBreath` already
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
