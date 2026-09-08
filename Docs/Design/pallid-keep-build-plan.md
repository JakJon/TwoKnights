# Pallid Keep — build plan (handoff)

*Written 2026-08-18 for a fresh agent. Read `pallid-keep.md` first for the design intent;
this document is the work order.*

---

## STATUS — built 2026-08-18

All four tasks are done. What actually shipped, and where it differs from the plan below:

- **Task 4 (recolour) was already done** before this session started, and correctly — all
  three files, both layers, both frames, exact target hexes, frame ramp untouched. Verified
  by dumping every cel with Aseprite rather than re-run. The script in section 4 was not
  executed.
- **Task 5 (the mirror system)** is `Assets/Scripts/Mirrors/` — `MirrorColor`, `MirrorPane`,
  `MirrorPaneSet`, `MirrorLayout`, `MirrorNetwork`, `MirrorPassenger`. `Spawner.Mirrors`
  is the wave-facing handle, matching `Spawner.Rails`.
- **The redirect rule (the blocking decision)** is authored on the layout as
  `MirrorRedirect`, with **both** rules implemented. Default is `MirrorAboutFacing` — true
  portal behaviour, which is what "one pair aimed at each other" describes.
- **Tasks 6 and 7 (map and wave)** are built by `Assets/Editor/PallidKeepWiring.cs` on
  Unity's next compile, because sprite sub-asset fileIDs cannot be hand-authored. Nothing
  in section 6's "order of operations" blocker applies any more.
- **Section 3's permissions note is still unresolved** — the settings edit was refused by
  the sandbox classifier.

Two facts found while building that this plan did not know:

- **Player arrows are destroyed after 4 seconds** (`PlayerShooter.projectileLifetime`), so
  "loops forever" is four seconds of laps. The loop is real; the timer is what ends it.
- **Only an exactly-level shot loops.** Flat mirrors cannot focus, so a shot entering the
  loop pair at a slight climb keeps that climb every pass and walks out of the top. This is
  true of *both* candidate redirect rules, not a consequence of the one chosen.

The goal: add the castle map to the game alongside the forest and the mine, recolour the
mirror sprite into its four twins, and ship one test wave called **"Looking Good"** that
puts a pair of every mirror colour on the board with no enemies, purely to prove the mirror
mechanic works.

---

## 1. What is already done — do not redo

- **The Pallid Keep palette exists.** 34 colours, 7 groups.
  - Aseprite strips in `Assets/Graphics/palletes/`: `background_keep`, `keep_wall`,
    `keep_stone`, `keep_grit`, `keep_spectre`, `keep_portal`, `keep_full`.
    `keep_full.aseprite` is all 34 in one 34×1 row.
  - GPL copies in `Docs/Design/Palettes/pallid-keep-*.gpl`, plus a combined
    `pallid-keep-environment.gpl` and a flat `.hex`.
  - Reference images: `pallid-keep-swatches.png`, `pallid-keep-mock.png`.
- **The design doc exists**: `Docs/Design/pallid-keep.md` — portal rules, the two skeleton
  types, the Warden, art specs, palette rationale, open questions.
- **The user has drawn two new files** (both untracked, both **not yet imported by Unity**):
  - `Assets/Graphics/palletes/mirror_green.aseprite`
  - `Assets/Graphics/background_castle.aseprite`

## 2. What does not exist yet

**There is no mirror/portal system in the codebase at all.** No component, no layout asset,
no projectile hook. Task 3 below is a from-scratch build, and it is the bulk of this work.

There is also no castle `MapDefinition`, no castle wave folder, and no entry in the map
catalog.

---

## 3. Environment facts worth not rediscovering

**Aseprite** is at `C:\Program Files (x86)\Steam\steamapps\common\Aseprite\Aseprite.exe`
(version 1.3.18.2). Two traps:

- Invoke it from PowerShell with `Start-Process -NoNewWindow -Wait -PassThru`. The `&` call
  operator **silently does nothing** under the tool sandbox — no output, no file written,
  empty `$LASTEXITCODE`. This will look like the script failed when it never ran.
- Lua `print` never reaches stdout. Write results to a file with `io.open` and read that
  back.

**Scale:** 32 pixels per unit. A 32×32 sprite is 1 world unit. Mob size comes from a
`scale` field, not a bigger canvas — see `EnemyGiantSlime.Config.scale`, where the giant is
`6` and a size-3 slime is `3`.

