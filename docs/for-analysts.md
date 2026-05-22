# For analysts

Single-file entry point for anyone (human or AI) doing a *read-only*
analysis of this repository.  Optimized for accuracy of first-pass
diagnosis, not for getting up to speed on usage.  If you're here to
*use* the system, start at [`../README.md`](../README.md) instead.

Read order:

1. This file (orientation + common analyst mistakes).
2. [`cost-model.md`](cost-model.md) — where money is and isn't spent.
3. [`architecture.md`](architecture.md) — full data-flow map.
4. [`operator-contract.md`](operator-contract.md) — the *behavioral*
   rules the agent operates under.
5. [`goal.md`](goal.md) + [`roadmap.md`](roadmap.md) — outcome layer
   + work queue.  Goal is read first by sessions that ask for work.
6. [`ideas.md`](ideas.md) — v2+ parking log.  Useful for "what was
   considered and deferred, and why" — anything *not* in the active
   roadmap that's been thought about lives here.

Each downstream file is self-contained.  You should not need to read
agent source to give a useful first-pass review.

---

## TL;DR (60 seconds)

This is a **two-tier** system on **one machine** (macOS).

```
Tier 1 — Conversational      ┃ Anthropic API. Opus + Sonnet.
                             ┃ Claude Code CLI is the runtime.
                             ┃ Cost lives here.
Tier 2 — Mission execution   ┃ Local. bash + ffmpeg + whisper.cpp.
                             ┃ Zero API cost. Free.
Tier 1 (opt-in)              ┃ One creative stage — faceless-short
within Tier 2                ┃ narration-script generation — can route
                             ┃ to Sonnet via FACELESS_SCRIPT_OVERRIDE
                             ┃ (Max-plan quota, no incremental $).
                             ┃ Default is ollama (Tier 2).
                             ┃ See docs/cost-model.md for the routing
                             ┃ rule and case-study #1.
```

The split is the central design choice.  An analyst who misses it
will diagnose phantom problems.

**A mission run is effectively free.**  The mechanical stages
(transcribe, render, B-roll fetch) burn zero Anthropic tokens.  By
default the faceless-short script stage routes to local ollama
(zero tokens too); operators who want the quality lift documented in
case-study #1 can opt into a Sonnet hop via `FACELESS_SCRIPT_OVERRIDE`
— that path is ~500 tokens per call, operationally negligible
against the existing Max-plan quota.  whisper.cpp and ffmpeg are
the always-local stages regardless of routing.

---

## Reproducibility evidence (not just a claim)

The clone-to-output path is exercised by
[`scripts/test-fresh-clone.sh`](../scripts/test-fresh-clone.sh):
clone the public repo into a temp dir → run `scripts/bootstrap.sh`
(auto-fetches whisper model + ollama model, prints OS-specific
install hints for missing CLIs) → run one highlight mission against
the Sintel CC-BY-3.0 trailer → assert `short.mp4` ≥ 1 MB.  Each
run appends a PASS / FAIL line to
[`onboarding/fresh-clone-log.txt`](onboarding/fresh-clone-log.txt).

If that log's most recent line is PASS against
`https://github.com/MelonS/MelonS-Agents.git`, the README's
"Quick start" has been **exercised** on a clean tree, not just
asserted.  First-run FAIL with diagnostic detail is also a
common state — see the first entry in the log for the
ffmpeg / ffmpeg-full Homebrew packaging gotcha that the test
uncovered and the env.sh fix that resolved it.

The faceless-short pilots (2026-05-16 niche-selection deliverables,
now in Past goals) live under [`pilots/`](pilots/) — caption-verify
thumbnails, source scripts, caption-correction logs, and ready-to-paste
platform upload copy.  Full MP4s stay in gitignored `records/` since
their long-term value is the platform URL + view metrics, not the
local file.

The simulator is one of the project's [deliverable subgoals](goal.md):
infrastructure subgoals alone don't complete a goal until a
concrete artifact exists that proves the path works.

---

## Subagent layout (already verified, do not re-recommend)

