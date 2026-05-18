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

### 2026-05-19 | A/B test — planner + resourcer at Opus vs Sonnet | M

**Motivation**: external review (community member, anonymized per
`no-pii-in-repo`) noted on 2026-05-19 ~01:30 KST:

> "Planner 와 Resourcer 가 Sonnet 이 아니라 Opus 로 셋팅했을 때
> 전체 퀄리티 차이가 어느 정도나 날지 문득 궁금하네요."

The question is well-posed.  We've never measured this dimension.
Current model assignments (in `.claude/agents/*.md`):

| Subagent | Current model | Rationale at the time |
|---|---|---|
| orchestrator | **opus** | top-level mission decomposition + cross-stage coordination |
| planner | sonnet | mission brief → plan.md (fixed format, low-creativity output) |
| resourcer | sonnet | fetch / probe / prepare assets |
| editor | sonnet | apply changes → outputs/ |
| qa | sonnet | validate against plan.md acceptance criteria |
| auditor | sonnet | repo-wide read-only audit |

**Where the question bites**: planner's `plan.md` and resourcer's
manifest are *upstream* artifacts that everything downstream
(editor, qa) consumes.  A weak plan compounds — editor renders
the wrong window, qa validates against a flawed acceptance line.
Opus's reasoning depth might catch ambiguous briefs that Sonnet
flat-tones.  But in practice — most of our current mission flow
is bash-scripted, not subagent-delegated, so the upgrade might
not bite where the subagents are most active (which is the
*orchestration* tier, where we already use Opus).

**Suggested A/B test design** (when this becomes priority):

