---
name: test-mode-validation
description: How to validate gameplay changes in Two Knights using dev Test Mode — jump a run to wave N with chosen upgrades pre-applied, driven programmatically through TestRunConfig + Unity MCP rather than the gamepad UI.
---

# Two Knights — Validating Changes with Test Mode

## What Test Mode is

A dev-only system (`#if UNITY_EDITOR || DEVELOPMENT_BUILD`, compiled out of release)
that starts a run at an arbitrary wave with upgrades already applied to the knights.
Use it whenever a change needs to be observed under mid/late-run conditions —
new upgrades, boss behavior, wave difficulty, poison stacking, HUD state — instead
of playing 12 waves to reach the thing you changed.

The pieces (all paths relative to `Assets/Scripts/`):

| Piece | Role |
|---|---|
| `TestRunConfig.cs` | static carrier: `Set(startWave, leftUpgrades, rightUpgrades)` → `Pending` |
| `Spawner.cs` → `ApplyTestRunConfigIfPending()` | consumes config in `Start()` of the Main scene, after `BeginRun()`, before the first wave |
| `Waves/WaveManager.cs` → `ApplyTestStart(int)` | sets `_completedWavesCount = startWave-1`; startWave **>** gateBossWaveNumber also marks the gate boss beaten this run (== spawns the boss) |
| `UI/TestModePanel.cs` | the human-facing camp UI (collapsible chain list, budget display) |
| `UI/CampMenuController.cs` | reveal combo: LT+RT+LB+RB or F9 on the camp menu, `static _testModeUnlocked` |
| `UI/TestWavePicker.cs` + `Spawner.PickNextWaveThenStart()` | per-wave picker: during a test run, every wave start pauses and lists the scheduled boss + all playable pool waves (with real odds) to choose from |
| `TestRunConfig.AutoPickWave` | static string that bypasses the picker UI (see below) |

While a test run is active (`TestRunConfig.ActiveRun`), `furthestWave` is NOT saved,
but gold, quest progress, stats, and map/boss progress STILL accrue — a test-run
boss kill permanently marks the map cleared. Keep validation runs short of victory
screens unless that's what you're testing.

## The Claude path: skip the UI, set the config directly

The panel is just an editor for `TestRunConfig` — Claude should call it directly
via Unity MCP `execute_code` (see wave-authoring skill for MCP session mechanics).

**Domain-reload gotcha (the one that bites):** entering play mode resets statics,
so a config set in edit mode is WIPED when play starts. Always set the config
*from inside play mode*, then load/reload Main:

1. `manage_editor action:"play"` (any scene; Camp is cleanest — Main will have
   already consumed an empty config, which is fine).
