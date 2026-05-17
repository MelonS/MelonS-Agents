# Architecture

> Doing a read-only analysis? Start with [`for-analysts.md`](for-analysts.md) +
> [`cost-model.md`](cost-model.md).  Those two cover the API-cost vs
> local-execution tier split that's easy to miss from this diagram.

## One-glance map

```
═══════════════════════════════════════════════════════════════
  TIER 1 — Conversational orchestration       (Anthropic API)
═══════════════════════════════════════════════════════════════

                       User mission brief
                              │
                              ▼
                  ┌───────────────────────┐
                  │      Orchestrator     │     model: opus
                  │   .claude/agents/     │     (file-based handoff
                  │   orchestrator.md     │      across plan.md and
                  └───────────┬───────────┘      MANIFEST.md)
                              │
                              │ sequential delegation
                              ▼
                  ┌───────────────────────┐
                  │   planner (sonnet)    │ → plan.md
                  └───────────┬───────────┘
                              ▼
                  ┌───────────────────────┐
                  │  resourcer (sonnet)   │ → resources/ + MANIFEST.md
                  └───────────┬───────────┘
                              ▼
                  ┌───────────────────────┐
                  │   editor (sonnet)     │ → outputs/ + CHANGELOG.md
                  └───────────┬───────────┘
                              ▼
                  ┌───────────────────────┐
                  │     qa (sonnet)       │ → qa-report.md
                  └───────────────────────┘

      Out-of-band track (not in the mission pipeline):
                  ┌───────────────────────┐
                  │   auditor (sonnet)    │ → docs/audit/<date>-<focus>.md
                  │   read-only           │   + CURRENT-ALERT.md when
                  │                       │     verdict is non-CLEAN
                  └───────────▲───────────┘
                              │
                  Three trigger layers:
                    L1 (reactive)   — git post-commit hook fires on
                                      drift-risk commits  (~30s latency)
                    L2 (reactive)   — 15-min poll fires on mission
                                      anomaly patterns    (new blocker,
                                      QA-FAIL burst)
                    L3 (scheduled)  — daily 03:00 baseline via launchd

                              │ shell-out via Bash tool
                              │ (no API tokens cross this line)
                              ▼
═══════════════════════════════════════════════════════════════
  TIER 2 — Mission execution                  (Local, free)
═══════════════════════════════════════════════════════════════

 ┌────────────┐  ┌────────────┐  ┌────────────┐  ┌────────────┐
 │ highlight  │  │ summarize  │  │shorts-batch│  │  faceless  │
 │ run.sh     │  │ run.sh     │  │ run.sh     │  │  -short    │
 │            │  │            │  │            │  │  run.sh    │
 └─────┬──────┘  └─────┬──────┘  └─────┬──────┘  └─────┬──────┘
       │               │               │               │
       │ input: URL    │ input: URL    │ input: URL+N  │ input: topic+
       │ → 1 short     │ → summary.md  │ → N shorts    │   prompt
       │               │               │               │ → 1 short
       │               │               │               │   (synthesized)
       └──────────┬────┴─────────────┬─┴───────────────┘
                  ▼                  ▼
          agents/lib/*.sh       config/*.yaml
          (shared helpers)      (autonomy policy)
                      │
                      ▼  Local tools — NOT Anthropic (except where noted):
                      │    • yt-dlp        download
                      │    • whisper.cpp   transcribe (Metal GPU)
                      │    • ollama        select / summarize
                      │                    (llama3.2:3b — highlight,
                      │                     summarize, shorts-batch)
                      │    • Kokoro-ONNX   TTS for faceless-short (CPU)
                      │    • claude CLI    narration script + scorer
                      │                    (faceless-short only;
                      │                     Sonnet via Max quota)
                      │    • ffmpeg        render
                      ▼
            $RECORDS_DIR/missions/<date>/<mission-id>/
              ├── plan.md
              ├── resources/
              │   ├── source.mp4
              │   ├── transcript.json
              │   └── segments.json
              ├── outputs/
              │   ├── short(.NN).mp4
              │   ├── captions(.NN).srt
              │   ├── thumb.jpg
              │   └── summary.md          (summarize only)
              ├── qa-report.md
              ├── summary.md
              └── metrics.json
```

