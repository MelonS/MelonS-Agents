# Engineering case studies

[한국어](engineering-case-studies.ko.md) | **English**

Each entry records a decision that surfaced from a concrete production
problem — not a design picked off a whiteboard. Format: **problem →
constraint → decision → artifact (file + commit)**. Read top to bottom
for the order they hit the pipeline, or scan headings for the topic.

---

## 1. Quality ceiling on the default-local LLM — Tier-1/Tier-2 routing

**Problem.** The first stable faceless-short pipeline ran every stage on
local models, including narration script generation via `llama3.2:3b`
(later `qwen2.5:7b`). The mechanical stages were fine. Script quality
plateaued: encyclopedia-style prose, weak 5-second hook, conflated
close-but-distinct facts (e.g., hydrogen body composition by mass vs by
atom count — different numbers, presented in one breath as if the same
fact).

**Constraint.** The project rule "Tier 2 (local) = default" was meant to
make the pipeline reproducible without external dependencies. Treating
that rule as universal produced a quality ceiling on the one stage
where local couldn't compete. Paying per-token at scale was also out —
high-volume stages would burn the budget instantly.

**Decision.** Split the rule by stage volume and quality leverage.
**Mechanical, high-volume stages** (transcribe, render, B-roll fetch)
stay local — token cost would dominate at scale. **One-shot creative
stages** (script hook, factual framing) route to Sonnet — ~500 tokens
per call, operationally negligible against the Max-plan subscription
quota, and quality compounds over the next 60 seconds of viewing. The
routing rule is now explicit in `docs/cost-model.md`; deviations from
"local default" require a stated reason on this axis.

**Artifact.**
- `scripts/gen-script-claude.sh` — Sonnet-only script generation
- `docs/cost-model.md` § "When Tier 2 is the wrong default"
- Commit `d205b15` (Sonnet routing + v6 trial)

**Score impact.** Hittites EN: v5 → v6 lifted the Hook axis 3 → 9 and
Factual axis 4 → 9 on the same B-roll, same TTS, same render —
isolating the script stage as the regression source.

---

## 2. Naive parallelization OOMs the host — semaphore-bounded batch

**Problem.** Running multiple `faceless-short/run.sh` invocations
sequentially was slow (~5 min per render); naively backgrounding them
all in a shell pipeline OOMed the host. Each mission peaks ffmpeg +
whisper.cpp + Ollama simultaneously; inferred 100% concurrent peak is
~16 GB working memory plus Metal GPU pressure on an M2 16 GB.

**Constraint.** No `wait -n` on macOS bash 3.2 (the default shell).
Couldn't introduce a Python or external scheduler — would add a runtime
dependency for what is fundamentally a small bash control loop.

**Decision.** A job-count semaphore polled via `jobs -r | wc -l`, bash
3.2-compatible, no external tools. `MAX_PARALLEL=1` by default
(sequential — the safe choice for an M2), `=2` flagged as OK on an
M2 16 GB+ without other GPU load, `=3+` prints an explicit
high-risk warning. Per-job rc/elapsed/start/end recorded to a TSV
summary so retry/triage is grep-friendly rather than scrollback.

**Artifact.**
- `scripts/batch-faceless.sh` — the throttled runner
- `records/batch-faceless/summary-<ts>.tsv` — per-run summary
- Commit `4b5bbee`

**Operational note.** The first music-trial batch run still exited
after one mp4 — root cause was an interaction between `set -uo
pipefail` and a child pipe, not the semaphore. Filed in
`docs/pilots/music-trial/README.md` as a follow-up; the remaining
renders were completed in foreground sequential mode as a workaround.

---

## 3. LLM drafts could still ship below threshold — content-quality feedback loop

