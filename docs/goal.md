# Goal — current outcome target

**This file is the outcome layer.**  It is *not* a work queue (that's
`docs/roadmap.md`).  An empty work queue does **not** mean the goal is
achieved.  The goal is achieved only when a concrete deliverable
exists and matches the "Done when" criteria below.

> **Session-start contract**: every conversation that asks for work
> reads this file **first**, before `docs/roadmap.md`.  If the
> current goal here is unmet, the agent's job is to advance it, not
> to drain the roadmap.  See
> [`CLAUDE.md`](../CLAUDE.md#session-start-protocol-read-this-first)
> and [`operator-contract.md` §9](operator-contract.md).

---

## Active goal

_(no active goal — operator sets the next one.  Previous goal
"Production-ready music-video short" achieved 2026-05-17 ~23:30
KST and migrated to Past goals below.  Optional 24h post-publish
metrics capture in
[`docs/pilots/first-upload-metrics.md`](pilots/first-upload-metrics.md)
remains open and is operator-actioned, not a new goal.)_

### Candidate active goal — pending operator confirmation (2026-05-18 ~19:50 KST)

**Generalize this repo as a multi-skill AI assistant framework**,
with music-video production refactored into "Skill #1" and a
job-hunt support tool built as "Skill #2".  Operator stated the
vision verbatim at ~19:50 KST: 사람들을 돕기 위해, 나의 취직을
위해, 다른 사람들의 취직을 위해, 모두의 먹고 사는 문제를
해결하기 위해.  Connects to:

- Existing parked `domain-pivot portability` analysis in
  [`docs/ideas.md`](ideas.md) (2026-05-16) which had already noted
  *"job-app automation → same repo (ollama + curl + playwright
  reuse, all-text assets)"* — that idea is what's now being
  promoted from parking.
- The "problem solver" job-category framing surfaced at the
  ~19:30 KST coffee-shop session — both personal need + observed
  market need (peers losing jobs in 2026 economy).
