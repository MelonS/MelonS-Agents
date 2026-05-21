# Contributing to MelonS-Agents

Thanks for considering contributing. This is one engineer's evening project — small, specific contributions are the most useful kind.

## Quick paths

- **Try the demo and tell me where it broke** → open a [First-touch friction report](https://github.com/MelonS/MelonS-Agents/issues/new?template=first-touch-friction.yml). The single most useful contribution right now: onboarding success rate is the active priority.
- **Casual questions, ideas, Show & Tell** → [Discussions](https://github.com/MelonS/MelonS-Agents/discussions).
- **Bug** → [Bug report](https://github.com/MelonS/MelonS-Agents/issues/new?template=bug-report.yml).
- **Propose a new Skill** → [Skill request](https://github.com/MelonS/MelonS-Agents/issues/new?template=skill-request.yml).

## Repo conventions

- **Standards-compliant** — Skills follow the [agentskills.io](https://agentskills.io) open spec.
- **Env-driven paths** — read binaries/paths from `.env`; never hardcode `/Users/...` or `/opt/homebrew/...`.
- **`records/` is gitignored** — outputs stay local; only the framework itself is tracked.
- **Branch strategy** — `main` is the always-runnable trunk; `feat/<name>` for structural changes. Details in [`docs/operator-contract.md`](docs/operator-contract.md) §6.
- **No PII in committed files** — the repo is public; please synthesize examples rather than excerpting real personal data.

## Code of conduct

Be kind. Constructive feedback is gold; dismissive feedback is noise.

## Pull requests

- Small and focused beats large and bundled.
- Run `scripts/pre-merge-check.sh` before opening if you're touching `agents/`, `.claude/agents/`, `config/`, or `scripts/`.
- For docs/typo PRs, no pre-merge check needed.

## Maintainer

[@MelonS](https://github.com/MelonS) — see profile for contact.
