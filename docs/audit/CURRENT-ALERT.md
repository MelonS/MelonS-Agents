# Current audit alert

> This file is written by `scripts/audit-run.sh` whenever the most
> recent audit has a non-CLEAN verdict.  It is auto-removed on the
> next CLEAN run.  Do not edit by hand — fix the underlying findings
> and re-run the auditor.

**Verdict**: DRIFT_DETECTED
**Full report**: [`docs/audit/2026-05-17-contract.md`](2026-05-17-contract.md)
**Generated**: 2026-05-17 18:09:21 KST

## Summary (from audit)


Fifteenth contract-focused audit of the session; HEAD = `e793662`
(2026-05-17 ~19:00 KST).  Supersedes the fourteenth audit at HEAD `579ef3a`.
Scope: all twelve hard rules in `docs/operator-contract.md`, §5
commit-marker compliance for commits to `.claude/agents/*.md` and root-level
`agents/*.md` after the marker-convention date (`7c6ff4f`, 2026-05-17), model-
assignment parity between `.claude/agents/*.md` frontmatter and
`docs/for-analysts.md`, `.gitignore` coverage, secrets scan, and
output-artifact placement.

One new commit since the fourteenth audit:

- `e793662` — `docs(contract): §12 override clause for intentionally public
  identity surfaces`.  Added "Operator override for intentionally public
  identity surfaces" subsection to `docs/operator-contract.md §12` (three
  conjunctive conditions for a formal exception); added inline
  `<!-- §12 operator-authorized deviation -->` marker at `site/index.html:169`
  (the LinkedIn anchor).  Resolves the prior [low, carry-forward] §12 LinkedIn
  finding.  Commit carries `Requested-by: user`; no §5-scope agent-definition
  files touched.

The §12 LinkedIn carry-forward is now fully resolved.  Remaining open items:
one [low, carry-forward] §8 structural finding (launchd plists) and two
[info, carry-forward] items (daily-report naming, operator first name in
immutable commit body).  Verdict stays DRIFT_DETECTED until the §8 structural
gap is addressed.

## Critical / High findings

_(no critical/high findings — verdict is DRIFT_DETECTED but only medium-or-lower findings)_

## How to clear this alert

1. Read the full report linked above.
2. Resolve each critical / high finding (suggested fixes are in the report).
3. Re-run `./scripts/audit-run.sh` — verdict returning to CLEAN auto-removes this file.
