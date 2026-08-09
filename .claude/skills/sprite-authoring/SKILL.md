---
name: sprite-authoring
description: How to create and edit pixel-art sprites for Two Knights with the Aseprite MCP (pixel-mcp) — tool inventory, stdio fallback gotchas, the procedural draw_pixels workflow, PIL preview loop, and Unity import/wiring conventions.
---

# Two Knights — Sprite Authoring via Aseprite MCP

## The one hard rule: sprites are `.aseprite`, never `.png`

**Never author a PNG sprite into `Assets/`.** Every sprite the game ships — mob art,
icons, particles, UI plates — is a `.aseprite` file. PNG is an export format for
previews and diffing, and nothing else. There are no PNG sprites left in the project
and none should come back.

Why: a `.aseprite` keeps layers, frames and tags, so the art stays editable and the
importer can regenerate clips from it. A PNG is a flattened dead end — the moment one
needs a second frame or a palette swap it has to be redrawn.

The move is `save_as` straight into `Assets/`, not export-then-copy:

```
create_canvas -> draw_pixels -> save_as "C:/Projects/Two Knights/Assets/Graphics/<name>.aseprite"
```

Export PNGs only into the scratchpad, for the PIL preview loop below.

## Setup (already done — verify, don't redo)

- Server: **pixel-mcp v0.5.0** at `C:/Users/grobb/Tools/pixel-mcp/pixel-mcp.exe`
  (willibrandon/pixel-mcp, checksum-verified release).
- Config: `C:/Users/grobb/.config/pixel-mcp/config.json` → Steam Aseprite
  (`C:/Program Files (x86)/Steam/steamapps/common/Aseprite/Aseprite.exe`, 1.3.17.2),
  temp dir `C:/Users/grobb/Tools/pixel-mcp/temp`. **Forward slashes only** in this
  JSON — backslash escapes have already corrupted it once.
- Registered project-local as `aseprite` (`claude mcp list` to check). Health check:
  `"C:/Users/grobb/Tools/pixel-mcp/pixel-mcp.exe" --health`.
- Aseprite does NOT need to be running — every tool call launches it headless
  (batch mode) and exits. ~1–2s per call.

## Two ways to drive it

1. **Native tools** (`mcp__aseprite__*`, 50 tools): available when the session
   started after registration. Prefer these.
2. **Stdio fallback** (server registered mid-session, or native tools missing):
   pipe newline-delimited JSON-RPC into the exe. Pattern that works:

```bash
{ cat batch.jsonl; sleep <N>; } | "C:/Users/grobb/Tools/pixel-mcp/pixel-mcp.exe" 2>/dev/null | <python line parser>
```

   Stdio gotchas (each cost real debugging time):
   - **LF only.** A single CRLF kills the whole server silently (python
     `open("w")` on Windows writes CRLF — write bytes: `open(f,"wb").write(...)`).
   - **Forward slashes in JSON paths.** Backslashes become invalid `\U`-style
     escapes somewhere between bash and the Go JSON parser.
   - The `sleep N` holds stdin open; the server dies on EOF mid-batch. Budget
     ~2s per tool call (each spawns Aseprite).
   - Batch of messages: `initialize` → `notifications/initialized` → `tools/call`s.
     Responses are one JSON per line; parse with python, not grep.
   - `export_sprite` REQUIRES `frame_number` (invalid-params error without it).
   - TWO `create_canvas` calls in one batch can collide on the same
     timestamp-named temp file — one canvas per server invocation.
   - Recolor-to-tier-palette recipe (produced bat_dark from bat_basic): dump both
     ramps' colors via Lua, rank-match by luminance, exact-color map per cel,
     `spr:saveAs` directly into Assets/Graphics — frames/layers/tags survive, and
     the Aseprite importer regenerates AnimatorController+clips with the SAME
     names, so a prefab clone just remaps controller + clip refs by name.
     (Remember: NEW .aseprite imports default to PPU 100 — set 32 via importer
     property `m_TextureImporterSettings.m_SpritePixelsToUnits`.)

