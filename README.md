<div align="center">

# MelonS-Agents

[한국어](./README.ko.md) · **English** · [**Live site →**](https://melons.github.io/MelonS-Agents/)

### A multi-agent system built solo with [Claude Code](https://docs.anthropic.com/claude-code). It turns music into short-form video, and builds a colony-sim game it plays to verify itself.

**Zero runtime API cost · English + Korean from day one.**

[![main-protection](https://github.com/MelonS/MelonS-Agents/actions/workflows/main-protection.yml/badge.svg?branch=main)](https://github.com/MelonS/MelonS-Agents/actions/workflows/main-protection.yml)
![GitHub last commit](https://img.shields.io/github/last-commit/MelonS/MelonS-Agents?style=flat-square)
![Runtime API tokens](https://img.shields.io/badge/runtime%20API%20tokens-0-10B981?style=flat-square)
![Built with Claude Code](https://img.shields.io/badge/built%20with-Claude%20Code-D97757?style=flat-square&logo=anthropic&logoColor=white)
![License](https://img.shields.io/github/license/MelonS/MelonS-Agents?style=flat-square)

![PawnSim 16-in-game-day colony timelapse (2026-06-12 build) — three colonists found a camp on open grass, designate a stockpile and farm plots, build a walled bedroom with beds/stove/research bench, and grow the colony while raids are repelled.  Every frame is from an unattended soak run; the loop shown (stockpile → housing → farming → logging → mining) is machine-verified by effect assertions + isolated-grader rubric verdicts](docs/demo/pawnsim-2026-06-12-colony-timelapse.gif)

*Unattended 16 in-game-day colony soak — the agent built this game **and** verified it.*

</div>

- **It ships.** The **music-video** pipeline delivers on a schedule — a song → a 60-second 9:16 short (beat-cut, genre-graded). A second pipeline, **content-shorts**, shipped its first real short to YouTube on 2026-07-01 (idol format) — not on a cadence yet.
- **Zero runtime cost.** Local open-source tools (ffmpeg · whisper.cpp · ollama · aubio) do the mechanical work; Claude Code agents only orchestrate — so a mission spends **zero runtime API tokens**.
- **It checks its own work.** The showcase — the colony-sim **PawnSim** — is *built and play-verified* by the agent: real player clicks replayed, each asserted to actually change game state, long unattended runs graded by a screenshots-only sub-agent.

*One operator's agent system, in the open: the media pipelines are yours to clone and run (Mac/Linux) — the game and the engineering are here to read and learn from.*

![MelonS-Agents — by the numbers: 100+ outputs, 1 production skill, 23 shaders, 0 runtime API tokens, 15-scenario gate, 23 subagents, 3 audit layers, MIT](docs/visuals/01-hero-stats.png)

## What works today

| Track | What it does | Status | Runnable today |
|-------|--------------|--------|----------------|
| **music-video** | a song in → a 60-second 9:16 short (beat-aligned cuts, vintage ffmpeg shaders) | Production\* | ✅ Mac/Linux — `./scripts/first-touch.sh`, ~60 s |
| **job-hunt** | a keyword → a Korean job-board digest (11 source plugins) | Parked | ❌ KR job boards now block scraping — mock/dry-run only |
| **PawnSim** · built by `game-dev-agent` | a self-verified colony-sim game prototype | In development | ⚠️ Windows + Unity 6000.0.75f1 |
| **content-shorts** · built by a 4-team legal-gated pipeline | a topic → a sourced, copyright-reviewed 9:16 short (info / news / idol formats) | In development | ⚠️ first real short shipped to YouTube 2026-07-01 (idol); not on a cadence yet · needs a Pexels key |
| **product-cf** | a product photo → a CF-style short | Parked | ❌ parked on an honest negative finding |

<sub>\*"Production" = ships a real deliverable on a schedule (only **music-video** qualifies today).  `game-dev-agent` is the meta-skill that builds PawnSim; it joins the production count once PawnSim ships on a cadence.</sub>

## It checks its own work

![Verification — two gates: a 15-scenario input-level repro gate per commit + an isolated grader sub-agent on long soaks](docs/visuals/14-verification-loop.png)

Anyone can make an agent *emit* code. The hard part is proving the result actually works — and that's the spine of this repo. PawnSim passes two gates before anything lands:

- **A 15-scenario repro gate on every commit.** The agent synthesizes real player clicks through the same UI a player uses, and asserts each one had an *effect* ("the click placed a designation") — not just that the click landed.
- **An isolated grader on long unattended soaks.** A separate sub-agent sees only evidence (screenshots + raw logs), never the author's intent, and grades the run against a written rubric.

That grader repeatedly caught what self-review missed: a silent harness blind spot that had voided *every* designation, a "food-rich colony starving to death" mood-gate trap, and a permanent-mental-break colony freeze. The basic loop (stockpile → housing → farming → logging → mining) is now machine-verified end-to-end, with the rubric verdicts committed alongside the fixes. Nine incidents, each written up *problem → constraint → decision → artifact*: [`docs/engineering-case-studies.md`](docs/engineering-case-studies.md).

**Key terms** — *repro gate*: replay real player clicks, assert each had an effect.  *isolated grader*: a separate sub-agent that judges from screenshots + logs only.  *soak*: a long, unattended test run.

## Try it in ~60 seconds

> **Prerequisite:** Mac or Linux with `ffmpeg`, `ollama`, and `aubio` on your PATH — the wizard checks first and prints the exact `brew` / `apt` install command for anything missing (clone-and-go is verified on macOS).

```bash
git clone --depth 1 https://github.com/MelonS/MelonS-Agents.git
cd MelonS-Agents
./scripts/first-touch.sh        # guided demo: checks tools, renders a 9:16 short, opens it
```

No Pexels signup, no Suno round-trip, no `.env` edit — the wizard fetches a demo cache and renders a 60-second short from bundled CC-BY clips + music.  Manual, advanced, and per-skill paths are under **Run paths** below.

## PawnSim in motion

![PawnSim 2026-06-12 — an early colony on open grass: three named colonists with health and mood bars, a wood-wall room frame going up at top-right (top-down block walls from the 32px art generation), gathered wood plus scattered ore and rock nodes, live resource counters, and the build menu open at bottom](docs/demo/pawnsim-2026-06-12-built-house.png)

Colonists chop / mine / farm / cook / haul / build / research / fight under a utility AI; an AI Director schedules threats on a jittered clock; the player drafts pawns and paints build + designation orders.  Every sprite (a full **32px art generation**), every scene, and every C# system is CLI-scaffolded by [`game-dev-agent`](skills/game-dev-agent/) with **no manual Unity Editor work**.  Full feature list + honest verification status (including known gaps): [`skills/game-prototype/README.md`](skills/game-prototype/README.md).

## Sample output — music-video

![5-second animated preview from the 2026-05-22 noir-detective render — 9:16 vertical short, smoky bar interior, bearded man with pipe in pink-magenta rnb_low_key grade profile, phrase-aware shaders + per-genre color grade transforming generic Pexels B-roll into a genre-coded look](docs/demo/music-video-noir-detective-2026-05-24-preview.gif)

A song in → a music-as-primary-audio 9:16 short out: beat-aligned cuts, onset-aligned glitch micro-edits, and one of six per-genre color grades (plus a neutral pass-through) shaping generic stock B-roll into a genre-coded look.  Picked over the earlier narration-driven format on 2026-05-17.  Full pipeline — 23 shaders, the genre catalog, the v1→v6 evolution: [`docs/music-video-pipeline-reference.md`](docs/music-video-pipeline-reference.md).

## How it stays at zero runtime cost

![The 3-shape skill model — Shape A missions-routed 5-agent pipeline, Shape B standalone, Shape ? future skills](docs/visuals/05-three-shapes.png)

The system doesn't force every skill through one shape.  **Shape A** routes through a 5-agent mission pipeline (orchestrator + planner / resourcer / editor / qa); **Shape B** is a standalone script when planner/qa stages would be near-empty.  Subagents share no conversation history — they hand off through committed files (`plan.md` / `MANIFEST.md` / `qa-report.md`), so each one's context and cost stay bounded.  Per-role model routing (planner/resourcer = opus, editor/qa = sonnet) plus a cost firewall keep Anthropic tokens to orchestration only — mission execution runs entirely on local tools, so runtime API tokens stay at **zero**.  `.claude/agents/` holds **23** definitions (6 core + 12 game roster + 5 content-pipeline teams).  Full data-flow map + the game-prototype build chain: [`docs/architecture.md`](docs/architecture.md).

## Autonomy signal — measured, not claimed

![Two-panel intervention trend — Panel A (Daily commit attribution) stacks daily commit counts by initiator (agent-autonomous blue vs user-initiated red) with a user-initiated percentage line and per-day percentage labels; Panel B (Operator engagement) charts daily operator prompts and active session minutes mined from local Claude Code session JSONLs.  Korean mirror at docs/metrics/intervention-ko.png.](docs/metrics/intervention-en.png)

A multi-agent system that needs constant steering hasn't escaped the effort it was meant to replace.  So every commit on `main` is classified **user-initiated** vs **agent-autonomous**, and the operator's Claude Code session logs are mined for prompt count + active minutes — the goal is for both panels to trend down as the system absorbs more decisions.  Classification heuristics + reduction analysis: [`docs/research/2026-05-22-intervention-reduction.md`](docs/research/2026-05-22-intervention-reduction.md).

## Honest by design

Documented negatives, kept in the open — because honest scoping is the credibility the rest of this rests on:

- **`product-cf` is parked** on a real negative finding.  The free / local "make it genuinely 3D" approaches (depth-parallax, cylinder-wrap turntable, local image-to-video) didn't clear a real-CF quality bar on a 16 GB machine; a convincing result needs paid cloud image-to-video or a bigger GPU.  Kept gated-off in the tree, decision pending.
- **`job-hunt` is parked** — the Korean job boards it targeted (Saramin, Wanted, JobKorea, Worknet…) now block scraping, so the live digest never came together; it runs only on mock / dry-run data.  Built out over two days in May, then stopped — the code stays in the tree.
- **Cel-shading was deliberately deferred** — knowing where the ffmpeg wall is beats faking the result.
- **`100+ outputs` is a working estimate, not a ledger** — mission outputs stay local under `records/` (gitignored), so the count isn't independently auditable from the repo.

More negatives and deferred scope: [`skills/game-prototype/README.md`](skills/game-prototype/README.md) (honest verification status + out-of-scope).  Resolved-issue log (e.g. the Homebrew ffmpeg/libass split): [`docs/known-limitations.md`](docs/known-limitations.md).

<details>
<summary><b>Design notes — choices that set this apart from a typical agent demo</b></summary>

- **Outcome layer vs work queue, kept separate.**  [`docs/goal.md`](docs/goal.md) holds the active goal as a concrete deliverable; [`docs/roadmap.md`](docs/roadmap.md) holds the day-level queue.  An empty queue ≠ goal achieved — the split exists because an earlier 24-hour stretch produced 11 infra commits with the queue reading 0 open items and 0 actual outputs.
- **Out-of-band auditor with a live alert surface.**  The [`auditor`](.claude/agents/auditor.md) subagent fires on three triggers — an L1 post-commit hook, an L2 15-minute anomaly poll, and an L3 daily 03:00 baseline (via `launchd`/`cron`) — walks the repo read-only, and writes [`docs/audit/CURRENT-ALERT.md`](docs/audit/) iff the latest verdict is non-CLEAN; the next session is contractually obligated to read it before picking up the goal.
- **Operator tooling that absorbs status-check prompts.**  `scripts/doctor.sh` (a Claude-free ~2-second health check), `scripts/statusline.sh`, and `scripts/morning-brief.sh` answer "what's the state / what happened overnight?" without the operator typing.  Full catalog: [`docs/operator-tooling.md`](docs/operator-tooling.md).

The full operator contract (12 hard rules + autonomy modes) lives in [`docs/operator-contract.md`](docs/operator-contract.md) and [`CLAUDE.md`](CLAUDE.md).

</details>

<details>
<summary><b>Run paths beyond 60 seconds — manual music-video · job-hunt · PawnSim build</b></summary>

**Manual music-video** (after the clone)
```bash
./scripts/bootstrap.sh                       # verifies tools, prints brew/apt hints for anything missing
MUSIC_VIDEO_DEMO_MODE=1 ./agents/missions/music-video/run.sh demo
```
Output at `records/missions/<date>/music-video-demo-<HHMMSS>/outputs/short.mp4`.  All env vars, flags, the shader catalog, and the full Pexels + operator-music path: [`docs/music-video-pipeline-reference.md`](docs/music-video-pipeline-reference.md).

**job-hunt** (parked — mock/dry-run only)
```bash
skills/job-hunt/scripts/run.sh --seed "Problem Solver" --dry-run
```
The KR sources (Saramin, Wanted, JobKorea…) now block scraping; the `global-*` plugins (`JH_GLOBAL_ATS_LIVE=1 …`) may still return live remote postings.  Per-source activation + the 4 Claude enrichment utilities: [`docs/skills/job-hunt.md`](docs/skills/job-hunt.md) · sample digest: [`docs/samples/job-hunt-digest-mock.md`](docs/samples/job-hunt-digest-mock.md).

**PawnSim** (Windows + Unity 6000.0.75f1 LTS)
```bash
cd skills/game-prototype
python ../game-dev-agent/scripts/agent.py integrate --project unity-project --method scenes
python ../game-dev-agent/scripts/agent.py integrate --project unity-project --method build --day PLAY
"$(ls -dt builds/day-*/ | head -1)PawnSim.exe"   # always resolve the newest build
```
No pre-built `.exe` is committed (`builds/` is gitignored).  Full controls + flags: [`skills/game-prototype/README.md`](skills/game-prototype/README.md).

Full recipe collection for both media skills: [`EXAMPLES.md`](EXAMPLES.md).

</details>

<details>
<summary><b>Setup &amp; platform — supported OSes, prerequisites, Claude Code cost</b></summary>

**Platforms.**  macOS is the primary, end-to-end-tested platform for the media pipeline; the PawnSim build chain is Windows-primary (Unity batchmode); Linux runs mission execution but the schedulers need OS-specific adaptation.  Clone-and-go is verified on macOS.  Windows setup: [`docs/platform-windows.md`](docs/platform-windows.md).

**Prerequisites.**  macOS 14+ / Linux / Windows 11 · [Claude Code](https://docs.anthropic.com/claude-code) (only for the agent-driven path; the scripts run without it) · Homebrew or `apt` · Apple Silicon recommended (`h264_videotoolbox`, with libx264 fallback) · ~3 GB free disk · a free [Pexels API key](https://www.pexels.com/api/) for B-roll.  `scripts/bootstrap.sh` checks every tool (`ffmpeg`/`ffprobe`, `whisper.cpp`, `ollama`, `yt-dlp`, `aubio`, `jq`) and prints the exact `brew` / `apt` install command for anything missing, so a missing tool is never a silent failure.

**Claude Code cost.**  The agent-driven path spends Anthropic tokens only during orchestration; the mission scripts run standalone and burn **zero** tokens.  Operator chat usually dominates spend more than the missions themselves.  The Tier-1 / Tier-2 firewall (what stays local vs what goes to Anthropic): [`docs/cost-model.md`](docs/cost-model.md).

</details>

## Documentation

| Area | Doc |
|------|-----|
| Engineering case studies — 9 incidents, *problem → constraint → decision → artifact* | [`docs/engineering-case-studies.md`](docs/engineering-case-studies.md) |
| Architecture + full data-flow map | [`docs/architecture.md`](docs/architecture.md) |
| Music-video pipeline reference (shaders, genres, env vars) | [`docs/music-video-pipeline-reference.md`](docs/music-video-pipeline-reference.md) |
| Content-shorts pipeline — four-team research→produce⇄legal→release | [`docs/content-shorts-pipeline.md`](docs/content-shorts-pipeline.md) |
| Resolved-issue log (ffmpeg/libass packaging, etc.) | [`docs/known-limitations.md`](docs/known-limitations.md) |
| Cost model — Anthropic vs local | [`docs/cost-model.md`](docs/cost-model.md) |
| Platform / Windows setup | [`docs/platform-windows.md`](docs/platform-windows.md) |
| Operator contract — autonomy rules | [`docs/operator-contract.md`](docs/operator-contract.md) |
| Pilot decision log | [`docs/pilots/decision-log.md`](docs/pilots/decision-log.md) |

Doing a read-only review? Start at [`docs/for-analysts.md`](docs/for-analysts.md) — a single-file entry point optimized for first-pass diagnosis.

## Code / Data separation

| Layer | Path | Tracked |
|-------|------|---------|
| Code (logic) | `.claude/agents/`, `agents/`, `config/`, `scripts/` | ✓ |
| Skills (agentskills.io-spec packages) | `skills/<name>/` | ✓ |
| Data (outputs) | `records/missions/<date>/<id>/` | ✗ (gitignored) |
| Secrets | `.env` | ✗ (gitignored) |

The repository contains only the agent system itself — mission outputs (videos, transcripts, generated assets) stay local under `records/`.  What appears on GitHub is the system's own evolution, not its products.

## License

MIT. See [`LICENSE`](LICENSE).
