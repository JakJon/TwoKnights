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

**1. Frost has no damage of its own.** Every number in the Order is a second, a
percentage of speed, or a multiplier on a blow *you* land. Frigid never ticks.
Serpent and Ember already own damage-over-time between them, and a third one would
be a re-skin wearing a new colour.

The exceptions prove it rather than breaking it. **Shatter** is damage the player
aimed and landed on a body they chose to freeze. **Rimeblade** deals the sword's
own damage in a wider circle. Neither is a timer paying out.

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
- **A thaw buys a reprieve — but only a thaw.** For 2s after a freeze *runs out*,
  that body cannot be chilled again. A freeze that was *broken* earns nothing,
  which is what lets a frost arrow shatter a statue and re-chill it in the same
  instant. That asymmetry is the engine of the Order's whole loop.
- **Any blow of 5 or more ends a freeze**, whether or not the knight owns Shatter.
  A statue you can hit for twenty and watch stand there reads as a bug. Chip damage
  stays under the bar on purpose, so a burning body burns through the whole hold —
  and the poison and fire ticks bypass `TakeDamage` entirely in any case.
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
| Deep Freeze I | 55 | Rare | Frost Tip I *or* Glacial Ward I |
| Deep Freeze II | 26 | Epic | Deep Freeze I |
| Shatter I | 55 | Rare | Deep Freeze I |
| Shatter II | 24 | Epic | Shatter I |
| Rimeblade I | 55 | Rare | Frost Tip I *or* Glacial Ward I |
| Rimeblade II | 26 | Epic | Rimeblade I |
| Rimeblade III | 14 | Legendary | Rimeblade II |
| Permafrost | 10 | Legendary | `requiresOrderCount: 4` |

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

**Deliberately short of stopping anything.** The Order already has a way to stop a
body and it costs a second blow. If rank III left a wolf crawling, Deep Freeze would
be buying almost nothing and the two-state reading would collapse back into "cold is
a slow that gets slower". The gap between rank III and frozen is what Deep Freeze is
paid for.

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
landing anywhere near a knight froze — which quietly made Deep Freeze automatic
rather than something to aim. The ward is a *last line*: the cold a body walks into
once it is already close enough to be a problem.

The ward never freezes. See pillar 2.

### Deep Freeze I–II — the second state

Cold landing on an already chilled body **freezes** it. Held where it stands.

- **I** — 3s.
- **II** — 6s.

This is the pick that turns the Order on. Everything before it is a slow; everything
after it is a stop. Gating it behind a Rare means jamming the mine's carts is a
deliberate build choice rather than something that happens to a player who took one
Common.

It opens off **either** door, so a ward-first build can still reach the stop —
`unlockedBy` is any-of, which is exactly what that needs.

**The carts are the showcase.** A held cart anchors `MineCart`'s spacing sweep, so
the run behind it stacks up against it: one arrow turns the mine's signature hazard
into a wall of the player's own making. That is Frigid's equivalent of the burning
racetrack Fire Trail draws around the Rat King, and it needed almost no new code —
`HoldFor`, `IsHeld` and `Movable` were already there for the sleeping dart.

### Shatter I–II — the only damage in the Order

A blow landed on a body held in ice breaks it, hard.

- **I** — the landing blow deals ×2.
- **II** — ×3, and the breaking ice throws splinters that **chill** everything
  nearby.

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
Deep Freeze owned, a swing into a body an arrow chilled on the way in stops it dead
at arm's length — the strongest single moment in the Order, and it costs two picks
from different chains to reach.

Damage lands **before** the cold, the same order an arrow uses. Chilling first would
let one swing freeze a body and then break its own ice on the very next line.

### Permafrost — capstone (requires 4 Frigid picks)

**Frozen things do not thaw.** A freeze lasts until something breaks it.

The board fills with statues and the knights take them apart in whatever order they
like. It is the literal end state of "you take the enemy's time", the way Scorched
Earth is the end state of "you burn the arena". It also makes Shatter the whole
endgame, since breaking the ice becomes the only way a statue ever moves again.

**It buys safety, not speed.** Frigid deals no damage of its own, so the arrows
still have to do all the killing and the wave takes exactly as long. A Permafrost
knight is not faster, just untouched.

Two things it demands, both paid:

- **The hold is a very long deadline, not a second code path**
  (`EnemyBase.PermafrostSeconds`), so nothing downstream — the pin, the cart holds,
  the tells — has to learn the capstone exists.
- **The ice dies with the WAVE, not the run.** `FrigidBoost.ClearFieldFrost()` is
  called from `Spawner.BeginWave` beside `FireField.ClearAll()`. Without that
  second half, wave twelve begins inside wave eleven's statues and the difficulty
  curve inverts.

**Balance watch:** this is the upgrade most likely to make late waves easier than
mid waves. If it over-performs the lever is the thaw reprieve or the freeze
duration, never the mechanic — the field of statues is the fantasy worth protecting.

### Unlock DAG

Starting picks: **Frost Tip I, Glacial Ward I**.

```
Frost Tip I ──► Frost Tip II ──► Frost Tip III
Glacial Ward I ──► Glacial Ward II

(Frost Tip I OR Glacial Ward I) ──► Deep Freeze I ──► Deep Freeze II
                                          └────────► Shatter I ──► Shatter II
(Frost Tip I OR Glacial Ward I) ──► Rimeblade I ──► Rimeblade II ──► Rimeblade III

Permafrost: requiresOrderCount 4, no prerequisites
```

Cross-family parents gate availability but correctly do **not** extend pip counts —
`UpgradeManager.GetChainDepth` skips parents of a different type.

## What was built

**New** (`Assets/Scripts/`): `FrigidBoost.cs` (the stat sheet and every tuning
const), `FrostFx.cs`, `GlacialWard.cs`, `IChillable.cs`, plus `FrostTipUpgrade`,
`GlacialWardUpgrade`, `DeepFreezeUpgrade`, `ShatterUpgrade`, `RimebladeUpgrade`,
`PermafrostUpgrade`. One class per *discipline*; tiers are assets, because
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
| Nothing Moves | acquire Permafrost | **Heart of Ice** |

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
- **Winter's Tooth** — Frost Tip I from wave one.
- **Heart of Ice** — freezes hold +2s.

Chill is **scaled** rather than topped up, and freeze is topped up. That asymmetry
is deliberate. The Frost Tip chain's whole shape is in how long the cold lasts —
3s to 6s to 10s — so a flat "+2 seconds" would be most of rank one and almost
nothing by rank three, which quietly makes the Band a beginner's trinket you stop
carrying. Doubling is worth the same at every rank: 3→6, 6→12, 10→20. Freeze has no
such spread (3s or 6s), so seconds are honest there.

Both ride separate fields on `FrigidBoost` rather than inflating a rank, because
rank also drives chill *depth* and whether the knight can freeze at all — a band
that "gives you Frost Tip II" to buy duration would quietly hand over the slow as
well. Same reason Ember keeps its zone dps bonus off its level counter.
`Heart of Ice` does nothing without Deep Freeze, by design: a knight who cannot
freeze is not handed a freeze by an item.

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
  back once. The next thing to watch is Glacial Ward II plus Deep Freeze: the ward
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
