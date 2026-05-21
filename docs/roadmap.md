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

_Last updated 2026-05-20 ~19:55 KST after audit
`2026-05-20-contract.md` flagged the prior "Now" as stale
(v0.4.0 was complete but the block still said "awaiting FF-merge")._

**Active goal**: Multi-skill AI assistant framework (`docs/goal.md`
2026-05-19 entry).  Skill #1 (music-video) shipped v0.2.0 + v0.3.0.
Skill #2 (`job-hunt`) v0.4.0 **SHIPPED** — see Done entry below.

**Active subgoal — operator-activation of `job-hunt` v0.4.0**
(operator-only, no further scaffolding needed):
- `cp skills/job-hunt/config/operator-profile.example.md → operator-profile.md`
  and edit (gitignored, per-machine).
- Flip `JH_FIT_SCORE_LIVE=1` etc. per utility module to activate live
  Claude calls (Max plan absorbs; no incremental USD).
- For live KR job-board HTTP, run the per-source operator-validation
  curl + flip `JH_<source>_LIVE=1` + supply API key where required
  (`WANTED_API_KEY`, `SARAMIN_KEY`).

**Parallel context**: CRITICAL candidate goal "First-touch success
rate 10-20% → 60%+" remains filed in `docs/goal.md`.  Build Day
Seoul (2026-06-16) application landed 2026-05-19 mapping to this
candidate; if accepted, pre-build of the wizard prototype becomes
the next active goal.  Until then, multi-skill framework remains
primary.


## Next — queued, in priority order

1. **Zero-friction onboarding path — first-touch demo without
   Pexels key AND without Suno generation** — surfaced 2026-05-18
   ~19:00 KST during a real-time discussion with an external
   security professional (3 yr exp, n=1, anonymized).  Two assets
   currently gate the headline `music-video` mission, both with
   compounding friction:

   **B-roll friction (Pexels API)** — three layers:
   (a) Pexels API key required for `music-video` / `faceless-short`;
   (b) Pexels signup forces Google / Apple / Facebook OAuth (no
   email path) — friction for KR users on Naver/Kakao primary, plus
   identity-correlation risk; (c) the "Get API key" UI is buried in
   Pexels' dashboard.  Cumulative bail rate before first output ≈
   high.

   **Music friction (Suno web UI)** — current flow is fully manual:
   (a) Suno signup + OAuth; (b) write custom-mode prompt in web UI;
   (c) wait for generation, pick best of N; (d) download mp3;
   (e) drop in `assets/music/`; (f) update `SOURCES.md`.  Worse
   than Pexels — there's no API at all, every track is a manual
   round-trip.  First-time user expecting "see a demo" gets blocked
   *before* even reaching the B-roll step.

   **Security framing** (not just UX): the Pexels OAuth + buried
   API-key pattern is intentional bot defense — fighting the vendor's
   design.  First-time users editing `.env` with API keys is the
   typical credential-leak vector (GitHub auto-revoke logs show
   thousands of API-key commits per day).  A demo path that never
   touches `.env` and never opens an external signup removes the
   attack surface entirely.

   **Design principles**:
   - Zero-account first touch: clone → bootstrap → produce a
     music-video output → see result, all with no external signup
     (no Pexels, no Suno, no .env edit).
   - Gradual permission escalation: full Pexels + Suno integration
     stays available, but as an *advanced* path for users who chose
     to commit.
   - Vendor lock-in mitigation: at least one CC-licensed alternative
     supported per asset class (B-roll: Blender CDN / Wikimedia /
     archive.org; audio: Internet Archive Open Music / Free Music
     Archive / Incompetech / Bensound — CC-BY or CC0).
   - Show, don't promise: if the demo output is high enough quality,
     users self-onboard to the advanced path; if it isn't, no
     external accounts have been spent fighting through the friction.

   **Recommended implementation** (~2 days):
   - `scripts/fetch-demo-broll.sh` — pull 6–8 CC-BY video clips
     from a curated set (Blender CDN open movies, Wikimedia
     Commons, archive.org).  Domains already in
     `config/copyright-allowlist.yaml`.
   - `scripts/fetch-demo-music.sh` — pull 3–5 CC-BY / CC0 audio
     tracks (lo-fi, ambient, jazz, hip-hop categories) from
     archive.org / Incompetech / FMA.  No API key.  Persist
     attribution metadata in `assets/music/demo-SOURCES.md`.
   - `agents/missions/music-video/run.sh` `MUSIC_VIDEO_DEMO_MODE=1`
     branch — uses local demo B-roll cache + accepts a bundled
     demo track when no operator music is provided.
   - `bootstrap.sh` — if `PEXELS_API_KEY` empty AND
     `assets/music/` is empty, default to demo mode + print
     actionable next-command instead of warnings.
   - README first-run section rewritten: zero-account demo as the
     headline, Pexels + Suno integration documented as "advanced —
     unlock the full mood-keyword catalog + custom music" path.
     "Try this first, then if you want better music we'll show you
     how to upgrade."
   - Optional follow-on: bundle demo assets via git LFS (~40 MB
     B-roll + ~10 MB music = ~50 MB) so even bootstrap doesn't need
     to hit the network.  Trade-off: clone heavier.  Decide based
     on whether offline-first is a goal.

   **Open questions** (operator decides at implementation time):
   - Whether to deprecate Pexels + Suno as defaults entirely after
     demo lands, or keep both with demo as "first-touch" and
     Pexels/Suno as the "scale-up" path.
   - Whether to bundle assets (LFS) or fetch on bootstrap
     (network).  Bundled = offline-first + heavier clone; fetched
     = lighter repo + first-bootstrap hits the network.

