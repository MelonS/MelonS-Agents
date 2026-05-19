# Claude Code permission bootstrap — eliminating per-tool prompts

## Why this exists

The MelonS-Agents project's scripts invoke ~70 distinct binaries
(ffmpeg, ollama, aubiotrack, aubioonset, jq, curl, git, python3,
brew, yt-dlp, whisper-cli, mkdir / mv / cp / rm scoped to safe
paths, etc.).  Out of the box, Claude Code asks for permission
on each distinct command the first time it's invoked.  That
turns a "clone and run the demo" session into a click-marathon
before the first frame renders.

The project's tracked `config/claude-settings.template.json`
already enumerates the safe set with a wide allow list and a
narrow deny list.  `scripts/install-claude-local.sh` renders
that template into a per-machine `.claude/settings.json` at
bootstrap time.

What was missing until 2026-05-19: **the user-level
`~/.claude/settings.json`** is also consulted by Claude Code on
session start, and on first-time directory trust.  Without
bulk-merging the project's allow list at the user level, the
project file alone doesn't suppress every prompt.

## The fix (one script)

`scripts/install-claude-permissions.sh` merges the rendered
project allow list into `~/.claude/settings.json`.

```bash
# interactive — asks Y/n once with a diff preview
scripts/install-claude-permissions.sh --prompt

# non-interactive (for CI or autonomous bootstrap)
scripts/install-claude-permissions.sh --yes

# show what would be added, write nothing
scripts/install-claude-permissions.sh --dry-run
```

`scripts/bootstrap.sh` calls this automatically in interactive
mode.  Override with the `CLAUDE_PERMISSION_BOOTSTRAP` env var:

```bash
# silent install (good for fresh-clone scripts / CI)
CLAUDE_PERMISSION_BOOTSTRAP=yes ./scripts/bootstrap.sh

# operator opt-out (keep user settings untouched)
CLAUDE_PERMISSION_BOOTSTRAP=skip ./scripts/bootstrap.sh
```

## What the script promises

- **Never deletes** existing entries from your
  `~/.claude/settings.json`.  Append + deduplicate only.
- **Validates** the merged JSON before overwriting; partial
  writes can't corrupt the existing file.
- **Idempotent**: re-running with the same project state adds
  zero entries the second time.
- **Traceable**: writes a `_notes.melons_agents` block
  recording source path + merge timestamp + regeneration
  command, so a future audit can attribute the additions.
- **Safe-by-default**: skips silently if `~/.claude/` doesn't
  exist (Claude Code not installed).

## What the script does NOT do

- It does **not** touch the deny list.  Your existing deny
  rules are preserved.
- It does **not** install Claude Code itself.  If Claude Code
  isn't installed, the script no-ops — install Claude Code
  first, then re-run.
- It does **not** add `Bash(sudo *)` or any disk-erase / OS-
  destructive permission.  Those stay in the project deny
  list and are inherited by intent.

## Verifying

Before:

```bash
$ jq '.permissions.allow | length' ~/.claude/settings.json
12   # whatever you had before
```

After running `install-claude-permissions.sh`:

```bash
$ jq '.permissions.allow | length' ~/.claude/settings.json
~80  # 12 + ~70 project entries, deduped

$ jq '._notes.melons_agents' ~/.claude/settings.json
{
  "source": "/Users/<you>/MelonS-Agents",
  "merged_at": "2026-05-19T05:00:00Z",
  "regenerate": "cd /Users/<you>/MelonS-Agents && scripts/install-claude-permissions.sh --yes"
}
```

Restart Claude Code to pick up the new user-level allow list.
The prompts should stop firing.

## When to re-run

- After upgrading to a new MelonS-Agents tag that added new
  binaries (a new shader needing a new CLI, for example).
- After cloning the repo on a new machine.
- After any local edit to `config/claude-settings.template.json`
  that you want to propagate to the user-level file.

`install-claude-local.sh` re-renders the project file
automatically on every bootstrap.  `install-claude-permissions.sh`
needs an explicit re-run because the user-level file is shared
across all your Claude Code projects — we don't silently mutate
it without consent.

## Why not just one global allow file?

Two reasons:

1. **Trust isolation.**  The project `.claude/settings.json`
   only applies inside the project directory.  If you clone a
   different repo, you don't inherit MelonS-Agents' grants.
   Some operators prefer that scoping.
2. **Per-tool granularity.**  The project file lets us scope
   `Write(...)` rules to repo paths and `rm` rules to specific
   directories (e.g., `rm @@REPO_ROOT@@/**`, not bare `rm *`).
   The user-level merge inherits these scoped grants without
   widening them.

The bootstrap installs both layers because both are consulted
at runtime — neither alone is sufficient to suppress all
first-run prompts.

## Threat model

The allow list grants are deliberate but not unbounded.  The
deny list (preserved from the project template) blocks:

- `Bash(sudo *)` — no privilege escalation
- `Bash(rm -rf /)`, `/System*`, `/Library*`, `~/` etc. — no
  catastrophic deletions
- `Bash(dd if=* of=/dev/*)`, `diskutil eraseDisk *` — no disk
  erase
- `Bash(curl * | sh)`, `wget * | bash` — no piped-curl execution
- `Bash(git push --force *main)` — no force-push to protected
  branches

A user who reviewed and approved the bulk grants is trusting
the project's tracked deny list to catch the destructive cases.
If you want to inspect before approving, run with `--dry-run`
to see the exact additions.

## Field observation — where this fix came from

This script's existence was driven by a 2026-05-19 in-person
session with a security professional running the demo on a
fresh clone.  Even with the project `.claude/settings.json` in
place, the per-tool prompt rate during the demo mission was
sufficient to make the experience unusable — operator framed
it as "다 하나씩 승인하기에는 너무 장벽이커 첨에 권한관련해서도
승인하면 어느정도 넘어가게 되어야 할듯" (approving each one is
too much friction; one consent at the start should cover the
rest).  The script ships in `feat/permission-bootstrap` to be
merged after the next round of testing.  See engineering case
study #6 in
[`docs/engineering-case-studies.md`](../engineering-case-studies.md)
for the field-observed addendum.
