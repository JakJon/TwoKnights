# The Guardian Order — Design & Build Plan

*Finished 2026-09-08. The sixth Order in the roster from `orders-and-the-full-run.md`
§2, and the last one to be completed. Greatshield, Bowsight and Long Sword predate this
pass; Reflector, Guided Shot, Guided Reflections and the capstone were built into it.*

## The concept: the Order that buys the landing

Every other Order in the game buys a **bigger number on a shot the player already had
to land**. Serpent adds rot to a hit, Ember adds burn, Frigid adds cold, Shadow adds
volume, Dawn adds health afterwards. All five assume the arrow connected.

Guardian buys the connecting. The guard is wider, so more of what arrives is stopped.
The arrow bends, so more of what is loosed lands. The rock comes back, so the ammunition
being thrown at the knight becomes the knight's. Nothing in the Order asks the player to
aim better than they already do — which is exactly why it is the Order you can draft
without meaning to and still be rewarded for.

That also makes it the natural partner Order. A Guardian knight beside a Serpent one is
landing more of the poison; beside an Ember one, more of the fire. Guardian is the
multiplier that never appears in anyone's damage number.

## THE PILLAR, in two parts

Both live as a comment on `GuardianBoost`, and neither may be relaxed.

1. **Guardian never asks the knight to aim better.** Every number in the Order pays for
   a shot landing or a threat being stopped, never for precision the player has to
   supply. The moment an upgrade here reads "if you hit the weak point", it belongs to
   a different Order.
2. **Nothing rewards being hit.** A block is a mistake the guard covered for, not an
   achievement. The Order pays out for what the guard **sends back**, never for the
   fact that it was struck.

Part 2 has a body buried under it. **Stalwart** — "blocks charge the special" — was
designed, costed and cut, because it paid the player for taking incoming fire and would
have made the correct play "stand in it". The capstone is part 2 taken to its end
instead. Don't put Stalwart back.

### Why the reflect rolls when the rocks do not

`RockVariant` carries the game's loudest design rule: **a count, never a roll**. What
the shafts throw is a cadence the player can count toward, and it must stay one.

Reflector rolls anyway, and the distinction is deliberate: the rule is about *what the
game throws at you*, not *what your build does with it*. The wave is still fully
deterministic — the same rocks arrive on the same beats whether or not a knight has
Reflector. The roll is entirely inside the player's own kit, where Venom Tip and
Rimeblade already establish the shape. And the last rank is certainty rather than a
better coin, so a finished chain is a promise the player can build around rather than
a dice game they keep playing.

## The roster (17 upgrades)

Accent `rgb(158, 168, 178)` — steel (`order--guardian`, already in `UpgradeMenu.uss`).
Weight → rarity via `BaseUpgrade.Rarity`.

### Greatshield I–III — the guard's shape *(pre-existing)*
Each tier lengthens the span and bows the bar around the knight; the bow never costs
span. Lives on the shield GameObject as `ShieldShape`, which regenerates the sprite and
its `PolygonCollider2D` from one arc so the shape that stops an arrow is the shape on
screen. Assets stay named `Greatshield N`; the cards read "Dawn Shield".

### Bowsight I–II — the shot's line *(pre-existing, retagged this pass)*
A laser sight on the shield that reads the line of fire and cooks what it rests on.

**These were tagged `order: 0` (Neutral) despite living in the Guardian folder**, so
they earned no Guardian affinity and counted toward no capstone gate. Retagged `order: 3`
on 2026-09-08. Stat slugs derive from the *filename* (`UpgradeManager.StatSlug`), so no
save progress moved.

### Long Sword I–III — reach for speed *(I–II pre-existing)*
Length and slowness compound; base damage is set outright (15 → 20 → 26). Rank III is
roughly 2.35× the stock reach at nearly half the swing speed — a genuinely heavy trade,
which is what a third tier of a two-tier chain has to be to be worth drafting.

### Reflector I–II — the guard pays out
| Tier | Chance | Damage | Weight |
|---|---|---|---|
| I | 50% | ×1.0 | 60 (Rare) |
| II | 100% | ×2.0 | 16 (Legendary) |