**Permissions:** the user wants zero approval prompts. `.claude/settings.local.json` has
~50 exact-match `Bash(...)` entries and no `PowerShell(...)` rule at all, so every
PowerShell call prompts. This was left unresolved — sort it out before starting or the run
will stall constantly.

**Unity may or may not be running.** I never confirmed it. If the `mcp__UnityMCP__` tools
are absent, drive the bridge directly over raw JSON-RPC at `127.0.0.1:8080/mcp`
(initialize → grab the session header → `execute_code`); no restart needed.

---

## 4. Task — recolour the mirror into four twins

`mirror_green.aseprite` is **32×64, RGB, 2 layers × 2 frames** (it has a 2-frame shimmer).

| layer | contents |
|---|---|
| Layer 1 | pane body — `2a8a52` (1656 px) plus the frame in `2e211e` / `48332a` / `654837` |
| Layer 2 | glow — `7ef09a` (28 px), bounds 6,8 20×50 |

Note the frame is painted in the keep's **grit** ramp (rotted wood), not the
`portal-frame` greys from the palette. That is the user's call and it is shared by all four
panes — **leave the frame alone**. Only the two greens move:

| variant | body `2a8a52` → | glow `7ef09a` → |
|---|---|---|
| `mirror_cyan` | `228494` | `7aecfa` |
| `mirror_vermilion` | `9c3828` | `ff7a54` |
| `mirror_magenta` | `922e7a` | `f682da` |

Save the three new files next to the green one in `Assets/Graphics/palletes/`.

**The trap:** the recolour must touch every cel — 2 layers × 2 frames. A swap that only
hits frame 1 leaves the shimmer flashing the old hue, and it will not be obvious in the
Aseprite thumbnail.

This script was written and verified against the file's actual contents but **never
executed** (the run was interrupted). Run it as-is:

```lua
local dir = "C:\\Projects\\Two Knights\\Assets\\Graphics\\palletes\\"
local src = dir .. "mirror_green.aseprite"
local GREEN_BODY, GREEN_GLOW = "2a8a52", "7ef09a"

local variants = {
  { name = "mirror_cyan",      body = "228494", glow = "7aecfa" },
  { name = "mirror_vermilion", body = "9c3828", glow = "ff7a54" },
  { name = "mirror_magenta",   body = "922e7a", glow = "f682da" },
}

local function parse(h)
  return tonumber(h:sub(1,2),16), tonumber(h:sub(3,4),16), tonumber(h:sub(5,6),16)
end

for _, v in ipairs(variants) do
  local s = app.open(src)
  local br,bg,bb = parse(v.body)
  local gr,gg,gb = parse(v.glow)
  local sbr,sbg,sbb = parse(GREEN_BODY)
  local sgr,sgg,sgb = parse(GREEN_GLOW)

  for _, layer in ipairs(s.layers) do
    for _, cel in ipairs(layer.cels) do          -- every cel: 2 layers x 2 frames
      local img = cel.image:clone()
      for y = 0, img.height - 1 do
        for x = 0, img.width - 1 do
          local p = img:getPixel(x, y)
          local a = app.pixelColor.rgbaA(p)
          if a > 0 then
            local r,g,b = app.pixelColor.rgbaR(p), app.pixelColor.rgbaG(p), app.pixelColor.rgbaB(p)
            if r==sbr and g==sbg and b==sbb then
              img:drawPixel(x, y, app.pixelColor.rgba(br,bg,bb,a))
            elseif r==sgr and g==sgg and b==sgb then
              img:drawPixel(x, y, app.pixelColor.rgba(gr,gg,gb,a))
            end
          end
        end
      end
      cel.image = img
    end
  end
  s:saveAs(dir .. v.name .. ".aseprite")
  s:close()
end
```

Afterwards, get Unity to import all four so their sprite sub-assets exist.

---

## 5. Task — the mirror system (the big one)

Nothing here exists. Build it as a direct sibling of the rail system, which solves the same
shape of problem and is the house pattern.

**Read these first** — they are the templates:

- `Assets/Scripts/Rails/RailLayout.cs` — a track shape as a ScriptableObject. Runs, loose
  tiles, teleport links, drop points; `BuildPlacements` and `BuildLines` resolve it into
  world space against the camera bounds.
- `Assets/Scripts/Rails/RailNetwork.cs` — owns the live track. `Lay(layout)` clears the old
  one and builds the new; `LaySettled(layout)` builds it with no fall animation; singleton
  via `Instance`; everything parented under one holder so the next wave's clear takes it all
  down.
