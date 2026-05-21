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
+ [`docs/onboarding/demo-mode.md`](onboarding/demo-mode.md).
Merged to main in `v0.2.0`.

**Field observation addendum (2026-05-19 ~14:00 KST)** — the
same security professional ran the demo on a fresh clone in
person at a follow-up meeting.  The clone-and-render path
worked.  But a second wall emerged that the test gate didn't
catch: **per-tool Claude Code permission prompts**.  The
project's tracked `.claude/settings.json` has a 70-entry allow
list and renders correctly via `install-claude-local.sh` — but
Claude Code also consults the **user-level**
`~/.claude/settings.json` at session start and on first
directory trust, and the project file alone wasn't sufficient
to suppress all prompts.  Friend's experience: ~30 individual
"Allow this command?" dialogs during a single demo run.
Operator's framing: "다 하나씩 승인하기에는 너무 장벽이커.. 첨에
권한관련해서도 승인하면 어느정도 넘어가게 되어야 할듯"
(approving each individually is too much friction; one consent
at the start should cover the rest).

The follow-on fix is one script:
[`scripts/install-claude-permissions.sh`](../scripts/install-claude-permissions.sh).
It reads the rendered project allow list, asks operator once
("merge these into your user-level settings? Y/n"), and on
consent merges them into `~/.claude/settings.json` — append +
dedupe only, never mutating the user's existing deny list,
validating the JSON before overwrite, writing a
`_notes.melons_agents` provenance block.  bootstrap.sh invokes
it in interactive mode with a TTY check so CI runs don't hang.

The lesson is one the test gate missed:
**reproducibility tests verify "the script ran".  They don't
verify "a human's onboarding experience was tolerable".**
test-demo-mode.sh ran fine because it skipped Claude Code
entirely — it executed bash directly.  A real first-time user
running the same flow through Claude Code hit ~30 prompts
that the test scenario never saw.  The fix and the test gate
are now both shipped, but the field observation came from a
human, not the gate.

Field-observation artifact:
[`scripts/install-claude-permissions.sh`](../scripts/install-claude-permissions.sh)
+ [`docs/onboarding/claude-permissions.md`](onboarding/claude-permissions.md).
On `feat/permission-bootstrap`, pending merge after the next
round of testing.

---

## 7. Default treatment mis-fits the genre — declarative preset routing as additive scaffold

**Problem.** The v6 music-video pipeline applies a single default
treatment to every output: drum-onset zoom-pulse (a 0.6 s Gaussian
scale bell at every detected drum hit) + film grain intensity 8 +
vignette PI/5 + cuts every 12 beats.  Operator-reported failure mode
on 2026-05-20: "어떤 곡은 화면이 갑자기 띠용하는 쉐이더가 나오면
이상해보임" — the zoom-pulse reads as out-of-place on a 5-short
batch where two of the songs (lo-fi rain, minimal ambient) belong to
genres whose visual contract *forbids* glitch.

Per-short diagnosis (`docs/research/2026-05-21-shader-song-mismatch-
diagnosis.md`):

| # | Genre | Violation |
|---|---|---|
| 1 Rain | lo-fi hip-hop | zoom-pulse forbidden (lo-fi is anti-glitch) |
| 2 Linen | minimal ambient | cuts forbidden + zoom-pulse forbidden |
| 3 Arcade | synthwave | wrong filter family (grain instead of scanlines) |
| 4 Coastline | tropical house | heavy grain forbidden |
| 5 Noir | jazz | missing halation, borderline zoom-pulse |

Two of five (#1, #2) were severe — the "띠용" the operator flagged.
None of the per-short failures was a bug in the pipeline; the bug
was applying *any* one default across all genres.  A genre's
forbidden-effect set is a real visual contract — Lofi Girl, Chillhop,
and Ambient Worlds have spent years training their audiences on a
specific visual code, and a render that violates that code reads as
"wrong genre" even when every other detail is correct.

**Constraint.** Existing pipeline is in production (`scripts/daily-
music-video.sh` runs via cron / launchd cadence; 8 prior shorts on
YouTube; not to be broken).  No new dependency that requires `pip
install` or `brew install` of anything operator hasn't already
accepted.  Logic changes to `agents/*.md` need explicit operator OK;
script-level changes don't (see `operator-contract.md` §5, §
infra-maintenance).

