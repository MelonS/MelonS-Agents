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

- [ ] **Automated copyright filter** — implement the punch list in
  `docs/copyright-policy.md`: domain allowlist for source URLs,
  license-string probe for sources exposing machine-readable metadata
  (Wikimedia, Internet Archive, Vimeo CC channel), audio-fingerprint
  check (chromaprint / `fpcalc`), publish-gate hook in any future
  `publish.sh`, strike-record log. Required before any external publish
  step is wired.

## Next — queued, in priority order

1. **Iterative QA-feedback loop inside editor** — finer-grained than the
   mission-level retry: have the editor re-cut a single failing window
   without rerunning transcribe/select. Lower priority — only worth it
   if the coarse retry loop in `agents/lib/retry.sh` is observed to
   waste compute on a per-output basis (today retries the whole
   model+render block).

## Blocked / parked

- **Real user-supplied URL fixture** — needs a URL from the user. Catalog
  currently lists only Blender open-movie samples (CC-BY).

## Done — most recent first

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