- `Assets/Scripts/Rails/RailTeleporter.cs` — **this is already a portal.** An exit pad
  linked to an entry pad; a cart reaching the exit reappears at the entry and keeps going.
  The pairing, the hand-off and the gizmos are all solved here. Reuse the shape.

Suggested pieces:

- **`MirrorColor`** — enum: `Green`, `Cyan`, `Vermilion`, `Magenta`.
- **`MirrorPane`** — a MonoBehaviour on the pane prefab. Holds its colour, its orientation,
  a reference to its twin, and the trigger that catches projectiles.
- **`MirrorLayout`** — ScriptableObject, sibling of `RailLayout`. A list of pane entries:
  position, orientation (tall/wide), colour. Waves name a layout asset rather than
  hardcoding coordinates.
- **`MirrorNetwork`** — sibling of `RailNetwork`. `Lay(layout)` / `Clear()`, pairs panes by
  colour, warns loudly if a colour appears once or three-plus times.
- **The projectile hook** — read `Assets/Scripts/PlayerProjectile.cs` and
  `Assets/Scripts/ProjectileMovement.cs` before deciding where this lives. **I did not read
  either file**, so treat the integration point as unknown.

Rules from the design doc:

1. Only projectiles pass through — not enemies, not knights, not pickups. (Enemies passing
   through is reserved for a boss wave, far later.)
2. Colour is the pairing. Green goes to green. A colour appears exactly twice on the board.
3. Both faces work; a pane is not one-way.
4. Panes can move on a fixed path later. For now they are all stationary.
5. Work out the exit at the moment the shot enters; don't track a moving pane afterwards.
6. Your own arrow can come back and hit you. That is intended.

### Decision needed before writing the redirect

The test wave asks for a pair that loops forever, and the two plausible rules disagree
about how to get one:

- **Absolute direction preserved** — a shot travelling right enters pane A and leaves pane
  B still travelling right. Under this rule, two panes *facing each other* do **not** loop;
  the shot exits and flies away. You get a loop by putting the exit pane **upstream** of the
  entry pane along the travel axis (entry on the right, exit on the left, shot travelling
  right — it runs the same stretch over and over). This is exactly how `RailLayout`'s
  teleport table wraps a straight track around off-frame.
- **Direction mirrored about each pane's facing** (true Portal behaviour) — a shot enters
  A's front and leaves B's front, so two panes facing each other bounce it back and forth
  forever.

The user described the loop as *"one pair aimed at each other"*, which matches the second
rule. The design doc currently specifies the first. **Resolve this with the user before
building** — it changes the redirect maths and it changes how the test wave is laid out.

---

## 6. Task — add the castle map

### How the Mine was added (the pattern to copy)

- **`MapDefinition`** (`Assets/Scripts/Maps/MapDefinition.cs`) is the ScriptableObject. Read
  the file — the field comments explain how the Camp Fields and the Mine deliberately answer
  the gate-boss question differently.
- **The asset** lives at `Assets/Scripts/Maps/The Mine.asset`. Read it as YAML; it is the
  cleanest template. Notable values: `mapId: mine`, `unlockedByDefault: 0`,
  `gateBossWaveNumber: 10`, `gateBossRepeats: 0`, `gateBossEndsRun: 0`,
  `trueBossWaveNumber: 20`, `strongerEnemiesFromWave: 1`.
- **Stages** carry the backdrop. Each stage is `label`, `fromWaveNumber`, `backdrop`,
  `foreground`, `canopy`, `ventureLine`. The Mine's second stage pulls `backdrop` and
  `foreground` from **two different fileIDs inside the same `.aseprite` guid** — that is an
  Aseprite imported with Layer Import Mode = Individual Layers.
- **The catalog** is `Assets/Resources/MapCatalog.asset`, loaded by
  `Resources.Load<MapCatalog>("MapCatalog")`. A new map must be appended to its `maps` list
  or it never appears in level select. This is the step that is easy to forget.
- **Unlocking** is a string on the *previous* map: set `unlocksMapId` on The Mine to the
  castle's `mapId`. The Mine currently has it blank.
- **Waves** live in `Assets/Scripts/Waves/Cave/<WaveName>/`, typically four numbered
  variants per idea (`Choo Choo 1..4`). The map's `waveFolders` list names each folder, and
  the asset's `Find Waves In Folders` context menu adds what it finds. That menu **adds
  rather than replaces**, on purpose.

### The castle backdrop

`background_castle.aseprite` is **640×360, 1 frame, 2 layers**:

