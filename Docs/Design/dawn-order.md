# The Dawn Order — Design & Build Plan

*Built 2026-08-06. The fifth and last Order in the roster from
`orders-and-the-full-run.md` §2, and the only one whose value routes through the
other knight.*

## The concept: you don't heal yourself, you hold each other up

Serpent wins by patience. Shadow wins by volume. Ember wins by owning the ground.
Dawn wins by **refusing to lose the pair** — it is the only Order whose best
numbers are paid to someone other than the knight who drafted it.

That matters because Two Knights has exactly one failure state that counts: a
knight dying ends the run for both. Every other Order answers that by killing
things faster. Dawn is the only one that answers it directly, and it does so in
the shape of the game's title — the run's story becomes the pair, not the build.

A knight who drafts Dawn selfishly gets mediocre sustain. A pair who both lean in
get the strongest defensive engine in the game, and Shared Light II is the moment
that flips: once overflow routes across, two Dawn knights stop having separate
health pools in any way that matters.

## THE PILLAR: no heal without a source, and nothing is wasted

Two halves, both inviolable:

1. **Every point Dawn pays out is bought by something the player did** — an orb
   they shot, a kill they counted, a special they spent, a blow they survived.
   **Dawn has no passive regeneration**, and must never grow one. Health that
   arrives on a timer is health the player didn't earn, and it would turn the
   Order from a set of decisions into a slow drip that makes every other decision
   matter less.
2. **Dawn never lets healing evaporate.** At full health, Shared Light II hands
   the whole overflow to the partner rather than dropping it on the floor. This
   is the half that makes the Order feel generous rather than fiddly.

`DawnBoost` and `PlayerHealth` both carry this comment. Nothing may relax either
half.

### Why the cadences are counted, not rolled

Lifebloom fires on every Nth kill, not on an N% chance. Ember's Fireball
established the rule: a payoff the player can *count toward* is a decision, and
the identical payoff delivered by dice is weather. Dawn's whole job is to make
the player feel like the pair is being held up deliberately, so nothing in the
Order rolls.

Second Wind and Last Light are the same idea at a longer scale — once per wave and
once per map are budgets the player can spend on purpose, not procs they hope for.

## The roster (12 upgrades)

Accent `rgb(240, 200, 170)` (`order--dawn`, already in `UpgradeMenu.uss`).
Weight → rarity via `BaseUpgrade.Rarity`.

### Sunwell I–III — the orb door
Health orbs are collected by **shooting** them (`CollectibleOrb`), so this chain
pays for aim rather than for standing still. It is the cheap, obvious entry to
the Order.

| | Effect | Weight |
|---|---|---|
| I | Orbs heal +50% (20 → 30) | 110 |
| II | +100% (20 → 40); mana orbs also mend 5 | 70 |
| III | +150% (20 → 50); orbs travel 35% slower | 40 |

Rank III is the one Dawn effect that lands on the **field** rather than on a
knight: a slowed orb is a wider window for *both* knights. That is deliberate —
even the selfish-looking tier of the orb chain ends up shared.

### Shared Light I–II — the partnership door *(signature)*
| | Effect | Weight |
|---|---|---|
| I | 40% of any healing you receive is echoed to the other knight | 100 |
| II | 80% echoed; healing taken at full health goes across **whole** | 55 |

Because `PlayerHealth.Heal` is the single funnel every heal in the game passes
through, this catches orbs, specials, Lifebloom, Second Wind, equipment, and
anything added later, for free.

The echo is **not itself echoable** — `PlayerHealth.Heal` passes
`allowEcho: false` for it, so two Dawn knights lift each other once instead of
bouncing one heal between them forever. Rank II's routed overflow **replaces**
the fractional echo rather than stacking with it: the promise is "the whole
amount goes across", not "the whole amount plus a share of it on top".

### Lifebloom I–II — the kill door
| | Effect | Weight |
|---|---|---|
| I | Every 12th enemy you kill mends 3 | 105 |
| II | Every 8th kill mends 4 | 60 |

Rides `EnemyBase.OnEnemyKilledBy`, the same kill-credit event Thousand Cuts and
Blood Tithe already use.

### Second Wind I–II — the panic door
| | Effect | Weight |
|---|---|---|
| I | First time per wave you fall to ≤30% HP: untouchable 1.2s | 90 |
| II | ≤45%, 2s, and mends 5 | 45 |

It answers the blow rather than preventing it — the hit that takes you low still
lands, which keeps the moment legible. Reuses `PlayerHealth.SetInvulnerable`, the
same deadline-based window Iron Vigil opens, so the game has exactly one
untouchable system. The charge is rearmed in `Spawner.BeginWave`.

### Benediction I–II — the special door
| | Effect | Weight |
|---|---|---|
| I | Firing your special also mends the other knight 15 | 65 |
| II | 30, and both knights are untouchable 1s | 35 |

