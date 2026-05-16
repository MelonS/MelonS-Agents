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
5. [`roadmap.md`](roadmap.md) — current focus + queued + done.

Each downstream file is self-contained.  You should not need to read
agent source to give a useful first-pass review.

---

## TL;DR (60 seconds)

This is a **two-tier** system on **one machine** (macOS).

```
Tier 1 — Conversational      ┃ Anthropic API. Opus + Sonnet.
                             ┃ Claude Code CLI is the runtime.
                             ┃ Cost lives here.
Tier 2 — Mission execution   ┃ Local. bash + ffmpeg + whisper.cpp +
                             ┃ ollama (llama3.2:3b / qwen2.5-coder:7b).
                             ┃ Zero API cost. Free.
```

The split is the central design choice.  An analyst who misses it
will diagnose phantom problems.

**A single mission run (transcribe → highlight pick → render → QA) is
free.** No Anthropic tokens are spent during mission execution.
Anthropic tokens are spent only when the user is *talking to Claude
Code* — orchestration, planning, code review, debugging.

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

The simulator is one of the project's [deliverable subgoals](goal.md):
infrastructure subgoals alone don't complete a goal until a
concrete artifact exists that proves the path works.

---

## Subagent layout (already verified, do not re-recommend)

`.claude/agents/*.md` frontmatter sets the model.  As of 2026-05-15:

| Agent          | Model     | Role                                              |
| -------------- | --------- | ------------------------------------------------- |
| `orchestrator` | **opus**  | Top-level mission decomposition + coordination    |
| `planner`      | sonnet    | Mission brief → `plan.md` + acceptance criteria   |
| `resourcer`    | sonnet    | Fetch / probe / prepare assets → `resources/`     |
| `editor`       | sonnet    | Apply changes → `outputs/`                        |
| `qa`           | sonnet    | Validate outputs against plan.md → `qa-report.md` |

A common analyst suggestion is "downgrade subagents from opus to
sonnet."  This is already done.

A *valid* future move is downgrading `resourcer` to **haiku** —
file fetching needs little reasoning depth.  Not done because the
delta is small and risk of misfetched assets is non-trivial.

---

## Mission execution: where the money *isn't*

The three missions (`agents/missions/{highlight,summarize,shorts-batch}/run.sh`)
do **not** call Anthropic.  They call `agents/lib/ollama.sh` which
posts to `http://127.0.0.1:11434/api/generate`.  See:

- [`agents/lib/ollama.sh`](../agents/lib/ollama.sh) — HTTP client to local Ollama.
- [`.env.example`](../.env.example) — `OLLAMA_MODEL_HIGHLIGHT=llama3.2:3b`.
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
- No CI / no automated tests on push.  The repo treats every push
  to `main` as a logical unit; pre-commit gating is intentionally
  absent so the auto-commit / auto-push contract stays simple.  See
  [`operator-contract.md`](operator-contract.md) §6.  One regression
  check that does exist:
  [`scripts/test-fresh-clone.sh`](../scripts/test-fresh-clone.sh)
  runs a full clone → bootstrap → mission cycle against `origin/main`
  on demand, with PASS / FAIL evidence in
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
