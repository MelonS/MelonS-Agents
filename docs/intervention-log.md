# Intervention log — the substance of operator-shaped decisions

Chronological record of **operator interventions that shaped the
system**.  This is the qualitative companion to:

- [`docs/metrics/intervention.json`](metrics/intervention.json) — the
  *quantitative* trend (per-day count + ratio + leverage).
- [`docs/autonomous-decisions.md`](autonomous-decisions.md) — the
  *agent's* unilateral decisions during autonomous runs.

What this file captures that the other two don't: the **SUBSTANCE**
of each intervention — what the operator surfaced, why it mattered,
what shipped as a result, and what kind of intervention it was.
Without this, the chart shows "ratio is 30%" but loses the actual
shaping signal.  Future case-studies, hiring evidence, and
post-incident reviews need substance, not just count.

## Privacy contract

- **No verbatim prompts.**  Synthesize, don't paste.  Operator's
  raw Claude Code prompts contain context, sometimes typos, often
  personal references — they fail `[[no-pii-in-repo]]`.
- **No internal-conversation quotes longer than ~50 chars.**  Short
  paraphrased Korean is fine ("operator wanted shader restraint");
  extended verbatim is not.
- **Synthesize the nuance, not the speech.**  Capture what the
  operator's intent was, not how they phrased it.

## Entry format

Each entry under today's date section:

```markdown
- **`HH:MM KST`** — one-line synthesized summary (≤ 100 chars)
  - **why**: the constraint, insight, correction, or taste judgment that
    made this an operator-initiated decision (≤ 150 chars)
  - **shipped**: commit hash(es) or file path(s) that resulted (or
    "deferred" / "rejected" / "research-only" if no commit landed)
  - **tag**: one of:
    - `direction` — operator pointed at a new objective or scope shift
    - `taste` — operator approved / rejected / refined an aesthetic
      decision (mp4 picks, copy style, palette choice)
    - `correction` — operator pointed out a wrong assumption or
      bug in agent reasoning
    - `hypothesis-rejection` — operator dismissed a planned approach
      after evidence (e.g., spot-check, A/B)
    - `preference` — operator stated a personal style preference
      that became a default
    - `guard` — operator added a safety rule, money firewall, or
      privacy constraint
    - `constraint` — operator added a real-world limit (budget,
      timeline, compatibility)
```

## Helper

```bash
scripts/intervention-log-add.sh "summary" \
  --why "constraint or insight" \
  --shipped "<sha> or <path>" \
  --tag direction
```

Auto-prepends an entry under today's date section.  Creates the
date header if today isn't present yet.  Append-only.

---

## 2026-05-22

- **`16:30 KST`** — Surfaced need to record intervention *substance*, not just count
  - **why**: quantitative chart shows ratio but loses the shaping signal; future case-studies + hiring evidence + repo development positive ROI
  - **shipped**: `docs/intervention-log.md` + `scripts/intervention-log-add.sh` + backfill from commit history
  - **tag**: direction

- **`14:50 KST`** — Floated intervention-record idea after 2h autonomous window started
  - **why**: thinking ahead about how the system documents its own evolution
  - **shipped**: this file
  - **tag**: direction

- **`13:30 KST`** — Reported install-claude-local bug visible on operator machine (9 stacked decorative openers)
  - **why**: visible UX failure of an idempotent script; class of bug that recurs on every install
  - **shipped**: `7f44c59` + `482500f` + `[[idempotency-test-first]]` memory entry + test
  - **tag**: correction

- **`13:00 KST` (approx)** — Requested intervention chart be split EN/KO + visually polished
  - **why**: chart was hard to read; English-only labels in KO context create cognitive friction
  - **shipped**: `46a8fe7` — bilingual chart + visual polish + chart auto-mirror to site
  - **tag**: taste

- **`12:00 KST` (approx)** — Reauthorized autonomous work mid-session ("여기도 자율로 가능한거 하고 있어")
  - **why**: agent had paced down to monitor mode; operator wanted continued progress
  - **shipped**: 8+ commits in the second autonomous window (test-install-claude-local, test-log-decision, doctor intervention-trend, roadmap-done-sync, operator-tooling catalog)
  - **tag**: direction