The dashed boundary between Tier 1 and Tier 2 is a `Bash` tool
invocation.  Anthropic tokens do not cross it — once the mission
`run.sh` starts, all model calls go to local Ollama, all transcripts
go to local whisper.cpp, all renders go to local ffmpeg.  See
[`cost-model.md`](cost-model.md) for the per-call cost table.

## Layers

| Layer | Lives in | Tracked | Mutable in autonomous mode |
|-------|----------|---------|-----------------------------|
| Logic (agent defs + missions + libs + scripts) | `.claude/agents/`, `agents/`, `scripts/`, `config/` | ✓ git | ✗ (firewall) |
| Outputs / data | `$RECORDS_DIR/` | ✗ (gitignored) | ✓ |
| Secrets / tool paths | `.env` | ✗ (gitignored) | ✓ |
| Committed evidence | `docs/caption-verify/`, `docs/metrics-dashboard.md` | ✓ git | regenerated by scripts |

## Mission types

| Type | Input | Output | Time-dominant stage |
|------|-------|--------|---------------------|
| `highlight` | long video | one 30–60s 9:16 captioned short | render |
| `summarize` | long video | structured EN+KO summary markdown | model |
| `shorts-batch` | long video + N | up to N 9:16 captioned shorts | render×N |
| `faceless-short` | topic prompt + tone | one 60 s 9:16 captioned short (TTS-narrated, B-roll stitched) | TTS + render |

The first three are *transformation* missions — they start from a long
source video and reshape it.  `faceless-short` is a *generation* mission
— there is no source video; the entire short is synthesized from a topic
prompt via local LLM + TTS + a stock-footage API (Pexels, free tier).
Output shape is identical so the renderer and layout engine are shared.

All four reuse the same shell library set:

- `agents/lib/env.sh` — env loader + `require_bin` / `require_env`
- `agents/lib/log.sh` — colored `log_info` / `log_ok` / `log_err` etc.
- `agents/lib/ollama.sh` — HTTP wrapper for `/api/generate`
- `agents/lib/whisper.sh` — transcribe + segment extraction
- `agents/lib/ffmpeg.sh` — duration / aspect / cut / 9:16 crop / SRT
  generation / single-pass `ffmpeg_render_short`
- `agents/lib/clamp-window.jq` — model picks → 30–60s clamped to
  segment boundaries and source duration
- `agents/lib/tts.sh` — Kokoro-ONNX (Apache 2.0) with macOS `say`
  fallback (used by `faceless-short`)

## Faceless-short pipeline

The `faceless-short` mission has no input video — it generates the
short end-to-end from a topic prompt.  Marginal cost: ~500 tokens
against the existing Max-plan quota for the one Tier-1 hop (script
+ scorer); everything else is local.  See
[`docs/engineering-case-studies.md`](engineering-case-studies.md) §1
for the routing rationale.

```
  topic prompt
       │
       ▼
  ┌──────────────────────────────┐
  │ claude --print               │  60s narration script
  │ --model claude-sonnet-4-6    │  (130–160 words EN /
  │ via scripts/gen-script-claude│   ~300–360 chars KO)
  │ retry loop with score gate   │
  └────────────┬─────────────────┘
               ▼
  ┌──────────────────────────────┐
  │ Kokoro-ONNX TTS              │  narration.wav
  │ (Apache 2.0, am_michael)     │  (~50–60 s)
  └────────────┬─────────────────┘
               ▼
  ┌──────────────────────────────┐
  │ whisper.cpp small            │  raw SRT (timing)
  │ (TIMING only — text drifts   │  + script-aware correction
  │  on proper nouns)            │  → captions.srt (text from script)
  └────────────┬─────────────────┘
               ▼
  ┌──────────────────────────────┐
  │ ollama → 6 visual search     │  keyword JSON
  │ terms (concrete imagery)     │  ["hittite ruins", "cuneiform", …]
  └────────────┬─────────────────┘
               ▼
  ┌──────────────────────────────┐
  │ scripts/pexels-fetch.sh      │  6 × B-roll.mp4 + .meta.json
  │ (Pexels API, free tier)      │  (photographer attribution)
  └────────────┬─────────────────┘
               ▼
  ┌──────────────────────────────┐
  │ ffmpeg trim+concat → 9:16    │  outputs/short.mp4
  │ letterbox-blur, audio mux,   │  + caption-verify.jpg
  │ libass burn-in, attribution  │
  └──────────────────────────────┘
```

