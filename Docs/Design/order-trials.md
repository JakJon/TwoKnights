# Order Trials

Short tests that can turn up in the gap between two waves while an Order's
initiation quest is open. Passing them is how each initiation now completes. They
replaced the old initiation objectives ("set 300 enemies alight", "reflect 60
rocks", ...), which were too plain for an Order's welcome.

Designed with the owner on 2026-09-23. Code: `Assets/Scripts/Trials/`.

## When a trial happens

After a wave is survived, once the "Wave Survived" panel has gone:

- Trials are the MEDIUM tier of the between-wave events (owner, 2026-09-26): a
  quest scene (high) always beats them, and they always beat the target range
  (low). The full ladder and every event's trigger are in the `event-authoring`
  skill (`.claude/skills/event-authoring/SKILL.md`); the code is
  `BetweenWaveEvents`.
- Only if no NPC scene is queued for that gap. If a quest is being offered or
  completed, there is no trial.
- Never after a boss wave.
- One trial per gap at most.
- Only while the initiation quest has been offered by its NPC and is not yet
  complete.

When each trial is due (owner, 2026-09-25). Every one is a 100% chance when due:

| Order | Trial | Due after | Phases |
|---|---|---|---|
| Ember | Fire orbs | every odd wave (1, 3, 5...) | 3 |
| Frigid | Ice orbs | every odd wave | 3 |
| Serpent | Venom orbs | every even wave (2, 4, 6...) | 3 |
| Shadow | Catch the Ninja | every even wave | 3 |
| Dawn | Paladin's bout, mirrored | every 6th wave | 1 |
| Guardian | Paladin's bout, one knight at a time | every 7th wave | 1 |

**When more than one is due, the one that has gone the longest without playing
gets the gap.** A trial that has never played beats any that has, and an exact tie
is picked at random. Playing counts whether it was won or lost. "How long" is
counted in gaps where a trial was decided, and it carries over from one run to the
next (stats `trials.clock` and `trials.<order>.last`). This replaced "the rarest
wins" on 2026-09-25.

In practice the trials take turns: fire and ice alternate on odd waves, the
Serpent and the Ninja alternate on even ones, and the Paladin usually wins his
waves because he has waited longest. He can lose one, though. On a fresh file with
everything open, Dawn loses wave 12 to the Ninja, who has waited longer, and comes
back at wave 18. All of these numbers live in one list, `OrderTrials.All`.

**Phases are separate events.** Passing phase I means the next time that trial
comes up, it is phase II. Losing a phase means that same phase comes back next
time; you never drop to an earlier one. Progress is saved across runs as the stat
`trials.<order>`, and the initiation quest's single objective reads it
("0/3 fire trials passed").

The dice only decide whether a trial happens. Everything inside a trial is fixed:
the same orbs, lanes, rocks and spots at the same seconds, every time. That keeps
the no-randomness rule the waves follow.

## What every trial shares

- The HUD fades out and both knights' specials are locked until it ends. Passive
  upgrades still work (Sunwell III and frost slow trial orbs, for example).
- Nothing in a trial pays out: trial orbs give no health or special, and trial
  rocks give no special charge or Guardian tallies.
- During the fire, ice and venom trials, knights fire only their own arrow: no
  shadow arrows and no shurikens (owner, 2026-09-25).
- On a pass, the "you found it" chime plays (`Assets/Sounds/trial_won.wav`).
  On a loss, everything on the board disappears at once and the "womp womp"
  plays (`trial_lost.wav`). Then the gap carries on to the upgrade menu as normal.
- If a trial completes the initiation, the Order's completion scene plays right
  after it, in the same gap.

## The trials

### Ember: fire orbs

Health orbs with flames, moving at twice a health orb's speed. Shoot every one.
The moment one gets away, the trial is lost. Top and bottom orbs come as a pair
down the same lane, the second one second behind the first (doubled on
2026-09-25). Side orbs come one at a time. Fire's top and bottom lanes sit half a
unit further in than ice's (y ±4.25), and its side lanes 2.5 units in from where
they were (x ±6.8) — both moved toward the knights on 2026-09-26.

- **I:** a pair along the top edge.
- **II:** a pair along the top, then a pair along the bottom five seconds later.
- **III:** a pair along the top, a pair along the bottom, then one orb up the left
  side and one down the right, five seconds apart. Side orbs are on screen for
  under 3 seconds, against about 5 for top and bottom.

The Wizard explains this in the initiation offer, so there is no announcement when
it happens.

### Frigid: ice orbs

Mana orbs with frost, moving at half a mana orb's speed. Same rule as fire. Each
phase sends half its orbs, waits 5 seconds after the last one, then sends the other
half. Orb counts were doubled on 2026-09-25.

Every orb comes in from a different corner from the one before it (2026-09-25),
in a fixed order: top-left, bottom-right, top-right, bottom-left, and round again.
Each runs along the top or bottom edge away from its corner. They used to go as a
line along the top and then a line along the bottom.

- **I:** 8 orbs, 1 second apart (about 26 seconds).
- **II:** 12 orbs, 1 second apart (about 30 seconds).
- **III:** 16 orbs, 2 seconds apart. The last orb leaves after 33 seconds, so the
  phase runs up to about 48 seconds.

### Serpent: venom orbs

Green orbs (`Assets/Resources/Trials/Orb_Venom.aseprite`, a recolour of the mana
orb) with poison bubbles, at a normal orb's speed. Each orb crosses the field three
times. Hit each one exactly once: a hit orb darkens and keeps flying. You lose if
you hit an orb a second time, or if an orb finishes its third crossing without
being hit. The shot that lands is used up, so one arrow can't hit two orbs.

Orbs in the same lane ride in a tight line, 0.6 seconds (about 1.8 units, just
under four orb-widths) apart, so a hit orb always has fresh ones beside it. A
second lane's orbs leave on the half-beats in between.

- **I:** three orbs in a line.
- **II:** five, in top and bottom lanes moving opposite ways.
- **III:** seven. Three run inner lanes the opposite way to four on the outer
  lanes, so a hit orb keeps crossing between a knight and a fresh one.


### Shadow: catch the Ninja

The Ninja appears in smoke at a spot round the edge of the arena, holds for a
moment, and vanishes. Hit him before he goes; missing one window loses.

- **I:** 6 appearances, 2.5 seconds each, alternating sides.
- **II:** 10 appearances at 2.2 seconds, some twice in a row on the same side.
- **III:** 12 appearances at 2.0 seconds. From the third one on, a see-through
  violet copy appears on the opposite side at the same moment, in a puff of
  smoke as see-through as it is.
  Hitting the copy loses.

The windows are sized to allow for arrow travel time, since a shot takes most of a
second to reach the far edge. He says a line before each phase. Only
"Catch me, if you can" was approved; the phase II and III lines are placeholders
in `NinjaTrial.IntroLine`.

### Guardian: the Paladin's bout

The Paladin says his line, leaves, and then about 45 seconds of fast rock (4.5
units per second, against about 1.75 in waves) comes at one knight at a time. The
turn switches every 7.5 seconds, left knight first, and the knight whose turn it
is gets the Paladin's light and ring of shields over their head. Any rock that
touches a knight ends the trial. The rocks do no damage.

Each turn has its own character: a warm-up, sweeping arcs, up-up-down-down-left-
right-left-right, a scramble, a mix, and a finale.

### Dawn: the mirrored bout

The same kind of bout, but at both knights at once for about 45 seconds, mirrored:
when the left knight blocks up, the right knight blocks down at the same instant.
When the left knight blocks toward the outside, so does the right knight on his own
side. The pattern is rotated half a turn, not flipped top to bottom, because a rock
coming "from the left" at the right knight would have to fly through the left
knight.

### Rules both bouts keep

- No rock ever comes straight from the other knight's side. Those directions come
  in on steep diagonals (50° off horizontal) instead, and pass at least 3 units
  from the other knight.
- Rocks at one knight are always at least 0.12 seconds apart.
- The "chaotic" parts are a golden-ratio walk around the dial, so they look random
  but are the same every time.
- Every rock is scheduled by when it should arrive and released early by its own
  flight time. The longest flight is just under 2.9 seconds, which is why the bout
  opens with a 1.4-second runway.

## Testing

Test Mode has a row called **Force event between waves** that cycles Off /
Fire I–III / Ice I–III / Venom I–III / Ninja I–III / Guardian / Dawn / Targets 1–5.
With it set, every gap of that test run plays the chosen trial as practice: it
ignores quest state and records nothing. Over the Unity bridge, set
`TestRunConfig.ForcedTrial = "ember"` and `TestRunConfig.ForcedTrialPhase = 2`.

Every gap between waves writes a line to the Unity log saying what happened to
it. `[Events]` lines cover a boss gap, an NPC scene taking the gap, and a gap
where nothing played. `[Trials]` lines list which trials were due and whether
each was closed or missed its roll, or which one came up.
A trial only rolls if its initiation has been offered and isn't complete, so a
save that finished an initiation under the old counters never sees that trial.

## Open items

- None of this has been played in the editor yet. It compiles, and the bout
  timings were checked offline.
- The Ninja's phase II and III lines are placeholders.
- Several initiation completion lines still talk about the old objectives (for
  example Ember's "reducing them to ashes" and Frigid's "stop enemies in their
  tracks").
- `Docs/Design/quests.md` needs regenerating (`scratchpad/gendoc.py`, needs Unity).
