---
name: music-video
description: Generate a 60-second 9:16 vertical music video (YouTube Shorts / TikTok / Reels format) from an operator-supplied music file plus mood keywords. Use when the user provides an audio file (mp3 / wav / m4a / aac) and wants a short-form vertical video with beat-aligned cuts, mood-matched Pexels B-roll, and optional vintage post-shaders (pond ripple / breathing zoom / halation / combo). Music is the primary audio — no narration, no captions.
license: MIT
compatibility: Requires ffmpeg (libass-enabled), aubio CLI tools (aubiotrack + aubioonset), ollama running locally, jq, and either a Pexels API key (`PEXELS_API_KEY` in `.env`) or future demo-mode CC-BY B-roll cache.  macOS or Linux.  Tested via `agents/missions/music-video/run.sh` since 2026-05-17.
metadata:
  authors: MelonS-Agents
  version: "1.0.0"
  pipeline-source: agents/missions/music-video/run.sh
  spec: agentskills.io
  added: "2026-05-19"
allowed-tools: Bash(bash:*) Bash(ffmpeg:*) Bash(ffprobe:*) Bash(ollama:*) Bash(aubiotrack:*) Bash(aubioonset:*) Bash(curl:*) Bash(jq:*) Read Write
---

# music-video

Generate a 60-second 9:16 vertical music video from a music file +
mood keywords.  Designed for YouTube Shorts / TikTok / Reels upload.

## What this produces

Given:

- A music file (mp3 / wav / m4a / aac) — typically 60–240 seconds of
  operator-supplied music (Suno-generated, YouTube Audio Library,
  Pixabay, etc.).  Music itself is the *only* audio track in the
  output.
- A short list of mood keywords (3–6 comma-separated phrases like
  `"rainy street, jazz cafe, vinyl, wet pavement"`).

Produces:

- A **1080 × 1920 (9:16 vertical)** mp4, exactly 60 seconds.
- 8 B-roll clips fetched from Pexels (per-keyword), trimmed and
  ordered to match phrase boundaries detected via `aubiotrack`.
- Drum-onset-aligned glitch micro-edits (`aubioonset`) on static-
  camera clips only.
- Vintage lo-fi processing (film grain, vignetting, zoom-pulse) per
  v6 defaults; tunable via env vars.
- Optional post-shader pass: `pond` (water-surface ripple),
  `breathing` (5-s scale wave), `halation` (warm bloom), or
  `combo` (phrase-aware pond + halation envelope).

## How to invoke

User-facing invocation: `/music-video <music_file_path> "<comma_separated_keywords>"`

Examples:

```text
/music-video "assets/music/Rainy Bossa.mp3" "rainy street, jazz cafe, vinyl, wet pavement"
/music-video ~/Downloads/track.wav "tokyo neon, vibraphone, late night, shibuya"
```

If the user invokes without a path or without keywords, ask them
for the missing input rather than guessing.

## Step-by-step (what the agent does)

When this skill activates:

1. **Verify the music file exists** at the path the operator provided.
   If not, ask for the correct path.  Do not attempt to fetch music
   from the network — the operator supplies it.
2. **Verify environment readiness.**  Run
   `./scripts/bootstrap.sh --check-only` (or inspect the resulting
   warnings).  In particular check that `PEXELS_API_KEY` is set in
   `.env`.  If missing, point the operator at the Pexels signup link
   in the README and stop — do *not* proceed without B-roll source.
3. **Generate a mission id** in the form
   `skill-music-video-<HHMMSS>`.
4. **Run the bundled pipeline** (script symlinked from the mature
   `agents/missions/music-video/run.sh` so this skill inherits all
   v5 + v6 tuning):

   ```bash
   bash scripts/run.sh "<mission_id>" "<music_file>" "<keywords>"
   ```

   The pipeline writes to `records/missions/<date>/music-video-<mission_id>-*/outputs/short.mp4`
   (see `agents/missions/music-video/run.sh` for the full stage
   breakdown — 8 stages including beat detection, per-window
   keyword resolution, Pexels fetch with caching, per-clip
   trim+crop+speed-class, concat, music overlay, lo-fi shading).

   Typical runtime: 3–6 minutes for a 60-second output (depends on
   Pexels response latency and post-shader presence).

5. **(Optional) Apply post-shader** for the validated music-video
   look.  Default recommendation: `combo` (phrase-aware pond +
   halation envelope, tuned for a 95.8 BPM cadence by default —
   override `GATE_POND` and `OPACITY` env vars for other tempos):

   ```bash
   bash "$REPO_ROOT/scripts/music-video-shaders.sh" combo \
     "<short_mp4_input>" \
     "<output_path>"
   ```

6. **Report the final mp4 path to the operator** along with file size
   and duration (use `ffprobe` to confirm 60.0 ± 0.5 s and
   1080×1920).  Suggest the operator move it to `outputs/publish/`
   if they intend to upload (per operator-contract §8 outputs-publish
   exception).

## What this skill does NOT do

- Generate the music itself (operator provides via Suno / YouTube
  Audio Library / Pixabay / etc.).
- Upload to YouTube / TikTok / Reels (operator uploads manually
  through the platform's UI — public URLs are intentionally not
  committed to this repo per the 2026-05-18 threat-model decision;
  see `docs/pilots/first-upload-metrics.md` "Public URL policy").
- Generate captions or burned-in text (music-video format is
  intentionally text-free; for narrated shorts use the
  `faceless-short` mission instead).

## Required environment

Pulled from `.env` (rendered locally; not committed):

- `FFMPEG_BIN` — set automatically by `agents/lib/env.sh` libass
  discovery; only override if you need a specific build.
- `FFPROBE_BIN` — same discovery.
- `OLLAMA_HOST` — default `127.0.0.1:11434`.
- `OLLAMA_MODEL_HIGHLIGHT` — the local model used for keyword
  expansion (default `llama3.2:3b`).
- `PEXELS_API_KEY` — free signup at <https://www.pexels.com/api/>.
- `RECORDS_DIR` — defaults to `./records/`.

Optional tuning env vars for the lo-fi processing layer (read by
the pipeline at run time):

- `MUSIC_VIDEO_FILM_GRAIN_INTENSITY`
- `MUSIC_VIDEO_VIGNETTE_ANGLE`
- `MUSIC_VIDEO_ZOOM_PULSE_AMP`

See `agents/missions/music-video/run.sh` for the full env-var list
and defaults.

## See also

- Source pipeline (this skill wraps it):
  `agents/missions/music-video/run.sh`
- Post-shaders: `scripts/music-video-shaders.sh`
- Engineering case study (why these shader choices, where the wall
  is): `docs/engineering-case-studies.md` §5
- Decision log (niche pivot to music-video format, 2026-05-17):
  `docs/pilots/decision-log.md`
- Daily upload queue (cron / launchd cadence):
  `scripts/daily-music-video.sh`
