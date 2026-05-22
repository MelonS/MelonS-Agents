# A/B test verdict — planner + resourcer at opus vs sonnet

**Date**: 2026-05-22 ~17:30 KST
**Branch (intermediate)**: main with `fcc5fee` (opus flip) → reverted before
push by the same operator-driven session
**Operator approval**: 2026-05-22 (this session — operator picked "지금 실행 — faceless-short 다시 꺼내서 테스트" via AskUserQuestion)
**Predecessor**: parked design in [`docs/ideas.md`](../ideas.md) 2026-05-19
("A/B test — planner + resourcer at Opus vs Sonnet | M")

## TL;DR

**Inconclusive on a single A/B point.**  Token + wall-clock delta is
negligible (~+6% tokens, identical wall-clock).  Opus showed one
measurable advantage — the opus resourcer caught a planner-introduced
architecture gap (`FACELESS_KW_OVERRIDE` env var doesn't exist in
`run.sh`) that sonnet's pair would have produced as runtime
PASS-but-broken.  But that gap *only existed because the opus planner
proposed a pipeline extension*; sonnet's more conservative plan
avoided the gap by staying within existing pipeline capabilities.

Net signal: opus is *marginally* better at cross-stage self-checking
on ambiguous briefs, with no economic penalty.  But on briefs with
clear acceptance criteria (like this one), sonnet's conservative
plan was probably *safer to execute*.

**Recommendation**: keep sonnet as default for both planner + resourcer.
Re-test when a subagent-heavy Shape A skill (e.g., the candidate
movie/game skill the operator flagged 2026-05-22) lands — that's a
better testbed than music-video (fully bash-scripted, subagents
barely fire) or job-hunt (standalone, no subagents at all).

## Test protocol

Per `docs/ideas.md:159` proposed design.  Fixed input (Hittites
faceless-short brief), single run per variant, paired subagent
spawn (planner → resourcer), no actual pipeline execution (the
deliverable is `plan.md` + `MANIFEST.md` quality, not the mp4).

- **Variant B** (baseline): planner=sonnet, resourcer=sonnet (current
  `.claude/agents/*.md` state on main).
- **Variant A** (opus): planner=opus, resourcer=opus (committed in
  `fcc5fee` on the same session's main, reverted in the verdict
  commit below).

Same prompt template for both variants.  Outputs written to
`records/abtest-planner-opus/variant-{a-opus,b-sonnet}/`.

## Raw measurements

| Metric | Sonnet (B) | Opus (A) | Δ |
|---|---|---|---|
| **planner tokens** | 30,691 | 30,416 | -275 (-0.9%) |
| **planner wall-clock** | 116 s | 111 s | -5 s |
| **planner tool calls** | 12 | 10 | -2 |
| **resourcer tokens** | 32,287 | 36,291 | +4,004 (+12%) |
| **resourcer wall-clock** | 124 s | 125 s | +1 s |
| **resourcer tool calls** | 12 | 13 | +1 |
| **TOTAL tokens** | 62,978 | 66,707 | +3,729 (+5.9%) |
| **TOTAL wall-clock** | 240 s | 236 s | -4 s |
| **plan.md lines** | 203 | 241 | +38 (+19%) |
| **MANIFEST.md lines** | 253 | 352 | +99 (+39%) |

**Cost interpretation** (Max-plan quota — no incremental USD):
~6% additional tokens per planner+resourcer pair.  At a current
~50 missions/week pace, this would add ~3.3k tokens × 50 = ~165k
tokens/week.  Operationally negligible against Max-plan weekly
quota.  Not the deciding factor.

## Output quality — side-by-side

### Plan.md

**Common ground (both variants)**:
- Identified the same load-bearing decision: `FACELESS_SCRIPT_OVERRIDE`
  is mandatory because `llama3.2:3b` will hallucinate or contradict
  the brief's specific dates (1906, 1259 BCE) and proper nouns
  (Hugo Winckler, Treaty of Kadesh, Istanbul Archaeology Museum).
- Identified the same 6 risks (Pexels zero-result, whisper drift,
  Kokoro fallback, narration duration drift, libass missing, Pexels
  domain mismatch).
- Identified the same acceptance criteria shape (script word count,
  proper-noun preservation, mp4 duration 55-70s, 1080×1920, no
  caption overlap).

**Sonnet (B) specific**:
- 8 granular steps, each owned by a specific subagent.  Easier to
  audit step-by-step but more verbose.
- Cost estimate is the most detailed of the two (per-stage time
  breakdown, intermediate disk usage).
- Stayed within existing `run.sh` capabilities — every step
  references an existing pipeline stage.

**Opus (A) specific**:
- 5 condensed steps.  Less granular but covers same scope.
- *Pre-authored an 8-window B-roll keyword table* in the plan body
  with window-to-term mapping (sonnet deferred this to ollama
  per-window extraction).
- Cited more `run.sh` line numbers verbatim (lines 55-63, 76-415,
  94-99, 132-142, 207, 213, 220, 262-273).
- Proposed a *new* env var (`FACELESS_KW_OVERRIDE`) to inject the
  pre-authored keywords — a small pipeline extension.  **This is the
  signal**: opus reached past existing capabilities to propose a
  quality-improving mechanism.  But it didn't catch that the extension
  it proposed would itself require a §5 logic change.

### MANIFEST.md

