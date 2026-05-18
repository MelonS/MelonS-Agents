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

_Last updated 2026-05-18 ~14:10 KST.  Previous active goal
"Production-ready music-video short" closed 2026-05-17 ~23:30 KST
(operator uploaded `03e-velvet1-jazz-combo.mp4` to YouTube; goal
migrated to `docs/goal.md` Past goals).  No active goal — operator
sets the next one._

_Operator-actioned, not a new goal_: 24h post-publish metrics
capture in
[`docs/pilots/first-upload-metrics.md`](pilots/first-upload-metrics.md)
once the upload crosses the 24h mark (~23:30 KST 2026-05-18).

> Open queue items below in "Next" are eligible today — see #1
> (filter-repo backup-branch deletion, eligible 2026-05-18+).  None
> blocks operator's next-goal decision; advance only with explicit
> direction since branch deletion is a destructive remote op.


## Next — queued, in priority order

1. **Delete filter-repo backup branch** — on or after 2026-05-18, if no
   issues observed with the rewritten history, delete the safety branch
   `main-backup-pre-filter-20260517-173615` from both local and origin.
   Command: `git branch -D main-backup-pre-filter-20260517-173615 && git push origin --delete main-backup-pre-filter-20260517-173615`.
   This was the rollback safety net created before the 2026-05-17 email
   history rewrite (replacing the old personal commit email with the
   GitHub noreply form).  Once confirmed stable, the branch is dead
   weight.

