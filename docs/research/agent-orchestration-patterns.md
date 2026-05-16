# Field notes — agent orchestration patterns

External observations on how heavy Claude Code users (web-app
development domain) actually run multi-agent setups in 2026.
Captured here for comparison with this repo's video-pipeline
approach.  Source is field discussion with an experienced operator;
identifying details intentionally omitted.

## Observed pattern (web-app development domain)

### Hardware + account
- **1 high-spec MacBook** (full-option Apple Silicon), kept running.
- **1 Claude Max subscription** (no separate accounts).
- No additional cloud infrastructure, no separate worker machines.

### Concurrency model
- **Multiple Claude Code sessions** running simultaneously, not
  multiple subagents-within-one-session.
- "1 session ≈ 1 ticket-sized task" is the unit of work.
- 3–4 distinct projects (different directories) concurrent.
- Peak concurrency observed: ~15 sessions across all projects.

### Coordination across sessions
- **Sessions cannot directly communicate by default.**  Inter-session
  state is shared via committed Markdown files + git + an external
  ticket system (Jira / GitHub Issues / equivalent).
- "다른 메모리 보라고 하거나" — agent A is told to read a file that
  agent B wrote.  Sync is human-driven (or pull-based on each
  session start) rather than real-time push.
- A recently-added Claude Code feature ("team agent" / multi-agent
  option) reportedly enables some session-level communication, but
  the operator hasn't fully adopted it yet.

### Two deployment modes
1. **Local mode** (at desk): MacBook runs Claude Code locally,
   third-party harness available, full local tooling.
2. **Remote mode** (mobile, e.g., commute): Claude Code on the
   Anthropic cloud working against the GitHub repo directly.  No
   local harness in this mode — less powerful but accessible from
   any device with the app.

### Tooling (public references)
- **Harness plugin**: [`revfactory/harness`](https://github.com/revfactory/harness) —
  open-source Claude Code plugin that adds skill / agent management
  on top of the stock CLI.  Operator describes it as "install it,
  then ask it to build the skill/agent you want."
- **Skill collection**: [`NomaDamas/k-skill`](https://github.com/NomaDamas/k-skill) —
  Korean-language Claude skill collection that pairs with the
  harness pattern above.
- **Model routing via proxy**: `claude → proxy → claude / openai / local`.
  A proxy layer in front of Claude that can dispatch to multiple
  backend LLMs based on task type.  Implementation specifics not
  observed.

### Philosophy ("Claude-dependent")
- "99% of users run 100% Claude-dependent."
- Local LLM held to be impractical at this scale: "10× slower than
  Claude," "per-machine setup cost too high," used only when
  internet is unavailable (e.g., on a plane).
- Heavy Max-plan users do hit the weekly quota around day 5–6.

### Quota observation
- Even heavy users (15 concurrent sessions × multiple projects)
  fit within a single Max plan's weekly quota.
- Single-session, single-project users (this repo's pattern) use
  ~2% of the same quota per overnight pass — orders of magnitude
  more headroom.

## How this repo compares

| Dimension | Field-observed pattern | This repo (`MelonS-Agents`) |
|-----------|------------------------|------------------------------|
| Domain | web-app development | video production pipeline |
| Concurrency | multiple Claude Code sessions | single Claude Code session + bash background tasks |
| Claude tier load | heavy (most work flows through Claude) | light (Claude orchestrates; ffmpeg / whisper / ollama do the work locally) |
| Subagent definitions | dynamic (harness generates) | static (`.claude/agents/*.md`, 6 defined) |
| Inter-agent coordination | committed MD files + Jira | shared `docs/goal.md` + `docs/roadmap.md` + records/ outputs |
| Local LLM usage | rare / offline-only | central to mission execution (Tier 2 boundary in `docs/cost-model.md`) |
| Per-task cost (Claude tokens) | high (Claude generates code) | low (Claude judges; local tools render) |

**Both designs are valid for their respective domains.**  Video
production has a hard requirement for local heavy tools (ffmpeg,
whisper.cpp) that Claude can't perform itself, so the
Tier-1-orchestration / Tier-2-execution split is natural here.  Web
development has no equivalent "must run locally" component, so
funneling all work through Claude is cheaper than building a local
inference stack.

## Patterns potentially transferable to this repo

### Worth considering for v2+

1. **Multiple-session concurrency for batch video production**.
   When the operator wants to process N videos in parallel, opening
   N Claude Code sessions (each on a different mission directory)
   would scale better than today's single-session pattern.  Existing
   `records/missions/<date>/<mission-id>/` separation makes this
   natural — no coordination needed between sessions.
2. **Harness plugin evaluation**.  The on-the-fly skill/agent
   generation pattern is unusual.  Worth a read of
   [`revfactory/harness`](https://github.com/revfactory/harness) to
   see whether it composes with this repo's mission scripts or
   conflicts with them.
3. **Model-routing proxy**.  Useful when one subagent's task
   benefits from a different LLM (e.g., Korean output quality
   improvement via a Korean-tuned model for `summarize`).  Today
   this repo uses ollama directly; adding a proxy layer is a
   refactor, not a new feature.

### NOT worth adopting

1. **Full Claude-dependency for mission execution**.  Video
   pipeline's ffmpeg / whisper / ollama work is faster, cheaper,
   and more deterministic than asking Claude to "render a video."
   The existing Tier 2 design is correct for this domain.
2. **Multi-machine / mac-mini-fleet pattern**.  Validated by the
   field operator as well: single high-spec machine + multiple
   sessions covers any realistic concurrency for a single operator.
   Multi-machine adds coordination complexity for no compute gain.

## Open questions (pending response from field operator)

These were asked but answers haven't arrived yet.  Slots reserved
so the answers can drop in here.

### F. Proxy implementation
> What is the proxy in `claude → proxy → claude/openai/local`?
> Custom-built, off-the-shelf, or a community project?  How is
> routing decided per-call?

_(awaiting answer)_

### G. Monitoring 15 concurrent sessions
> When 15 sessions are running, how is "which session is doing
> what right now" tracked?  Custom dashboard, tmux discipline, or
> something else?

_(awaiting answer)_

### H. Team-agent option
> The recently-shipped Claude Code feature for inter-session
> communication — has the operator tried it?  What does it look
> like in practice?  Worth pulling into this repo?

_(awaiting answer)_

### I. Office mac-mini vs. mobile LTE mode
> Comparing the at-desk local-harness pattern with the on-the-go
> remote-cloud pattern — what specifically does each one do better?
> What's the practical workflow split between them?

_(awaiting answer)_

## Method note

This document is a synthesis of one extended conversation with one
experienced operator working in a different application domain
(web-apps, not video).  Generalizations may not hold for other
operators or other domains.  Treat as "one data point + reasoning,"
not "industry consensus."

Source identifying details (operator's name, employer, current
projects, repository URLs that aren't already public) are
intentionally omitted from this committed document.  Field notes
worth preserving but not appropriate for public commit live in the
agent's local memory, not here.
