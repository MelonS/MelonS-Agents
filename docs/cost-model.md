# Cost model

Where Anthropic-API tokens are actually spent in this repository,
and where they are not.  Read this before recommending any
"token-saving" changes.

## One-sentence model

> Conversational use of Claude Code costs Anthropic tokens.
> Everything else is local and free.

## The two tiers

```
┌────────────────────────────────────────────────────────────────┐
│ TIER 1 — Conversational orchestration       Anthropic API      │
│                                                                │
│  • User ↔ Claude Code CLI                   opus + sonnet      │
│  • Top-level conversation, planning,        prompt caching ON  │
│    code review, debugging                                      │
│  • Subagent invocations via Agent tool:                        │
│      orchestrator (opus)                                       │
│      planner / resourcer (opus)                                │
│      editor / qa (sonnet)                                      │
│  • Hand-off between subagents: file-based                      │
│    (plan.md, MANIFEST.md), not message-based                   │
└────────────────────────────────────────────────────────────────┘
                                 │
                                 ▼  bash exec (no token cost)
┌────────────────────────────────────────────────────────────────┐
│ TIER 2 — Mission execution                  Local machine      │
│                                                                │
│  • agents/missions/{highlight,summarize,                       │
│      shorts-batch,faceless-short}/run.sh                       │
│  • yt-dlp     (download)                    free, local        │
│  • whisper.cpp (transcribe, Metal GPU)      free, local        │
│  • ollama     (selection + summarization)   free, local        │
│        llama3.2:3b                                             │
│        llama3.1:8b                                             │
│        qwen2.5-coder:7b                                        │
│  • Kokoro-ONNX (faceless TTS, Apache 2.0)   free, local        │
│  • ffmpeg    (cut + render + caption burn)  free, local        │
│  • Pexels Videos API (faceless B-roll)      free tier, no card │
│        200 req/hr, 20k req/month                               │
│  • All artifacts → records/missions/<…>/                       │
└────────────────────────────────────────────────────────────────┘
```

A mission run from `./agents/missions/highlight/run.sh <url>` makes
**zero Anthropic API calls**.  Trigger it from cron and the system
churns through video without spending a token.

## What costs what (cost per typical call)

Approximate, for context — exact pricing is Anthropic's to set.

| Action                                            | Tier | Cost              |
| ------------------------------------------------- | ---- | ----------------- |
| User asks Claude Code "build this feature"        | 1    | $$$ — depends on conversation length / cache hit ratio |
| Orchestrator decides "run highlight mission"      | 1    | $ — one opus turn, mostly cached |
| Subagent reads `plan.md`, writes `MANIFEST.md`    | 1    | ¢ — sonnet, small input bounded by file size |
| `./agents/missions/highlight/run.sh <url>`        | 2    | **0**             |
| whisper.cpp transcribes 10-min lecture            | 2    | **0** (M2 Metal)  |
| Ollama picks the highlight window (3B model)      | 2    | **0** (local)     |
| ffmpeg renders 60s 9:16 short with burn captions  | 2    | **0** (local)     |
| QA retry attempt 2 of 3                           | 2    | **0** (local)     |
| Nightly autonomous run picks 3 missions from queue| 1+2  | budget-capped at $5/night ([config/policies.yaml](../config/policies.yaml)) |

## Cache hit ratio matters in Tier 1

Claude Code applies Anthropic's prompt-caching defaults — system
prompts, agent-definition files (`.claude/agents/*.md`), and
recent message history are eligible for 90% discount on cached
read.  Long conversations cost less than naively expected because
the cache stays warm across turns.

The cache is *invalidated* when the system prompt or early
messages change — so renaming an agent definition has higher
real cost than its file size suggests.

## Tier 2: the design that makes mission execution free

Two deliberate choices push the heavy lifting off Anthropic:

1. **Whisper.cpp instead of Claude transcription.**  Transcribing
   a 30-minute talk via Claude API would cost real money and dump
   ~5–10k tokens into context.  whisper.cpp on M2 Metal does the
   same job offline in ~8 seconds.  See
   [`agents/lib/whisper.sh`](../agents/lib/whisper.sh).

2. **Ollama for selection / summarization.**  A 3B-parameter local
   model picks the highlight window from the segments JSON.  Quality
   is below Sonnet but more than enough for a 30–60s pick.  The
   savings are 100% of what would otherwise be Anthropic spend on
   the picker.  See
   [`agents/missions/highlight/select-highlight.prompt.md`](../agents/missions/highlight/select-highlight.prompt.md).

The price paid: model picks are noisier than Claude would produce.
The QA retry loop ([`agents/lib/retry.sh`](../agents/lib/retry.sh))
catches the worst noise; bad picks get the previous `qa-report.md`
prepended to the prompt on retry.

### When Tier 2 is the wrong default — creative stages

Tier-2 was originally applied to **every** Tier-2 stage in a mission.
On 2026-05-17 the operator surfaced a real failure of that blanket
rule: the `faceless-short` script-generation stage was running on
`llama3.2:3b` and producing scripts that didn't hold a viewer's
attention.  Specific failures observed across 4 pilots:

- Abstract "What if…" openings the model couldn't be prompted out of.
- Encyclopedia-style flat prose, no narrative shape.
- Fact-mixing (hydrogen = "10 % of body" stated as a hook, but the
  same script later cited body composition figures that only hold
  by atom count — different frame, same paragraph).

Routing the same step to Claude Sonnet via `scripts/gen-script-claude.sh`
produced scripts with strong specific-number hooks ("63 of every 100
atoms in your body is hydrogen"), proper narrative beats, and
factually coherent number framing.

The lesson: **Tier-2 makes sense for high-volume, deterministic,
mechanically-correct stages.  It does not make sense for one-shot
creative stages where quality compounds downstream.**

Heuristic for choosing the tier per pipeline stage:

| Stage type | Right tier | Why |
|---|---|---|
| Repeated, deterministic (transcribe, render, fetch) | Tier 2 | Volume × API cost would be ruinous; output is mechanically correct |
| Repeated, semi-deterministic (clip selection, keyword extraction) | Tier 2 | Volume still high; QA retry compensates for model noise |
| One-shot, creative (script hook, opening line) | **Tier 1** | Volume is 1× per mission; quality compounds (a weak hook makes the next 55 seconds invisible to the viewer); price is ~$0.01 per call ≈ negligible against Max plan quota |
| One-shot, precision-sensitive (script with mass-vs-atom-count traps) | **Tier 1** | Small model conflates close-but-distinct facts in one breath; the precision price is the entire output |

**Money firewall does NOT trigger for Tier-1 routing within a
Max-plan subscription** — the subscription is pre-paid; per-call
quota is the cost frame, not new dollar spend.  See
[`docs/operator-contract.md`](operator-contract.md) §3.

The `faceless-short` mission's script stage routes through Sonnet
when `FACELESS_SCRIPT_OVERRIDE` points at a pre-generated script
file (typically produced by `scripts/gen-script-claude.sh`).  Other
stages stay Tier-2 unchanged.

### The opposite case — `music-video` is fully Tier-2

Not every mission needs the Tier-1 escape hatch.  `music-video`
(shipped 2026-05-17, see [`agents/missions/music-video/run.sh`](../agents/missions/music-video/run.sh)) is entirely
Tier-2 at runtime — operator-supplied music + `aubio` beat/onset
detection + Pexels mood-keyword B-roll + ffmpeg.  No `claude` CLI
call, no `ollama` either.

Why this fits the rule rather than violating it: the creative-decision
load was moved **outside the mission script**.  Music selection
(taste-driven, one shot per render) is done by the operator before
calling the mission — typically via Suno's web UI or a curated free-
music source.  By the time `run.sh` runs, there is no remaining
creative judgement to make; every downstream step is mechanical
(detect beats, fetch B-roll, stitch, glitch on onsets).  The
Tier-1 routing rule says "creative one-shot stages route to Tier 1"
— if the operator pre-handles the creative input, the in-pipeline
creative load drops to zero and Tier 2 covers everything.

The takeaway: routing is per **stage**, not per mission.  A mission
can be all-Tier-2 (`music-video`, `highlight`, `summarize`,
`shorts-batch`) or hybrid (`faceless-short` — script Tier-1, the
rest Tier-2).  Pure-Tier-1 missions are not present in the system
and would only make sense if a future mission's value were entirely
in a long-form Claude generation.

## Tier 1 cost optimizations already in place

- **File-based subagent handoff.**  Subagents do not share the
  parent conversation.  They get a fresh context with only
  `plan.md` + `resources/MANIFEST.md` as input.  See
  [`for-analysts.md`](for-analysts.md) and
  [`.claude/agents/orchestrator.md`](../.claude/agents/orchestrator.md).
- **Sonnet for subagents, Opus only for orchestrator.**  See the
  subagent table in [`for-analysts.md`](for-analysts.md).
- **`docs/roadmap.md` as the source of truth.**  The agent reads
  one ordered file instead of inferring "what's next" from the
  whole git history.  Cuts orchestrator context size.
- **Auto-commit / auto-push.**  Commits happen often, so
  conversation context doesn't have to remember work that already
  landed.  Stale state is what makes long conversations expensive.

## When real money will start to matter

These are the future scenarios where token cost would jump:

- **Larger conversational rebuilds** of the agent system (like the
  one that built it).  Mitigation: that's a one-time cost.
- **Audio-fingerprint check** (deferred — see
  [`copyright-policy.md`](copyright-policy.md)) — would call an
  external service; still not Anthropic, but real money if the
  fingerprint API isn't free-tier.
- **License-string probe expansion** — already implemented for
  archive.org and wikimedia (both free APIs).  Adding paid sources
  (Vimeo Pro, Getty, etc.) would cross the money firewall and
  require explicit user confirmation per
  [`operator-contract.md`](operator-contract.md) §3.

## Bottom line for analysts

Diagnose Tier 1 (conversation) cost issues by looking at:

- Cache hit ratio (Claude Code logs it).
- Conversation length and how often the system prompt mutates.
- Whether the orchestrator re-reads files instead of using cached
  results.

Do **not** diagnose Tier 2 (mission execution) as a token cost
problem.  It isn't one.
