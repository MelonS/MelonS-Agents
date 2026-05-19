# Operator contract

Committed, canonical version of the operating rules that govern how
Claude works on this repository.  Local agent memory at
`~/.claude/projects/-Users-melons-ai/memory/` exists as a fast-access
cache, but **this file is the source of truth** — if the two ever
disagree, the committed file wins, and the memory entry should be
updated to match.

The contract is split into two sections:

1. **Hard rules** — non-negotiable; describe how the agent must
   behave.  Violating these is a bug.
2. **Conventions** — formatting, tone, and presentation defaults.
   Deviating requires explicit user instruction.

Cross-cutting principle: the user is async.  They send a message and
walk away.  Asymmetric latency makes "let me ask first" a cost; act
on reasonable assumptions, let the user redirect.

---

## Hard rules

### 1. Agent does all the work

The user does not run commands, install packages, edit config files,
or touch the terminal.  Claude does all of it.

- Default to running the action (`brew install`, `pip install`,
  `git push`, file edits) yourself.  Auto-mode + the allow list cover
  almost everything.
- When a hard guardrail blocks you (auto-mode classifier denying
  self-modification of `.claude/settings.json`, push-to-main inside
  a compound command, etc.), do **not** respond with "please run X"
  or "please paste Y into Z".  Surface the blocker in one line and
  the *minimal* user action (a single click in the permissions UI),
  never a multi-step bash recipe.
- The user only acts on the *exact* things Claude cannot do.

### 2. Never pause unless told

The user often writes a message and steps away.  Do not end a turn
with "or pause?" / "다음 갈까요?" / "shall I continue?".

- When a task finishes and `docs/roadmap.md`'s **Next** queue has
  an item, promote it to **Now** and start it in the *same* turn.
- Stop only when the user explicitly says stop / wait / pause / 멈춰,
  when a hard guardrail blocks (and even then, switch to the next
  non-blocked item), or when both Now and Next are genuinely empty.
- An unanswered "next?" question turns into hours of idle time.  An
  action the user disagrees with is a 30-second course correction.
  The action is cheaper than the question.

### 3. Money firewall

Auto-approve does not cover anything that spends money or commits to
future money.  Always pause and request explicit confirmation for:

- Paid API usage / SaaS subscription / paid library purchase.
- Paid API calls — including the transition from free credits to
  metered billing.
- Cloud resource creation (AWS, GCP, Azure, any provider where
  standing infrastructure incurs cost).

Local resources (Ollama, FFmpeg, whisper.cpp, macOS `say`, brew
packages) are fully auto-approved.

### 4. Auto-approve mode (local resources)

Non-catastrophic system actions proceed without asking:

- Brew install/uninstall, pip install, npm install.
- File deletion (under the repo / `/tmp`).
- Settings changes, MCP/config edits in the repo.
- All actions inside `.claude/settings.json`'s allow list.

Pause only for **truly catastrophic** risks: hardware damage,
irreversible data loss outside the repo (`rm -rf ~`, disk format,
force-push to shared remotes, sending external messages).

User accepts macOS-environment-level mess (broken brew state, lost
browser data, etc.) as acceptable risk.

### 5. Logic changes require explicit user OK

Editing `agents/*.md` or `.claude/agents/*.md` always requires
explicit user OK, regardless of autonomy mode.  These files define
the subagent contracts; changing them changes the system's logic.

**Audit-trail marker.**  When the user OK has been obtained
in-session and the commit lands a §5-scope change, include a
footer line on the commit body to make the approval mechanically
verifiable in `git log`:

```
Requested-by: user
```

Place above the `Co-Authored-By` line if present.  Cite the in-
session evidence (a paraphrase or short quote) in the commit body
above the footer.  This is a convention, not a hard gate — a
missing marker on a §5-scope commit is not a build failure, but
it is a [low] finding the next contract audit will surface.

