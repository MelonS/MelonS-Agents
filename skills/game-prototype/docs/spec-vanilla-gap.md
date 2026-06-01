# PawnSim — Vanilla the reference sim Feature-Gap Analysis

**Author:** Game Designer subagent
**Date:** 2026-05-30
**Status:** Design note only — no code, no Unity. Written in parallel with the work-priority spec and the needs/mood spec; this doc cross-references them, does not duplicate them.
**Baseline:** `docs/ROADMAP_40H.md` coverage table (~85% vanilla by system count).

> **Operator stance (load-bearing):** "기능추가는 보수적으로, 게임이 되는 게 먼저"
> — conservative on new features, playability first. Every recommendation below is ranked
> against that stance, not against "match vanilla feature-for-feature." A system that
> raises the *system-count* coverage number but doesn't make the colony loop more of a
> *game* is explicitly demoted.

---

## 0. Framing — coverage % is the wrong scoreboard

The roadmap reports 85% vanilla coverage by counting *systems present*. That number is honest
but misleading for "feels like the reference sim," because the reference sim's feel comes from **depth and
interlock of a few core systems**, not breadth. A colony sim with 22 shallow systems feels
like a tech demo; one with 6 deep, interlocking systems feels like a game.

The single biggest insight: **PawnSim has the *surface* of most systems but not the
*player-agency loop* that makes them a game.** Right now the player mostly *watches* —
pawns auto-decide, farms are pre-placed, stockpiles are decorative markers, bills don't
exist. The gap to "feels like a real game" is almost entirely a **player-decision gap**,
not a feature-count gap.

---

## 1. Present-but-shallow — depth wins, ranked by feel-impact

These systems already exist in some form. Adding *depth* here is cheaper and higher-feel
than building net-new systems, and aligns directly with "playability first."

### 1.1 — Bills / recipes at workbenches  ⭐ HIGHEST FEEL-IMPACT
- **Current:** cooking (stove) and crafting happen as one-shot/auto actions. No "do X until
  Y" instruction surface.
- **Vanilla:** every workbench has a **bill queue** — "Cook simple meal × 4", "do forever",
  "do until you have 20", ingredient radius, skill-range filter. This is *the* core player
  authoring loop. It is how the player turns "I have pawns" into "I run a colony."
- **Why it's the #1 depth item:** without bills, the player has no standing-order language.
  Bills convert the sim from "watch pawns" to "direct pawns." Almost every other system
  (cooking, crafting, butchering, smithing) plugs into the *same* bill abstraction, so one
  depth investment pays out across many workbenches.
- **Depth, not new system:** the stove already exists; this adds a repeat-count + condition
  to an existing action. Minimum playable bill = "Cook meal × N / forever, stop at food≥K."

### 1.2 — Work priorities (the 1-4 grid)  ⭐ being spec'd separately — DO NOT duplicate
- **Current:** pawns auto-pick work via utility AI; player can't express preference.
- **Cross-ref:** see the **work-priority spec** (parallel doc). That spec owns the full
  design (1-4 priority grid, per-pawn × per-worktype matrix, disabled work from traits).
- **My ranking note only:** this is the **#2 feel-impact depth item** and pairs tightly with
  bills (1.1). Bills create work; priorities decide *who* does it *first*. Ship them together
  or the player authors orders nobody obeys in the order they want. Do not re-spec here.

### 1.3 — Mood / social depth  — partially spec'd separately
- **Current:** mood exists as a need + mood-break threshold; 8 traits influence it.
- **Vanilla depth:** **thought stack** (individual buffs/debuffs with decay — "ate without
  table -3", "slept on floor -4", "saw corpse -6"), mental-break *variety* (not just one
  break), and **social relationships** (rivalries, lovers, opinion-of-each-other driving
  mood).
- **Cross-ref:** the **needs/mood spec** (parallel doc) owns the thought-stack + need-decay
  detail. My note: **the thought stack is the highest-feel piece of that spec** because it's
  what makes pawns feel like *people who react to your decisions* rather than HP bars. A
  single mood number that ticks down is a meter; a stack of named thoughts the player can
  read ("-3 slept in the cold", "+5 fine meal") is *story*. Rank thought-stack above social
  relationships for now (relationships are luxury — see §2).

### 1.4 — Stockpile zones with filters + priority  — being spec'd in part by stockpile work
- **Current:** 9 yellow dashed markers, decorative only. No filtering, no priority, no
  hauling logic driving items into them.
- **Vanilla:** stockpiles have **item filters** (allow/disallow by category, quality, hit-points)
  and **zone priority** (Critical→Low), and a **hauling job** moves loose items into the
  highest-priority accepting zone. This is what makes a base feel *organized* and is the
  backbone of logistics.
