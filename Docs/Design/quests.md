# Two Knights - Quest Reference

Generated from `QuestDatabase` on 2026-08-06. Do not hand-edit: change the quest line files under `Assets/Scripts/Quests/Lines/` and regenerate.

**31 quests.** Only unlocked quests appear in the log. Locked ones are hidden entirely rather than shown greyed out, because half the content is Order lines whose existence is itself a reveal.

---

## The Camp Fields (11)

### A Ruckus in the Wood

The animals have been coming out of the treeline wrong — too many of them, too bold, and all headed the same direction. The cook thinks it's the weather. The quartermaster thinks someone upstream has been dumping something. Neither of them has been far enough in for their opinion to be worth much. Go and find what's actually pushing them out.

| | |
|---|---|
| **Unlocked by** | Available from a new game |
| **Objective** | Venture further into the forest |
| **Reward** | **The Gnawed Crown** - +50% damage to rats, bats and the Rat King. |
| **Id** | `ruckus_in_the_wood` |

### The First Watch

Nobody expects much of a pair on their first nights. You stand where you're told, you keep the shield up, and you learn that the wood makes a particular sound just before something comes out of it. Three waves is the traditional measure of whether a new pair is worth feeding.

| | |
|---|---|
| **Unlocked by** | Available from a new game |
| **Objective** | 3 waves into the forest |
| **Reward** | 1 crystal |
| **Id** | `first_watch` |

### Camp Cleanup

The ground between the tents and the treeline used to be pasture. Nothing will graze there now, and the vermin have grown comfortable enough to come in after the stores. The quartermaster has stopped asking politely and started keeping a tally.

| | |
|---|---|
| **Unlocked by** | Available from a new game |
| **Objective** | 500 slain in the wood |
| **Reward** | 1 crystal |
| **Id** | `camp_cleanup_1` |

### Camp Cleanup II

Clearing them out taught them very little except to come at a different hour. The tally board by the mess tent has been wiped down and started again. The quartermaster insists this is normal and has not slept properly in a week.

| | |
|---|---|
| **Unlocked by** | complete **Camp Cleanup** |
| **Objective** | 1000 slain in the wood |
| **Reward** | 2 crystals |
| **Id** | `camp_cleanup_2` |

### Camp Cleanup III

There's a point where killing things stops being a chore and starts being a border. The wood has worked out roughly where the edge is now, and most nights it respects it. The quartermaster has taken the board down. He kept the numbers.

| | |
|---|---|
| **Unlocked by** | complete **Camp Cleanup II** |
| **Objective** | 2000 slain in the wood |
| **Reward** | 2 crystals |
| **Id** | `camp_cleanup_3` |

### Into the Deep Wood

Past the treeline the trees stop being spaced like trees and start being spaced like a wall. No map in camp goes further than the second creek, and the two scouts who tried came back disagreeing about how long they had been gone. Push in and find where the ground stops holding.

| | |
|---|---|
| **Unlocked by** | `maps.camp_fields.gate_cleared` reaches 1 |
| **Objective** | 15 waves into the forest |
| **Reward** | 1 crystal |
| **Id** | `into_the_deep_wood` |

### The Crimson Twins

Whatever the wood has been pushing out, this is what has been doing the pushing. The scouts' accounts don't agree on much, but they agree on the colour, and they agree there were two of them. Go to the far end of it and settle the question.

| | |
|---|---|
| **Unlocked by** | `maps.camp_fields.gate_cleared` reaches 1 |
| **Objective** | Answer what waits beyond the treeline |
| **Reward** | **A second equipment slot** (both knights) |
| **Id** | `the_crimson_twins` |

### Forest Ranger

The rangers who worked this wood before the camp went up carried a bow, a knife, and nothing else worth naming. They also went further in than anyone has managed since. The quartermaster thinks the two facts are related and has said so more than once. Strip both knights to nothing — no equipment, no special — put the Rat King down, and he will stop saying it.

| | |
|---|---|
| **Unlocked by** | `maps.camp_fields.gate_cleared` reaches 1 |
| **Objective** | Defeat the Rat King with no equipment or special equipped |
| **Reward** | 2 crystals |
| **Id** | `forest_ranger` |

