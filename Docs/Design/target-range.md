# The Target Range

Red-and-white targets that pop up round the edge of the forest arena in the gap
between two waves. Shoot them all before the time runs out and you get a health orb
and a mana orb. Designed with the owner on 2026-09-26. Code:
`Assets/Scripts/Trials/TargetRange.cs` (schedule, patterns, payout) and
`RangeTarget.cs` (one target).

## When it happens

- In the forest only.
- After every odd wave from wave 5 (5, 7, 9, 11…), every time — no roll. It was
  one in three until the owner dropped the roll on 2026-09-26, because after Target
  Practice was finished a run of misses read as the range having gone for good. It
  stays for the whole file, quest done or not.
- It is a **low-priority** event. It only plays when no quest scene is owed and no
  Order trial is going to play in that gap. Never after a boss. See the
  `event-authoring` skill for the whole priority ladder.

## Which pattern

Not rolled. It is the lowest-numbered pattern you have never cleared, so the five
work as a ladder. Once all five have been cleared, they take turns: the one after
the last pattern you cleared, 1 to 5 and round again. A pattern you fail comes back
next time.

| # | Targets | Time |
|---|---|---|
| 1 | One in each corner | 8 s |
| 2 | Five along the top (not in the corners) and five along the bottom | 15 s |
| 3 | One in each corner, three along the top between them, three along the bottom | 15 s |
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
- The targets can appear during the tutorial run (forest waves 5, 7 and 9), because
  nothing holds them back there. Confirm that is wanted.
- `Docs/Design/quests.md` needs regenerating for Target Practice
  (`scratchpad/gendoc.py`, needs Unity).
