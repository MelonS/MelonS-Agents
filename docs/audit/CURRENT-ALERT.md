# Current audit alert

> This file is written by `scripts/audit-run.sh` whenever the most
> recent audit has a non-CLEAN verdict.  It is auto-removed on the
> next CLEAN run.  Do not edit by hand — fix the underlying findings
> and re-run the auditor.

**Verdict**: DRIFT_DETECTED
**Full report**: [`docs/audit/2026-07-31-contract.md`](2026-07-31-contract.md)
**Generated**: 2026-07-31 03:39:04    

## Summary (from audit)


Focused pass over `docs/operator-contract.md` compliance at HEAD `45a23d2`
(2026-07-31 03:30 KST), cross-checked against `docs/for-analysts.md`,
`docs/architecture.md`, `docs/roadmap.md`, `docs/goal.md`, every
`.claude/agents/*.md` frontmatter, `.gitignore`, `.github/workflows/`, and
`git log`. The pre-computed skill-drift report is clean (0 findings) and is
not repeated below. `main` is green (`gh run list` shows 5/5 recent
`main-protection` runs `success`). Secret scan outside `docs/`, `*.example`,
`*.md` returned nothing. `.env` remains gitignored and untracked. No output
artifacts (mp4/wav/jpg/etc.) are committed under `agents/` or `scripts/`.
The section-8 hardcoded-path exception registry's 11 listed files all still
carry the required grep-anchor; several additional Users-shaped path strings
found under `scripts/` are all inert comments (illustrative paths, an
already-fixed-and-annotated historical reference, a stale test-fixture
comment shadowed by a REPO_ROOT-relative path in the actual code) and are
not new registry gaps.

One genuinely good-news item: the CRITICAL "audit reports generated but
never committed" finding that has now reproduced across six consecutive
audit cycles was fixed at the code level today, in-session, immediately
before this run — commit `45a23d2` adds `commit_audit_trail()` to
`scripts/audit-run.sh`, which now runs `git commit --only` scoped exactly
to `docs/audit/` (report + `CURRENT-ALERT.md`) and best-effort pushes, with
merge/rebase/lock guards and an `AUDIT_NO_COMMIT=1` escape hatch. The commit
message documents a temp-clone self-test (no-op path, new-report path,
unrelated-staged-changes preserved, idempotent re-run). This is a
`scripts/` change, not an `agents/*.md` or `.claude/agents/*.md` change, so
it is outside hard rule 5's scope and needs no Requested-by marker; it is
downgraded from the standing CRITICAL to informational below pending one
live, unattended trigger cycle (L1/L2/L3) actually exercising the new
commit path, since every test cited in the commit message was run
interactively in a temp clone rather than through the real
hook/launchd/Task-Scheduler path.

Two problems remain live from the 2026-07-26 report, both still unresolved
five days later: `docs/roadmap.md` "Now" (own last-updated stamp:
2026-05-20) still frames the active work as multi-skill-framework /
job-hunt-v0.4.0-activation, while `docs/goal.md`'s actual Active goal has
been PawnSim since 2026-06-12 (49 days) and the most recent five commits are
about a duplicate-upload incident fix, a shorts scene-layout mode, and the
audit-trail self-commit fix — none of which touch either the stated Now or
the actual goal; and `.claude/wb/` (94 tracked JSON files, last touched
2026-07-01) remains outside `docs/architecture.md`'s Layers table with no
gitignore rule and no section-8 deviation marker. `ta.md` also still has no
`model:` field, unlike the other 26 subagent definitions, and the same six
section-5-scope commits from 2026-06-01 through 2026-07-24 still lack the
Requested-by marker (no new section-5-scope commits landed since the last
audit, so the set is unchanged, not growing). One new finding this cycle:
`README.md`/`README.ko.md` are now self-inconsistent — the hero-stats image
plus alt text (line 31, both files) still claims 23 subagents while the
prose two paragraphs down (line 177 EN) correctly says 27 — and
`for-analysts.md` undercounts the CI gate by one (it says "six static
checks"; the workflow has had seven since `06dd751` on 2026-07-01, when the
README EN-KO parity check was added).

## Critical / High findings

- **[high]** `docs/roadmap.md` "Now" section stale against `docs/goal.md` Active goal and the last 5 commits, unresolved 2nd consecutive audit cycle — `docs/roadmap.md:16-50`, `docs/goal.md:19`

## How to clear this alert

1. Read the full report linked above.
2. Resolve each critical / high finding (suggested fixes are in the report).
3. Re-run `./scripts/audit-run.sh` — verdict returning to CLEAN auto-removes this file.
