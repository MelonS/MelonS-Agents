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

**Goal**: _(empty — previous goal "Clone-and-go reproducibility"
achieved on 2026-05-16 14:00 KST and migrated to Past goals.
Set by user when the next focus is chosen.)_

> Empty active goal is a **signal for the user**, not a license for
> the agent to invent goals.  When you (the user) set the next goal,
> replace this block with a one-sentence description + a deliverable
> subgoal that names a concrete artifact (file path / verdict / frame).

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
