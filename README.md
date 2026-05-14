# MelonS-Agents

An efficient multi-agent system for short-form video production.
macOS-first, Linux-portable. Designed to evolve its own logic over time via
commits to this repository.

## Architecture

```
                       ┌───────────────────┐
                       │   Orchestrator    │
                       │     (opus)        │
                       └────────┬──────────┘
                                │ delegates
            ┌──────────┬────────┼────────┬──────────┐
            ▼          ▼        ▼        ▼          ▼
       ┌─────────┐┌─────────┐┌─────────┐┌──────────┐
       │ Planner ││Resourcer││ Editor  ││    QA    │
       │(sonnet) ││(sonnet) ││(sonnet) ││ (sonnet) │
       └─────────┘└─────────┘└─────────┘└──────────┘
            │          │        │           │
            ▼          ▼        ▼           ▼
       plan.md   resources/  outputs/   qa-report.md
```

Each subagent has a focused role with a defined input/output contract.
Definitions live in [`.claude/agents/`](.claude/agents/) as native Claude Code
agent specs.

| Agent | Role | Output contract |
|-------|------|-----------------|
| `orchestrator` | Decomposes missions, delegates, aggregates | `summary.md` |
| `planner` | Goal → concrete steps + acceptance criteria | `plan.md` |
| `resourcer` | Fetches/prepares external assets | `resources/MANIFEST.md` |
| `editor` | Produces final deliverables | `outputs/CHANGELOG.md` |
| `qa` | Validates outputs against the plan | `qa-report.md` |

## Code / Data separation

| Layer | Path | Tracked in git |
|-------|------|----------------|
| Code (logic) | `.claude/agents/`, `agents/`, `config/`, `scripts/` | ✓ |
| Data (outputs) | `records/missions/<date>/<id>/` | ✗ (gitignored) |
| Secrets | `.env` | ✗ (gitignored) |

The repository contains only the agent system itself. Mission outputs —
videos, transcripts, generated assets — stay local under `records/`.
What you see on GitHub is the system's own evolution, not its products.

## Portability

All tool paths and endpoints are env-managed. To move from macOS to Linux,
swap `.env` values; no code changes required.

```bash
FFMPEG_BIN=/opt/homebrew/bin/ffmpeg     # macOS
FFMPEG_BIN=/usr/bin/ffmpeg              # Linux
```

See [`.env.example`](.env.example) for the full list.

## Autonomy modes

Defined in [`config/policies.yaml`](config/policies.yaml).

- **Interactive** (`AUTONOMY_MODE=false`, default) — pauses for user
  confirmation on logic changes, destructive FS operations, external
  publishes, and undeclared credential use.
- **Autonomous** (`AUTONOMY_MODE=true`) — runs unattended within an
  `AUTONOMY_BUDGET_USD` ceiling. Writes outputs to `records/` only.
  Logic-layer files (`agents/`, `.claude/agents/`) remain immutable in
  this mode; any required logic change halts the run and writes a
  blocker log for the next interactive session.

## Mission flow

1. User states a mission ("turn this URL into a 60-second highlight short").
2. `orchestrator` opens `records/missions/<date>/<id>/` and a task list.
3. `planner` → `plan.md` with acceptance criteria.
4. `resourcer` → assets under `resources/` + `MANIFEST.md`.
5. `editor` → deliverables under `outputs/` + `CHANGELOG.md`.
6. `qa` → `qa-report.md` with PASS / PARTIAL / FAIL per criterion.
7. On PASS, `orchestrator` writes `summary.md`. On FAIL, either loops
   back to `editor` with QA notes or halts (depending on autonomy mode).

## Toolchain

| Tool | Purpose |
|------|---------|
| `ffmpeg` / `ffprobe` | Cut, crop, encode, validate video |
| `yt-dlp` | Fetch source video |
| `whisper.cpp` | Local speech-to-text |
| `ollama` | Local LLM for highlight selection / classification |
| Claude API (Opus / Sonnet) | Orchestration and reasoning-heavy steps |

## Quick start

```bash
git clone git@github.com:MelonS/MelonS-Agents.git
cd MelonS-Agents
cp .env.example .env
$EDITOR .env
./scripts/bootstrap.sh
```

`bootstrap.sh` verifies that every required binary resolves from `.env`
and that the records directory is writable.

## Repository layout

```
.
├── .claude/
│   ├── agents/          # native subagent definitions (orchestrator + 4 roles)
│   └── settings.json    # auto-approve permission profile
├── agents/              # mission templates + shared shell libs (planned)
├── config/
│   ├── mcp.json         # project-local MCP servers
│   └── policies.yaml    # autonomy rules
├── scripts/
│   └── bootstrap.sh     # env / toolchain health check
├── records/             # ← outputs go here (gitignored)
├── .env.example
├── CLAUDE.md            # agent-facing project rules
└── README.md
```

## Status

PoC in progress: long-form video → 9:16 highlight short, captioned.
Initial target platforms: TikTok and YouTube Shorts.

## License

Not yet declared. Treat as all rights reserved until a license file is added.