- **Feel-impact:** medium-high, but it's a **dependency**, not a standalone thrill. Bills
  (1.1) need ingredients *somewhere findable*; without functioning stockpiles + hauling, a
  bill "cook × 4" stalls because nobody gathers the rice. So stockpile-with-hauling is the
  *plumbing* that makes 1.1 actually run.
- **Minimum playable:** one global "haul loose items to nearest stockpile" job + a simple
  allow/disallow-by-category filter. Skip quality/HP filters Day 1.

### 1.5 — Health / medical depth
- **Current:** 6 body parts, bleed, downed, death. No treatment.
- **Vanilla:** **tend wounds** (bandage → stop bleed, infection chance, medicine quality),
  diseases, surgery, prosthetics. Roadmap explicitly SKIPPED bandage (Step 47) leaving bleed
  to "natural decay."
- **Feel-impact:** medium. A *tend* action closes the loop on the combat→injury→recovery arc
  that already half-exists (downed pawns exist but you can't *do* anything about them, which
  feels broken — a downed pawn just dies or doesn't, player has no agency). One tend action
  (consume medicine item, stop bleed, small infection roll) is high-value-per-effort because
  it makes the *existing* combat damage system matter.

### 1.6 — Farming designation
- **Current:** 12 pre-placed crop tiles, real growth + harvest.
- **Vanilla:** player **designates** growing zones, **picks the crop**, sets sow/harvest.
- **Feel-impact:** low-medium. Pre-placed farms already give the growth loop. Letting the
  player *draw* a zone and *choose* the crop is real agency but is a step-2 nicety once
  bills/priorities/hauling land. Don't front-load it.

---

## 2. Genuinely-missing vanilla systems — ranked by impact-per-effort

| System | Impact on feel | Effort | Playability-first verdict |
|---|---|---|---|
| **Hauling job** (move loose items → stockpile) | HIGH | LOW-MED | **CORE** — unblocks bills + stockpiles. Build first. |
| **Bills/recipe queue** (see §1.1) | HIGH | MED | **CORE** — the authoring loop. |
| **Animal taming** | MED | MED | Playability-positive but **defer** — adds a charming subsystem, not core loop. |
| **Power grid** (conduits/battery/solar) | MED | HIGH | **Scope-creep risk.** See note below. |
| **Trading caravan UI** | MED | HIGH | **Luxury** — defer. See note. |
| **Temperature / indoor climate** | MED-HIGH | HIGH | Deep the reference sim signature, but big. Defer past Day 3. |
| **Drop-pod / quest / world map** | LOW (for this scope) | VERY HIGH | **Out of scope** — pure scope-creep. |

### Notes per system

- **Hauling** — not in the original "absent" list as its own line, but it is the silent
  dependency under stockpiles *and* bills. It is the single highest impact-per-effort
  *missing* system because two §1 depth items depend on it. **This is the one genuinely-missing
  system I'd build first.**

- **Power grid** — research already gates "전기/태양광" techs, so the *tech-tree promise*
  exists but pays out nothing. That's a real coherence gap (you research electricity and
  nothing changes). BUT a full conduit/battery/consumer-load grid is high-effort and mostly
  adds *constraint-management*, not *story*. **Conservative call: build a 1-tier stub** (a
  generator + a powered light/heater that visibly turns on) only to make the research
  payoff real — do NOT build the full load-balancing grid. Flag as "tech-promise debt," not
  "must-have game system."

- **Trading caravan UI** — high-effort (caravan arrival, goods generation, silver economy,
  trade negotiation UI). It adds an economy loop but the colony isn't yet a *game* without
  bills/priorities/hauling, so trading would be furnishing an empty house. **Defer — luxury.**

- **Animal taming** — moderate effort, genuinely charming, low coupling. Good *Day-4+*
  addition once the core loop runs. Not playability-critical.

- **Temperature** — the reference sim's most distinctive survival pressure (freeze/heatstroke, food
  spoilage in/out of freezer). High feel but high effort and it cascades (needs walls to be
  airtight, needs heaters → needs power → §power-grid). **Defer; it's a Phase-2 pillar, not a
  Day-1 fix.**

---

## 3. The "feels like a real game" gap — beyond features

Even with every system above, three non-feature gaps separate "tech demo" from "game":

1. **Player-agency loop (the real gap).** The recurring theme of §1-2 is that the player
   currently *observes*. Bills + work-priority + draw-your-own-zones are what convert
   observation into authorship. **This is 80% of the "feels like a game" gap and it is a
   *depth* problem, not a *feature-count* problem** — which is exactly the operator's
   conservative thesis vindicated.

