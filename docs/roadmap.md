# Roadmap

Day-level focus document. **Source of truth for "what to work on next."**
README's Status section is a flat checklist — do not use it for picking work.

> Maintenance contract:
> - **"Now" / "Next" / "Blocked"** sections are edited by the user. Claude
>   reads them but only appends suggestions in a `<!-- suggest -->` HTML
>   comment, never silently rewrites.
> - **"Done"** is appended to by Claude when work lands (commit hash + date).
> - If goals shift mid-day, the user edits "Now". Claude re-reads at the
>   start of each new conversation turn that asks for work.

---

## Now — active focus

_Set by shutdown protocol on 2026-05-15: session closed cleanly with
all queued items shipped.  Pick the resume point on the next session
in this order:_

1. Read **`docs/daily/2026-05-16-overnight.md`** for what landed
   overnight and the user-action queue.
2. If the user has a new directive, set it as "Now" and start.
3. Otherwise, promote a candidate from `docs/copyright-policy.md`
   "Still TODO" block — those are scoped, real, and load-bearing
   ahead of any external publish.
4. The pre-existing **Iterative QA-feedback loop inside editor**
   stays parked below (in "Blocked / parked") — its own description
   defers it until we see compute pressure, which hasn't happened.

- [ ] _(intentionally empty — the session ended on a complete state.
  See above for resume order.)_

<!-- suggest (Claude, 2026-05-15 overnight, awaiting user OK)
  Two items shipped tonight script-level:
  1. docs/ideas.md (parking log, v1-promise device)
  2. scripts/audit-run.sh active surface → docs/audit/CURRENT-ALERT.md

  Two items still gated on user approval (proposal at
  docs/proposals/2026-05-15-auditor-active.md):
  - Apply Part A: paragraph in .claude/agents/auditor.md "Principles"
    declaring the alert surface (so the agent knows its report is
    post-processed).
  - Apply Part B: line in CLAUDE.md session-start protocol pointing
    to CURRENT-ALERT.md.

  Both are 1-paragraph edits. Promote whichever the user OKs into Now.
-->


## Next — queued, in priority order

_(no items queued — promote a deferred item from `docs/copyright-policy.md`
("Still TODO" block) when one becomes load-bearing, or set a new focus.)_

## Blocked / parked

- **Real user-supplied URL fixture** — needs a URL from the user. Catalog
  currently lists only Blender open-movie samples (CC-BY) + Pexels API
  via `scripts/pexels-fetch.sh`.
- **Iterative QA-feedback loop inside editor** — finer-grained than the
  mission-level retry shipped on 2026-05-15.  Have the editor re-cut a
  single failing window without rerunning transcribe/select.  Worth
  picking up when the coarse retry is observed to waste compute on a
  per-output basis.  Touches `agents/lib/ffmpeg.sh` (re-cut helper) +
  an opt-in flag in each mission's retry loop.

## Done — most recent first

- **2026-05-16** (overnight) Audit parser regression test +
  `docs/audit/` directory README.  `scripts/test-audit-parser.sh`
  exercises the verdict-parsing block in `audit-run.sh` against
  synthetic CLEAN / DRIFT_DETECTED / CRITICAL reports in a `/tmp`
  sandbox; 6 cases, 6/6 PASS on first run (after a `set -e` shadowing
  fix in the test harness itself).  `docs/audit/README.md` orients
  any human picking up the repo: report file convention,
  `CURRENT-ALERT.md` lifecycle, manual trigger commands, retention,
  playbook for resolving an alert (commit `bc2381b`).
- **2026-05-16** (overnight) README Status reconciliation.  Status
  had 1 stale unchecked item (per-platform reuse rules, shipped in
  `ef0f825`) — checked off + added an entry for the auditor active
  surface.  Every remaining unchecked item now carries an italicized
  inline reason (`_blocked_` / `_deferred_` / `_parked_`).  Trailing
  note pins the policy: Status is inventory, the day-level priority
  queue lives in `docs/roadmap.md`.  Mirrored in `README.ko.md`
  (commit `d547a32`).
