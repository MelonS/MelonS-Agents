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

_Last updated 2026-05-16 afternoon.  Active goal in `docs/goal.md`
is currently **empty** — the "Clone-and-go reproducibility" goal
shipped at `d8f29e9` and migrated to Past goals.  Pick the resume
point on the next session in this order:_

1. Check `docs/goal.md` — if user has set a new active goal, work it.
2. Check `docs/audit/CURRENT-ALERT.md` — if present, address the
   audit finding before picking up the goal.
3. If neither, look at this section's open items (currently none)
   or promote a deferred item from `docs/ideas.md`.
4. The pre-existing **Iterative QA-feedback loop inside editor**
   stays parked below (in "Blocked / parked") — its own description
   defers it until we see compute pressure, which hasn't happened.

- [ ] _(intentionally empty — last goal landed cleanly.  Waiting
  on the user to set the next active goal in `docs/goal.md`.)_


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

- **2026-05-16** (late evening, ~23:38 KST) **Upload-metadata generator —
  ready-to-paste platform copy for each pilot.**  The pilot deliverables
  produce `short.mp4` but the next bottleneck is the operator drafting
  4 platform's worth of copy by hand (YouTube Shorts title +
  description, TikTok caption, Reels caption, hashtag set, attribution
  credits).  New [`scripts/gen-upload-metadata.sh`](../scripts/gen-upload-metadata.sh)
  reads a mission directory, aggregates per-clip Pexels attribution from
  the sidecar JSONs (dedup by photographer, page URLs preserved), asks
  ollama to draft per-platform copy in strict-JSON shape with tone
  guardrails (no clickbait, no all-caps, no emoji, no "mind-blowing"),
  and writes `outputs/upload-metadata.md` next to the rendered short.
  Run against both v2 pilots; copies committed to
  [`docs/pilots/upload-metadata/hittites.md`](pilots/upload-metadata/hittites.md)
  and [`docs/pilots/upload-metadata/hydrogen.md`](pilots/upload-metadata/hydrogen.md)
  so the operator can review on phone/desktop without diving into
  `records/`.  Quality observation: small-model copy is decent
  starter material — title and reels caption land well, hashtags
  occasionally drift on the lowercase rule (one camelCase leak in
  Hittites set).  Acceptable as a draft pass; operator reviews before
  uploading.
- **2026-05-16** (late evening, ~23:34 KST) **Script-aware caption
  correction — v2 pilots re-rendered with clean proper nouns.**  The v1
  Hittites pilot exposed a real defect: whisper-cpp small mis-transcribed
  `Hattusa` → `Hadusa` (and `Winckler` → `Winkler`, etc.) on proper
  nouns the small model has no training mass for.  Key insight: when
  the audio is synthesized from a script we wrote, the SCRIPT is ground
  truth for TEXT and whisper is only needed for TIMING.
  New [`scripts/correct-captions.py`](../scripts/correct-captions.py)
  tokenizes both, runs `difflib.SequenceMatcher` (case-folded,
  punct-stripped) to align whisper tokens against script tokens, and
  emits a corrected SRT that uses the script's wording at whisper's
  timestamps.  Wired into `agents/missions/faceless-short/run.sh` between
  the whisper step and the ASS sidecar generation.  Re-ran both pilots:
  Hittites (`faceless-hittites-233021`) corrected 5/21 cues including
  `Hadusa` → `Hattusa`, `Sipululiumii` → `Suppiluliuma I`,
  `archeological` → `archaeological` ×2;  Hydrogen
  (`faceless-hydrogen-233219`) corrected 4/18 including `75%` → `75 percent`
  and dash punctuation around `H2O`.  V2 thumbnails, scripts, and full
  correction logs committed under
  [`docs/pilots/screens/`](pilots/screens/);
  [`docs/pilots/decision-log.md`](pilots/decision-log.md) updated to
  point at the v2 mission IDs and note the defect closure.  V1
  intermediate artifacts can be garbage-collected from `records/`
  whenever (gitignored either way).
