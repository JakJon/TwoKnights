# The Target Range

Red-and-white targets that pop up round the edge of the forest arena in the gap
between two waves. Shoot them all before the time runs out and you get a health orb
and a mana orb. Designed with the owner on 2026-09-26. Code:
`Assets/Scripts/Trials/TargetRange.cs` (schedule, patterns, payout) and
`RangeTarget.cs` (one target).

## When it happens

- In the forest only.
- After any wave from wave 5 on, odd or even (owner, 2026-10-13; it was odd waves
  only), with a 1 in 4 chance.
- If 4 or more waves have gone by in this run without it, the chance is 1 in 2
  until it plays. The wait is counted from the wave it last played after, or from
  wave 4 while it has not played yet in the run. So a new run rolls 1 in 4 after
  waves 5, 6 and 7, and 1 in 2 from wave 8 until the range turns up. After it
  plays, the next three waves are 1 in 4 and the ones after that are 1 in 2.
- Cart sorting in the Mine rolls the same way; the code for both is
  `Assets/Scripts/Trials/LowEventRoll.cs`.
- History: 1 in 3 at first, then no roll at all from 2026-09-26 (a run of misses
  after Target Practice was finished read as the range having gone for good), then
  this. It stays for the whole file, quest done or not.
- It is a **low-priority** event. It only plays when no quest scene is owed and no
  Order trial is going to play in that gap. Never after a boss. See the
  `event-authoring` skill for the whole priority ladder.

## Which pattern

Not rolled. They go in batches of five (owner, 2026-10-05): each time, the next
pattern after the one last put up that has not been cleared in this batch, wrapping
from 5 back to 1. Pass or fail, it moves on, so a pattern you fail comes back later
in the batch, not next time. Once all five are cleared the batch starts over at 1.

The tutorial run moves through them the same way (owner, 2026-10-13). Before that
it showed pattern 1 every time, because the tutorial run did not save which pattern
had been put up. It ends at the wave 10 boss, so it has five gaps for the range
(after waves 5 to 9); with the roll it will usually show one or two patterns. The next forest run carries on from wherever it got
to. Clears in the tutorial run still do not count toward Target Practice.

| # | Targets | Time |
|---|---|---|
| 1 | One in each corner | 8 s |
| 2 | Five along the top (not in the corners) and five along the bottom | 13.5 s |
| 3 | One in each corner, three along the top between them, three along the bottom | 12.5 s |
| 4 | One in each corner, each sliding to the next corner clockwise: top right down to bottom right, bottom right across to bottom left, bottom left up to top left, top left across to top right. They arrive as the time runs out. | 8 s |
| 5 | Twenty round the edge of the view, one in each corner: seven along the top, seven along the bottom, three down each side | 28 s |

All the targets appear at once. The time starts when they can be hit.

## How it plays

- Each target pops into view in a puff of white smoke, the same puff as the Ninja's
  but white, and leaves in the same puff, whether it was hit or time ran out.
- Any of a knight's arrows breaks a target. The arrow is used up, so one arrow breaks
  one target.
- For the last 3 seconds, every target still standing blinks.
- Specials are locked and the HUD is hidden while it runs, as in the Order trials.
  Knights fire only their own arrows: no shadow arrows, no shurikens.
- **All hit in time:** the fanfare (`trial_won`) plays. A health orb crosses just
  above the knights, left to right, and a mana orb crosses just below them, right to
  left, both inside sword reach. They are ordinary orbs and restore the usual amounts.
  The gap waits until both have been collected or have flown off.
- **Time runs out:** the remaining targets vanish in smoke and the "womp womp"
  (`trial_lost`) plays. Nothing is paid.

## The quest

**Target Practice**, from the Cartographer. It is offered in the same gap as your
first clear of any pattern (not during the tutorial run, when quests are held back;
then it is offered on the next forest run). The objective counts different patterns
ever cleared, so the clear that opened it already counts: "Complete all 5 different
target range patterns successfully. 1 / 5". Reward: 3 crystals.

## Art and sound

- Target: `Assets/Resources/Trials/Target.aseprite`, 32x32, a 28-pixel bullseye at 32
  pixels per unit (about one world unit across), pivot in the centre. Loaded from
  Resources in code.
- A target breaking plays `execute_flash`, the sound the Ninja makes when he is hit.
  There is no sound of its own yet.

## Testing

Test Mode row **Force event between waves** → Targets 1–5. Over the Unity bridge:
`TestRunConfig.ForcedTrial = "targets"; TestRunConfig.ForcedTrialPhase = <1-5>;`
Practice records nothing but still pays the orbs. The log shows `[Targets]` lines.

## Open items

- Not yet played in the editor. It compiles, and the target art was checked pixel
  for pixel after saving, but Unity has not imported it yet.
- `Docs/Design/quests.md` needs regenerating for Target Practice
  (`scratchpad/gendoc.py`, needs Unity).
