# Worktree workflow — quick reference

Per `docs/operator-contract.md` §6 (revised 2026-05-21), parallel
Claude Code sessions on this machine each get their own git
worktree alongside the primary checkout.

## Why

- Two (or more) sessions can run in parallel without context-switching
  branches inside a single checkout.
- Each session owns its own commits on its own feat branch.
- The "branch made but never used" failure mode (caught by the
  2026-05-21 audit drift) is eliminated — each session is *physically*
  inside its branch's worktree, so there's no path that says "open the
  branch then forget to switch."

## Layout

```
/Users/melons/ai                   ← primary checkout (this repo)
/Users/melons/ai-job-hunt          ← worktree on feat/job-hunt-*
/Users/melons/ai-music-video       ← worktree on feat/music-video-*
```

The two helper scripts handle creation + teardown so the operator
never types raw `git worktree …` incantations.

## Start a new parallel-session topic

```bash
scripts/worktree-new.sh <topic>
```

What it does:

1. Fetches latest `origin/main`.
2. Creates `feat/<topic>` branch from `origin/main`.
3. Creates a sibling worktree at `../ai-<topic>`.
4. Copies operator-local files (`.env`, `operator-profile.md`) into
   the worktree so it can run anything without a fresh bootstrap.
5. Prints the `cd` command to enter the worktree.

Example:

```bash
$ scripts/worktree-new.sh job-hunt
[worktree-new] fetching latest from origin...
[worktree-new] creating worktree:
  path:    /Users/melons/ai-job-hunt
  branch:  feat/job-hunt
  base:    origin/main
[worktree-new] copied .env into worktree
[worktree-new] done.  enter the worktree with:

  cd /Users/melons/ai-job-hunt
```

Then in the new Claude Code session, `cd /Users/melons/ai-job-hunt`
and work there.  Every commit lands on `feat/job-hunt`, not `main`.

## Finish + merge

When the work is done, from inside the worktree:

```bash
scripts/worktree-done.sh
```

What it does:

1. Verifies the worktree is clean (no uncommitted changes).
2. Fetches latest `origin/main`, rebases the feat branch on it.
   Conflicts → stops + exits non-zero so the operator can resolve
   manually inside the worktree.
3. Pushes the rebased feat branch.
4. From the primary checkout's `main`, fast-forwards `main` to
   include the feat branch commits, then pushes `main`.
5. Removes the worktree directory + deletes the feat branch
   (local and remote).

Options:

```bash
scripts/worktree-done.sh --no-merge   # push branch, but skip the
                                       # FF-merge to main; useful when
                                       # operator wants to review
                                       # before merging
scripts/worktree-done.sh --keep       # merge to main, but keep the
                                       # worktree + branch in place
```

## When NOT to use a worktree

The §6 flexible guideline doesn't require worktree for every change.
Direct-to-`main` is fine when:

- The change is small / self-contained / unlikely to break things
  (typo, doc edit, audit-fix, single-source-file fix).
- The work is a single coherent session of 1-3 commits and there's
  no parallel session that would conflict.
- The change is a *meta* layer (operator-contract, README,
  `.gitignore`) that any concurrent session might also need
  immediately — worktree-scoped commits don't belong here.

## Cross-session hot-fix

When you discover a bug inside a worktree but the fix is something
the *other* concurrent session also needs right now (e.g. an audit
alert resolution, a shared script regression), commit it on `main`
instead of inside the worktree:

```bash
cd /Users/melons/ai      # primary
git pull                 # get latest main
# make the fix
git add … && git commit -m "fix: …" && git push
cd -                     # back to worktree
git pull --rebase origin main   # bring the fix into the worktree
```

Heuristic: *"Does the other concurrent worktree need this change
right now?  Yes → main.  No → keep on the worktree's branch."*

## Limitations + edge cases

- `git worktree remove` will refuse if there are uncommitted changes
  in the worktree.  Commit (or stash + drop) first.
- Worktrees share the same `.git` storage, so disk overhead is
  minimal (no full repo duplication).
- The same branch cannot be checked out in two worktrees
  simultaneously.  Each topic gets its own branch.
- If `worktree-done.sh` reports a non-FF condition on the final
  merge step, that means `main` has moved while the rebase was in
  flight — re-run the rebase + push cycle and try again.

## Audit-trail

The 2026-05-21 audit-drift incident that motivated this section:

- A single-session autonomous overnight run + a parallel operator
  session both landed structural commits directly on `main` despite
  the prior §6 hard rule requiring feat branches.
- The thirtieth contract audit (`docs/audit/2026-05-21-contract.md`)
  caught 9 such commits as `[high]` findings.
- Operator direction: "유연한 전략이 필요하다 지금 먼가 타이트하게
  박아 버리면 계속 못지킬 가능성 생김."
- The flexible §6 + worktree mode is the response.  Old rule is
  archived in `feedback_branch_strategy_strict.md` git history;
  current memory file is `feedback_branch_strategy_flexible.md`.
