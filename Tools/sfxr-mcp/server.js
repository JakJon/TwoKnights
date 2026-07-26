import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { execFile } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import jsfxr from "jsfxr";

const { Params, SoundEffect } = jsfxr;

// Preset methods live on Params.prototype; everything that isn't a
// serialization helper is a sound preset (pickupCoin, laserShoot, ...).
const NON_PRESETS = new Set(["constructor", "toB58", "fromB58", "fromJSON"]);
const PRESETS = Object.getOwnPropertyNames(Params.prototype).filter(
  (n) => !NON_PRESETS.has(n)
);

const PARAM_DOC = {
  wave_type: "0=square 1=sawtooth 2=sine 3=noise",
  p_env_attack: "attack time 0..1",
  p_env_sustain: "sustain time 0..1",
  p_env_punch: "sustain punch (initial volume boost) 0..1",
  p_env_decay: "decay time 0..1",
  p_base_freq: "start frequency 0..1",
  p_freq_limit: "min frequency cutoff 0..1",
  p_freq_ramp: "frequency slide -1..1 (negative = downward)",
  p_freq_dramp: "delta slide -1..1",
  p_vib_strength: "vibrato depth 0..1",
  p_vib_speed: "vibrato speed 0..1",
  p_arp_mod: "arpeggio pitch change -1..1",
  p_arp_speed: "arpeggio speed 0..1",
  p_duty: "square duty 0..1 (square wave only)",
  p_duty_ramp: "duty sweep -1..1",
  p_repeat_speed: "retrigger rate 0..1",
  p_pha_offset: "phaser/flanger offset -1..1 (whoosh)",
  p_pha_ramp: "phaser sweep -1..1",
  p_lpf_freq: "low-pass cutoff 0..1 (1 = filter off)",
  p_lpf_ramp: "low-pass sweep -1..1",
  p_lpf_resonance: "low-pass resonance 0..1",
  p_hpf_freq: "high-pass cutoff 0..1",
  p_hpf_ramp: "high-pass sweep -1..1",
  sound_vol: "output volume 0..1 (default 0.25)",
  sample_rate: "44100, 22050, 11025 or 5512 (default 44100)",
  sample_size: "bits per sample, 8 or 16 (default 16)",
};

function resolveOutPath(outPath) {
  const abs = path.resolve(process.cwd(), outPath);
  if (path.extname(abs).toLowerCase() !== ".wav") {
    throw new Error(`out_path must end in .wav, got: ${outPath}`);
  }
  fs.mkdirSync(path.dirname(abs), { recursive: true });
  return abs;
}

function applyOverrides(params, overrides) {
  for (const [key, value] of Object.entries(overrides || {})) {
    if (!(key in PARAM_DOC)) {
      throw new Error(
        `Unknown parameter "${key}". Valid: ${Object.keys(PARAM_DOC).join(", ")}`
      );
    }
    params[key] = value;
  }
  return params;
}

function analyzeWav(buf) {
  const sampleRate = buf.readUInt32LE(24);
  const bits = buf.readUInt16LE(34);
  const channels = buf.readUInt16LE(22);
  const dataSize = Math.min(buf.readUInt32LE(40), buf.length - 44);
  const frames = dataSize / (bits / 8) / channels;
  let peak = 0;
  if (bits === 16) {
    for (let i = 44; i + 1 < 44 + dataSize; i += 2) {
      const v = Math.abs(buf.readInt16LE(i)) / 32768;
      if (v > peak) peak = v;
    }
  } else {
    for (let i = 44; i < 44 + dataSize; i++) {
      const v = Math.abs(buf[i] - 128) / 128;
      if (v > peak) peak = v;
    }
  }
  return {
    duration_ms: Math.round((frames / sampleRate) * 1000),
    sample_rate: sampleRate,
    bits,
    peak: Math.round(peak * 1000) / 1000,
  };
}

function renderToFile(params, absPath) {
  const wave = new SoundEffect(params).generate();
  const b64 = wave.dataURI.split(",")[1];
  const buf = Buffer.from(b64, "base64");
  fs.writeFileSync(absPath, buf);
  const stats = analyzeWav(buf);
  const synthdef = {};
  for (const key of Object.keys(PARAM_DOC)) {
    if (params[key] !== undefined) synthdef[key] = params[key];
  }
  const result = {
    file: absPath,
    ...stats,
    share_url: "https://sfxr.me/#" + params.toB58(),
    params: synthdef,
  };
  if (stats.peak < 0.05) {
    result.warning = "output is near-silent (peak < 0.05); check envelope/volume";
  }
  return result;
}

function newParams(args) {
  const p = new Params();
  p.sound_vol = args?.sound_vol ?? 0.25;
  p.sample_rate = args?.sample_rate ?? 44100;
  p.sample_size = args?.sample_size ?? 16;
  return p;
}

function jsonContent(obj) {
  return { content: [{ type: "text", text: JSON.stringify(obj, null, 2) }] };
}

const server = new McpServer({ name: "sfxr", version: "1.0.0" });

server.registerTool(
  "list_presets",
  {
    title: "List sfxr presets and parameters",
    description:
      "List available sfxr sound presets and every tweakable synth parameter with its range and meaning.",
    inputSchema: {},
  },
  async () => jsonContent({ presets: PRESETS, parameters: PARAM_DOC })
);