- **2026-05-16** (overnight) Per-platform reuse rules in
  `guard_publish`.  Pulled from `docs/copyright-policy.md` "Still
  TODO" — the third of three deferred copyright items.  `guard_publish`
  now takes an optional platform arg (`internal-demo` default; `public`
  / `youtube` / `instagram` / `tiktok` aliases) and consumes all four
  `publish_rules` fields (`publish_blocked`, `require_attribution`,
  `share_alike`, `commercial_repost`).  v1 binary check was leaving
  75% of the rule schema unread.  Exit codes 0/3/4/5 unchanged
  (stable contract); new codes 7 (commercial repost forbidden) and 8
  (missing attribution on public target).  16/16 PASS across all
  license × platform combinations (commit `ef0f825`).
- **2026-05-15** (overnight) Auditor active surface via wrapper.
  `scripts/audit-run.sh` now extracts the audit verdict and maintains
  `docs/audit/CURRENT-ALERT.md` — a stable, committed alert file that
  exists iff the latest audit verdict is non-CLEAN (DRIFT_DETECTED or
  CRITICAL).  Self-clears on the next CLEAN run.  Auditor agent itself
  is unchanged (logic-changes-need-OK rule); the wrapper does all the
  active surface work.  Verified with three synthetic verdicts.  Two
  follow-up edits gated for user approval — paragraph in `auditor.md`
  Principles + line in `CLAUDE.md` session protocol — described in
  `docs/proposals/2026-05-15-auditor-active.md` (commit `a37d37f`).
- **2026-05-15** (overnight) `docs/ideas.md` parking log created with
  3 starting categories (Agents / Pipeline+Infra / Intelligence+Misc).
  First entry: Scout agent (external information gathering), parked
  for v2+, language toned down per `writing_tone` rule.  Holds the
  v1-only promise — new ideas land here instead of derailing the
  main pipeline (commit `71d785f`).
- **2026-05-15** Auditor goes autonomous + statusline live.
  `scripts/com.melons.agents.auditor.plist` schedules
  `audit-run.sh all` daily at 03:00 local via launchd.
  `scripts/install-scheduler.sh` now manages both the queue and
  auditor jobs (`install [queue|auditor|all]`); rewritten without
  bash-4 associative arrays since macOS ships bash 3.2. Auditor
  loaded and waiting for its 03:00 fire (`RunAtLoad=false` to avoid
  surprise token spend at install). cc-statusline (chongdashu, 598⭐)
  installed via `npx @chongdashu/cc-statusline@latest init`; wired
  into `~/.claude/settings.json` so the terminal now shows
  `dir · git · model · context-remaining` at the bottom on every
  refresh. The auto-generated `.claude/statusline.sh` is gitignored
  (per-user, regenerable).
- **2026-05-15** Repository auditor agent. New
  [`.claude/agents/auditor.md`](../.claude/agents/auditor.md) — a
  read-only subagent (model: sonnet) that walks the whole repo and
  writes a structured report to
  `docs/audit/<ISO-date>-<focus>.md`. Six audit dimensions:
  architecture-vs-docs drift, roadmap freshness, operator-contract
  compliance, cost-model accuracy, stale TODOs / dead code,
  security / secrets. Invocation wrapper at
  [`scripts/audit-run.sh`](../scripts/audit-run.sh): supports a
  focus arg (`roadmap` / `contract` / `security` / `all`).
  Distinct from `qa` (mission-scoped); the auditor is project-wide.
  Reports go to `docs/audit/` (committed) so the trail survives a
  machine swap.
- **2026-05-15** Minimal Claude Code statusline at
  [`scripts/statusline.sh`](../scripts/statusline.sh) — zero-dep
  bash script that reads the JSON Claude Code feeds it on stdin
  and prints `dir · git · model · cost · session-id` on a single
  line. To enable, the user adds 4 lines to `~/.claude/settings.json`
  (or runs `/config` interactively). Heavier alternatives noted in
  the script header (chongdashu/cc-statusline, 598⭐, adds context
  bars + burn rate but pulls npm dependencies).