1. Pick a moderately ambiguous mission brief — e.g., a faceless-
   short topic that needs interpretive framing ("explain the
   significance of the Hittites" without a script override).
2. Run twice:
   - Variant A: planner=opus, resourcer=opus (rest unchanged).
   - Variant B: current (planner=sonnet, resourcer=sonnet).
3. Hold input fixed.  Capture:
   - `plan.md` quality (subjective score 1-10 from operator,
     plus token count + section completeness).
   - `MANIFEST.md` quality (asset count, mood-keyword matching
     against the script's beats).
   - Final output quality (operator approval: yes / "이대로 올려도
     됨" / fix needed).
   - Token cost delta (Max plan quota burn).
   - Wall-clock delta.
4. If Opus yields ≥ 1 point on the subjective scale + measurable
   downstream win, promote planner + resourcer to Opus for at
   least the script-generation missions.  If not, keep Sonnet
   and document the null result.

**Where to do it**: this A/B *changes* `.claude/agents/*.md`,
which is logic-changes-need-OK per §5.  So the A/B itself runs
on a feat branch (`feat/abtest-planner-opus`) with explicit
operator OK to flip + revert after.  The two runs are commits on
that branch; the result + verdict commits to main as docs/.

**Dependencies**: faceless-short or another subagent-heavy mission
type is the better testbed than music-video (which is fully
scripted).  May also benefit from running after Skill #1 lands —
Skills may shift where the subagents are most active.

**Estimated cost**: ~2-3h hands-on (set up branch, swap models,
run two missions, score outputs, doc the verdict).  Token burn
depends on which missions we test — bounded by the Max plan
quota.

**Status**: parked.  External insight, not yet operator-decided.
Surface in the next "what's parked vs active" review with
operator.

---

---

## Pipeline + Infrastructure

### 2026-05-19 | Main-protection v2 — functional + periodic skill smoke | H

**Motivation**: operator post-midnight 2026-05-18 → 2026-05-19
exposed multiple dimensions of "main 깨짐" beyond what the current
static-check + audit layers cover.  Key insights:

1. **Our project's "main is broken" ≠ typical build-fail.**
   We're not a build-the-binary project; the real value is *the
   skill's actual functional output*.  "쇼츠가 제대로 생성되지
   않는다면" = main is broken, even if all static checks pass.
2. **Solo dev + multi-skill = silent decay risk.**  Once Skills
   are separated, working on Skill #2 (job-hunt) without
   invoking Skill #1 (music-video) means Skill #1 could break
   for weeks before operator notices.

The layered defense needed:

| Layer | Catches | Cost | Status |
|---|---|---|---|
| 1. Static check (GH Actions) | syntax / secrets / missing files | ~30s/push | ✅ shipped 2026-05-19 |
| 2. Audit (launchd 03:00 + 15min + commit hook) | drift / docs ↔ code | ~3min | ✅ existing |
| 3. **Pre-merge functional test** | "이 스킬 진짜 작동?" | ~2min/skill | ⏳ to build |
| 4. **Periodic skill smoke** | "어제 작동했던 스킬 오늘도?" | ~5min/day total | ⏳ to build |

**Sketch — Layer 3 (pre-merge functional)**:
- Per-skill `tests/functional.sh` — runs the actual pipeline
  against a fixed test fixture (small CC0 music / known input),
  asserts output validity (duration, dimensions, codec, file
  size, audio integrity).
- `scripts/pre-merge-check.sh` gate 1.5 added: detect what
  skills changed in the feat branch, run their functional tests
  before allowing merge.
- ~2 min per skill (running real ffmpeg + ollama).  Local only
  (operator's machine), not CI (ollama / Pexels API).

**Sketch — Layer 4 (periodic skill smoke)**:
- Per-skill `tests/smoke.sh` — lightweight (30s-1min) verifier
  that doesn't run full render but checks key invariants
  (B-roll fetcher returns clips, aubio detects beats, ffmpeg
  filter graph parses).
- New launchd plist `com.melons.agents.skill-smoke.plist` —
  fires daily (e.g., 04:00 — after auditor at 03:00), iterates
  all `.claude/skills/*/tests/smoke.sh`, writes status to
  `records/skill-smoke/<date>/status.json`.
- If any skill fails: writes `docs/skill-alerts/CURRENT.md`
  (same pattern as `docs/audit/CURRENT-ALERT.md`).
- Session-start protocol expanded: read skill-alerts file
  before goal selection.

**Sketch — Layer 5 (settings.json portability) — ✅ SHIPPED 2026-05-19 ~00:50 KST**:
- 2026-05-18 + 2026-05-19 audits flagged `.claude/settings.json`
  hardcoded `/Users/melons/` paths at 9 locations as [medium].
- Resolution: same template-render pattern as
  `scripts/com.melons.agents.*.plist` (`ab6555e`).  Shipped on
  `feat/skill-music-video` branch:
  - `config/claude-settings.template.json` (tracked) with
    `@@HOME@@` / `@@REPO_ROOT@@` / `@@HOME_PARENT@@` /
    `@@MEMORY_NAMESPACE@@` placeholders.
  - `scripts/install-claude-local.sh` renders to
    `.claude/settings.json` (now gitignored).
  - `bootstrap.sh` calls install-claude-local on every fresh
    run — idempotent.
  - Verified by fresh-clone simulation in /var/folders/... temp
    dir: `.claude/settings.json` rendered with the NEW machine's
    paths, not the operator's.  External user can clone-and-go.
- Commits: `912d61c` (template + install script), `40aeab1`
  (bootstrap integration).  Pending merge to main.

**Dependencies**: Layer 3 + 4 benefit from Skill #1 landing
first — without a Skill to test, the functional/smoke tests
have nothing to point at.  Sequence now:
  Skill #1 conversion ✅ → Layer 5 ✅ → Layer 3 (functional gate) →
  Layer 4 (periodic smoke) → Layer 6 (subagent migration).

**Estimated cost** (remaining):
- ✅ Layer 5: ~1-2h (template + install script + docs update) — SHIPPED.
- Layer 3: ~3-4h (framework + first skill's functional test).
- Layer 4: ~2-3h (launchd timer + dispatcher + status reporting +
  session-start protocol extension).
- Layer 6 (subagent migration `.claude/agents/` → `subagents/`):
  ~30 min (logic-changes-need-OK applies; not autonomous).

**Status**: Layer 5 complete; Layer 3, 4, 6 parked for "남는 시간에
하나씩" execution per operator direction 2026-05-19 ~00:50 KST.

---

### 2026-05-18 | Music-video format variations + per-video quality upgrade — brainstorm | H

**Motivation**: operator at ~18:20 KST surfaced three direction
clusters for the next stretch of work, distinct from "just queue
more identical music-videos."  Captured here so they aren't lost
between meeting + late-evening sessions; operator asked to revisit
this list when home (~23:00 KST).  No decision made yet.

**Cluster A — music-format variations (existing pipeline, light twist)**:

- **A1. Music + minimal captions** — 3–5 emotion / track-title words
  burned at beat-stress times.  Reuses existing libass burn-in path;
  ~half-day work.
- **A2. Mini-narrative B-roll** — replace mood-keyword extraction with
  a *sequential* arc ("morning → cafe → window → street → night") so
  the 8 windows tell a micro-story.  Touches the keyword generator in
  `music-video/run.sh`.
- **A3. Single-subject multi-angle** — 8 angles of the same subject
  (e.g., 8 rainy-Tokyo shots) instead of distinct mood keywords.
  Pexels query change only.
- **A4. Slow-cinema mode** — cuts at 3–4 s instead of beat-quick
  0.5–1 s, no glitches, vibe-first.  aubio settings flip.
- **A5. Channel branding cards** — 0–1 s logo intro + 58–60 s
  subscribe outro.  ffmpeg overlay only.

**Cluster B — content category expansion (beyond music)**:

- **B1. Music + ASMR layering** — rain / coffee / vinyl crackle noise
  bed mixed under the Suno track.  Audio-only change.
- **B2. Short-film cinematic trailer** — 1–2 min trailer (script + AI
  VO + B-roll).  New mission type, large scope, new active goal.
- **B3. Poem / quote + music** — text-led, music supports.  Hybrid of
  faceless and music-video.
- **B4. Game demo / speedrun** — operator's prior-domain expertise.
  Zero-overlap with current stack; already parked under "domain-pivot
  portability" below.  Keep as long-tail.

**Cluster C — per-video quality upgrades (revisit existing outputs)**:

- **C1. Shader pass for the rest** — pond / halation / combo applied
  to 01–02 / 04–09.  Per-video 3–5 min ffmpeg.
- **C2. Unified color LUT** — current 8 clips per video are from 8
  different photographers; tone is inconsistent.  Apply one LUT in
  the render stage.
- **C3. Beat-stress transitions** — hard-cuts → crossfade / whip-pan
  on beat-stress points.  Reuses existing aubio data.
- **C4. Designed thumbnails** — currently auto-extracted mid-frame.
  Replace with hook-text designed 1080×1920 PNG per video.
- **C5. Audio mastering** — Suno raw → ffmpeg `loudnorm` + light EQ /
  compression so the track sits at YT Shorts loudness target.

**Estimated visual-delta-per-time leaders** (operator can pick highest
leverage when home):

- **A1** (~half day, visible delta on every future render)
- **C1** (~5 min/video, applies to 8 existing videos retroactively)
- **C4** (design decision + automation, CTR-direct impact)

**New-active-goal candidates** (would re-set `docs/goal.md`):

- **B2** (short-film trailer mission) — new mission type, ~3–5 day
  scope.
- **A2** (mini-narrative B-roll) — keyword-gen rewrite + 1 day for
  pipeline + 1 day for prompt iteration.

**Dependencies**: none blocking — these are all pure pipeline / ffmpeg
/ prompt work.  Cluster B2 is the only one that needs operator design
input (the script-writing prompt + tone).

**Estimated cost**: per-cluster as listed.  No Anthropic spend at
runtime; cluster A/C all stay Tier-2.  B2's script generation could
use the existing `FACELESS_SCRIPT_OVERRIDE` Sonnet opt-in for quality
(operationally negligible against Max quota).

**Status**: brainstorm parked.  Operator scheduled to revisit when
home (~23:00 KST 2026-05-18).  Roadmap "Now" carries a pointer to
this entry until then.

---

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
