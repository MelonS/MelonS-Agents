# 2026-05-25 (evening) — Windows pivot handoff

> Session handoff document.  Mac Claude Code session ended ~21:00 KST after
> operator decision to migrate active work to Windows (RTX 4070 Ti Super).
> This doc is the **single source** for Windows session to pick up state.

## TL;DR for Windows pickup

1. **Active goal unchanged** — multi-skill framework + music-video skill
   refinement.  Music-video is the active production line.
2. **Production pipeline working** — batch-3 (10 vocal shorts) shipped,
   7 of 10 uploaded to YT scheduled through 2026-06-01, 6 of 13 posted
   to TikTok with mid-tier organic reach.
3. **One longform experiment shipped** (`yt-mix-1-korean-lofi-rainy-seoul.mp4`,
   YT id `9SqgNBKk5JE`, 44min instrumental mix, public).  Visual quality
   poor — fix path = Windows + GPU encode + better source materials.
4. **Pending decision** — TikTok paid boost for `어디쯤이야 v1`:
   evaluate 2026-05-26 ~21:00 KST per thresholds in
   [[#tiktok-boost-decision-tree]].
5. **Windows migration in progress** — Bootstrap docs to be written
   *on Windows* (not Mac) to test the flow against the actual machine.

---

## Production state snapshot

### YouTube (channel)

| Asset | Status | Notes |
|---|---|---|
| 10 vocal shorts batch (`shorts-2026-05-23-batch/`) | 7 uploaded, 3 pending | quota 10K/day, ~1600 per upload |
| `yt-mix-1-korean-lofi-rainy-seoul` (44min instrumental mix) | public, id `9SqgNBKk5JE` | flagged Reused Content (yellow), no real impact pre-YPP |
| 06 smallhand-folk-v2 | scheduled 2026-05-31 09:00 KST | next quota window |
| 08 smallhand-younha-v2 | scheduled 2026-05-31 21:00 KST | next quota window |
| 04 convenience-v2 | scheduled 2026-06-01 09:00 KST | next quota window |
| 10 smallhand-en-v2 | scheduled 2026-06-01 21:00 KST | next quota window |

### TikTok (channel, 15 followers as of 2026-05-25)

| Asset | Posted | 21h~24h view | Like | Comment | Engagement |
|---|---|---|---|---|---|
| rainy window (lofi) | 5/20 21:00 | 794 | 23 | 0 | 2.9% |
| **어디쯤이야 v1** | 5/22 21:00 | **721** | **48** | 0 | **6.7%** (top) |
| **그 작은 손** | 5/24 23:53 | **341 in 21h** | 13 | **4** | 3.8% (most comments) |
| late light | 5/23 21:00 | 288 | 6 | 2 | 2.1% |
| tomorrow is a question | 5/24 09:00 | 269 | 15 | 0 | 5.6% |
| midnight rambler EN | 5/24 21:00 | 200 | 7 | 0 | 3.5% |
| 사이렌 v1 | 5/23 09:00 | 232 | 11 | 0 | 4.7% |
| (plus 6 older posts, 80~263 views range) | | | | | |
| 4 scheduled v2 posts | 5/26~5/27 9am/9pm | 0 | 0 | 0 | — |

**Pattern**: vocal tracks > instrumental on engagement rate. `어디쯤이야`
is the breakout candidate.  `그 작은 손` has the highest comment density
(4 comments on 21h-old post = exceptional for 15-follower channel).

### TikTok boost decision tree (open, evaluate 2026-05-26 ~21:00 KST)

| `그 작은 손` 48h cumulative view | Action |
|---|---|
| 1000+ | **No boost** (organic push working, don't interrupt) |
| 500~1000 | **Hold** (re-evaluate 5/27 evening) |
| <500 | **Boost `어디쯤이야 v1` $20** with Followers goal |

Note: operator's standing 2026-05-24 decision was "wait until 5/26
evening".  My earlier draft tried to push to 5/27~5/28 then snapped back
to 5/26 on operator correction ("하루만 더 보자").  Original timeline holds.

---

## Mix #1 post-mortem (yt-mix-1)

Visual quality flagged poor by operator on review.  Root causes — only
one is GPU-related:

| Issue | Cause | Fix path |
|---|---|---|
| **Bitrate 3.8 Mbps for 1080p** | libx264 medium + CRF 22 = under-provisioned | Windows NVENC + 12~15 Mbps |
| **Static-looking footage** | All sources were stills + ffmpeg zoompan (fake Ken Burns) | Stable Video Diffusion img2vid OR actual Pexels video clips |
| **Jarring transitions** | 12 tracks concatenated with no fade/crossfade | Pipeline design — add transitions, cap track count per mix |

Only `Bitrate` is solved by GPU.  The other two are design issues that
need addressing regardless of platform.

YT monetization status: "수익창출제한 (Reused Content)" yellow icon.
**Material impact: $0** (channel not YPP-eligible).  Algorithmic reach
not affected.  Keeping the video up for data collection (48h watch
pattern as control sample).

---

## Windows migration rationale

Operator's machine: Windows 11 + RTX 4070 Ti Super (16GB VRAM).

What this unlocks beyond "render faster":

| Current Mac pain | Windows resolution | Multiplier |
|---|---|---|
| libx264 medium = 44min mix takes 50min | NVENC h264 = ~5~7 min | 7~10× |
| Pollinations.ai rate-limited (1/min) | local SDXL/Flux unlimited | 100× |
| AI hands grotesque ([[ai-hands-avoid]]) | SDXL + handfix LoRA + ControlNet pose | mostly solved |
| Fake motion (zoompan on stills) | Stable Video Diffusion (img2vid) | real motion |
| KlingAI API requires separate payment (Premier $80 web ≠ API) | local SVD = free | $0 |
| 1080p ceiling | 4K NVENC possible | resolution headroom |

### Migration setup path (recommended)

1. **WSL2 + Ubuntu 22.04** (Microsoft Store) — keeps Linux env identical
   to Mac, all existing bash scripts + ffmpeg + python venv work as-is
2. **NVIDIA Windows driver + CUDA Toolkit** — enables GPU passthrough
   to WSL2 (supported on Win11 since 2022)
3. **Claude Code inside WSL2** (Linux build) — not Windows-native, to
   match Mac's environment exactly
4. **Repo clone**: `git clone` to `/Users/melons/ai` inside WSL (same
   path as Mac — keeps `~/.claude/projects/-Users-melons-ai/memory/`
   directory mapping identical)
5. **Memory transfer**: rsync `~/.claude/projects/-Users-melons-ai/memory/`
   from Mac → Windows WSL.  This is operator-private context; **not**
   committed to public repo.  Transfer mechanism: rsync over SSH, USB,
   or compressed tarball via cloud drive — operator's call.
6. **Tool install**: ffmpeg-nvenc (apt or compile), ollama, yt-dlp,
   youtubeuploader, python3-pip + diffusers/ComfyUI
7. **Continue**: same git remote, same workflow

### What stays on Mac

- Mac becomes secondary monitor / backup
- Mac's `outputs/publish/` archive — kept for reference, not re-rendered
- Mac's `~/.config/youtubeuploader/` OAuth tokens — can be copied to
  Windows or re-authenticated
- Mac's `~/.config/kling/credentials` — copy to Windows
  (operator-private, not committed)

### Memory directory transfer details (operator-only, do not auto-execute)

The `~/.claude/projects/-Users-melons-ai/memory/` directory contains
~30+ markdown files capturing operator preferences, project state,
references.  These are operator-private (job-hunt context, billing
plan, TikTok follower count, etc.) and never go in the public repo.

Two transfer options:

```
# Option A: rsync over SSH (if Mac SSH'able from Windows)
rsync -avz /Users/melons/.claude/projects/-Users-melons-ai/memory/ \
  <windows-wsl-host>:/Users/melons/.claude/projects/-Users-melons-ai/memory/

# Option B: tarball via cloud drive (iCloud / Google Drive)
cd ~/.claude/projects/-Users-melons-ai
tar czf /tmp/memory-2026-05-25.tar.gz memory/
# upload to cloud → download on Windows → extract to same path
```

Verify post-transfer on Windows side:

```
ls ~/.claude/projects/-Users-melons-ai/memory/ | wc -l  # should be 30+
cat ~/.claude/projects/-Users-melons-ai/memory/MEMORY.md | head -10
```

---

## Uncommitted Mac work being committed in handoff push

(See accompanying commit messages for full diff.  Listed here for the
Windows session reading this doc.)

### New scripts (additive, no impact on existing code paths)

- `scripts/batch-recover.sh` — bulk recovery for batch-rejected mission
  outputs (applies `video-audio-pad.sh` to all in `records/missions/<date>/`)
- `scripts/video-audio-pad.sh` — standalone utility to fix
  stream-duration-mismatched mp4s via ffmpeg tpad clone
- `scripts/build-full-pollinations.sh` — 28-segment AI-generated music
  video pipeline (folk track template, hardcoded LRC)
- `scripts/build-full-pollinations-monday.sh` — variant for `monday` track
- `scripts/pollinations-gen.sh` — single-clip Pollinations image → Ken
  Burns video utility
- `scripts/kling-test.py`, `scripts/kling-generate.py` — KlingAI API
  client (auth + text2video + status polling); blocked by Premier $80
  web ≠ API payment gap, kept for documentation

### New documentation

- `docs/music-video-render-checklist.md` — 5-section operator checklist
  derived from this session's render failures
  (A: design pre-render, B: genre safety table, C: post-render validation,
  D: batch-specific, E: red flags)

### New audit files

- `docs/audit/2026-05-23-all.md`
- `docs/audit/2026-05-23-contract.md`
- `docs/audit/2026-05-24-all.md`
- `docs/audit/2026-05-25-all.md` (latest, DRIFT_DETECTED: model-
  assignment drift now 5 cycles unresolved)

### New lyric assets

- `assets/lyrics/folk-small-hand.txt`
- `assets/lyrics/folk-small-hand-en.txt`
- `assets/lyrics/comedy-monday.txt`
- `assets/lyrics/comedy-convenience.txt`
- `assets/lyrics/_suno-prompts.md` (catalog of Suno prompts used)

### Modified pipeline files (autonomous fixes during batch render)

- `agents/missions/music-video/run.sh` — added `tpad=stop_mode=clone:
  stop_duration=999` to final concat stage + forced `pix_fmt yuv420p`.
  Fix for duration mismatch (audio longer than concatenated video) +
  Mac yuv444p pixel format that broke playback on Windows/iOS.
  Applied during batch-3 production; rescued 5 of 10 failed renders.
- `scripts/music-video-grade.sh`, `music-video-lyrics.sh`,
  `music-video-shaders.sh` — `pix_fmt yuv420p` enforcement applied
  consistently across all 32 ffmpeg invocations in the pipeline

These are **fix** not **feature** — both bug fixes for production
breakage caught during operator's batch review.

### Modified asset

- `assets/lyrics/kpop-ballad.txt` — minor revision during 어디쯤이야
  v2 lyric polish

### Modified docs

- `docs/audit/2026-05-22-contract.md` — addressed prior audit
- `docs/audit/CURRENT-ALERT.md` — refreshed to current cycle
- `docs/onboarding/fresh-clone-log.txt` — appended v0.4.0 fresh-clone
  test result (PASS, see commit `587ad01`)
- `docs/metrics/intervention*.{png,json}` — updated chart with current
  user-ratio (chart-only update)
- `site/assets/intervention*.png` — mirror copy for marketing page

### Deletion

- `outputs/demos/2026-05-21-genre-shader-experiments/README.md` —
  obsolete after genre shader work landed mainline

---

## Open agenda (for Windows session to consider)

These are not committed work items; they're conversation threads left open.

1. **Windows bootstrap scripts** — operator decided to do bootstrap *on
   Windows directly* rather than have Mac generate them.  Windows
   session writes `docs/windows-wsl2-bootstrap.md` against the actual
   machine.
2. **Mix #2 design** — paused on Mac.  Windows session should design
   from scratch with NVENC + better sources in mind.  Operator's
   guidance: focus on quality over speed.
3. **AI image local stack** — ComfyUI vs Automatic1111 vs InvokeAI
   decision pending; depends on whether operator wants GUI workflow
   editing or just CLI batch generation
4. **TikTok strategy** — vocal-track focus validated.  v2 series test
   pending (4 scheduled posts 5/26~5/27).  Compare engagement vs v1.
5. **YT longform mix strategy** — operator approved original direction
   ("긴 믹스가 답") despite Mix #1 visual quality issues.  Once Windows
   pipeline ready, second mix attempt with proper bitrate + sources.

---

## Companion memory references

(Read these in `~/.claude/projects/-Users-melons-ai/memory/` after rsync)

- `feedback_vocal_never_cut.md` — never truncate vocal songs
- `feedback_yt_scheduled_videos.md` — check `status.publishAt`
- `feedback_ai_hands_avoid.md` — AI hand rendering grotesque
- `feedback_music_video_mode_validated.md` — vocal-primary direction
- `feedback_music_video_quality_bar.md` — 6 directives from 2026-05-22
- `project_boost_decision_pending.md` — TT $20 boost decision tree
  (5/26 evening, updated this session)
- `reference_billing_plan.md` — Claude Max $200/mo, token-only
  reporting (no $ math)

---

## Commit attribution for this handoff

Commits landing with this handoff (all under one `chore(handoff)` group):

- `feat(scripts): batch-recover + video-audio-pad + Pollinations + Kling utilities`
- `feat(lyrics): 4 new original tracks + Suno prompt catalog`
- `docs(handoff): 2026-05-25 Windows pivot session record`
- `docs(audit): 2026-05-23~25 audit cycles`
- `fix(music-video): force pix_fmt yuv420p + tpad final concat (duration mismatch + Mac pixel format)`