_(Next queue is currently empty.  Promote a deferred item from
`docs/copyright-policy.md` ("Still TODO" block) when one becomes
load-bearing, or set a new focus.)_

<!-- suggest 2026-05-22 — music-video quality bar phases (A.1 shipped,
     A.2/A.3/B.1/C.1 queued).  See docs/research/2026-05-22-music-
     video-quality-bar.md for the full decomposition.

  - A.2  Lyric overlay vocal-onset alignment.  Use whisper.cpp on the
         vocal stem to detect word timings; align lyric file lines via
         fuzzy match (`scripts/correct-captions.py` pattern).  Operator
         tolerance: ±200ms.  ~2-3h.
  - A.3  Ethnicity-language match.  KR lyric → Pexels keyword anchored
         on "korean"/"seoul"; EN lyric → global / Western contexts.
         Pollinations.ai prompt template updated to specify ethnicity.
         QA gate: render fails if ≥30% of B-roll contradicts the anchor.
         ~2h.
  - B.1  Shader vocabulary survey (research + catalog expand).  Current
         15 shaders is too narrow.  Catalog ShaderToy + AE + music-video
         editor breakdowns; map per-mood-family with situational notes.
         Target ~30 shaders.  ~3-4h.
  - C.1  Shader restraint gating.  Replace blanket `shader:` preset
         field with `shader_events:` list (at musical beat/onset/bar/ts,
         duration_ms, intensity).  Default: shader-active ≤25% of total
         runtime.  Preserves an explicit `shader_always_on: true`
         opt-in.  ~3-4h.

     Operator decides ordering — A.2 (lyric sync) is the most
     viewer-visible from yesterday's batch and would be the natural
     next pick.  -->


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

