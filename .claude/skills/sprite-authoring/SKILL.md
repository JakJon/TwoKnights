---
name: sprite-authoring
description: How to create and edit pixel-art sprites for Two Knights with the Aseprite MCP (pixel-mcp) — tool inventory, stdio fallback gotchas, the procedural draw_pixels workflow, PIL preview loop, and Unity import/wiring conventions.
---

# Two Knights — Sprite Authoring via Aseprite MCP

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
- `draw_circle/rectangle/line`, `fill_area`, `draw_with_dither` (16 patterns),
  `apply_shading/outline/antialiasing`, palette tools, `add_frame`/tags for
  animation, `export_sprite`, `export_spritesheet`, `import_image`,
  `get_pixels` (read-back for verification), `get_sprite_info`.

Reliability facts:
- **Prefer ONE `draw_pixels` call over many shape calls** — rapid sequential
  shape calls sporadically fail with `exit status 0xffffffff` (Steam Aseprite
  spawn flakiness); a single big pixels array is atomic and reliable.
- **Export in a SEPARATE server invocation from the draw** — exporting in the
  same batch races the sprite save and yields an empty PNG (draw reports
  success; `get_pixels` shows the data; the PNG is blank). Draw → let the
  process exit → new invocation → `export_sprite`.
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

1. Export PNG → copy into `Assets/Graphics/`.
2. Import settings via Unity MCP `execute_code` (TextureImporter):
   `textureType=Sprite, spriteImportMode=Single, filterMode=Point,
   textureCompression=Uncompressed, mipmapEnabled=false, SaveAndReimport()`.
   (House rule: all pixel art is point-filtered + uncompressed; PPU stays 100
   for resource sprites — particle startSize controls world scale.)
3. Wire the sprite where it's consumed (e.g. `PoisonResourceManager.poisonPuffSprite`
   via SerializedObject on `Assets/Resources/PoisonResourceManager.asset`), keep
   a fallback path in code.
4. Verify in play mode: spawn the consumer, check
   `renderer.material.mainTexture.name` matches.

## Style context for new sprites

Pixel-art game, PixelPerfectCamera at 32 PPU (visible field 20×11.25 units).
Mob sprites are roughly 16–32 px. Existing art sources are `.aseprite` files in
`Assets/Graphics/`. Palette anchors: field greens (dark `#223822`-ish ground),
poison/Serpent `#7AD64F`, Shadow violet `#9488FF`, ember `#FF7A29`, UI gold
`#F4AA36` (see the Gilded Vigil tokens in UpgradeMenu.uss). Expectation setting:
procedural/geometric sprites (icons, particles, auras) come out well; character
art via LLM is placeholder-grade — rough in mobs to unblock design, flag them
for hand-repainting.

## Known good first-run sequence (stdio)

```bash
# 1) create canvas, capture file_path from response
# 2) python builds batch.jsonl (LF!) with ONE draw_pixels + no export
# 3) { cat batch.jsonl; sleep 15; } | pixel-mcp.exe
# 4) NEW invocation: export_sprite -> scratchpad/foo.png
# 5) PIL preview -> Read -> iterate
# 6) copy to Assets/Graphics + Unity import via MCP
```