| layer | bounds | colours |
|---|---|---|
| Layer 1 | 0,0 640×360 | `111c3c`, `0a1228` — the base fill |
| Layer 2 | 34,0 595×360 | `101937`, `070b1a`, `0b1126`, `0a1228`, `111c3c` — the castle art |

Layer 2 is drawn almost entirely in the **wall** ramp and covers nearly the whole canvas, so
it is very unlikely to be a `foreground` overlay in the `MapDefinition` sense (that would
black out the arena). Treat it as art meant to sit on top of the base fill, and confirm the
intended import mode with the user before wiring it.

### The blocker you will hit immediately

**Neither new `.aseprite` has a `.meta` file, so Unity has not imported them.** Sprite
sub-asset fileIDs are generated by the importer and cannot be hand-authored offline. You
cannot write the castle `MapDefinition`'s `previewImage` or `stages[].backdrop` references
until Unity has imported `background_castle.aseprite` and you can read the generated guid
and fileID back.

Order of operations: get Unity to import the art → read the guid/fileID → then author the
map asset. Trying to shortcut this produces an asset with broken sprite references that
looks fine in YAML and is blank in the editor.

### Suggested values

Nothing here is decided; `pallid-keep.md` sketches roughly 20 waves.

```
mapId: pallid_keep
displayName: Pallid Keep
unlockedByDefault: 1        # while it is being built out — gate it behind the
                            # Mine's Millstone once it has a real setlist,
                            # exactly as The Mine was handled
tagline: (open)
lockedHint: Defeat the Millstone
waveFolders: [ Assets/Scripts/Waves/Castle/LookingGood ]
strongerEnemiesFromWave: 1
stages: [ { label: Keep, fromWaveNumber: 1, backdrop: <castle sprite> } ]
```

---

## 7. Task — the "Looking Good" test wave

*(The user's message left the quote unclosed — `called "Looking good, that has a pair…`. I
read the name as **"Looking Good"**. Worth confirming.)*

A wave asset in `Assets/Scripts/Waves/Castle/LookingGood/`, registered in the castle map's
setlist and `waveFolders`.

What it does:

- Lays **four pairs of mirrors — eight panes**, one pair of each colour, all stationary.
- **No enemies at all.** Nothing spawns. It exists purely so the mirror mechanic can be
  shot at and watched.
- **One pair is arranged to loop forever**, so a single arrow fired into it keeps going
  round and never leaves. Which geometry produces that depends on the redirect rule — see
  the decision in section 5.

Read `BaseWave` and `Spawner` before writing it — **I did not read either**, so the wave
contract is unverified from my side. The `wave-authoring` skill in `.claude/skills/` covers
the `BaseWave` contract, the `.asset` conventions and the Unity MCP workflow; use it.

Two notes carried over from the design doc:

- Eight panes at once is a lot of furniture — `pallid-keep-mock.png` shows all eight and it
  reads as clutter. That is fine and correct for a test wave, but it is not a template for
  a real one. Two pairs is the normal case.
- The mirror art is 32×64, so a pane is 1×2 world units. Every pane on the map being tall
  means only roughly-horizontal shots can ever be redirected. The design doc recommends
  authoring a 64×32 variant too. For this test wave, tall-only is fine.

---

## 8. Decisions the user still owes you

1. **The redirect rule** — absolute direction preserved, or mirrored about each pane's
   facing? Section 5. This one blocks the build.
2. **Wave name** — "Looking Good"?
3. **What Layer 2 of `background_castle.aseprite` is for** — merged art, or a separate
   foreground layer?
4. **Map name** — the palette and design doc call it "Pallid Keep", picked to sit alongside
   "Gilded Vigil" and "Amethyst Hollow". Never confirmed.
5. **Whether the castle starts unlocked** while it is being built.

## 9. What I did not verify

Stated plainly so nothing here gets trusted further than it earned:

- I never read `BaseWave.cs`, `Spawner.cs`, `BackgroundController.cs`,
  `PlayerProjectile.cs`, or `ProjectileMovement.cs`.
- I never confirmed whether the Unity editor is running.
- I never ran the recolour script in section 4 — it is written against the real contents of
  `mirror_green.aseprite` but has not been executed.
- Enemy readability against the deep blue floor has not been measured. The cave hit exactly
  this problem with its purple mobs (`Docs/Design/Palettes/amethyst-hollow.md` has the ΔE
  table and the options). Worth the same pass before any enemy art is committed.
