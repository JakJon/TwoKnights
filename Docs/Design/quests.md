# Two Knights - Quest Reference

Generated from `QuestDatabase` on 2026-09-08. Do not hand-edit: change the quest line files under `Assets/Scripts/Quests/Lines/` and regenerate.

**47 quests.** Only unlocked quests appear in the log. Locked ones are hidden entirely rather than shown greyed out, because half the content is Order lines whose existence is itself a reveal.

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
| **Unlocked by** | complete **The Crimson Twins** |
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

## The Mine (6)

### What Runs the Carts

Every cart you break is back on the rails the next time you go down, and nobody at camp will say who is putting them there. The survey maps stop where the upper galleries stop, which the scouts insist is because there is nothing under them worth drawing. Follow the track instead of the maps. Whatever is turning the wheel down there is at the end of it.

| | |
|---|---|
| **Unlocked by** | `maps.mine.unlocked` reaches 1 |
| **Objective** | Follow the rails to the bottom |
| **Reward** | 1 crystal |
| **Id** | `what_runs_the_carts` |

### Off the Rails

The carts run all night on a loop that goes somewhere and comes back, and there is never anyone driving them. Breaking them is loud, wasteful, and the only thing that reliably stops the loop. Nobody has explained who keeps putting them back on the track.

| | |
|---|---|
| **Unlocked by** | `maps.mine.unlocked` reaches 1 |
| **Objective** | 100 carts |
| **Reward** | 1 crystal |
| **Id** | `off_the_rails` |

### Powder and Patience

Whoever worked this seam left their powder exactly where it sat, strapped to carts that still run. You can shoot around it all night, or you can let the mine do the work it was always going to do and make sure you're standing elsewhere when it does.

| | |
|---|---|
| **Unlocked by** | `maps.mine.unlocked` reaches 1 |
| **Objective** | 50 slain by blast |
| **Reward** | 2 crystals |
| **Id** | `powder_and_patience` |

### The Gold Cart

Something has been putting the carts back on the rails all along, and the survey maps have no word for it. The millstone was only the wheel. Whatever was turning it rides the same loop its shifts ride, in a cart nobody who works this seam could afford, and it does not get off.

| | |
|---|---|
| **Unlocked by** | `maps.mine.gate_cleared` reaches 1 |
| **Objective** | Put down whatever rides the gold cart |
| **Reward** | **A second special slot** (both knights) |
| **Id** | `the_gold_cart` |

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
| **Objective** | 42 mine wave types met |
| **Reward** | 3 crystals |
| **Id** | `mine_explorer_2` |

---

## The Camp (30)

### Initiation: The Green Oath

The Order of the Serpent does not recruit and does not advertise. What it does is notice, eventually, when someone has been using poison properly — not as a finisher but as the whole plan, letting the work happen while you stand somewhere safe. Do enough of it and someone will find you. There is no ceremony. There is a small green mark on your kit that you did not put there.

| | |
|---|---|
| **Unlocked by** | `applied.poison` reaches 60 |
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
| **Objective** | Acquire Acid Dagger, the Serpent capstone |
| **Reward** | **Hollow Fang** - +8 special charge when an enemy dies of your venom. |
| **Id** | `serpent_coil_2` |

### Initiation: The Ashen Oath

Anyone can start a fire. The Order of the Ember is interested in the considerably rarer skill of keeping one — setting something alight and then not needing to do anything further about it. They will want to see it more than once, on different nights, before anyone says a word to you.

| | |
|---|---|
| **Unlocked by** | `applied.ignite` reaches 60 |
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
| **Reward** | **Cinder Crown** - All fire zones (trails and fireball craters) are 0.5u wider. |
| **Id** | `ember_pyre_2` |

### Initiation: The Silent Oath

The Order of the Shadow has no interest in how loudly you can kill. What gets their attention is arrows that connect — the dark ones, the ones nobody hears coming, put into something often enough that it stops looking like luck. Show enough of it and the invitation arrives without ceremony, usually folded into something you were already carrying.

| | |
|---|---|
| **Unlocked by** | `hits.shadowarrow` reaches 60 |
| **Objective** | 6 shadow upgrades taken<br>250 shadow arrows landed |
| **Reward** | 2 crystals |
| **Id** | `shadow_initiation` |