_(beyond #1: promote a deferred item from `docs/copyright-policy.md`
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

- **2026-05-17** (~22:00 KST) **Post-processing shader layer for music-video.**
  Four ffmpeg-only shader effects landed in `scripts/music-video-shaders.sh`
  (committed; ~190 lines including the docstring): `pond` (animated water-
  surface displacement via geq + displace), `breathing` (5 s-period upscale-
  only zoom), `halation` (warm bloom around bright pixels), and `combo`
  (pond + halation with phrase-aware strength envelope tied to a 95.8 BPM
  reference cadence — off at intro / full at climax / taper at outro).
  Operator validation: `pond` "완전 잘되고", `halation` "확실히 티남",
  `breathing` "괜찮네", `combo` rendered as `03e-velvet1-jazz-combo.mp4`
  for review.  Cartoon (cel-shading) attempted via lutyuv posterise but
  rejected ("완전 그냥 초록색만 나옴" — chroma quantisation broke hue);
  parked as a separate R&D branch (would need GLSL / EbSynth / AI
  stylisation rather than ffmpeg).  README EN + KO mirror both updated
  with effect descriptions and reproduction commands.  Commit `23832fa`.

- **2026-05-17** (~20:00 KST) **§8 plist templating + new active goal.**
  Closed the [low, carry-forward] §8 audit finding ("Four launchd plists
  hardcode /Users/melons/...") that persisted across 14+ audits.  Plists
  now render from committed `*.plist.template` sources via `sed`
  substitution of `@@REPO_ROOT@@` / `@@HOME@@` at install time, so a
  machine swap doesn't leave hardcoded `/Users/melons/...` paths in
  place.  Verified byte-identical render against committed pre-refactor
  plists.  Commit `ab6555e`.  Same session: 15th contract audit
  persisted (`b268ca2`), new active goal set in `docs/goal.md`
  (production-ready upload candidate, cost-minimal mode).

- **2026-05-17** (15:30 KST) **Music-video mission shipped + niche pivot
  to format option 3.**  Original goal A/B (Hittites topic vs Hydrogen
  topic) resolved as a format pivot rather than a topic pick: operator
  confirmed satisfaction with `music-video-velvet1` v5 prototype
  (music-as-sole-audio + phrase-aligned cuts + onset-aligned glitches
  on static-camera clips only).  Promoted prototype to
  [`agents/missions/music-video/run.sh`](../agents/missions/music-video/run.sh)
  with aubiotrack beat detection + aubioonset drum-hit detection +
  per-keyword motion/speed classification + Pexels caching for motif
  reuse, all bash 3.2 compatible.  Decision-log entry at
  [`pilots/decision-log.md`](pilots/decision-log.md#operator-pick--2026-05-17).
  Commit `828070f`.

- **2026-05-17** (~13:00 KST) **Disk-watch infrastructure** (periodic
  monitor every 30 min + pre-render guard inside faceless-short) and
  **selective records cleanup script**.  Internal SSD recovered from
  8.6 GB free → 34 GB free (Unity 17 GB + Ollama models 6.7 GB +
  intermediate records 3.4 GB).  Commits `eb93015` (cleanup script),
  `1537ca6` (disk-watch + plist + pre-render guard).

- **2026-05-17** (~13:30 KST) **Scrum-master footer convention** in
  operator-contract: every work-bearing reply ends with
  `[Next Action]` / `[Git Commit]` / `[Pace]`.  Plus the `[EPM Nudge]`
  → `[Pace]` rename to keep imported jargon out of the repo.
  Commits `6f45fa6`, `50168f4`.

- **2026-05-17** (~14:00 KST) **GitHub Pages site + engineering case
  studies + LinkedIn footer.**  Pages live at
  https://melons.github.io/MelonS-Agents/.  `docs/engineering-case-studies.md`
  + KO mirror frame four production-incident decisions (Tier-1
  routing, semaphore-throttler, content-quality feedback loop,
  three-layer reactive audit).  Commits `e07411d`, `fb6fdd2`, `75b10a8`.

- **2026-05-17** (overnight, ~04:00 KST) **Reactive auditor L1 + L2
  + README full-file review pass + operator-contract HOW rule.**
  Operator flagged two systemic problems in one session:
  (1) auditor only runs daily 03:00 + manual — drift can exist for
  up to 24h before catch; (2) README updates are append-only,
  existing sections silently rot (mission count "15" while reality
  is 32, animated preview showcasing last week's highlight while
  faceless is the current focus, recent-runs table missing the v4/v5
  pilots, charts unchanged, KO B-roll description directly contradicting
  v4 per-language-keyword behaviour).
  - **L1** (`scripts/hooks/post-commit.sh` via
    `scripts/install-hooks.sh`): git post-commit hook fires
    `audit-run.sh contract` in background when a commit touches
    drift-risk paths (`agents/`, `.claude/agents/`, `config/`,
    `CLAUDE.md`, `docs/operator-contract.md`, `scripts/audit-run.sh`,
    `.claude/settings.json`).  End-to-end validated: commit `7c6ff4f`
    touched `docs/operator-contract.md` and the hook fired
    `[audit-hook] firing audit-run.sh contract in background after
    7c6ff4f` on stdout.  Trigger logged at
    `records/audit/hook-trigger.log`.
  - **L2** (`scripts/audit-poll.sh` via
    `com.melons.agents.audit-poll.plist`, loaded by
    `install-scheduler.sh install audit-poll`): 15-min poll
    detects NEW BLOCKER (any new file in `records/blockers/<date>/`)
    + QA-FAIL BURST (≥2 mission qa-report.md with `Verdict: FAIL`
    within 60 min).  Fires audit-run.sh with the appropriate focus.
    First-run mode seeds the seen-blockers list with existing files
    and does NOT fire — stops false-positive on pre-install state.
  - **Observer pattern rejected** — subagents in this repo aren't
    long-running observables; communication is via files.  Reactor
    + Hook patterns are the actual fit.  Pushed back honestly on
    Gemini's pattern recommendation before implementing.
  - **README full review** EN + KO: mission count rederived, lead
    showcase swapped to faceless v5, pipeline prose synced with
    shipped code (8 windows not 6, caption-split step documented),
    KO B-roll description rewritten to match v4 reality, Recent
    missions table rotated to current week, chart scope explicitly
    labelled "v1 highlight only".
  - **operator-contract.md HOW rule**: Conventions / README
    maintenance now defines a 9-item full-file checklist that runs
    every time a cadence trigger fires — stops the append-only
    failure mode.  Also §5 — defined `Requested-by: user` commit
    footer as the audit-trail marker.
  - **Audit-cleanup commit** (`fbf3d70`) before the L1/L2 build:
    cleared stale `docs/audit/CURRENT-ALERT.md` lifecycle bug,
    fixed §8 hardcoded-path comment in `scripts/statusline.sh`,
    normalized `.claude/settings.json` double-slash permission
    patterns (`//Users/...` → `/Users/...`), added §8 exception
    comment to `scripts/audit-run.sh` launchd-fallback loop.
    Audit re-run after this verified CLEAN.
- **2026-05-17** (overnight, ~03:30 KST) **v5 pilots — single-line
  caption enforcement, 2-line opaque-box overlap eliminated.**
  Operator feedback after watching the v4 pilots: caption boxes from
  consecutive cues grazed each other when libass wrapped a cue onto
  2 lines (BorderStyle=3 opaque box per line), the visual artifact
  was distracting enough to block a clean niche A/B decision.  New
  `scripts/split-long-captions.py` runs between caption-correction
  and ASS rendering — splits any cue whose text exceeds CHAR_MAX
  (default 28) at natural punctuation breaks (commas, em-dashes,
  periods — they match speech pauses so the cut doesn't read as
  awkward), falls back to greedy word-split for remaining long
  chunks.  Sub-1s cues merge into their previous sibling so we
  don't emit blips.  Wired into `agents/missions/faceless-short/run.sh`
  (commit `61fac70`).  A v5 attempt that also rewrote the script +
  B-roll prompts regressed quality (qwen2.5:7b copied prompt
  examples verbatim, all 8 windows pulled the same Pexels clip;
  script ran ~230 words past the 60s target) — reverted to v4
  baseline prompts; only the caption splitter landed.  Re-rendered
  all 4 pilots with `FACELESS_SCRIPT_OVERRIDE` + `FACELESS_REUSE_BROLL`
  so the only delta from v4 is caption rendering.  Total compute:
  ~3m 21s for all four (B-roll reuse skips Pexels API + per-window
  keyword extraction).  v5 mission IDs:
  - `faceless-hittites-032538` (EN, 62.7 s, 49 MB, 32 cues from 18 split).
  - `faceless-hittites-ko-032653` (KO, 60.3 s, 35 MB, 23 cues from 10 split).
  - `faceless-hydrogen-032742` (EN, 59.7 s, 21 MB, 34 cues from 11 split).
  - `faceless-hydrogen-ko-032846` (KO, 38.9 s, 14 MB, 16 cues from 6 split).
  v4 thumbnails in `docs/pilots/screens/` overwritten with v5 captures.
  Goal subgoals 2 + 3 still ticked (Hittites + Hydrogen deliverables),
  v5 mission paths updated in `docs/goal.md` + `decision-log.md`.
  Operator pick (subgoal 4) still the only gate to goal completion.
- **2026-05-17** (overnight, ~01:50 KST) **Per-window B-roll keyword
  extraction — visuals track the caption being spoken.**  Operator
  feedback after watching the Korean v3 pilots: "the more the video
  and captions match the context, the more interesting it would be"
  — v3's 6-equal-slot B-roll didn't track narration beats, so a
  caption about Hugo Winckler's 1906 discovery might play over a
  generic ruins clip from a different beat.
  Fix structure: the caption-corrected SRT already carries whisper
  timing.  New `scripts/plan-broll-windows.py` groups cues into N
  (default 8) temporal windows of variable duration matching the
  natural narration beats.  Stage 4 in `run.sh` now sends each
  window's text individually to ollama with the topic as global
  context → one search term per window; Stage 5 fetches one Pexels
  clip per window; Stage 6 trims each clip to its window's exact
  duration (not `NARRATION_DUR/N`).
  Results validate the architecture:
  - EN Hittites window 6 (caption: Treaty of Kadesh): keyword
    `Treaty of Kadesh map`, exact contextual match.
  - KO Hittites window 4 (이집트 양식이 어우러진): `Mesopotamian architecture`.
  - KO Hittites window 5 (무와탈리 2세): `Muwatalli II portrait`.
  - KO Hydrogen window 5 (약 1킬로그램, 큰 설탕 한 봉지): `sugar bottle` —
    exact metaphor match, the visual literally matches the
    narration's literal-bag-of-sugar image.
  Side effect: EN and KO variants no longer share B-roll (each
  language extracts its own keywords from its own captions, so
  visual-equality A/B is gone).  `FACELESS_REUSE_BROLL` env still
  works if the shared-visuals comparison is wanted again.
  Four v4 pilots produced: `faceless-hittites-014312` (EN, 62.8s/49MB),
  `faceless-hittites-ko-014703` (KO, 57.8s/32MB),
  `faceless-hydrogen-014508` (EN, 63.7s/22MB),
  `faceless-hydrogen-ko-014816` (KO, 38.9s/13MB).  Thumbnails +
  scripts + caption-correction logs + window-keyword JSONs all
  committed under `docs/pilots/screens/`.
- **2026-05-17** (overnight, ~00:09 KST) **Operator review pass —
  screen-fill 9:16 + Korean A/B variants.**  Operator looked at the v2
  pilots and flagged two issues for accurate evaluation:
  (1) the foreground occupied a small strip in the middle of a mostly-
  blurred frame — Pexels stock is landscape, `force_original_aspect_ratio=decrease`
  was producing 1080×607 fg over 1080×1920 letterbox-blur background;
  (2) need Korean voice + Korean captions on the **same content** to
  judge the format independent of language.  Both fixes landed in this
  pass:
  - **Screen-fill 9:16**: per-clip trim now uses `scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920`
    directly so concat clips are already vertical.  Final filter graph
    drops the bg/fg/overlay stack; only ass-burn + drawtext attribution
    remain.  Result fills the frame the way TikTok/Reels actually do.
  - **Korean A/B variants**: `agents/lib/tts.sh` now routes by voice-hint
    pattern — Kokoro-shape hints (`^[abjzefhip][fm]_`) go to Kokoro,
    anything else (Yuna, Daniel, etc.) goes to `say`.  Kokoro v1.0 has
    no Korean voice; macOS `say` has nine ko_KR voices including Yuna.
    Two new run.sh env vars: `FACELESS_SCRIPT_OVERRIDE` bypasses ollama
    script generation with a pre-written file, and `FACELESS_REUSE_BROLL`
    copies a previous mission's stitched B-roll so the localized variant
    shares identical visuals with its English counterpart.
  - **Korean translation**: llama3.2:3b's Korean output was unusable
    (Hindi/Thai/Russian script leak, topic confusion across prompts).
    Manually translated the two scripts directly.  Noted in the
    decision log; a 7B+ instruct model is the path forward for
    automated localization.
  - 4 pilots committed: `faceless-hittites-000112` (EN, 55.2s/42MB),
    `faceless-hittites-ko-000654` (KO, 52.9s/40MB, same B-roll),
    `faceless-hydrogen-000112` (EN, 38.5s/12MB),
    `faceless-hydrogen-ko-000755` (KO, 38.9s/12MB, same B-roll).
    Thumbnails + scripts + caption-correction logs all renamed to
    `<topic>-<lang>-*` shape under `docs/pilots/screens/`.
    Decision-log restructured with side-by-side EN/KO columns per pilot.
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