**Pre-marker carry-forward.**  Commits to `.claude/agents/*.md` or
`agents/*.md` that landed BEFORE the marker convention was defined
(prior to 2026-05-17, commit `7c6ff4f`) are exempt as
carry-forward.  Audits should classify them as `[low]
carry-forward — predates marker convention` rather than fresh
[low] findings.  Only §5-scope commits dated 2026-05-17 or later
need the marker.

### 6. Git workflow — auto-commit, auto-push, branch strategy

Every code or doc change (anything under `agents/`, `.claude/agents/`,
`config/`, `scripts/`, `docs/`, `CLAUDE.md`, `README.md`,
`README.ko.md`, `.env.example`, `.gitignore`) is committed and pushed
to the appropriate branch on completion.

- Remote: `git@github.com:MelonS/MelonS-Agents.git` (public).
- Records (`records/`) are never committed.
- Commit message style: imperative subject ≤72 chars, optional body
  with bullets explaining *why*.  Group changes by concern; don't
  bundle unrelated edits.

**Branch strategy (codified 2026-05-18 ~21:30 KST after operator
direction "브랜치 전략 너의 추천대로 갈게")**:

The repo is now publicly clone-able by external users (KakaoTalk +
LinkedIn sharing; first external clone verified 2026-05-18).  A
broken `main` damages reputation.  Therefore:

- **`main` = always-runnable trunk** — micro-commits go here directly.
- **`feat/<name>` = structural-change branches** — big changes live
  here until validated, then merge to `main`.
- **`v<MAJOR>.<MINOR>.<PATCH>` tags** — stable checkpoints.  README
  recommends external users clone the latest tag, not `main`.  Tag
  on milestone events (first shipped batch, new skill, etc.).

**What counts as `micro-commit` (direct to `main` OK)**:

- `docs/` updates (daily reports, audit reports, roadmap entries,
  pilot tracking).
- Audit-fix commits (any commit driven by an audit report).
- `README.md` / `README.ko.md` typo / small wording fixes.
- One-line bug fixes that don't change semantics.
- `assets/music/SOURCES.md` track additions.
- Daily report files in `docs/daily/`.

**What counts as `structural-change` (feat branch required)**:

- `.claude/agents/*.md` edits (already gated by §5; now also branch).
- `skills/<name>/` create / edit (new layer — Skills work).
- `agents/missions/<type>/run.sh` — new mission type or major
  semantics change.
- New `scripts/` tooling that other components will call.
- `.env.example` schema additions (new env vars).
- Dependency changes (new external API, replaced library).
- Multi-file refactors crossing 3+ concerns.

**Workflow on a `feat/<name>` branch**:

1. `git checkout -b feat/<name>` from latest `main` (or a tag).
2. Iterate; commit + push to `origin/feat/<name>` (not main).
3. When work is ready, run the **pre-merge gate** (below).  All
   four gates must pass before merge.
4. Fast-forward merge to `main`:
   `git checkout main && git merge --ff-only feat/<name> && git push origin main`.
5. Delete the branch:
   `git branch -d feat/<name> && git push origin --delete feat/<name>`.
6. If the merge represents a user-visible milestone, also tag
   (e.g., `git tag -a v0.2.0 -m "..."` + `git push origin v0.2.0`).

**Pre-merge gate** (mandatory; operator concern 2026-05-18 ~21:50
KST — "main 브랜치가 깨지는건 정말 부담스러움"):

Four gates.  Automated gates (1, 3) run via
[`scripts/pre-merge-check.sh`](../scripts/pre-merge-check.sh);
manual gates (2, 4) require operator action.