Hooked once in `PlayerSpecial` after `_special.Activate(...)`, so it works with
Rapid Fire, Field Mending, and anything a later loadout adds without those
specials knowing Dawn exists. Rank II raises the number rather than making the
special cheaper — Dawn buys resilience, never tempo.

### Last Light — capstone (requires 4 Dawn picks)
Once per map, when the **other** knight would die, they hold at 1 HP, are mended
25, and both knights get 2s untouchable. Weight 15 (Legendary).

It saves the **partner**, never its owner. A capstone that insured its buyer
would be a safety net; one that insures the person next to you is a vigil, and
only the second one is Dawn. Implemented from the dying knight's side in
`PlayerHealth.TakeDamage`, which reads the *partner's* `DawnBoost` before the
death branch.

*(This is the doc's original "Guardian's Vigil", renamed so it doesn't collide
with the Guardian Order.)*

### Unlock DAG
Starting picks (no prerequisites): **Sunwell I, Shared Light I, Lifebloom I**.

```
Sunwell I ──► Sunwell II ──► Sunwell III
    └────────► Benediction I ──► Benediction II
Shared Light I ──► Shared Light II
    └────────► Second Wind I ──► Second Wind II
Lifebloom I ──► Lifebloom II
Last Light: requiresOrderCount 4, no prerequisites
```

The two cross-family doors gate availability but correctly do **not** extend pip
counts — `UpgradeManager.GetChainDepth` skips parents of a different type.

## What was built

**New** (`Assets/Scripts/`): `DawnBoost.cs` (the stat sheet, mirroring `EmberBoost`
and `NinjaBoost`), plus `SunwellUpgrade`, `SharedLightUpgrade`, `LifebloomUpgrade`,
`SecondWindUpgrade`, `BenedictionUpgrade`, `LastLightUpgrade`. One class per
*discipline*; tiers are assets, because `UpgradeManager` keys per-draft category
uniqueness and chain grouping off `GetType()`.

**Assets**: 12 SOs in `Assets/Upgrades/Dawn Ups/`, all `order: 5`, all registered
in `Assets/Resources/UpgradeManager.asset`.

**Hooks** (four existing files):
- `PlayerHealth` — `Heal(int, bool)` overload carrying the Shared Light echo and
  overflow routing; `TrySecondWind()` and `TryLastLightRescue()` in `TakeDamage`.
- `CollectibleOrb` — scales the heal by the **collecting** knight's sheet; asks
  `DawnBoost.AnyKnightSlowsOrbs()` once at spawn.
- `PlayerSpecial` — `PayBenediction(tag)` after the special activates.
- `Spawner.BeginWave` — `DawnBoost.OnWaveStarted()` rearms Second Wind.

**No art was needed** — `BaseUpgrade` has no icon field; cards render from the
Order colour and diamond, and `order--dawn` was already styled.

## The holy light (`DawnFx.cs`)

Code-built, mirroring `FireFx` / `ShadowFx`: no prefab, no new art, just the
shared white `EmberMote` alpha mask tinted to the Order's gold. That sprite was
authored as a code-tintable mask, so this is its intended reuse rather than a
restyle of fire's art.