### The Quiet End

Wounded things take considerably longer to die than they need to, and every second of it is a second you are not aiming somewhere more useful. The Order regards a slow finish as a form of rudeness, mostly toward yourself.

| | |
|---|---|
| **Unlocked by** | complete **Initiation: The Silent Oath** |
| **Objective** | 5000 shadow arrows landed |
| **Reward** | **Nightglass Shard** - +15% shadow arrow and shuriken damage. |
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

### What Follows the Blade

Swing once and let the dark swing after you, and do the same courtesy to anything an arrow finds already most of the way dead. Neither half is really a technique. The branch treats them as one habit, which is declining to do a job twice, and the Order has offered no account of how the echoes work beyond politely asking that you stop asking.

| | |
|---|---|
| **Unlocked by** | complete **A Hundred Edges** |
| **Objective** | Land a swing and both its phantom echoes<br>250 finished by Killing Blow |
| **Reward** | **Echo Ribbon** - +1 sword echo. |
| **Id** | `shadow_fan_2` |

### Initiation: The Held Breath

The Order of the Frigid does not kill anything. This is stated plainly at the door, usually to someone who has arrived expecting otherwise. What they teach is how to take an evening away from something that was in a hurry, and they will want to watch you do it a great many times before they accept that you understood the distinction.

| | |
|---|---|
| **Unlocked by** | `frigid.chilled` reaches 60 |
| **Objective** | 100 enemies chilled<br>50 enemies frozen<br>5 frigid upgrades taken |
| **Reward** | 2 crystals |
| **Id** | `frigid_initiation` |

### Take Their Evening

Anyone can make a wolf slower. The Order's interest begins at the point where the wolf stops entirely and has to be walked around. Do that until it is unremarkable, then come back and they will find something else to be unimpressed by.

| | |
|---|---|
| **Unlocked by** | complete **Initiation: The Held Breath** |
| **Objective** | 200 enemies frozen |
| **Reward** | **Hoarfrost Band** - Your chill lasts twice as long. |
| **Id** | `frigid_rime_1` |

### The Standing Field

Four at once is the number the Order uses to settle arguments. Below it you are answering things one at a time, which is a skill but not this one. At four the field stops being a fight and starts being a room you are tidying at your own pace.

| | |
|---|---|
| **Unlocked by** | complete **Take Their Evening** |
| **Objective** | Hold four enemies in ice at once |
| **Reward** | **Winter's Tooth** - Your arrows carry cold from the first wave. |
| **Id** | `frigid_rime_2` |

### Brittle Things

The other branch thinks holding is only half of it. Cold makes a body brittle, and a brittle body answers an arrow very differently to a warm one. Their complaint about the Rime branch is that it leaves the work undone and calls it mercy.

| | |
|---|---|
| **Unlocked by** | complete **Initiation: The Held Breath** |
| **Objective** | 100 enemies shattered |
| **Reward** | 1 crystal |
| **Id** | `frigid_silence_1` |

### Nothing Moves

The deepest rite of the Order is the admission the whole thing was always heading toward: enough time is the same as winning. Fifteen seconds is long enough that the field stops being ground you defend and becomes a row of things waiting their turn.

| | |
|---|---|
| **Unlocked by** | complete **Brittle Things** |
| **Objective** | Acquire Deep Freeze III, the deepest ice |
| **Reward** | **Heart of Ice** - Freezes hold two seconds longer, and Frigid upgrades appear twice as often. |
| **Id** | `frigid_silence_2` |

### Initiation: The Kept Watch

The Order of the Dawn holds that there is no such thing as a knight who survived a night, only a pair who did. They are aware this is a technicality on most evenings. They are also aware that on the evenings it is not a technicality it is the only thing that mattered, and they would like to see you act as though you knew which evening you were having.

| | |
|---|---|
| **Unlocked by** | `dawn.shared_light` reaches 40 |
| **Objective** | 100 heals passed to the other knight<br>50 kills that healed<br>5 dawn upgrades taken |
| **Reward** | 2 crystals |
| **Id** | `dawn_initiation` |

### The Longer Half

Every Order in the field measures a knight by what they killed. This one measures you by what reached the person beside you, and it is the only tally where sending more of it away is the better score. Some recruits take a while with this.