**Two picks, not three** (owner, 2026-09-09). It shipped as 30 / 60 / 100 across three
tiers and both halves of that were wrong. At thirty percent rank one was a thing that
occasionally happened rather than a thing to play around, and the chain's selling point
is that the guard becomes an *answer* — you cannot aim a return line you only get one
block in three. A coin flip is the floor at which a player starts angling the shield on
purpose. With rank one raised, the middle tier became a step from "usually" to
"usually", so it was cut outright rather than retuned. The chain now reads *maybe*, then
*always*, and the second pick is a **Legendary** because certainty is the whole of what
it sells.

The old rank III asset is what became rank II: the file keeps the name `Reflector 2` on
disk after the middle asset was deleted, and `GuardianBoost` answers any rank above 2 as
2 so a save from before the change never reads as no Reflector at all.

**Everything thrown at a knight is fair game** (owner, 2026-09-09). Rocks, a gnome's
pickaxe, a giant slime's fireball, a cart's bomb — anything that lands on the guard can
come back off it. Until that date it was rocks alone, which was never a rule anybody
wrote down; it was simply where the code lived, inside `ProjectileSettings`. The roll,
the mirror and the credit now live in `GuardianReflect`, and `ReflectedShot` is what a
returned pickaxe or fireball *becomes*: the behaviour that flew it in is switched off,
which is also what stops it hurting a knight or being blocked a second time, and the
object keeps its own art so the player reads it by its heading and its pace. The one
exemption is a giant slime's **ward** fireball, which is the boss's guard rather than a
shot at anybody — popping it is what opens the damage window, so it always pops.

A reflected bomb is the odd one: its powder normally only ever hurts knights (a gnome's
blast clearing the gnome's own carts would be doing the player's work), and once the
guard has batted it away it is the one case where that blast bills bodies instead.

A blocked rock is mirrored about the shield's outward face normal and leaves at ×2.5
speed. **It keeps the payload it was thrown with** — a plain stone hits like an arrow, a
green one rots what it lands on, a powder one actually detonates — so the mine's own
escalation is what makes this chain scale. A Guardian knight deep in The Mine is being
handed better ammunition by the level itself.

The mirror is the right read because it makes the guard's *angle* mean something: block
head-on and the rock goes back up the lane, block at a graze and it sprays off sideways.
A rebound that pointed inward would be the Order paying out in damage to its own knight,
so anything not clearly heading away is sent straight out along the normal instead.

**Two things this chain must never lose.** A reflected rock is destroyed 4 seconds after
the block whatever it hits — a wave is not complete while a tracked projectile lives
(`BaseWave.IsWaveComplete`), and every rock before this one was *guaranteed* to die
because it always ended on a knight or a guard. And the block still charges the special
by 1, before the reflect is even considered; routing that reward through the reflect
would have quietly deleted a charge source on every rebound.

A reflected powder rock is also **the only rock blast in the game that deals damage**.
The enemy's own powder rock stays cosmetic, because a gnome's blast clearing the gnome's
carts off the track would be doing the player's work for them — the same call
`EnemyBomb.Explode` makes.

### Guided Shot I–III — the line lands
| Tier | Radius | Weight |
|---|---|---|
| I | 0.5u | 60 (Rare) |
| II | 1.0u | 30 (Epic) |
| III | 1.6u | 16 (Legendary) |

The knight's main arrow bends onto the nearest mob **or orb** within the radius. Orbs
count because they are collected by shooting them, so bending onto one is the same
favour as bending onto a rat.

A tier raises the radius and nothing else. The turn rate is fixed at 720°/s, so what the
player buys is **how early the shot commits**, never whether it connects — the pillar
written as a number.

**The target is sticky.** A shot holds its target until it hits it or the target stops
existing; it never shops around. A shot that re-picked the nearest thing every frame
would weave between two rats and hit neither, and would stop being something the player
can predict. The one re-acquire that does happen is the useful one: an arrow that pops a
health orb survives it (`CollectibleOrb` destroys itself and lets the arrow fly on), so
the moment that orb is gone the arrow is free to find the next thing in front of it.

**Only the main shot steers.** Shadow arrows and shurikens fly straight — the same line
`PlayerProjectile.absorbsFieldEffects` already draws, and for the same reason: a steered
shuriken fan would clear a screen without anybody aiming at anything.

### Guided Reflections I–III — where the two halves weld
| Tier | Radius | Weight |
|---|---|---|
| I | 0.5u | 30 (Epic) |
| II | 1.0u | 16 (Legendary) |
| III | 1.6u | 12 (Legendary) |

A rock the guard turned around now steers the way the knight's arrows do, so a rebound
stops being a hopeful line off the shield face and starts finding a body.

