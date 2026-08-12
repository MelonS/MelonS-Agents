# Current audit alert

> This file is written by `scripts/audit-run.sh` whenever the most
> recent audit has a non-CLEAN verdict.  It is auto-removed on the
> next CLEAN run.  Do not edit by hand — fix the underlying findings
> and re-run the auditor.

**Verdict**: DRIFT_DETECTED
**Full report**: [`docs/audit/2026-08-12-contract.md`](2026-08-12-contract.md)
**Generated**: 2026-08-12 09:40:04    

## Summary (from audit)


Focused pass over `docs/operator-contract.md` compliance at HEAD `d9aeca1`
(2026-08-12, ~09:35 KST), cross-checked against `docs/for-analysts.md`,
`docs/architecture.md`, `docs/roadmap.md`, `docs/goal.md`, every
`.claude/agents/*.md` frontmatter, `.gitignore`, `.github/workflows/`, and
`git log` (1634 commits reachable from `main`). The pre-computed
skill-drift report (`scripts/audit-skill-drift.sh`) is clean (0 findings)
and is not repeated below. `main` is green - `gh run list` shows 5/5 recent
`main-protection` runs `success`. Secret scan outside `docs/` / `*.example`
returned nothing; `.env` remains gitignored and untracked. The section-8
hardcoded-path exception registry's 12 listed files all still carry the
required `section-8 exception` grep-anchor. No output artifacts are committed
under `agents/` or `scripts/`. Subagent roster: 27 files under
`.claude/agents/`, matching `for-analysts.md`'s claimed count and group
breakdown exactly (6 core + 13 game-line + 5 content-shorts + 3 judges);
the 6 core agents' `model:` frontmatter matches the routing table exactly
(opus/opus/opus/sonnet/sonnet/sonnet).

Two items flagged high in the prior report (`2026-07-31-contract.md`)
are now resolved: the README hero-stats alt text now correctly reads "27
subagents" (was "23"), and `docs/roadmap.md` "Now" was refreshed
2026-08-10 to reflect the NAN2026-submission-complete state instead of the
72-day-stale multi-skill-framework text. Three prior medium findings
remain open unresolved for a third consecutive contract-focused cycle:
`.claude/wb/` (94 tracked files, undocumented, gitignore-absent), `ta.md`
missing a `model:` frontmatter field, and `for-analysts.md` undercounting
the CI gate ("six" vs the actual seven checks, unchanged since `06dd751`
on 2026-07-01).

Two new findings emerged this cycle that were not visible to the prior
audit. First: cross-referencing every commit hash cited in
`docs/roadmap.md`'s Done section against `git cat-file -e` shows 50
entries (dated 2026-05-14 through 2026-05-19, i.e. the project's first six
days) reference hashes that do not exist anywhere in the current repo -
not on `main`, not on any branch, not as a loose object. Root cause is
documented in the repo's own history: an email-history rewrite via
`git filter-repo` landed 2026-05-17 (`b46a2ba`), and its rollback safety
branch (`main-backup-pre-filter-20260517-173615`) was deleted three days
later (`a90bc9d`, 2026-05-20) after no issues were observed - a reasonable
operational call at the time, but nobody went back to annotate the
now-orphaned Done entries or the operator-contract's own citation of the
same pre-rewrite hash space. That citation is the second half of this
finding: `docs/operator-contract.md` section 5's pre-marker carry-forward
clause names `7c6ff4f` as the reference commit, and that hash is also
unresolvable in the current repo - the contract's own audit-trail
exemption is unverifiable by the exact mechanism (`git cat-file -e`) this
audit dimension prescribes. Second: while investigating the L1 post-commit
hook (`scripts/hooks/post-commit.sh`), a live instance was caught
mid-failure - the hook correctly fired for today's `d9aeca1` (it touched
`.claude/agents/ta.md`, a drift-risk path) at 09:27:02, and the spawned
`audit-run.sh contract` background process (pid 5788) is still alive and
producing no further log output more than 8 minutes later, well past the
short startup banner where every other logged hook-run in
`records/audit/hook-trigger.log` had already produced substantive content
by this point. That process is targeting the exact same output path this
report is writing to (`docs/audit/2026-08-12-contract.md`), which is a
live overwrite/commit-race risk, not a hypothetical one.

## Critical / High findings

- **[high]** L1 post-commit-hook audit process appears hung, targeting the same output path as this report - `scripts/hooks/post-commit.sh`, `records/audit/hook-run-20260812-092702-d9aeca1.log`, `records/audit/.hook.inflight`
- **[high]** `docs/roadmap.md` Done section (50 entries) and `docs/operator-contract.md` section 5's carry-forward citation reference commit hashes invalidated by the 2026-05-17 filter-repo history rewrite - `docs/roadmap.md` Done entries dated 2026-05-14 to 2026-05-19; `docs/operator-contract.md:117` (`7c6ff4f`)

## How to clear this alert

1. Read the full report linked above.
2. Resolve each critical / high finding (suggested fixes are in the report).
3. Re-run `./scripts/audit-run.sh` — verdict returning to CLEAN auto-removes this file.