### One With the Trees

Doing it once was a point being made. Doing it at the far end of the wood, against the thing the whole map has been arranged around, is something else. No equipment, no special, both knights, all the way through. The rangers left no account of trying it.

| | |
|---|---|
| **Unlocked by** | complete **Forest Ranger** |
| **Objective** | Defeat the Crimson Twins with no equipment or special equipped |
| **Reward** | 3 crystals |
| **Id** | `one_with_the_trees` |

### The Long Way Round

You have stood in the same field enough nights to notice it isn't the same field. What the wood sends depends on how deep you have pushed and what you did the last time you were out. The scouts never worked this out, which explains the maps. Keep going out, and keep count of what comes.

| | |
|---|---|
| **Unlocked by** | `waves.distinct.camp_fields` reaches 20 |
| **Objective** | 32 forest wave types met |
| **Reward** | 1 crystal |
| **Id** | `forest_explorer_1` |

### Every Path in the Wood

There is nothing left out there that hasn't already come at you at least once. That isn't mastery exactly — it's closer to having run out of surprises. The pack in particular has stopped treating you as something worth testing, and the two of you have started walking a little differently for it.

| | |
|---|---|
| **Unlocked by** | complete **The Long Way Round** |
| **Objective** | 45 forest wave types met |
| **Reward** | **Wolfsbane Pendant** - +50% damage to wolves. |
| **Id** | `forest_explorer_2` |

---

## The Mine (5)

### Down the Shaft

The mine was worked and then it wasn't, and nobody in camp gives a straight answer about which came first — the men leaving, or whatever it was that made them leave. The head of the shaft is still shored and still lit. Somebody has been maintaining it.

| | |
|---|---|
| **Unlocked by** | `maps.camp_fields.gate_cleared` reaches 1 |
| **Objective** | 5 waves into the mine |
| **Reward** | 1 crystal |
| **Id** | `down_the_shaft` |

### Off the Rails

The carts run all night on a loop that goes somewhere and comes back, and there is never anyone driving them. Breaking them is loud, wasteful, and the only thing that reliably stops the loop. Nobody has explained who keeps putting them back on the track.

| | |
|---|---|
| **Unlocked by** | `maps.camp_fields.gate_cleared` reaches 1 |
| **Objective** | 100 carts |
| **Reward** | 1 crystal |
| **Id** | `off_the_rails` |

### Powder and Patience

Whoever worked this seam left their powder exactly where it sat, strapped to carts that still run. You can shoot around it all night, or you can let the mine do the work it was always going to do and make sure you're standing elsewhere when it does.

| | |
|---|---|
| **Unlocked by** | `maps.camp_fields.gate_cleared` reaches 1 |
| **Objective** | 50 slain by blast |
| **Reward** | 2 crystals |
| **Id** | `powder_and_patience` |

### Deeper Workings

The upper galleries are a known quantity now — same rails, same shifts, same places the roof drips. Further down the shafts stop following the seam and start following something else, and what you meet down there does not work the way the upper crews do. Go and see the rest of it.

| | |
|---|---|
| **Unlocked by** | `waves.distinct.mine` reaches 20 |
| **Objective** | 24 mine wave types met |
| **Reward** | 1 crystal |
| **Id** | `mine_explorer_1` |

### Every Shaft Walked

You have been down every gallery this mine still has open. The survey maps back at camp are wrong in nine places and you can name all nine from memory. Whatever else the dark below is, it is no longer unknown to you.

| | |
|---|---|
| **Unlocked by** | complete **Deeper Workings** |
| **Objective** | 28 mine wave types met |
| **Reward** | 3 crystals |
| **Id** | `mine_explorer_2` |

---

## The Camp (15)

### Initiation: The Green Oath

The Order of the Serpent does not recruit and does not advertise. What it does is notice, eventually, when someone has been using poison properly — not as a finisher but as the whole plan, letting the work happen while you stand somewhere safe. Do enough of it and someone will find you. There is no ceremony. There is a small green mark on your kit that you did not put there.

| | |
|---|---|
| **Unlocked by** | `applied.poison` reaches 20 |
| **Objective** | 100 enemies poisoned<br>50 slain by venom<br>5 serpent upgrades taken |
| **Reward** | 2 crystals |
| **Id** | `serpent_initiation` |

