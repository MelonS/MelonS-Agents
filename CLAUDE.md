# Project: Multi-Agent System

Hierarchical agent system. See `README.md` for layout, `config/policies.yaml` for autonomy rules.

## Operator preferences

- **Auto-approve mode** (user directive, 2026-05-14): non-catastrophic system actions — `brew install/uninstall`, `pip install`, `npm install`, file deletion, settings changes, MCP/config edits — proceed without asking. User accepts macOS-environment-level mess (browser data loss, broken brew state, etc.) as acceptable risk.
- Only pause for **truly catastrophic** risks: hardware damage, irreversible data loss outside the repo (e.g., `rm -rf ~`, disk format, force-push to shared remotes, sending external messages).
- Report results, not approvals.

### Money firewall (explicit confirmation required)

Auto-approve does **not** cover actions that spend money or commit future money. Always pause and request explicit user confirmation for:

1. **Paid API usage / SaaS subscription / paid library purchase** — any action that triggers an actual payment.
2. **Paid API calls** — including the transition point where free credits end and metered billing begins.
3. **Cloud resource creation** — AWS, GCP, Azure, or any provider where standing infrastructure incurs ongoing cost.

Local-only resources (Ollama, FFmpeg, whisper.cpp, macOS `say`, brew packages) stay fully auto-approved.

## Git workflow — auto-commit, auto-push

- **Every Code change** (anything under `agents/`, `.claude/agents/`, `config/`, `scripts/`, `CLAUDE.md`, `README.md`, `.env.example`, `.gitignore`) is committed and pushed to `origin/main` on completion.
- Remote: `git@github.com:MelonS/MelonS-Agents.git` (private).
- `records/` is **never** committed (gitignored). The history on GitHub reflects only how the agent system itself evolves, not its outputs.
- Commit message style: imperative subject ≤72 chars, optional body with bullets explaining *why*. Group changes by concern; don't bundle unrelated edits.

## Core rules

- **Code vs Data separation**: agent logic lives under `agents/` and `.claude/agents/` (git-tracked). All outputs go to `$RECORDS_DIR` (default `./records/`, gitignored).
- **Env-driven paths**: never hardcode `/opt/homebrew/...` or `~/...`. Read `$FFMPEG_BIN`, `$OLLAMA_HOST`, `$RECORDS_DIR`, etc. from `.env`.
- **Autonomy policy**: respect `config/policies.yaml`.
  - `AUTONOMY_MODE=false` (default): pause for user confirmation before logic changes, destructive FS ops, external publishes.
  - `AUTONOMY_MODE=true`: overnight mode. Stay within `AUTONOMY_BUDGET_USD`. Never edit agent definitions unattended.
- **Logic changes**: editing `agents/*.md` or `.claude/agents/*.md` always requires explicit user OK, regardless of mode.

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

