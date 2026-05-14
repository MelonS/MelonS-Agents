[한국어](./README.ko.md) | **English**

# MelonS-Agents

An efficient macOS-based multi-agent system for short-form video production.
Linux-portable via env-managed tool paths. Designed to evolve its own
logic over time via commits to this repository.

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
       └─────────┘└─────────┘└─────────┘└──────────┘
            │          │        │           │
            ▼          ▼        ▼           ▼
       plan.md   resources/  outputs/   qa-report.md
```

Subagent definitions live in [`.claude/agents/`](.claude/agents/).
Mission templates and shared shell libs are under [`agents/`](agents/).

## Code / Data separation

| Layer | Path | Tracked |
|-------|------|---------|
| Code (logic) | `.claude/agents/`, `agents/`, `config/`, `scripts/` | ✓ |
| Data (outputs) | `records/missions/<date>/<id>/` | ✗ (gitignored) |
| Secrets | `.env` | ✗ (gitignored) |

The repository contains only the agent system itself. Mission outputs —
videos, transcripts, generated assets — stay local under `records/`.
What you see on GitHub is the system's own evolution, not its products.

## Portability

All tool paths and endpoints are env-managed. Swap `.env` to move
between macOS and Linux; no code changes required.

## Autonomy modes

Defined in [`config/policies.yaml`](config/policies.yaml).

- **Interactive** (`AUTONOMY_MODE=false`, default) — pauses for user
  confirmation on logic changes, destructive ops, external publishes.
- **Autonomous** (`AUTONOMY_MODE=true`) — runs unattended within
  `AUTONOMY_BUDGET_USD`. Logic files (`agents/`, `.claude/agents/`) are
  immutable in this mode.

## Mission flow

1. User states a mission.
2. `orchestrator` opens `records/missions/<date>/<id>/` + a task list.
3. `planner` → `plan.md` with acceptance criteria.
4. `resourcer` → assets + `resources/MANIFEST.md`.
5. `editor` → deliverables + `outputs/CHANGELOG.md`.
6. `qa` → `qa-report.md` with PASS / FAIL per criterion.
7. On PASS, `orchestrator` writes `summary.md`.

## Toolchain

`ffmpeg` (static libass build) · `yt-dlp` · `whisper.cpp` (small,
multilingual) · `ollama` (`llama3.2:3b`) · Claude API for orchestration.

## Quick start

```bash
git clone git@github.com:MelonS/MelonS-Agents.git
cd MelonS-Agents
cp .env.example .env
./scripts/bootstrap.sh   # also creates EN+KO synthetic fixtures under /tmp/smoke/
./agents/missions/highlight/run.sh <url_or_local_path>
```

Multi-source batch:

```bash
./scripts/batch-mission.sh -f sources.txt
```

Queue-based autonomous drain (used by the launchd scheduler):

```bash
echo 'https://example.com/long.mp4' >> records/queue/pending.txt
./scripts/mission-queue.sh
```

Install the nightly scheduler:

```bash
./scripts/install-scheduler.sh install
```

## Status

<!-- status:start -->
- [x] Hierarchical agent scaffold (orchestrator + 4 subagents)
- [x] Code/Data separation enforced (records/ gitignored)
- [x] Env-driven tool paths (.env / .env.example)
- [x] PoC end-to-end: highlight extraction (EN + KO)
- [x] libass burned captions via static ffmpeg build
- [x] Multilingual whisper.cpp (small) + language-aware highlight prompt
- [x] Batch runner (scripts/batch-mission.sh)
- [x] Auto-commit + auto-push of every logic change to origin/main
- [ ] Real user-supplied URL fixture
- [x] Nightly launchd scheduler for autonomous mode
- [ ] Iterative QA-feedback loop in editor
- [x] Other mission types beyond highlight extraction
- [x] Cost / runtime metrics per mission
- [ ] QA feedback retry loop (failed runs auto-retried with QA notes)
- [x] Cost / runtime metrics per mission
- [x] Bilingual summarize mission (transcribe → structured EN+KO summary)
- [x] Single-pass ffmpeg render (~3× render speedup)
- [x] Third mission type: shorts-batch (one long video → N captioned shorts)
<!-- status:end -->

## License

MIT. See [`LICENSE`](LICENSE).