**Decision.** Land the fix as a fully **additive scaffold**: a new
declarative YAML preset table + a wrapper script that exports env
vars to the existing `run.sh`, leaving the original entry point
untouched.  Backwards-compatible by construction — the default code
path is identical until the operator opts in via either a wrapper
flag or `MUSIC_VIDEO_GENRE=<name>`.

The mechanism:

- `skills/music-video/data/genre-presets.yaml` — 14-genre table.
  Each preset declares `phrase_beats`, `grain_intensity`,
  `vignette_angle`, `zoom_pulse_amp`, `shader`, `cut_mode`,
  `lut_direction`, `forbidden_effects`, plus a `keyword_pool` (default
  Pexels queries) and a `phrase_pool` (default kinetic typography
  phrases).  Sourced from a cross-references rule table that
  synthesizes industry-practice + synesthesia research (`docs/
  research/2026-05-21-music-shorts-formats-landscape.md`).

- `scripts/music-video-shaders.sh` — 6 new genre-coded effects
  (`scanline`, `chromatic_split`, `neon_edge`, `vhs`,
  `saturation_pulse`, `kaleidoscope`) alongside the existing 4
  classic effects (`pond`, `breathing`, `halation`, `combo`).
  Smoke-tested 10/10.

- `scripts/music-video-stillzoom.sh` — image + music → 60s slow
  Ken-Burns 9:16 mp4.  Required for genres whose contract forbids
  *any* cut (ambient, classical, dreamcore).

- `scripts/music-video-genre.sh` — wrapper that resolves a genre
  name (or alias) to preset, exports the env overrides, runs the
  unchanged `agents/missions/music-video/run.sh`, chains the matching
  post-shader; auto-routes stillzoom-mode genres to the stillzoom
  entry point.

- `scripts/music-video-auto.sh` — top-level all-in-one: detect genre
  from filename (+ID3) via `genre-detect.sh`, auto-fetch a Pexels
  still for stillzoom genres without `--image`, run genre wrapper,
  optionally chain Canvas 8s loop + kinetic typography + audio-
  reactive grading variants.  Operator's intended entry point.

- `scripts/music-video-bulk-regenerate.sh` — one-command re-render
  of the diagnosed-mismatched 5/20 batch with correct presets.

- `skills/music-video/tests/genre-aware-smoke.sh` — 16-assertion
  smoke test covering genre detection, alias resolution (regression
  for the yq v4 substring bug below), all 10 shaders, Canvas spec
  compliance, typography overlay, and full pipeline end-to-end.

**Outcome.** Twenty commits over ~3 hours autonomous overnight,
all on `main`, all pushed, smoke 16/16 passing.  Operator's "띠용"
complaint maps to a documented diagnosis + a fix that took a fresh
re-render of the worst-case Linen ambient short from "12 cuts + drum-
onset zoom-pulses on a 90 BPM ambient track" to "single still image
+ slow zoom + warm halation + zero cuts" — visually it is a different
short, structurally it is the same channel + same music + same UC
channel ID.  Five v2 mp4s staged at `outputs/publish/2026-05-21-
regen-v2/` ready for operator-approved re-upload via the existing
`scripts/yt-batch-upload.sh outputs/publish/upload-meta-v2/`.

**Two latent bugs caught during the run** (regression-tested in the
smoke):

1. `MUSIC_VIDEO_VIGNETTE_ANGLE=off` didn't disable vignette — run.sh
   used `${VAR:-PI/5}` which falls back on empty.  Patched to
   case-match `none`/`off`/empty → disabled.  Without this, synthwave
   preset still baked in a vignette, violating the genre contract.
