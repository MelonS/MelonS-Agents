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
                  │    planner (opus)     │ → plan.md
                  └───────────┬───────────┘
                              ▼
                  ┌───────────────────────┐
                  │   resourcer (opus)    │ → resources/ + MANIFEST.md
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

 ┌────────────┐  ┌────────────┐  ┌────────────┐  ┌────────────┐  ┌────────────┐
 │ highlight  │  │ summarize  │  │shorts-batch│  │  faceless  │  │music-video │
 │ run.sh     │  │ run.sh     │  │ run.sh     │  │  -short    │  │ run.sh     │
 │            │  │            │  │            │  │  run.sh    │  │            │
 └─────┬──────┘  └─────┬──────┘  └─────┬──────┘  └─────┬──────┘  └─────┬──────┘
       │               │               │               │               │
       │ input: URL    │ input: URL    │ input: URL+N  │ input: topic+ │ input:
       │ → 1 short     │ → summary.md  │ → N shorts    │   prompt      │  music
       │               │               │               │ → 1 short     │  +kws
       │               │               │               │   (synth,     │ → 1 short
       │               │               │               │    narrated)  │   (music
       │               │               │               │               │    primary,
       │               │               │               │               │   no narr.)
       └──────────┬────┴─────────────┬─┴───────────────┴───────────────┘
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
                      │                    (faceless-short, opt-in via
                      │                     FACELESS_SCRIPT_OVERRIDE;
                      │                     Sonnet via Max quota.
                      │                     Default is local ollama.)
                      │    • aubio         beat + onset detection
                      │                    (music-video only)
                      │    • Pexels API    portrait B-roll
                      │                    (faceless-short + music-video)
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
| Skills (agentskills.io-spec packages) | `skills/<name>/` (top-level) | ✓ git | ✗ (firewall) |
| Outputs / data | `$RECORDS_DIR/` | ✗ (gitignored) | ✓ |
| Publish-ready outputs (§8 operator-directed deviation) | `outputs/publish/` | `.gitkeep` + `outputs/publish/upload-meta-v2/*.json` tracked (intentional v2 batch from 2026-05-21 `a182380`; v3+ gitignored); mp4/wav/jpg gitignored | ✓ |
| Review queue (§8 operator-directed deviation) | `outputs/review-queue/` | `README.md` + `.gitkeep` tracked; `pending/` and `decided/` subdirs gitignored | ✓ |
| Secrets / tool paths | `.env` | ✗ (gitignored) | ✓ |
| Committed evidence | `docs/caption-verify/`, `docs/metrics-dashboard.md` | ✓ git | regenerated by scripts |