| # | Gate | Source | Mode |
|---|------|--------|------|
| 1 | **Audit CLEAN** — drift / contract / cost-model / secrets / TODOs | `scripts/audit-run.sh all` returns CLEAN verdict on the feat branch HEAD | automated |
| 2 | **Functional integration test** — run the affected mission / skill end-to-end and verify output | manual (varies per change); skipped for doc-only changes with operator OK | manual |
| 3 | **§5 marker compliance** — every commit touching `agents/` or `.claude/agents/*.md` carries `Requested-by: user` in body | `git log <base>..<feat> --grep="Requested-by:"` matches every §5-scope commit | automated |
| 4 | **Operator merge OK** — merge itself is a public-surface change, so the final gate is human | explicit operator approval ("merge OK") in conversation | manual |

If any gate fails, the work stays on the feat branch.  Fixes
land as new commits on the feat branch (not retroactively rewriting
history; preserves audit trail).  Then re-run gates.

**Bootstrap exception**: structural commits that introduce the
gate itself (or its required infrastructure) land directly on
`main` since the gate doesn't exist yet to validate them.  Covered
commits: `a2a3807` (branch-strategy rules), `22a45ea`
(`scripts/pre-merge-check.sh` itself), and `a537018`
(`.github/workflows/main-protection.yml` — Actions CI that the
gate depends on for the main-protection layer).  All subsequent
structural changes go through the gate.

**Tag convention**:

- `v0.1.0` = 2026-05-18 — first batch of music-shorts shipped to YT.
- `v0.x.0` bumps for skill milestones (Skill #1 shipped, Skill #2
  shipped, etc.) **or** named major infra/UX milestones that
  meaningfully change first-touch behavior.  Named exceptions:
  - `v0.3.0` (2026-05-19) — "Permission bootstrap + pluggable
    B-roll"; not a new Skill delivery but did materially change
    onboarding behavior (per-tool permission prompts collapsed
    + custom B-roll directory + AI-anime generator), so promoted
    to a minor bump rather than `v0.2.1`.
- Patch bumps `v0.x.y` for stability-only fixes within a minor.
- No `v1.0.0` until the multi-skill framework vision (see
  `docs/goal.md`) is shipped.

### 7. Split commit and push

Run `git commit -m "..."` and `git push origin main` as two
separate Bash calls — never the `git commit ... && git push ...`
compound.  The auto-mode classifier blocks the compound; the
workaround (editing `.claude/settings.json` to allow it) hits the
self-modification guardrail.  Splitting is one second per cycle and
always passes.

### 8. Code / data separation + portability — five principles

**Codified 2026-05-19 ~01:30 KST** after operator articulated the
principle set in successive messages:

> "표준 무조건 준수. 로컬은 정말 필요시에만 사용. 대부분의 정보를
> 이루는건 반드시 git에 올라가야함. 대신에 올라가면 안되는게 git에
> 올라가는것도 문제. 그리고 내일이라도 지금 당장이라도 이 맥북은
> 파괴되거나 고장날수있음. 맥북이 고장나서 프로젝트가 망할수도
> 있어야겠음? ... 난 머신 다른곳에서도 할수가 있는데... 하나의
> 머신만 고집하면 안됨."

The repo's portability contract:

1. **Standards-compliant by default** — adopt established specs
   (agentskills.io for skills, agent-skills format for SKILL.md,
   etc.) rather than inventing local conventions.  Skills written
   to spec drop in across ~38 compatible runtimes.
2. **Tracked-by-default** — git is the single source of truth.
   Anything required to reconstruct the project on a fresh
   machine is tracked.  Local-only is the exception, not the
   default, and must be justified per file/directory.
3. **Machine-resilient** — the MacBook can be destroyed today.
   `git clone <repo> && ./scripts/bootstrap.sh` on a fresh
   qualified machine must produce a working setup.  Project
   continuity does not depend on this specific machine's disk.
4. **Multi-machine portable** — the same repo must run
   identically on any qualified machine.  No hardcoded
   machine-specific values in committed files.  All paths
   env-resolved or rendered from templates at install time.
5. **No PII or secrets in tracked files** (see §12).  The
   inverse exception to principle 2: a small allow list of
   secrets and identity surfaces stays *out* of git.

**Concrete enforcement paths**:

- `.env` (gitignored) for secrets; `.env.example` (tracked)
  for the schema.
- `launchd` plists rendered from `*.plist.template` via
  `@@HOME@@` / `@@REPO_ROOT@@` substitution at install time
  (shipped 2026-05-17, commit `ab6555e`).
- `.claude/settings.json` rendered from
  `config/claude-settings.template.json` at install time
  (shipped on the multi-skill-framework feat branch).
- All committed scripts and configs use `$HOME`, `$REPO_ROOT`,
  `$FFMPEG_BIN`, `$OLLAMA_HOST`, `$RECORDS_DIR` etc. — never
  literal `/Users/...` or `/opt/homebrew/...`.  Exceptions
  (e.g., env.sh's libass-discovery fallback list) are
  documented inline with `§8 exception:` comments.
- **`.claude/` becomes truly local** over time: only files
  Claude Code requires to be at that path live there, and
  those that *must* be tracked are rendered/symlinked from
  top-level tracked sources by `scripts/install-claude-local.sh`.
  Final state: `.claude/` is fully gitignored.

**Default placement rules for new project assets**:

| Asset type | Tracked at | Discovered by Claude Code at |
|---|---|---|
| Skills (agentskills.io spec) | `skills/<name>/` (top-level) | `.claude/skills/<name>/` (symlink) |
| Subagent definitions | `subagents/<name>.md` (top-level, future migration) | `.claude/agents/<name>.md` (symlink, future migration) |
| Mission scripts | `agents/missions/<type>/run.sh` | direct path |
| Shared libs | `agents/lib/<name>.sh` | direct path |
| Tool wrappers | `scripts/<name>.sh` | direct path |
| Permission config | `config/claude-settings.template.json` (template) | `.claude/settings.json` (rendered, gitignored) |

The migration of existing `.claude/agents/*.md` to top-level
`subagents/` is tracked separately (logic-changes-need-OK
applies; not autonomous).

**Hardcoded path exception registry** (the documented
deviations):

- `agents/lib/env.sh:38,50` — ffmpeg-full keg discovery
  fallback (libass-enabled ffmpeg).  Falls back only when
  PATH ffmpeg lacks libass.
- `scripts/audit-run.sh:37–42` — claude CLI candidate list
  for launchd's minimal PATH.  Only consulted when
  `command -v claude` fails.
- `scripts/music-video-shaders.sh:57,64` — ffmpeg/ffprobe
  parameter-expansion defaults.  Inline §8 exception
  comment present (commit `39c5db3`).

### 9. Goal and roadmap are the source of truth for work selection

Every conversation that asks for work reads two files in order:

1. **`docs/goal.md`** — the outcome layer.  Read **first**.  The
   active goal describes success as a concrete deliverable.  An
   empty work queue does **not** mean the goal is achieved — only
   the goal's "Done when" criteria do.  If a deliverable subgoal is
   unchecked, that is the next work even when the roadmap reads
   clean.
2. **`docs/roadmap.md`** — the work queue.  Read second.  The
   **Now** section is the day-level priority advancing the current
   goal.

The split exists because of a real failure mode: 2026-05-15 → 2026-05-16
produced 11 commits of infrastructure with the roadmap reading 0 open
items, while the actual outcome (a real CC short emerging from that
infrastructure) was 0 produced.  Without a separate outcome layer, an
empty queue reads as "done" when the goal is unmet.

- Do **not** use the README's `Status` checklist to pick work — it's
  a flat capability ledger, not an ordered backlog.
- Do **not** infer the next task from `git log` alone — the log
  shows what landed, not what's *being* worked on or what's *now*
  most important.
- If `docs/goal.md` active goal is empty, ask the user before
  assuming a goal.  Do not invent goals.
- If `docs/roadmap.md` "Now" is empty but goal subgoals are unmet,
  the next task is whatever advances the most-blocked subgoal.
- If `docs/audit/CURRENT-ALERT.md` exists, read it before picking
  up the goal — it means the last audit run flagged drift or a
  critical issue, which may bump priority above the goal queue.
- After work lands, append a one-line entry to roadmap's **Done**
  section with the commit hash and date; tick any goal subgoals the
  work cleared.
- Goal "Active goal" + roadmap "Now" / "Next" / "Blocked" are
  user-edited; Claude only appends to roadmap "Done", ticks goal
  subgoals when the relevant commit lands, and proposes new goals
  via `<!-- suggest -->` HTML comment when the active goal is
  achieved or absent.

Subagents (orchestrator, planner, resourcer, editor, qa, auditor)
do **not** read `docs/goal.md` or `docs/roadmap.md` — day-level
decisions stay at the top-level conversation layer so subagents
remain pure functions of the prompt they receive.

### 10. Session resume protocol

When the user opens with one of these patterns —

- "어제 하던 작업 계속" / "이어서 하자"
- "어제 [기능/이슈]..."
- "방금 그거 어디까지 했지?"

— deliver a **[관리자 브리핑]** before starting the new task:

1. Repo state (clean / dirty, last commit, last push).
2. Last work performed (most recent mission / commit / change).
3. Next entry point (what `docs/roadmap.md` Now says).

Pull context from `records/missions/`, `git log`, `git status`, and
the roadmap.  Do not start the new task before the briefing lands.

### 11. Shutdown protocol

When the user closes the day with one of these patterns —

- "오늘 퇴근하자" / "퇴근" / "오늘 마무리" / "끝"
- "wrap up" / "end of day"
- Any phrase that clearly means the session is over

— execute the shutdown sequence in order, then deliver one final
[관리자 브리핑]:

1. **`git status` must be clean.**  Commit any in-flight changes
   with a clear message.  No "WIP" commits — finish the thought
   or revert.
2. **`git push origin main`** — confirm `origin/main` is fully
   synced.  Re-push if the prior auto-push failed silently.
3. **Write the daily report** to
   `docs/daily/<ISO-date>.md`.  Use the structure of the most
   recent report in `docs/daily/` as the template.  Required
   sections:
   - 요약 (commit count, hash range, working-tree state)
   - 푸시된 커밋 (one-line each)
   - 새로 만들어진 인프라
   - 운영 규칙 변경 (if any)
   - 핵심 기술 발견 (bugs, gotchas, learnings)
   - 내일 시작점 — what `docs/roadmap.md` Now will be on resume
   - English mirror (one short paragraph)
4. **Archive the previous day's report** if a `docs/today-summary.md`
   pointer-style file is being used.  Each daily report lives at
   `docs/daily/<ISO-date>.md` once written — never overwrite, only
   append history.
5. **Verify `docs/roadmap.md` "Now"** is either:
   - non-empty and actionable (so resume is one-step), or
   - explicitly empty with a one-line note for the next session.
   Do not leave a stale "Now" that refers to something already
   shipped.
6. **Verify autonomous schedulers are still loaded**:
   `./scripts/install-scheduler.sh status`.  If queue or auditor
   dropped, re-install before logging off.
7. **Final commit + push of the report** so the trail is durable
   across machine swaps.
8. **Deliver the Korean shutdown briefing** to the user:
   - `[퇴근 보고]` one-liner with commit count + theme.
   - 오늘 새로 영구화된 규칙 (if any) — bullet list.
   - 내일 첫 작업 한 줄.
   - "푹 쉬세요" — short closer, no homework for the user.

The protocol mirrors the session-resume protocol (§10) so that the
*next* session's briefing has everything it needs to start cold.
A clean shutdown is the cheapest way to make tomorrow's first turn
cheap.

### 12. No PII or secrets in repo-bound data

This repo is public and actively promoted.  Any byte that may end
up under `origin/main` must be free of personal information and
credentials — of the operator, of third parties, of anyone.

- **Personal identifiers**: real names, employer names, private
  project names, contact info (email / phone / messenger handles)
  of the operator or of anyone the operator talks to — stripped
  before write.
- **Credentials**: API keys, tokens, passwords, OAuth secrets,
  webhook URLs containing tokens — never committed, even
  temporarily.  `.env` stays gitignored; `.env.example` is
  schema-only.
- **Source material**: chat exports, screenshots, scraped pages,
  field-note transcripts go under `$RECORDS_DIR` (gitignored) or
  outside the repo path entirely (e.g., `~/Downloads/`).  Never
  drop raw source files into `docs/` or `agents/`.
- **Agent memory is at risk too.**
  `~/.claude/projects/-Users-melons-ai/memory/` is local today,
  but entries may be migrated to repo-tracked storage in the
  future (machine-swap durability).  Apply the same scrubbing
  rules to memory files as to committed docs.  If a name or
  credential would be wrong to commit, it is wrong to write into
  memory.
- **Synthesis is preferred over excerpts.**  When recording
  external conversations, distill the *pattern* (anonymized) into
  `docs/research/` and discard the raw source.  An anonymized
  synthesis survives review; a raw quote with names does not.
- **Embarrassment test**: before writing, ask "would I want this
  on the public GitHub project page?"  If no, scrub or move it
  out of repo scope.

The rule applies regardless of file location.  Local-only storage
is a weak guarantee, not a license to write whatever.

**Operator override for intentionally public identity surfaces.**
Identity information that the operator has *deliberately chosen to
expose* on a specific public-facing surface is allowed, narrowly
scoped, and only when all three of the following hold:

1. The operator authorized the specific exposure in a recorded
   conversation (the `Requested-by: user` commit marker on the
   originating commit is sufficient written record).
2. The exposure surface is intentionally public-facing
   (Pages site footer, README maintainer credit line, GitHub
   profile sidebar) — not buried in agent logic, scripts, or
   data files.
3. The committed reference carries an inline
   `<!-- §12 operator-authorized deviation -->` marker so a
   future review pass can distinguish authorized exposure from
   accidental leakage.

This clause does not weaken the general rule; it formalizes that
audits should treat marked, authorized exposures as known
exceptions rather than as new findings.  Unmarked PII remains a
violation regardless of intent.

---

## Conventions

### Dual-stack reporting

- Korean **[관리자 브리핑]** one-liner at the top of every user-facing
  message.
- English internals — commits, code, logs, file paths.
- No raw code dumps in chat.  Reference paths + line numbers; the
  user reads the file directly.
- Periodic Korean status dashboard (`[완료]/[진행]/[다음]`) at natural
  10-minute breakpoints during long autonomous work.

### Terminal / shell format

When a single user message requires multiple shell operations:

- Write the entire sequence into `/tmp/agent_worker.sh` (fixed path).
- Run it once.  One approval per script, not per command.
- Log format inside the script:
  - `[STEP] N/M description` — entering a step.
  - `[DONE] description` — exiting a step successfully.
  - `[PROGRESS] N% description` — long-running progress markers.
  - `[CRITICAL] message` — only at completion or hard failure.

### Documentation style

For all repository documentation (README, docs/, etc.):

- Neutral, technical tone.  No personal credentials, "X years of
  experience", or marketing superlatives.  Clean open-source feel.
- README split into `README.md` (English) and `README.ko.md`
  (Korean) with a language switcher at the top.
- Center-aligned title + badges via `<div align="center">`.
- `for-the-badge` style badges + custom value badges (AI-Powered,
  Self-Evolving, Autonomous).
- Architecture sections use markdown tables with role emojis
  (Orchestrator / Planner / Resourcer / Editor / QA) showing
  responsibility + output.
- No fruit / casual emojis in committed content.

### Batch execution

Multi-step shell work goes into a single `.sh` script — one approval
per script, not per command.  Internal flags (`jq -f`, `--slurpfile`,
`2>&1`, pipes, etc.) are implicitly approved by virtue of being
inside the script.  Avoid `bash -c '...'` chains in the chat;
they're noisy and re-prompt for each invocation.

### Writing tone

Same as documentation style above, applied to:

- Commit messages — explain *why*, not what.
- PR descriptions, when those happen.
- Any public-facing writing.

Keep it tight, technical, and neutral.

### README maintenance cadence

`README.md` / `README.ko.md` is the system's public face — an
outside reader should be able to scan it and reach an accurate
read of the current state within 30 seconds.  But it does **not**
get rewritten on every commit; per-commit edits create churn (a
five-section diff for a one-line capability) without improving
the reader's experience.

Update on these four natural triggers, batching multiple commits'
worth of changes into one README pass:

1. **Active goal lands** — when `docs/goal.md` migrates a goal to
   Past goals, review the README's Status section, Sample output
   section, and any related sections for items that should land
   now.  Most goal-landings produce at least one new Status entry.
2. **Audit detects README drift** — when `scripts/audit-run.sh`
   flags a stale section (Status entry missing a hash, capability
   description out of sync with a script, etc.), fix it
   immediately as part of clearing the alert.  Do not defer.
3. **Mission outputs visibly change the gallery / charts** — a new
   caption-verify frame worth showing, a new chart variant, a
   GIF refresh.  Decide per-output whether it changes what a
   reader sees; if yes, update.
4. **Operator contract / architecture changes** — immediate.  The
   contract and the architecture diagram are canonical claims;
   stale text here is a worse failure than stale Status.

Anti-rule: do **not** open the README to add a single Status line
for every routine commit.  Status entries accumulate until a
trigger above fires.  If unsure whether an edit is worth the
diff, defer — the next audit run will surface it if it actually
matters.

KO mirror (`README.ko.md`) updates in the same commit as EN.
A drift between the two files counts as audit-relevant
documentation drift.

#### How (not just when) — full-file review on every trigger

Each time a trigger above fires, do **NOT** treat the README pass
as an append-only operation.  The historical failure mode (operator
flagged on 2026-05-17) was: a trigger lands, a new section gets
added at the bottom, the *existing* sections silently rot — old
"15 missions produced" sentinel left in place, animated preview
pointing at last week's showcase, recent-runs table not refreshed,
charts unchanged, capability descriptions out of sync with the
shipped code.  Append-without-review accumulates exactly the kind
of stale that the cadence rule was supposed to prevent.

On every README pass, walk the **full file once** and check each
of these against the current state:

1. **Mission count + tense** — any phrase like "N mission outputs
   have been produced" must match `find records/missions -name
   metrics.json | wc -l`.  Re-derive, don't trust the existing
   number.
2. **Lead showcase relevance** — the first visual artifact below
   the Overview must represent the project's *current* focus, not
   the historical showcase.  If a newer mission type has become
   the active work, it leads; older showcases move down.
3. **Pipeline / capability prose** — every numbered or imperative
   description of how a mission works ("ollama extracts 6 visual
   search terms", "letterbox-blur background") must match the
   shipped script.  Grep the relevant script before claiming a
   specific number or step still exists.
4. **Recent missions table** — drop missions older than the most
   recent week unless they carry instructive value (e.g., the
   preserved FAIL row).  Add the most recent N missions.  Don't
   append; rotate.
5. **Charts** — regenerate (`scripts/generate-charts.py`).  If the
   chart's scope no longer represents current activity (e.g.,
   highlight-only chart while recent work has been faceless),
   add an explicit scope note rather than implying the chart
   covers all missions.
6. **Status checklist** — every checked entry must point at code
   that still ships.  Every unchecked entry must carry an inline
   reason (`_blocked_` / `_deferred_` / `_parked_`).  Drop entries
   that are no longer applicable; don't keep dead ones for history.
7. **Embedded image / GIF paths** — verify each referenced asset
   still exists at its path.  If an asset's caption claims a
   specific visual feature ("KO captions reuse EN B-roll"), verify
   that's still true of the file at that path.
8. **EN ↔ KO parity** — every change to the EN file gets a mirror
   change in the KO file in the same commit.  Drift between the two
   is audit-relevant.
9. **Status section count** — README and Status are inventory, not
   priority; `docs/roadmap.md` is the priority queue.  If a
   Status entry has migrated to roadmap "Done", check it off here
   rather than re-claiming it as pending work.

This is a checklist, not a prescription — finishing the 9 items in
one sitting is the goal, not just running through them
mechanically.  The point is: read the *whole file* as if you were a
new reviewer landing on the README for the first time, and ask
"would I get an accurate read of the system in 30 seconds?"  Where
the answer is no, edit.

### Idle-state signaling

When a turn finishes and there is no further work in progress, end
the message with an explicit, **colored** marker so the operator
can scan the bottom of the message and instantly tell idle from
in-progress.  The user is async and otherwise has to ask "are you
working or done?" to find out — the marker eliminates that
question.

Format:

- **True idle** (no background tasks, no scheduled follow-up):
  close with `🟢 **대기중.**` on its own line.  May precede with
  one sentence naming the next decision point if there is one.
- **Background task running** (e.g., a long ffmpeg job started
  with `run_in_background`): close with
  `🟡 **대기중** (background: <one-line description>)` — the agent
  is idle for input but compute is still happening.
- **Active work continuing into the next turn**: do **not** write
  the marker — it is only for genuine idle.  An interim turn
  inside a multi-turn task closes without any marker.

Choice of colors mirrors a traffic-light convention: 🟢 = clear,
nothing running; 🟡 = clear for input but compute in flight; (no
marker) = still working, expect more output.

This pairs with `never-pause` (§2): keep going through the
roadmap *Next* queue without asking, and only signal idle when
there is genuinely nothing queued and no in-flight work.

### Scrum-master footer

Every reply that involves work closes with a fixed 3-line footer
block.  The purpose is mechanical re-anchoring at end of turn so the
operator picks the thread back up at zero cognitive cost.

```
[Next Action] — one sentence, the single most concrete next step.
                No alternatives, no "or".
[Git Commit]  — short hash + subject of the commit that just landed
                on origin/main.  This is the commit Claude already
                ran (per §1 + §6) — NOT a paste-ready future command.
                If no commit landed this turn, write "none this turn".
[Pace]        — remaining estimated time on the current micro-task
                + one short focus line.  Dry tone, not cheerleader.
```

Companion rules:

- **15-min micro-tasks.**  When a request expands past ~20 min of
  work, decompose into 15-min chunks before starting.  Name the
  first chunk in `[Next Action]`.
- **Scope-creep nudge.**  If the user veers into an abstract or
  large-design question mid-mission, the `[Pace]` line names
  the deviation and pulls back to the current Now-queue item.
  Answer the question briefly; do not refuse it.
- **Skip the footer for** pure clarification Q&A (e.g.,
  "what file does X live in?" — single-fact answer, no footer).
- **Layer order** in a typical reply:
  1. `[관리자 브리핑]` opener (Dual-stack reporting)
  2. Body (work updates)
  3. Idle marker (🟢/🟡) when applicable
  4. 3-line scrum-master footer

This is a literal deviation from the persona spec ("paste-ready
git command") — see [[scrum-master-mode]] memory for the reasoning.
The deviation is principled: §1 forbids pasting commands at the
user, so `[Git Commit]` becomes a record of what Claude already did
instead of an instruction for the user to execute.

---

## Memory and this file

`~/.claude/projects/-Users-melons-ai/memory/` mirrors these rules
into one feedback file per rule for fast lookup at conversation
start.  Each memory file's description should match the
corresponding section heading here.  The MEMORY.md index links each
entry back to a section in this file.

If a memory file disagrees with this file, **this file wins**.  Fix
the memory file in that case, not the contract.
