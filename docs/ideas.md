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

_(none yet)_

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
