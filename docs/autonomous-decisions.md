# Autonomous decisions log

> Append-only log of decisions the agent made **without operator input**
> during autonomous (overnight / between-session) work.  Goal: when the
> operator wakes up they can scan a single page to see what was decided,
> instead of typing "어디까지 했어?" / "what's the state?" prompts and
> driving up the
> [intervention chart](metrics/intervention.png) Panel B count.

## What goes here

- Decisions where the agent picked one option from several plausible
  paths (per [[minimize-intervention]] "default to recommended option")
- Lever / hypothesis dismissals — when a planned approach turned out
  wrong on closer look
- Architecture / scope nudges the agent made unilaterally because of
  [[infra-maintenance]] / [[no-premature-done]]
- Cross-session decisions affecting future agent runs

## What does NOT go here

- Atomic implementation moves (those are in `git log`)
- Status / progress reporting (use `docs/daily/<date>.md` for narrative)
- Goal changes (those need operator OK — go in `docs/goal.md`)
- Anything in `agents/*.md` / `.claude/agents/*.md` (logic-changes-need-OK)

## Helper

```bash
scripts/log-decision.sh "Lever 1 classifier tightening dropped — spot-check showed no false positives"
```

Appends one bullet under today's section.  Auto-creates the date
header if today's section doesn't exist.

---

## 2026-05-25 (autonomous)

