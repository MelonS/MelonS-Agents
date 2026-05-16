# Ideas — parking log for v2+ concepts

## Purpose

A place to record ideas and improvements that come up during v1 development
**without implementing them immediately**.  v1-안정화 전까지 메인 파이프라인
외 구현 금지가 약속이고, this file is the device that holds that promise.

If an idea matters, it goes here first.  Implementation happens only after
v1 stabilizes and the item gets promoted to `docs/roadmap.md`.

## Writing rules

- New ideas go at the **top** of the relevant category section (newest first).
- Header format: `### YYYY-MM-DD | Title | Priority (L/M/H)`
- Body covers: motivation, rough implementation sketch, dependencies,
  estimated cost (tokens / time), current status.
- When an item is promoted to active work, **move it** to
  `docs/roadmap.md` (don't leave a copy here).
- When an item is rejected, leave the title with ~~strikethrough~~ and
  one line on why (so the rejection itself is durable knowledge).

## Categories

Three sections to start.  Split further only when one section exceeds ~10
live entries — empty subcategories are noise.

1. **Agents** — new subagents, changes to existing agents.
2. **Pipeline + Infrastructure** — mission flow, handoff protocols, retry
   logic, schedulers, backups, monitoring, token management.
3. **Intelligence + Misc** — external information gathering, trend
   detection, ideas that don't fit elsewhere.

---

## Agents

_(none yet)_

---

## Pipeline + Infrastructure

### 2026-05-16 | 4-tier autonomy model (replace Layer 1 / Layer 2 binary) | M

**Motivation**: the current "Layer 1 (main conversation) decides
everything, Layer 2 (subagents) are pure functions of their prompt"
model is too coarse, and operator caught it.  The auditor already
runs **partially independent**: launchd wakes it without Layer 1,
it makes real autonomous judgments (severity classification, verdict
synthesis), and writes to a stable channel
(`docs/audit/CURRENT-ALERT.md`) that the next Layer 1 session is
contractually obliged to read.  Calling that "Layer 2" undersells
its actual separation.

The single-Layer-1 model also creates real problems:
- Layer 1 = single point of decision-making.  When no session is
  active, priority-judgment stops.  A CRITICAL audit alert can sit
  unread for days if sessions are sparse.
- Three operationally-different kinds of work (real-time
  decisions / mission decomposition / periodic monitoring) all
  forced through the same orchestration path.
- The setup assumes one machine + one operator + frequent sessions.
  Multi-machine deployment (e.g., Mac orchestrator + Linux GPU
  worker) doesn't fit.

**Sketch — four explicit autonomy tiers**:

| Tier | Pattern | Examples | Autonomy scope |
|------|---------|----------|----------------|
| **Interactive** | user conversation | main conversation Claude | day-level decisions, goal selection |
| **Mission** | invoked per task | orchestrator + planner / resourcer / editor / qa | mission decomposition, task-scoped autonomy |
| **Monitor** | scheduled / event-triggered | auditor (today); future cost-watcher / backup-watcher | observe state, classify, alert; reversible auto-fix within own domain (e.g., regenerate metrics, clear empty dirs) |
| **Action** | external side-effects | publish.sh / deploy / push to Slack (not built yet) | always behind explicit user OK; money firewall reinforced here |

Auditor would migrate Layer 2 → Monitor tier with slightly expanded
autonomy: can do reversible self-clearing fixes (metric refresh,
caption-verify regeneration) but never edits agent definitions or
external systems.  Mission subagents stay Mission tier.

**Multi-machine readiness (further-future deliverables)**:
- Replace file-based handoff (`records/`, `docs/audit/`) with a
  message-queue or RPC layer when more than one host is involved.
- Decide records sync mechanism (S3 / syncthing / git-lfs — each has
  trade-offs).
- Add a push-notification channel for Monitor-tier CRITICAL findings
  so they don't wait for the next interactive session.
- Per-tier credentials / scope so a single compromised host doesn't
  give full system access.

**Dependencies**: v1 fully stabilized; at least two cooperating
agents running on different schedules in production (Monitor tier
gets meaningful only when there's enough monitoring volume to
matter); user-driven push of needing multi-machine.

**Estimated cost**: design-only at first (1 day to write the
contract); per-component delivery later (~1–2 days each — Monitor
auto-fix scope, alert escalation channel, multi-host sync).

**Status**: parked (v2+).  Direct operator quote that surfaced this:
"Layer1이 모든걸 다 판단하는게 맞나 싶기도 하고 ... 감시자 같은건
사실 어느정도 별개로 돌아야 하는거 아닌가 싶기도 하고".  Captures
a real architectural ceiling in the v1 design; don't lose it.

---

## Intelligence + Misc

### 2026-05-15 | Scout agent (external information gathering) | M

**Motivation**: Multi-agent harness construction precedent is thin in
Korean-language sources, so external community signal has outsized
value.  Manual community monitoring costs time.  Collected signal can
feed back as candidate missions — gives the orchestrator something to
chew on when the user-supplied queue is empty.

**Sketch**:
- Separate mission track; runs independently of the shorts pipeline.
- Sources: Reddit (`r/ClaudeAI`, `r/LocalLLaMA`, `r/AI_Agents`),
  GitHub trending (claude-code / agent topics), GeekNews RSS,
  Hacker News Algolia API.  X / Twitter held back pending cost review.
- Tier 2 (local, free) does the fetching: `curl` + small Python.
- Stage 1 filter — keyword regex, local, no tokens.
- Stage 2 — classify + summarize each surviving item with Haiku.
  Sonnet is over-spec for this; reject any temptation to upgrade.
- Optional daily digest — Sonnet allowed if it adds judgement, not
  just formatting.
- Storage: `records/intel/<date>/` (gitignored — never commit
  scraped third-party content).

**Dependencies**: v1 stabilized; QA retry loop already shipped (good).

**Estimated cost**: Haiku tokens trivial (low thousands / day).
Build effort 1–2 days.

**Status**: parked (v2+).

---

## Rejected ideas

_(none yet — this section exists so future-you knows that strikethrough
entries are intentional and reasoned, not accidental clutter.)_