2. `execute_code` (CodeDom, C# 6 — no interpolation, no `?.` on Unity objects):

```csharp
var um = UnityEngine.Resources.Load<UpgradeManager>("UpgradeManager");
var left = new System.Collections.Generic.List<BaseUpgrade>();
var right = new System.Collections.Generic.List<BaseUpgrade>();
// order names low tier -> high tier; Spawner applies in list order
string[] leftNames = new string[] { "Shadow 1", "Shadow 2", "Shadow 3", "Damage 1", "Damage 2", "Damage 3" };
string[] rightNames = new string[] { "Venom Tip 1", "Venom Tip 2", "Virulence 1", "Health Major 1", "Reload 1" };
foreach (string n in leftNames) { foreach (var u in um.AllUpgrades) { if (u.name == n) { left.Add(u); break; } } }
foreach (string n in rightNames) { foreach (var u in um.AllUpgrades) { if (u.name == n) { right.Add(u); break; } } }
TestRunConfig.Set(12, left, right);
TestRunConfig.AutoPickWave = "*"; // REQUIRED for unattended runs — see below
UnityEngine.SceneManagement.SceneManager.LoadScene("Main");
return "queued: wave 12, L=" + left.Count + " R=" + right.Count;
```

3. Verify it took, a beat after the scene loads:

```csharp
var wm = WaveManager.ActiveInstance;
var um = UnityEngine.Resources.Load<UpgradeManager>("UpgradeManager");
string names = "";
foreach (var n in um.GetAppliedUpgradeNames(KnightTarget.LeftKnight)) names += n + ", ";
return "wave=" + wm.CurrentWaveNumber + " leftOwned=[" + names + "]";
```

4. Observe the thing you changed (screenshots, console, reflection reads), then
   `manage_editor action:"stop"`.

Remember from wave-authoring: play mode and screenshots FREEZE while the editor
app is unfocused (`runInBackground = false`) — verify state programmatically, and
only trust time-advancing behavior when the user has the editor focused.

## The per-wave picker and AutoPickWave (do not skip this)

During any test run (`ActiveRun == true`) the Spawner PAUSES before every wave
and shows a picker: scheduled boss + each playable pool wave with its selection
odds, a collapsed "LOCKED WAVES (n)" section holding waves outside their unlock
window (expandable — forcing a locked wave is allowed and `AutoPickWave` matches
them too), and "Random". A human navigates it with dpad/A (B = random); **an unattended
MCP run will stall on it forever** unless `TestRunConfig.AutoPickWave` is set:

- `"*"` — weighted random each wave, no UI. Set this in the same `execute_code`
  that calls `TestRunConfig.Set` whenever you don't care which waves come up.
- `"<wave asset name>"` (e.g. `"NightHunt2"`, case-insensitive, matched against
  `wave.name` of the boss + playable candidates) — forces that wave, no UI.
  Perfect for validating a specific wave: it repeats every wave until the asset
  leaves the playable set (pool waves play once per run, then it logs a warning
  and falls back to weighted random).
- `null`/empty (the default) — picker UI appears; only correct when a human is
  driving.

`Clear()` does NOT reset `AutoPickWave` — it steers the whole run. Reset it to
null yourself when handing the editor back to the user, or their next test run
will silently skip the picker they asked for.

The picker is also the fastest human repro loop: to see one specific wave under
a loadout, set the loadout in the camp panel and pick the wave by hand each time.

## Choosing loadouts that mean something

- `TestRunConfig.Set` does **zero validation** — no budget, no prerequisites. The
  panel's rules are UI-side only. For representative tests, mimic a real run:
  wave N grants **N-1** upgrade levels, left knight gets the odd one
  (L = ceil, R = floor), and every tier includes its lower tiers, listed low→high.
- Deliberately unrealistic loadouts (tier 3 without tier 1, 20 upgrades at wave 2,
  empty loadout at wave 15) are valid stress tests — just say so in findings, since
  balance conclusions drawn from them don't transfer.
- Upgrade identity = **asset name** (`u.name`), not `UpgradeName` (display names
  repeat across tiers). Families as of 2026-07-18: `Damage 1-4`, `Fire Speed 1-2`,
  `Reload 1-4`, `Health Minor/Mid/Major/Epic 1-4` (13 assets, DAG-linked),
  `Shadow 1-5`, `Killing Blow 1-2`, `Phantom Blade 1-2`, `Shuriken Fan 1-2`,
  `Thousand Cuts`, `Venom Tip 1-3`, `Virulence 1-2`, `Miasma 1-2`,
  `Serpents Breath 1-3`, `Plaguebringer`. Enumerate live from
  `Resources.Load<UpgradeManager>("UpgradeManager").AllUpgrades` rather than
  trusting this list.

## Boss / deep-run shortcuts

- Gate boss fight now: `Set(gateBossWaveNumber, ...)` (default map: wave 10 —
  read `MapDefinition.GateBossWaveNumber`, don't assume).
- Post-gate deep waves: any startWave **past** the gate wave — gate is treated as
  beaten this run, true boss still schedules at its own wave.
- True boss: startWave ≥ trueBossWaveNumber with startWave > gate wave.

## Validating the Test Mode UI itself

Only when the panel/combo is what changed. The combo needs real input, but the
unlock is just a static: via `execute_code` reflection, set
`CampMenuController._testModeUnlocked = true` (private static, `BindingFlags.NonPublic |
BindingFlags.Static`), reload the Camp scene in play mode, and the button registers
itself; `TestModePanel` can then be shown with `GetComponent` + `Show()`. UI
verification rules from wave-authoring apply (forced repaint before `resolvedStyle`,
CodeDom needs `UQueryExtensions.Q(root, "name", (string)null)` statics).

## When NOT to use Test Mode

- Pure logic changes with no runtime observation needed — the offline csc compile
  check (unity-offline-editing-recipes memory) is faster.
- Anything about the wave 1-N *progression itself* (upgrade draft weights, wave
  selection windows, order affinity) — Test Mode bypasses exactly that machinery;
  play a real run from wave 1 instead.
- Testing on a REAL save's progression state — Test Mode still mutates gold/quests/
  map progress; back up `SaveManager` data or use a wiped save.
