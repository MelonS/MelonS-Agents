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

**Goal**: _(set by user; one sentence describing what success looks like)_

Below is the first goal recorded under this layer.  Future goals
replace this section; achieved goals move to "Past goals", abandoned
goals to "Abandoned" with a one-line reason.

### 2026-05-15 → 2026-05-16 | Alien aesthetic 탈출 | **ACHIEVED**

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

_(empty — awaiting user direction.  Candidates that surfaced
overnight: see `docs/proposals/2026-05-15-auditor-active.md` for the
auditor self-documenting edits awaiting user OK; see
`docs/copyright-policy.md` "Still TODO" for the audio-fingerprint /
logo-detection items which need external resources.)_

---

## Past goals

_(none yet — the active goal above is the first to be recorded under
this layer.  When achieved goals accumulate, they migrate here in
reverse-chronological order.)_

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