The `outputs/publish/` row is an explicit §8 deviation directed by
the operator on 2026-05-17 (`docs/goal.md` 2026-05-17 active-goal
entry — "publish-ready path not buried under
`records/missions/<date>/<id>/outputs/`").  The deviation marker
lives at `outputs/publish/.gitkeep`.  No output artifact is
committed — only the scaffolding file — but the directory itself
sits outside `$RECORDS_DIR` by design.

## Mission types

| Type | Input | Output | Time-dominant stage |
|------|-------|--------|---------------------|
| `highlight` | long video | one 30–60s 9:16 captioned short | render |
| `summarize` | long video | structured EN+KO summary markdown | model |
| `shorts-batch` | long video + N | up to N 9:16 captioned shorts | render×N |
| `faceless-short` | topic prompt + tone | one 60 s 9:16 captioned short (TTS-narrated, B-roll stitched) | TTS + render |
| `music-video` | operator-supplied music file + mood keywords | one 60 s 9:16 short, music-as-sole-audio, no narration, no captions, beat-aligned cuts + onset-aligned glitches | beat detection + render |
| `product-cf` | product brief + assets | one 9:16 product CF short (builds on music-video) | render |
| `content-short` | topic/profile (`info`\|`news`\|`idol`) + research.json | one fact-checked, legal-gated 9:16 short (wraps faceless-short; 리서치→제작⇄법률→출시 — `docs/content-shorts-pipeline.md`) | TTS + render + legal loop |

The first three are *transformation* missions — they start from a long
source video and reshape it.  `faceless-short` and `music-video` are
*generation* missions — there is no source video; the entire short is
synthesized from a prompt (faceless-short) or assembled around an
operator-supplied music track (music-video) via local LLM/TTS or
local beat-detection + a stock-footage API (Pexels, free tier).
Output shape is identical so the renderer and layout engine are shared.

The first four missions reuse the same shell library set; `music-video`
uses a distinct subset (`env.sh`, `log.sh`, `ffmpeg.sh`) plus the
`aubio` CLI tools for beat/onset detection — no `ollama.sh`,
`whisper.sh`, or `tts.sh` because there is no narration path.

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

## Skills layer — two shapes

The `skills/<name>/` directory holds
[agentskills.io](https://agentskills.io)-spec packages.  A Skill is
a portable description of a pipeline: any compatible runtime
(Claude Code, Cursor, Goose, Gemini CLI, OpenAI Codex, GitHub
Copilot, etc.) that implements the spec can git-clone the skill
and invoke it.  The Skill is the canonical user-facing unit; the
mission-vs-skill distinction below is an *implementation* concern,
not a user-visible one.

Skills come in two shapes depending on whether the work fits the
5-agent (orchestrator + planner + resourcer + editor + qa)
mission pipeline:

| Shape | When to pick | `scripts/run.sh` body | Examples |
|---|---|---|---|
| **Missions-routed** | The work decomposes naturally into discrete planner / resourcer / editor / qa stages with non-trivial work in each | Symlink to `agents/missions/<type>/run.sh` (re-uses mission tuning, lib functions, retry loops) | Skill #1 `music-video` — beat extraction (resourcer) + B-roll fetch (resourcer) + multi-stage ffmpeg render (editor) + codec/duration verification (qa) all non-trivial |
| **Standalone** | The work is mechanical (HTTP + parse + format) — planner / qa would be near-empty stages | Direct implementation in the skill itself; no `agents/missions/` counterpart | Skill #2 `job-hunt` (scaffold on `feat/skill-job-hunt`) — fetch from N job boards → filter → dedupe → render markdown; planner and qa stages would be trivial |

The decision is recorded per-skill in `SKILL.md` `metadata.pipeline-source`:

```yaml
# missions-routed example (skills/music-video/SKILL.md):
pipeline-source: agents/missions/music-video/run.sh

# standalone example (skills/job-hunt/SKILL.md):
pipeline-source: scripts/run.sh (this skill is self-contained — no agents/missions/ counterpart)
```

Why distinguish: forcing every skill through the 5-agent
orchestrator adds four file-based handoffs per invocation
(plan.md → resources/ → outputs/ → qa-report.md).  Worth it when
each handoff carries real work; pure overhead when the stages
are empty.  Both shapes share the same external interface (the
agentskills.io frontmatter + invocation contract), so adding a
new skill doesn't force a structural choice at the spec layer —
only at the implementation layer.

## Faceless-short pipeline

The `faceless-short` mission has no input video — it generates the
short end-to-end from a topic prompt.  Default is fully local
(ollama writes the script); operators can opt into a Tier-1 hop via
`FACELESS_SCRIPT_OVERRIDE` pointing at a pre-generated script file
(typically produced out-of-band by
[`scripts/gen-script-claude.sh`](../scripts/gen-script-claude.sh) —
Sonnet via Max quota, ~500 tokens per call).  See
[`docs/cost-model.md`](cost-model.md#when-tier-2-is-the-wrong-default--creative-stages)
and
[`docs/engineering-case-studies.md`](engineering-case-studies.md) §1
for the routing rationale.

```
  topic prompt
       │
       ▼
  ┌──────────────────────────────┐
  │ ollama (default)             │  60s narration script
  │ llama3.2:3b via ollama.sh    │  (130–160 words EN /
  │  — or —                      │   ~300–360 chars KO)
  │ FACELESS_SCRIPT_OVERRIDE     │
  │ → cp pre-gen file (opt-in,   │
  │   typically claude sonnet)   │
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
  │ per-window keyword extraction│  keyword JSON, one term per
  │ (8 narration windows by      │  narration window
  │  default, FACELESS_NUM_BROLL)│  ["hittite ruins", "cuneiform", …]
  └────────────┬─────────────────┘
               ▼
  ┌──────────────────────────────┐
  │ scripts/pexels-fetch.sh      │  8 × B-roll.mp4 + .meta.json
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