- **2026-05-16** (late evening, ~23:25 KST) **Faceless pilot A/B
  produced — Hittites + Hydrogen shorts rendered end-to-end at $0
  marginal cost.**  New mission type `faceless-short` shipped:
  `agents/missions/faceless-short/run.sh` + `agents/lib/tts.sh` with
  Kokoro-ONNX as primary TTS backend (Apache 2.0, commercial-safe —
  picked after discovering Coqui XTTS v2's Coqui Public Model License
  is non-commercial).  Pipeline: ollama → 130–160 word script →
  Kokoro `am_michael` voice → whisper.cpp captions → ollama extracts
  6 visual search terms → `pexels-fetch.sh` pulls 6 B-roll clips →
  ffmpeg 9:16 letterbox-blur stitch + libass burn-in + attribution
  overlay.  Two pilots produced:
  - Hittites (history+Bible): 57.2 s, 13 MB, mission
    `faceless-hittites-232141`.  Caption-verify
    [`docs/pilots/screens/hittites-caption-verify.jpg`](pilots/screens/hittites-caption-verify.jpg).
  - Hydrogen (science): 56.7 s, 19 MB, mission `faceless-hydrogen-232334`.
    Caption-verify
    [`docs/pilots/screens/hydrogen-caption-verify.jpg`](pilots/screens/hydrogen-caption-verify.jpg).
  Production notes + A/B comparison in
  [`docs/pilots/decision-log.md`](pilots/decision-log.md).  Pilot
  artifacts stay in gitignored `records/missions/...` (32 MB combined
  too heavy for the repo); only thumbnails + scripts in `docs/pilots/`.
  **Defects fixed during pilot run**: (1) `tts.sh` referenced the
  removed `scripts/tts-xtts.py` from the abandoned XTTS path — now
  tries Kokoro first via `from kokoro_onnx import Kokoro` probe.
  (2) `run.sh` used the bash 4.0+ `mapfile` builtin (macOS ships
  bash 3.2) — rewrote both call sites as portable `while IFS= read`
  loops.  Two subgoals from `docs/goal.md` ticked; final subgoal
  (operator decision in `decision-log.md`) awaits review.
- **2026-05-16** (evening, 19:47 KST) **Clone-and-go reproducibility
  reinforcement — three new variant tests, all PASS.**  Operator
  asked: "is the clone-and-go path *actually* covered for a stranger,
  or only on your already-set-up machine?"  Honest answer was "three
  corners untested".  All three corners now exercised:
  - `scripts/test-fresh-clone.sh --force-model-download` flag —
    overrides `WHISPER_MODEL` to a fresh temp path inside the clone
    so bootstrap calls `fetch-whisper-model.sh` and actually
    downloads `ggml-small.bin`.  Logged
    `variant=force-model-download model_download=465MB` PASS.
    The basic variant had skipped this because the host already
    had a cached model.
  - `scripts/test-bootstrap-hints.sh` — runs bootstrap under `env -i`
    with `PATH=/usr/bin:/bin` so the env.sh `command -v` discovery
    fails for whisper-cli / ollama / yt-dlp.  Asserts each is
    flagged missing AND each gets the matching macOS install hint.
    8 / 8 asserts PASS.  Validates the "stranger with no prereqs"
    path that fresh-clone test skips on the maintainer's machine.
  - `scripts/test-fresh-clone-linux.sh` — runs bootstrap inside an
    `ubuntu:24.04` Docker container with apt-installed
    ffmpeg / yt-dlp / git / curl.  Asserts apt-supplied ffmpeg's
    libass check passes, whisper-cli + ollama flagged missing with
    Linux install hints (`build from source`, `curl ... | sh`),
    macOS hint phrases (`brew install`) absent.  9 / 9 asserts PASS.
    Validates the Platform-support claim's Linux side — first
    actual Linux execution of the bootstrap.
  Suite log lives at
  [`docs/onboarding/fresh-clone-log.txt`](onboarding/fresh-clone-log.txt);
  variants documented in
  [`docs/onboarding/README.md`](onboarding/README.md).