- **`11:30 KST` (approx)** — Asked for the time-stamp `[관리자 브리핑]` brief status format
  - **why**: returning operator needs zero-cognitive-load status absorption
  - **shipped**: continued via existing dual-stack reporting pattern; morning-brief.sh consolidates further
  - **tag**: preference

- **`02:30 KST`** — Authorized first overnight autonomous window with intervention-reduction focus until 11 AM
  - **why**: operator out for ~8h; system needs progress on the meta-axis (intervention monitoring + reduction) without supervision
  - **shipped**: 26 commits during 02:30–11:00 KST window — chart restoration, 5 reduction levers, audit-hook coalescing, morning-brief, decision log, doctor signals
  - **tag**: direction

- **`02:00 KST`** — Surfaced multi-dimensional intervention tracking requirement
  - **why**: simple commit-count chart misses operator *time* engaged; high-leverage autonomous days and high-touch live-coding days both produce ~10 commits
  - **shipped**: `d0afd03` — Panel B mining of Claude Code session JSONLs for prompt count + active minutes
  - **tag**: direction

- **`01:35 KST`** — Surfaced that intervention chart was missing from README
  - **why**: chart added 2026-05-17 was silently dropped in 2026-05-18 README rewrite; data 2 days stale
  - **shipped**: README EN+KO "Autonomy signal" section restored + chart regen scheduler
  - **tag**: correction

- **`01:30 KST`** — Stated 6 music-video quality directives (parallel session, not this autonomous track)
  - **why**: B-roll reuse, shader restraint, lyric sync, shader vocabulary, KR-lyric → KR-person, EN-lyric → global match
  - **shipped**: parallel session — quality-bar Phase A.1 + A.2 + A.3 + B.1 + C.1
  - **tag**: taste

## 2026-05-21

- **`~15:00 KST`** — Surfaced "best company you can plausibly get into" framing for job-hunt scoring
  - **why**: role_fit alone overweights prestige; hire_prob captures realistic chance
  - **shipped**: `6b61f84` — fit-score composite (0.6 × role_fit + 0.4 × hire_prob) + operator-profile Hire-bar comfort section
  - **tag**: direction

- **`~14:30 KST`** — Insight on KR-domestic vs global company candidate-pool bias
  - **why**: global companies are higher-prestige but lower-hire-probability; need KR-domestic surfaced
  - **shipped**: `cc6a104` — 4 KR companies on Greenhouse added + Saramin OpenAPI live path activated
  - **tag**: direction

- **`~14:00 KST`** — Direction to relax §6 branch strategy from strict to flexible
  - **why**: thirtieth audit caught 9 structural commits landing on main across two parallel sessions; rule that can't be kept under realistic conditions is worse than a softer guideline
  - **shipped**: `6ddba86` — §6 revised + worktree-new/done helpers
  - **tag**: direction

## 2026-05-19

- **`~17:40 KST`** — Flagged first-touch friction as CRITICAL after in-person friend test
  - **why**: ~10 prompts faced by qualified user produced bounce, not output; multi-skill framework value gated on this
  - **shipped**: `3bec8e9` + later `feat/permission-bootstrap` v0.3.0 + first-touch.sh wizard
  - **tag**: correction

## 2026-05-18

- **`~19:50 KST`** — Stated multi-skill framework vision verbatim (helping people, own job, others' jobs, livelihood)
  - **why**: project pivot from music-shorts to general framework; needed before any further skill work
  - **shipped**: `8b39cac` — promote multi-skill framework to active goal
  - **tag**: direction

## Earlier history

Most pre-2026-05-18 interventions are NOT backfilled into this log
because the chart's commit-classifier doesn't reliably distinguish
the *substance* of the intervention from older commit bodies.  Use
`git log --grep='operator\|user'` + `docs/daily/<date>.md` as the
source for earlier reconstruction if a case-study needs to cite a
specific older intervention.
