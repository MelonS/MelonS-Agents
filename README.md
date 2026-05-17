<div align="center">

# MelonS-Agents

[한국어](./README.ko.md) | **English** · [**Live site →**](https://melons.github.io/MelonS-Agents/)

**Topic prompt → 60-second 9:16 vertical short.**

**Local for the mechanical, Claude for the creative.**  Three trigger layers — commit, anomaly, schedule — so the system corrects its own drift.  English + Korean dual track from day 1.

`32 missions · 0 runtime API tokens · 3 audit layers · v6 scorecard 44 / 50 · MIT`

![AI-Powered](https://img.shields.io/badge/AI--Powered-FF6B35?style=for-the-badge&logo=anthropic&logoColor=white)
![Self-Evolving](https://img.shields.io/badge/Self--Evolving-8B5CF6?style=for-the-badge&logo=git&logoColor=white)
![Autonomous](https://img.shields.io/badge/Autonomous-10B981?style=for-the-badge&logo=robotframework&logoColor=white)

![macOS](https://img.shields.io/badge/macOS-000000?style=for-the-badge&logo=apple&logoColor=white)
![Shell](https://img.shields.io/badge/Shell-4EAA25?style=for-the-badge&logo=gnu-bash&logoColor=white)
![FFmpeg](https://img.shields.io/badge/FFmpeg-007808?style=for-the-badge&logo=ffmpeg&logoColor=white)
![Ollama](https://img.shields.io/badge/Ollama-000000?style=for-the-badge&logo=ollama&logoColor=white)
![Claude](https://img.shields.io/badge/Claude-D97757?style=for-the-badge&logo=anthropic&logoColor=white)

![GitHub stars](https://img.shields.io/github/stars/MelonS/MelonS-Agents?style=for-the-badge)
![GitHub forks](https://img.shields.io/github/forks/MelonS/MelonS-Agents?style=for-the-badge)
![GitHub last commit](https://img.shields.io/github/last-commit/MelonS/MelonS-Agents?style=for-the-badge)
![License](https://img.shields.io/github/license/MelonS/MelonS-Agents?style=for-the-badge)

![5-second animated preview of the faceless-short v6 pipeline output — Hittites topic, 9:16 vertical short, screen-fill B-roll of historical battle reenactment, English caption "Scholars called the Hittites fiction" burned in, Pexels-licensed attribution top-left](docs/demo/v6-hittites-preview.gif)

</div>

## Overview

> A macOS-based multi-agent system.  **The current focus** — and what
> you see in the demo above — is faceless short-form video generation.
> **The system itself is not shorts-specific.**  The scaffold —
> orchestrator + 4 mission subagents + file-based handoff + 3-layer
> reactive audit + Tier-1/Tier-2 cost routing — is general-purpose by
> design; short-form video is the v1 mission type chosen to exercise
> the architecture against a concrete, visually verifiable deliverable.
> Additional mission types (research workflows, multi-stage data
> pipelines, automation jobs the operator picks up next) will land on
> the same scaffold as the project matures.
>
> Built on a single premise: **automate the production pipeline, then
> let the system evolve its own logic.**  Every commit in this
> repository is a step in that evolution — the history is not a record
> of outputs, but of how the agent system itself grows over time.

> **Engineering decisions, one page.**
> [`docs/engineering-case-studies.md`](docs/engineering-case-studies.md)
> — four production incidents and the minimum mechanism each one
> produced (Tier-1 routing, semaphore-bounded batch, content-quality
> feedback loop, three-layer reactive audit). Each entry follows
> *problem → constraint → decision → artifact*.

## Design notes

A few choices that distinguish this from a typical agent demo:

- **Outcome layer vs. work queue, kept separate.** [`docs/goal.md`](docs/goal.md)
  holds the active goal as a concrete deliverable; [`docs/roadmap.md`](docs/roadmap.md)
  holds the day-level work queue. An empty queue does **not** mean the
  goal is achieved — only the goal's "Done when" criteria do. The split
  exists because an earlier 24-hour stretch produced 11 infra commits
  with the queue reading 0 open items and 0 actual outputs.
- **Operator contract as canonical, committed source of truth.**
  [`docs/operator-contract.md`](docs/operator-contract.md) — 12 hard
  rules + conventions. The agent's local memory is a fast-access cache
  that links back to this file; if the two disagree, the file wins and
  the memory entry is corrected.
- **Cost firewall between orchestration and execution.** Anthropic API
  tokens are spent only during orchestration (Tier 1). Mission execution
  (transcribe → select → render → QA) runs entirely on local tools —
  `whisper.cpp` + `ollama` + `ffmpeg` — and costs zero tokens. See
  [`docs/cost-model.md`](docs/cost-model.md).
- **Out-of-band auditor with an active alert surface.** The
  [`auditor`](.claude/agents/auditor.md) subagent runs daily at 03:00
  via `launchd`, walks the whole repo read-only, and writes to a stable
  channel: [`docs/audit/CURRENT-ALERT.md`](docs/audit/) exists iff the
  latest verdict is non-CLEAN; the next interactive session is
  contractually obligated to read it before picking up the goal.
- **File-based subagent handoff.** Subagents do not share conversation
  history. They communicate through committed files (`plan.md` /
  `MANIFEST.md` / `qa-report.md`). Each subagent's context is bounded
  by its prompt + the manifest it reads — predictable token cost,
  predictable failure modes.

## Sample output

60+ mission outputs have been produced to date across **five** mission
types.  Most recent focus (2026-05-17) is the new `music-video`
mission — music-as-primary-audio shorts (no narration, no captions,
beat-aligned cuts, onset-aligned glitch micro-edits) — chosen via
operator pilot pick documented in
[`docs/pilots/decision-log.md`](docs/pilots/decision-log.md#operator-pick--2026-05-17).
A four-effect post-processing shader layer landed the same evening
(pond surface, breathing zoom, halation, phrase-aware combo;
cartoon deferred — see [case study 5](docs/engineering-case-studies.md#5-shader-effects-in-ffmpeg--knowing-where-the-wall-is)),
and `scripts/daily-music-video.sh` wraps the mission + shader as a
queue runner suitable for cron / launchd daily-upload cadence.
The `faceless-short` mission (narration-driven shorts) remains the
showcase below; the v1 pipeline outputs (single-clip highlight +
shorts-batch) remain as the baseline reference further down.

### Music-video pilots (post-pivot, 2026-05-17)

The `music-video` mission produces a 60-second 9:16 short where the
music IS the message: operator-supplied music track is the sole
audio; cuts land on aubiotrack-derived phrase boundaries; per-clip
playback speed is varied by mood (slow contemplative scenes at
0.55×, ambient at 0.70×, active at 0.80×, natural at 1.00×); micro
"scratch" glitches (0.2 s reverse + 0.2 s forward jump-cut) fire on
detected drum onsets but **only on clips classified as static-camera**
so the frame doesn't shake during the glitch; subtle film grain +
soft vignette + Gaussian zoom-pulse on each glitch onset add a
vintage lo-fi treatment.

Five prototype renders (v1 → v5) iterated through this design with
operator feedback at each step:

- v1: even 7.5 s cuts (no beat-sync)
- v2: cuts moved to phrase boundaries from `aubiotrack`
- v3: + per-clip variable playback speed (calm scenes slowed)
- v4: + glitch micro-edits at every slow clip's mid-point
- v5: + glitch placement restricted to `aubioonset` drum hits on
       static-camera clips only (no glitch on handheld pans)

v5 was operator-validated and promoted into
[`agents/missions/music-video/run.sh`](agents/missions/music-video/run.sh).
The v6 vintage-lofi visual treatment (grain + vignette + zoom-pulse)
landed on top of v5 in the same mission, tunable per render via
`MUSIC_VIDEO_FILM_GRAIN_INTENSITY`, `MUSIC_VIDEO_VIGNETTE_ANGLE`, and
`MUSIC_VIDEO_ZOOM_PULSE_AMP` env vars.  Output mp4s remain gitignored
(records/ directory); music files themselves are local-only by policy
([`assets/music/README.md`](assets/music/README.md)) — a "free to use
in your video" license is not the same as a "free to redistribute
the file" license, so the repo never carries audio assets.

Reproduction:

```bash
agents/missions/music-video/run.sh <id> <path/to/music.mp3>
```

#### Post-processing shaders (2026-05-17 evening)

Operator asked for shader-style effects on top of the v6 vintage-lofi
treatment.  Three effects landed via pure ffmpeg filter graphs (no GLSL,
no external tool) and one was deliberately deferred:

- **`pond`** — Animated water-surface displacement on the whole frame.
  Two procedural displacement maps (X and Y) are generated by `geq` as
  3-component sin wave fields at 540×960 (4× faster than full res),
  scaled up via bicubic to 1080×1920, then fed into `displace`.  Max
  ±13 px (~1.2 % of frame width) — visible across the entire image but
  not jarring.  Reads as "the whole screen IS a pond surface, gently
  sway".
- **`breathing`** — Continuous gentle scale wave, 5 s period, +0–5 %.
  Always upscale so the post-`crop` frame never under-runs (the
  first attempt with `sin(t)` range −1 to +1 crashed libx264 mid-frame
  when scale went below 1080; fixed by reformulating as
  `(0.5 + 0.5*sin)` so the multiplier is always ≥ 1.0).
- **`halation`** — Warm light bloom around bright pixels.  Split the
  source, brighten-threshold + 22 px gblur the copy, screen-blend back
  onto the original at 0.30 opacity.  Looks like 80s-film light leak
  on amber / neon regions — operator confirmed "확실히 티남" (clearly
  visible) on first pass.
- **`combo`** — `pond` + `halation` with **phrase-aware strength
  envelopes**.  Both effects' intensity is a function of `T` (time):
  off / quiet during intro (0–15 s), ramping up across the build
  (15–22.5 s), full during the climax (22.5–45 s), tapering through
  the wind-down (45–52.5 s), settling for the outro (52.5–60 s).
  The phrase boundaries match the Velvet Turntable reference track's
  95.8 BPM × 12-beat phrase = 7.5 s cadence; for other tracks the
  envelope is parameterised in the script.

What was *not* attempted: **cel-shading / cartoon** rendering.  ffmpeg
posterising luma and chroma independently (`lutyuv` with
`round(val/N)*N` quantisation) breaks hue — operator's reaction was
"완전 그냥 초록색만 나옴" (everything turned green).  Real cel-shading
requires either GLSL shaders (mpv + libplacebo, ~200–500 LOC),
EbSynth (paint one keyframe, propagate by motion), or AI stylisation
(Stable Diffusion + AnimateDiff, ComfyUI, RunwayML / Kaiber).  None
of those fit inside the ffmpeg pipeline, so the cartoon route is
parked as a separate R&D branch rather than half-implemented in
production.

Reproduction:

```bash
# Apply a single effect
scripts/music-video-shaders.sh pond     <input.mp4> <output.mp4>
scripts/music-video-shaders.sh halation <input.mp4> <output.mp4>

# Phrase-aware combo (the validated end product)
scripts/music-video-shaders.sh combo    <input.mp4> <output.mp4>
```

### Faceless pilots (English + Korean A/B)

The `faceless-short` mission produces a complete 60-second short from a topic prompt alone — no input video.  Pipeline: ollama drafts the narration script → Kokoro-ONNX (`am_michael`, or macOS `Yuna` for Korean) synthesizes voice → whisper.cpp transcribes for timing → script-aware caption correction maps proper nouns back to the original script → SRT cues split to single-line at natural punctuation breaks (stops 2-line opaque-box overlap on mobile) → ollama extracts one Pexels search term per temporal narration window (8 windows) → Pexels Videos API fetches one B-roll clip per window → ffmpeg trims each clip to its window's duration, crops to 9:16 screen-fill, burns libass captions and an attribution overlay.

Each topic is rendered in two language variants so the operator can A/B voice + caption rendering side by side:

| | Hittites (history × Bible) | Hydrogen (science) |
|---|---|---|
| EN | ![Hittites EN — 9:16 screen-fill, English caption 'and siege warfare.' on a single line over an aerial Hattusa archaeological dig](docs/pilots/screens/hittites-en-caption-verify.jpg) | ![Hydrogen EN — 9:16 screen-fill, English caption 'The human body's reliance' on a single line over a pasta-macro B-roll](docs/pilots/screens/hydrogen-en-caption-verify.jpg) |
| KO | ![Hittites KO — Korean caption '도시의 모습이 드러났습니다.' single-line over aerial Hattusa archaeology, AppleGothic font, macOS Yuna voice](docs/pilots/screens/hittites-ko-caption-verify.jpg) | ![Hydrogen KO — Korean caption '평균적으로 사람 몸무게의' single-line over olive-oil-drop macro, Yuna voice](docs/pilots/screens/hydrogen-ko-caption-verify.jpg) |

Each language variant picks its OWN B-roll by extracting Pexels search terms from its own captions per window — so the EN and KO shorts share script structure but not always identical clips (the v3/v4 design picked per-window keywords for narration-beat alignment).  Pass `FACELESS_REUSE_BROLL=<en_mission_dir>` to force the KO render to reuse the EN stitched B-roll when an apples-to-apples "same visuals, swapped audio" test is wanted.

A/B production notes, per-platform upload metadata, and the next-10 topic queue all live under [`docs/pilots/`](docs/pilots/).  Per-pilot cost: **$0** (Pexels free tier, all other stages local).

### v1 pipeline (single-clip highlight / shorts-batch)

The original v1 missions — `highlight`, `summarize`, `shorts-batch` — take a real source URL (e.g., a Creative-Commons video) and produce 9:16 outputs with burned-in source attribution + captions.  These predate `faceless-short`; they're still in active use when you want a clip *from* a video rather than a clip *of* a topic.

![6-second animated preview of highlight-015213, showing the 9:16 letterbox-blur layout, top-left source attribution, and bottom libass-burned caption](docs/demo/highlight-015213-preview.gif)

Six-second slice of `highlight-015213/outputs/short.mp4` — Sintel trailer (CC-BY-3.0, © Blender Foundation), 39 s 9:16 with watermark + captions.  Full mp4 lives under `records/` (gitignored); this GIF is a size-optimized excerpt (360 px wide, 12 fps, ≈ 2.8 MB) generated by ffmpeg with palette dithering, kept in `docs/demo/` as durable evidence of the v1 pipeline.

| Single highlight | Shorts batch |
|------------------|--------------|
| ![Sintel single highlight, 9:16 short with burned-in captions and top-left source attribution](docs/caption-verify/highlight-015213-sintel-cap.jpg) | ![Sintel shorts-batch first cut, 9:16 short with burned-in caption](docs/caption-verify/shorts-batch-024840-short-01-cap.jpg) |
| `highlight-015213` · 39 s · PASS attempt 1 | `shorts-batch-024840 / short-01` · 44 s · PASS attempt 1 |

Both sourced from the *Sintel* trailer (CC-BY-3.0, © Blender Foundation — `durian.blender.org`).  Top-left source-attribution overlay, 9:16 letterbox-blur background, libass-burned caption inside the bottom safe-zone box.

### Recent missions

| Mission | Type | Source | Output | Wall time | QA |
|---------|------|--------|--------|-----------|----|
| `faceless-hittites-032538` | faceless-short | topic prompt + Pexels B-roll (8 windows) | 62.7 s 9:16 short (49 MB) | ~75 s | PASS attempt 1 |
| `faceless-hittites-ko-032653` | faceless-short | Korean script + Yuna voice | 60.3 s 9:16 short (35 MB) | ~49 s | PASS attempt 1 |
| `faceless-hydrogen-032742` | faceless-short | topic prompt + Pexels B-roll | 59.7 s 9:16 short (21 MB) | ~64 s | PASS attempt 1 |
| `faceless-hydrogen-ko-032846` | faceless-short | Korean script + Yuna voice | 38.9 s 9:16 short (14 MB) | ~33 s | PASS attempt 1 |
| `highlight-032405` | highlight | Korean CC-BY-3.0 interview clip | 60 s 9:16 short | — | PASS attempt 1 |
| `summarize-025121` | summarize | Sintel 1080p · Blender CC-BY-3.0 | EN + KO `summary.md` (551 B) | — | PASS attempt 1 |
| `highlight-203219` | highlight | earlier dev fixture (2026-05-15) | 30 s short | 73.2 s | **FAIL** — QA gate, retry exhausted (blocker file written) |

The FAIL row is preserved: the QA gate isn't theatre.  On exhaustion of `QA_RETRY_MAX`, a blocker file is written to `records/blockers/<date>/<mission-id>.md` and the mission halts.  Full ledger of all tracked runs: [`docs/metrics-dashboard.md`](docs/metrics-dashboard.md).

### Pilot scorecard — how each version actually improved

Operator-asked question: *"thumbnails alone don't tell me what's
getting better."*  Honest answer is a structured self-evaluation
across five dimensions that map to short-form viewer retention.

![Stacked horizontal bar chart, Pilot scorecard — Hittites EN v4 26/50, Hittites EN v5 32/50, Hittites EN v6 44/50, Hydrogen EN v5 28/50, Hydrogen EN v6 43/50; five-color segments per bar showing Hook, Visual sync, Readability, Factual, Polish dimensions](docs/metrics/scorecard.png)

The lift from v5 → v6 (single-line caption was already in place at
v5; v6 swapped the script-generation stage from local `llama3.2:3b`
to Claude Sonnet) is +12 points on Hittites EN and +15 on Hydrogen
EN.  Most of the v5→v6 delta is **Hook** and **Factual coherence**
— exactly the dimensions the operator surfaced as broken in v5
("초반 5초에 시선 끌만한게 없음", "10%인지 60%인지 헷갈리네").

Honest disclosure: scores are assigned by Claude, not a viewer
panel.  They are a structured progress signal until real platform
watch-time data replaces them.  Full per-version breakdown +
reasoning + dimension definitions in
[`docs/pilots/scorecard.md`](docs/pilots/scorecard.md).  Source
data: [`docs/pilots/scorecard.json`](docs/pilots/scorecard.json).
Regenerate the chart after editing the JSON:
`.venv/bin/python scripts/generate-scorecard-chart.py`.

### Operator-intervention trend — is the system getting more autonomous?

A multi-agent system that needs constant human steering hasn't
escaped the same effort it was meant to replace.  This chart tracks
the ratio of commits with explicit user direction vs commits the
agent picked up on its own.

![Stacked-bar chart of commits per day from 2026-05-14 to 2026-05-17, split by initiator (blue = agent-autonomous, red = user-initiated), with a black line tracking user-initiated percentage on the right axis](docs/metrics/intervention.png)

Honest read: the line is **not** trending the right direction yet.
05-14 (0 % user) was setup automation.  05-15 and 05-16 were
overnight infra work where the agent ran the roadmap.  05-17 (42 %
user) was a niche-decision day — the operator was deliberately in
the loop choosing topics, picking models, and surfacing quality
gaps.  This high-intervention pattern is expected during decision
phases; the target is for the line to drop back under 15 % once
the system enters production mode (chosen niche, recurring topic
queue, scheduled uploads).

The `Requested-by: user` commit-message marker (introduced in
`7c6ff4f` on 2026-05-17 — dashed line in the chart) gives the
classifier a strict signal going forward.  Older commits are
classified by heuristic match on the body text, so the historical
recall for "user-initiated" is lower than reality.

Source data + per-commit classification: [`docs/metrics/intervention.json`](docs/metrics/intervention.json).  Regenerate the chart any time with `.venv/bin/python scripts/generate-intervention-chart.py`.

### Per-mission timing (v1 highlight missions only)

![Per-mission time breakdown — v1 highlight missions, stacked by stage (transcribe + select + render + other)](docs/metrics/per-mission-time.png)

![Throughput — output seconds produced per second of total compute time, one bar per v1 highlight mission](docs/metrics/throughput-realtime.png)

> **Scope note**: the charts cover **only `highlight-*` missions** because their `metrics.json` carries a per-stage `stages_s` breakdown.  `faceless-short` mission timing is captured per-run in the mission's own `metrics.json` (no stage split — pipeline is a single bash script), so it doesn't show up here.  v2 of the chart generator would unify the two.  Charts regenerate from current `records/missions/*/metrics.json` via `scripts/setup-venv.sh` once + `.venv/bin/python scripts/generate-charts.py`.

Every second plotted above is local CPU / GPU time: `whisper.cpp` for transcribe, `ollama` (`llama3.2:3b`) for select, `ffmpeg` for render.  **Anthropic API tokens spent during these stages: 0.**  The Tier-1 / Tier-2 cost
firewall is explained in [`docs/cost-model.md`](docs/cost-model.md).

## For analysts / reviewers

Doing a read-only analysis of this repository? Start at
[`docs/for-analysts.md`](docs/for-analysts.md) — a single-file entry
point optimized for first-pass diagnosis. Pairs with
[`docs/cost-model.md`](docs/cost-model.md) (where Anthropic vs. local
cost lives) and [`docs/architecture.md`](docs/architecture.md) (full
data-flow map).

## Architecture

```
              ┌───────────────────┐
              │   Orchestrator    │   model: opus
              └─────────┬─────────┘
                        │ delegates the mission, in order
                        ▼
              ┌───────────────────┐
              │      Planner      │   model: sonnet
              └─────────┬─────────┘
                        ▼
              ┌───────────────────┐
              │     Resourcer     │   model: sonnet
              └─────────┬─────────┘
                        ▼
              ┌───────────────────┐
              │       Editor      │   model: sonnet
              └─────────┬─────────┘
                        ▼
              ┌───────────────────┐
              │         QA        │   model: sonnet
              └───────────────────┘

              ┌───────────────────┐
              │      Auditor      │   model: sonnet  (out-of-band)
              └───────────────────┘   read-only; scheduled daily
                                       at 03:00 via launchd
```

| Agent | Responsibility | Output |
|-------|----------------|--------|
| 🤖 **Orchestrator** (opus) | Mission decomposition, delegation, final synthesis | task list · `summary.md` |
| 🧠 **Planner** (sonnet) | Strategy, work breakdown, acceptance criteria | `plan.md` |
| 📦 **Resourcer** (sonnet) | Asset fetching, external tool execution (ffmpeg / yt-dlp / whisper) | `resources/MANIFEST.md` |
| 🎞️ **Editor** (sonnet) | Output rendering, deliverable assembly | `outputs/CHANGELOG.md` |
| ✅ **QA** (sonnet) | Validation against plan criteria, regression detection | `qa-report.md` |
| 🔍 **Auditor** (sonnet) | Repository-wide drift / contract / cost / security audit (out-of-band, daily 03:00) | `docs/audit/<date>-<focus>.md` + `docs/audit/CURRENT-ALERT.md` when non-CLEAN |

Subagent definitions: [`.claude/agents/`](.claude/agents/) · Mission templates and shared shell libs: [`agents/`](agents/)

## Code / Data separation

| Layer | Path | Tracked |
|-------|------|---------|
| Code (logic) | `.claude/agents/`, `agents/`, `config/`, `scripts/` | ✓ |
| Data (outputs) | `records/missions/<date>/<id>/` | ✗ (gitignored) |
| Secrets | `.env` | ✗ (gitignored) |

The repository contains only the agent system itself. Mission outputs —
videos, transcripts, generated assets — stay local under `records/`.
What appears on GitHub is the system's own evolution, not its products.

## Platform support

| Surface | macOS 14+ | Linux |
|---------|-----------|-------|
| Mission execution (transcribe → select → render → QA) | ✓ | ✓ (`ffmpeg` / `whisper.cpp` / `ollama` all available) |
| Hardware-accelerated render (`h264_videotoolbox`) | ✓ Apple Silicon | n/a — falls back to libx264 via `-allow_sw 1` |
| `bootstrap.sh` synthetic fixtures (macOS `say`-based TTS) | ✓ | skipped — point at real CC fixtures via `scripts/fetch-fixtures.sh` |
| `launchd` schedulers (nightly auto-run, daily audit) | ✓ | replace with systemd timers or cron — see `scripts/com.melons.agents.*.plist` for the schedule to mirror |

macOS is the **primary, end-to-end tested** platform.  Linux works for
mission execution but the schedulers and synthetic-fixture generation
need OS-specific adaptation.  Cross-platform CI is not yet in place;
the clone-and-go flow is verified on Darwin only.

All tool paths and endpoints are env-managed — `agents/lib/env.sh`
resolves any blank `*_BIN` var via `command -v`, so a working PATH
install is enough.  Override in `.env` only when needed.

## Autonomy modes

Defined in [`config/policies.yaml`](config/policies.yaml).

| Mode | Flag | Behavior |
|------|------|----------|
| ⚙️ **Interactive** (default) | `AUTONOMY_MODE=false` | Pauses for user confirmation on logic changes, destructive ops, and external publishes. |
| 🌙 **Autonomous** | `AUTONOMY_MODE=true` | Runs unattended within `AUTONOMY_BUDGET_USD`. Logic files (`agents/`, `.claude/agents/`) are immutable. |

## Mission flow

1. User states a mission.
2. `orchestrator` opens `records/missions/<date>/<id>/` + a task list.
3. `planner` → `plan.md` with acceptance criteria.
4. `resourcer` → assets + `resources/MANIFEST.md`.
5. `editor` → deliverables + `outputs/CHANGELOG.md`.
6. `qa` → `qa-report.md` with PASS / FAIL per criterion.
7. On PASS, `orchestrator` writes `summary.md`.

## Toolchain

`ffmpeg` (libass-enabled — `brew install ffmpeg-full` on macOS,
`apt install ffmpeg` on Linux) · `yt-dlp` · `whisper.cpp`
(`small`, multilingual) · `ollama` (`llama3.2:3b`) · `Kokoro-ONNX` (TTS,
Apache 2.0 — faceless-short narration) · macOS `say` (Korean +
fallback voice) · Pexels Videos API (free tier — faceless-short B-roll) ·
Claude API for orchestration.

## Prerequisites

- **macOS 14+** (primary, fully tested) or **Linux** (best-effort —
  see [Platform support](#platform-support) above)
- **Homebrew** on macOS, or `apt` / `pacman` / equivalent on Linux
- **Apple Silicon recommended** — `h264_videotoolbox` is used for
  hardware-accelerated render; `-allow_sw 1` is set so the pipeline
  falls back to libx264 on Intel / Linux
- **~3 GB free disk** — whisper.cpp `small` model (~150 MB), Sintel
  CC-BY-3.0 trailer fixture, two synthetic `bootstrap.sh` fixtures
- **Tools**: `ffmpeg` (built with libass), `ffprobe`, `whisper.cpp`,
  `ollama`, `yt-dlp`.  `scripts/bootstrap.sh` checks all of them and
  prints an exact `brew install …` / `apt install …` command for
  anything that's missing, so a missing tool isn't a silent failure.

## Quick start

```bash
# Clone — either URL form works
git clone https://github.com/MelonS/MelonS-Agents.git    # HTTPS
# git clone git@github.com:MelonS/MelonS-Agents.git      # SSH
cd MelonS-Agents

# Boot: copies .env from .env.example, verifies tools, auto-fetches
# the whisper model (~150 MB) and the ollama highlight model
# (llama3.2:3b), generates two macOS-only synthetic fixtures.
./scripts/bootstrap.sh

# Produce a 9:16 short from the Sintel trailer (CC-BY-3.0)
./agents/missions/highlight/run.sh https://download.blender.org/durian/trailer/sintel_trailer-1080p.mp4
```

The mission writes its output to
`records/missions/<date>/highlight-<HHMMSS>/outputs/short.mp4`
(gitignored — products stay on your machine; only the agent system
itself is on GitHub).

Multi-source batch:

```bash
./scripts/batch-mission.sh -f sources.txt
```

Queue-based autonomous drain (used by the launchd scheduler):

```bash
echo 'https://example.com/long.mp4' >> records/queue/pending.txt
./scripts/mission-queue.sh
```

Install the nightly scheduler:

```bash
./scripts/install-scheduler.sh install
```

## Operator contract

This repository is fully agent-operated. The day-to-day rules:

- The **agent does all the work** — installs, edits, configs, commits, pushes, scheduling. The user does not run commands in the terminal.
- The user steps in **only** when a hard guardrail blocks the agent (e.g., self-modifying its own permissions, force-pushing to `main`) — and even then only as a single click of approval, never a multi-step recipe.
- **Active focus** lives in [`docs/roadmap.md`](docs/roadmap.md). The list below ("Status") is a flat capability ledger; do not read it as a TODO list. The roadmap's *Now* section is the source of truth for "what to work on next."
- **Money firewall**: paid APIs, SaaS subscriptions, and cloud-resource creation require explicit user confirmation. Local resources (Ollama, FFmpeg, whisper.cpp, brew) stay fully autonomous.

Full contract: see [`CLAUDE.md`](CLAUDE.md) and the [`config/policies.yaml`](config/policies.yaml) autonomy rules.

## Status

<!-- status:start -->
- [x] Hierarchical agent scaffold (orchestrator + 4 mission subagents + 1 read-only auditor subagent)
- [x] Code/Data separation enforced (records/ gitignored)
- [x] Env-driven tool paths (.env / .env.example)
- [x] PoC end-to-end: highlight extraction (EN + KO)
- [x] libass burned captions (`agents/lib/env.sh` auto-detects a libass-enabled ffmpeg; falls back to the `ffmpeg-full` keg on macOS)
- [x] Multilingual whisper.cpp (small) + language-aware highlight prompt
- [x] Batch runner (scripts/batch-mission.sh)
- [x] Auto-commit + auto-push of every logic change to origin/main
- [x] Nightly launchd scheduler for autonomous mode
- [x] Four mission types operational: highlight, summarize, shorts-batch, faceless-short
- [x] Faceless-short pipeline — topic prompt → ollama script → Kokoro-ONNX TTS (Apache 2.0) → whisper.cpp timing + script-aware caption correction → Pexels B-roll → 9:16 screen-fill render; Korean variant via macOS Yuna + AppleGothic.  Pilot evidence: [`docs/pilots/`](docs/pilots/)
- [x] Single-pass ffmpeg render (~3× render speedup)
- [x] Bilingual summarize mission (transcribe → structured EN+KO summary)
- [x] Cost / runtime metrics per mission
- [x] Real CC-licensed source fixtures (Blender open movies) + downloader
- [x] Standard 9:16 layout engine — safe-zone margins, semi-transparent caption box, top-left source-attribution overlay
- [x] Source-attribution wiring across all three missions (`outputs/SOURCES.txt` + burned watermark + `summary.md` footer)
- [x] QA feedback retry loop (failed missions auto-retried up to `QA_RETRY_MAX`, then dropped to `records/blockers/`)
- [x] Copyright filter v1 — domain allowlist, publish-gate, strike-record log, strike-aware source rejection
- [x] License-string probe for archive.org + commons.wikimedia.org
- [x] Day-level roadmap at [`docs/roadmap.md`](docs/roadmap.md) (source of truth for "what to work on next")
- [x] Per-platform reuse rules in `scripts/publish-gate.sh` (`internal-demo` / `public` / `youtube` / `instagram` / `tiktok` — honors all four `publish_rules` fields)
- [x] Repository auditor subagent + active surface (`docs/audit/CURRENT-ALERT.md` auto-maintained by `scripts/audit-run.sh`)
- [x] **Reactive auditor — L1**: git post-commit hook (`scripts/hooks/post-commit.sh`) fires `audit-run.sh contract` automatically when a commit touches drift-risk paths (`.claude/agents/`, `agents/`, `config/`, `CLAUDE.md`, `docs/operator-contract.md`, `scripts/audit-run.sh`, `.claude/settings.json`). Install with `scripts/install-hooks.sh install`.
- [x] **Reactive auditor — L2**: 15-min mission-anomaly poll (`scripts/audit-poll.sh` via `com.melons.agents.audit-poll.plist`) detects new blockers + QA-FAIL bursts and fires a focused audit; cheap no-op when nothing's wrong. Install with `scripts/install-scheduler.sh install audit-poll`.
- [x] Clone-and-go reproducibility — host-agnostic `.env.example`, OS-aware `scripts/bootstrap.sh` with install hints, `scripts/fetch-whisper-model.sh` model auto-fetch, `scripts/test-fresh-clone.sh` simulator with PASS evidence at [`docs/onboarding/fresh-clone-log.txt`](docs/onboarding/fresh-clone-log.txt)
- [x] Per-mission metric charts (v1 highlight missions only) — [`docs/metrics/per-mission-time.png`](docs/metrics/per-mission-time.png) + [`docs/metrics/throughput-realtime.png`](docs/metrics/throughput-realtime.png), regenerated by `.venv/bin/python scripts/generate-charts.py` (venv via `scripts/setup-venv.sh`)
- [x] **Single-line caption enforcement** — `scripts/split-long-captions.py` runs between caption-correction and ASS rendering, splits any cue > 28 chars at natural punctuation breaks. Stops 2-line opaque-box overlap on mobile.
- [ ] Real user-supplied URL fixture — _blocked, waiting on URL from user_
- [ ] License-string probe for additional hosts (Vimeo CC channel, etc.) — _deferred, Vimeo lacks a per-item license endpoint; revisit on demand_
- [ ] Audio-fingerprint check (chromaprint / `fpcalc`) — _deferred, needs a fingerprint dataset to compare against; revisit after first takedown_
- [ ] Logo / watermark detection on source frames — _deferred, needs OCR or a trained model; revisit when the failure mode is observed_
- [ ] Iterative QA-feedback loop *inside* editor (per-output re-cut without rerunning transcribe/select) — _parked, only useful when coarse retry wastes compute; not yet observed_
<!-- status:end -->

> Unchecked items above are **all intentionally deferred** — each carries an inline reason. The day-level priority queue lives in [`docs/roadmap.md`](docs/roadmap.md), not here.

## License

MIT. See [`LICENSE`](LICENSE).
