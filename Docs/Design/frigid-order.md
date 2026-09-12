# The Frigid Order — Design & Build Plan

*Built 2026-09-06. The sixth Order, and the first one that does not kill anything.*

## The concept: you don't damage them, you take their time

Serpent wins by patience. Shadow wins by volume. Ember wins by owning the ground.
Dawn wins by refusing to lose the pair. **Frigid wins by taking the enemy's time.**

That is worth more here than it would be almost anywhere else, because of the one
rule the whole game is built on: **the knights cannot move.** Every wave is things
closing distance against a fixed rate of fire. A slowed enemy is not a weakened
enemy — it is extra shots, for free, at no cost to anyone's damage numbers. Frigid
is the only Order that attacks the distance instead of the health bar.

## The rule: two states, and one sentence

> **The first touch of cold slows. The second stops.**

A body hit by anything Frigid becomes **Chilled** — visibly blue, moving slower,
animating slower. Cold landing on something already chilled **Freezes** it: held
where it stands, taking no steps, throwing nothing, arriving nowhere.

Two states, not a stack counter. That was a deliberate move away from the original
backlog phrasing ("stacks, and enough stacks freeze one outright"). On a screen
with twenty rats on it, a player has to be able to look at a body and know what one
more arrow does to it. Blue means slow. Ice means stopped. No arithmetic.

## THE PILLAR: three parts, none of them negotiable

**1. Frost deals no damage until the capstone, and then it deals all of it.**
Every other number in the Order is a second, a percentage of speed, or a multiplier
on a blow *you* land — and the whole roster is priced against that, so a Frigid
knight without **Frost Bite** is still buying time and nothing else.

**Shatter** and **Rimeblade** are not exceptions to this: both are damage the
player aimed and landed, not a timer paying out.