`.claude/agents/*.md` frontmatter sets the model.  As of 2026-05-22
(unchanged since 2026-05-15 — last commit touching `.claude/agents/*.md`
was `8570a9c`):

| Agent          | Model     | Role                                              |
| -------------- | --------- | ------------------------------------------------- |
| `orchestrator` | **opus**  | Top-level mission decomposition + coordination    |
| `planner`      | sonnet    | Mission brief → `plan.md` + acceptance criteria   |
| `resourcer`    | sonnet    | Fetch / probe / prepare assets → `resources/`     |
| `editor`       | sonnet    | Apply changes → `outputs/`                        |
| `qa`           | sonnet    | Validate outputs against plan.md → `qa-report.md` |
| `auditor`      | sonnet    | Out-of-band read-only audit (three trigger layers: L1 post-commit hook on drift-risk paths, L2 15-min mission-anomaly poll, L3 daily 03:00 baseline via launchd) → `docs/audit/<date>-<focus>.md` + `CURRENT-ALERT.md` when non-CLEAN |

A common analyst suggestion is "downgrade subagents from opus to
sonnet."  This is already done.

A *valid* future move is downgrading `resourcer` to **haiku** —
file fetching needs little reasoning depth.  Not done because the
delta is small and risk of misfetched assets is non-trivial.

---

## Skills (agentskills.io-spec packages)

Added 2026-05-19 (merged to main in v0.2.0):
top-level `skills/<name>/` is a new tracked layer holding
agentskills.io-spec-compliant skill packages.  Each skill is a
runtime-portable description of a pipeline that other compatible
runtimes (Cursor, Goose, Gemini CLI, OpenAI Codex, Claude Code,
GitHub Copilot, etc.) can also invoke.  First skill:
[`skills/music-video/SKILL.md`](../skills/music-video/SKILL.md);
its `scripts/run.sh` symlinks to
`agents/missions/music-video/run.sh` so the skill inherits the
mature mission pipeline without logic duplication.

Layer distinction:

- **agents/missions/** = the canonical bash pipeline.  Tracked.
- **skills/\<name\>/SKILL.md** = the agentskills.io-spec wrapper
  pointing at the pipeline.  Tracked.  Discoverable cross-runtime.
- **.claude/skills** = per-machine symlink to `skills/` so Claude
  Code's path resolver finds them.  Gitignored, rendered by
  `scripts/install-claude-local.sh`.

---

## Mission execution: where the money *isn't*

Three of the five missions (`agents/missions/{highlight,summarize,shorts-batch}/run.sh`)
do **not** call Anthropic.  They call `agents/lib/ollama.sh` which
posts to `http://127.0.0.1:11434/api/generate`.

`faceless-short` is **opt-in Tier-1 by design**: by default the
narration-script stage calls local ollama (`agents/missions/faceless-short/run.sh:98`
runs `ollama_generate "$OLLAMA_MODEL_HIGHLIGHT"`).  Operators who
want the quality lift documented in case-study #1 run
`scripts/gen-script-claude.sh` out-of-band (Sonnet via Max quota,
no incremental dollar spend) and point `FACELESS_SCRIPT_OVERRIDE` at
the generated script file — `run.sh` then copies it in place of
the ollama-generated draft.  The remaining stages stay Tier-2 in
both routings: Kokoro-ONNX (TTS, Apache 2.0, CPU), whisper.cpp
(transcribe), and the Pexels Videos free-tier HTTP API (200 req/hr,
20k req/month — no card, commercial reuse via the Pexels License).
Reasoning for the split routing rule is in
[`docs/cost-model.md`](cost-model.md); the engineering case study
(score impact: hook + factual axes 3→9) is
[`docs/engineering-case-studies.md`](engineering-case-studies.md) §1.

`music-video` is fully Tier-2 (no Anthropic at runtime).  Audio is
operator-supplied (Suno-generated, YT Audio Library, or any music
the operator has rights to use, kept local-only via
[`assets/music/`](../assets/music/README.md) — never committed).
Beat + onset detection runs locally via `aubio`.  B-roll matches mood
keywords through the same Pexels API as faceless-short.  No
narration / no captions — music is the message.  See
[`agents/missions/music-video/run.sh`](../agents/missions/music-video/run.sh)
and the niche-pivot decision in
[`docs/pilots/decision-log.md`](pilots/decision-log.md#operator-pick--2026-05-17).

- [`agents/lib/ollama.sh`](../agents/lib/ollama.sh) — HTTP client to local Ollama (highlight / summarize / shorts-batch).
- [`scripts/gen-script-claude.sh`](../scripts/gen-script-claude.sh) — Sonnet-routed narration script generator (faceless-short; opt-in via `FACELESS_SCRIPT_OVERRIDE`, default is ollama).
- [`scripts/score-content.sh`](../scripts/score-content.sh) — Sonnet-routed content-quality scorer (feedback loop).
- [`agents/missions/music-video/run.sh`](../agents/missions/music-video/run.sh) — aubiotrack + aubioonset + Pexels (no Anthropic).
- [`scripts/music-video-library-audit.sh`](../scripts/music-video-library-audit.sh) — operator utility that lists every `assets/music/*.mp3` with detected genre / shader preset / render status; quick "what's left to render?" view (no Anthropic).
- [`scripts/music-video-fetch-ai-still.sh`](../scripts/music-video-fetch-ai-still.sh) — wraps the free Pollinations.ai image API to generate stillzoom backgrounds when no operator-supplied still is available (`--ai-still` flag in `music-video-auto.sh`; no Anthropic).
- [`scripts/music-video-lyric-align.sh`](../scripts/music-video-lyric-align.sh) — whisper.cpp-based vocal-onset alignment that converts a plain-text lyric file into an LRC-timed file consumed by `music-video-lyrics.sh` (quality-bar directive #3, no Anthropic).
- [`scripts/music-video-genre-detect.sh`](../scripts/music-video-genre-detect.sh) — heuristic genre auto-detect from filename + filename-keywords; resolves an mp3 to one of the 19 declared presets in `skills/music-video/data/genre-presets.yaml` (no Anthropic).
- [`scripts/music-video-fetch-still.sh`](../scripts/music-video-fetch-still.sh) — fetches a Pexels still image for stillzoom genres (ambient / classical / dreampop / kpop_ballad) when no operator-supplied still is available; companion to the AI-still fetcher (no Anthropic; free Pexels API).
- [`scripts/log-decision.sh`](../scripts/log-decision.sh) — operator tooling.  Appends one bullet to `docs/autonomous-decisions.md` under today's date section; used by autonomous runs to record unilateral decisions for the operator's morning scan (no Anthropic).
- [`scripts/music-video-doctor.sh`](../scripts/music-video-doctor.sh) — skill-specific health check for `skills/music-video/`.  Verifies pipeline prerequisites (aubio / whisper / Pexels key / shader catalog) and emits human or `--json` verdicts; consumed by the generic `scripts/doctor.sh` aggregator (no Anthropic).
- [`scripts/music-video-trim.sh`](../scripts/music-video-trim.sh) — utility that trims a finished `short.mp4` to a tighter sub-segment for re-upload variants (e.g., 30 s teaser from a 60 s short); accepts `--start` / `--end` and preserves the audio + video codec (no Anthropic).
- [`scripts/music-video-upload-meta.sh`](../scripts/music-video-upload-meta.sh) — generates the per-mission YouTube upload metadata template (title / description / tags / privacy / thumbnail path) at `outputs/publish/upload-meta-v2/<id>.json`; consumed by the operator-side YT upload step (no Anthropic).
- [`scripts/music-video-validate.sh`](../scripts/music-video-validate.sh) — combined pre-publish gate run before any upload.  Wraps duration / resolution / loudness LUFS / shader-anchor coverage / lyric-sync-drift checks; emits PASS / WARN / FAIL verdict with an actionable hint per failed check (no Anthropic).
- [`scripts/music-video-thumbnail.sh`](../scripts/music-video-thumbnail.sh) — extracts an upload-ready 9:16 still frame (default mid-climax t=30 s) from a rendered `short.mp4`; auto-chained by `music-video-auto.sh` post-render (no Anthropic).
- [`scripts/lyric-extract.sh`](../scripts/lyric-extract.sh) — whisper-based plain-text lyric pull from an `.mp3`.  Strips `♪` markers + parenthetical notes; output feeds `music-video-lyric-align.sh` upstream of the overlay step.  Useful when no operator-supplied `.txt` exists (no Anthropic).
- [`scripts/first-touch.sh`](../scripts/first-touch.sh) — single-command zero-account demo wizard for fresh clones.  Detects environment, runs bootstrap, fetches CC-BY demo cache, renders + opens a 60 s `short.mp4` with one Y/n consent.  The conversion path for a stranger cloning the repo (no Anthropic).
- [`scripts/music-video-batch.sh`](../scripts/music-video-batch.sh) — multi-track render wrapper; iterates an `.mp3` glob through `music-video-auto.sh` with per-track lyric pairing + skip-if-already-rendered idempotency.  Used for overnight production batches (no Anthropic).
- [`scripts/shot-plan.sh`](../scripts/shot-plan.sh) — pre-render intent layer between phrase detection and B-roll fetch.  Opt-in scaffold (`MUSIC_VIDEO_USE_SHOT_PLAN=1`); generates a per-segment shot plan from the lyric LRC + phrase boundaries, paralleling working music-video director discipline.  Research origin: `docs/research/2026-05-22-music-video-director-methodology.md` (no Anthropic in current scaffold).
- [`scripts/roadmap-done-sync.sh`](../scripts/roadmap-done-sync.sh) — auto-bulk-reconciliation of `docs/roadmap.md` Done section.  Auto-detects base SHA from the most-recently-referenced commit, lists commits since then, appends a single bulk entry covering the gap.  Idempotent against partial prior syncs.  `--apply` writes; default is preview (no Anthropic).
- [`scripts/morning-brief.sh`](../scripts/morning-brief.sh) — single-command overnight digest.  Reads doctor + audit-alert + intervention 7-day trend + commit attribution + autonomous-decisions + review queue + blockers into ~30 lines.  `--lang en|ko` (default autodetects from `$LANG`); read-only (no Anthropic).
- [`.env.example`](../.env.example) — `OLLAMA_MODEL_HIGHLIGHT=llama3.2:3b` still required by the three local-only missions.
- [`agents/missions/highlight/run.sh`](../agents/missions/highlight/run.sh) — `ollama_generate "$OLLAMA_MODEL_HIGHLIGHT" "$PROMPT" true`.

The transcript flow specifically:

1. whisper.cpp (local C binary) writes
   `<mdir>/resources/transcript.json`.
2. A jq filter extracts `[{start, end, text}, ...]` into
   `<mdir>/resources/segments.json` (typical size: 2–5 KB for a
   2-minute lecture, ~50 KB for a 30-minute talk).
3. The segments JSON is concatenated into the model prompt and
   sent to **local Ollama**, never to Anthropic.

**An analyst who says "transcript chunking to save tokens" has
mis-tiered the architecture.**  Ollama prompts are RAM/CPU-bound on
the user's M2; they have no per-token cost.

---

## Retry semantics (what re-runs on QA FAIL)

See [`agents/lib/retry.sh`](../agents/lib/retry.sh) and the loop in
each mission's `run.sh`.

| Step                          | Re-runs on retry? | Cost on retry  |
| ----------------------------- | ----------------- | -------------- |
| 1. Source fetch (yt-dlp/curl) | **no**            | 0              |
| 2. Whisper transcribe         | **no**            | 0              |
| 3. Ollama model select        | yes               | local CPU only |
| 4. ffmpeg render              | yes               | local CPU only |
| 5. QA verdict                 | yes               | 0              |

`QA_RETRY_MAX` defaults to 2 (i.e. up to 3 total attempts), set in
[`agents/lib/retry.sh`](../agents/lib/retry.sh).  Steps 3 and 4 are
the only ones that re-run.  The retry loop prepends the previous
`qa-report.md` to the model prompt so the next attempt sees what
failed and why.

A future refinement (`Iterative QA-feedback loop inside editor` in
[`roadmap.md`](roadmap.md)) would let editor re-cut a single window
without re-prompting the model — cheaper CPU, no token impact.

---

## Autonomy budget (not the analyst's worry)

[`config/policies.yaml`](../config/policies.yaml) caps overnight
autonomous mode at **`budget_usd_ceiling: 5.00`**.  This is the
**Anthropic** budget for orchestration when the user is asleep; not
the Ollama budget (which is zero).

If autonomous mode runs into a blocker, the orchestrator writes to
`records/blockers/<date>/<mission-id>.md` and halts cleanly.  See
[`agents/lib/retry.sh:qa_write_blocker`](../agents/lib/retry.sh).

---

## Where the costs actually come from

Real Anthropic-API consumers, in descending order:

1. **Long user-Claude conversations** — like the session that built
   the system in the first place.  Cache-hit ratio matters here;
   Claude Code applies prompt caching by default, so cost scales
   with *new* content, not full history.
2. **Orchestrator on big missions** — opus deciding which mission
   to fire, reading roadmap.md, deciding when to halt.  Single-mission
   cost is small; budget concern is overnight loops.
3. **Subagent invocations** — sonnet, file-based handoff via
   `plan.md` / `MANIFEST.md`.  Each handoff is bounded by the
   manifest file size; not the full repo.

Total mission-execution API cost: **0**.

---

## What's *not* in this repo (analyst-relevant gaps)

- No Anthropic SDK usage anywhere.  All Anthropic API traffic goes
  through Claude Code CLI, which manages caching and model routing
  itself.
- No cloud resources, no databases, no message queues.  Everything
  on disk under `records/` (gitignored).
- **CI scope is intentionally narrow.** GitHub Actions runs on
  every push to `main` via
  [`.github/workflows/main-protection.yml`](../.github/workflows/main-protection.yml)
  (added `a537018`, 2026-05-18): six static checks — bash syntax
  on tracked scripts, secret scan, required-files presence,
  `.env.example` schema sanity, README link hygiene, gitignore
  coverage.  No test runner, no build step, no deploy gating
  beyond the Pages deploy workflow (`pages.yml`).  Pre-commit
  gating is intentionally absent so the auto-commit / auto-push
  contract stays simple — see
  [`operator-contract.md`](operator-contract.md) §6.  One
  functional regression check exists outside CI:
  [`scripts/test-fresh-clone.sh`](../scripts/test-fresh-clone.sh)
  runs a full clone → bootstrap → mission cycle against
  `origin/main` on demand, with PASS / FAIL evidence in
  [`onboarding/fresh-clone-log.txt`](onboarding/fresh-clone-log.txt).
  Not yet wired to a hook; run manually after substantive changes.
- No publish path yet — `scripts/publish-gate.sh` exists as a stub
  that any future `publish.sh` will call; no current `publish.sh`.
- No multi-user, no auth, no secrets management beyond `.env`
  (gitignored).  Single-developer machine.

---

## Common analyst mistakes (avoid these)

1. **"Downgrade subagent models"** — already done; see table above.
2. **"Transcript chunking will save tokens"** — wrong tier; Ollama,
   not Anthropic.
3. **"QA retry is unbounded loop"** — capped at `QA_RETRY_MAX=2`,
   blocker written on exhaustion.
4. **"Subagents share full conversation history"** — they don't;
   handoff is file-based (`plan.md` + `MANIFEST.md`), not message-
   based.
5. **"Auto-commit means no review"** — true, by design.  Operator
   contract treats every commit as reviewed *at write time* by the
   conversation participant.  Not a defect; a policy.

If you find a real defect that isn't on the "common mistakes" list,
flag it.  The above are pre-empted to save you cycles.