This is **the only upgrade in the game that requires BOTH prerequisites rather than
either**. `unlockedBy` has always been ANY-of, which is right for a chain where rank II
opens off rank I; `BaseUpgrade.requiresAllUnlocks` was added for this one case and
defaults false, so nothing already authored changed. A knight who can only reflect has
nothing to steer, and one who can only steer has nothing to send.

Bodies only — a rock cannot collect an orb, so letting one chase an orb would send the
Order's payout somewhere it physically cannot land.

### Bulwark — the capstone
Weight 12 (Legendary), `requiresOrderCount: 5`.

Every knight in the game answers a body that reaches the guard the same way: the enemy
is deleted and the knight is charged health for it. It is a trade, and it is the trade
that kills runs.

Bulwark refuses it. **Nothing is paid and nothing is deleted** — the body is thrown two
units back off the shield, over 0.18s, and has to make the walk again. That is a real
cost as well as a real gift: the enemy is still alive, still coming, and still has to be
killed properly. What the knight has bought is that reaching the guard is no longer
worth anything to it.

**Bosses are shoved the full two units, and there is no cooldown on any shove**
(owner's call, 2026-09-08). Know what that buys: the Crimson Twins approach at
0.58 u/s and their config calls that walk *the fight timer*, so every contact rewinds
their clock by about three and a half seconds — and the guard orbits between the
knight and the slime, so a player holding the shield toward one can keep it off more
or less indefinitely. That is the intended power level and it is not a defect: a player
who has drafted the Guardian capstone and works out that the guard can hold a boss
off the whole fight has found something, and finding it is the point. The lever is
the `Shield` branch in `EnemyGiantSlime` rather than `Shove`, if it is ever genuinely
wanted lower.

That branch had to be added at all because `EnemyGiantSlime` and `EnemyRatKing` both
override `OnTriggerEnter2D` wholesale and never call base — both were written to say
"a boss cannot be popped by contact", long before this capstone existed, so neither
had a `Shield` branch and Bulwark silently did nothing to either boss. The logic now
lives in `EnemyBase.TryBulwarkShove` so all three handlers share it; **anything that
overrides the trigger from here on has to call it, or it quietly opts its enemy out.**

Going without a cooldown is safe because the shield carries exactly one trigger
(`ShieldShape` disables the authored capsule so a block cannot fire its callbacks
twice), and because `Shove` *replaces* the outstanding displacement rather than
adding to it — a body shoved twice inside one payout window travels two units from
wherever it had got to, never four.

The shove is applied after the subclass's own movement, exactly as Searing Panic and the
chill slow are, so it reaches every enemy that moves itself — rat, bat, wolf, ogre,
slime, and both bosses — without a single subclass being taught anything. It is refused
for anything whose position is scripted (a cart on rails cannot be pushed off its
track), and it carries the freeze and sleep anchors with it so a body shoved and then
held doesn't get dragged back to where it was standing.

## The tells

`GuardianFx` — code-built, no prefab and no art of its own, the same arrangement as
`FireFx` / `FrostFx` / `ShadowFx`, borrowing their mote sprite. Steel `#9EA8B2` over the
shield's gold trim `#D4A24A`, which are the Order's two existing colours.

Both tells answer one question — *this thing is yours now*. A rock that has been turned
around looks exactly like a rock, and an arrow that has committed looks exactly like an
arrow; without a tell the player cannot tell either from what it was a frame ago. That
is the whole job, and neither should grow into a light show.

Two sounds, both deliberately near the noise floor: `guardian_reflect` (a short bright
flick with an upward pitch) and `guardian_guide` (a soft sine tick on lock). A Reflector knight
turns rocks around all wave and a guided knight locks on with most shots they take, so
these are textures rather than events — the particles are the loud half of each tell.
The lock chime is additionally rate-limited across every arrow on the field
(`GuidedShot.TellCooldownSeconds`), the same treatment the burn tick gets.

## What is still open

- **No Guardian quest line.** `Assets/Scripts/Quests/Lines/` has Dawn, Ember, Forest,
  Frigid, Mine, Serpent and Shadow. The stat key `upgrades.order.guardian` already
  exists, so there is something to gate on.
- **Duo upgrades** (`orders-and-the-full-run.md` §1) are now fully unblocked — Guardian
  is the second half of the best-named pair in the doc, *Guardian of Thorns*, and
  Reflector is the mechanic it would hang off.
