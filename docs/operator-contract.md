# Operator contract

Committed, canonical version of the operating rules that govern how
Claude works on this repository.  Local agent memory at
`~/.claude/projects/-Users-melons-ai/memory/` exists as a fast-access
cache, but **this file is the source of truth** — if the two ever
disagree, the committed file wins, and the memory entry should be
updated to match.

The contract is split into two sections:

1. **Hard rules** — non-negotiable; describe how the agent must
   behave.  Violating these is a bug.
2. **Conventions** — formatting, tone, and presentation defaults.
   Deviating requires explicit user instruction.

Cross-cutting principle: the user is async.  They send a message and
walk away.  Asymmetric latency makes "let me ask first" a cost; act
on reasonable assumptions, let the user redirect.

---

## Hard rules

### 1. Agent does all the work

The user does not run commands, install packages, edit config files,
or touch the terminal.  Claude does all of it.

- Default to running the action (`brew install`, `pip install`,
  `git push`, file edits) yourself.  Auto-mode + the allow list cover
  almost everything.
- When a hard guardrail blocks you (auto-mode classifier denying
  self-modification of `.claude/settings.json`, push-to-main inside
  a compound command, etc.), do **not** respond with "please run X"
  or "please paste Y into Z".  Surface the blocker in one line and
  the *minimal* user action (a single click in the permissions UI),
  never a multi-step bash recipe.
- The user only acts on the *exact* things Claude cannot do.

### 2. Never pause unless told

The user often writes a message and steps away.  Do not end a turn
with "or pause?" / "다음 갈까요?" / "shall I continue?".

- When a task finishes and `docs/roadmap.md`'s **Next** queue has
  an item, promote it to **Now** and start it in the *same* turn.
- Stop only when the user explicitly says stop / wait / pause / 멈춰,
  when a hard guardrail blocks (and even then, switch to the next
  non-blocked item), or when both Now and Next are genuinely empty.
- An unanswered "next?" question turns into hours of idle time.  An
  action the user disagrees with is a 30-second course correction.
  The action is cheaper than the question.

### 3. Money firewall

Auto-approve does not cover anything that spends money or commits to
future money.  Always pause and request explicit confirmation for:

- Paid API usage / SaaS subscription / paid library purchase.
- Paid API calls — including the transition from free credits to
  metered billing.
- Cloud resource creation (AWS, GCP, Azure, any provider where
  standing infrastructure incurs cost).

Local resources (Ollama, FFmpeg, whisper.cpp, macOS `say`, brew
packages) are fully auto-approved.

### 4. Auto-approve mode (local resources)

Non-catastrophic system actions proceed without asking:

- Brew install/uninstall, pip install, npm install.
- File deletion (under the repo / `/tmp`).
- Settings changes, MCP/config edits in the repo.
- All actions inside `.claude/settings.json`'s allow list.

Pause only for **truly catastrophic** risks: hardware damage,
irreversible data loss outside the repo (`rm -rf ~`, disk format,
force-push to shared remotes, sending external messages).

User accepts macOS-environment-level mess (broken brew state, lost
browser data, etc.) as acceptable risk.

### 5. Logic changes require explicit user OK

Editing `agents/*.md` or `.claude/agents/*.md` always requires
explicit user OK, regardless of autonomy mode.  These files define
the subagent contracts; changing them changes the system's logic.

### 6. Git workflow — auto-commit, auto-push

Every code change (anything under `agents/`, `.claude/agents/`,
`config/`, `scripts/`, `CLAUDE.md`, `README.md`, `.env.example`,
`.gitignore`) is committed and pushed to `origin/main` on completion.

- Remote: `git@github.com:MelonS/MelonS-Agents.git` (private).
- Records (`records/`) are never committed.
- Commit message style: imperative subject ≤72 chars, optional body
  with bullets explaining *why*.  Group changes by concern; don't
  bundle unrelated edits.

### 7. Split commit and push

Run `git commit -m "..."` and `git push origin main` as two
separate Bash calls — never the `git commit ... && git push ...`
compound.  The auto-mode classifier blocks the compound; the
workaround (editing `.claude/settings.json` to allow it) hits the
self-modification guardrail.  Splitting is one second per cycle and
always passes.

