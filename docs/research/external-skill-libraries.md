# External skill libraries — ecosystem survey

2026-05-22 — initial survey, on-demand companion to [`skills/`](../../skills/).

Purpose: when authoring a new skill in this repo, this file is the
first thing to skim.  It catalogues OSS skill collections and the
shared [`agentskills.io`](https://agentskills.io) spec so we can
borrow *patterns* (architecture, frontmatter conventions,
documentation style) rather than re-inventing.  We follow the same
open spec — these are peers in the ecosystem, not parent projects.

Nothing in this repo is forked, vendored, or copied from these
projects.  Where a pattern was inspired by one of them, the
inspiration is acknowledged in a sentence inside this file and
nowhere else — committed code is original.

---

## The shared specification

| Project | License | Why it matters |
|---|---|---|
| [`agentskills.io`](https://agentskills.io) | Open spec (no code license — spec text under their site terms) | The frontmatter shape our `SKILL.md` files follow (`name`, `description`, `license`, `compatibility`, `metadata`, `allowed-tools`).  All projects below either follow this spec or a near-superset. |

---

## OSS skill collections

| Repo | License | Skill style | What to look at |
|---|---|---|---|
| [`openai/skills`](https://github.com/openai/skills) | MIT (verify at fetch time) | Markdown-first; one folder per skill, `SKILL.md` + assets | Reference implementations for common task families (file I/O, web, codegen).  Their folder layout informed our `skills/<name>/{SKILL.md,scripts/,config/,tests/}` convention but the bash + TSV manifest is ours. |
| [`anthropics/skills`](https://github.com/anthropics/skills) | MIT (verify at fetch time) | Same as above; tighter on documentation style | Documentation tone for `When to Use` / `Procedure` sections; we keep our own structure ("Status", "What this produces", "How to invoke", "Filter schema", "Scope explicitly out", "Privacy / data handling"). |
| [`NousResearch/hermes-agent`](https://github.com/NousResearch/hermes-agent) | MIT | Runtime + skill loader; declarative tool gating in YAML frontmatter | Has `requires_tools` / `fallback_for_toolsets` frontmatter keys that gate whether a skill is *visible* to the agent at all.  Our `activation.tsv` solves a *different* problem (per-feature LIVE flags toggling mock vs live data inside an always-visible skill) and uses a different mechanism (TSV + bash), but the "declarative manifest > grep-the-script" philosophy is shared.  Not a clone — different scope, different format, no shared code. |
| [`skills-sh`](https://skills.sh) (Vercel-hosted directory) | Per-skill licenses vary | Public skill marketplace | Useful as a discovery surface for skill ideas / naming conventions.  Not a code source. |

---

## Patterns we follow (and where they came from)

- **YAML frontmatter per skill** — `agentskills.io` spec.
- **`scripts/` + `config/` + `tests/` subfolders** — convergent
  convention across `openai/skills` and `anthropics/skills`; we
  adopted because the layout is well-understood by anyone who has
  read either of those repos.
- **Mock-fallback default + LIVE-flag gating** — in-house pattern;
  no equivalent in the projects above (their gating is visibility,
  not data-source toggle).  See
  [`reference_scaffold_pattern.md`](../../../.claude/projects/-Users-melons-ai/memory/reference_scaffold_pattern.md).
- **Declarative activation manifest + status dashboard** —
  in-house format (TSV) inspired by Hermes' frontmatter-as-truth
  philosophy.  Different file format, different scope, no shared
  code.  Single source of truth: `skills/<name>/config/activation.tsv`.

---

## What we deliberately do NOT do

- **Pull skills from external hubs at runtime.**  All skills under
  `skills/` are operator-authored and committed.  Hermes supports
  pulling from `openai/skills` etc. live; we don't because (a) the
  operator wants to read every skill before it runs and (b) the
  money-firewall ([[feedback-money-firewall]]) wants every external
  fetch gate-checked.
- **Vendor skill code from external repos.**  If a pattern is
  worth borrowing, it gets re-implemented from scratch in our
  style (bash + TSV + our log conventions).  This keeps the repo
  honest as a "MelonS-Agents original work" credibility signal
  rather than a glue layer.

---

## When to re-survey

Update this file when:
- A new mainstream OSS skill collection appears (>1k stars, MIT-
  compatible).
- Our skill count crosses 5 (currently 2) — at that point patterns
  become more load-bearing and a fresh look at neighbors is cheap.
- `agentskills.io` releases a v2 spec.

Companion memories:
- [[nolanx-ai-video-skills]] — 20 cinematic prompt-engineering
  skill names from `nolanx-ai/nolanx.ai`, separate from this
  survey (those are *prompt skills* for video gen, not runtime
  skills in the agentskills.io sense).
- [[reference_scaffold_pattern]] — our in-house gating pattern.
