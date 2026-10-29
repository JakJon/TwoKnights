# Cart Sorting

A challenge in the gap between two waves in the Mine. A piece of track drops in
with flags at its ends, coloured carts roll onto it, and every cart has to be sent
to the flag of its own colour. Designed with the owner on 2026-10-09. Code:
`Assets/Scripts/Trials/CartSorting.cs` (schedule, the five phases, payout),
`RailSwitchLever.cs` (one lever), `CartSortingKit.cs` (the asset that holds its
prefabs), and the switch support in `Assets/Scripts/Rails/RailNetwork.cs`.

All five phases are built. The page they were designed on, with a mock-up of
each: https://claude.ai/artifact/7eXwrBX2CuZ4JFBcXXhhsx (its phase numbers are the
numbers the proposals used: 2 twin wheels, 3 pass it on, 4 over and under, 5 the fountain, which is the order now played).

## When it happens

- In the Mine only.
- After any wave from wave 5 on, with a 1 in 4 chance (owner, 2026-10-13; it was
  1 in 3 from wave 3).
- If 4 or more waves have gone by in this run without it, the chance is 1 in 2
  until it plays. The wait is counted from the wave it last played after, or from
  wave 4 while it has not played yet in the run. So a new run rolls 1 in 4 after
  waves 5, 6 and 7, and 1 in 2 from wave 8 until the carts turn up. The forest's
  target range rolls the same way; the code for both is
  `Assets/Scripts/Trials/LowEventRoll.cs`.
- It is a **low-priority** event, like the forest's target range. It only plays
  when no quest scene is owed and no Order trial is going to play in that gap.
  Never after a boss. See the `event-authoring` skill.

## The rules every phase shares

- Carts and flags come in **four colours**: red, blue, green, yellow.
- There are only **two levers**, red and blue. The track is ordinary rail except
  for a few **corner pieces** drawn in red or blue. Hitting a lever with a sword or
  an arrow turns every corner piece of its colour to its other branch. Hitting it
  again turns them back. (Owner: one coloured corner per fork, never a whole
  stretch of coloured rail.)
- Where a lever looks after two colours, the red lever has red and yellow and the
  blue lever has blue and green.
- A cart takes whichever way a corner is pointing when the middle of the cart
  reaches the middle of the corner.
- A cart at the flag of its own colour leaves in white smoke. **The first cart at
  any other flag loses the challenge** (dark smoke, then `trial_lost`).
- Every cart home: `trial_won`, then a health orb and a mana orb fly past, the same
  as the target range.
- Specials are locked, the HUD is hidden, and knights fire only their own arrows.
- **Every upgrade is off for the length of it** (owner, 2026-10-13). The bow fires
  one plain arrow at the reload and speed it had before any upgrade, and the sword
  makes one plain swing. The sight beam and the Glacial Ward ring are put away.
  Everything comes back before the fanfare. See `UpgradeHold.cs`. Not reached: the
  shield's shape (Tower Shield, Curved Aegis) and equipment.
- The levers stand half a unit either side of the middle, between the knights, so
  each knight can only reach their own. Phase 4 is the exception.

## The five phases

In the order they are played. The owner moved the twin wheels from last to second
on 2026-10-09 after playing it ("easier than I thought"). "Track" is the layout
asset, `Mine Sorting N`, which is numbered in the order the tracks were built.

| # | Name | Track | Colours | Carts | Fewest lever hits |
|---|---|---|---|---|---|
| 1 | Two forks | 1: a fork each side of the knights | red, blue | 8 | 5 |
| 2 | Twin wheels | 5: a one-way ring each side that carts roll onto and go round | all four | 6 | 6 |
| 3 | Pass it on | 2: one track, two forks in a row, three flags | red, blue, green | 7 | 7 |
| 4 | Over and under | 3: two long lanes crossing the screen, one over the knights, one under | all four | 18 | 6 |
| 5 | The fountain | 4: one track up between the knights, fanning out to four flags | all four | 8 | 9 |

