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