- **2026-05-22** (~02:30 KST) **Intervention reduction — Lever 1
  invalidated, Lever 4 shipped**.  Operator follow-up "그럼 어떻게
  해야함?" after intervention-monitoring restore landed.  Acted on
  the 5 levers from `docs/research/2026-05-22-intervention-reduction.md`.
  (a) Spot-checked 5 commits the memo flagged as Lever 1 false-
  positive candidates (`cc6a104`, `6b61f84`, `82cb15d`, `f4881cf`,
  `f796aad`); all five are *legitimately* user-initiated (explicit
  "Operator strategic shift" / "Per operator '다해봐'" prefixes +
  `Requested-by: user` footer on cc6a104).  Lever 1 dropped — the
  classifier was tuned correctly, the 36% 5/21 ratio is honest signal.
  (b) Lever 4 shipped: `scripts/statusline.sh` extended to surface
  `scripts/doctor.sh --json` verdict (`doctor:✓` / `doctor:⚠N` /
  `doctor:✗N`) + `·audit⚠` flag when `docs/audit/CURRENT-ALERT.md`
  exists.  Cache pattern (`/tmp/cc-doctor-cache.json`, 60s TTL,
  background regen) keeps per-refresh cost <1ms while doctor's 2s
  run happens out-of-band.  Smoke validated end-to-end.  Expected
  effect: ~30% reduction in "status-check" Panel B prompts on
  routine days.  Re-measure 2026-05-29 against the 9-day baseline.

- **2026-05-22** (~02:00 KST) **Operator-intervention monitoring
  restored + extended to time/count signal**.  Operator framed the
  ask: "유저 개입 시간 횟수 등 계속해서 데이터를 쌓아야함 ...
  스스로 작업하는 시간을 늘려야함".  The chart added 2026-05-17 was
  silently dropped from README in `aa10ba0` (music-video-first
  rewrite, 2026-05-18) and data was 2 days stale.  This pass:
  (a) extends `scripts/generate-intervention-chart.py` to a 2-panel
  signal — Panel A keeps commit attribution + adds leverage ratio +
  longest autonomous gap, Panel B mines local Claude Code session
  JSONLs at `~/.claude/projects/-Users-melons-ai/*.jsonl` for
  operator-prompt count + active session minutes (capped 60/sess);
  (b) ships `scripts/intervention-chart-collect.sh` +
  `com.melons.agents.intervention-chart.plist.template` + wiring
  into `scripts/install-scheduler.sh` for a daily 02:00 KST
  regeneration job (installed and loaded);  (c) writes
  `docs/research/2026-05-22-intervention-reduction.md` with the
  current 9-day data table + 5 prioritized reduction levers
  (classifier false-positive on Korean direct-quotes; default to
  recommended option; batch taste reviews; statusline absorbs status
  checks; shipped permission bootstrap); (d) restores the chart
  reference to README EN + KO under a new "Autonomy signal" section.

- **2026-05-22** (~01:35 KST) **Music-video quality bar — Phase A.1
  B-roll dedup registry** (commit `05e6c2a`).  Operator-stated six
  quality directives 2026-05-22 ~01:30 KST; full decomposition at
  `docs/research/2026-05-22-music-video-quality-bar.md`.  Phase A.1
  ships the first: both Pexels caller paths (`scripts/pexels-fetch.sh`
  + the inline curl in `agents/missions/music-video/run.sh`) now
  consult a shared registry at `records/youtube/broll-used.txt`
  (gitignored, 196 ids seeded by `scripts/broll-history-backfill.sh`
  from prior renders) and append the chosen id after download.
  `BROLL_HISTORY=off` disables per-render.  Phases A.2 (lyric
  sync), A.3 (ethnicity-language match), B.1 (shader vocabulary
  research), C.1 (shader restraint gating) queued.

- **2026-05-22** (~01:25 KST) **YT stats Phase 1 + daily scheduler +
  dreampop drafted** (commits `a43057f`, `ec5a5bb`).  Resolves
  snapshot open item (C).  `scripts/yt-stats-collect.sh` snapshots
  view/like/comment counts for every video on the operator's uploads
  playlist via Data API videos.list (existing youtubeuploader OAuth
  scope already covers it — no new consent).  Auto-discovers
  channel via `channels.list?mine=true` (no channel id hardcoded,
  PII-clean).  Writes `records/youtube/stats/<date>.csv` +
  `<date>-raw.json`; quota cost ≈ 2 units/run.  `yt-stats-diff.sh`
  compares two snapshots, sorted by view delta.  Daily 09:00 KST
  launchd job installed (`com.melons.agents.yt-stats`).  Side
  action: dreampop `KirKdDUWOpc` (the broken 5/22 render) moved to
  privacyStatus=private with publishAt cleared via videos.update —
  the 5/24 21:00 publish is defused; re-render or delete decision
  deferred.

