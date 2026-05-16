# Ideas — parking log for v2+ concepts

## Purpose

A place to record ideas and improvements that come up during v1 development
**without implementing them immediately**.  v1-안정화 전까지 메인 파이프라인
외 구현 금지가 약속이고, this file is the device that holds that promise.

If an idea matters, it goes here first.  Implementation happens only after
v1 stabilizes and the item gets promoted to `docs/roadmap.md`.

## Writing rules

- New ideas go at the **top** of the relevant category section (newest first).
- Header format: `### YYYY-MM-DD | Title | Priority (L/M/H)`
- Body covers: motivation, rough implementation sketch, dependencies,
  estimated cost (tokens / time), current status.
- When an item is promoted to active work, **move it** to
  `docs/roadmap.md` (don't leave a copy here).
- When an item is rejected, leave the title with ~~strikethrough~~ and
  one line on why (so the rejection itself is durable knowledge).

## Categories

Three sections to start.  Split further only when one section exceeds ~10
live entries — empty subcategories are noise.

1. **Agents** — new subagents, changes to existing agents.
2. **Pipeline + Infrastructure** — mission flow, handoff protocols, retry
   logic, schedulers, backups, monitoring, token management.
3. **Intelligence + Misc** — external information gathering, trend
   detection, ideas that don't fit elsewhere.

---

## Agents

_(none yet)_

---

## Pipeline + Infrastructure

### 2026-05-16 | Domain-pivot portability — framework vs vertical split | L

**Motivation**: this repo is currently optimized for short-form video
production, but the operator may pivot to other domains later (job-
application automation, game development, etc.).  Pre-decision note
on how the current design holds up under domain change — recorded
now so future-us doesn't have to re-derive the analysis when a
pivot becomes concrete.

**Domain-neutral surface (~60% of the repo, survives any pivot)**:
- Mission directory layout (`records/missions/<date>/<id>/{plan.md,
  resources/, outputs/, qa-report.md, summary.md}`)
- Subagent pattern (orchestrator/planner/resourcer/editor/qa/auditor)
  + file-based handoff
- Tier 1 / Tier 2 cost firewall concept
- Operator contract (12 hard rules — money firewall, never-pause,
  PII, auto-commit, dual-stack reporting are all domain-agnostic)
- `goal.md` / `roadmap.md` / `ideas.md` three-layer planning
- Audit subagent + drift detection
- Shared libs: `env.sh`, `log.sh`, `retry.sh`, `ollama.sh`

**Domain-bound surface (~40%, rewritten per pivot)**:
- `agents/lib/{ffmpeg,whisper,attribution,copyright}.sh`
- Mission templates (`highlight`, `summarize`, `shorts-batch`)
- `scripts/publish-gate.sh` + `config/copyright-allowlist.yaml`
- `LAYOUT_*` env vars, caption-verify frames, chart code
- README / architecture vocab tied to video

**Real gaps the current design will hit on pivot** (not bugs today;
become bugs in a new domain):
1. **No cross-mission persistent state store** — `records/` holds
   single-run outputs.  "Have I applied to this company already?" or
   "Which characters has this game generated?" has nowhere to live
   except git log.  Likely needs SQLite or a flat JSON registry.
2. **Outbound-only I/O** — yt-dlp / curl / ffmpeg are all outbound
   shell-outs.  No inbound webhook / API path.  Job-app reply
   tracking would need a small FastAPI listener.
3. **No interactive browser automation in mission libs** —
   Playwright MCP exists at the agent level, but isn't usable from
   inside a mission's `run.sh`.  Job sites + SPA forms need it.
4. **Weak human-in-the-loop gate at publish** — video QA PASS auto-
   approves publish.  Job-app submission probably wants explicit
   confirmation per action (parallel to money firewall, but for
   irreversible non-money actions).
5. **Game dev domain distance is large** — Unity/Godot toolchains,
   binary asset LFS, build pipelines have ≈ 0% overlap with current
   video stack.

**Same-repo-vs-new-repo decision rule** (drafted; revisit per pivot):
- **Same repo** when tool overlap > 50% AND assets are text/JSON/
  small media AND portfolio narrative survives the merge.
- **New repo** when tool overlap < 25% OR assets need git-lfs OR
  domain story gets muddled in one README.
- Current tentative reads: **job-app automation → same repo**
  (ollama + curl + playwright reuse, all-text assets);
  **game development → new repo** (Unity/Godot zero overlap, binary
  assets force LFS, story muddle).

**Dependencies**: a concrete pivot direction from the operator.
None pending right now; this is documentation, not work.

**Estimated cost**: 0 for this note.  Per-pivot integration cost
estimated separately when a pivot lands.

**Status**: parked.  No domain pivot is confirmed.  When one does
become concrete, re-read this entry first, pick the gaps that
apply to the target domain, and address them before the migration
rather than during it.

---

### 2026-05-16 | 4-tier autonomy model (replace Layer 1 / Layer 2 binary) | M

**Motivation**: the current "Layer 1 (main conversation) decides
everything, Layer 2 (subagents) are pure functions of their prompt"
model is too coarse, and operator caught it.  The auditor already
runs **partially independent**: launchd wakes it without Layer 1,
it makes real autonomous judgments (severity classification, verdict
synthesis), and writes to a stable channel
(`docs/audit/CURRENT-ALERT.md`) that the next Layer 1 session is
contractually obliged to read.  Calling that "Layer 2" undersells
its actual separation.

The single-Layer-1 model also creates real problems:
- Layer 1 = single point of decision-making.  When no session is
  active, priority-judgment stops.  A CRITICAL audit alert can sit
  unread for days if sessions are sparse.
- Three operationally-different kinds of work (real-time
  decisions / mission decomposition / periodic monitoring) all
  forced through the same orchestration path.
- The setup assumes one machine + one operator + frequent sessions.
  Multi-machine deployment (e.g., Mac orchestrator + Linux GPU
  worker) doesn't fit.

**Sketch — four explicit autonomy tiers**:

| Tier | Pattern | Examples | Autonomy scope |
|------|---------|----------|----------------|
| **Interactive** | user conversation | main conversation Claude | day-level decisions, goal selection |
| **Mission** | invoked per task | orchestrator + planner / resourcer / editor / qa | mission decomposition, task-scoped autonomy |
| **Monitor** | scheduled / event-triggered | auditor (today); future cost-watcher / backup-watcher | observe state, classify, alert; reversible auto-fix within own domain (e.g., regenerate metrics, clear empty dirs) |
| **Action** | external side-effects | publish.sh / deploy / push to Slack (not built yet) | always behind explicit user OK; money firewall reinforced here |

Auditor would migrate Layer 2 → Monitor tier with slightly expanded
autonomy: can do reversible self-clearing fixes (metric refresh,
caption-verify regeneration) but never edits agent definitions or
external systems.  Mission subagents stay Mission tier.

**Multi-machine readiness (further-future deliverables)**:
- Replace file-based handoff (`records/`, `docs/audit/`) with a
  message-queue or RPC layer when more than one host is involved.
- Decide records sync mechanism (S3 / syncthing / git-lfs — each has
  trade-offs).
- Add a push-notification channel for Monitor-tier CRITICAL findings
  so they don't wait for the next interactive session.
- Per-tier credentials / scope so a single compromised host doesn't
  give full system access.

**Dependencies**: v1 fully stabilized; at least two cooperating
agents running on different schedules in production (Monitor tier
gets meaningful only when there's enough monitoring volume to
matter); user-driven push of needing multi-machine.

**Estimated cost**: design-only at first (1 day to write the
contract); per-component delivery later (~1–2 days each — Monitor
auto-fix scope, alert escalation channel, multi-host sync).

**Status**: parked (v2+).  Direct operator quote that surfaced this:
"Layer1이 모든걸 다 판단하는게 맞나 싶기도 하고 ... 감시자 같은건
사실 어느정도 별개로 돌아야 하는거 아닌가 싶기도 하고".  Captures
a real architectural ceiling in the v1 design; don't lose it.

---

## Intelligence + Misc

### 2026-05-16 | Generative-AI for visual / audio assets | M

**Motivation**: operator direction "할거 없으면 생성형ai 사용해서
하는것도 해보고 있어".  The current pipeline produces 9:16 shorts
from existing CC video sources.  Generative AI could fill two
gaps that don't have a content source: thumbnails / cover frames
(currently a frame-extract from the rendered short) and
audio-fillers (silence between dialogue lines, intro / outro
beds).  Worth a small probe before committing — many directions
overlap with what's already in the pipeline and could be
redundant.

**Sketch — three sub-directions to evaluate**:
1. **Local image generation for thumbnails** — Stable Diffusion
   via ComfyUI or `diffusers` running on the same Apple Silicon
   that drives whisper / ollama.  Tier 2 (local, no API cost) by
   design.  Output: one 1080×1920 PNG per short as a "cover" —
   currently we just extract a frame.  Concrete value if the
   source video has long static frames where a frame-extract is
   ugly.
2. **TTS for filler narration** — Coqui / Piper / Bark.  Useful
   only for missions where there's a logical gap in source audio
   that captions can't bridge.  Probably narrow value; the macOS
   `say` we already use for fixtures is good enough for that
   case.
3. **Tier-1 image generation (paid APIs)** — DALL-E 3, Imagen 4,
   Midjourney via Discord — **money firewall** applies.  Reject
   by default; only consider if a specific mission needs a
   non-local-renderable asset and operator opts in.

**Dependencies**: v1 fully stabilized (done — clone-and-go goal
shipped); a target mission that *needs* a generated asset
(speculative — no current mission does).  Risk: turning on
generative-AI tools "because we can" violates the
v1-안정화-전까지-메인-외-구현-금지 promise.  Hold direction (1)
behind a concrete mission-side use case.

**Estimated cost**: probe phase 1 day (install one local image
generator, render 3 test thumbnails, compare against
frame-extract on the same short).  No paid-API spend in the
probe.

**Status**: parked (v2+).

---

### 2026-05-15 | Scout agent (external information gathering) | M

**Motivation**: Multi-agent harness construction precedent is thin in
Korean-language sources, so external community signal has outsized
value.  Manual community monitoring costs time.  Collected signal can
feed back as candidate missions — gives the orchestrator something to
chew on when the user-supplied queue is empty.

**Sketch**:
- Separate mission track; runs independently of the shorts pipeline.
- Sources: Reddit (`r/ClaudeAI`, `r/LocalLLaMA`, `r/AI_Agents`),
  GitHub trending (claude-code / agent topics), GeekNews RSS,
  Hacker News Algolia API.  X / Twitter held back pending cost review.
- Tier 2 (local, free) does the fetching: `curl` + small Python.
- Stage 1 filter — keyword regex, local, no tokens.
- Stage 2 — classify + summarize each surviving item with Haiku.
  Sonnet is over-spec for this; reject any temptation to upgrade.
- Optional daily digest — Sonnet allowed if it adds judgement, not
  just formatting.
- Storage: `records/intel/<date>/` (gitignored — never commit
  scraped third-party content).

**Dependencies**: v1 stabilized; QA retry loop already shipped (good).

**Estimated cost**: Haiku tokens trivial (low thousands / day).
Build effort 1–2 days.

**Status**: parked (v2+).

---

## Rejected ideas

_(none yet — this section exists so future-you knows that strikethrough
entries are intentional and reasoned, not accidental clutter.)_