- **2026-05-15** Analyst-facing docs.  New
  [`docs/for-analysts.md`](for-analysts.md) is the single-file entry
  point for read-only review of the repo — orientation, subagent
  table, retry semantics, common-mistakes pre-empt list.  New
  [`docs/cost-model.md`](cost-model.md) makes the Tier-1 (Anthropic)
  vs Tier-2 (local Ollama / whisper.cpp / ffmpeg) split explicit
  with a per-call cost table.  [`docs/architecture.md`](architecture.md)
  one-glance map updated to mark the same Tier 1 / Tier 2 boundary
  on the diagram.  Motivation: an external analyzer mis-tiered the
  architecture and recommended optimizations to the wrong layer;
  these docs short-circuit that for future analysts.
- **2026-05-15** Pexels Videos integration. New
  `scripts/pexels-fetch.sh` queries the Pexels Videos API by search
  string, picks the smallest file ≥ `min_height` (default 720), and
  drops `<id>.mp4` + `<id>.meta.json` into `/tmp/smoke/pexels/`.
  `agents/lib/attribution.sh` learned to read a `<source>.meta.json`
  sidecar at the *first* resolution step, so Pexels fetches don't
  need fixture-catalog edits — the photographer + Pexels-license is
  pulled automatically and lands in `SOURCES.txt` / the burned
  watermark. `config/copyright-allowlist.yaml` adds
  `videos.pexels.com` (license `pexels-license`, commercial reuse
  OK, attribution appreciated but not required).  Verified: fetch
  "ocean waves" → 1280×720 / 34s clip + sidecar; summarize on the
  clip recorded "Video by Wave Stock Footage Free on Pexels" /
  `pexels-license` in `outputs/SOURCES.txt` before the transcribe
  step (silent nature footage; transcribe step would fail on any
  source without speech, separate from the attribution flow).
- **2026-05-15** Operator contract committed at
  `docs/operator-contract.md` (47c7a18). Twelve operating rules
  that had lived only in `~/.claude/projects/-Users-melons-ai/memory/`
  (machine-local, vulnerable to a MacBook swap) now have a single
  canonical source-of-truth file in the repo. CLAUDE.md shrinks to
  a four-bullet summary + pointer; memory becomes a fast-access
  cache that links each entry back to the matching contract
  section. "If memory disagrees, this file wins."
- **2026-05-15** License-string probe for archive.org + wikimedia
  commons. `probe_license(url, out_json)` reads the per-item license
  metadata (archive.org's `/metadata/<id>` JSON and the wikimedia
  `extmetadata` API), maps CC license URLs / short codes onto canonical
  tags (`CC-BY-3.0`, etc.). `resolve_final_license` glues it into each
  mission: when the allowlist says `requires-per-item-probe`, the probe
  runs, `FIXTURE_LICENSE` gets populated, and `resources/license.json`
  records the provenance. End-to-end verified: archive.org BBB URL →
  probed → CC-BY-3.0 → publish gate accepts.
- **2026-05-15** Strike-aware source rejection — the strike log is no
  longer write-only. `check_source_allowed` consults
  `records/strikes.log` *before* the allowlist; a URL with any prior
  strike is refused (exit 6) even if its domain is otherwise
  permitted. Refusal surfaces the original strike row to stderr.
  Verified: baseline blender.org URL passes; after `append_strike`,
  same URL refused with strike provenance; after cleanup, baseline
  restored.
- **2026-05-15** Automated copyright filter v1. New
  `config/copyright-allowlist.yaml` (Blender + Xiph + archive.org +
  wikimedia.org permissive domains, per-license publish rules), new
  `agents/lib/copyright.sh` (`check_source_allowed`, `guard_publish`,
  `append_strike`), new `scripts/publish-gate.sh` stub for the future
  `publish.sh`. All three missions abort with exit 67 when invoked
  against a non-allowlisted URL; local file paths bypass (fixture
  catalog handles them). Verified: blender.org → CC-BY-3.0;
  example.com → refused with helpful stderr; locally-generated →
  publish gate refuses (correct); CC-BY-3.0 → publish gate accepts.
  Deferred items (strike-aware rejection, license probe, audio
  fingerprint, logo detection) listed in `docs/copyright-policy.md`
  with rationale for each.