## State model & key tools

State lives in `.aseprite` files (temp dir) — separate server invocations can
keep working on the same sprite via its path.

- `create_canvas {width, height, color_mode:"rgb"}` → returns `file_path`.
  New canvases have one layer named **"Layer 1"**.
- `draw_pixels {sprite_path, layer_name, frame_number, pixels:[{x,y,color}]}` —
  color is `#RRGGBB` or `#RRGGBBAA`. **The workhorse — see workflow below.**
- `save_as {sprite_path, output_path}` → writes a `.aseprite` anywhere, including
  straight into `Assets/`. **This is how art reaches the project** — there is no
  export-PNG-and-copy step.
- `draw_circle/rectangle/line`, `fill_area`, `draw_with_dither` (16 patterns),
  `apply_shading/outline/antialiasing`, palette tools, `add_frame`/tags for
  animation, `export_sprite`, `export_spritesheet`, `import_image`,
  `get_pixels` (read-back for verification), `get_sprite_info`.

Reliability facts:
- **Prefer ONE `draw_pixels` call over many shape calls** — rapid sequential
  shape calls sporadically fail with `exit status 0xffffffff` (Steam Aseprite
  spawn flakiness); a single big pixels array is atomic and reliable.
- **`save_as` / export go in a SEPARATE server invocation from the draw** —
  saving in the same batch races the sprite write and yields an empty file (draw
  reports success; `get_pixels` shows the data; the output is blank). Draw → let
  the process exit → new invocation → `save_as`, then verify by exporting from
  the *saved project file*, not the temp one.
- **A long-lived stdio session beats one-shot batches.** Keeping stdin open lets
  `create_canvas` → `draw_pixels` round-trip in one process (the tool returns the
  temp path you need for the next call). See the client sketch at the end.