**Problem.** Routing to Sonnet (case #1) lifted the average. It did
not eliminate drift — some drafts still mixed factual frames or
opened on a weak verb. The pipeline had no quality gate between
script generation and downstream TTS / B-roll / render: a bad script
would propagate through and only be caught by the scorecard *after*
the mp4 was rendered.

**Constraint.** A QA step that requires the operator to read every
draft kills the throughput gain Sonnet bought. A QA step that uses
the same model that wrote the draft has obvious self-grading risk.

**Decision.** Score with a separate Sonnet call (different prompt,
different role — "evaluator under-rate rather than inflate"), output
strict JSON on two axes (hook strength, factual coherence — the two
axes that v5→v6 lifted from 3 to 9 each, i.e. the two the LLM
controls). If both axes ≥ threshold (default 7), accept. If not,
regenerate with **axis-specific feedback** prepended to the prompt,
up to N retries (default 2). Every attempt is appended to a JSONL
scoring log next to the script for reproducibility.

**Artifact.**
- `scripts/score-content.sh` — strict-JSON scorer with code-fence stripping
- `scripts/gen-script-claude.sh` — retry loop with axis-specific feedback
- `<out>.scoring.log` — attempt trail
- Commit `2217bce`

**Verification.** AutoTune script scored 9/9 (accepted first attempt).
Hydrogen v5 (pre-fix retro-scored) was caught at 2/3 with the exact
"10% vs 60%" frame collision called out in the reasoning field — i.e.,
the scorer can surface the failure mode the operator originally
flagged.

---

## 4. Drift detected days later was expensive — three-layer reactive audit

**Problem.** A nightly auditor (launchd at 03:00) caught documentation
drift, contract-breaking edits, and stale roadmap entries — but
sometimes 18–24 hours after the drift landed. By then, downstream
work had already built on the drifted state, multiplying the cost
to fix.

**Constraint.** A long-running observer process is overkill — most of
the day there's nothing to observe. Polling every 30 seconds wastes
tokens (and the project has a money firewall). The observer pattern
in its classical form assumes long-lived subjects; subagents in this
repo are short-lived processes triggered on demand, not subjects in
that sense.

**Decision.** Replace "Observer" with **Reactor + Hook** — files as
events, three trigger layers:

- **L1 — post-commit hook (~30 s response).** Fires
  `audit-run.sh contract` only when the commit touched a drift-risk
  path (`agents/`, `.claude/agents/`, `config/`, `CLAUDE.md`, the
  operator contract). Nothing fires for unrelated changes.
- **L2 — 15-min mission-anomaly poll.** Looks for new blocker files
  or QA-FAIL bursts. No-op (zero tokens) when nothing is wrong. First
  run seeds state without firing, so existing blockers don't trigger
  a false alert.
- **L3 — daily 03:00 baseline.** launchd fires the full sweep.
  Catches anything L1 + L2 missed (e.g., drift that landed before
  the hook was installed).

The three layers are independent — any one going down still leaves
two trigger paths.

**Artifact.**
- `scripts/hooks/post-commit.sh` + `scripts/install-hooks.sh` (L1)
- `scripts/audit-poll.sh` + `scripts/com.melons.agents.audit-poll.plist` (L2)
- `scripts/audit-run.sh` + existing launchd job (L3)
- Commits `785dafd` (L1), `de71875` (L2)

**Verification.** L1 correctly fired on `7c6ff4f` (operator-contract edit)
and `764f3f0` (`.claude/agents/` edit), correctly did not fire on
`ac8c02a` (scorecard data, not in risk regex). L2 first-run seed
captured 3 existing blockers without firing.

---

## 5. Shader effects in ffmpeg — knowing where the wall is

**Problem.** Operator asked for shader-style effects on the music-video
output: water-surface ripple, breathing zoom, warm halation, and
cel-shading (cartoon). The base pipeline already does grain + vignette
+ glitch zoom-pulse via ffmpeg filters; the question was how far the
ffmpeg-only path scales before a real shader stack (GLSL / mpv +
libplacebo / GPU compute) becomes mandatory.

**Constraint.** The pipeline is a single ffmpeg pass. No GLSL toolchain,
no second renderer, no AI stylization service. Anything that ships has
to be expressible as a filter graph the same `ffmpeg` binary can read.
The operator's quality bar was binary per effect: either the result
looked atmospheric and intentional, or it got cut.

**Decision.** Three effects landed. One was deferred.

1. **Pond surface (`displace` + procedural `geq` wave maps).** First
   attempt was a discrete "drop" — three radial bulge pulses at phrase
   boundaries via time-gated `scale` expressions. Operator reaction
   was "먼가 떨어지긴하는데 좁쌀만함" — the drops were visible but
   felt like specks, not water. The reframe: don't simulate a discrete
   drop, simulate the whole frame as a *pond surface*. Reimplemented
   as `displace` with two animated grayscale maps (X and Y), each a
   three-component sin wave field generated by `geq` at 540×960 (4×
   faster than full-res) then scaled to 1080×1920 before feeding into
   `displace=edge=smear`. Max ±13 px (~1.2 % of width) — visible
   across the whole image but not jarring. Operator confirmed "완전
   잘되고".

2. **Breathing zoom — the libx264 stride-mismatch bug.** First attempt:
   `scale=w='1080*(1+0.015*sin(2*PI*t/5))'` for a continuous ±1.5 %
   wave. Crashed libx264 at 3.5 seconds with `Input picture width
   (1080) is greater than stride (1072)`. Cause: when `sin` went
   negative, the multiplier dropped below 1.0, so `scale` produced
   frames *smaller* than the crop target (1080×1920). The subsequent
   `crop=1080:1920` tried to read 1080 pixels from a 1064-wide image
   and the codec choked. Fix was to reformulate the wave as
   `(0.5 + 0.5*sin(...))` so the multiplier was always ≥ 1.0 —
   the frame always upscales relative to the crop, never downscales.
   Lesson: any time-varying `scale` in front of a fixed-size `crop`
   has to be one-sided.

3. **Halation — bright-bloom screen-blend.** Split the source, brighten-
   threshold + 22 px `gblur` the copy, screen-blend back onto the
   original at 0.30 opacity. ~60 lines of filter graph, no expression
   magic. Worked on the first try ("확실히 티남"). The implementation
   detail worth noting: the `blend=all_expr='A + (255-A)*B/255 * OPACITY_EXPR'`
   form lets the opacity be a time-varying expression — that's the
   building block for the phrase-aware combo below.

4. **Cel-shading (cartoon) — knowing where to stop.** This is the
   case-study payload. Two attempts failed and the failure mode is
   instructive. First attempt: `bilateral` filter for color
   flattening + `eq` saturation boost + `edgedetect` for outlines +
   `multiply` blend. Result was "사람이 반만 심슨 된거 같은데" —
   shading variation was still present, no real cartoon look. Root
   cause: no posterize step. Second attempt added `lutyuv` with
   independent luma / chroma quantization (`round(val/51)*51` for
   luma, `round(val/64)*64` for U and V). Result was "완전 그냥
   초록색만 나옴" — everything turned green. Root cause: quantizing
   luma and chroma on separate quantization grids causes the U/V
   distribution to collapse into a small set of stepped pairs that
   no longer encode the source hue distribution faithfully — most
   pixels land on the same (U, V) pair, which in YUV space
   corresponds to a single hue. Real cel-shading needs RGB-space
   posterize (or HSV-space with luma preserved as a soft signal) and
   thick anti-aliased outlines — neither expressible cleanly inside
   `lutyuv` or `geq` at 1080p in real time.

   The decision was **not** to ship a third attempt with more knobs.
   The honest read of the failure modes is that ffmpeg's
   filter primitives don't compose into cel-shading without
   accepting an obviously broken artifact. Real cel-shading lives in
   one of three places: GLSL shaders (mpv + libplacebo, the project
   would pick up ~200-500 lines of shader code and an mpv-render
   adapter), EbSynth (paint one keyframe, propagate by motion —
   bypasses procedural shading entirely), or AI stylization (Stable
   Diffusion + AnimateDiff, ComfyUI, RunwayML). All three are real
   tools used by people who ship video at production quality; none
   of them fit inside the music-video mission's single-ffmpeg-pass
   architecture. So cartoon is parked as a separate R&D branch
   rather than half-implemented as a fourth ffmpeg variant that
   would have to be defended in a review.

**The phrase-aware combo** (the deliverable that *did* ship — `combo`
mode of `scripts/music-video-shaders.sh`) glued pond + halation
together with envelopes tied to a 95.8 BPM reference cadence. Pond
amplitude is multiplied by a `clip()`-based gate that is 0 during
intro (0–15 s), ramps to 1 over the build (15–22.5 s), holds full
through the climax (22.5–45 s), then tapers back. Halation opacity
follows a similar curve, 0.10 → 0.35 → 0.20. The whole thing is one
`ffmpeg -filter_complex` invocation; the envelopes live inside
filter expressions, not in a Python frame-stitcher.

**Decision artifact:** [`scripts/music-video-shaders.sh`](../scripts/music-video-shaders.sh)
(committed in `23832fa`) with all four effects + the deferred-cartoon
docstring explaining what doesn't fit.

**Lesson preserved:** Half-implementing a shader effect because the
tool *almost* gets there is worse than not shipping it. The
ffmpeg-can-do-everything assumption is fine until it isn't, and the
honest move is to name the wall and route to the right tool — even
if that tool isn't in the project yet.

---

## 6. Onboarding friction kills first-touch — zero-account demo path

**Problem.** A security professional reviewing the repo at a
coffee-shop session walked through the README "Quick start" cold.
Three hard stops before the music-video mission ever ran:

1. `PEXELS_API_KEY` required.  Pexels signup forces Google /
   Apple / Facebook OAuth — no email path.  KR users on
   Naver / Kakao primary have no usable OAuth provider; the
   identity-correlation surface is non-trivial even for users
   who do have a Google account.
2. "Get API key" UI is buried in the Pexels dashboard behind two
   menu hops most users will not find without external
   instructions.
3. The music-video mission needs an operator-supplied music file.
   The canonical source is Suno — manual six-step round-trip
   (signup → custom-mode prompt → wait → pick best of N →
   download mp3 → drop in `assets/music/`).  No Suno API exists;
   every track is a separate UI session.

Cumulative bail rate before first output ≈ high.  And:
first-time users editing `.env` with API keys IS the typical
credential-leak vector (GitHub's auto-revoke logs thousands of
key-in-commit incidents per day).  A demo path that never opens
the `.env` removes that attack surface entirely.

**Constraint.** Can't break the existing full path — operators
who *have* a Suno track and Pexels key still want per-keyword
mood-matched B-roll.  Can't violate copyright policy — every
source needs a real allowlist entry and a deduplicated
attribution credit in `outputs/SOURCES.txt`.  Can't add Anthropic
spend — this is the zero-account *demo*, not the curated path.

**Decision.** Add a parallel demo path that pre-populates the
same `$CLIPS_DIR/raw-<keyword>.mp4` paths the mission already
checks for, before the per-segment Pexels fetch loop runs.  The
existing `if [[ ! -f "$RAW" ]]` check short-circuits the API call
without any new code in the hot path.

The mechanism:

- `scripts/fetch-demo-broll.sh` — curated CC-BY-3.0 clips from
  Blender Foundation's CDN.  HEAD-checked URLs, sidecar JSONs
  matching the shape `agents/lib/attribution.sh` already reads
  for Pexels.
- `scripts/fetch-demo-music.sh` — curated CC-BY-4.0 tracks from
  Kevin MacLeod's Incompetech catalog.  Five moods across the
  keyword categories the mission understands.  Required adding
  `incompetech.com` to `config/copyright-allowlist.yaml` plus a
  CC-BY-4.0 publish_rule that was missing.
- `MUSIC_VIDEO_DEMO_MODE=1` env switch in
  `agents/missions/music-video/run.sh` — skips
  `require_env PEXELS_API_KEY`, defaults `MUSIC_FILE` to the
  first cached demo track when no argument given, pre-populates
  `$CLIPS_DIR` from the demo cache.  Twenty new lines in the
  hot path; the existing non-demo flow is byte-identical.
- `scripts/bootstrap.sh` UX — detects the
  no-key-AND-no-music state and prints the exact
  `MUSIC_VIDEO_DEMO_MODE=1 …` command as the recommended Next
  Step instead of two warning blocks.

**Verification.** `scripts/test-demo-mode.sh` exercises the
whole path against a freshly-cloned tree: `git clone` →
`bootstrap.sh` → `MUSIC_VIDEO_DEMO_MODE=1 ./run.sh demo` →
assert `short.mp4` ≥ 1 MB, duration ≥ 50 s, `SOURCES.txt`
contains ≥ 2 CC-BY credit lines.  First PASS recorded
2026-05-19 01:25 KST against the local feat branch: 81 MB,
60 s, 3 deduplicated credit lines.  Cold-start wall time clone
→ playable mp4 ≈ 2 min 30 s on the test machine.

**Lesson preserved:** "Make the friction-heavy path opt-in for
the advanced case, not the default."  The full Pexels + Suno
flow is still there — documented as the upgrade path for users
who commit to the system.  But it's no longer the gatekeeper.
The existing infrastructure (allowlist + sidecar attribution +
filesystem-cache short-circuit) was *almost* designed for this
already; the demo-mode change was mostly composition of pieces
that already worked, not new mechanism.

**Decision artifact:** [`scripts/fetch-demo-broll.sh`](../scripts/fetch-demo-broll.sh)
+ [`scripts/fetch-demo-music.sh`](../scripts/fetch-demo-music.sh)
+ [`scripts/test-demo-mode.sh`](../scripts/test-demo-mode.sh)
+ [`docs/onboarding/demo-mode.md`](onboarding/demo-mode.md).  All
on `feat/demo-mode`, pending merge to main at the v0.2.0
milestone.

---

## What these have in common

- Each started from a **specific observed failure**, not a theoretical
  concern.
- Each fix was **the minimum mechanism that solved the failure**, not a
  generalized framework.
- Each leaves an **artifact a future operator can inspect** — a script,
  a config, or a doc the agent system itself reads.
- Each is **reversible** — bash scripts in version control, no
  external state, no DB migrations to walk back.

The cost-routing rule, the throttler, the feedback loop, and the
audit layers are independent — you could adopt any one without the
others. They cohere because they all answer the same underlying
question: *what is the minimum mechanism that lets one operator run
this pipeline without becoming the bottleneck?*