### The Slow Work

The first thing they teach is that you are not in a hurry. A killed thing and a dying thing are worth the same by morning, and the dying one cost you one arrow instead of four. Go and be unhurried about it, at length, until it stops feeling like waiting.

| | |
|---|---|
| **Unlocked by** | complete **Initiation: The Green Oath** |
| **Objective** | 200 slain by venom |
| **Reward** | **Fangbone Charm** - +1 poison damage per tick. |
| **Id** | `serpent_fang_1` |

### The Long Coil

By now the wood knows how you taste and comes anyway, which the Order regards as the actual result. Nothing out there has learned to avoid you. They have simply worked out that there is no avoiding it, and adjusted their expectations accordingly.

| | |
|---|---|
| **Unlocked by** | complete **The Slow Work** |
| **Objective** | 500 slain by venom |
| **Reward** | **Serpent's Eye** - Serpent upgrades appear twice as often in your drafts. |
| **Id** | `serpent_fang_2` |

### A Cloud That Lingers

The second branch is less about the arrow and more about the air. What one of them carries, the next one breathes, and you were never required to be present for the second part. Learn to leave something behind you.

| | |
|---|---|
| **Unlocked by** | complete **Initiation: The Green Oath** |
| **Objective** | Poison four enemies with a single cloud |
| **Reward** | 1 crystal |
| **Id** | `serpent_coil_1` |

### What the Dead Carry

The Order's last lesson is the one they leave out of the written rites: a corpse is not the end of a job, it is a delivery. Take up the rite and the dead start working the shift after yours.

| | |
|---|---|
| **Unlocked by** | complete **A Cloud That Lingers** |
| **Objective** | Acquire Plaguebringer, the Serpent capstone |
| **Reward** | **Hollow Fang** - +8 special charge when an enemy dies of your venom. |
| **Id** | `serpent_coil_2` |

### Initiation: The Ashen Oath

Anyone can start a fire. The Order of the Ember is interested in the considerably rarer skill of keeping one — setting something alight and then not needing to do anything further about it. They will want to see it more than once, on different nights, before anyone says a word to you.

| | |
|---|---|
| **Unlocked by** | `applied.ignite` reaches 20 |
| **Objective** | 100 enemies set alight<br>50 slain by fire<br>5 ember upgrades taken |
| **Reward** | 2 crystals |
| **Id** | `ember_initiation` |

### Stoke the Fire

A flame that only frightens is a flame you are wasting. The Order's standing complaint about most people who carry fire is that they use it as a slightly worse arrow. Make it do the killing on its own and stop supervising.

| | |
|---|---|
| **Unlocked by** | complete **Initiation: The Ashen Oath** |
| **Objective** | 200 slain by fire |
| **Reward** | **Emberbrand** - +1.5 fire damage per second. |
| **Id** | `ember_brand_1` |

### Scorched Ground

There is nothing coming out of that treeline anymore that has not already burned once. Camp has noticed the smell and stopped commenting on it. The Order has noticed the count and has not stopped commenting at all.

| | |
|---|---|
| **Unlocked by** | complete **Stoke the Fire** |
| **Objective** | 500 slain by fire |
| **Reward** | **Everburning Coal** - Ember upgrades appear twice as often in your drafts. |
| **Id** | `ember_brand_2` |

### Lay the Pyre

The other branch cares less about what you hit and more about where it walks afterwards. Ground that remembers the fire does half the night's work for you, and it does that work while you are facing entirely the wrong way.

| | |
|---|---|
| **Unlocked by** | complete **Initiation: The Ashen Oath** |
| **Objective** | Have four fire trails burning at once |
| **Reward** | 1 crystal |
| **Id** | `ember_pyre_1` |

### Salt the Earth

The Order's final rite is an admission: some fires are not meant to go out. What you light stays lit, and the field stops being ground you defend and becomes ground they have to cross.

| | |
|---|---|
| **Unlocked by** | complete **Lay the Pyre** |
| **Objective** | Acquire Scorched Earth, the Ember capstone |
| **Reward** | **Cinder Crown** - Fire trails burn 3 seconds longer. |
| **Id** | `ember_pyre_2` |