- **2026-05-16** (afternoon, 16:51 KST) **Post-goal cleanup + manual
  audit pass.**  Five follow-up commits after the clone-and-go goal
  landed, plus one manual-audit-driven fix commit:
  - `8aa850c`  scripts/setup-venv.sh + chart-regen pointer
    so a stranger who wants to refresh `docs/metrics/*.png` after
    new missions has a one-line bootstrap path.
  - `394be57`  for-analysts.md "Reproducibility evidence" section;
    README EN/KO Status entries refreshed.
  - `5560348`  Second PASS line in fresh-clone-log.txt — re-verified
    the clone-and-go path after the polish commits, still
    passes in ~30 s.
  - `ae8eba9`  docs/known-limitations.md restructured for the
    ffmpeg-full default; README Toolchain line replaced its
    "static libass build" phrasing with the actual install
    command per OS.
  - `ce9e158`  manual audit (DRIFT_DETECTED) cleared: for-analysts
    auditor row added, 11 missing 2026-05-15 commit hashes
    backfilled, roadmap Now resume notes rewritten to current
    post-goal state, generative-AI exploration parked in
    docs/ideas.md.  Re-audit at 16:51 returned CLEAN;
    `docs/audit/CURRENT-ALERT.md` self-cleared.
- **2026-05-16** (afternoon, 14:00 KST) **Clone-and-go reproducibility
  goal achieved.**  A stranger cloning the public repo from GitHub
  HTTPS reaches a passing `short.mp4` on their own machine via
  `bootstrap.sh` + one mission run.  Six subgoals landed across
  `692c755` (host-agnostic `.env.example`, prereq-aware bootstrap
  with OS-specific install hints, whisper-model auto-fetch,
  Prerequisites + Platform-support sections in README EN/KO, goal
  decomposition) and `6349039` (env.sh smart ffmpeg discovery —
  prefers libass-enabled build, falls back to the ffmpeg-full keg
  on macOS).  Deliverable artifact:
  [`docs/onboarding/fresh-clone-log.txt`](onboarding/fresh-clone-log.txt) —
  two-line append log showing the diagnostic narrative (first run
  FAIL caught the Homebrew libass packaging change; second run PASS
  after env.sh fix).  Verified against
  `https://github.com/MelonS/MelonS-Agents.git`: 7 MB `short.mp4`
  produced in ~30 s.  Goal migrated to `docs/goal.md` Past goals.
  **Real defect uncovered**: Homebrew split `ffmpeg` (regular, no
  libass) and `ffmpeg-full` (keg-only, includes libass).  Plain
  `brew install ffmpeg` no longer suffices for the caption pipeline;
  `env.sh` now auto-detects the ffmpeg-full keg path and the
  bootstrap hint points there explicitly.