- **2026-05-21** (~03:00 KST, autonomous continued) **Skill #2
  `job-hunt` — fit-score hire_prob dimension + worknet region
  parser fix** (commit `6b61f84`).  Operator insight 2026-05-21
  ~15:00 KST: "본인이 갈수있는 회사중에 가장 좋은회사를 찾는게
  베스트이지 않을지?".  fit-score now emits role_fit + hire_prob
  + composite score (0.6 × role_fit + 0.4 × hire_prob).  New
  operator-profile.example.md section "Hire-bar comfort"
  documents the four tier calibration (high / medium / low /
  very-low) the operator records.  Side burr: worknet region
  parser no longer falls back to work-pattern chips ("주5일",
  "교대근무") when no admin-region keyword matches.  Tests
  68/68 PASS.

- **2026-05-21** (~02:30 KST, autonomous continued) **Skill #2
  `job-hunt` — kr-saramin live path activated + 4 KR ATS boards
  added** (commit `cc6a104`).  kr-saramin.sh live HTTP path
  fully wired against the verified Saramin OpenAPI spec
  (https://oapi.saramin.co.kr/guide/job-search); flips on with
  JH_SARAMIN_LIVE=1 + SARAMIN_KEY.  Operator-issued key pending
  Saramin's approval queue (likely morning business hours).
  ats-boards.example.yaml gains 4 KR companies on Greenhouse
  (coupang 486 / daangn 44 / sendbird 18 / krafton 54) — most
  KR-domestic companies run self-hosted careers pages but these
  4 use Greenhouse.

- **2026-05-21** (~02:00 KST, autonomous continued) **§6 branch
  strategy revised to flexible / worktree-based** (commit
  `6ddba86`).  Operator direction "유연한 전략이 필요하다 지금 먼가
  타이트하게 박아 버리면 계속 못지킬 가능성 생김" after the
  thirtieth contract audit caught 9 structural commits landing
  directly on main across two parallel sessions.  Hard "feat
  branch + 4-gate" rule replaced with judgment-based guideline
  (table in §6).  Worktree mode recommended for parallel
  sessions on one machine.  Two helper scripts ship:
  `scripts/worktree-new.sh <topic>` (sibling worktree creation)
  and `scripts/worktree-done.sh` (rebase + FF main + cleanup).
  Both smoke-tested end-to-end.  Memory updated
  (`branch-strategy-strict` → `branch-strategy-flexible`).

- **2026-05-21** (~00:30 → 03:00 KST, autonomous overnight)
  **Skill #2 `job-hunt` — 5 live-ready plugins + survey + 43
  curated ATS boards** (commits `b3789ba` survey + `58a2b58`
  first 3 plugins + `a6c39c4` HN + worknet + `91c0a40` README
  EN/KO cadence + `8667da7` daily report + `62730cb` ATS list
  expand).  Operator request 2026-05-20 ~23:50 KST: enumerate
  every place job postings appear, test fetch viability.  Survey
  at `docs/research/job-sources-survey-2026-05-21.md` (30+ sites
  classified Tier 1-5 by legal posture).  Five new live-ready
  plugins ship zero-key live HTTP: `global-ats` (Greenhouse +
  Ashby + Lever, 43 boards), `global-remoteok`, `global-remotive`,
  `global-hn-whoshiring` (HN monthly thread via Algolia HN
  Search), `kr-worknet` (정부 공공고용서비스).  Permanent-mock
  conversion for `kr-jobkorea` (robots + 2017 precedent) and
  `kr-programmers` (service closed 2025-05-19).  End-to-end live
  pull on Problem-Solver seed: 5,000+ raw → 278 matched
  (Anthropic / Scale AI / Notion FDE Korea / Databricks Forward
  Deployment Engineer Seoul / Cohere Applied AI Korea / etc.).

- **2026-05-21** (between ~03:00 → 16:00, parallel session) **Skill
  #1 `music-video` — 18 commits of fast-loop work landed on main**
  (range `0c01fcc` → `8cc49cb`, summarized in
  `docs/daily/2026-05-21-morning-brief.md`).  Genre-aware shader
  presets (declarative preset table for 14 genres), 6 new shader
  effects, stillzoom mode for ambient/classical, audio-reactive
  saturation grading, 5 beat-synced "popping" shaders, citypop
  preset + designer lyrics overlay, Pollinations.ai free AI image
  generator (--ai-still flag), CPU-throttled ffmpeg wrapper (80%
  cap), and the v2 batch publish-metadata for 5/27-29.  Engineering
  case study #7 (declarative preset routing as additive scaffold)
  + genre-aware smoke test (16/16 PASS).  This block is operator's
  music-video work, not the job-hunt thread — captured here for
  audit-trail continuity.

- **2026-05-21** (~01:15 KST, autonomous overnight) **Skill #1
  `music-video` — genre-aware shader presets land (additive
  scaffold)** (commit `93cc5e8`).  Operator flagged 2026-05-20 ~23:30
  KST that the pipeline's drum-onset zoom-pulse + 12-beat cuts read
  as "띠용" / out-of-place on songs whose genre forbids glitch or
  forbids cuts (the 5/20 ToddStudio batch's Linen/ambient and Rain/
  lo-fi were the worst cases).  Root-cause traced + fixed.  Ships:
  (a) `skills/music-video/data/genre-presets.yaml` — 14-genre
  declarative preset table; (b) 6 new shader effects added to
  `scripts/music-video-shaders.sh` (scanline, chromatic_split,
  neon_edge, vhs, saturation_pulse, kaleidoscope — all smoke-tested);
  (c) `scripts/music-video-stillzoom.sh` — image+music→60s slow
  Ken-Burns for ambient/classical/dreamcore genres where ANY cut
  violates the contract; (d) `scripts/music-video-genre.sh` — wrapper
  that resolves genre → preset → env overrides + post-shader chain
  + stillzoom routing; (e) 3.4K-word formats-landscape research +
  per-short mismatch diagnosis under `docs/research/`; (f) 8 demo
  mp4s staged at `outputs/demos/2026-05-21-genre-shader-experiments/`
  for morning side-by-side review.  Back-compat: existing v6
  pipeline + run.sh entry points unchanged.  Operator decisions
  still open: which preset(s) to default, retroactive regen for
  5/20 batch, genre-detect helper.

- **2026-05-21** (~02:00 KST, autonomous overnight) **Skill #2
  `job-hunt` — 5 live-ready plugins land (no API key required)**
  (commits `b3789ba` survey, `58a2b58` first 3 plugins + deprecate
  jobkorea/programmers, `a6c39c4` HN + worknet).  Operator request
  2026-05-20 ~23:50 KST: "공고올라는오는 모든곳을 다찾아보고
  가져올수있는지 없는지 확인해봐 낼 아침10시까지 쉬지않고 해".
  Audit at `docs/research/job-sources-survey-2026-05-21.md` walked
  30+ candidate boards through robots.txt + ToS + endpoint probe;
  ranked Tier 1-5 by legal posture.  Five live-ready plugins
  shipped: `global-ats` (Greenhouse + Ashby + Lever public boards —
  27 boards spanning Anthropic / OpenAI / Cursor / Stripe / Notion
  / Datadog / etc), `global-remoteok` (`remoteok.com/api`),
  `global-remotive` (`remotive.com/api/remote-jobs`),
  `global-hn-whoshiring` (HN monthly "Who is hiring?" thread via
  Algolia HN Search), and `kr-worknet` (정부 공공고용서비스).
  End-to-end live test pulled 5,474 raw postings → 200 matched
  the Problem-Solver 24-synonym filter.  Permanent-mock conversion
  for `kr-jobkorea` (robots.txt forbids /Search/?stext= + 2017
  잡코리아 vs 사람인 precedent) and `kr-programmers` (service
  permanently closed 2025-05-19).  Orchestrator argv-limit fix
  (--slurpfile pattern) lets 5MB ATS payloads flow through; schema
  pattern broadens to accept `global-*` source identifiers.
  Tests 68/68 PASS.  README EN+KO cadence batch with the live
  invocation example.

- **2026-05-20** (v0.4.0 tag at `96d1270`, ~15:00 KST) **Skill #2
  `job-hunt` v0.4.0 shipped** — v2 short-keyword UX (`--seed
  "Problem Solver"` → role-synonyms.yaml family expansion, 5
  families / 50+ synonyms); 5 source plugins (`_mock` + `kr-wanted`
  + `kr-programmers` + `kr-jobkorea` + `kr-saramin`) with
  mock-fallback default + `JH_<SOURCE>_LIVE=1` live-HTTP gate;
  5 utility scaffolds (`fit-score`, `cover-letter-draft`,
  `company-research`, `interview-prep`, `derive-profile`) all gated
  on `JH_*_LIVE=1` per [[scaffold-pattern]] — preview JSON emitted
  on stdout under scaffold mode, exit 10.  63/63 + 11/11 tests PASS;
  fresh-clone regression PASS (`docs/onboarding/demo-mode-log.txt`
  row 4).  README cadence batch + site refresh shipped under the
  same tag (`4f43e63`, `96d1270`).  Operator activation tracked in
  "Now" above.

- **2026-05-20** (~00:30 KST) **filter-repo backup branch deleted**
  (Roadmap Next #2).  `main-backup-pre-filter-20260517-173615`
  removed from both local and `origin` — 3 days since the
  2026-05-17 email-history rewrite with no issues observed,
  eligibility threshold (2026-05-18+) cleared.  Tip commit was
  `222684c`.  No corresponding repo commit; the action is the
  delete itself.

- **2026-05-19** (~17:51 KST) **v0.3.0 milestone shipped — Permission
  bootstrap + pluggable B-roll merged to main, tag pushed**
  (commits `c496a0a` user-level Claude Code permission bootstrap;
  `7897e96` jq string-interpolation + non-empty validation fix;
  `2bd0828` broaden allow list with common shell utilities;
  `7ee3670` edge-case smoke for install-claude-permissions.sh;
  `fdcedbc` MUSIC_VIDEO_BROLL_DIR + AI-anime B-roll generator).
  Two feat branches in this milestone: `feat/permission-bootstrap`
  (6 commits) + `feat/custom-broll-dir` (1 commit).  Driven by the
  2026-05-19 in-person friend-test that surfaced ~30 permission prompts
  in one session.  Companion docs landed alongside: `3bcfd6d`
  claude-permissions onboarding; `24392e7` case-studies #6 field-obs
  addendum; `3bec8e9` CRITICAL candidate goal filed in `docs/goal.md`;
  `9b827b0` friend-meeting friction capture; `9f19708` v0.2.0 Done tick.

- **2026-05-19** (~00:16 KST) **Multi-skill AI assistant framework
  promoted to active goal** (`8b39cac`).  Operator direction at
  2026-05-18 ~19:50 KST: skill structure first so subsequent skills
  can iteratively improve the prior ones.  Parks main-protection v2
  parallel work as gated on the framework goal.

- **2026-05-19** (~12:35 KST) **v0.2.0 milestone shipped — Skills
  framework + zero-friction demo path merged to main, tag pushed**
  (commits `ae07233` Skill #1 12-commit train merged FF to main;
  `febb1f3` feat/demo-mode rebased + merged FF; tag `v0.2.0`
  pushed; `8c3e045` README EN+KO cadence batch with demo
  Quick Start + skills/ layer + 6 case studies).
  Fresh-clone test against PUBLIC GitHub URL PASS at 12:35:46 KST
  (81MB / 60s / 3 CC-BY credit lines) — the "friend with a
  laptop at 2pm KST clones the repo and gets a working demo"
  scenario is empirically validated.  Both merged feat branches
  deleted from origin per §6 step 5.

- **2026-05-19** (~02:00 KST, overnight autonomous) **Roadmap Next
  #1 (Zero-friction onboarding path) shipped on feat/demo-mode**
  (9 commits, since merged in v0.2.0).  Five pieces:
  - `scripts/fetch-demo-broll.sh` — CC-BY-3.0 Blender CDN clip
    cache (`e3dd657`).
  - `scripts/fetch-demo-music.sh` + CC-BY-4.0 publish_rule +
    incompetech.com allowlist (`4aace92`).
  - `MUSIC_VIDEO_DEMO_MODE=1` wiring in the music-video mission
    (`15a897b`).
  - `scripts/bootstrap.sh` UX rewrite — demo path recommended
    when no keys/music present (`a77af98`).
  - `scripts/test-demo-mode.sh` fresh-clone reproducibility
    gate + `docs/onboarding/demo-mode.md` + first PASS log
    (`d2e145a`).
  Plus docs polish: session report + EN/KO case study #6 +
  preview thumbnail.  9 commits total.

- **2026-05-19** (~01:30 KST, overnight autonomous) **Skill #1
  shipped + portability foundation laid** (branch
  `feat/skill-music-video`, since merged in v0.2.0).
  Five sub-deliverables:
  - **5 portability principles codified** in
    [`docs/operator-contract.md`](operator-contract.md) §8
    (Standards-compliant / Tracked-by-default / Machine-resilient
    / Multi-machine portable / No-PII).  Commit `5b1aafb`.
  - **Skill #1 — music-video** at `skills/music-video/SKILL.md`
    (top-level, tracked, agentskills.io-spec-compliant).
    `scripts/run.sh` symlinks to `agents/missions/music-video/run.sh`
    so v5+v6 tuning is inherited.  Commit `a993753`.
  - **Settings.json portability (Layer 5)** — `config/claude-settings.template.json`
    + `scripts/install-claude-local.sh` + bootstrap.sh integration.
    `.claude/settings.json` now rendered per-machine; gitignored.
    Resolves the [medium] audit finding present since 2026-05-18.
    Commits `912d61c`, `40aeab1`.
  - **Fresh-clone portability test PASS**: cloned to
    `/var/folders/.../portability-test/MelonS-Agents`, ran
    install-claude-local, verified `.claude/settings.json`
    rendered with the temp dir's paths (not operator's machine),
    `.claude/skills` symlink resolved to the music-video skill.
    Validates the multi-machine principle empirically.
  - **External insight parked** (anonymized community feedback):
    A/B test idea for planner + resourcer = Opus vs Sonnet
    captured in [`docs/ideas.md`](ideas.md) Agents section,
    priority M, suggested test design + cost.  Commit `c9ecb15`.

- **2026-05-18** (~22:30 KST) **GitHub Actions main-protection
  workflow** (commit `a537018`).  Solo-dev safety net for the §6
  branch strategy: 6 static checks (bash syntax, secret scan,
  required files present, `.env.example` sanity, README link
  hygiene, gitignore pattern coverage) triggered on push to `main`
  and `feat/**`.  First green run verified on `main` HEAD.  Pairs
  with `22a45ea` (pre-merge-check) as the automated half of the
  4-gate process.

- **2026-05-18** (~22:00 KST) **Pre-merge gate + automated check
  script** (commit `22a45ea`).  `scripts/pre-merge-check.sh`
  exercises gates 1 (audit CLEAN) + 3 (§5 marker compliance)
  automatically; gates 2 (functional test) + 4 (operator OK)
  remain manual.  Per §6, every structural feat branch runs the
  gate before FF merge to main.

- **2026-05-18** (~21:00 KST) **Branch strategy codified in
  operator-contract §6** (commit `a2a3807`).  Option B locked
  in: `main` always-runnable trunk + `feat/<name>` for structural
  changes + `v0.x.0` tags for stability checkpoints.  Strategy
  is now automatic — operator does not need to invoke it
  per-task; structural-change triggers list defines when feat
  branching applies.

- **2026-05-18** (~20:00 KST) **Music-video-first bootstrap +
  README rewrite** (commit `aa10ba0`).  `bootstrap.sh` rewritten
  to lead with the music-video mission as the first-touch
  experience (replacing the highlight mission in that slot).
  README EN + KO refreshed to match.  375 lines changed across
  both languages.

- **2026-05-18** (~14:55 KST) **Close two eighteenth-audit lows —
  `.claude/*.lock` gitignore + d40abd3 Done entry** (commit
  `e1fda78`).  Eighteenth audit (`docs/audit/2026-05-18-all.md`)
  confirmed the three mediums + earlier lows resolved by `d40abd3`,
  and flagged three new [low] findings: stale `CURRENT-ALERT.md`
  (will auto-clear on next CLEAN run), missing roadmap Done entry
  for `d40abd3`, and `.claude/scheduled_tasks.lock` (a Claude Code
  scheduled-wakeup runtime artifact) not covered by `.gitignore`.
  Added `.claude/*.lock` to gitignore and appended the d40abd3 Done
  entry.

- **2026-05-18** (~14:34 KST) **Resolve two remaining audit lows —
  §8 outputs deviation marker + stale goal reference** (commit
  `d40abd3`).  Seventeenth audit cleared the three mediums + §8
  shaders fallback from `39c5db3`, leaving two [low] findings:
  `docs/for-analysts.md:78` still said "the current active goal's
  deliverables" after the goal was cleared, and `outputs/publish/.gitkeep`
  lacked a §8 deviation marker.  Rephrased the for-analysts line to
  point at the 2026-05-16 niche-selection Past goal, added
  `<!-- §8 operator-directed deviation -->` to the .gitkeep with a
  matching row in `docs/architecture.md` Layers table.

- **2026-05-18** (~14:15 KST) **Clear audit DRIFT_DETECTED — sync
  faceless-short Tier-routing docs + migrate achieved goal**
  (commit `39c5db3`).  Sixteenth audit (`docs/audit/2026-05-18-all.md`)
  flagged three medium findings: (1) `docs/architecture.md` +
  `docs/for-analysts.md` presented Sonnet as the primary script-
  generation path for `faceless-short`, but the code defaults to
  ollama and the Sonnet route is opt-in via `FACELESS_SCRIPT_OVERRIDE`
  pointing at a `gen-script-claude.sh`-pre-generated file (cost-model.md
  already had this right); (2) roadmap "Now" still referenced the
  completed music-video goal's steps; (3) `CURRENT-ALERT.md`
  hadn't auto-cleared after `ab6555e`'s §8 plist fix landed.  Plus
  two low: 2026-05-17 ACHIEVED goal hadn't migrated to Past goals,
  and `scripts/music-video-shaders.sh` had an undocumented §8
  ffmpeg-fallback pattern.  All resolved in this commit: docs
  rewritten to match code, roadmap "Now" cleared with operator-action
  note for 24h metrics capture, goal migrated to Past goals (plus
  cleaned the orphaned 2026-05-16 entry that had been sitting under
  Active as a "Prior goal" subsection), §8 exception comment added
  to `music-video-shaders.sh`.  Re-running `audit-run.sh all` to
  confirm CLEAN.

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