### Initiation: The Silent Oath

The Order of the Shadow has no interest in how loudly you can kill. What gets their attention is economy — the wounded thing finished before it can turn around, the shot that did not need a second. Show enough of it and the invitation arrives without ceremony, usually folded into something you were already carrying.

| | |
|---|---|
| **Unlocked by** | `upgrades.taken.shadow_2` reaches 1  **OR**  `upgrades.taken.shuriken_fan_1` reaches 1 |
| **Objective** | 6 shadow upgrades taken<br>25 finished outright |
| **Reward** | 2 crystals |
| **Id** | `shadow_initiation` |

### The Quiet End

Wounded things take considerably longer to die than they need to, and every second of it is a second you are not aiming somewhere more useful. The Order regards a slow finish as a form of rudeness, mostly toward yourself.

| | |
|---|---|
| **Unlocked by** | complete **Initiation: The Silent Oath** |
| **Objective** | 1000 shadow arrows landed |
| **Reward** | **Nightglass Shard** - +15% shadow arrow damage. |
| **Id** | `shadow_blade_1` |

### Thousand Cuts

The last lesson of the blade branch is that one blade was always a compromise you agreed to for no particular reason. Take up the rite and stop agreeing to it.

| | |
|---|---|
| **Unlocked by** | complete **The Quiet End** |
| **Objective** | Acquire Thousand Cuts, the Shadow capstone |
| **Reward** | **Starless Quiver** - Shadow upgrades appear twice as often in your drafts. |
| **Id** | `shadow_blade_2` |

### A Hundred Edges

The other branch gave up on aim as a luxury some time ago. Coverage is cheaper, far more forgiving, and considerably harder to walk through. Widen the fan and stop being precious about where each one lands.

| | |
|---|---|
| **Unlocked by** | complete **Initiation: The Silent Oath** |
| **Objective** | Land four shurikens from a single volley |
| **Reward** | 1 crystal |
| **Id** | `shadow_fan_1` |

### Phantom Blade

Swing once and let the dark swing after you. The Order has declined to explain how this works and has politely asked that you stop asking.

| | |
|---|---|
| **Unlocked by** | complete **A Hundred Edges** |
| **Objective** | Land a swing and both its phantom echoes |
| **Reward** | **Echo Ribbon** - +1 sword echo. |
| **Id** | `shadow_fan_2` |

---

## Crystal economy

Quests pay out **30 crystals** across the full set. Clearing the shop costs **22**.

| Shop item | Tab | Cost | Effect |
|---|---|---|---|
| Waxed Cord | Equipment | 2 | Confusion wears off 65% faster. |
| Powder Ward | Equipment | 3 | Take 75% less damage from powder kegs and bombs. |
| Whetstone | Equipment | 5 | Draft one upgrade before the first wave. |
| Quiver Strap | Equipment | 2 | 10% less time between shots. |
| Padded Gambeson | Equipment | 2 | +20 maximum health. |
| Iron Vigil | Specials | 4 | 5 seconds where no damage can reach you. |
| Blood Tithe | Specials | 4 | Each of your kills heals you 3, for 8 seconds. |

## Quest-only equipment

| Item | Effect | Source |
|---|---|---|
| The Gnawed Crown | +50% damage to rats, bats and the Rat King. | A Ruckus in the Wood |
| Wolfsbane Pendant | +50% damage to wolves. | Every Path in the Wood |
| Fangbone Charm | +1 poison damage per tick. | The Slow Work |
| Serpent's Eye | Serpent upgrades appear twice as often in your drafts. | The Long Coil |
| Hollow Fang | +8 special charge when an enemy dies of your venom. | What the Dead Carry |
| Emberbrand | +1.5 fire damage per second. | Stoke the Fire |
| Everburning Coal | Ember upgrades appear twice as often in your drafts. | Scorched Ground |
| Cinder Crown | Fire trails burn 3 seconds longer. | Salt the Earth |
| Nightglass Shard | +15% shadow arrow damage. | The Quiet End |
| Starless Quiver | Shadow upgrades appear twice as often in your drafts. | Thousand Cuts |
| Echo Ribbon | +1 sword echo. | Phantom Blade |