1. **Two forks.** In both forks the left branch ends at a red flag and the right at
   a blue. Carts take turns between the forks, a little slower than the others.
2. **Twin wheels.** Each wheel has two doors half a lap apart and its lever opens
   both: red and yellow doors on the left wheel, blue and green on the right. A
   cart at a closed door goes round again. A cart at an open door leaves, right or
   wrong. Both wheels must be empty in **40 seconds**. For the last 3 seconds every
   cart still on the track flashes; then they are taken off and the challenge is lost.
3. **Pass it on.** The red fork keeps a red cart for the red flag or passes a cart
   on, over the knights, to the blue fork, which sorts blue (left) from green (right).
4. **Over and under.** Blue and green ride the top lane to the right fork; red and
   yellow ride the bottom lane to the left fork. Carts come in threes, nose to tail,
   and the lever has to be hit in the gap between two of them. No train asks one
   lever for two hits (sword cooldown 1 s, bow 1.5 s).
5. **The fountain.** The red lever picks a side. The one blue lever turns the blue
   corner on both sides. Flags left to right: red, yellow, green, blue, so red is
   left-left, yellow left-right, green right-left, blue right-right. The levers
   stand outside the knights (3.5 units from the middle) because the track runs
   between them.

**Pace (owner, 2026-10-09):** every cart runs a third slower than the cart lists
are written, and the time between carts is stretched to match (`Pace` in
`CartSorting.cs`), so the spacing on the track is unchanged. The gap inside a train
is 0.82 s. The clock is in real seconds and is not stretched. The four coloured
flags are one and a half times the size of the delivery flag.

Which phase plays is not rolled: they go in batches the way the range's patterns
do (the next one not yet cleared in this batch; pass or fail it moves on).

## The quest

**Sorting Carts**, from the Cartographer, offered in the same gap as the first
clear. Counts different phases ever cleared, out of 5. Reward: 3 crystals. The offer
and reward text are the owner's; the name, description and objective lines are still
Claude's placeholders.

## How it is built

- Each track is a `RailLayout` asset, `Mine Sorting 1` to `5`, laid by the mine's
  own `RailNetwork`. A fork's corner tile is in the layout twice (at rest, thrown),
  marked with its lever colour, and so is the link off the end of the run leading
  into it. `RailNetwork.Throw(colour)` swaps them.
- Flags are the layout's drop points, each marked with a `CartColor`.
- Cart lists, speeds, lever positions, which way each lever leans at rest, and the
  clock are in `CartSorting.Phases`.
- Prefabs, layouts and the kit are made by **Tools > Two Knights > Build Cart
  Sorting Assets** (`Assets/Editor/CartSortingAssetBuilder.cs`), which is where the
  tracks are written down. Change a track there and run it again.
- **Tools > Two Knights > Check Cart Sorting Phases** plays every phase through on
  paper, without play mode: it checks every cart can reach its own flag, counts the
  lever hits, and reports the tightest gap between hits. Run it after any change.
- Art in `Assets/Graphics`: `mine_cart_*` and `dropoff_flag_*` in the four colours,
  `rail_*_red/blue`, `rail_switch_red/blue`. Red and blue are the knights' plume
  colours; green and yellow are Claude's picks.
- Sounds are borrowed: a lever hit plays `rail_land`, a cart home plays
  `cargo_drop`. Add `switch_throw.wav` or `cart_sorted.wav` to `Assets/Sounds` and
  rebuild to replace them.

## Not yet played

2026-10-09: phase 1 was seen running (track, both forks, levers, carts leaving at
the right flag) but not to the end. Phases 2 to 5 pass the on-paper check and
compile, and have **never been played**. Unseen in any phase: a win (fanfare and
orbs), the 30 second clock, and the quest offer. The owner is doing the play
testing.