2. **Feedback & legibility.** the reference sim constantly tells you *why* things happen — the mood
   thought stack, the "needs roof" alert, the job-priority tooltip, the red event letters.
   PawnSim has TopBar + InfoPanel but lacks the **alert/why layer** (e.g., "pawn idle: no
   reachable work", "meal failed: no ingredients"). Without legibility, even working systems
   feel like a black box. *Low-effort, high-feel:* a small alert strip surfacing 2-3 standing
   problems would punch above its weight. (Coordinate with the needs/mood spec's thought
   stack — same "tell the player why" principle.)

3. **Motion / sound / UI polish — already in flight, do not re-scope.** The brief notes
   animation/motion is queued, sound is in flight, UI is done. These close the *sensory*
   tech-demo gap (static sprites → living scene). They are necessary but **not sufficient** —
   a beautifully animated colony that the player can't *direct* is still a tech demo. Polish
   and the agency loop must both land; polish alone is the trap.

---

## 4. Recommended next 3 (depth-first, conservative)

Ranked, with rationale tied to "게임이 되는 게 먼저":

### #1 — Bills/recipe queue at workbenches (depth on cooking/crafting)
The single highest-leverage move from "watch" to "play." One abstraction (repeat-count +
stop-condition + ingredient source) plugs into every existing workbench. It is the language
the player uses to *run* the colony. Pairs with #2 below. **This is what makes it a game.**

### #2 — Hauling job + functional stockpile filter (the plumbing under #1)
Genuinely-missing, low-medium effort, and a hard dependency: bills can't run if ingredients
never reach the stove and outputs never reach storage. Minimum viable = one "haul to nearest
accepting stockpile" job + allow/disallow-by-category filter. Turns the decorative markers
into real logistics and makes #1 actually execute.

### #3 — Work priorities (the 1-4 grid) — owned by the parallel work-priority spec
Ship alongside #1/#2, not after. Bills create work and hauling moves goods; priorities let
the player decide *who does what first*, which is the third leg of the authoring loop. Defer
the *implementation* design to the work-priority spec — this doc only asserts its ranking and
its tight coupling to bills/hauling.

**Why these three and not power/trading/taming:** all three are *depth on existing surfaces*
or *the minimum plumbing those depths require*. Together they convert the existing 85%-by-count
sim into a directable colony — the exact "becomes a game" threshold — without adding a single
luxury subsystem. They honor the operator's conservative stance precisely: zero glamorous new
pillars, maximum playability gain.

**Honorable mention (do #4 only if #1-3 land cleanly):** a *tend wound* action (§1.5) and a
*power-grid stub* (§2 note) — both small, both close existing coherence gaps (downed pawns you
can't help; researched electricity that does nothing).

---

## 5. Explicitly AVOID (over-scoping per operator guidance)

Do **not** build these now, regardless of how "vanilla" they are. Each is flagged as the
specific trap it represents:

- **Full power grid** with conduit routing, battery charge/discharge, per-building load.
  *Trap: high-effort constraint-management that adds bookkeeping, not story.* Build only the
  1-tier stub if the research payoff bothers you.
- **Trading caravan + silver economy + trade UI.** *Trap: furnishing an empty house — an
  economy loop on top of a colony that can't yet be directed.* Defer until the core loop runs.
- **World map / quests / drop-pods / multiple maps.** *Trap: pure scope explosion, an entire
  second game layer.* Out of scope for "make the colony a game."
- **Temperature + climate + food spoilage cascade.** *Trap: deep but it pulls in airtight
  rooms → heaters → power → freezers — a multi-system avalanche.* Phase-2 pillar, not a fix.
- **Social-relationship web** (lovers/rivals/opinion).* Trap: high-fidelity flavor before the
  player can even direct pawns.* The mood **thought stack** (needs/mood spec) delivers most of
  the "pawns are people" feel at a fraction of the cost — do that first, defer relationships.
- **Surgery / prosthetics / disease / infection severity tiers.** *Trap: medical-system
  fidelity.* A single *tend* action is enough Day-1; the rest is luxury.
- **Quality tiers / hit-points-based stockpile filters / deterioration.** *Trap: filter-system
  over-fidelity.* Allow/disallow-by-category is enough to make stockpiles real.

**One-line rule for the team:** if a proposed feature adds a *new pillar* rather than *depth on
an existing surface or the plumbing an existing surface needs*, it fails the operator's
conservative test — park it.

---

## Cross-references (do not duplicate)
- **Work-priority spec** (parallel) — owns the 1-4 priority grid design. This doc ranks it #2
  feel-impact / #3 recommended, asserts coupling to bills+hauling, defers detail to that spec.
- **Needs/mood spec** (parallel) — owns need-decay + mood thought-stack design. This doc ranks
  the thought stack as that spec's highest-feel element and defers detail to it.
