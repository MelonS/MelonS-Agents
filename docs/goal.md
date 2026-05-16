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

### 2026-05-16 | Clone-and-go reproducibility | _in progress_

_(2026-05-16 — operator direction: "다른 사람이 저장소를 클론해서
나와 같은 방식으로 사용이 가능해야 한다.  결과물까지 뽑을 수 있게.")_

_One sentence_: a stranger pulling the public repo from GitHub
should reach a passing mission output (a real 9:16 short under
their own machine's `records/`) using only the README, with no
follow-up questions to the maintainer.

**Subgoals (acceptance signals)**:

- [ ] **`.env.example` is host-agnostic** — no `/Users/<name>/...`
      absolute paths, no Apple-Silicon-only Homebrew paths baked
      as defaults.  Values resolve via `command -v` /
      `${HOME}/...` or are clearly marked "set this to your local
      binary".  Each var has a one-line comment explaining purpose
      and where to obtain the underlying tool.
- [ ] **Prerequisite installer / checker** — a single script
      (extend `bootstrap.sh` or add `scripts/check-prereqs.sh`)
      that on first run: detects missing tools (ffmpeg, ollama,
      whisper.cpp, yt-dlp), prints the *exact* brew / apt / port
      command needed on the host OS, and either auto-installs
      via brew (with user confirmation) or exits with a clear
      pointer.  No silent failure on a missing binary.
- [ ] **Whisper.cpp model auto-fetch** — first mission run sees
      that `$WHISPER_MODEL` is missing and downloads
      `ggml-small.bin` (≈ 150 MB) into the configured path, or
      fails with a single clear "run `scripts/fetch-whisper-model.sh`"
      pointer.  Stranger never has to know the upstream URL
      themselves.
- [ ] **README "Prerequisites" section** — short, factual block
      before "Quick start".  Lists: macOS 14+ (primary) or
      Linux (best-effort), Homebrew, Apple Silicon for
      `h264_videotoolbox` (`-allow_sw 1` falls back to libx264
      otherwise), ~2 GB free disk for models + fixtures.  HTTPS
      clone URL alongside the SSH one.  No "13년차" / personal
      claims (§12).
- [ ] **Fresh-clone simulation evidence** — a script
      (`scripts/test-fresh-clone.sh` or similar) clones the repo
      into a temp dir, runs `cp .env.example .env` + bootstrap +
      one highlight mission against a Blender CC-BY-3.0 fixture,
      asserts a `short.mp4` exists under the temp records dir,
      and writes a one-line PASS/FAIL log to
      `docs/onboarding/fresh-clone-log.txt`.  The log is the
      deliverable artifact — durable evidence the clone-to-output
      path works on a clean tree, not just on the maintainer's
      machine.
- [ ] **Honest cross-platform note** — a "Platform support"
      paragraph in README that says exactly what works where.
      macOS primary; Linux works for missions but launchd-based
      schedulers are macOS-only (cron equivalent listed).  The
      `say`-based fixture-generation in `bootstrap.sh` is
      macOS-only; document the espeak fallback or mark it
      explicitly as "skip on Linux, use real CC fixtures
      instead".  No promises that aren't tested.

**Done when**: subgoals 1–6 all checked AND
`docs/onboarding/fresh-clone-log.txt` exists with a PASS verdict
from a temp-dir clone-and-run.

**Why the deliverable matters**: this file's lesson contract
(infrastructure-vs-outcome) says infra subgoals alone don't
complete a goal.  "Clone-and-go" reproducibility is exactly the
kind of claim that the existing portability paragraph in README
makes without evidence; the deliverable forces that claim to be
verified against a real fresh tree on disk.

#### Past-goal reference (read-only)

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
      `docs/caption-verify/highlight-015213-sintel-cap.jpg`.

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

## Next goal (candidate — for user to confirm or replace)

_(promoted to Active goal below by user direction "일단 다하고 있어봐"
 at 2026-05-16 02:40 KST, after operator confirmed satisfaction with
 the first deliverable.)_

---

## Past goals

_Migrated from "Active goal" on housekeeping pass; most recent first._

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