- `22:58 KST` — Pipeline fixes (pix_fmt yuv420p + tpad clone) being committed to mainline. These are bug fixes (Mac yuv444p broke iOS playback + audio-longer-than-video duration mismatch). Applied during batch-3 production, rescued 5 of 10 renders. Not feature work; not requiring agents/*.md edit; OK to land per infra-maintenance rule.
- `22:58 KST` — Mix #1 (yt-mix-1, id 9SqgNBKk5JE) kept public despite quality complaint + Reused Content yellow icon. Material impact $0 pre-YPP. Kept for 48h data collection. Mix #2 paused on Mac (same pipeline would yield same quality). Wait for Windows + NVENC + better source design.
- `22:58 KST` — TT boost decision timeline locked to 5/26 evening (not 5/27-28) after operator correction. My initial draft pushed timeline back based on 그 작은 손 strong organic signal (341 view + 4 comments in 21h on 15-follower channel). Operator overrode: '하루만 더 보자'. Decision tree: 1000+ no-boost, 500-1000 hold, <500 boost 어디쯤이야 v1 $20.
- `22:58 KST` — Windows + RTX 4070 Ti Super pivot decided — Mac slowness on 44min mix render (50min wall) + Pollinations rate-limit + AI hand quality + NVENC absence all converge. Operator opted to install Claude Code on Windows directly via WSL2. Mac becomes secondary. Bootstrap docs to be written ON Windows against actual machine, not generated on Mac.
- `23:05 KST` (Windows session) — **WSL2 path REJECTED** in favor of native Windows. Mac handoff recommended WSL2 + Linux toolchain (~4-6h setup). Windows session evaluated and chose native: ComfyUI portable + git-bash for legacy .sh + native ffmpeg-NVENC. Native setup completed in ~30 min (24/7 power config + ffmpeg n8.1.1 with NVENC+libass+libplacebo+whisper at G:\\tools\\ffmpeg + ComfyUI v0.22.0 + LTX-Video 2B). WSL2 adds 50GB+ disk, file I/O boundary cost, GPU passthrough overhead — all penalties for our img2vid workload. Documented in docs/platform-windows.md.
- `23:10 KST` (Windows session) — **SVD-XT 1.1 abandoned** for Mix #2 motion. Stability AI gating (HF login + license accept required) blocks autonomous install. Pivoted to **LTX-Video 2B v0.9.5** (Lightricks, open-license, no gating). Faster (27s for 4-sec clip on 4070 Ti SUPER), same img2vid capability, smaller VRAM (6GB model + 4.5GB T5 fp8 encoder = ~11GB total).
- `23:25 KST` (Windows session) — **Cross-platform tier A formalized** (operator chose "풀 크로스플랫폼화" earlier). Added .gitattributes (LF for .sh/.py/.md/.yaml, CRLF for .ps1/.bat), scripts/windows/setup-env.ps1 (idempotent Windows env setup), docs/platform-windows.md (Windows tier docs mirroring mac/linux). README EN+KO platform tables updated with Windows column.
- `23:35 KST` (Windows session) — **Mix #2 pipeline = 3-stage Python orchestrator** (cross-platform). scripts/ltx-img2vid.py (image -> 5s clip via ComfyUI REST) + scripts/mix2-design-matrix.py (segment matrix with sub_mood × time_of_day diversity) + scripts/mix2-build.py (Pollinations stills -> LTX clips -> ffmpeg NVENC compose). Hand-avoidance baked in: HAND_FORBIDDEN_PROMPT_TOKENS check + global AI_HANDS_AVOID_NEGATIVE prompt. Resumable via skip-if-exists.
- `23:40 KST` (Windows session) — **Mix #1 source music structure**: yt-dlp opus webm extraction of YT id 9SqgNBKk5JE (44.45MB, 2685s duration confirmed). ffmpeg silencedetect at -35dB/1s found 9 boundaries; lofi cross-fades hide some track separators. tracks.json synthesized with 12 entries (split long "blocks" into a/b pairs). 598 segments emitted by mix2-design-matrix.py, total 2677.6s. Sub-mood distribution 11-17%, time-of-day evenly split 33% each.
- `23:42 KST` (Windows session) — **POC = first track only** (5 min, 26 segments). Validates pipeline end-to-end before committing 4-5 hour overnight render of all 598 segments. POC kicked off 23:42 KST; Pollinations slower than expected (~90s/still vs target 5-10s) so POC ETA ~50 min total.
- `04:55 KST` (Windows session, 2026-05-26) — **OAuth set up on Windows + channel keywords applied.** Operator pushed back on Mac file transfer ("귀찮음"), preferred new Cloud Console OAuth client.  Created new "youtube-uploader-windows" client in existing yt-shorts-uploader-496910 project, downloaded JSON, minted request.token via google-auth-oauthlib InstalledAppFlow.  Applied RECOMMENDED_KEYWORDS to ToddStudio channel (351/500 chars, EN + KR mix).
- `05:07 KST` (Windows session, 2026-05-26) — **Mix #2 uploaded** as video `7f7PeuNuIfI`, scheduled public 2026-05-26 19:13 KST (off-hour evening prime per 8 YT tips #5).  First upload attempt failed with `invalidTags` (582 UTF-8 bytes > 500 limit due to Korean tags = 3 bytes each).  Curated to 27 tags / 399 bytes (kept high-volume EN SEO terms + top 6 Korean), retried successfully.  2 manual YT Studio steps remain (auto-chapter OFF + AI content disclosure) — Data API doesn't expose.
- `05:15 KST` (Windows session, 2026-05-26) — **Operator feedback on Mix #2 → Mix #3 pivot.** Operator: visual cuts distracting + AI text artifacts grotesque, prefers "single hero clip × infinite loop, audio variation only".  Added two permanent rules to operator-private memory: "AI text avoid" + "Hero-loop preference".  Ship Mix #3 = `scripts/mix3-hero-loop.py` orchestrator + `docs/mix-3-design.md`.  Generated 5 hero candidates (rooftop / neon-void / rain-glass / mist-mountains / aurora) via LTX-Video 40-step / 1024x576 / 193-frame (8s).  Test mix `outputs/publish/mix-3-test/yt-mix-3-rooftop-rainy-night-2026-05-26.mp4` built (44m 45s, 169 MB, no video re-encode).  Default hero = rooftop-rainy-night (idx 0); operator picks on return.
- `23:50 KST` (Windows session) — **8 YT settings** from operator-shared video https://www.youtube.com/watch?v=5QVuFhhcCrU codified. Automatable subset shipped in scripts/yt-channel-settings.py (channel keywords 500 chars via Data API channels.update + --check / --apply-keywords commands). Non-API tips (layout, AI disclosure toggle, auto-chapter off) documented in config/mix-2-upload-meta.template.json as operator-action notes. OAuth path: reuses youtubeuploader credentials, pending operator client_secrets.json transfer from Mac.
## 2026-05-22 (overnight autonomous)

- `16:55 KST` — docs/intervention-log.md + scripts/intervention-log-add.sh shipped — qualitative companion to intervention.json (count) + autonomous-decisions.md (agent). Captures SUBSTANCE of operator-shaping decisions: summary + why + shipped + tag (direction/taste/correction/hypothesis-rejection/preference/guard/constraint). Privacy contract: synthesize, no verbatim. Backfilled today + 5/21 + 5/19 + 5/18 from commit history
- `16:35 KST` — README EN+KO Design notes Operator tooling 블릿에 docs/operator-tooling.md 카탈로그 링크 추가
- `16:30 KST` — docs/operator-tooling.md shipped — single-page catalog of 12 operator-tooling commands with what/when/output table + per-tool reference + composition diagram. Discoverability for the stack shipped this autonomous window
- `16:20 KST` — for-analysts inventory adds morning-brief.sh + roadmap-done-sync.sh entries — discoverability for the two operator-tooling commands shipped this autonomous window
- `16:15 KST` — Cleared 45th-cycle [info]: shot-plan.sh, roadmap-done-sync.sh, morning-brief.sh added to for-analysts inventory. Plus 2nd run of roadmap-done-sync backfilled latest commits.
- `16:05 KST` — scripts/roadmap-done-sync.sh shipped — eliminates manual Done-backlog reconciliation work that audit cycles 39+ kept flagging. Preview mode + --apply mode, idempotent re-run. Test (5/5 PASS) pins the regression where the v1 awk -v with multi-line var nuked the roadmap on first --apply. 36-commit backfill applied to docs/roadmap.md.
- `15:55 KST` — Doctor gains intervention-trend check — turns the chart into an alert signal. WARN if user-ratio 7d avg > 50%, OR if direction contains 'user-ratio↑'/'prompts↑'. PASS shows headline number ('30.3% user-ratio 7d (direction: stable)'). Operator's morning-brief now reacts to intervention regression.
- `14:35 KST` — scripts/test-log-decision.sh shipped (5/5 PASS) — applies the [[idempotency-test-first]] rule to log-decision.sh: validates same-day calls nest under one date header, newest-first ordering, boilerplate preservation, and documents current no-dedup behavior
- `14:30 KST` — Memory entry [[idempotency-test-first]] added — generalizable lesson from today's install-claude-local stacking bug. Future scripts that modify persistent state should ship idempotency test in same commit
- `14:20 KST` — scripts/test-install-claude-local.sh shipped — 7 asserts validate idempotency + legacy migration + path substitution. test-all.sh auto-discovers via ls scripts/test-*.sh glob. Caught a regression: legacy '└─...┘ -->' was wrongly treated as block-end, leaking body content; fixed.
- `14:00 KST` — install-claude-local.sh idempotency bug fixed — single-line BEGIN/END markers + improved awk pattern + pre-substitution of @@REPO_ROOT@@ before awk feed. Operator's ~/.claude/CLAUDE.md cleaned 271→142 lines, 9 stacked openers→1 single comment, md5 stable across 3 reruns
- `13:45 KST` — Intervention chart split EN/KO + visual polish — bigger figure, legends below panels (no bar overlap), per-bar % labels only (removed totals clutter), CJK font for KO. README EN→intervention-en.png, README.ko→intervention-ko.png
- `05:30 KST` — CLAUDE.md session-start protocol now includes reading docs/autonomous-decisions.md + suggests morning-brief.sh. Future session starts auto-discover the overnight signal stack
- `05:20 KST` — Hardcoded '-Users-melons-ai' in generate-intervention-chart.py replaced with str(ROOT).replace('/','-') derivation. Plus docs/review-digest.md gitignored. Plus 11-commit Done backlog reconciled.
- `05:15 KST` — intervention-chart-collect.sh now auto-mirrors PNG into site/assets/ — no more manual copy step needed. Daily 02:00 KST launchd job keeps site asset in sync
- `05:05 KST` — L1 audit hook trampoline now auto-logs audit verdict transitions (DRIFT→CLEAN or CLEAN→DRIFT) to autonomous-decisions.md. Morning brief sees these without needing alert diffs
- `04:55 KST` — morning-brief.sh surfaced in README EN+KO + site/index.html (Operator tooling card). Discoverability for the canonical 'what happened overnight?' command
- `04:45 KST` — L1 post-commit audit hook gains coalescing lock — sentinel file at records/audit/.hook.inflight tracks in-flight pid; subsequent drift-risk commits within an audit's runtime defer rather than spawn new claude CLI processes. Saves Max-plan tokens during commit bursts
- `04:35 KST` — scripts/morning-brief.sh shipped — single-command digest combining doctor + audit + intervention trend + commit attribution + decisions + review queue + blockers. Operator types one command, reads ~30 lines, knows overnight state
- `04:20 KST` — Phase 16 — intervention.json gains trend_7d field (last7 avg + prev7 avg + delta + direction hints). 7-day comparison populates from 2026-05-29; currently null prev7 since only 9 days of data
- `04:10 KST` — README EN+KO Design notes section refreshed — operator tooling bullet now describes all 5 reduction scripts (doctor, audit-skill-drift, statusline, log-decision, review-queue)
- `04:00 KST` — §8 registry restructured to drop line numbers — anchors by filename + pattern instead, audited via grep. Prevents coordinate-staleness from recurring (39th audit's structural-fix suggestion)
- `03:55 KST` — Site refresh: case-studies count 6→8, operator-tooling card expanded (statusline + log-decision + review-queue), intervention chart copy refreshed to 2-panel, alt text updated
- `03:45 KST` — Daily report written — docs/daily/2026-05-22-overnight-intervention.md — operator can scan this + autonomous-decisions.md in <2min on return
- `03:40 KST` — 38th audit cleared 10 prior findings + flagged 5 new (4 low + 1 medium); all 5 addressed in this batch
- `03:35 KST` — Engineering case study #8 written EN+KO — intervention measurement as the unmeasured axis. Portfolio signal per [[repo-as-credibility-signal]]
- `03:20 KST` — Phase 6 shipped — doctor.sh now reports actionable_warn (excludes opt-in env keys + git-tree). Statusline doctor:⚠N count dropped from 7 to 3 (real items only)
- `03:05 KST` — Lever 10 shipped — statusline now surfaces goal:N/M subgoal progress alongside doctor health
- `03:00 KST` — Autonomous-decision log infrastructure shipped (lever 9)
- `02:55 KST` — Closed 7-cycle §8 audit drift across 7 scripts
  (`ffmpeg-throttled`, 5 music-video helpers, `doctor.sh`) +
  rewrote the §8 exception registry in `docs/operator-contract.md`
  with correct line numbers; updated `docs/architecture.md` Layers
  table to document the `outputs/publish/upload-meta-v2/` v2 batch
  exception (intentional 5/21 a182380) and added a row for
  `outputs/review-queue/`.  Expected next audit verdict: CLEAN.
- `02:50 KST` — **Lever 1 (classifier tightening) INVALIDATED.**
  Spot-checked 5 commits flagged as user-initiated false-positives
  in the reduction memo; all 5 were legitimately user-initiated
  (`cc6a104` has `Requested-by: user` footer; others have explicit
  "Operator strategic shift" / "Per operator '다해봐'" prefixes).
  Conclusion: classifier is tuned correctly; the 36% 5/21 ratio is
  honest signal, not over-counting.  Dropped lever 1; will not
  spend cycles on regex tweaks.
- `02:50 KST` — Shipped **lever 3 (review queue, lever 4 already
  committed earlier)**.  `outputs/review-queue/` + 3 scripts
  + music-video mission post-render hook.  New renders enqueue
  automatically instead of pinging the operator.  Expected effect:
  ~10× drop in per-render review prompts (same total decision
  count, batched).
- `02:35 KST` — Shipped **lever 4 (statusline absorbs doctor signal)**.
  `scripts/statusline.sh` now reads `/tmp/cc-doctor-cache.json`
  (60s TTL, background regen) and renders `doctor:✓/⚠N/✗N` +
  `audit⚠` suffix when `docs/audit/CURRENT-ALERT.md` exists.
  Operator no longer has to type "what's the state?" prompts.
- `02:00 KST` — **Restored intervention chart** to README EN + KO
  under new "Autonomy signal" section.  Chart had been silently
  dropped in 5/18 `aa10ba0` README rewrite; data was 2 days stale.
  Extended the generator to 2-panel signal (commits + Claude Code
  session JSONL mining for prompt count + active minutes).
  Daily 02:00 KST launchd job (`com.melons.agents.intervention-chart`)
  installed for ongoing auto-refresh.

## How to interpret this log

When the operator opens the laptop in the morning, they should be
able to read this section top-to-bottom in <60s and understand:

1. What's now different in the repo (each bullet ties to a commit
   range — see git log if details needed)
2. What was decided NOT to do (lever dismissals are recorded too —
   so the same hypothesis doesn't get re-explored)
3. What's still queued (nothing here means nothing queued; the
   roadmap "Now" / "Next" is authoritative for that)

If a bullet's reasoning needs to be revisited, the corresponding
commit message + git diff is the durable record; this log just
makes it scannable.