Key design choice: **whisper provides timing, the script provides text.**
The source script is ground truth (we wrote it; the audio is synthesized
from it).  `scripts/correct-captions.py` aligns whisper tokens against
script tokens via `difflib.SequenceMatcher` and emits a corrected SRT
that uses the script's wording at whisper's timestamps — eliminating
small-model proper-noun drift (`Hattusa` → `Hadusa`, etc.).

Companion script: `scripts/gen-upload-metadata.sh` reads a finished
mission and drafts per-platform upload copy (YouTube Shorts title +
description, TikTok caption, Reels caption, hashtag set, B-roll
attribution credits aggregated from `.meta.json` sidecars).

Pilot artifacts and the A/B decision log live under
[`docs/pilots/`](pilots/).

## Single-pass render path

`ffmpeg_render_short` combines cut, 9:16 letterbox-with-gblur, and
caption burn into one `-filter_complex` invocation:

```
[0:v]scale=1080:1920:force_original_aspect_ratio=increase,
     gblur=sigma=15,crop=1080:1920[bg];
[0:v]scale=1080:1920:force_original_aspect_ratio=decrease[fg];
[bg][fg]overlay=(W-w)/2:(H-h)/2,setsar=1,subtitles=<basename>
```

Encoded by `h264_videotoolbox` on Apple Silicon (`-allow_sw 1` falls
back to software if hardware is missing). This collapsed the
previously triple-encoded pipeline (cut → crop → burn) into one pass,
~3× faster end-to-end on M2.

## Autonomous flow (overnight)

```
1. records/queue/pending.txt accumulates sources (URLs / paths)
2. launchd `com.melons.agents.queue` fires every 30 minutes
3. scripts/mission-queue.sh pops the next pending source, runs the
   highlight mission, logs to done.log
4. Each mission writes its plan/resources/outputs/qa-report/metrics
   under records/missions/<date>/<mission-id>/
5. scripts/aggregate-metrics.sh regenerates docs/metrics-dashboard.md
6. launchd `com.melons.agents.auditor` fires once daily at 03:00,
   runs scripts/audit-run.sh all, writes the dated report to
   docs/audit/ and maintains docs/audit/CURRENT-ALERT.md when the
   verdict is non-CLEAN
```

The two launchd jobs (`queue` every 30 min + `auditor` once daily at
03:00) run with `AUTONOMY_MODE=true` baked in.  Logic-layer files
(`agents/`, `.claude/agents/`) are NOT modified in this mode; only the
records/ tree and `docs/audit/` change.  Logic changes that *do* happen
during interactive sessions are auto-committed and auto-pushed per the
operator contract (`docs/operator-contract.md` §6).  Money firewall
remains active: paid API / cloud calls still require explicit human
confirmation per `config/policies.yaml`.

## Why "Code/Data separation"

The repo on GitHub shows only how the *system* evolves over time. The
products (megabytes of MP4, JPG, WAV) stay on the operator's machine.
This keeps clone size small, history readable, and removes the temptation
to git-track regenerable artifacts.

The exceptions — caption-verify JPGs and the metrics dashboard — are
*evidence*, not products. They're small, regenerable, and prove that
the pipeline works at the snapshot in time of each commit.
