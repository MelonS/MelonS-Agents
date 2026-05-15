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

_Promoted by Claude per "Never pause" rule: previous "Now" finished, top of
"Next" queue takes over automatically._

- [ ] **Iterative QA-feedback loop inside editor** — finer-grained than the
  mission-level retry shipped today. Have the editor re-cut a single
  failing window without rerunning transcribe/select. Only worth doing
  if the coarse retry loop is observed to waste compute on a per-output
  basis. Touches `agents/lib/ffmpeg.sh` (re-cut helper) + an opt-in
  flag in each mission's retry loop. Probably defer until we have
  takedown data or compute pressure.

## Next — queued, in priority order

_(no items queued — promote a deferred item from `docs/copyright-policy.md`
("Still TODO" block) when one becomes load-bearing, or set a new focus.)_

## Blocked / parked

- **Real user-supplied URL fixture** — needs a URL from the user. Catalog
  currently lists only Blender open-movie samples (CC-BY).

## Done — most recent first

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
