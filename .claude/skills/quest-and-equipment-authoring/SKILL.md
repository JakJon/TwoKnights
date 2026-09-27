---
name: quest-and-equipment-authoring
description: How to author quests, equipment, and specials in Two Knights — the Quest/QuestBuild contract, unlock gating via stats, EquipmentDefinition and SpecialDefinition, the EquipmentCatalog registry, icon art, and the traps that silently produce content that never appears or never fires.
---

# Two Knights — Quest & Equipment Authoring

Covers the whole between-run layer: **quests** (C#), **equipment** and **specials**
(ScriptableObject `.asset`), the **camp shop**, and the **icons** all three need.

Runtime upgrades drafted inside a run are a different system — see the Orders in
`Assets/Upgrades/` and `BaseUpgrade`. Equipment is the persistent counterpart: chosen in
camp, kept across runs, applied once at `Spawner.Start`.

---

## The shape of the thing

```
quest completes ──► CrystalBank.Add        (currency)
                └─► Loadout.Own(id)        (grants an item)
                └─► equipmentSlots = 2     (Crimson Twins)
                └─► specialSlots = 2       (The Gold Cart — both specials fire on one bar)

camp shop ──► CrystalBank.TrySpend ──► Loadout.Own(id)

Loadout ──► EquipmentDefinition.Apply(knight) ──► EquipmentBoost / Order boosts
                                                        ▲
                                          combat systems read this, never the item
```

**Equipment never contains gameplay logic.** `Apply` writes numbers into a boost
component; the systems that already own the behaviour read them. A new item must not
reach into combat code, and combat code must not learn what an item is.

---

## Hard rules

1. **Register or it does not exist.** Every equipment `.asset` and special `.asset` must
   be listed in `Assets/Resources/EquipmentCatalog.asset`. An unregistered item resolves
   to null everywhere and fails silently.
2. **Ids are save keys. Never change one after a build ships** — saves store the string.
   Renaming an id orphans whatever the player owned.
3. **Shop items must have non-empty `effect`.** That is the line the purchase decision
   rests on. A blank one ships an item nobody can evaluate.
4. **Route through the Order's own boost**, never a parallel path. Poison goes through
   `PoisonTipBoost`, fire through `EmberBoost`, shadow through `NinjaBoost` /
   `ShadowArrowBoost`. Duplicating their logic in equipment means two things to keep in
   step forever.
5. **The ignition pillar holds.** The only legal callers of `EnemyBase.Ignite` are
   `PlayerProjectile`, `FireballProjectile`, and `ShieldSight`. An item that grants ignite
   raises the chance on `EmberBoost`; it does not light anything itself. See
   `EnemyBase.cs` around the `Ignite` declaration and `Docs/Design/ember-order.md`.
6. **Quest prose carries no numbers.** Numbers live in the objective line and in an item's
   `effect`. Same house rule as upgrades.

---

## Quests

Quests are **C#, not `.asset`** — a line is only worth reading as a whole, and the chain,
its gates and its rewards belong in one diffable block.

Files: `Assets/Scripts/Quests/Lines/{Forest,Mine,Serpent,Ember,Shadow}Quests.cs`,
aggregated in `QuestDatabase.Build()`. A new line file must be added there or it never loads.

### Anatomy

```csharp
using static QuestBuild;   // gives you Forest / Mine / Camp, Obj, One, After, Stat, Gate, Reward

yield return new Quest(
    id:          "camp_cleanup_2",          // stable save key, snake_case
    name:        "Camp Cleanup II",
    description: "Clearing them out taught them very little except to come at a " +
                 "different hour. The tally board by the mess tent has been wiped " +
                 "down and started again. The quartermaster insists this is normal.",
    mapId:       Forest,                    // Forest | Mine | Camp ("" — Order lines, camp business)
    objectives:  One("kills.map.camp_fields", 1000),
    reward:      Reward(crystals: 2),
    unlocks:     Gate(After("camp_cleanup_1")));
```

### Objectives

`One(...)` for the common case, `new[] { Obj(...), Obj(...) }` when a quest needs several
met **together** — that is what the Order initiations are for.

```csharp
objectives: new[]
{
    Obj("applied.poison", 100, "enemies poisoned"),
    Obj("kills.poisoned",  50, "slain by venom"),
    Obj("upgrades.order.serpent", 5, "serpent upgrades taken"),
},
```

The third argument overrides the stat's own short label. The fourth, `hideProgress: true`,
suppresses the counter — use it whenever `0/1` would give the answer away:

```csharp
objectives: One("maps.camp_fields.gate_cleared", 1,
                "Venture further into the forest", hideProgress: true),
```

That quest is "beat wave 10" and must never say so.

`countAfter: true` is for a label that is a whole sentence with the target already in
it. The log prints the count after the label instead of in front of it, and the offer
and completion cards show the label alone:

```csharp
objectives: One(TargetRange.PatternsStat, 5,
                "Complete all 5 different target range patterns successfully.",
                lifetime: true, countAfter: true),
// quest log: "Complete all 5 different target range patterns successfully. 2 / 5"
```

### Unlock gating

`Gate(...)` takes conditions; default is **all** must hold. `unlockMode: UnlockMode.Any`
makes one enough — the Shadow Order opens on Shadow Arrow II *or* the first Shuriken Fan.

```csharp
unlocks: Gate(Stat("applied.poison", 20)),                       // a stat threshold
unlocks: Gate(After(ForestQuests.Cleanup1)),                     // another quest finished
unlocks: Gate(Stat("upgrades.taken.shadow_2"),
              Stat("upgrades.taken.shuriken_fan_1")),
unlockMode: UnlockMode.Any,
```

**There is one unlock mechanism, not two.** Finishing a quest publishes the stat
`quests.<id>.completed`, which is what `After()` reads. Do not add a separate prerequisite
field — chains cascade for free because the stat write re-enters `QuestProgress.Evaluate`.

A locked quest is **hidden entirely**, not greyed out. Half the content is Order lines
whose existence is itself a reveal.

### Rewards

```csharp
Reward(crystals: 2)
Reward(equipmentId: "fangbone_charm")
Reward(extraSlot: true)                    // only the Crimson Twins quest does this
Reward(extraSpecialSlot: true)             // only The Gold Cart does this
Reward(crystals: 1, equipmentId: "waxed_cord")
```

There is no honor reward — that system was removed in save v7. Crystals are the only
currency.

### The Order line shape

Order lines are not chains. A light trigger reveals **only** the initiation; the
initiation admits you and opens **both branches at once**, which then run in parallel.

```
poison 20 enemies ──► INITIATION (passed by its Order trial)
                            │
                    ┌───────┴───────┐
                 BRANCH A        BRANCH B
                (2 quests)      (2 quests)
```

**Initiations are passed by Order trials, not counters** (owner, 2026-09-23). Each
initiation has ONE objective reading `trials.<order>` (3 phases, or 1 for the
Paladin's bouts), and the trial itself plays between waves — see
`Docs/Design/order-trials.md` and `Assets/Scripts/Trials/`. The trigger gate is
still a light stat threshold. The offer text is where the NPC explains the trial,
because fire, ice and venom trials arrive unannounced. Don't put stat counters
back on an initiation.

Map lines are ordinary chains. Explorer lines are **per map** (`waves.distinct.<mapId>`),
one in the forest and one in the mine — there is no global explorer line.

### Writing the prose

Descriptions are **paragraphs**, 3-5 sentences, plain and grounded. Terse portentous
one-liners were tried and rejected. Write like someone describing a situation, not a
fantasy blurb. Recurring threads to pull on: the quartermaster and his tally board, the
scouts whose maps disagree, the Orders finding you rather than the reverse.

> The animals have been coming out of the treeline wrong — too many of them, too bold, and
> all headed the same direction. The cook thinks it's the weather. The quartermaster thinks
> someone upstream has been dumping something. Neither of them has been far enough in for
> their opinion to be worth much.

Keep objective labels consistent with what already exists. Depth is always
`maps.<id>.furthest_wave` phrased "N waves into the forest/mine" — never mix in the
lifetime counter for depth.

---

## Stat keys you can build objectives from

| Key | Meaning |
|---|---|
| `kills.<statkey>` | per enemy class — `rat`, `bat`, `darkbat`, `wolf`, `slime`, `ratking` |
| `kills.family.<family>` | `vermin`, `beast`, `ooze`, `cart` (see `EnemyFamily`) |
| `kills.map.<mapId>` | kills on one map |
| `kills.poisoned` / `.burned` / `.blasted` / `.executed` | how it died |
| `applied.poison` / `applied.ignite` | enemies affected, counted **once each** |
| `maps.<mapId>.furthest_wave` | high-water depth (skipped for test runs) |
| `maps.<mapId>.gate_cleared` / `.true_cleared` | boss kills |
| `waves.distinct.<mapId>` | wave types met on that map |
| `upgrades.taken.<slug>` | a specific upgrade asset, e.g. `shuriken_fan_2` |
| `upgrades.order.<order>` | Order picks, cumulative across runs |
| `quests.<id>.completed` | use `After()` rather than writing this by hand |

### Feats — the interesting objectives

Kill counters answer "how much"; **feats** answer "can you", and they are what makes a
quest worth reading. Keys live in `Feats.cs` so the detector and the quest that reads it
cannot drift apart. Every one needs a deliberate detector — none falls out of an existing
counter.

| Key | Detected in |
|---|---|
| `Feats.PoisonCloudFour` | `PoisonCloud.PoisonEnemiesInside` — per cloud, once |
| `Feats.FireTrailsFour` | `FireField.Add`, counting only `isTrail` zones |
| `Feats.ShurikenVolleyFour` | a `ShurikenVolley` instance shared by one fan |
| `Feats.PhantomFullThree` | `SwordSwing`, per swing phase that connected |
| `Feats.ShadowArrowHits` | `PlayerProjectile`, guarded so one arrow counts once |
| `Feats.RatKingBare` / `TwinsBare` | `WaveManager.WaveCompleted`, gated on `RunPurity.Bare` |

Two patterns worth copying:

- **Scope the counter to the thing being tested.** "Four from one volley" gets an object
  shared by that volley's projectiles — no registry, no cleanup, it dies with them. A
  global tally would have answered a different question.
- **Guard every counter against double-counting.** A projectile that hits, a cloud that
  ticks, a swing phase that sweeps — all of them fire more than once.

`RunPurity` tracks "no equipment carried, no special fired". A knight always *has* a
special (stock ones cannot be unequipped), so the restriction is on **using** it. Reset it
from `Spawner.Start` every run — it is static and survives scene reloads.

Feats earned in Test Mode are excluded the same way `furthest_wave` is.

Slugs come from the **asset** name lowercased with spaces to underscores
(`Shuriken Fan 2` → `shuriken_fan_2`). Add readable names to `StatsDatabase` for anything
new, or the log shows the raw key.

Adding a new emitter: write it wherever the event happens and let `PlayerStats.Flush()`
persist it. **Never call `SaveManager.Save()` per event** — stat writes are batched
deliberately; they used to hit disk on every kill.

---

## Equipment

One `.asset` per item in `Assets/Equipment/`, plus an entry in the catalog.

### Pick the right class first

| Class | Use when |
|---|---|
| `StatEquipment` | almost always — it already covers health, cooldown, head start, poison, ignite, fire, shadow arrows, phantom echoes, confusion |
| `FamilyBaneEquipment` | extra damage against one `EnemyFamily` |
| `BlastWardEquipment` | reducing one `DamageKind` |
| a new class | only for genuinely new behaviour that has no field yet |

Twelve of the sixteen shipped items are `StatEquipment`. What differs between them lives in
the `.asset`, not in code. **Adding a field to `StatEquipment` beats adding a class.**

### Equipment always stacks (owner's rule, 2026-09-10)

Two items touching the same number BOTH count, and an item stacks on top of drafted
upgrades rather than acting as a floor under them. Advertised multipliers multiply
(2x and 2x drafts = 4x; 1.5x and 1.5x damage = 2.25x); flat amounts add. Never write an
equipment setter with `Mathf.Max` — and never route equipment through an Order's
monotonic tier setter (`SetEchoFraction`, `SetFrostTip`, ...), because its `Max` swallows
the item. Give the boost a separate equipment field instead (see
`DawnBoost.AddEchoFractionFromEquipment`, `FrigidBoost.AddFrostTipFromEquipment`).

**Damage bonuses go through `EquipmentBoost.ScaleHit(damage, enemy, knightTag)`** at
every place a knight's projectile or blade hurts an enemy — arrows, fireball blasts,
guard rebounds and their bursts, the sword and Rimeblade. A new knight projectile that
skips it silently ignores every bane item. Damage-over-time ticks (poison, burn, fire
ground, Shield Sight beam) deliberately do not use it.

### The asset

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: <the class's .cs.meta guid>, type: 3}
  m_Name: Padded Gambeson
  m_EditorClassIdentifier:
  id: padded_gambeson
  displayName: Padded Gambeson
  description: 'Quilted linen, and rather more of it than is comfortable.'
  effect: '+20 maximum health.'
  icon: {fileID: 21300000, guid: <the png's meta guid>, type: 3}
  crystalCost: 2          # 0 = quest reward only, never sold
  stats:
  - valueText: '+20'
    labelText: MAX HEALTH
    isPositive: 1
  maxHealthBonus: 20
```

Hand-authoring is fine and supported: write the `.asset` plus a `.meta` with a guid from
`openssl rand -hex 16` (or `head -c 16 /dev/urandom | od -An -tx1 | tr -d ' \n'`), and
Unity adopts it on refresh. **A Single-mode sprite's sub-asset fileID is always
`21300000`** — that one is safe to write by hand, unlike animation clips.

Omitted fields keep their C# initializer defaults, so an asset only needs the lines it
cares about.

### description vs effect

- `description` — flavor. One sentence, second person, **no numbers**.
- `effect` — exactly what it does, **with** the numbers.

`ItemText.Detail` renders both (effect plain on top, flavor dimmed and italic below) in the
shop and the equipment screen. Never put mechanics in `description`; several early items
did and read as instructions rather than colour.

### Never print world units to the player

This holds for every player-facing string in the game — equipment `effect`, upgrade
`description`, and every `stats` `valueText`/`labelText`, in `Assets/Equipment/` and
`Assets/Upgrades/` alike.

A Unity world unit means nothing to someone holding a controller. Text like `1.6u`,
`2.3 units`, or `BURST RADIUS 2.3u` is a number the player cannot check against anything
they can see, so it reads as noise and it dates the moment the value is retuned.

- **Tier one states the thing exists**, without a size. "Your sword swings can throw off a
  burst of cold." No radius line at all.
- **Later tiers state the change as a percentage**, e.g. `44%` / `LARGER BURST`,
  `20%` / `LONGER REACH`. A player can feel "bigger than what I had"; they cannot feel
  "2.3".
- **If a tier does not grow the size, it gets no size line.** Repeating the previous
  tier's number reads as a gain that isn't there.

Units are fine in the places players never see: C# comments, `[Tooltip]` strings, and the
design docs under `Docs/Design/`. The rule is about what reaches the screen.

### Ownership and equipping

`Loadout` is static and save-backed. Equipment is **exclusive across knights** — you own
one Gnawed Crown, so equipping it on the right takes it off the left. That is what makes a
single slot a real choice. Specials are **not** exclusive; there is no physical object to
argue otherwise.

Buying never auto-equips.

---

## Specials

A knight's meter ability. `SpecialDefinition` adds two fields to the equipment shape:

- `ownedFromTheStart` — true for Rapid Fire and Field Mending, the two stock specials.
  They never appear in `ownedEquipment`; `Loadout.IsSpecialOwned` asks the definition.
- `freezeGainSeconds` — blocks special **gain** while the effect runs.

**The freeze trap:** `RapidFire.ActivateRapidFire` already calls `FreezeSpecialGain`
itself, so `Rapid Fire.asset` leaves `freezeGainSeconds: 0`. Any special whose effect
does not self-freeze must set it, or its own hits refill the bar mid-effect. Timed
specials that heal or protect (Iron Vigil, Blood Tithe) set it to their duration.

Order inside `PlayerSpecial`: spend and redraw the bar **before** freezing. `updateSpecial`
returns early while frozen, so freezing first leaves the bar drawn full over an empty meter.

A special that needs to react to events over a window gets a small runtime component —
see `BloodTitheWindow`, which subscribes to `EnemyBase.OnEnemyKilledBy` in
`OnEnable`/`OnDisable` and gates on a deadline rather than adding and removing itself.

`PlayerSpecial` resolves `Loadout.ResolveSpecials(knightId, stock)` — a LIST, because The
Gold Cart raises `specialSlots` to 2 and both go off on one full bar. `stock` falls back to
sniffing which component the prefab carries, so the two original knights work with no prefab
wiring at all; it applies to **slot 0 only**, since a slot unlocked later has nothing stock
about it. Every slot's freeze is applied before the first `Activate`, so a short special
cannot cut a long one's window.

---

## Icons

Every item and special needs one. **The filename must equal the item's `id`** — that
is how the wiring pass finds it.

- Location: `Assets/Graphics/Equipment/<id>.aseprite` — **never a PNG.** See
  `sprite-authoring`; PNG sprites are banned project-wide and none remain.
- Size: **32×32**. The 16×16 set was redrawn in Aug 2026 because a crown read as a
  blob and a quilted coat and a coiled rope both read as wooden crates — ~200 usable
  pixels is not enough for sixteen distinguishable objects.
- Palette: **per item, from the existing ramps** in `Assets/Graphics/palletes/` — bone
  and venom green for the Serpent charms, ember for fire, cave-crystal violet for
  Shadow, leather browns for worn gear. Not one house scheme.

Workflow:

1. Compose the icon procedurally (`poly`/`disc`/`bevel`/`outline` helpers beat hand-typed
   32-row ASCII), then draw it through the Aseprite MCP and `save_as` into
   `Assets/Graphics/Equipment/`.
2. **Read the contact sheet and actually judge it.** First passes are routinely
   unreadable. Check silhouettes against each other, not just individually — the two
   fangs, the two crowns and the two quivers all needed deliberate separation.
3. Alpha-guard the canvas corners and clear the importer's cached rect, or the icon
   imports trimmed to its art and renders at the wrong scale. Full detail under
   "The trim trap" in `sprite-authoring` — this is not optional.
4. Assign it to the asset's `icon` through `AssetDatabase.LoadAssetAtPath<Sprite>` over
   the MCP bridge. An .aseprite sprite's fileID is generated per asset, so unlike a
   PNG's fixed `21300000` it **cannot** be hand-authored in YAML.
5. Verify every catalog entry resolves: non-null icon, path ending `.aseprite`, rect
   32×32, Point filter.

Icons render at `.row-icon` (32px) and `.reward-square-image` (32px) in `CampMenu.uss` —
both 1:1 with the source art. Changing icon size means changing those too, or the art
lands on a fractional scale.

Shared UI art (the crystal and equipment-slot icons) hangs off `EquipmentCatalog`, not
`Resources.Load` — the catalog is already the one true singleton the UI can reach.

---

## Camp UI implications

If content changes force a UI change, the camp splits two ways:

- **Self-polled input** (`QuestPanel`, `EquipmentPanel`, `ShopPanel`, `MapSelectPanel`) —
  anything needing left/right. Each **must** be listed in
  `CampMenuController.PanelPollsOwnInput()` or the camp reacts to the same press.
- **Camp action set** (`StatsPanel`) — up/down/confirm/cancel only.

Two rules that bite:

- Rows built as plain `VisualElement`s never scroll themselves into view. Call
  `ScrollView.ScrollTo` on every selection change or a controller can select what it
  cannot see.
- Arm an entry-input grace on `Show()`, or the press that opened the panel immediately
  activates something inside it.

Everything must be reachable on a controller. Hover-only information is a bug.

---

## Verify

**Compile offline first** — `scratchpad/compile.sh` runs Unity's own Roslyn over
`Assembly-CSharp` in seconds without launching the editor. Then refresh through the bridge
and check `read_console` for `CS` errors.

Then prove the content actually resolves, in the editor rather than by reading:

```csharp
var cat = Resources.Load<EquipmentCatalog>("EquipmentCatalog");
// every item registered, with art and an effect line
foreach (var e in cat.Equipment) { /* e.Icon != null, e.Effect non-empty */ }
// every quest reward id resolves to a real item
foreach (var q in QuestDatabase.All)
    if (q.Reward.GrantsEquipment && cat.Find(q.Reward.EquipmentId) == null) /* broken */;
```

Also worth checking after any asset edit: **hand-written YAML field names must match the
C# field names exactly**, or values silently stay at their defaults. Dump each asset's
serialized properties and confirm nothing reads as "no effect set".

Gating is worth testing directly rather than by inspection — set stats, call
`QuestProgress.Evaluate` (private, reach it by reflection), and assert that the initiation
reveals *only* itself, that both branches open on admission, and that chains cascade.

## Regenerating the quest reference

`Docs/Design/quests.md` is **generated** from `QuestDatabase` by `scratchpad/gendoc.py`.
Never hand-edit it; change the line files and regenerate.

Run that generator from a **`.py` file, not a bash heredoc** — backticks inside the C#
payload get command-substituted by the shell and silently gut the output.

---

Related skills: `wave-authoring` (the waves explorer quests count),
`test-mode-validation` (jumping a run to a wave to reach a boss gate fast),
`sprite-authoring` (the wider pixel-art pipeline and the Aseprite MCP).