*This half was rewritten on 2026-09-08 (owner's call).* It used to read "frost has
no damage of its own, Frigid never ticks", and Frost Bite is exactly the tick it
forbade. The revision was deliberate: the Order had no way to convert all that held
time into dead bodies, and the old rule's real job — keeping Frigid from being a
blue re-skin of Serpent — is now done by the shape instead of by the absence.
Poison is bought per-arrow and rides one target; Frost Bite is bought once and
bills every chilled body on the field at a rate the player raises by freezing.

**Nothing below the capstone may grow a tick of its own.** That is the part of the
old rule still standing, and it is what keeps the roster legible: everything Frigid
does is time, and there is exactly one place that time turns into damage.

**2. Only a blow can freeze.** Fields, auras and splinters *chill*. They never stop
anything. Freezing takes a hit you landed — an arrow, or the blade. This is the
direct analogue of Ember's "only your shots can ignite" and it exists for the same
reason: without it, Glacial Ward alone locks the board down on a timer with nobody
deciding anything, and every statue on the field is supposed to be one the player
chose to make.

It is enforced structurally, not by convention. `FrigidBoost.TouchWithCold` takes an
`isBlow` flag, and the only two callers that pass `true` are `PlayerProjectile`'s
hit and `SwordSwing`'s burst.

**3. One hit does one thing.** An arrow that chills cannot also freeze on the same
hit. The second touch has to be a second blow.

### The fine print

- **Chill wears off** (3s at rank I). Without decay everything on screen is
  permanently chilled after one volley and the second state stops being earned.
- **A thaw buys nothing.** However the ice ended — outlasted, shattered, or broken
  by a stray hit — the body can be chilled on the very next touch and stopped on the
  one after. There used to be a 2s reprieve after a freeze *ran out*; it was removed
  on 2026-09-08 (owner's call) so the cycle never stalls. Pillar 2 is the only brake
  on holding one body forever, and it is enough: freezing takes a **blow**, so a
  knight who wants an ogre permanently stopped is spending an arrow a time that the
  rest of the wave does not get.
- **Ice breaks after 5 damage in total** (more with Deep Freeze — see below), whether
  or not the knight owns Shatter. Damage adds up across hits. Poison, fire and
  Frost Bite ticks never count, so a burning body burns through the whole hold.
- **Frozen is not asleep.** Frigid's ice and the Sleeping Dart's nap share the hold
  machinery (`EnemyBase.IsHeld`) but are separate states, so they do not cancel each
  other and the tells differ.
- **A frozen body still burns and still rots.** `SleepFreeze.RunFrozenFrame`
  guarantees it. Frigid must never become a way to *protect* a body from Serpent or
  Ember.
- **Nothing is immune.** Not carts, not bosses. `EnemyBase` already states the house
  position: a boss shrugging off a status "would send the player looking for the bug
  that is not there". Frigid does not consult `ImmuneToAreaDamage` — ice on iron is
  fine, and stopping carts is the entire point of Deep Freeze.
- **Orbs are slowed by cold that is not an arrow.** An arrow that reaches an orb
  *collects* it, so there would be nothing left to slow; the ward and the blade get
  to do it instead. A slowed orb is a wider window for both knights, the same
  reading Sunwell III already has.

## The roster (13 upgrades)

Accent `rgb(120, 200, 224)` (`order--frigid`). Cyan rather than a plain blue,
because blue already reads as the *right knight* in this game (`arrow_blue`,
`shield_blue`, `Player_Projectile_Blue`) and an Order accent that could be mistaken
for a knight's colourway tells the player nothing.

Rarity is weight-derived in `BaseUpgrade` (≥100 Common / ≥50 Rare / ≥20 Epic / else
Legendary).

| Upgrade | Weight | Rarity | Unlocked by |
|---|---|---|---|
| Frost Tip I | 110 | Common | — (starting pick) |
| Frost Tip II | 70 | Rare | Frost Tip I |
| Frost Tip III | 30 | Epic | Frost Tip II |
| Glacial Ward I | 100 | Common | — (starting pick) |
| Glacial Ward II | 50 | Rare | Glacial Ward I |
| Deep Freeze I | 55 | Rare | Frost Tip I |
| Deep Freeze II | 26 | Epic | Deep Freeze I |
| Shatter I | 55 | Rare | Frost Tip I |
| Shatter II | 24 | Epic | Shatter I |
| Rimeblade I | 55 | Rare | Frost Tip I *or* Glacial Ward I |
| Rimeblade II | 26 | Epic | Rimeblade I |
| Rimeblade III | 14 | Legendary | Rimeblade II |
| Deep Freeze III | 12 | Legendary | Deep Freeze II |
| Frost Bite | 12 | Legendary | `requiresOrderCount: 4` |

Two Common doors, as Ember has: one that rewards aim, one that does not.

### Frost Tip I–III — the arrow door

Every arrow carries cold. The chain deepens the cold rather than buying access to
it, and **the depth carries to everything the knight's cold touches** — the ward
and the blade get colder too. Frost Tip is not "your arrows are colder", it is "you
are colder".

| | Slow | Lasts |
|---|---|---|
| I | −30% | 3.0s |
| II | −50% | 6.0s |
| III | −70% | 10.0s |
| IV | −85% | 10.0s |

**Frost Tip freezes on its own** (owner's call, 2026-09-11). A Frost Tip arrow that
lands on a body that is already chilled freezes it for **however long the chill had
left** (read before the arrow refreshes the chill). So higher Frost Tip ranks, with
their longer chills, also make longer freezes. Deep Freeze no longer unlocks
freezing; it adds time and makes the ice harder to break. This applies to any Frost
Tip rank, including the one Winter's Tooth gives, and to the knight's other blows
(the sword, Rimeblade) too, because `CanFreeze` is a property of the knight.

A body chilled only by Glacial Ward has about 0.6s of chill left at any moment, so
an arrow freezes it for about 0.6s plus the Deep Freeze bonus.

**The slow is deliberately short of stopping anything.** The Order already has a
way to stop a body and it costs a second blow. If rank III left a wolf crawling, the
freeze would be buying almost nothing and the two-state reading would collapse back
into "cold is a slow that gets slower".

**Rank IV is the exception, and the draft cannot reach it** (owner's call,
2026-09-10). Equipment stacks on top of drafted ranks, so Winter's Tooth + Frost Tip
III lands at IV. It exists so that the last Frost Tip pick is never wasted for a
knight wearing the Tooth. It deepens the slow and leaves the duration at III's ten
seconds.

Rank I on its own is a real upgrade that is not yet a build — the rule Firebrand's
code comment states, that an entry tier strong enough to define a build leaves the
chain nowhere to grow.

Applies to every projectile the knight puts out, independently: main arrow,
shurikens, shadow arrows, companion darts.

### Glacial Ward I–II — the standing cold *(signature)*

A ring of cold sits permanently around the knight. Anything inside it is Chilled.

- **I** — 1.6u radius.
- **II** — 2.4u, **and enemy ammunition crossing it moves at half speed.**

The most Two-Knights upgrade in the game, and the reason it is a Common door: the
knights are fixed points, so an aura is a real, readable, stationary piece of the
board rather than something the player carries around, and it needs no aim at all.

Rank II is the clearest statement of what the Order is for. The core loop of this
game is rotating a shield to intercept things. Halving the speed of an incoming rock
is not damage and it is not defence — it is *time to get the shield there*, which is
the only currency Frigid deals in.

**The radius is tight, and tighter than it first shipped.** At 2.5u/3.5u the ring
covered so much of the approach that everything arrived pre-chilled and every arrow
landing anywhere near a knight froze — which quietly made freezing automatic
rather than something to aim. The ward is a *last line*: the cold a body walks into
once it is already close enough to be a problem.

The ward never freezes. See pillar 2.

### Deep Freeze I–III — longer, tougher ice

Frost Tip already freezes a chilled body for the chill it had left. Each Deep Freeze
rank adds **3 seconds** to that and **10** to the damage the ice takes before it
breaks:

| | Freeze time | Damage to break |
|---|---|---|
| Frost Tip only | chill left | 5 |
| I | chill left + 3s | 15 |
| II | chill left + 6s | 25 |
| III | chill left + 9s | 35 |

Break damage adds up across hits (`EnemyBase.BreakFreezeIfHardEnough`), and it is
set by the knight who froze the body. Constants: `FrigidBoost.FreezeSecondsPerDeepFreeze`
and `FreezeBreakDamagePerDeepFreeze`.

*Changed 2026-09-11 (owner's call).* Deep Freeze used to be the pick that unlocked
freezing at all, with flat holds of 3s / 6s / 15s and a 5-damage single-hit break.

**The carts are the showcase.** A held cart anchors `MineCart`'s spacing sweep, so
the run behind it stacks up against it: one arrow turns the mine's signature hazard
into a wall of the player's own making. That is Frigid's equivalent of the burning
racetrack Fire Trail draws around the Rat King, and it needed almost no new code —
`HoldFor`, `IsHeld` and `Movable` were already there for the sleeping dart.

### Shatter I–II — the only damage in the Order

Blows landed on a body held in ice hit harder.

- **I** — every blow on a frozen body deals ×2.
- **II** — ×3, and the ice throws splinters that **chill** everything nearby when
  it breaks.

With Deep Freeze the ice can take several hits, and **every one of them is
multiplied** — Deep Freeze III + Shatter II means a stack of ×3 hits before the
statue breaks. The burst, sound, splinters and the `frigid.shattered` tally only
happen on the hit that actually breaks the ice (or kills the body).

**The tension is the whole point**, and it is present on every frozen body on the
field: break it for the damage, or leave it standing as a statue and keep the time.
There is no right answer, which is what makes this a discipline rather than a number.

Rank II is also Frigid's only answer to a crowd — splinters are how cold reaches
more than one thing at once — and it stays inside pillar 2 by reaching them with
cold rather than with a blow.

### Rimeblade I–III — the sword door

A swing throws off a burst of cold centred on the knight, dealing the sword's own
damage to everything it catches.

| | Chance | Radius |
|---|---|---|
| I | 33% | 1.6u |
| II | 60% | 2.3u |
| III | 100% | 2.3u |

Every Order hangs a discipline off the sword and each does something different with
it — Serpent exhales a cloud, Shadow echoes the swing, Ember throws ordnance.
Frigid's blade is cold iron, and it answers the thing that has *already* closed the
distance.

The burst is centred on the **knight**, not thrown along the facing, because the
whole point of the sword in this game is that something is already too close. With
Frost Tip owned, a swing into a body an arrow chilled on the way in stops it dead
at arm's length — the strongest single moment in the Order, and it costs two picks
from different chains to reach.

Damage lands **before** the cold, the same order an arrow uses. Chilling first would
let one swing freeze a body and then break its own ice on the very next line.

### Deep Freeze III — the top of the chain (requires Deep Freeze II)

**Chill left + 9 seconds, and 35 damage to break.** With Frost Tip III's 10s chill
that is up to 19 seconds.

Long enough that the board fills with statues and the knights take them apart in
whatever order they like. It is the end state of "you take the enemy's time", the way
Scorched Earth is the end state of "you burn the arena", and it makes Shatter the
endgame — breaking the ice early is the only way a statue moves again before its
deadline.

**On its own the hold buys safety, not speed.** It does no damage; the arrows still
have to do all the killing and the wave takes exactly as long. A knight with rank
three and no Frost Bite is not faster, just untouched.

**With Frost Bite it stops being that**, and that pairing is the Order's whole top
end — a body held nineteen seconds while the cold bills it is a body that dies without
being shot. See the capstone's balance-watch note below.

#### It began as a separate capstone called Permafrost

It was folded into this chain and then given a real duration on 2026-09-08 (owner's
call). Both halves of that were fixes:

- As a parallel capstone it **silently voided Deep Freeze II.** `Freeze()` took
  `permafrost ? PermafrostSeconds : seconds`, so the eternal hold overrode the 3s/6s
  outright and rank two's only selling point stopped existing the moment it landed.
  As rank *three of the same chain* that is simply correct — a tier obsoleting the
  tier below it is what a tier chain is.
- It could be drafted **completely dead.** Gated only on `requiresOrderCount: 4` with
  no prerequisites, a knight with no Deep Freeze could take it and get literally
  nothing: `CanFreeze` was false, `TouchWithCold` passed `freezeFor = 0`, `ApplyCold`
  never reached `Freeze()`, and the flag was never read.
- **A deadline made the hold something the player has to renew.** Ice that never
  expires is a body removed from the game; a deadline is a body the knight has
  to keep choosing to stop.

The asset keeps the filename `Permafrost.asset` even though the card now reads "Deep
Freeze III": stat slugs are minted from the filename (`UpgradeManager.StatSlug`) and
the Frigid quest line spends `upgrades.taken.permafrost`. Same trick Greatshield
plays to read "Dawn Shield".

**The ice still dies with the WAVE, not the run.** `FrigidBoost.ClearFieldFrost()` is
called from `Spawner.BeginWave` beside `FireField.ClearAll()`. A long freeze
straddles a wave boundary happily, so without it wave twelve can begin inside wave
eleven's statues and the difficulty curve inverts.

### Bosses: every freeze is halved

`FrigidBoost.BossFreezeMultiplier = 0.5f` (owner's call, 2026-09-08), applied in
`EnemyBase.Freeze` rather than at the knight's end so it covers every source of cold
there will ever be and no future upgrade can forget it — the same argument that keeps
`Freeze` non-virtual.

The reason it is needed: **Frigid's hold is the one effect in the game that removes a
fight rather than shortening it.** A stopped boss is not fighting, and at full rank
that would be up to nineteen seconds of a duel simply not happening. Halved, the Order stays
strong against a crowd — which is what it is for — without switching off the
encounters the run is built around.

### Frost Bite — capstone (requires 4 Frigid picks)

**Chilled bodies lose 2 a second. Frozen bodies lose 3.**

This is the Order's only damage on a timer, and adding it **rewrote pillar 1**,
which used to read "frost has no damage of its own, Frigid never ticks". That was
a deliberate revision, not a leak — the rule was protecting against Frigid being a
blue re-skin of Serpent, and that protection is now carried by the *shape* rather
than by the absence: poison is bought per-arrow and rides one target, while Frost
Bite is bought once and bills every chilled body on the field at a rate the player
raises by freezing.

The rate is the Order's own cycle priced as damage. Slow it and it bleeds; stop it
and it bleeds faster — so "chill, then freeze" is also the damage upgrade, and the
player is paid for landing the second blow rather than for standing near things.

**Not gated on Deep Freeze**, deliberately. A knight who only ever chills — the
Glacial Ward build, which needs no aim and is the Order's no-skill door — still
gets the chilled rate. Gating it on the freeze chain would have made that whole
half of the roster a dead end. That matters MORE since 2026-09-09, not less: Deep
Freeze no longer opens off Glacial Ward at all (owner's call — the ward door was
removed from `unlockedBy`, leaving Frost Tip I as the only way in), so Frost Bite
and Rimeblade are now the whole of what the ward build has to grow into.

The reason the door closed is that it usually led nowhere. Only a BLOW can freeze
(pillar 2), and arrows carry cold only while `ArrowsChill` — which is Frost Tip. A
knight who drafted Glacial Ward and then Deep Freeze straight off the back of it
had bought a card that could not fire at all: the ward chills, the ward cannot
freeze, and nothing else they owned was cold. The one build where it did work was
Glacial Ward → Rimeblade → Deep Freeze, because the blade's burst is a blow. That
route is gone too, and that is the cost of the change, taken knowingly: a ward
knight who wants to stop a body now has to buy Frost Tip I first, which is one
Common. Rimeblade itself still opens off the ward, so the no-aim door still leads
somewhere — it just leads to damage rather than to a hold.

**The ceiling, and it is deliberate.** The longest drafted freeze is Frost Tip III's
10s chill + Deep Freeze III's 9s = **19s** (when the freezing arrow lands right
after the chilling one). Frost Bite bills it 3 a second and its ticks never wear the
ice down, so a full-rank freeze is **57 damage that cannot interrupt itself**.

Against the actual roster that is a real number without being a solved one:

| Body | HP | One full 19s hold |
|---|---|---|
| Bat | 15 | dead in 5s |
| Rat (brown/grey) | 20 | dead in ~7s |
| Rat (black), Dark bat | 30 | dead in 10s |
| Wolf (brown) | 30 | dead in 10s |
| Wolf (grey) | 45 | dead in 15s |
| Wolf (black) | 60 | **survives on 3 HP** — needs a second freeze |
| Ogre | 80 | **survives** |
| Bosses | 1500–2500 | held 9.5s, takes ~28. Nothing. |

Hoarfrost Band doubles the chill, so the same freeze becomes 29s and 87 damage — an
ogre dies inside one freeze. Heart of Ice adds 2s more.

Note the wolves: `EnemyWolf.Awake` overwrites the prefab's stale `health: 10` with
30/45/60 at runtime, so the prefab YAML lies about the toughest ordinary mob in the
game. Read wolf health from the script, never the prefab.

A second freeze is always available, because the thaw reprieve was removed — so the
tough half of the roster dies to a *chain* of freezes rather than to one. What keeps
that from being free is pillar 2: only a blow can freeze, so a knight holding one
ogre down is spending an arrow a time the rest of the wave does not get.

**None of this is a defect, and it should not be quietly sanded off.** A player who
builds all the way to Deep Freeze III plus Frost Bite has spent most of a run's picks
on one idea, and watching the board stop moving is the payoff for that — finding the
combination is the fun. The numbers are written down here so the shape is *known*,
not so someone tunes it back down. If it is ever genuinely wanted lower, the one
number is `FrigidBoost.FrostBiteFrozenDps`.

Cold cannot break its own ice: `ApplyFrostDamage` never calls
`BreakFreezeIfHardEnough`. Ice damage adds up across hits, so if the ticks counted
the capstone would wear down and break every statue it creates.

Frost Tip I ──────────────────────► Deep Freeze I ──► Deep Freeze II
      └────────────────────────────► Shatter I ──► Shatter II
(Frost Tip I OR Glacial Ward I) ──► Rimeblade I ──► Rimeblade II ──► Rimeblade III

Deep Freeze I ──► Deep Freeze II ──► Deep Freeze III

Frost Bite: requiresOrderCount 4, no prerequisites
```

Cross-family parents gate availability but correctly do **not** extend pip counts —
`UpgradeManager.GetChainDepth` skips parents of a different type.

## What was built

**New** (`Assets/Scripts/`): `FrigidBoost.cs` (the stat sheet and every tuning
const), `FrostFx.cs`, `GlacialWard.cs`, `IChillable.cs`, plus `FrostTipUpgrade`,
`GlacialWardUpgrade`, `DeepFreezeUpgrade`, `ShatterUpgrade`, `RimebladeUpgrade`,
`FrostBiteUpgrade`. One class per *discipline*; tiers are assets, because
`UpgradeManager` keys per-draft uniqueness and chain grouping off `GetType()`.

**Assets**: 13 SOs in `Assets/Upgrades/Frigid Ups/`, all `order: 6`, all registered
in `Assets/Resources/UpgradeManager.asset` (pool 85 → 98).

**Hooks:**

- `EnemyBase` — the Chilled/Frozen state, `ApplyCold`/`Freeze`/`Thaw`/`PurgeFrost`,
  the slow pass in `LateUpdate`, the tells, and the freeze-break rule.
- `SleepFreeze` — generalised to serve both holds via `EnemyBase.IsHeld`. Its
  `Release` now refuses while the *other* hold is still running, so a shattered
  statue that is also carrying a dart does not walk away wearing the Zs.
- `PlayerProjectile` — shatter, splinters, cold on hit.
- `PlayerShooter` — the sheet carried on all four spawn sites.
- `SwordSwing` — Rimeblade, plus the blade's own shatter.
- `MineCart` / `EnemyMineCart` — cart slow and the jam.
- `CollectibleOrb`, `ProjectileMovement`, `EnemyPickaxe`, `EnemyBomb`,
  `EnemyFireball` — `IChillable`.
- `Spawner.BeginWave`, `EnemySlime.Split`, `GiantSlimeDuel`, `PurgeStatusEffects` —
  the edge cases below.

### The three places a slow half-lands, and what was done

1. **Carts.** `MineCart` owns its position absolutely from `_travelled`, so the
   delta trick does nothing to one — `EnemyMineCart` overrides `AcceptsChill` to
   false and the cart scales its own travel instead. The multiplier is applied to
   the *travel*, never to `speed`: `EnemyDarkGnomeCart`'s last stand already
   multiplies that field, and `LeaveWreck` hands it to the wreck, so a chill written
   into it would follow a wreck around for the rest of the wave.
2. **Screen entrances.** `EnemyRat`, `EnemyBat` and `EnemyDarkBat` all fly in on a
   `Vector3.Lerp` to an absolute position. Scaling deltas there does not delay
   anything — the lerp rewrites the position next frame regardless — so all it buys
   is a stutter. `positionIsScripted` holds the cold off until the entrance is done.
3. **Slime splitting.** `Split()` instantiates the whole parent, tint and ice
   included, so `PurgeFrost()` joins its manual reset list.

### The tells

Three channels, each picked because nothing else uses it:

- **`spriteRenderer.color`, not `GlowManager`.** `StartGlow` is first-come and
  exclusive, so a frost glow would be swallowed by the red hit flash half the time
  and would block it the rest. No enemy code writes the sprite colour, so cold gets
  a channel that never fights poison or fire — a body can honestly be green, on
  fire, and blue at once.
- **`animator.speed`**, which nothing in the codebase assigns. A walk cycle at full
  tempo on a half-speed body reads as ice-skating; this is the whole difference
  between "slowed" and "broken". Zero while frozen — a statue that twitches is a bug.
- **`FrostFx`** motes that *sink* (fire licks upward, Dawn rises; cold falls — the
  one line that stops this reading as blue fire), and a brighter rim-only shell once
  a body is actually stopped.

**No new art.** Everything is the shared white `EmberMote` alpha mask tinted cyan.
That sprite was authored as a code-tintable mask, so this is its intended reuse.

### Sound

Three cues, not one per discipline — the player should learn "cold landed", "it
stopped" and "it broke", never five separate sounds for one Order.
`frost_chill.wav` / `frost_freeze.wav` / `frost_shatter.wav`, wired as
`AudioManager.frostChill` / `frostFreeze` / `frostShatter` and set on the
AudioManager in **both** Main and Camp.

**All three are unauditioned.** They compile, they are under the clipping ceiling
(peaks 0.42 / 0.69 / 0.60), and nobody has heard them in a wave.

## The quest line (`FrigidQuests.cs`)

Five quests, the same shape as Serpent and Ember: a light stat trigger reveals the
initiation, the initiation admits you, admission opens two branches.

The branches are the argument Shatter puts in front of the player on every frozen
body. **Rime** says a stopped enemy is the point. **Silence** says a stopped enemy
is a setup. The line lets you finish both, but it makes you notice you chose.

| Quest | Objective | Reward |
|---|---|---|
| Initiation: The Held Breath | 100 chilled, 50 frozen, 5 Frigid upgrades | 2 crystals |
| Take Their Evening | 200 frozen | **Hoarfrost Band** |
| The Standing Field | hold four in ice at once | **Winter's Tooth** |
| Brittle Things | 100 shattered | 1 crystal |
| Nothing Moves | acquire Deep Freeze III | **Heart of Ice** |

`upgrades.order.frigid` and `upgrades.taken.<slug>` come free from
`UpgradeManager.ApplyUpgrade`; `frigid.chilled`, `frigid.frozen` and
`frigid.shattered` are published by `EnemyBase` and `PlayerProjectile`. Chilled
counts each body once in its life, matching how the poison and ignite tallies work.

**`Feats.FrozenFour`** is the one objective needing a real detector. Four at once is
the Order's showpiece image, so it is a feat rather than a counter. `EnemyBase`
keeps a live `_liveFrozenCount` on both sides of the hold rather than sweeping the
field on every freeze — a Frigid knight freezes often enough that a
`FindObjectsByType` each time would be a cost the player can feel. `OnDeath` hands
the tally back, because a shattered body never reaches `Thaw` on its own once
`isDead` makes `IsFrozen` read false.

### The three items

All three bend the Frigid draft ×2, the way Everburning Coal does for Ember — an
Order's items should make that Order easier to keep building.

- **Hoarfrost Band** — chill lasts **twice as long**.
- **Winter's Tooth** — one Frost Tip rank from wave one, stacked on top of drafted
  ranks, so the full chain with the Tooth reaches rank IV.
- **Heart of Ice** — freezes hold +2s.

Chill is **scaled** rather than topped up, and freeze is topped up. That asymmetry
is deliberate. The Frost Tip chain's whole shape is in how long the cold lasts —
3s to 6s to 10s — so a flat "+2 seconds" would be most of rank one and almost
nothing by rank three, which quietly makes the Band a beginner's trinket you stop
carrying. Doubling is worth the same at every rank: 3→6, 6→12, 10→20. Since
2026-09-11 a freeze lasts as long as the chill had left, so the Band lengthens
freezes too.

Both ride separate fields on `FrigidBoost` rather than inflating a rank, because
rank also drives chill *depth* and whether the knight can freeze at all — a band
that "gives you Frost Tip II" to buy duration would quietly hand over the slow as
well. Same reason Ember keeps its zone dps bonus off its level counter.
`Heart of Ice` does nothing without Frost Tip, by design: a knight who cannot
freeze is not handed a freeze by an item. (Before 2026-09-11 it needed Deep Freeze,
which used to be what enabled freezing.)

## Open / next

- **The icons are drawn but unjudged in situ.** Six 16×16 `.aseprite` icons in
  `Assets/Graphics/Equipment/`, in the house style (three-tone Order ramp inside a
  hard `#14100A` outline), corner-alpha-guarded so Unity's tight crop does not
  import them at six different sizes. All six verified importing at 16×16 and
  wired. **`oathbound_locket` is the weakest of the six** — it reads as two shells
  more than as a locket, and is the one to repaint first.
- **Nobody has watched a Frigid run.** The mechanics are proven programmatically —
  chill, freeze, the pillar-2 refusal, the blow-breaks-ice rule, cart jamming — but
  mote density, how cyan reads against the Mine's dark purple, and whether Chilled
  and Frozen are actually distinguishable in a swarm are all guesses.
- **Tuning is expected.** Frost Tip and the ward radius were both already pulled
  back once. The next thing to watch is Glacial Ward II plus Frost Tip: the ward
  keeps everything in the ring chilled, so every arrow that lands there freezes.
  That is the intended synergy, but it is the combination most likely to overshoot.
- **A Frigid duo upgrade** is the natural next slot — duos are named as an unbuilt
  phase in `orders-and-the-full-run.md` §1, and Frigid + Ember ("thermal shock")
  writes itself.
- **Guardian still has no quest line, and no capstone.** `QuestDatabase.Build()`
  now registers Forest, Mine, Serpent, Ember, Shadow, Frigid and Dawn. Guardian is
  the last Order with neither, and it is still six upgrades short of finished.
- **Pool size.** `orders-and-the-full-run.md` says the pool stays curated at ~60–65
  assets across five Orders plus Neutral. It is at 98 across six. That ceiling has
  been quietly abandoned and the vision doc should either say so or say why not.