- **NEVER bulk-edit existing multi-layer/multi-frame sprites via draw_pixels**
  (v0.5.0). Two confirmed defects: (1) multiple `draw_pixels` calls to the same
  file lose earlier draws (server re-saves from a stale cache; even one server
  session per call doesn't fix it), and (2) on cels whose origin isn't (0,0),
  `get_pixels` coordinates and `draw_pixels` coordinates disagree — drawn pixels
  land offset, silently corrupting the sprite. For bulk pixel edits use headless
  Lua instead: `Aseprite.exe -b --script foo.lua` with `app.open(path)`, iterate
  `spr.cels`, `cel.image:pixels()` iterator, compare/assign `it()` rgba values,
  `spr:saveAs(spr.filename)`. Atomic per file, preserves layers/frames/cels
  exactly (used for the 2026-07 palette unification; see
  `Assets/Graphics/palletes/`). MCP `export_sprite`/`get_sprite_info`/
  `create_canvas` remain reliable.
- **`get_pixels` response format**: JSON with `"color"` BEFORE `"x","y"` in each
  pixel object (regexes assuming x,y,color order match nothing), colors as
  `#RRGGBBAA` uppercase, and pagination via `next_cursor` — the server caps
  pages at a few hundred pixels regardless of `page_size`; follow the cursor.

## The art workflow that produced good results

For organic shapes (clouds, glows, icons), compute the sprite procedurally in
python and ship it as one `draw_pixels` call:

1. **Distance field**: union of lobes `d = min(dist(p, lobe_i)/r_i)`; add shape
   bias terms (e.g. `d += (y-22)*0.18` to flatten a cloud's underside).
2. **Posterize alpha** into 3–5 steps (e.g. d≤0.45→242, ≤0.68→199, ≤0.88→133,
   ≤1.0→66) — smooth falloff reads as soft pixel art, not airbrush.
3. **Ragged rim**: deterministic jitter `(((x*7+y*13)%5)-2)*0.03` added to d.
4. **Shading**: darker value (not alpha) on the underside for volume.
5. **White + alpha for anything code-tinted** (particle sprites): PoisonCloud
   etc. tint via `ParticleSystem.main.startColor`, so a white sprite serves
   every color. Check the consumer before picking colors.

**Preview loop (mandatory — don't ship unseen art):** a 32px white-on-white PNG
is invisible in the Read tool. Composite with PIL (pillow is installed):
tint to the in-game color, alpha-composite onto a dark field-green background,
`resize(×10, Image.NEAREST)`, save, then **Read the preview and actually judge
it**. Iterate — v1 of the poison puff looked like a frog; v3 shipped.

## Unity handoff

1. `save_as` the sprite directly to `Assets/Graphics/...aseprite`, then
   `refresh_unity` so the AsepriteImporter picks it up. **No PNG step.**
2. Import settings via Unity MCP `execute_code`. The AsepriteImporter's properties
   are NOT the TextureImporter's — they hang off
   `m_TextureImporterSettings.*` and `m_AsepriteImporterSettings.*` on the importer's
   SerializedObject. What matters:
   - `m_TextureImporterSettings.m_FilterMode = 0` (Point) and platform
     `m_TextureCompression = 0` — house rule for all pixel art.
   - `m_TextureImporterSettings.m_SpritePixelsToUnits` — **new .aseprite imports
     default to PPU 100.** World/mob art wants 32; particle sprites stay 100
     (startSize controls their world scale). Match whatever the sprite replaces.
   - `m_AsepriteImporterSettings.m_DefaultPivotAlignment` — defaults to
     **BottomCenter** (right for mobs standing on their feet, wrong for anything
     that was a centred PNG). Set `Center` + `m_DefaultPivotSpace = Canvas` for
     particles, projectiles and icons.
   - `m_GenerateModelPrefab` / `m_GenerateAnimationClips` → false for a static
     single-frame sprite, or the asset carries a pointless GameObject + clips.
3. Wire the sprite where it's consumed (e.g. `PoisonResourceManager.poisonPuffSprite`
   via SerializedObject) — **you cannot hand-author the reference in YAML.** A PNG's
   sprite is always fileID `21300000`; an .aseprite's is a generated per-asset id, so
   the ref must be set through `AssetDatabase.LoadAssetAtPath<Sprite>`.
4. Verify by loading the sprite back in the editor and asserting size, PPU, pivot and
   that `AssetDatabase.GetAssetPath` ends in `.aseprite`.

### The trim trap — read this before shipping any icon

**Unity's AsepriteImporter tight-crops every Sprite to its non-transparent bounds.**
A PNG imported as Single keeps its full rect; an .aseprite does not. Twenty-two 32×32
icons imported as 22 different sizes (16×32, 22×23, 31×21 …), so a fixed-size UI slot
scaled each by a different non-integer factor — which destroys pixel art.

Neither the canvas size nor the cel bounds prevent it, and there is no trim toggle on
the importer. Two things are needed:

1. **Alpha-guard the corners.** Put a pixel of alpha `1` in each corner of the canvas
   (only where the art doesn't already reach). At 1/255 it is invisible once
   composited, but it is non-zero, so the crop keeps the full bounds:
   ```lua
   local guard = app.pixelColor.rgba(0, 0, 0, 1)
   if app.pixelColor.rgbaA(img:getPixel(x, y)) == 0 then img:drawPixel(x, y, guard) end
   ```
2. **Clear the importer's cached rect.** The rect is persisted in the `.meta` under
   `m_AnimatedSpriteImportData`, and it goes *stale* — 6 of 22 files kept importing at
   their old trimmed size even after the pixels changed and a forced synchronous
   reimport. `ClearArray()` that property and `SaveAndReimport()`. Do **not** delete
   the `.meta` to force it: that mints a new guid and breaks every reference you just
   wired.

Always assert the imported `sprite.rect` is the size you drew. The draw succeeding
tells you nothing about what Unity produced.

## Animated enemy art: tags are the contract

Anything animated lives as a multi-frame `.aseprite` in `Assets/Graphics/`, and
the **Aseprite tag names are load-bearing** — the importer generates one
`AnimationClip` and one Animator state per tag, named after the tag. Gameplay
code plays them by that name, so a tag rename silently breaks the animation.

House tag vocabulary (match an existing file rather than inventing a name):

| Tag | Used by | Meaning |
|---|---|---|
| `Walking` | rats, wolves, Rat King | ground idle/locomotion |
| `Flying` | bats | airborne locomotion |
| `Rolling` | mine carts, gnome carts | on-rails locomotion |
| `Damage` | rats, bats, Rat King, gnome carts | the hurt/flinch frames |
| `Hurt` | wolves only | same thing as `Damage`, older name — don't copy it into new art |

**An enemy can only flinch if its art has a hurt tag.** Adding one is a real art
task, not wiring: draw the flinch frame(s), tag them `Damage`, save. Until that
tag exists the enemy still flashes red and still staggers, it just has no pose
to change to. `mine_cart`, `mine_cart_tnt` and `mine_cart_cage` are `Rolling`-only
on purpose — a cart has no face to wince with.

To check what tags a file actually has **without opening Aseprite or Unity**,
read the ASCII strings out of the `.aseprite` binary; tag names sit in plain text
near the head of the file:

```powershell
$b=[System.IO.File]::ReadAllBytes("Assets\Graphics\gnome_mine_cart.aseprite")
[regex]::Matches([System.Text.Encoding]::ASCII.GetString($b),'[A-Za-z][\x20-\x7E]{2,20}') |
  ForEach-Object { $_.Value } | Select-Object -Unique
```

The `.meta` will NOT tell you this — it lists frames (`Frame_0`…) and layers, but
never clip or tag names. It is also the file to check when a freshly-saved
`.aseprite` looks wrong in game: a new import lands at **PPU 100**, and
`spritePixelsToUnits: 32` has to be set before anything built from it is correct.

Once the tag exists, wiring it onto the prefab is the enemy-prefab-authoring
skill's job — see "Wiring stagger clips WITHOUT Unity open" there, and note that
the red hit-flash needs `WaveGlowMaterial` on the renderer or it does nothing.

## Style context for new sprites

Pixel-art game, PixelPerfectCamera at 32 PPU (visible field 20×11.25 units).
Mob sprites are roughly 16–32 px. Existing art sources are `.aseprite` files in
`Assets/Graphics/`. Palette anchors: field greens (dark `#223822`-ish ground),
poison/Serpent `#7AD64F`, Shadow violet `#9488FF`, ember `#FF7A29`, UI gold
`#F4AA36` (see the Gilded Vigil tokens in UpgradeMenu.uss). Expectation setting:
procedural/geometric sprites (icons, particles, auras) come out well; character
art via LLM is placeholder-grade — rough in mobs to unblock design, flag them
for hand-repainting.

## Known good sequence (stdio)

```
per sprite:
  session A: create_canvas -> draw_pixels          (one canvas per session)
  session B: save_as -> Assets/Graphics/<name>.aseprite
  session C: export_sprite from the SAVED file -> scratchpad/<name>.png
then:
  PIL contact sheet -> Read it -> judge -> iterate
  alpha-guard corners (Lua) -> refresh_unity -> clear m_AnimatedSpriteImportData
  set importer props -> wire refs via AssetDatabase -> assert sprite.rect
```

Instead of the `{ cat batch.jsonl; sleep N; }` trick, hold stdin open from python so
calls can round-trip (the sleep is a guess; this isn't):

```python
p = subprocess.Popen([EXE], stdin=PIPE, stdout=PIPE, bufsize=0)   # LF only!
# initialize -> notifications/initialized -> tools/call ... -> p.stdin.close()
# read stdout on a thread; match responses by request id
```

A working copy of that client, the 32×32 composition helpers, and the icon
definitions all live in this session's scratchpad pattern — `pixlib.py`
(`poly`/`disc`/`bevel`/`outline`/`contact_sheet`), `mcpclient.py`, `icons_all.py`.
Rebuild them the same way rather than reaching for one-off shell pipelines.