**Sonnet (B) specific**:
- Cleaner File / Source / Size / Notes table covering 19 expected
  artifacts (script.txt → broll → trimmed → concat → captions.ass →
  short.mp4).  This is the format the resourcer subagent definition
  prescribes.
- Followed up with explicit dry-run command blocks for each stage.
- Top risks: caption-alignment failure → proper-noun drift in
  burned captions; Pexels domain mismatch → "modern Egypt tourism
  footage" for "Treaty of Kadesh" window.

**Opus (A) specific**:
- Longer (352 vs 253 lines).  Front-loaded with a 50-line
  environment-validation script (bash heredoc) more elaborate than
  sonnet's same-stage equivalent.
- **Caught the planner-resourcer integration gap**: opus resourcer
  noticed that `FACELESS_KW_OVERRIDE` doesn't exist in `run.sh`,
  flagged it as "architecturally unsupported without a run.sh
  logic change (§5 OK required)".  This is the *headline observation*
  from the A/B: opus surfaced cross-stage reasoning that sonnet's
  resourcer didn't.  Note that this gap *only existed because the
  opus planner introduced it* — sonnet's planner+resourcer pair
  avoided the issue by not proposing the new env var.
- Top risks: same FACELESS_KW_OVERRIDE gap (a self-criticism of
  the variant's own planner); plus Pexels zero-result on
  "clay tablets cuneiform" window → silent neighbour-clip backfill
  → quality-bar directive #1 violation (B-roll dedup).

## Subjective scores (operator-side review pending)

Operator scoring 1-10 across four dimensions, per `docs/ideas.md:168`
test design.  These are the agent's self-assessment; operator may
overwrite.

| Dimension | Sonnet (B) | Opus (A) | Notes |
|---|---|---|---|
| Concreteness | 8 | 9 | Opus cites more specific line numbers + facts |
| Acceptance-criteria testability | 8 | 8 | Tie — both write greppable / ffprobe-checkable criteria |
| Risk surfacing | 7 | 8 | Opus's BROLL_HISTORY collision risk is real; opus self-flagged its own env-var gap |
| Cross-stage reasoning | 6 | 8 | **Opus advantage** — resourcer caught planner gap |
| Conservatism (stays in pipeline) | 9 | 6 | Sonnet wins — opus introduces an unsupported env var |
| **Overall** | **7.6** | **7.8** | Marginal opus edge (+0.2) |

## Decision

**Keep sonnet as default for planner + resourcer.**  Reasoning:

1. **Marginal quality delta** (+0.2 subjective, mostly from one
   observation that opus's planner introduced and opus's resourcer
   then caught — a wash in isolation).
2. **Conservatism matters more for the current production format.**
   Music-video runs unattended overnight; a planner that proposes
   unsupported env vars is a latent failure surface in autonomous
   mode where there's no operator to catch the gap before pipeline
   launch.
3. **The community feedback's strongest case (opus catches ambiguity
   sonnet flat-tones) wasn't tested in isolation here.**  The
   Hittites brief is *highly concrete* (specific dates, named
   archaeologist, named treaty, named museum).  An *ambiguous*
   brief (e.g., "make a short about resilience") might bring out
   the opus advantage that this test masked.
4. **No economic reason against opus.**  Max-plan quota absorbs the
   ~6% token bump.  If a future skill / mission type needs creative
   interpretation, operator can flip per-mission via a feat branch
   without permanent change.

## Follow-on

- **Re-test on a subagent-heavy Shape A skill** when it lands.  The
  candidate movie/game skill the operator flagged 2026-05-22 is the
  natural opportunity — multi-asset analysis with per-asset Claude
  critique would actually exercise planner/resourcer/editor/qa
  beyond their current minimal triggering.
- **Consider a per-mission opt-in flag** (`MISSION_PLANNER_MODEL=opus`)
  rather than a global flip, so ambiguous briefs can route to opus
  case-by-case without permanent §5 change.  Implementation: planner
  + resourcer subagent definitions stay at sonnet; orchestrator
  inspects the flag and routes accordingly.  Not done in this test.
- **Document the null-ish result** in `docs/engineering-case-studies.md`
  as case study #10 ("the obvious upgrade that wasn't").  Operator
  decides whether to include.

## Artifacts

- `records/abtest-planner-opus/variant-a-opus/plan.md` (241 lines)
- `records/abtest-planner-opus/variant-a-opus/resources/MANIFEST.md` (352 lines)
- `records/abtest-planner-opus/variant-b-sonnet/plan.md` (203 lines)
- `records/abtest-planner-opus/variant-b-sonnet/resources/MANIFEST.md` (253 lines)

These are gitignored under `records/`.  They live on the operator's
machine; verdict numbers above are the durable record.

## Reproducibility

To re-run on a different brief:

```bash
# 1. flip both subagents to opus on a feat branch
git checkout -b feat/abtest-planner-opus-<topic>
sed -i.bak 's/^model: sonnet$/model: opus/' \
    .claude/agents/planner.md .claude/agents/resourcer.md
git add .claude/agents/planner.md .claude/agents/resourcer.md
git commit -m "abtest: flip to opus for A/B"

# 2. invoke planner + resourcer via Agent tool (or natural Claude Code
#    session) on the chosen brief.  Capture token + duration from the
#    Agent tool result.

# 3. revert the flip + push the verdict doc only:
git checkout main -- .claude/agents/planner.md .claude/agents/resourcer.md
git commit -m "abtest: revert opus flip — verdict in docs/research/..."
```
