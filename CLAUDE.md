# Project: Multi-Agent System

Hierarchical agent system. See `README.md` for layout, `config/policies.yaml` for autonomy rules, and [`docs/operator-contract.md`](docs/operator-contract.md) for the full set of operating rules.

## Session-start protocol (read this first)

**The first action of every conversation that asks for work is to read [`docs/roadmap.md`](docs/roadmap.md) — specifically the "Now" section.** That document is the source of truth for what to work on next. It is dated, ordered, and survives across sessions.

- Do **not** use the README's "Status" checklist to pick work — it has no order, no dates, no priority signal.
- Do **not** infer the next task from `git log` alone — the log shows what landed, not what was *being* worked on or what is *now* most important.
- If `docs/roadmap.md` "Now" is empty, promote the top of "Next"; if both are empty, make a reasonable assumption and start, letting the user redirect.
- After work lands, append a one-line entry to `docs/roadmap.md` "Done" with the commit hash and date.
- Subagents (orchestrator, planner, resourcer, editor, qa) do **not** read `docs/roadmap.md`. Day-level decisions belong to the top-level conversation; subagents stay pure functions of the mission prompt they receive.

## Operating rules

The full contract — agent behavior, never-pause rule, money firewall, dual-stack reporting, terminal format, documentation style, split-commit-push, session-resume protocol — lives in [`docs/operator-contract.md`](docs/operator-contract.md). Committed; survives machine changes; agent memory is a fast-access cache pointing back to it. The four most-load-bearing summarized inline:

- **Agent does all the work** — user never touches the terminal. Claude installs, edits, configs, commits, pushes. User intervenes only on hard guardrails (single-click approval, never a multi-step recipe).
- **Never pause unless told** — user is async; "or pause?" turns into hours of idle. When `docs/roadmap.md` Next has an item and Now finishes, promote it and continue in the same turn.
- **Money firewall** — paid APIs, SaaS, cloud-resource creation require explicit user confirmation. Local resources (Ollama, FFmpeg, brew, whisper, yt-dlp) stay auto-approved.
- **Logic changes need explicit OK** — editing `agents/*.md` or `.claude/agents/*.md` always pauses for user confirmation, regardless of autonomy mode.

## Git workflow — auto-commit, auto-push

- **Every code change** (anything under `agents/`, `.claude/agents/`, `config/`, `scripts/`, `docs/`, `CLAUDE.md`, `README.md`, `.env.example`, `.gitignore`) is committed and pushed to `origin/main` on completion.
- Remote: `git@github.com:MelonS/MelonS-Agents.git` (private).
- `records/` is **never** committed (gitignored). The history on GitHub reflects only how the agent system itself evolves, not its outputs.
- Use `git commit` and `git push` as two separate Bash calls; never `&&`-compound (classifier blocks it; see operator-contract §7).
- Commit message style: imperative subject ≤72 chars, optional body with bullets explaining *why*. Group changes by concern; don't bundle unrelated edits.

## Core rules

- **Code vs Data separation**: agent logic lives under `agents/` and `.claude/agents/` (git-tracked). All outputs go to `$RECORDS_DIR` (default `./records/`, gitignored).
- **Env-driven paths**: never hardcode `/opt/homebrew/...` or `~/...`. Read `$FFMPEG_BIN`, `$OLLAMA_HOST`, `$RECORDS_DIR`, etc. from `.env`.
- **Autonomy policy**: respect `config/policies.yaml`.
  - `AUTONOMY_MODE=false` (default): pause for user confirmation before logic changes, destructive FS ops, external publishes.
  - `AUTONOMY_MODE=true`: overnight mode. Stay within `AUTONOMY_BUDGET_USD`. Never edit agent definitions unattended.

## Subagents

Defined in `.claude/agents/`. Orchestrator delegates via the Agent tool.

| Agent | Role |
|-------|------|
| `planner` | Mission decomposition, strategy |
| `resourcer` | Fetch assets, run external tools |
| `editor` | Apply changes, write outputs |
| `qa` | Validate outputs, regressions |

## Records layout

```
records/
├── missions/<ISO-date>/<mission-id>/
│   ├── plan.md           # planner output
│   ├── resources/        # resourcer artifacts
│   ├── outputs/          # editor outputs
│   └── qa-report.md      # qa output
└── blockers/<ISO-date>/  # autonomous-mode halt logs
```

## Permanent autonomy contract (session-stable)

This project's autonomy rules are persisted at three levels:

1. **Per-project**: `.claude/settings.json` (committed, applies in this repo).
2. **Per-user**: `~/.claude/settings.json` (mirrors the same allow/deny list, applies anywhere on this machine).
3. **Per-memory**: agent memory in `~/.claude/projects/-Users-melons-ai/memory/` records the *why* and *how* of each rule.

When a new session starts on this repo, Claude Code reads all three. The default operating mode is:

- Auto-approve every local-resource action (brew, git, gh, ffmpeg, yt-dlp, whisper, ollama, jq, python, file ops).
- Pause only for the **money firewall** (paid APIs/SaaS/cloud) and **OS-destructive** ops (sudo, rm at system roots, disk erase, force-push to main).
- Treat multi-step shell work as a single batch — write a script, run it once, never one-off prompts.

Re-establishing this contract should never require re-asking the user. If a prompt fires for something this contract already covers, that's a configuration drift; the fix is to update the allow list, not to ask.

