# skills/

agentskills.io-compliant skill definitions, tracked in git.

The bundle of skills here is the project's primary user-facing
capability surface.  Each subfolder is one skill; each contains a
`SKILL.md` per the [agentskills.io open spec](https://agentskills.io/specification),
plus optional `scripts/`, `references/`, and `assets/` subdirs.

## Why top-level `skills/` (not `.claude/skills/`)?

Per the operator-contract
[§8 portability principles](../docs/operator-contract.md) — five
principles codified 2026-05-19:

1. Standards-compliant by default
2. **Tracked-by-default — git is the source of truth**
3. Machine-resilient (clone + bootstrap = full restore)
4. Multi-machine portable (same repo, any machine)
5. No PII / secrets in tracked files

The `.claude/` directory carries a strong "local-only" semantic
(like `.git/`, `.vscode/`).  Putting tracked project assets there
muddles the convention.  Top-level `skills/` keeps the asset
visible at repo root and matches the Hermes Agent + agentskills.io
spec convention.

Claude Code's default discovery path remains `.claude/skills/`.
That gap is bridged by
[`scripts/install-claude-local.sh`](../scripts/install-claude-local.sh)
which creates the symlink `.claude/skills/<name>` → `../../skills/<name>`
at install time.  `bootstrap.sh` calls it automatically on a fresh
clone.

## Currently shipped

| Skill | What it does | Source |
|---|---|---|
| [`music-video/`](music-video/) | 60-second 9:16 vertical music video from music file + mood keywords | Wraps `agents/missions/music-video/run.sh` |

## Planned

| Skill | Status | Notes |
|---|---|---|
| `job-hunt-digest` | Active goal Subgoal #2 | AI-era job posting aggregator + LLM filter for "AI integration / problem-solver" pattern + daily digest |

See [`docs/goal.md`](../docs/goal.md) "Active goal" for the
multi-skill framework roadmap.

## Authoring a new skill

1. `mkdir -p skills/<name>/scripts skills/<name>/references skills/<name>/assets`
   (omit the ones you don't need).
2. Write `skills/<name>/SKILL.md` with required frontmatter
   (`name`, `description`) per the
   [spec](https://agentskills.io/specification).  `name` must match
   the parent folder name (lowercase, hyphens only, no leading /
   trailing / consecutive hyphens, ≤ 64 chars).
3. Add the runtime bash / python to `scripts/` (or symlink an
   existing mission script if the skill wraps one).
4. Run `./scripts/install-claude-local.sh` to refresh the
   `.claude/skills/` symlinks (idempotent — safe to re-run).
5. Validate against the spec with the
   [`skills-ref`](https://github.com/agentskills/agentskills/tree/main/skills-ref)
   reference tool:
   ```bash
   skills-ref validate ./skills/<name>
   ```

## Cross-runtime compatibility

The agentskills.io spec is implemented by ~38 agent runtimes
(Cursor, Goose, Gemini CLI, OpenCode, Codex, etc. — see the
[Client Showcase](https://agentskills.io/clients)).  Skills under
this directory should work in any of them with the spec-canonical
file structure (`SKILL.md` + optional dirs).  Runtime-specific
extensions to the frontmatter (e.g., Claude Code's `context: fork`)
should be used sparingly — prefer the open standard's minimal
surface.