server.registerTool(
  "generate_sfx",
  {
    title: "Generate sound effect from preset",
    description:
      "Generate retro (sfxr) sound effects from a preset and write WAV file(s). " +
      "Presets are intentionally randomized each call - use `variations` to roll several candidates, " +
      "then reproduce/refine the best one exactly with render_sfx using the returned params. " +
      "Optional `overrides` pin specific synth parameters after the preset rolls.",
    inputSchema: {
      preset: z.enum(PRESETS).describe("Sound preset to roll from"),
      out_path: z
        .string()
        .describe(
          "Output .wav path, absolute or relative to the project root. With variations > 1, files get -1, -2... suffixes"
        ),
      variations: z
        .number()
        .int()
        .min(1)
        .max(8)
        .optional()
        .describe("How many randomized candidates to generate (default 1)"),
      overrides: z
        .record(z.number())
        .optional()
        .describe("Synth parameters to pin after the preset rolls (see list_presets)"),
      sound_vol: z.number().min(0).max(1).optional(),
      sample_rate: z.number().optional(),
      sample_size: z.number().optional(),
    },
  },
  async (args) => {
    const abs = resolveOutPath(args.out_path);
    const count = args.variations ?? 1;
    const results = [];
    for (let i = 0; i < count; i++) {
      const file =
        count === 1
          ? abs
          : abs.replace(/\.wav$/i, `-${i + 1}.wav`);
      const p = applyOverrides(newParams(args)[args.preset](), args.overrides);
      results.push(renderToFile(p, file));
    }
    return jsonContent(count === 1 ? results[0] : results);
  }
);

server.registerTool(
  "render_sfx",
  {
    title: "Render sound effect from exact parameters",
    description:
      "Deterministically render a WAV from an exact synth definition: either a `params` object " +
      "(as returned by generate_sfx / list_presets keys) or a `b58` share code from an sfxr.me URL. " +
      "Use this to reproduce, tweak, or hand-design sounds.",
    inputSchema: {
      out_path: z
        .string()
        .describe("Output .wav path, absolute or relative to the project root"),
      params: z
        .record(z.number())
        .optional()
        .describe("Synth parameters (see list_presets); unspecified ones use defaults"),
      b58: z
        .string()
        .optional()
        .describe("Base58 synth definition from an sfxr.me share URL (the part after #)"),
    },
  },
  async (args) => {
    if (!args.params && !args.b58) {
      throw new Error("Provide either `params` or `b58`");
    }
    const abs = resolveOutPath(args.out_path);
    const p = newParams(args.params);
    if (args.b58) p.fromB58(args.b58.replace(/^#/, ""));
    if (args.params) applyOverrides(p, args.params);
    return jsonContent(renderToFile(p, abs));
  }
);

server.registerTool(
  "render_sequence",
  {
    title: "Render a multi-note sequence into one WAV",
    description:
      "Concatenate several synth renders (notes) back-to-back into a single WAV — " +
      "for jingles and melodies that need more than sfxr's two-note arpeggio. " +
      "Each note is a full params object (see list_presets); optional gap_ms adds " +
      "silence after that note. All notes render at 44100 Hz / 16-bit mono.",
    inputSchema: {
      out_path: z
        .string()
        .describe("Output .wav path, absolute or relative to the project root"),
      notes: z
        .array(
          z.object({
            params: z.record(z.number()).describe("Synth parameters for this note"),
            gap_ms: z.number().min(0).max(2000).optional().describe("Silence after this note"),
          })
        )
        .min(1)
        .max(16),
    },
  },
  async (args) => {
    const abs = resolveOutPath(args.out_path);
    const chunks = [];
    for (const note of args.notes) {
      const p = newParams(note.params);
      p.sample_rate = 44100;
      p.sample_size = 16;
      applyOverrides(p, note.params);
      const wave = new SoundEffect(p).generate();
      chunks.push(Buffer.from(wave.dataURI.split(",")[1], "base64").subarray(44));
      if (note.gap_ms) {
        chunks.push(Buffer.alloc(Math.round((44100 * note.gap_ms) / 1000) * 2));
      }
    }
    const data = Buffer.concat(chunks);
    const header = Buffer.alloc(44);
    header.write("RIFF", 0);
    header.writeUInt32LE(36 + data.length, 4);
    header.write("WAVE", 8);
    header.write("fmt ", 12);
    header.writeUInt32LE(16, 16);
    header.writeUInt16LE(1, 20); // PCM
    header.writeUInt16LE(1, 22); // mono
    header.writeUInt32LE(44100, 24);
    header.writeUInt32LE(44100 * 2, 28);
    header.writeUInt16LE(2, 32);
    header.writeUInt16LE(16, 34);
    header.write("data", 36);
    header.writeUInt32LE(data.length, 40);
    const buf = Buffer.concat([header, data]);
    fs.writeFileSync(abs, buf);
    const stats = analyzeWav(buf);
    const result = { file: abs, ...stats, note_count: args.notes.length };
    if (stats.peak < 0.05) {
      result.warning = "output is near-silent (peak < 0.05)";
    }
    return jsonContent(result);
  }
);

server.registerTool(
  "play_sfx",
  {
    title: "Play a WAV file",
    description:
      "Audition a .wav file through the system speakers (Windows). Blocks until playback finishes.",
    inputSchema: {
      file: z.string().describe("Path to the .wav file, absolute or relative to the project root"),
    },
  },
  async (args) => {
    const abs = path.resolve(process.cwd(), args.file);
    if (!fs.existsSync(abs)) throw new Error(`File not found: ${abs}`);
    await new Promise((resolve, reject) => {
      execFile(
        "powershell.exe",
        [
          "-NoProfile",
          "-NonInteractive",
          "-Command",
          `(New-Object Media.SoundPlayer '${abs.replace(/'/g, "''")}').PlaySync()`,
        ],
        (err) => (err ? reject(err) : resolve())
      );
    });
    return jsonContent({ played: abs });
  }
);

const transport = new StdioServerTransport();
await server.connect(transport);