**Everything rises, and everything dies bright.** Fire licks upward and cools to
ash (`FireFx`'s gradient ends at soot); Dawn rises and fades to white. Same
sprite, opposite ending — that contrast is why the two Orders don't read alike.

| Surface | Hook | Look |
|---|---|---|
| `Blessing(knight)` | `PlayerHealth.Heal` | one-shot ring of motes ascending around the knight |
| `ShowInvulnerableAura(knight, s)` | `PlayerHealth.SetInvulnerable` | sustained halo for the length of the window |
| `AttachOrbGlow(orb)` | `CollectibleOrb.Initialize` | a slowed orb emanating light, leaving a wake |

All three hang off **funnels rather than individual effects**, which is the point:

- Any heal reaching a knight who owns *any* Dawn upgrade glows, so no Dawn
  effect — nor any added later — has to remember to ask. Non-Dawn knights heal
  silently, exactly as before.
- Untouchability is one thing to the player however it was bought, so Second
  Wind, Benediction II, Last Light **and Iron Vigil** all share the halo. The
  aura is sized from the *remaining* window, so an overlapping call extends it
  rather than cutting it short, and it replaces rather than stacks.
- The orb glow is attached where the slow is applied, because a slowed orb has
  to *look* different or Sunwell III is invisible until you do the arithmetic.

Emitters use `Circle` with `radiusThickness: 0` (rim only) for the halos and a
filled circle for the orb, sized off the target's `SpriteRenderer` bounds — and
`ParticleSystemScalingMode.Hierarchy` on the parented aura, the lesson `FireFx`
learned when local scaling shrank the band on scaled enemies.

**Quest keys**, all free or nearly so: `upgrades.order.dawn` and
`upgrades.taken.<slug>` come from `UpgradeManager.ApplyUpgrade` with no work, and
`DawnBoost`/`PlayerHealth` add `dawn.lifebloom`, `dawn.second_wind`,
`dawn.benediction`, `dawn.shared_light`, `dawn.last_light`.

## The quest line (`DawnQuests.cs`)

*Added 2026-09-07, thirteen months after the Order shipped — Dawn had 12 upgrades
and no quests at all, and neither did Guardian.*

Five quests, the house shape: a light stat trigger reveals the initiation, the
initiation admits you, admission opens two branches.

Dawn's branches are not a disagreement about tactics the way Frigid's are. They are
a disagreement about **who the light is for**. Vigil says it is for the other
knight. Wellspring says you cannot pour from an empty cup. That is the Order's own
pillar argued from both ends, which is the only way to make a line about healing
have any tension in it at all.

| Quest | Objective | Reward |
|---|---|---|
| Initiation: The Kept Watch | 100 echoed heals, 50 mending kills, 5 Dawn upgrades | 2 crystals |
| The Longer Half | 300 echoed heals | **Warm Lantern** |
| Both Of You, Standing | the feat below | **Oathbound Locket** |
| Draw From The Well | 200 mending kills | 1 crystal |
| The Last Light | acquire Last Light | **Dawnbreak Crown** |

Every objective except the feat was already being published — `dawn.shared_light`,
`dawn.lifebloom`, `upgrades.order.dawn` and `upgrades.taken.last_light` have been
incrementing since the Order shipped and nothing had ever read them. They were also
**unnamed in `StatsDatabase`**, so they would have rendered as raw keys; all six
`dawn.*` stats now have display names.

### The feat (`Feats.DawnPairPulledBack`)

**A wave that drove a knight to a quarter health and still ended with both of them
at full.**

It is a feat rather than a counter because it is a "can you", not a "how much" —
surviving is common; pulling the pair back from the edge inside the same wave is
something you either managed or did not. And it cannot fall out of any existing
tally: nothing else in the game knows what the pair looked like at the start of a
wave and again at the end of it.

`DawnVigil` in `Feats.cs` is the detector, wave-scoped static state on the same
discipline as `RunPurity`. `Spawner` opens and closes the window; `PlayerHealth`
reports the low-water mark **as it happens**, because a knight can dip to a sliver
and be mended back inside one second and no end-of-wave reading would ever see it —
which is exactly the moment the feat is about.

### The three items

All three bend the Dawn draft ×2, the way Everburning Coal does for Ember.

- **Warm Lantern** — a quarter of every heal echoes to the partner from wave one.
- **Oathbound Locket** — health orbs mend half again as much.
- **Dawnbreak Crown** — +25 max health, and 40% of heals echo across.

Two new `StatEquipment` fields (`startingEchoFraction`, `startingOrbHealMultiplier`)
route through `DawnBoost`'s own setters rather than a parallel path. Both keep the
larger value, so an item is only ever a floor under what the draft goes on to grant.

Icons are 16×16 `.aseprite` in the house style, corner-alpha-guarded against the
importer's tight crop. **`oathbound_locket` is the weakest of the six drawn this
session** — it reads as two shells more than as a locket.

## Open / next

- **Tuning is expected.** The proc numbers (3–5 HP) are deliberately timid. Dawn
  stacked with Blood Tithe equipment and a healthy orb count is the combination
  most likely to overshoot; watch a full run before loosening anything.
- **The chime is unauditioned.** `Assets/Sounds/dawn_blessing.wav` is a soft
  two-note rising fifth (sine, 362ms, peak 0.395) wired as
  `AudioManager.dawnBlessing` at volume 0.55. It is **one shared cue for the
  whole Order**, not one per discipline — the player should learn a single "the
  light answered", not five. Orb heals pass `HealSource.Orb` and stay silent
  because `orbCollect` already fires there, and a 0.12s guard stops a rescue
  that also echoes from stacking into a chord. Three other candidates are in the
  session scratchpad (`heal_design_a/c`, `heal_roll-1..4`) if this one is wrong;
  swapping is a one-file overwrite, since the `.meta` GUID carries the wiring.
- **`DawnFx` is unwatched.** It compiles and the emitter setup mirrors shipped
  Ember code, but nobody has seen it run — mote density, halo radius, and
  whether gold reads against the Gilded Vigil ground are all guesses until
  someone plays a Dawn run.
- **Duo upgrades** (`orders-and-the-full-run.md` §1) now have their best
  candidate pair available: Dawn + Guardian, where the shield's blocks mend the
  other knight.
- **Guardian was the last incomplete Order** and was finished on 2026-09-08 —
  see `guardian-order.md`. The capstone is *Bulwark*, not the *Unbreakable* named
  here; *Thorned Aegis* shipped as **Reflector** and *Stalwart* was cut.
