<div align="center">

# MelonS-Agents

[한국어](./README.ko.md) | **English**

![AI-Powered](https://img.shields.io/badge/AI--Powered-FF6B35?style=for-the-badge&logo=anthropic&logoColor=white)
![Self-Evolving](https://img.shields.io/badge/Self--Evolving-8B5CF6?style=for-the-badge&logo=git&logoColor=white)
![Autonomous](https://img.shields.io/badge/Autonomous-10B981?style=for-the-badge&logo=robotframework&logoColor=white)

![macOS](https://img.shields.io/badge/macOS-000000?style=for-the-badge&logo=apple&logoColor=white)
![Shell](https://img.shields.io/badge/Shell-4EAA25?style=for-the-badge&logo=gnu-bash&logoColor=white)
![FFmpeg](https://img.shields.io/badge/FFmpeg-007808?style=for-the-badge&logo=ffmpeg&logoColor=white)
![Ollama](https://img.shields.io/badge/Ollama-000000?style=for-the-badge&logo=ollama&logoColor=white)
![Claude](https://img.shields.io/badge/Claude-D97757?style=for-the-badge&logo=anthropic&logoColor=white)

</div>

## Overview

> An efficient macOS-based multi-agent system for short-form video
> production. Built on a single premise: **automate the production
> pipeline, then let the system evolve its own logic.** Every commit
> in this repository is a step in that evolution — the history is not
> a record of outputs, but of how the agent system itself grows over time.

## Design notes

A few choices that distinguish this from a typical agent demo:

- **Outcome layer vs. work queue, kept separate.** [`docs/goal.md`](docs/goal.md)
  holds the active goal as a concrete deliverable; [`docs/roadmap.md`](docs/roadmap.md)
  holds the day-level work queue. An empty queue does **not** mean the
  goal is achieved — only the goal's "Done when" criteria do. The split
  exists because an earlier 24-hour stretch produced 11 infra commits
  with the queue reading 0 open items and 0 actual outputs.
- **Operator contract as canonical, committed source of truth.**
  [`docs/operator-contract.md`](docs/operator-contract.md) — 12 hard
  rules + conventions. The agent's local memory is a fast-access cache
  that links back to this file; if the two disagree, the file wins and
  the memory entry is corrected.
- **Cost firewall between orchestration and execution.** Anthropic API
  tokens are spent only during orchestration (Tier 1). Mission execution
  (transcribe → select → render → QA) runs entirely on local tools —
  `whisper.cpp` + `ollama` + `ffmpeg` — and costs zero tokens. See
  [`docs/cost-model.md`](docs/cost-model.md).
- **Out-of-band auditor with an active alert surface.** The
  [`auditor`](.claude/agents/auditor.md) subagent runs daily at 03:00
  via `launchd`, walks the whole repo read-only, and writes to a stable
  channel: [`docs/audit/CURRENT-ALERT.md`](docs/audit/) exists iff the
  latest verdict is non-CLEAN; the next interactive session is
  contractually obligated to read it before picking up the goal.
- **File-based subagent handoff.** Subagents do not share conversation
  history. They communicate through committed files (`plan.md` /
  `MANIFEST.md` / `qa-report.md`). Each subagent's context is bounded
  by its prompt + the manifest it reads — predictable token cost,
  predictable failure modes.

## Sample output

![Sample frame — 9:16 short with burned-in captions and source attribution](docs/caption-verify/highlight-032405-son-heungmin-cap.jpg)

60-second 9:16 short produced from a CC-BY-3.0 interview clip on
Wikimedia Commons (mission `highlight-032405`). The frame shows the
top-left source-attribution overlay, the blurred-letterbox 9:16
background, and a libass-burned caption rendering the speaker's
transcribed Korean line. QA: PASS on attempt 1. The full mission
record lives under `records/` (gitignored); the caption-verify frame
is the only committed artifact, kept as durable visual evidence.

## For analysts / reviewers

Doing a read-only analysis of this repository? Start at
[`docs/for-analysts.md`](docs/for-analysts.md) — a single-file entry
point optimized for first-pass diagnosis. Pairs with
[`docs/cost-model.md`](docs/cost-model.md) (where Anthropic vs. local
cost lives) and [`docs/architecture.md`](docs/architecture.md) (full
data-flow map).

## Architecture

```
              ┌───────────────────┐
              │   Orchestrator    │   model: opus
              └─────────┬─────────┘
                        │ delegates the mission, in order
                        ▼
              ┌───────────────────┐
              │      Planner      │   model: sonnet
              └─────────┬─────────┘
                        ▼
              ┌───────────────────┐
              │     Resourcer     │   model: sonnet
              └─────────┬─────────┘
                        ▼
              ┌───────────────────┐
              │       Editor      │   model: sonnet
              └─────────┬─────────┘
                        ▼
              ┌───────────────────┐
              │         QA        │   model: sonnet
              └───────────────────┘

              ┌───────────────────┐
              │      Auditor      │   model: sonnet  (out-of-band)
              └───────────────────┘   read-only; scheduled daily
                                       at 03:00 via launchd
```

| Agent | Responsibility | Output |
|-------|----------------|--------|
| 🤖 **Orchestrator** (opus) | Mission decomposition, delegation, final synthesis | task list · `summary.md` |
| 🧠 **Planner** (sonnet) | Strategy, work breakdown, acceptance criteria | `plan.md` |
| 📦 **Resourcer** (sonnet) | Asset fetching, external tool execution (ffmpeg / yt-dlp / whisper) | `resources/MANIFEST.md` |
| 🎞️ **Editor** (sonnet) | Output rendering, deliverable assembly | `outputs/CHANGELOG.md` |
| ✅ **QA** (sonnet) | Validation against plan criteria, regression detection | `qa-report.md` |
| 🔍 **Auditor** (sonnet) | Repository-wide drift / contract / cost / security audit (out-of-band, daily 03:00) | `docs/audit/<date>-<focus>.md` + `docs/audit/CURRENT-ALERT.md` when non-CLEAN |

Subagent definitions: [`.claude/agents/`](.claude/agents/) · Mission templates and shared shell libs: [`agents/`](agents/)

## Code / Data separation

| Layer | Path | Tracked |
|-------|------|---------|
| Code (logic) | `.claude/agents/`, `agents/`, `config/`, `scripts/` | ✓ |
| Data (outputs) | `records/missions/<date>/<id>/` | ✗ (gitignored) |
| Secrets | `.env` | ✗ (gitignored) |

The repository contains only the agent system itself. Mission outputs —
videos, transcripts, generated assets — stay local under `records/`.
What appears on GitHub is the system's own evolution, not its products.

## Portability

All tool paths and endpoints are env-managed. Swap `.env` to move
between macOS and Linux; no code changes required.

## Autonomy modes

Defined in [`config/policies.yaml`](config/policies.yaml).

| Mode | Flag | Behavior |
|------|------|----------|
| ⚙️ **Interactive** (default) | `AUTONOMY_MODE=false` | Pauses for user confirmation on logic changes, destructive ops, and external publishes. |
| 🌙 **Autonomous** | `AUTONOMY_MODE=true` | Runs unattended within `AUTONOMY_BUDGET_USD`. Logic files (`agents/`, `.claude/agents/`) are immutable. |

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
./scripts/bootstrap.sh
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

## Operator contract

This repository is fully agent-operated. The day-to-day rules:

- The **agent does all the work** — installs, edits, configs, commits, pushes, scheduling. The user does not run commands in the terminal.
- The user steps in **only** when a hard guardrail blocks the agent (e.g., self-modifying its own permissions, force-pushing to `main`) — and even then only as a single click of approval, never a multi-step recipe.
- **Active focus** lives in [`docs/roadmap.md`](docs/roadmap.md). The list below ("Status") is a flat capability ledger; do not read it as a TODO list. The roadmap's *Now* section is the source of truth for "what to work on next."
- **Money firewall**: paid APIs, SaaS subscriptions, and cloud-resource creation require explicit user confirmation. Local resources (Ollama, FFmpeg, whisper.cpp, brew) stay fully autonomous.

Full contract: see [`CLAUDE.md`](CLAUDE.md) and the [`config/policies.yaml`](config/policies.yaml) autonomy rules.

## Status

<!-- status:start -->
- [x] Hierarchical agent scaffold (orchestrator + 4 mission subagents + 1 read-only auditor subagent)
- [x] Code/Data separation enforced (records/ gitignored)
- [x] Env-driven tool paths (.env / .env.example)
- [x] PoC end-to-end: highlight extraction (EN + KO)
- [x] libass burned captions via static ffmpeg build
- [x] Multilingual whisper.cpp (small) + language-aware highlight prompt
- [x] Batch runner (scripts/batch-mission.sh)
- [x] Auto-commit + auto-push of every logic change to origin/main
- [x] Nightly launchd scheduler for autonomous mode
- [x] Three mission types operational: highlight, summarize, shorts-batch
- [x] Single-pass ffmpeg render (~3× render speedup)
- [x] Bilingual summarize mission (transcribe → structured EN+KO summary)
- [x] Cost / runtime metrics per mission
- [x] Real CC-licensed source fixtures (Blender open movies) + downloader
- [x] Standard 9:16 layout engine — safe-zone margins, semi-transparent caption box, top-left source-attribution overlay
- [x] Source-attribution wiring across all three missions (`outputs/SOURCES.txt` + burned watermark + `summary.md` footer)
- [x] QA feedback retry loop (failed missions auto-retried up to `QA_RETRY_MAX`, then dropped to `records/blockers/`)
- [x] Copyright filter v1 — domain allowlist, publish-gate, strike-record log, strike-aware source rejection
- [x] License-string probe for archive.org + commons.wikimedia.org
- [x] Day-level roadmap at [`docs/roadmap.md`](docs/roadmap.md) (source of truth for "what to work on next")
- [x] Per-platform reuse rules in `scripts/publish-gate.sh` (`internal-demo` / `public` / `youtube` / `instagram` / `tiktok` — honors all four `publish_rules` fields)
- [x] Repository auditor subagent + active surface (`docs/audit/CURRENT-ALERT.md` auto-maintained by `scripts/audit-run.sh`)
- [ ] Real user-supplied URL fixture — _blocked, waiting on URL from user_
- [ ] License-string probe for additional hosts (Vimeo CC channel, etc.) — _deferred, Vimeo lacks a per-item license endpoint; revisit on demand_
- [ ] Audio-fingerprint check (chromaprint / `fpcalc`) — _deferred, needs a fingerprint dataset to compare against; revisit after first takedown_
- [ ] Logo / watermark detection on source frames — _deferred, needs OCR or a trained model; revisit when the failure mode is observed_
- [ ] Iterative QA-feedback loop *inside* editor (per-output re-cut without rerunning transcribe/select) — _parked, only useful when coarse retry wastes compute; not yet observed_
<!-- status:end -->

> Unchecked items above are **all intentionally deferred** — each carries an inline reason. The day-level priority queue lives in [`docs/roadmap.md`](docs/roadmap.md), not here.

## License

MIT. See [`LICENSE`](LICENSE).