2. `yq v4 contains()` does *substring* matching on strings — "jazz"
   falsely matched drone's aliases `[doom_jazz]`, causing the
   wrapper to resolve jazz to drone preset (stillzoom + no image →
   fail).  Patched to explicit `map(. == $g) | any`.  Caught only
   because the Noir track is the one of the 5 that *has* a genre
   name that's a substring of another genre's alias.

**Why this fits the pattern of the other case studies.** Like the
demo-mode path (case study #6), the fix was structurally additive —
a parallel mechanism that pre-populates the same checkpoints the
existing code already inspects.  Like the audit layers (#4), it
landed as scripts in version control with no DB or external state.
Like the cost-routing rule (#1), it answered a specific observed
failure ("이 곡은 안 맞아 보임") with the minimum mechanism that
solves it (a YAML preset table + a wrapper) rather than rewriting
the pipeline.

---

## 8. Intervention as the unmeasured axis — autonomy signal + reduction levers

**Problem.** A multi-agent system that *needs* constant human steering
hasn't actually escaped the effort it was meant to replace.  But "how
much human steering does this system need?" was never measured: the
project had a `docs/metrics/intervention.png` chart added on
2026-05-17, and one README rewrite later (`aa10ba0`, 2026-05-18
music-video-first refresh) it had been silently dropped from the
README.  By 2026-05-22 the underlying data was 2 days stale and
nobody had noticed.  An axis you don't measure is one that drifts.

**Constraint.** The signal had to be honest, multi-dimensional, and
private:

- **Honest** — agent must not be able to game the score by, e.g.,
  squashing 10 commits into one to lower the "user-initiated count".
  Solution: tie each commit to a per-line classifier that reads commit
  body for explicit user-direction markers (`Requested-by: user`,
  "Operator surfaced", verbatim Korean quotes).
- **Multi-dimensional** — a commit count alone misses the operator's
  *time* engaged.  A high-leverage day (autonomous overnight) and a
  high-touch day (live coding session) can both produce 10 commits.
  Need to capture session minutes too.
- **Private** — session JSONLs at `~/.claude/projects/-Users-melons-ai/`
  contain the operator's verbatim prompts, often with personal
  context.  The mining script must keep these local and never
  upload — only aggregate counts land in the committed JSON.

**Decision.** Two-source two-panel signal updated daily without
operator action:

- **Panel A** — `git log` commit attribution.  Per-day count of
  user-initiated vs agent-autonomous + ratio + leverage
  (`agent/max(1,user)`) + longest autonomous gap (h).
- **Panel B** — Local Claude Code session JSONL mining.  Per-day
  operator-prompt count (text-content user messages only; excludes
  `tool_result` auto-replies) + active session minutes (capped at
  60min per session to prevent idle laptops from inflating the
  signal).
- Chart regenerates daily 02:00 KST via
  `com.melons.agents.intervention-chart` launchd job.

Once the signal was honest, a companion reduction memo
(`docs/research/2026-05-22-intervention-reduction.md`) enumerated
5 prioritized **levers** to *act on* the trend:

1. Classifier false-positive scrub — **invalidated** after spot-check
   of 5 flagged commits showed all were legitimately user-initiated.
   Discipline lesson: a hypothesis that looks correct can collapse
   under one round of verification.
2. Default to recommended option (`[[minimize-intervention]]`) —
   memorized rule, ongoing enforcement.
3. **Batch taste reviews** — `outputs/review-queue/` + three scripts
   (`review-queue-add.sh` / `-digest.sh` / `-decide.sh`).  New renders
   auto-enqueue from `agents/missions/music-video/run.sh`; operator
   drains a contact-sheet markdown on their cadence instead of being
   pinged per-render.  10× fewer intervention events, same total
   decision count.
4. **Statusline absorbs status pings** — `scripts/statusline.sh`
   reads `scripts/doctor.sh --json` (60s background-regen cache) and
   the goal-lock skill's progress count.  Operator sees
   `doctor:✓/⚠N/✗N · goal:N/M · audit⚠` continuously, removing the
   "what's the state?" prompt class.  Companion: `actionable_warn`
   classification so opt-in env-key gaps don't inflate the count.
5. Permission bootstrap — already shipped in v0.3.0
   (`feat/permission-bootstrap`, ~30 prompt-per-session reduction
   for fresh-clone first sessions).

Plus an **autonomous-decisions log** (`docs/autonomous-decisions.md`
+ `scripts/log-decision.sh`) — when the agent makes a unilateral
decision during overnight work, it appends a one-liner.  Operator
wakes up, reads one page in <60s, understands what was decided and
*what was decided not to do* (lever dismissals recorded, so the same
hypothesis doesn't get re-explored next session).

**Artifact.**

- `docs/metrics/intervention.png` — the two-panel chart.
- `docs/metrics/intervention.json` — per-day raw data (`user_initiated`,
  `agent_autonomous`, `user_ratio_pct`, `leverage_ratio`,
  `longest_autonomous_gap_h`, `operator_prompts`,
  `active_session_minutes`, `session_count`).
- `scripts/generate-intervention-chart.py` — classifier + miner.
- `scripts/intervention-chart-collect.sh` — runner with venv
  bootstrap for matplotlib.
- `scripts/com.melons.agents.intervention-chart.plist.template` —
  launchd daily 02:00 KST.
- `docs/research/2026-05-22-intervention-reduction.md` — the lever
  memo with priority + status per lever.
- `docs/autonomous-decisions.md` + `scripts/log-decision.sh` — the
  one-page wake-up summary.
- `outputs/review-queue/` + 3 scripts — batched taste-decision queue.
- `scripts/statusline.sh` + `scripts/doctor.sh` actionable_warn —
  status absorbed into the always-visible UI.

**Result (9-day window, 2026-05-14 → 2026-05-22 partial):** median
user-ratio ≈ 19%, range 0%–69% (5/17 spike was the day chart + site +
scorecard first landed — heavy taste-call density).  Best-leverage
day was 2026-05-20 (7.7×, 11% ratio) — an autonomous overnight
shipping Skill #2 v0.4.0.  2026-05-22 partial (through 03:00 KST) is
on track to beat that: 8% ratio, 11.5× leverage, 9 operator prompts
across 8 sessions for ~99 active minutes — the bulk of the work in
this case study itself ran inside that signal.

Honest disclosure: this is one operator's daily signal, not a
statistical study.  But it's *the* honest signal — better noise than
no signal.

Why this case study matters separately from #4 (the audit): the
audit measures **does the system match the contract?**.  This
measures **does the operator have to be in the loop to make the
system work?**.  Different question, different mechanism.  Both
needed.  Like the cost-routing rule (#1), the answer was the
minimum mechanism — a chart and a memo, not a framework — and
each lever is independent (drop or swap any of them without
breaking the others).

---

## 9. The quality-bar wasn't a bug — it was 6 contracts the system didn't enforce

**Problem.** After uploading 12 vocal-track shorts in the 2026-05-21
overnight batch, the operator reviewed them and stated six quality
directives 2026-05-22 ~01:30 KST.  None of them were bugs.  All were
*contracts the renders were silently violating*:

| # | Contract | Failure mode |
|---|----------|--------------|
| 1 | Don't reuse B-roll across shorts | Repeat viewers recognize footage |
| 2 | Shaders should be restrained, not blanket | "도배되는 느낌" (slathered feel) |
| 3 | Lyric overlay should sync to vocal cue (±200 ms) | Lines arrive 1-3s late |
| 4 | Shader vocabulary is too narrow at 15 effects | Every short looks the same |
| 5 | Korean lyric → Korean person on screen | Mixed-ethnicity B-roll |
| 6 | English lyric → global subjects, no CJK signage | Tokyo neon on US pop |

Each is observable by a single viewer, but only emerges at the
*channel* layer — a sequence of shorts read collectively, not in
isolation.  No individual render was broken.

**Constraint.** Five contracts, ≤3 hours of session budget per the
session-as-a-cost-budget pattern from case #1.  The operator's bias
(per [[minimize-intervention]] memory) is that every directive should
ship as code, not as a future TODO list.

**Decision.** Decompose into five small phases — A.1 (dedup
registry), A.2 (whisper alignment), A.3 (lang anchor), C.1 (shader
restraint), B.1 (vocab expand) — each ≤1 hour, each shippable to
`origin/main` independently.  Each is an MVP with deferred refinement
queued in `docs/roadmap.md` "suggest" comments so the operator can
review and shape the next iteration.

Per directive:

- **A.1 — B-roll dedup**: shared registry at `records/youtube/broll-
  used.txt` (gitignored).  Both Pexels caller paths (the dedicated
  `scripts/pexels-fetch.sh` and the inline curl in
  `agents/missions/music-video/run.sh`) consult the registry before
  picking and append after download.  196 prior-Pexels-IDs seeded by
  walking `records/missions/*/resources/clips/*.json`.

- **A.2 — Lyric onset alignment**: derived LRC via whisper.cpp.
  For Korean, word-level (`-sow -ml 1 -ojf`) + character-by-character
  SequenceMatcher (handles whisper.cpp's occasional split of multi-
  byte CJK into invalid UTF-8 segments via `errors='replace'`).  For
  English, segment-level for better aggregation against lyric lines.
  Confidence scored per line; sub-floor lines explicitly marked as
  autofilled rather than producing fake-precise timing.

- **A.3 — Lang anchor**: new `lang_anchor: ko|en|mixed|neutral`
  field on every preset.  At runtime, every 4th segment of a vocal-
  anchored render uses a person-anchored keyword from the appropriate
  pool (Seoul cafe / NYC daylight / Tokyo aesthetic); scenery
  keywords still come from the genre's pool.  Pollinations.ai prompt
  template augmented for stillzoom-mode renders.

- **C.1 — Shader restraint**: new `shader_active_ratio: 0.0..1.0`
  field, default per-genre (1.0 ambient, 0.35 kpop_ballad).  When
  ratio < 1.0, the shaded output is blended back toward the un-
  shaded original via a final ffmpeg blend pass.  MVP uses uniform
  attenuation; time-windowed gating (shader fires only on beats)
  queued.

- **B.1 — Shader vocab**: catalog expanded from 15 → 23 effects.
  Stage-1 (`light_leak`, `duotone`, `vignette_pulse`) for broad
  applicability.  Stage-2 (`paper_grain`, `dust_speck`, `posterize`)
  for texture.  Stage-3 (`trail_echo`, `soft_bloom`) for temporal /
  quieter atmosphere.

**Cost.** ~2.5 hours of active work for all five phases + research
docs + roadmap updates + a morning brief.  Render of one demo
end-to-end through the integrated pipeline confirmed all five MVPs
compose without conflict.

**Lesson.** When the operator hands you a list of quality complaints
that don't map to bugs, the right read is that those are *contracts
the system isn't currently enforcing*.  The shape of the fix is then
mechanical — a contract per phase, expressed as a single field added
to the data layer + a single mechanism added to the runtime.  No
abstraction layer, no plugin architecture, no "extensibility for
future contracts" — just the literal six contracts, one at a time.
The minimum-mechanism rule from #1, applied at the quality layer.

The two ffmpeg traps documented in passing (`pow(x,2)` not `x^2`;
uppercase `T` not `t` for time inside `geq`) cost ~15 min of debug
total, mostly because the error messages from `geq` are unhelpful
("Undefined constant" rather than "use uppercase T").  Future shader
authors can read the research doc and skip the trap.

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