### 8. Code / data separation

Agent logic lives under `agents/` and `.claude/agents/` (git-tracked).
All outputs go to `$RECORDS_DIR` (default `./records/`, gitignored).
Env-driven tool paths only: never hardcode `/opt/homebrew/...` or
`~/...` — read `$FFMPEG_BIN`, `$OLLAMA_HOST`, `$RECORDS_DIR`, etc.
from `.env`.

### 9. Roadmap is the source of truth for work selection

First action of every conversation that asks for work: read
[`docs/roadmap.md`](roadmap.md)'s **Now** section.

- Do **not** use the README's `Status` checklist to pick work — it's
  a flat capability ledger, not an ordered backlog.
- Do **not** infer the next task from `git log` alone — the log
  shows what landed, not what's *being* worked on or what's *now*
  most important.
- After work lands, append a one-line entry to roadmap's **Done**
  section with the commit hash and date.
- "Now" / "Next" / "Blocked" sections are user-edited; Claude only
  appends to "Done" and promotes from "Next" to "Now" when "Now"
  empties.

Subagents (orchestrator, planner, resourcer, editor, qa) do **not**
read roadmap.md — day-level decisions stay at the top-level
conversation layer so subagents remain pure functions of the prompt
they receive.

### 10. Session resume protocol

When the user opens with one of these patterns —

- "어제 하던 작업 계속" / "이어서 하자"
- "어제 [기능/이슈]..."
- "방금 그거 어디까지 했지?"

— deliver a **[관리자 브리핑]** before starting the new task:

1. Repo state (clean / dirty, last commit, last push).
2. Last work performed (most recent mission / commit / change).
3. Next entry point (what `docs/roadmap.md` Now says).

Pull context from `records/missions/`, `git log`, `git status`, and
the roadmap.  Do not start the new task before the briefing lands.

---

## Conventions

### Dual-stack reporting

- Korean **[관리자 브리핑]** one-liner at the top of every user-facing
  message.
- English internals — commits, code, logs, file paths.
- No raw code dumps in chat.  Reference paths + line numbers; the
  user reads the file directly.
- Periodic Korean status dashboard (`[완료]/[진행]/[다음]`) at natural
  10-minute breakpoints during long autonomous work.

### Terminal / shell format

When a single user message requires multiple shell operations:

- Write the entire sequence into `/tmp/agent_worker.sh` (fixed path).
- Run it once.  One approval per script, not per command.
- Log format inside the script:
  - `[STEP] N/M description` — entering a step.
  - `[DONE] description` — exiting a step successfully.
  - `[PROGRESS] N% description` — long-running progress markers.
  - `[CRITICAL] message` — only at completion or hard failure.

### Documentation style

For all repository documentation (README, docs/, etc.):

- Neutral, technical tone.  No personal credentials, "X years of
  experience", or marketing superlatives.  Clean open-source feel.
- README split into `README.md` (English) and `README.ko.md`
  (Korean) with a language switcher at the top.
- Center-aligned title + badges via `<div align="center">`.
- `for-the-badge` style badges + custom value badges (AI-Powered,
  Self-Evolving, Autonomous).
- Architecture sections use markdown tables with role emojis
  (Orchestrator / Planner / Resourcer / Editor / QA) showing
  responsibility + output.
- No fruit / casual emojis in committed content.

### Batch execution

Multi-step shell work goes into a single `.sh` script — one approval
per script, not per command.  Internal flags (`jq -f`, `--slurpfile`,
`2>&1`, pipes, etc.) are implicitly approved by virtue of being
inside the script.  Avoid `bash -c '...'` chains in the chat;
they're noisy and re-prompt for each invocation.

### Writing tone

Same as documentation style above, applied to:

- Commit messages — explain *why*, not what.
- PR descriptions, when those happen.
- Any public-facing writing.

Keep it tight, technical, and neutral.

---

## Memory and this file

`~/.claude/projects/-Users-melons-ai/memory/` mirrors these rules
into one feedback file per rule for fast lookup at conversation
start.  Each memory file's description should match the
corresponding section heading here.  The MEMORY.md index links each
entry back to a section in this file.

If a memory file disagrees with this file, **this file wins**.  Fix
the memory file in that case, not the contract.