- External-security-review insight (~19:00 KST): zero-friction
  onboarding (current roadmap Next #1) is even more important if
  the repo is meant to serve *other people in similar straits*,
  not just the operator.

**Architectural direction (operator-stated, corrected
2026-05-18 ~20:10 KST)**: convert pipelines into **Skills following
the open [agentskills.io](https://agentskills.io) standard**.
Operator's reference point is the
[Nous Research Hermes Agent](https://github.com/NousResearch/hermes-agent)
project's skill model, not Anthropic's Claude Code Skills feature
(the two are related — both implement the same agentskills.io
open standard — but Hermes is the project the operator wanted me
to study).  An initial research pass at ~19:55 KST mistakenly
focused on Claude Code Skills; a follow-on fetch on Hermes
landed at ~20:10 KST.  Synthesis below combines both, since the
shared standard means a Skill written once can target multiple
runtimes.

#### Hermes Agent — research synthesis (2026-05-18 ~20:10 KST)

**Project**: `github.com/NousResearch/hermes-agent` —
"The agent that grows with you", Python primary (88%) +
TypeScript (8.8%), v0.14.0 released 2026-05-16 (very recent),
extremely active (4k+ open issues, 5k+ PRs).

**Core philosophy** — distinct from Claude Code:
- **Tools** = 40+ built-in capabilities (terminal, file I/O, API)
- **Skills** = *procedural memory* — autonomous reusable
  workflows that **the agent itself creates from experience**
  and refines over time.  Self-improving.
- **Agents** = subagents spawned for parallel workstreams.

The key Hermes innovation: skills are **agent-curated and
self-improving during use**, vs Claude Code's
operator-curated-static model.  A Hermes skill watches the
operator do something repeatedly, then proposes formalizing
that pattern into a callable skill; subsequent runs let the
agent refine the skill.

**Repo structure**:
```
hermes-agent/
├── skills/                     ← shipped/core skills
├── optional-skills/            ← non-core add-ons
├── agent/                      ← agent runtime
├── acp_registry/, acp_adapter/ ← ACP (Agent Comm Protocol) bits
└── ~/.hermes/skills/openclaw-imports/  (per-user runtime, migrated skills)
```

Per-user / per-config files in markdown:
- `SOUL.md` — agent persona
- `MEMORY.md` — durable agent memory
- `USER.md` — user profile

**Invocation**: slash commands (`/<skill-name>`) work in both CLI
and messaging platforms; auto-detection via `/skills` browser.
Hermes-specific: skills can also be **triggered by agent
introspection** ("I've seen this pattern before, should I make
a skill?").

**Distribution — this is the big one**:
[Skills Hub at agentskills.io](https://agentskills.io) is a
**vendor-neutral marketplace** for skills following the open
standard.  Skills written to this spec are portable between
Hermes / Claude Code / any other compliant runtime.  This is a
material upgrade over Claude Code's repo-based-only distribution.

#### Cross-compatibility insight (the strategic angle)

Both Hermes and Claude Code implement `agentskills.io` — meaning
**a Skill written to the open standard works in both runtimes**.
For the operator's "helping people" vision this is leverage:
- Build Skill #1 (music-shorts) once → runs in Claude Code
  (existing harness) AND Hermes (Python-heavier alt) AND any
  future agentskills.io-compliant agent.
- Publish to the agentskills.io marketplace → discoverable by
  users who use *neither* of our specific harnesses but want
  the music-shorts capability.
- Hermes-specific "self-improvement" is **optional**: a static
  Skill is valid spec; we can add the evolution loop later if
  it makes sense for our specific skills.

**Trade-off**:
- Claude Code Skill (simpler, our existing harness) = ~1 day,
  works for our users only.
- agentskills.io-compliant Skill = +0.5 day extra spec
  conformance, works in Hermes too, listable on Skills Hub.
- Recommended: go agentskills.io-compliant from day one.  Extra
  effort is minimal and unlocks a real distribution channel.

#### Bash pipeline → Skill conversion (unchanged from prior pass)

Concrete file structure (target):
```
.claude/skills/music-video-pipeline/      ← Claude Code location
└── SKILL.md
└── scripts/run.sh                         ← existing bash, unchanged
```
OR Hermes location at `~/.hermes/skills/music-video-pipeline/`,
same files.  Operator picks distribution path.

SKILL.md is the metadata wrapper:
- `description` + `when_to_use` (controls auto-invocation)
- `allowed-tools` (per-skill permission grant)
- `context: fork` for heavy pipelines (keeps main convo clean)
- body = invocation prompt that calls `scripts/run.sh`

The bash itself stays untouched in either case.

#### Step-by-step action plan (when operator confirms this goal)

**Operator feedback 2026-05-18 ~20:20 KST**: "몇일은 너무 오래
걸리는거 같은데... 단계별로 해야 할듯".  Re-decomposed into
shippable ~1-3 hour units.  Each step is "stop-able" — operator
can pause after any step and still have something working.

##### Skill #1 — Music-shorts (rough total ~6-8h split into 4 steps)

- **Step 1.1** (~1h) **Spec study + SKILL.md draft**.  Read
  agentskills.io spec, identify required vs optional fields,
  write an empty SKILL.md template at
  `.claude/skills/music-video-pipeline/SKILL.md` with TODO
  placeholders.  **Stop-here payoff**: we know exactly what
  fields are needed; placeholder file committed.
- **Step 1.2** (~2h) **Wrap existing bash in SKILL.md**.  Fill
  the placeholder fields: `description`, `when_to_use`,
  `allowed-tools`, `context: fork`, body invokes
  `${CLAUDE_SKILL_DIR}/scripts/run.sh` (symlink or copy of
  existing `agents/missions/music-video/run.sh`).  **Stop-here
  payoff**: Skill #1 exists, invocable via `/music-video-pipeline`.
- **Step 1.3** (~1h) **Test in Claude Code** (our existing
  harness).  Invoke the skill, verify it produces a 60s 9:16
  mp4, compare against existing mission output.  **Stop-here
  payoff**: confirmed our existing music-shorts pipeline runs
  through the Skill interface.
- **Step 1.4** (~2h) **Validate cross-compat in Hermes**.
  Clone hermes-agent, drop the skill in
  `~/.hermes/skills/music-video-pipeline/`, run.  Document any
  spec-conformance fixes needed.  **Stop-here payoff**: skill
  works in both runtimes → confirms open-standard claim →
  ready for Skills Hub.

##### Skill Hub publication (~2-3h, optional after Skill #1)

- **Step P.1** (~1-2h) **Marketplace listing prep**.  Write
  description, attribution (Suno music license, Pexels API
  license), example invocation, screenshots / demo gif.  Use
  existing `docs/demo/music-video-velvet1-jazz-combo-preview.gif`.
- **Step P.2** (~1h) **Publish to agentskills.io**.  Submit
  Skill #1.  **Stop-here payoff**: first public Skills Hub
  artifact under operator's identity.

##### Skill #2 — Job-hunt support (rough total 1-2 days split into 6 small steps)

Decomposed so each step delivers a useful artifact on its own.
Operator can stop after any step and have a working tool.

- **Step 2.1** (~2-3h) **ONE-site scraper — Wanted only**.
  `scripts/scrape-jobs-wanted.sh` fetches today's posts under
  one keyword (e.g., "AI engineer").  Saves to
  `records/jobs/<date>/wanted.json`.  Cron-able.  **Stop-here
  payoff**: daily Wanted snapshot, even without LLM filter.
- **Step 2.2** (~2h) **LLM filter**.  Take yesterday's snapshot,
  pass each posting through local ollama (`llama3.2:3b`) with a
  prompt that classifies it as "fits problem-solver profile /
  doesn't fit".  Output: filtered subset.  **Stop-here payoff**:
  noise reduction, even if from one site.
- **Step 2.3** (~1-2h) **Dedupe + persistence**.  Cross-day
  dedupe by job-id; persist seen-IDs in `records/jobs/seen.json`.
  **Stop-here payoff**: new postings only, no daily repeat.
- **Step 2.4** (~1-2h) **Daily digest markdown**.  Format
  filtered new postings as a daily markdown digest with title /
  company / fit-rationale / apply-link.  Write to
  `records/jobs/<date>/digest.md`.  **Stop-here payoff**:
  operator gets a useful daily morning digest.
- **Step 2.5** (~2-3h) **Second site (LinkedIn or JobPlanet)**.
  Same scraper shape, different selector.  Merge into digest.
  **Stop-here payoff**: multi-source coverage.
- **Step 2.6** (~2h) **Skill-ify**.  Wrap the whole pipeline in
  `~/.claude/skills/job-hunt-digest/` with SKILL.md.  Now
  invocable as `/job-hunt-digest` in any Claude Code session.
  **Stop-here payoff**: Skill #2 callable + shareable.

##### Public-release narrative (~2-3h, after both skills)

- **Step F.1** (~1h) README EN+KO reframing — repo identity from
  "music-shorts agent" → "multi-skill framework, music + job-hunt
  shipped".
- **Step F.2** (~1-2h) Publish Skill #2 to Skills Hub + write
  short blog-post-style narrative explaining the framework for
  external users.

##### Cumulative estimates

| Block | Steps | Total time |
|---|---|---|
| Skill #1 (music) | 1.1 → 1.4 | ~6-8h (1 session day) |
| Skills Hub | P.1 → P.2 | ~2-3h |
| Skill #2 (job-hunt) | 2.1 → 2.6 | ~10-14h (2 session days split) |
| Public release | F.1 → F.2 | ~2-3h |
| **Total** | 14 steps | **~20-28h** (vs prior 5.5-7.5 days)|

The TOTAL hour count is similar to before, but each step is
**stop-able**.  After Step 1.3 operator can pause days/weeks if
needed and the music-shorts skill is already useful.  After Step
2.1 the job scraper is daily-useful even without LLM filter.

##### Order flexibility

Operator can also reorder:
- **Music-first** (1.1→1.4 → P.1→P.2 → 2.1→2.6 → F.1→F.2) =
  ship Skill #1 fully before starting job-hunt
- **Job-hunt-first** (2.1→2.6 → 1.1→1.4 → P.1→P.2 → F.1→F.2) =
  ship the personal job-hunt tool first (operator's most
  immediate need)
- **Parallel** = interleave depending on motivation/blockers

Recommendation: **Job-hunt-first** — operator's immediate need
is more acute (active job search), and Skill #2 is the harder
one so derisking earlier is wise.  Skill #1 conversion is
trivial enough that it can land any time.


**Skill roadmap (operator-stated order)**:

1. **Skill #1 — Music-shorts production** (refactor of current
   pipeline).  Current state: working bash pipeline producing
   uploadable 60-second 9:16 shorts.  Skill conversion preserves
   the bash but adds a SKILL.md / canonical invocation surface.
2. **Skill #2 — Job-hunt support tool**.  New build.  Scope TBD
   pending operator design pass.  Probable shape: AI-era job-
   posting aggregator (Wanted / JobPlanet / LinkedIn / Lever /
   Greenhouse) + LLM filter for "AI integration / forward
   deployed / problem solver" pattern + daily digest.

**Not in scope yet** (operator may add):
- Additional skills beyond #1 and #2.
- Multi-machine / cloud deployment of the framework.
- (Publication to agentskills.io Skills Hub IS in scope — see
  Phase 3 above.  Removed from this exclusion list after
  2026-05-18 ~20:10 KST research revealed Hermes uses the open
  standard with an actual marketplace.)

**Why this isn't promoted to Active goal yet**:
- Per goal.md maintenance contract, agent does not silently
  promote.  Operator confirms.
- Hermes Agent + Claude Skills research landed at 2026-05-18
  ~20:10 KST (see synthesis above).  Technical feasibility is
  established (Skill #1 ~1 day, Skill #2 ~3-5 days, total
  5.5-7.5 days for 4 phases).  Promotion now blocked only on
  operator explicit OK ("활성 goal로 박아").

**Open questions for the home (~23:00 KST) session**:
- Same repo (skill folder structure) vs new repo for Skill #2?
- Skill #2 = "AI-era job aggregator" or different shape?
- Music-shorts work continuation parallel to Skill #1 conversion,
  or pause shorts until conversion done?
- Naming: is this repo's identity changing from `MelonS-Agents`
  to something framework-shaped?  (Probably no rename — additive
  evolution.)

---

## Next goal (candidate — for user to confirm or replace)

_(promoted to Active goal below by user direction "일단 다하고 있어봐"
 at 2026-05-16 02:40 KST, after operator confirmed satisfaction with
 the first deliverable.)_

---

## Past goals

_Migrated from "Active goal" on housekeeping pass; most recent first._

### 2026-05-17 | Production-ready music-video short — 업로드 가능한 1편 | **ACHIEVED 2026-05-17 ~23:30 KST**

> _Resolution note_: operator approved
> `outputs/publish/03e-velvet1-jazz-combo.mp4` ("좋아 너무 좋음" +
> "올려도 될정도긴 하네"), then uploaded to their existing YouTube
> channel (4 subscribers, <2k total views — well under the 1k subs +
> 4k watch-hour monetization threshold, so the Suno free-tier
> personal-use license fits cleanly).  Upload confirmed published
> at 2026-05-17 ~23:30 KST.  Channel-level performance stats
> (impressions, watch-time, retention) to be captured 24h+ later in
> [`docs/pilots/first-upload-metrics.md`](pilots/first-upload-metrics.md).
> 
> _Production trail_:
> 1. Mission render: `music-video-upload1-203521` (60 s, 71 MB).
> 2. Shader combo: pond + halation with phrase-aware envelope
>    (95.8 BPM × 7.5 s phrase cadence; 22.5–45 s climax window) —
>    `scripts/music-video-shaders.sh combo`, commit `23832fa`.
> 3. Music: `Velvet Turntable1.mp3` (Suno free tier, generated
>    2026-05-17 afternoon, license trail in
>    [`assets/music/SOURCES.md`](../assets/music/SOURCES.md)).
> 4. Upload metadata template + delivered to operator at ~23:00 KST.

_(2026-05-17 ~20:00 KST — operator direction after the pilot A/B
goal closed.  Operator literal: "실제로 올릴수있는 정도의 쇼츠를
만들어 내야함. 계속 테스트만 하는건 의미없음."  Cost-pressure
context stated in the same turn: SSD purchase deferred, paid Suno
deferred, disk pressure remains.)_

_One sentence_: produce **at least one** music-video short that the
operator approves as "이대로 올려도 됨" (would upload as-is) — not
"pilot-quality", not "close enough", an actual upload candidate for
YouTube Shorts.

**Constraints derived from operator turn**:

- **Cost-minimal mode**: no SSD purchase, no Suno paid tier, no new
  paid services.  Music sources limited to:
  - YouTube Audio Library (free, license-clean for YT Shorts uploads)
  - Pixabay Music (free, attribution-recommended)
  - Operator-supplied files (already validated path)
- **Disk-pressure mode**: 34 GB free as of 20:00 KST after manual
  cleanup pass.  Every mission must release its `resources/` intermediates
  immediately on completion until SSD lands.
- **No more test-iteration shorts**: each render is either upload-candidate
  or it gets deleted.  Decision-log entries replace mp4 archives.

**Subgoals (acceptance signals)**:

- [x] **Music source resolved** — at least one license-clean track
      sitting in `assets/music/` (gitignored), with source + license URL
      noted in a tracked `assets/music/SOURCES.md`.  Acceptance: a
      track exists locally that the operator confirms is OK to publish
      a video under.
- [x] **Pre-flight format polish** — v6 effects already integrated into
      `agents/missions/music-video/run.sh`; remaining knob-tuning (if any)
      driven by operator review of the next render, not pre-emptive
      iteration.  Acceptance: no open "needs format change" item in
      `docs/pilots/decision-log.md` between render and operator review.
- [x] **Deliverable** — one mp4 in a known publish-ready path
      (`outputs/publish/` or equivalent — not buried under
      `records/missions/<date>/<id>/outputs/`) that the operator
      explicitly approves with "이거 올려" or equivalent.  The mp4
      must remain on disk after the next cleanup pass.
- [ ] **(Optional, but the real goal)** — that mp4 lands on a
      YouTube Shorts channel, 24h watch-time + impression counts captured
      in `docs/pilots/first-upload-metrics.md`.  Not strictly required
      to close the goal (the operator may want to hand-upload), but the
      goal only fully "matters" when this happens.

**Done when**: the deliverable subgoal is checked — operator approves
a specific mp4 and it sits in the publish-ready path.

**Why this goal now**: 73 commits of infrastructure + 5 music-video
prototype iterations landed in one day and the operator's reaction is
"테스트만 하는건 의미없음" — the gap between "system can produce this"
and "operator would publish this" is the unmet outcome.  Closing it
also unlocks the next decision (양산 batch vs single-launch) on real
performance data instead of taste.

**Not in scope for this goal**:
- Music-video scorecard (deferred — operator approval is the only score
  that matters until first upload).
- 양산 batch (5–10 shorts) — only meaningful after one shipped piece
  validates the format/music/license stack.
- External SSD purchase (operator decision; not a blocker).
- Channel creation / YT account setup automation (operator action).

### 2026-05-16 | Faceless pilot A/B — science vs Bible-history | **ACHIEVED 2026-05-17**

> _Resolution note_: the operator's pick (logged 2026-05-17, see
> [`decision-log.md`](pilots/decision-log.md#operator-pick--2026-05-17))
> was format option 3 from the decision-log's list — **iterate the
> format itself**, not commit to one of the two original topics.  The
> chosen format is the new `music-video` mission (music-as-primary-audio,
> phrase-aligned cuts, onset-aligned glitches, no narration / no
> captions) shipped as `agents/missions/music-video/run.sh` in commit
> `828070f`.  The Hittites/Hydrogen pilots remain as historical
> artifacts of the format A/B; their topics are not the production
> niche.


_(2026-05-16 evening — operator picked "make one of each and decide
on the actual output" after long-form discussion about niche
selection.  Project frame was clarified: not a portfolio demo, but
a real shorts-account growth attempt — final output and platform
performance are the deliverable.  Pilot phase precedes the real
account work to validate niche fit on observable artifacts.)_

_One sentence_: produce one 60-second faceless short for each of
two candidate niches — science (Hydrogen, English) and Bible-history
(The Hittites, English, neutral-documentary tone) — using a new
`faceless-short` mission template, so the operator can pick the
niche from real watchable output rather than abstract preference.

**Subgoals (acceptance signals)**:

- [x] **`faceless-short` mission template shipped** —
      `agents/missions/faceless-short/run.sh` + `agents/lib/tts.sh`
      with Kokoro-ONNX (Apache 2.0, commercial-safe) as primary
      backend and macOS `say` fallback.  Script-driven pipeline
      (input = topic + tone): ollama generates 60 s script → Kokoro
      TTS narrates (am_michael voice) → whisper.cpp transcribes the
      TTS audio for SRT → ollama extracts visual search terms →
      `pexels-fetch.sh` pulls 6 B-roll clips → ffmpeg trims, concats,
      letterbox-blurs to 9:16, burns captions and attribution.  No
      paid APIs used.  Shipped `1663301` (and pilot-time fixes for
      mapfile + Kokoro wiring, see next commit).
- [x] **Pilot 1 deliverable — Hittites short** — 9:16 mp4 in two
      language variants (v5 after operator feedback on caption
      overlap; v5 adds single-line caption enforcement via
      `scripts/split-long-captions.py`.  Script + B-roll reused from
      v4 so the only delta is caption rendering):
      - EN: 62.7 s — `records/missions/2026-05-17/faceless-hittites-032538/outputs/short.mp4`
            ([thumbnail](pilots/screens/hittites-en-caption-verify.jpg)).
      - KO: 60.3 s — `records/missions/2026-05-17/faceless-hittites-ko-032653/outputs/short.mp4`
            ([thumbnail](pilots/screens/hittites-ko-caption-verify.jpg)) —
            macOS Yuna voice, AppleGothic captions, per-language window
            keywords (each language picks its own visuals).
      Neutral documentary tone; hook = "biblical kingdom dismissed
      as legend until 1906".  Production notes + A/B table in
      [`docs/pilots/decision-log.md`](pilots/decision-log.md#pilot-1--hittites-history--bible).
- [x] **Pilot 2 deliverable — Hydrogen short** — 9:16 mp4 in two
      language variants:
      - EN: 59.7 s — `records/missions/2026-05-17/faceless-hydrogen-032742/outputs/short.mp4`
            ([thumbnail](pilots/screens/hydrogen-en-caption-verify.jpg)).
      - KO: 38.9 s — `records/missions/2026-05-17/faceless-hydrogen-ko-032846/outputs/short.mp4`
            ([thumbnail](pilots/screens/hydrogen-ko-caption-verify.jpg)).
      Curiosity tone; hook = "75 percent of the universe and 10
      percent of your body".  v4 window 5 in KO landed `sugar bottle`
      for the caption "약 1킬로그램, 큰 설탕 한 봉지" — exact metaphor
      match.  Production notes in
      [`docs/pilots/decision-log.md`](pilots/decision-log.md#pilot-2--hydrogen-science).
- [x] **Operator decision logged** — `docs/pilots/decision-log.md`
      "Operator pick — 2026-05-17" section.  Pick = format option 3
      (iterate the format).  New mission `agents/missions/music-video/run.sh`
      shipped 2026-05-17 (commit `828070f`).  Validated against five
      prototype renders (v1 → v5 on Velvet Turntable lo-fi), with v5
      explicitly confirmed by the operator ("대만족").  Reasoning
      documented in decision-log.

**Done when**: both pilots committed AND `decision-log.md` shows
the operator's pick.

**Why pilot before commit**: niche selection on paper kept stalling
between "science (background expertise) vs history (consumer pattern)".
Real artifact in hand resolves the deadlock.  Pilot output also
de-risks the larger faceless infrastructure investment — if the
output quality at $0 cost path is unwatchable, the whole faceless
approach gets reconsidered before the 80-episode series commitment.

**Tier 1 / Tier 2 stance for pilot**: $0 Tier 2 only — ollama,
say, whisper, ffmpeg, Pexels API (free tier).  KlingAI / ElevenLabs
intentionally deferred to post-pilot to keep the niche-test cheap.

**Not in scope for this goal**: character generation (the
periodic-elements 정령 캐릭터 idea from earlier in the day), real
account creation, real upload + performance tracking.  Those land
after a niche is chosen.


### 2026-05-16 | Clone-and-go reproducibility | **ACHIEVED**

_(2026-05-16 14:00 KST — fresh-clone test ran from
`https://github.com/MelonS/MelonS-Agents.git` HTTPS into a temp dir
and produced `short.mp4` (7 MB) on attempt 1.  Total elapsed
clone → PASS: ~30 s.)_

_One sentence_: a stranger pulling the public repo from GitHub
reaches a passing mission output (a real 9:16 short under their
own machine's `records/`) using only the README, with no follow-up
questions to the maintainer.

**Subgoals (all ticked)**:

- [x] **`.env.example` is host-agnostic** — every `*_BIN` var blank
      by default with install hint comments; `agents/lib/env.sh`
      resolves blanks via `command -v` (and, for ffmpeg, prefers a
      libass-enabled binary by checking the ffmpeg-full keg too).
      Shipped `692c755`.
- [x] **Prerequisite checker** — `scripts/bootstrap.sh` rewritten:
      checks all five required CLIs (ffmpeg, ffprobe, whisper-cli,
      ollama, yt-dlp), prints OS-specific install hints for missing
      ones (`brew install ffmpeg-full` on macOS, `apt install
      ffmpeg` on Linux, etc.), verifies ffmpeg has libass,
      auto-fetches the whisper model + ollama highlight model,
      exits non-zero on missing pieces.  Shipped `692c755`.
- [x] **Whisper.cpp model auto-fetch** —
      `scripts/fetch-whisper-model.sh` downloads
      `ggml-<variant>.bin` from huggingface.co/ggerganov/whisper.cpp
      idempotently.  Bootstrap calls it when `$WHISPER_MODEL` is
      missing.  Shipped `692c755`.
- [x] **README "Prerequisites" + HTTPS clone** — new block before
      Quick start lists macOS / Linux options, Homebrew, Apple
      Silicon vs libx264 fallback, ~3 GB disk.  Quick start shows
      both HTTPS and SSH clone URLs.  Mirrored in `README.ko.md`.
      Shipped `692c755`.
- [x] **Honest Platform support note** — replaced the thin
      "Portability" paragraph with a 4-row table showing exactly
      what works on macOS vs Linux: mission execution (both),
      `h264_videotoolbox` (macOS only, falls back), `bootstrap.sh`
      `say`-fixtures (macOS only), `launchd` schedulers (macOS only;
      systemd / cron suggested for Linux).  Mirrored in
      `README.ko.md`.  Shipped `692c755`.
- [x] **Deliverable — fresh-clone simulator + PASS evidence** —
      `scripts/test-fresh-clone.sh` clones the public repo into
      a temp dir, runs bootstrap + one highlight mission against
      the Sintel CC-BY-3.0 trailer, asserts a `short.mp4` ≥ 1 MB,
      appends a PASS / FAIL line to
      [`docs/onboarding/fresh-clone-log.txt`](onboarding/fresh-clone-log.txt).
      First run logged FAIL (uncovered the libass packaging gotcha
      below); second run after the env.sh fix logged PASS with a
      7 MB short.mp4 produced in ~30 s.  Shipped `6349039`.

**Real defect uncovered + fixed inside this goal**:

Homebrew silently split `ffmpeg` into a regular formula (no longer
includes libass) and `ffmpeg-full` (keg-only, includes libass plus
many others).  `brew install ffmpeg` on a stranger's machine now
produces a build that can't burn captions — the project's caption
pipeline would silently fail without diagnosis.

Two fixes shipped:
1. `agents/lib/env.sh` walks a candidate list and prefers any
   ffmpeg whose `-version` mentions libass; falls back to the
   ffmpeg-full keg path (`/opt/homebrew/opt/ffmpeg-full/bin/ffmpeg`
   on macOS) when PATH ffmpeg lacks libass.  No `.env` edit needed
   after `brew install ffmpeg-full`.
2. `scripts/bootstrap.sh` libass-missing hint upgraded to point at
   `brew install ffmpeg-full` specifically, and `.env.example`
   install comment updated to match.

**Done when**: subgoals 1–6 all checked AND
`docs/onboarding/fresh-clone-log.txt` exists with a PASS verdict
from a temp-dir clone-and-run.  Both met on 2026-05-16 14:00 KST.

**Lesson preserved**: deliverable subgoals catch real defects.  This
goal's six subgoals would have passed pure-infra inspection
(.env.example clean, scripts present, README updated) without
revealing the Homebrew libass split.  Only running the *actual*
clone-to-output sequence on a clean tree surfaced the gap.  Every
future goal in this file gets a deliverable subgoal that exercises
the full path, not just the boundary.

### 2026-05-16 (later) | Real Korean celebrity video + visual bug pass | **ACHIEVED**

_(2026-05-16 03:25 KST — operator direction at 02:54 asked for a
real-celebrity Korean speaker on camera; iterated through several
Wikimedia Commons candidates before settling on a CC-BY-3.0 sports
interview clip.  The run sequence surfaced four real defects, all
fixed within this same goal.)_

**Deliverable**: `highlight-032405` — real CC-BY-3.0 Korean
interview clip → 9:16 60s short, QA PASS, top-left Korean
attribution rendering correctly, captions clean of `\,` escape
leak, real Korean speech burned in.  Caption-verify frame:
[`docs/caption-verify/highlight-032405-son-heungmin-cap.jpg`](caption-verify/highlight-032405-son-heungmin-cap.jpg).

**Defects fixed inside this goal**:
1. STRIP_NONSPEECH was over-aggressive — stripped `[FOREIGN]` /
   `[INAUDIBLE]` markers that signal real-but-untranscribed speech.
   Switched to whitelist (music / applause / laughter / silence / etc.).
2. QA duration check was zero-tolerance — `60.0029s` from
   h264_videotoolbox GOP alignment failed `<= 60`. Added
   `QA_DUR_TOLERANCE_S=0.5` default.
3. Visual bugs in attribution overlay: comma escape leaked as `\,`
   in captions (escape_ass wrongly escaped the text field); Korean
   glyphs rendered as boxes (Helvetica.ttc → AppleSDGothicNeo.ttc →
   finally AppleGothic.ttf single-font .ttf; also caught that `.env`
   had a shadowing Helvetica path that defeated code-level defaults).
4. Whisper auto-detect mis-classified Korean as `[FOREIGN]`. Added
   `WHISPER_LANG` env override.

**Lesson preserved**: env-var defaults via `:=` in code are silently
shadowed by `.env` values; when changing a default, update both code
AND `.env.example`. The Helvetica → AppleGothic fix took three runs
because the local `.env` had the old path baked in.

### 2026-05-16 | Mixed validation pass | **ACHIEVED**

_(2026-05-16 02:51 KST — all three deliverable subgoals cleared in
one pass; commits `df71bd6` → `c778bbe` and the runs that followed.
Total elapsed from goal set → all done: ~11 minutes.)_

_One sentence_: after the first real-CC short (`highlight-015213`)
satisfied the operator, exercise the remaining surface of the v1
pipeline — Korean input, multi-output shorts-batch, summarize text —
each against a real CC source, with the caption-quality improvement
the operator flagged ("[MUSIC] lines are noise") applied along the way.

**Subgoals (acceptance signals)**:
- [x] `STRIP_NONSPEECH` env var implemented in `agents/lib/ffmpeg.sh`,
      default ON — caption lines matching `^\[.*\]$` filtered before
      SRT/ASS render.  Old behavior available via `STRIP_NONSPEECH=false`.
      Shipped `df71bd6`.
- [x] **Deliverable**: Korean highlight short — `highlight-024629`
      (`/tmp/smoke/ko_lecture.mp4` → 9:16 49s short.mp4 PASS attempt 1,
      whisper.cpp multilingual auto-detected Korean, captions burned in
      Hangul, top-left attribution overlay rendered,
      `docs/caption-verify/highlight-024629-ko-lecture-cap.jpg` committed).
      Note: visual content is synthetic SMPTE bars (the fixture's
      nature, not a pipeline issue) — the Korean audio + Korean caption
      pipeline is what was validated.
- [x] **Deliverable**: Real-CC shorts-batch — `shorts-batch-024840`
      (Sintel trailer 720p → 2 shorts: short-01 44s/8.4MB +
      short-02 36s/7.2MB, both 1080×1920, QA PASS attempt 1; Blender
      attribution on both via shared SOURCES.txt + burned-in
      `source-attribution.txt` per short.  With a 52s source and N=2
      the two windows overlap — a constraint of the trailer's length,
      not a bug.  Caption-verify frames committed for both shorts.)
- [x] **Deliverable**: Real-CC summarize — `summarize-025121`
      (Sintel trailer 1080p → `summary.md` 551 bytes with TL;DR +
      3 Key points + EN original paragraph + KO mirror paragraph +
      Source & license footer; QA PASS attempt 1).  Pipeline-side OK;
      the KO mirror text quality is bottlenecked by `llama3.2:3b`
      (mixed Korean/English tail — small-model limit, not a pipeline
      bug).  Larger local model (e.g. `qwen2.5:7b-instruct`) would
      resolve; that's a model-swap decision separate from this goal.

**Done when**: all three deliverable subgoals checked AND each has a
committed artifact path recorded here.

### 2026-05-15 → 2026-05-16 | Alien aesthetic 탈출 | **ACHIEVED**

_(2026-05-16 02:00 KST — operator confirmed satisfaction after
viewing `highlight-015213` short.mp4.  Preserved in full because the
lesson it produced is load-bearing for this file's maintenance
contract.  Future achievements get a one-line entry unless they also
produced a generalizable lesson.)_

_One sentence_: produce a 9:16 short from a real Creative-Commons
source video, end-to-end, with burned-in source attribution + libass
captions and a passing QA verdict — proving the v1 pipeline can
output something that doesn't look like placeholder content.

**Subgoals (acceptance signals)**:
- [x] Fixture catalog with real CC sources (Blender / Xiph / Pexels /
      archive.org / wikimedia) — shipped over `8ae9449`, `3b9175d`.
- [x] Standard 9:16 layout engine with safe-zone margins and
      semi-transparent caption box — shipped `8ae9449`.
- [x] Source-attribution wired across all three mission types
      (highlight / summarize / shorts-batch) — shipped `0eaaee2`.
- [x] libass burned captions with correct font scale (fixed the 6.67×
      PlayRes bug) — shipped `3decfa7`.
- [x] Copyright filter v1 (domain allowlist + publish gate + strike
      log + license-string probe) — shipped `28dda8f` → `e530302`.
- [x] **Deliverable**: at least one real-CC mission output reaches
      QA PASS end-to-end → `highlight-015213` on 2026-05-16 01:52 KST
      (commit `6ae9da0`).  Sintel trailer 1080p → 39s 9:16 short.mp4,
      PASS attempt 1, watermark + captions visible in
      [`docs/caption-verify/highlight-015213-sintel-cap.jpg`](caption-verify/highlight-015213-sintel-cap.jpg).

**Done when**: a single real-CC mission output emerges with QA PASS
*and* visual verification (caption-verify frame committed).  Reached
on 2026-05-16 01:52 KST.

**Lesson written from this goal**: the infrastructure-vs-outcome gap.
Subgoals 1–5 were all marked done before the deliverable (subgoal 6)
ever existed.  Without an outcome subgoal that says "a real artifact
must exist," a goal can read 5/5 done and still have produced nothing.
Every future goal in this file must include at least one **deliverable
subgoal** — a file that has to exist, a verdict that has to be PASS,
a frame that has to be committed.  Subgoals describing infrastructure
("X is implemented") never on their own complete a goal.

---

## Abandoned

_(none.)_

---

## Maintenance contract

- **Set by the user.**  The active goal section is user-edited.  The
  agent reads it and uses it; it does not silently overwrite the
  goal.  When tonight's events warrant a goal change, the agent
  proposes via `<!-- suggest -->` HTML comment, same convention as
  roadmap Now.
- **Checked at every session start.**  Per `CLAUDE.md` session-start
  protocol, the first read of any work-asking conversation is this
  file.  Roadmap comes second.
- **Subgoal checkboxes** are append-only progress markers; the agent
  ticks one when the relevant commit lands, but does not invent new
  subgoals without user OK.
- **Migration**: when an active goal is achieved, the agent moves it
  to "Past goals" intact, writes a one-line lesson if one is worth
  preserving (as above), and leaves "Active goal" empty until the
  user sets the next one.  Empty active goal in the morning is a
  signal for the user, not a license for the agent to invent goals.
