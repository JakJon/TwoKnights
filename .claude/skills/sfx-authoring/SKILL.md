---
name: sfx-authoring
description: How to generate, audition, and wire retro sound effects for Two Knights with the local sfxr MCP server — tool inventory, the roll-then-pin workflow, clipping rules, Assets/Sounds conventions, and AudioManager wiring.
---

# Two Knights — SFX Authoring (sfxr MCP)

## The server

Local MCP server at `Tools/sfxr-mcp/server.js` (Node stdio, jsfxr synth engine),
registered as `sfxr` in the project `.mcp.json` — tools appear as `mcp__sfxr__*`.
Output is parameter-synthesized retro audio; jsfxr is UNLICENSE (public domain),
so generated sounds are safe to ship in the commercial game with no attribution.

If the `mcp__sfxr__*` tools are not loaded in the session, drive the server
directly: spawn `node Tools/sfxr-mcp/server.js` with **cwd = project root** and
speak newline-delimited JSON-RPC over stdio (`initialize` →
`notifications/initialized` → `tools/call`). Same idea as the Unity MCP HTTP
fallback, but stdio. A ready client-script pattern lives in past scratchpads;
it's ~60 lines to recreate.

## Tools

- `list_presets` — preset names + every synth parameter with range/meaning.
- `generate_sfx` — roll randomized candidates from a preset (`preset`,
  `out_path`, optional `variations` 1–8, optional `overrides` to pin params).
  **Random on purpose, per call.** Presets: pickupCoin, laserShoot, explosion,
  powerUp, hitHurt, jump, blipSelect, synth, tone, click, random.
- `render_sfx` — deterministic render from exact `params` JSON or a `b58`
  sfxr.me share code. This is the reproduce/tweak/pin path.
- `render_sequence` — concatenate up to 16 synth notes (each a full params
  object, optional `gap_ms` after each) into ONE wav. Use for jingles/melodies:
  sfxr alone can only do two-note arpeggios, which users hear as "boring".
  Note pitch math: Hz scales with `p_base_freq` SQUARED — an octave step is
  ×/÷ sqrt(2) ≈ 1.414 on p_base_freq, a major-triad step is ×sqrt(1.25)/sqrt(1.5).
  Vibrato on a long final note reads as "spring bouncing" — leave it off.
- `play_sfx` — audition a wav through the speakers (blocks until done).

Every result returns the full `params`, a `share_url`
(`https://sfxr.me/#<b58>` — open in browser to tweak by ear), `duration_ms`,
and `peak`. The b58 code does NOT encode `sound_vol` / `sample_rate` /
`sample_size` — keep the params JSON if those matter.

## Workflow: roll → audition → pin

1. **Roll** candidates with `generate_sfx` + `variations` into the session
   scratchpad — never straight into `Assets/`.
2. **Audition** with `play_sfx`. The user picks what ships — sounds are art;
   the same rule as sprites applies: never replace existing game audio
   unprompted, present candidates and wait for the pick.
3. **Pin** the winner: `render_sfx` with its exact `params` to the final
   `Assets/Sounds/<name>.wav`. All later tweaks go through `render_sfx` edits
   of that params JSON (e.g. "cracklier" = open `p_lpf_freq` + add
   `p_repeat_speed`).
4. For families of related sounds (combo tiers, small/large variants), render
   the SAME params with one stepped value (usually `p_base_freq`) — perfectly
   consistent series are the big advantage over hand-rolled one-offs.

## Batch design rules (learned 2026-07-23)

- **Vary the wave engine across a batch.** A set of noise-wave sounds
  differentiated only by envelope/filter reads as "all the same" to the user
  (batch 2 got rejected for exactly this). Give each sound in a batch a
  distinct synthesis identity: saw zing / sine whoo / noise fwoosh / square
  warble / arpeggio zap — before reaching for envelope tweaks.
- **Volume-stepping writes intermediate files.** Unity's file watcher can
  import a mid-write (clipped or partial) render and cache it as an
  empty/broken clip even after the final write lands. After rendering into
  `Assets/Sounds/`, always force-reimport via the bridge:
  `AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate)` — then
  sanity-check `clip.samples > 0` when wiring.
- Project standard: **16-bit / 44100 Hz** (user-confirmed 2026-07-23; 8-bit
  A/B sounded identical to them, and jsfxr only does 8 or 16 — no 24/32).
- **Respect the noise budget: no per-enemy spawn sounds.** Waves spawn packs;
  one sound per spawned enemy stacks into a wall of noise (user removed ALL
  entrance sounds — including the legacy rat one — on 2026-07-23). Prefer
  sounds gated by per-enemy BEHAVIOR moments (aggro flip, attack, hurt, death),
  which stagger naturally. If an arrival ever needs audio, do it once per wave
  or per pack, never per enemy. The orphaned entry clips (bat_screech,
  wolf_howl, slime_spawn) stay in Assets/Sounds + AudioManager for reuse
  (e.g. a single pack-howl at wave start) — ask before deleting.

## Quality rules

- `peak` must land < 0.99 or the render clipped: step `sound_vol` down
  (0.16 is a proven level for noise+resonance sounds; the default 0.25 clips).
  A `warning` key appears if the render is near-silent instead.
- Keep 44100 Hz / 16-bit (the server default). Mono is correct.
- Duration guidance: UI blips ≤ 0.15s, combat one-shots 0.1–0.45s,
  stingers/fanfares ≤ ~1.2s. AudioManager PlayOneShots onto a single
  AudioSource — overlapping long sounds turn to mud.
- Fire/whoosh recipe: `wave_type` 3 (noise) + phaser (`p_pha_offset`) + LPF
  ~0.55; add crackle by opening LPF to ~0.66 and `p_repeat_speed` ~0.55.

## File + Unity conventions

- **All SFX live flat in `Assets/Sounds/`** — no subfolders. New files are
  snake_case (`arrow_ignite.wav`); legacy names are PascalCase, leave them.
- The ENTIRE soundscape is generated audio as of 2026-07-23 — no original
  sounds remain in use, including the six Multi tier sounds. The originals are
  still recoverable from the initial commit (`git checkout HEAD -- <path>`)
  since every legacy replacement was an in-place overwrite.
- **Replacing an existing sound**: overwrite the wav bytes under the SAME
  filename — the `.meta` GUID survives, so every inspector reference keeps
  working with zero rewiring. (User approval first, always.)
- **Adding a new sound**:
  1. wav into `Assets/Sounds/`,
  2. new `SoundEffect` field in `Assets/Scripts/AudioManager.cs`,
  3. `AudioManager.Instance.PlaySFX(AudioManager.Instance.<field>)` at the
     event site,
  4. wire the clip onto the AudioManager object in the scene — Unity MCP
     editor route preferred; offline YAML fallback works because an
     AudioClip's main-asset fileID is constant `8300000`:
     `{fileID: 8300000, guid: <from the .wav.meta>, type: 3}`.
- AudioManager (`Assets/Scripts/AudioManager.cs`) is a DontDestroyOnLoad
  singleton, one AudioSource, `PlayOneShot` only — no loops, no pitch control.
  Extend it (separate looping source) before authoring any ambient/looping
  sound; don't fake loops with repeated one-shots.
- Per-sound volume exists on the `SoundEffect` entry, but prefer authoring the
  wav at a good level (peak ~0.7–0.9) and leaving the field volume at 1.