- **2026-05-15** QA feedback retry loop across all three missions.
  New `agents/lib/retry.sh` (qa_extract_feedback / qa_feedback_block /
  qa_write_blocker), wrapped highlight + summarize + shorts-batch in
  a retry loop capped by `QA_RETRY_MAX` (default 2 retries → up to 3
  attempts). On exhaustion writes a halt log under
  `records/blockers/<ISO-date>/<mission-id>.md`. Verified end-to-end:
  regression on summarize/synthetic_lecture PASS-on-attempt-1; forced
  failure on highlight (impossible `QA_DUR_MIN=999`) → 2 attempts
  both FAIL, model picked a different window on attempt 2 (feedback
  injection works), blocker file written.
- **2026-05-15** Source-attribution wiring propagated to summarize +
  shorts-batch. Extracted the 45-line resolver block from
  `highlight/run.sh` into a shared `agents/lib/attribution.sh` with
  `resolve_source_attribution()` + `write_sources_record()`. All three
  missions now emit `outputs/SOURCES.txt`; summarize also appends a
  "Source & license" footer to `summary.md`; shorts-batch passes the
  attribution string through to `ffmpeg_render_short` so every short
  in the batch gets the burned-in watermark.
- **2026-05-15** Visual layout verification on real footage. Found a libass
  scaling bug (Fontsize interpreted against default 384×288 PlayRes →
  fonts rendered 6.67× too large at 1920px output). Fixed by generating
  an explicit `.ass` sidecar with `PlayResY=1920` and switching the
  renderer from `subtitles=…:force_style=` to `ass=`. All four layout
  elements verified on Sintel: source-attribution top-left, blurred-fill
  9:16 background, centered foreground, bottom-center caption box inside
  the safe zone.
- **2026-05-15** "Agent does everything, user never touches terminal"
  operator contract pinned across CLAUDE.md, README EN/KO, and memory
  (`d171d29`). Split-commit-push pattern documented as the canonical
  workflow (`&&`-compound blocked by the auto-mode classifier; not worth
  fighting).
- **2026-05-15** `docs/roadmap.md` as source of truth for "what to work
  on next" + session-start protocol pinned to CLAUDE.md (`dae3d58`).
  Root cause: README's flat Status checklist was being read as a TODO
  list, leading to wrong-task selection earlier in the day.
- **2026-05-15** Real CC fixtures + standard layout + source-attribution
  (`8ae9449`). Replaced dead Google `gtv-videos-bucket` URLs with Blender
  CDN; fixed nested-heredoc-in-process-substitution bug in
  `fetch-fixtures.sh`; layout engine now enforces safe-zone margins +
  semi-transparent caption box + top-left source-attribution overlay.
- **2026-05-14** README EN/KO split + style guide applied (`a2d0949`,
  `e947dc0`).
- **2026-05-14** Shutdown report `docs/today-summary.md` (`ee833a0`).
- **2026-05-14** Longer bootstrap fixtures + full E2E across 3 mission
  types (`b485f29`, `e91f29b`).
- **2026-05-14** Shorts-batch mission, queue-based scheduler, per-mission
  metrics, single-pass ffmpeg render, libass burned captions — see
  `git log --oneline` between `d25b462` and `b485f29` for the full thread.

---

## Why this file exists (incident note, 2026-05-15)

Earlier today I (Claude) read the README's flat Status checklist and
proposed working on the QA retry loop, when the actual active goal —
established in the previous session — was "escape the alien aesthetic"
(real CC fixtures + layout engine + source-attribution). The user had to
manually steer back to the right thread. Root cause: no ordered, dated,
single-source-of-truth document for "today's focus" that survives across
sessions. This file is the fix.
