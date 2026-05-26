# Operator decision queue

> Decisions agent could not make autonomously, accumulated for operator
> on next return.  Append-only.  Operator removes / answers / annotates
> entries as they're addressed.
>
> Latest entries at the bottom.  Each entry has:
> - **id**: stable reference
> - **when**: KST timestamp the question arose
> - **context**: what triggered it
> - **default**: what the agent did in the meantime (so progress isn't blocked)
> - **decision needed**: precise question
> - **options**: enumerated choices
> - **impact**: what changes based on the answer

---

## OPQ-001 · sprite asset licensing direction

- **when**: 2026-05-26 ~21:30 KST (post Day 7 sprint)
- **context**: Operator reviewed `Assets/Sprites/` (SDXL-Turbo generated)
  and called them "너무 구림".  Suggested fetching from asset sites
  with proper licensing.
- **default (in flight)**: Switching to **Kenney.nl** as primary source.
  Kenney's packs are **CC0** (public domain dedication, no attribution
  required) and visually consistent (top-down pixel art).
  Specifically replacing pawn / grass tile / tree with Kenney's
  "Tiny Town" or "Tiny Battle" or "Roguelike Caves & Dungeons" pack.
- **decision needed**:
  1. Approve Kenney.nl CC0 as primary asset source?
  2. Allow CC-BY (with attribution recording in `ATTRIBUTIONS.md`) as
     fallback when Kenney lacks coverage?
  3. Reject CC-BY-SA / GPL (viral) and any non-commercial-restricted
     licenses?
- **options**:
  - (a) **CC0 only** — strictest, safest for commercial use, may lack variety
  - (b) **CC0 + CC-BY** — broader pool, just need attribution file in repo
  - (c) **CC0 + CC-BY + CC-BY-SA** — wider still, but Steam-release-risky
  - (d) **Custom (paid licensing later)** — defer commercial sourcing till Steam ramp
- **impact**: Determines which asset pools agent's `resourcer` can pull from.
  Default (a) chosen autonomously since it's least risky.

---

## OPQ-002 · multi-agent code generation backend

- **when**: 2026-05-26 ~21:35 KST
- **context**: Multi-agent refactor adds a `coder` subagent that
  generates C# from natural-language spec.  Needs an LLM backend.
- **default**: Wrapping the **Anthropic Python SDK** (`anthropic`)
  with `ANTHROPIC_API_KEY` env var.  Falls back to no-op if key not set.
  Pattern matches Skill #1 / #2 design intent.
- **decision needed**:
  1. Operator's `ANTHROPIC_API_KEY` set on this machine? (currently no
     env var detected at agent init)
  2. Per Claude Max $200/mo plan, API calls are absorbed — confirm OK
     to consume from this plan?
- **options**:
  - (a) **Use ANTHROPIC_API_KEY** — operator sets env var on machine,
        coder subagent calls Claude API for C# generation
  - (b) **Use local LLM (Ollama)** — slower but no API cost, runs
        wherever Ollama is set up
  - (c) **Defer** — coder remains a manual template-filling subagent
        for now (less impressive but still functional)
- **impact**: Determines whether subsequent prototypes can be built
  end-to-end without Claude in this conversation session.

---

## OPQ-003 · second prototype target after RimWorld-lite

- **when**: 2026-05-26 ~21:40 KST
- **context**: Operator's strategic priority = multi-agent framework
  validated by *multiple* prototypes (not just RimWorld-lite).
  Need to pick next target after framework refactor.
- **default**: **Suika Game clone** (simple physics merge puzzle) —
  smallest scope, fastest validation that the framework actually
  accelerates new-prototype creation.
- **decision needed**: which 2nd prototype?
- **options**:
  - (a) **Suika Game clone** — 1-2 days target, physics-driven, very
        different from RimWorld-lite (good framework stress-test)
  - (b) **Vampire Survivors lite** — 3-4 days, action-driven
  - (c) **Brotato lite** — 3-4 days, similar genre to (b)
  - (d) **대항해시대 라이트 prototype** — operator's passion project,
        2-4 weeks (too big for framework-validation purposes)
  - (e) **Skip** — focus on improving RimWorld-lite instead
- **impact**: After framework refactor, agent will start whichever
  is picked.

---

## OPQ-004 · YouTube Mix #2 / Mix #3 disposition

- **when**: 2026-05-26 ~22:00 KST (carried over from earlier session)
- **context**: Mix #2 published auto on YouTube channel ToddStudio
  (video id `7f7PeuNuIfI`).  Mix #3 hero-loop test mp4 ready on disk
  but not uploaded.
- **default**: No change — Mix #2 live, Mix #3 stays on disk.
- **decision needed**:
  1. Take down Mix #2?
  2. Upload Mix #3 hero-loop (snow + rhythm) as Mix #3 or v2 of Mix #2?
  3. Both stay (no further music work)?
- **options**:
  - (a) **Mix #2 stays public, Mix #3 stays local** — drift-data collection
  - (b) **Replace Mix #2 with Mix #3 hero-loop** — operator preferred direction
  - (c) **Upload Mix #3 alongside Mix #2** — A/B test
  - (d) **Both down, focus 100% on Skill #3** — clean cut
- **impact**: Channel positioning + future mix work.

---

## OPQ-005 · machine 24/7 power + agent uptime expectations

- **when**: 2026-05-26 ~21:45 KST
- **context**: Operator stated 1-month full uptime planned.
- **default**: Continuing 24/7 power settings as configured 2026-05-25
  evening (sleep/hibernate off, no auto-reboot, Windows Update during
  off-hours).
- **decision needed**:
  1. Auto-restart Claude Code (this agent's runtime) on machine reboot?
     Currently NO — operator manually opens terminal + claude code.
  2. Run a watchdog that pings ComfyUI / Unity / agent and restarts on
     failure?
- **options**:
  - (a) **No automation** — operator manually opens claude session each
        morning
  - (b) **Auto-start watchdog** — agent runs every N minutes via Task
        Scheduler, restarts crashed services
  - (c) **Full autonomous mode** — agent picks up tasks from a queue
        file automatically (closer to a true production system)
- **impact**: How much of "1 month uptime" actually translates to
  agent productivity vs idle machine.