| | |
|---|---|
| **Unlocked by** | complete **Initiation: The Kept Watch** |
| **Objective** | 300 heals passed to the other knight |
| **Reward** | **Warm Lantern** - A quarter of every heal you receive reaches the other knight, from wave one. |
| **Id** | `dawn_vigil_1` |

### Both Of You, Standing

The rite is not a demonstration of healing. It is a demonstration of a night going badly and then not going badly, which are two different skills and the Order only cares about the second one. Let it get close. Then do not let it finish.

| | |
|---|---|
| **Unlocked by** | complete **The Longer Half** |
| **Objective** | Finish a wave with both knights at full after one was driven low |
| **Reward** | **Oathbound Locket** - Health orbs heal half again as much. |
| **Id** | `dawn_vigil_2` |

### Draw From The Well

The other branch is unromantic about it. Light has to come from somewhere, it never arrives on a timer, and a knight who gave away more than they gathered is a knight who has arranged for two people to fall over instead of one.

| | |
|---|---|
| **Unlocked by** | complete **Initiation: The Kept Watch** |
| **Objective** | 200 kills that healed |
| **Reward** | 1 crystal |
| **Id** | `dawn_wellspring_1` |

### The Last Light

The Order's final rite is the one thing both branches agree on, which is why it is kept for the end. Once a map, the other knight simply does not fall. It insures the person beside you and never you, and every initiate asks about that exactly once.

| | |
|---|---|
| **Unlocked by** | complete **Draw From The Well** |
| **Objective** | Acquire Last Light, the Dawn capstone |
| **Reward** | **Dawnbreak Crown** - You carry more, and more of what heals you reaches the other knight. |
| **Id** | `dawn_wellspring_2` |

### Initiation: The Standing Order

The Order keeps no barracks and recruits nobody. Their position is that a thing arriving and a thing landing are one problem looked at from either end, and that anyone who has worked that out has already joined whether or not they were asked. The quartermaster finds them insufferable and has never once been able to say why.

| | |
|---|---|
| **Unlocked by** | `feats.guardian_awoken` reaches 1 |
| **Objective** | 60 rocks sent back<br>100 shots guided<br>5 guardian upgrades taken |
| **Reward** | 2 crystals |
| **Id** | `guardian_initiation` |

### Nothing Gets Through

The shafts have been throwing the same stone at this camp for as long as anyone has been counting, and the Order's whole answer is that it is a perfectly good stone. Send enough of them back and the argument stops being philosophical. The gnomes have not adjusted their aim, which the Order takes as a compliment.

| | |
|---|---|
| **Unlocked by** | complete **Initiation: The Standing Order** |
| **Objective** | 250 rocks sent back |
| **Reward** | **Returned Stone** - Rocks you send back hit twice as hard, and Guardian upgrades appear twice as often in your drafts. |
| **Id** | `guardian_wall_1` |

### The Immovable Watch

Every knight before you bought a body's removal with their own health and called it a fair trade. The Order's last teaching is that it was never a trade at all, and that anything which reaches the guard can simply be told to go back the way it came.

| | |
|---|---|
| **Unlocked by** | complete **Nothing Gets Through** |
| **Objective** | Acquire Bulwark, the Guardian capstone |
| **Reward** | 2 crystals |
| **Id** | `guardian_wall_2` |

### It Finds Them

The other half of the Order is less interested in the wall and more interested in the beam, and considers the Wall branch a very elaborate way of standing still. Their position is that an arrow which needed aiming was a badly made arrow. They are difficult to argue with and worse to drink with.

| | |
|---|---|
| **Unlocked by** | complete **Initiation: The Standing Order** |
| **Objective** | 300 shots guided |
| **Reward** | 1 crystal |
| **Id** | `guardian_line_1` |

### Their Own Powder

This is the rite the two branches finally agree on, largely because neither can perform it alone. A powder rock caught on the guard, turned, steered into company and allowed to finish what the mine started. The Order regards it as the plainest possible statement of what they have been saying the whole time.

| | |
|---|---|
| **Unlocked by** | complete **It Finds Them** |
| **Objective** | Catch three at once with one returned powder rock |
| **Reward** | **Worn Baldric** - You begin every run carrying the longer blade. |
| **Id** | `guardian_line_2` |
