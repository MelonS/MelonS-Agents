# Multi-Agent System

macOS-first, Linux-portable hierarchical agent system.

## Architecture

```
Orchestrator (main)
├── Planner    — mission breakdown, strategy
├── Resourcer  — fetches assets, runs tools (ffmpeg, web, files)
├── Editor     — applies changes, writes outputs
└── QA         — validates outputs, regression checks
```

## Layout

| Path          | Layer | Tracked |
|---------------|-------|---------|
| `agents/`     | Code  | ✅ git  |
| `.claude/agents/` | Code (Claude subagent defs) | ✅ git |
| `config/`     | Code  | ✅ git  |
| `scripts/`    | Code  | ✅ git  |
| `records/`    | Data  | ❌ gitignored (outputs only) |
| `.env`        | Secret | ❌ gitignored |

## Setup

```bash
cp .env.example .env
# edit .env with local paths
./scripts/bootstrap.sh
```

## Modes

- **Interactive** — `AUTONOMY_MODE=false`. Logic changes pause for user confirmation.
- **Autonomous** — `AUTONOMY_MODE=true`. Overnight mission execution within `AUTONOMY_BUDGET_USD` ceiling.