- **2026-05-16** (overnight, 01:52 KST) **First real-CC end-to-end short
  produced.**  This is the actual delivery of yesterday's "alien
  aesthetic 탈출" goal — every piece of infrastructure shipped over
  2026-05-14 → 2026-05-15 (fixture downloader / 9:16 layout engine /
  source-attribution / libass burned captions / copyright filter /
  QA retry loop) exercised end-to-end against a real CC source for the
  first time.  Mission `highlight-015213`: input
  `https://download.blender.org/durian/trailer/sintel_trailer-1080p.mp4`
  → 39-second 9:16 short.mp4 (1080×1920, 7.78MB), QA PASS on attempt 1,
  SOURCES.txt records `Sintel © Blender Foundation — durian.blender.org`
  / `CC-BY-3.0`, burned-in top-left source watermark + bottom-center
  caption box ("I'm searching for someone.") verified visually in
  [`docs/caption-verify/highlight-015213-sintel-cap.jpg`](caption-verify/highlight-015213-sintel-cap.jpg).
  **Root-cause lesson surfaced by this run**: yesterday's "Done" entries
  recorded the infrastructure landing but no entry recorded the *outcome*
  (a real short emerging from that infrastructure).  Without an outcome
  layer, a roadmap with all checkboxes ticked can still mean the goal
  isn't met — drove the creation of `docs/goal.md` in the next commit.
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
- **2026-05-15** Auditor goes autonomous + statusline live (commit
  `123f895`).  `scripts/com.melons.agents.auditor.plist` schedules
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
- **2026-05-15** Repository auditor agent (commit `af9857f`).  New
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
- **2026-05-15** Minimal Claude Code statusline (commit `af9857f`)
  at [`scripts/statusline.sh`](../scripts/statusline.sh) — zero-dep
  bash script that reads the JSON Claude Code feeds it on stdin
  and prints `dir · git · model · cost · session-id` on a single
  line. To enable, the user adds 4 lines to `~/.claude/settings.json`
  (or runs `/config` interactively). Heavier alternatives noted in
  the script header (chongdashu/cc-statusline, 598⭐, adds context
  bars + burn rate but pulls npm dependencies).
- **2026-05-15** Analyst-facing docs (commit `7a355a3`).  New
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
- **2026-05-15** Pexels Videos integration (commit `3b9175d`).  New
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
  commons (commit `e530302`).  `probe_license(url, out_json)` reads the per-item license
  metadata (archive.org's `/metadata/<id>` JSON and the wikimedia
  `extmetadata` API), maps CC license URLs / short codes onto canonical
  tags (`CC-BY-3.0`, etc.). `resolve_final_license` glues it into each
  mission: when the allowlist says `requires-per-item-probe`, the probe
  runs, `FIXTURE_LICENSE` gets populated, and `resources/license.json`
  records the provenance. End-to-end verified: archive.org BBB URL →
  probed → CC-BY-3.0 → publish gate accepts.
- **2026-05-15** Strike-aware source rejection (commit `7ca547b`) —
  the strike log is no longer write-only.  `check_source_allowed` consults
  `records/strikes.log` *before* the allowlist; a URL with any prior
  strike is refused (exit 6) even if its domain is otherwise
  permitted. Refusal surfaces the original strike row to stderr.
  Verified: baseline blender.org URL passes; after `append_strike`,
  same URL refused with strike provenance; after cleanup, baseline
  restored.
- **2026-05-15** Automated copyright filter v1 (commit `28dda8f`).
  New `config/copyright-allowlist.yaml` (Blender + Xiph + archive.org +
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
- **2026-05-15** QA feedback retry loop across all three missions
  (commit `8e71c9b`).  New `agents/lib/retry.sh` (qa_extract_feedback / qa_feedback_block /
  qa_write_blocker), wrapped highlight + summarize + shorts-batch in
  a retry loop capped by `QA_RETRY_MAX` (default 2 retries → up to 3
  attempts). On exhaustion writes a halt log under
  `records/blockers/<ISO-date>/<mission-id>.md`. Verified end-to-end:
  regression on summarize/synthetic_lecture PASS-on-attempt-1; forced
  failure on highlight (impossible `QA_DUR_MIN=999`) → 2 attempts
  both FAIL, model picked a different window on attempt 2 (feedback
  injection works), blocker file written.
- **2026-05-15** Source-attribution wiring propagated to summarize +
  shorts-batch (commit `0eaaee2`).  Extracted the 45-line resolver block from
  `highlight/run.sh` into a shared `agents/lib/attribution.sh` with
  `resolve_source_attribution()` + `write_sources_record()`. All three
  missions now emit `outputs/SOURCES.txt`; summarize also appends a
  "Source & license" footer to `summary.md`; shorts-batch passes the
  attribution string through to `ffmpeg_render_short` so every short
  in the batch gets the burned-in watermark.
- **2026-05-15** Visual layout verification on real footage (commit
  `3decfa7`).  Found a libass scaling bug (Fontsize interpreted against default 384×288 PlayRes →
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
