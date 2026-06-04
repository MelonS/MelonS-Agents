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
(v0.4.0 was complete but the block still said "awaiting FF-merge").
**As of 2026-05-22 ~15:00 KST**: this "Now" subgoal remains the active
operator-side step (no agent work queued); 40+ commits since the
update have all been music-video quality-bar, lyrics, audit, and
README/site refresh work that does not change the active subgoal.
The auditor flagged this as "4th consecutive stale cycle" — the
stale is structural (the subgoal is operator-only, agent can't tick
it) rather than neglect.  Music-video follow-on options surfaced in
the `<!-- suggest -->` block below remain available for the agent
to pick up between operator-driven priorities._

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

<!-- suggest 2026-05-25 22:58 KST
Production line (music-video skill) parallel work this session:

- Batch-3 (10 vocal shorts) shipped to outputs/publish/shorts-2026-05-23-batch/.
  7 of 10 uploaded to YT (scheduled through 2026-06-01). 6 of 13
  posted to TikTok with mid-tier organic reach (top: 어디쯤이야 v1
  721 view + 48 like + 6.7% engagement at 72h+).
- One longform mix experiment shipped (yt-mix-1, id 9SqgNBKk5JE,
  44min instrumental). Visual quality flagged poor → root cause
  triple (bitrate 3.8 Mbps under YT's 8-12 Mbps; static stills +
  zoompan fake motion; no transitions between 12 tracks). Only
  bitrate fixed by GPU; sources + composition need design redo.
- Decision: Migrate active production to Windows + RTX 4070 Ti
  Super. NVENC (7-10x render speed), local SDXL/Flux (Pollinations
  rate-limit bypass), Stable Video Diffusion (real motion vs
  zoompan). Mac becomes secondary monitor / backup.
- Windows session picks up via git pull + memory rsync. Full
  handoff in docs/daily/2026-05-25-windows-pivot.md.
- Pending decisions: (1) TT $20 boost on 어디쯤이야 v1 evaluate
  5/26 ~21:00 KST per thresholds in handoff doc; (2) Windows
  bootstrap docs to be written on Windows directly.

This does NOT change the Active subgoal above (operator-side
job-hunt activation remains primary). The music-video production
line is the agent-side parallel work that has been continuing
between operator-driven priorities, per "follow-on options"
clause in the stale-cycle context above.
-->


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

<!-- suggest 2026-05-22 — all 5 quality-bar phases shipped in the
     2026-05-22 01:30-02:50 KST autonomous block.  Follow-on work:

  - B.1 Stage-2  paper_grain / dust_speck / posterize.  ~2h.
  - B.1 Stage-3  trail_echo / soft_bloom.  ~2-3h.
  - C.1 Phase 2  time-windowed shader gating (per-beat enable= rather
                 than uniform attenuation).  ~3h.
  - A.3 QA gate  enumerate B-roll vs lang_anchor on a render; fail
                 if ≥30% contradict.  Needs concrete render to score.
  - Preset re-map  shift per-genre `shader:` toward Stage-1 shaders
                 per the table in docs/research/2026-05-22-shader-
                 vocabulary.md.  Operator decides per-genre.  ~30min.  -->


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
  **To unblock**: operator confirms a retry has been observed wasting
  compute on a per-output basis (e.g., a 5+ minute re-transcribe when
  only one window failed), then approves agent change to
  `agents/lib/ffmpeg.sh` (single-window re-cut helper + opt-in flag
  `QA_RETRY_GRANULARITY=window` in mission retry loops).  Per §5,
  agent change to `agents/lib/*` requires explicit operator OK.

## Done — most recent first

- **2026-06-05** 🔴 CRITICAL 림 전체 작업마비 fix (운영자 "운반물·저장공간 있는데 떠돈다" 실재현).
  근본: HuntAnimalAction food gate 가 '저장된' 식량(ResourceManager.food+meals)만 검사 → 자원모델
  haul-required 로 저장량 0 이라 gate 영구 true → Hunt 이 매 Decide 선점 → 운반/건축/요리/수확 전부
  굶음.  fix: 물리 MeatPile.Food 합산.  재현+가드 I49.  ISO84/INT49/LongPlay.  ★교훈: 운영자
  실증상을 실빌드로 재현해 고쳐야 함(감사 통과 ≠ 실경험 수정, verify-real-path).

- **2026-06-05** PawnSim 멀티에이전트 버그헌트 4사이클 (mood회귀+신규6차원, 확정12/기각4) — 12건 전부
  수정: mood-델타 회귀 3(clamp 비대칭 영구드리프트·save/load thought 이중가산·트레잇 baseline
  이중가산) + dead trait/장비 효과(Cheerful 이동+20%·Bloodthirsty 전투XP+50%·원거리 무기 게이트)
  + 청사진 컨텍스트취소 환불누수 + builder stand-cell 누수 + 화덕 영구점등 + 요리→건축XP 오귀속
  + 죽은폰 인스펙트 '사망' 표기.  ⚠ #4 builder fix 가 ClearTask(movement.ClearTarget) 를
  targetBp==null(거의 매프레임)에 호출해 전 폰 이동 마비(I2/I4) 자가-회귀 → ReleaseStandCell 로
  교정(verify-real-path: 검증된 확정버그라도 fix 가 회귀 가능).  기각4(신규스폰 이중가산·OnLevelUp·
  skill clamp·부패단위) 정확.  ISO 84/84 · INT 48/48 · LongPlay survived.
- **2026-06-05** PawnSim mood 모델 decay+thought 합산 전환 (운영자 결정, 확정 #10/#11/#12 해결).
  PawnThoughts 가 매초 needs.mood 를 `50+Σthought` 로 절대 override 하던 것을 **델타 가산**으로
  변경 — thought 추가/만료 시 그 음·양수만큼만 mood 변동, 사이의 자연 decay·식사 즉시보너스·
  save 복원 mood 는 누적 유지.  → mood = baseline-decay + Σthought 합산으로 굶주림·부상·동료사망
  누적 시 실제 20 밑 도달 → 정신붕괴 발동(이전 하한 ~38 로 불가) + 식사 즉시보너스 보존 + save
  복원 mood 유지.  회귀가드 V84(고mood 폰 -15 → 델타65, override35 구분).  ISO 84/84 · INT 48/48 ·
  LongPlay survived.
- **2026-06-04** PawnSim 멀티에이전트 버그헌트 3사이클 (회귀+신규 7차원, 확정13/기각7) — 수정 10건:
  자원회귀 2(hauler 청사진 초과분 영구손실, 길들이기 raw AddFood) · 레이드 스케줄 save/load(I48) ·
  마퀴 다중선택 stuck 해제 · 카메라 follow clamp · 마퀴 이동 잔여 task 정리 · 드래그-zone 오디오
  buzz/마커flood(Stockpile/Grow 배치패턴) · alert pitch sweep · build 맵밖 청사진 차단 · 동료사망
  thought 배선(dead feature).  적대적 검증이 다운폰타격(=cycle2 의도설계)·스톡파일필터(제거된 기능)
  등 7건 정확히 기각.  ISO 83/83 · INT 48/48 · LongPlay survived.  보류: mood-모델 3건(#10/#11/#12
  thought-override vs decay) = 자원모델처럼 설계결정 필요 → 운영자 확인.  minor: Mine 드래그 오디오
  (sparse 라 buzz 경미).
- **2026-06-04** PawnSim 🔴 CRITICAL #2 구조물 재시작 persist+reconstruct (운영자 "지금 바로 구현").
  로드 시 플레이어 건축물(벽/침대/문/화덕/램프/울타리/바리케이드/바닥)·작물·스톡파일이 재시작 후
  전부 소실되던 것 해결.  설계: StructureTag(빌드 Mode 스탬프) + BuildManager.SpawnFinished(빌드
  완료·로드 재구성 단일 경로, DRY) → BlueprintEntity 리팩터 → SaveData.structures(mode,pos) 직렬화
  → OnLoad 가 기존 player-built 파괴 후 SpawnFinished/Crop/Stockpile 재구성 → ApplyLoadedSubStates
  로 작물성장/우선순위 복원.  회귀가드 I47 + I35(build 경로 무손상 확인).  ISO 83/83 · INT 47/47 ·
  LongPlay survived=true.  (잔여: 스톡파일 allowed-kinds 필터 + 재시작 시 마커 스프라이트 — 기능은
  동작, 시각/필터 폴리시는 차후.)
- **2026-06-04** PawnSim 멀티에이전트 버그헌트 2사이클 — 신규 7차원, 확정10/기각12, 수정 8건:
  징집 자동공격 정지·출혈사망 corpse 회색조·부상폰 로드 Hp동기화·Hunter.HasTask·길들인동물
  자동사냥 제외·연구 mul 제곱 fix·징집 자율취침 금지·연구 진행도 save/load(I46).  회귀가드 V83/I46.
  ISO 83/83 · INT 46/46 · LongPlay survived.  🔴 보류 #2: 구조물 재시작 persist+reconstruct
  (대형 save-system 피처 — 운영자 결정/플레이테스트 필요, 진단 경고로그만 우선 추가) · #5 길들이기
  walk-to(feature).  상세 autonomous-decisions.
- **2026-06-04** PawnSim 자원모델 단일화 Stage 2c (완료) — trader 물리화.  판매(give)는
  SpendStockpiledWood/Stone/Food(카운터+물리 동시 소비), 구매(receive)는 trader 위치에 물리
  더미 드롭(카운터 아님 — 림이 운반해야 적립).  V32 테스트 물리 동작으로 갱신.
  ISO 82/82 · INT 45/45 · LongPlay survived=true.  **자원모델 단일화 종료**: build haul-required
  + pickup/build/refund/consume/trade 전부 물리 더미 기준 → '카운터 = Σ InStockpile 더미' 불변식
  성립.  (meals/fineMeals 는 조리식 물리 entity 부재로 추상 카운터 유지 — 별도 피처 시 물리화 가능.)
- **2026-06-04** PawnSim 자원모델 단일화 Stage 2b — 환불·소비 물리화.  (1) 해체 환불(#5 포함
  청사진 취소)을 카운터(+) 대신 물리 더미 드롭으로(RimWorld: 해체/취소 시 자재 바닥에).
  (2) cook 재료·저장고 직접섭취를 ResourceManager.SpendStockpiledFood 로 — 카운터 −amount +
  물리 InStockpile MeatPile decrement 함께(#2: '카운터 0인데 화면엔 식량 더미' 해소).
  ISO 82/82 · INT 45/45 · LongPlay survived=true issues=0.  ※ Stage 2c 잔여: trader 구매/판매
  물리화(spawn 위치 결정 필요), meals/fineMeals 는 물리 entity 없어 추상 유지.
- **2026-06-04** PawnSim 자원모델 단일화 Stage 2a — build haul-required (운영자 선택 "순수
  RimWorld").  TryPlace 의 카운터 즉시결제(#242) 제거 → 청사진은 빈 상태로 놓이고 림이 물리
  목재/석재를 현장으로 운반해야 건설.  #3 이중지불 dupe 근절(카운터로 build 결제 안 함).
  starter wood 50 물리라 haul-funding 정상.  검증: ISO 82/82 · INT 45/45 · I35(청사진→운반→벽
  건설=True) · LongPlay survived=true issues=0 (wood400/stone200 축적).  ※ Stage 2b 잔여
  물리화: trader구매·동물/늑대drop·해체환불·취소환불·eat/cook from stockpile (meals/fineMeals 는
  물리 entity 없어 추상 유지).
- **2026-06-04** PawnSim 자원모델 단일화 Stage 1 — pickup 대칭 차감.  운영자 "다 림월드식:
  물리 더미 단일화" 선택.  hauler 가 InStockpile 더미(목재/식량/석재)를 운반용으로 집을 때
  카운터 −amount (deposit 의 +amount 와 대칭) — 이전엔 차감 없어 카운터 영구 과대(#1).
  불변식 '카운터 = Σ InStockpile 더미'의 한 축 복원.  ISO 82/82 · INT 45/45.
  ※ 완전 단일화는 ~8지점(build결제·eat/cook·취소환불·trader·동물drop·해체환불) 추가 필요 +
  build instant-fund vs haul-required feel 결정 → 운영자 확인 후 진행(Stage 2~).
- **2026-06-04** PawnSim 멀티에이전트 버그헌트 1사이클 — 7차원 병렬감사+적대적검증(확정14/기각20),
  모델-독립 7건 수정: 폭풍지속 회귀(0.7실초→≈60실초), 해체환불 품질정합+복제익스플로잇,
  바리케이드 해체불가(영구봉쇄), 운반사망 자원소실, 다운 행동지속, 출혈사망 시체헛공격,
  의사 영구출혈면역.  회귀가드 V79-82.  ISO 82/82 · INT 45/45.  자원모델(카운터vs물리) 클러스터
  #1/#2/#3/#5 는 "다 림월드식" 단일화 설계결정 필요로 보류(autonomous-decisions 기록).
- **2026-06-04** PawnSim save/load 완성 #3 — 벌목/채광 지정(designation) 복원.  로드 시
  마킹된 나무·광맥 지정이 소실되던 것 수정.  TreeChopDesignation/MineDesignation 에
  GetMarked...Positions 접근자, SaveData.chopMarks/mineMarks(List<Vector2>), Save 직렬화,
  ApplyLoadedSubStates 가 respawn 된 나무/광맥에 **엔티티 위치 매칭+TryMark**로 재마킹
  (물리 OverlapBox 의 respawn-직후 collider 미등록 타이밍 회피 → false-fix 방지).
  회귀가드 I45(저장경로 + 로드 재마킹 실 경로).  ISO 78/78 · INT 45/45.
- **2026-06-04** PawnSim save/load 완성 #2 — 작물 성장도(growth 0..1) 복원.  로드 시 farm
  타일 성장이 0 으로 리셋되던 것 수정.  CropEntity.Growth/SetGrowth 노출, CropSave 추가,
  Save 직렬화 + ApplyLoadedSubStates 위치 매칭 재적용(beds/walls 와 동일 1:1 패턴).
  회귀가드 V78(ApplyLoadedSubStates 실 경로 + clamp).  ISO 78/78 · INT 44/44.
- **2026-06-04** PawnSim save/load 완성 #1 — 부위별 HP/출혈/붕대 직렬화 복원.  로드 시
  부위 HP 가 전부 full 로 리셋돼 부상/출혈/붕대/사망(머리·몸통 HP=0)/다운 상태가 소실되던
  것 수정.  PawnSave 에 partHp/partBleed/partBandaged 추가, SaveLoadManager.Save 채움,
  GameSaveButtons.OnLoad 가 ReRollFromName(트레잇 maxHp 확정) 직후 PawnHealth.RestorePartState
  호출(순서: maxHp 확정→실부상 복원→CheckDeath 사망/다운 재평가).  구 세이브 호환(길이 가드).
  회귀가드 V77(직렬화→복원 충실도 + 사망복원 + 구세이브 가드).  ISO 77/77 · INT 44/44.
- **2026-06-03** PawnSim 자율 세션 — 멀티에이전트 코드베이스 전면 버그 스윕 (11회 감사)
  - 신선-각도 헌트로 저감사 영역의 실버그 발견·수정: 카메라 줌-인식 경계(void 렌더),
    레이드 이벤트 중복발생, 날씨 폭풍 GameClock 전환, ResearchUI null, save/load 1:1 매칭
    (데이터손상), PawnHauler stand-cell 예약 leak(작업정체 유발), 마퀴 inspect 잔존.
  - 적대적 검증이 과확정/거짓/feature/paranoid 다수 기각(verify-real-path).  최종 스윕은
    20건 중 실버그 1건 = 코드베이스 정리 수렴.  매 수정 컴파일+76/76+44/44 게이트.
  - 보류(운영자 인지/플레이테스트): 트레잇 결정성(전원 동일 트레잇), save-load 완성,
    behavior-medium(자동근접·스케줄 하드게이트), 대형 RimWorld 피처, 전투 절대값 rescale —
    docs/autonomous-decisions.md 에 fix 계획과 함께 기록.

- **2026-06-03** PawnSim 4h+ 자율 세션 — RimWorld 정합 + 회귀 수정 (운영자 부재, '묻지말고 일해')
  - 멀티에이전트 6회 감사(RimWorld 정합/회귀헌트 등) → 적대적 검증으로 과확정 걸러내며 적용.
  - 작업종류 분리(건축/채광/운반/의료 별도 work type, e8657b2), hover 작업명(e5d8435),
    근접 데미지 1→5(전투 지루함 #8), 팔=다리 HP 통일.
  - **CRITICAL 회귀 자가발견·수정(f5969ff)**: 단일화 시 ChopTreeAction/MineStoneAction 을
    Decide 리스트에 빠뜨려 '지정해도 아무도 안 벰' → 복구 + 회귀가드(d2cebf6, I43).
  - cook task 수면/붕괴 중 미정리·ClearAllWorkTasks miner/harvester 누락 수정(15daa1c).
  - #34 나무 좌클릭 메뉴 회귀가드(I44, 547f46a) — 좌클릭 메뉴 정상 작동 확인.
  - 검증: 매 변경 컴파일 클린 + isolated 76/76 + integration 44/44; LongPlay 생존
    survived=true·issues=0(물리 식량 경제 하 3림 장기 생존 확인).

- **2026-06-03** PawnSim 림 시스템 RimWorld 정합 대개편 (운영자 실시간 플레이테스트)
  - `dc030f5` 작업배정 지정-구동 단일화: 벌목/채광 자율 AI를 '지정된 것만'으로 게이트 +
    중복 dispatch 폐기 + 우클릭=선택 림 전용.  반복 버그(다른 림 벌목/번갈이/freeze) 공통
    뿌리(3중 중첩) 제거.  I16/I43/V40 갱신.
  - `e7d229f` 통나무더미 sprite 일관화 + info 탭 본문 정렬.
  - `fee2325` 시작 식량 RimWorld식 물리 드롭(추상 식사50 카운터 폐기, '다 림월드식으로').
  - 검증: isolated 76/76 + integration 43/43 PASS, 컴파일·실화면 캡처 확인.

- **2026-06-03** PawnSim 세이브/로드 스킬·징집 직렬화 (`3e25dcf`)
  - 로드 시 스킬 level/xp + drafted 가 default 로 리셋되던 progression 소실 버그 수정
    (PawnSave 확장, OnLoad 순서 복원, 구 세이브 호환 가드).
  - isolated 76/76 + integration 43/43 PASS (I22 round-trip 회귀 없음).
  - 나머지 save 갭(부위 HP/지정/작물) 보류 — 운영자 scope 항목.

- **2026-06-03** PawnSim 3차 멀티에이전트 감사 — 디자인·폴리싱 (`6ca5b45`, `fc6de4f`)
  - 비주얼피드백/월드아트/가독성/UX흐름 4차원 → 24확정/7기각.
  - 적용: 벽 이음새 HP-tint 동기화, 작업탭 대비↑, 스케줄 폰트↑, 자원 천단위 구분,
    마퀴 다중선택 펄스 링(단일/멀티 선택 피드백 일관성, in-progress #50).
  - false-fix 거름: pawn 그림자(다른 스프라이트+베이크 α), crop scale(의도값),
    적 타격 flash(이미 동작) — 적대적 검증 통과해도 코드 재확인 후 기각.
  - isolated 76/76 PASS.  (마퀴 링은 인게임 시각 확인 필요.)

- **2026-06-03** PawnSim 2차 멀티에이전트 감사 (`8d8561e`, `<reservation-clear>`)
  - AI행동/사운드/밸런스/save-load/needs 5차원 감사 → 20확정/24기각(적대적 검증이
    move-speed·food-decay·skill-curve 등 의도적 스케일 정확히 기각).
  - 안전·고가치만 선적용: 청사진 예약 desync(죽은 builder→영구 예약) 해소,
    수면 중 food/mood 무한 무허기 버그 수정, PlayAlert tier 클램프, 로드 시 예약 reset.
  - 보류(운영자 판단 필요): 밸런스 수치(팔 HP 18·공격력 1) + save-load 직렬화 확장
    6건(작물성장/부위HP/지정/drafted/식사·휴식타겟/스킬) — 포맷·밸런스 영향.
  - isolated 76/76 PASS.

- **2026-06-03** PawnSim 멀티에이전트 폴리싱 감사 15건 (`aeb9c2e`)
  - 5차원 read-only 감사 → 적대적 검증(false-positive 7건 기각) → 확정 15건 일괄 적용:
    상호작용 hover 설명 12종(HoverTooltip), sortingOrder 정렬 3건(Bandit/Trader/캐리번들),
    동적 패널 높이 2건(ArchitectMenu/WorkTabUI 클리핑 해소), PawnInfoPanel 탭 상수화,
    dead-feature 정직화 2건(Stoic moodSwingMul 실배선 + Gourmand 주석 정정).
  - isolated 76/76 PASS.

- **2026-06-03** PawnSim 연구 트리 정직화 (truth-in-UI / spec-faithful)
  - `9d02e19` — feat: 연구 better_stove 배선 (해금 시 조리 2배, dead tech 실효화)
  - `def151a` — fix: dead/비바닐라 stone_walls tech 제거 + better_stove 설명 정정
    ('식사 mood +5' 미배선 문구 제거). 남은 tech(simple_bow·better_stove) 둘 다
    실배선 확인. isolated 76/76 PASS.

- **2026-05-22** (~16:04 KST, bulk auto-sync via `scripts/roadmap-done-sync.sh`)
  **1 commits backfilled** from base `f3d7781` to HEAD.
  Per §9 every commit needs a Done entry — this is the catch-up batch
  the auditor would otherwise repeatedly flag.  Entries grouped by
  scope; operator may rewrite into narrative form if a specific
  cluster warrants it.
  - `4f3e717` — docs(roadmap): auto-bulk-reconciliation — 4 commits backfilled (16:03 KST)
- **2026-05-22** (~16:03 KST, bulk auto-sync via `scripts/roadmap-done-sync.sh`)
  **4 commits backfilled** from base `f3d7781` to HEAD.
  Per §9 every commit needs a Done entry — this is the catch-up batch
  the auditor would otherwise repeatedly flag.  Entries grouped by
  scope; operator may rewrite into narrative form if a specific
  cluster warrants it.
  - `6a2c8db` — feat(roadmap): auto-bulk-reconciliation script + test + backfill 36 commits
  - `43cfc44` — docs(examples+skills): surface recent ships in EXAMPLES + job-hunt walkthrough
  - `8730cf0` — docs(research): grade_profile visual comparison — 3 of 5 strong, 2 subtle
  - `54ffc0b` — chore(audit+docs): clear 45th-cycle pending + register shot-plan + commit pre-existing artifacts
- **2026-05-22** (~16:00 KST, bulk auto-sync via `scripts/roadmap-done-sync.sh`)
  **36 commits backfilled** from base `f3d7781` to HEAD.
  Per §9 every commit needs a Done entry — this is the catch-up batch
  the auditor would otherwise repeatedly flag.  Entries grouped by
  scope; operator may rewrite into narrative form if a specific
  cluster warrants it.
  - `0703be8` — docs(roadmap): Done entries for music-video quality bar 5 phases + YT stats
  - `87920d6` — docs(roadmap): apply missed Done entry for 39th-cycle audit arc
  - `1a513a6` — docs(daily): morning brief — quality-bar 5 phases + YT stats Phase 1
  - `5c7c9a0` — docs(case-studies): add #9 — quality-bar as 6 unenforced contracts
  - `a701e41` — docs(daily): note A.2 marker-leak fix in morning brief
  - `bde9453` — docs(daily): note C.1 Phase 2 + A.3 tune in morning brief
  - `9ac5d22` — docs(roadmap): Done entries for quality-bar refinements + QA gate
  - `b523c00` — docs(research): qb demo #2 verification — phrase_climax + anchor 33% confirmed
  - `94190c2` — tune(broll): tighten vocal-genre keyword_pools with geographic anchors
  - `bc46a7f` — fix(lyrics): escape commas + equals signs in drawtext text field
  - `b5e61de` — fix(lyrics): substitute apostrophe with U+2019 to dodge filter parser
  - `63431b4` — tune(qa): expand qa-anchor regex to match tightened keyword_pools
  - `2ca305c` — docs(contract): session-start protocol — add autonomous-decisions + morning-brief
  - `01d9d83` — docs(research): demo #3 EN — final fix verification PASS
  - `2c38938` — docs(research): demo #4 KR — 100% anchor coverage after pool tightening
  - `90ddeb1` — docs(daily): morning brief — anchor-coverage progression table
  - `88b4ac4` — feat(shaders): C.1 Phase 3 — per-event shader gating (onsets + beats)
  - `5fe3e64` — feat(presets): shader_pool field — deterministic rotation per short_id
  - `aedc774` — feat(lyrics): Suno-drift detection gate — skip overlay when alignment fails
  - `fd22a4e` — test(qa-anchor): regression test for A.3 QA gate (6/6 pass)
  - `8cd5b8e` — docs(roadmap): bulk-reconciliation #2 — backfill 28 Done entries
  - `482500f` — test(install-claude-local): idempotency regression test + leak fix
  - `33e7f81` — docs(decisions): log idempotency-test-first memory entry
  - `ce7dd4d` — docs(readme): link music-video CHANGELOG + correct 271 seeded ids
  - `e33c488` — test(log-decision): idempotency + correctness regression test
  - `dec4557` — test(log-decision): idempotency + correctness regression test
  - `dbe2a01` — fix(lyrics): move overlay into cross-platform safe band
  - `16332d1` — feat(presets): cut_density semantic field on every preset
  - `e8b4162` — chore(audit): clear 43rd-cycle new findings — bad hash + 8 undocumented scripts
  - `90f9abb` — feat(lyrics): LYRICS_POSITIONS env override implementation
  - `9c4a081` — feat(plan): scripts/shot-plan.sh — director-discipline intent layer
  - `bec0f3b` — chore(audit): clear 44th-cycle new finding — lyric-extract §8 exception
  - `1fef3f0` — feat(grade): per-genre base color grade — research §2 implementation
  - `b2476b4` — feat(morning-brief): bilingual (EN+KO) — --lang ko|en flag + LANG autodetect
  - `f96549b` — feat(doctor): intervention-trend check — chart becomes a doctor signal
  - `26db705` — feat(metrics): quality-trend chart — mission-outcome companion to autonomy signal
- **2026-05-22 (bulk reconciliation #2)** Backlog of ~28 commits between
  `f3d7781` (2026-05-22 ~02:45 KST, already covered above) and `abb45d0`
  (2026-05-22 ~15:30 KST) appended below in compressed form, grouped by
  theme.  Per operator-contract §9, every commit deserves a Done entry;
  bulk-reconciliation trades narrative detail for completeness so the
  Done section catches up.

  **Music-video operator-utility cluster (15 commits, 5/22 morning →
  afternoon)**: `5c942be` first-touch zero-account demo wizard;
  `682b9cc` first-touch smoke test 3/3; `72e865c` music-video-batch
  multi-track render wrapper; `64e71c7` music-video-batch smoke 4/4;
  `54629c8` music-video-validate combined pre-publish gate; `a940d4d`
  validate regression 4/4; `f9f8f24` optional MUSIC_VIDEO_VALIDATE=1
  post-render gate; `e032cf5` music-video-thumbnail upload-ready still
  extract; `3fa5b8a` thumbnail 5/5 cases; `a2bdf0c` auto-extract
  thumbnail in pipeline; `c5fceaf` lyric-extract whisper-based pull;
  `2c950e0` lyric-extract ♪ marker strip; `28ef914` lyric-extract
  smoke 4/4; `a0ea9b9` music-video-trim utility + test-all aggregator;
  `0adb09c` test-all self-exclusion fix; `af1201c` music-video-upload-meta
  per-mission template; `652c7a8` auto chain upload-meta 4/4 PASS.

  **Music-video pipeline / shader refinement (5 commits)**: `26211a2`
  lyrics auto-wrap long lines; `90e6ef8` MUSIC_VIDEO_SHADER env override
  for one-off tests; `07428b1` shaders C.1 Phase 3 cap event count at 30
  (ffmpeg expr-length limit); `3b43043` shaders C.1 Phase 3 fallback for
  sparse onsets; `e1837b8` shaders C.1 gate-mode regression 6/6.

  **Documentation / metrics (5 commits)**: `5d7fbb3` operator-utilities
  table in pipeline-reference doc; `5fd83c7` consolidated music-video-
  pipeline-reference.md (env vars, flags, gates); `d899cfc` README EN+KO
  cadence — surface first-touch wizard + pipeline reference link;
  `0a4d622` daily brief — C.1 Phase 3 + drift gate + wrap + first-touch;
  `46a8fe7` bilingual intervention chart (EN+KO) + visual polish;
  `5b797e9` skills/music-video CHANGELOG capturing the 2026-05-22
  quality-bar batch.

  **Operator tooling / health (2 commits)**: `75f5dd7` music-video-doctor.sh
  skill-specific health check; `7f44c59` install-claude-local idempotency
  (single-line BEGIN/END + substitute-before-awk).

  **Audit clearing (3 commits)**: `ee3853b` clear stale machine-path
  in demo-mode-log line 1 (40th-cycle finding #5); `10ec7c0` clear
  .playwright-mcp gitignore + commit 2026-05-22-all report (40th-cycle
  findings #4 + #7); `e09a4ac` fresh-clone regression PASS row appended.

  Today's micro-batches (commits added to roadmap in dedicated entries
  rather than this reconciliation): `3ab852d` (README+site refresh),
  `b6c8b6c` / `bbb6faf` (Done entries for own commits), `3352d3e`
  (site polish v2 + job-hunt digest preview + faceless collapse),
  `92113ee` (commit pre-existing audit+onboarding files), `abb45d0`
  (clear 42nd-cycle persistent audit findings).

- **2026-05-22** (~14:50 KST, site polish v2 + disk cleanup pass)
  **Site/Skill #2 surface + faceless gallery collapse + 2 GB of
  records/ intermediates freed** (commit `3352d3e` + non-committed
  cleanup sweep).  Operator note "머신 용량이 별로 없는거 같아"
  triggered a parallel cleanup: 28 `records/missions/*/*/resources/`
  dirs (B-roll downloads / concat-noaudio / narration.wav — all
  regeneratable) swept; 103 deliverable mp4s untouched; disk free
  went 13Gi → 15Gi.  Site changes: Skill #2 gained a concrete code-
  block preview rendering of `docs/samples/job-hunt-digest-mock.md`
  (seed → role family → matched postings) since the prior site had
  no Skill #2 artifact; faceless-short 4-frame gallery moved into a
  `<details>` collapsed block so it stops dominating the visual
  real estate the music-video mission now owns; `site/style.css`
  gained `<details>` styling consistent with `.card` / `.callout`.
  Faceless-era scorecard kept inline (still the structured
  retention-mapping signal the format pivot was decided against).

- **2026-05-22** (~11:10 KST, README + live site refresh batch)
  **README EN + KO + site/index.html refreshed — stats / case-study
  count / "What's shipped on top of v5" / first-touch-leads-the-fold**
  (commit `3ab852d`).  readme-cadence triggers: goal-aligned UX
  overhaul (lead with first-touch wizard per CRITICAL candidate goal
  in goal.md) + content drift on four undocumented ships
  (`first-touch.sh`, `music-video-batch.sh`, `music-video-validate.sh`,
  `music-video-thumbnail.sh`, `lyric-extract.sh`,
  `music-video-pipeline-reference.md`).  Stats updated 69+→91+
  mission outputs + new "23 ffmpeg shaders" stat; case-study count
  6/8→9 across both READMEs + site; OG meta regenerated to advertise
  both skills not just music-video; new "Try it in ~60 seconds"
  callout above-the-fold in README EN+KO; new "Quality bar — 5
  contracts" card on the site listing A.1/A.2/A.3/B.1/C.1.  EN+KO+site
  single commit per readme-cadence rule.

- **2026-05-22** (~05:15 KST, autonomous intervention-reduction
  continued) **Phase 7+ — audit-hook coalescing + auto-decision
  logging + morning brief + chart auto-mirror + portability fix**
  (commits `ae73973`, `2c9d072`, `e415875`, `bd118ac`, `4cd3aaa`,
  `1747d70`, `ff7fe5d`, `f5d909a`).  Closes intervention-reduction
  Phase 7 work: (a) L1 audit hook coalescing via sentinel file —
  saves Max-plan tokens during commit bursts (was 6+ concurrent
  claude CLI processes; now 1 with deferred re-fire log entry);
  (b) audit verdict transitions auto-log to autonomous-decisions
  via the post-commit trampoline (DRIFT↔CLEAN visible without alert
  diffing); (c) `scripts/morning-brief.sh` — single-command overnight
  digest combining doctor + audit + intervention 7-day trend +
  commit attribution + decisions + review-queue + blockers;
  (d) `intervention.json` gains `trend_7d` annotations (delta from
  prior 7-day window, populates fully on 2026-05-29);
  (e) chart regen auto-mirrors to `site/assets/intervention.png` so
  Pages site stays in sync without manual copy; (f) README EN+KO +
  site Operator tooling card both surface morning-brief.sh;
  (g) `generate-intervention-chart.py` `-Users-melons-ai` literal
  replaced with `str(ROOT).replace("/", "-")` derivation — script
  now portable to any clone path.

- **2026-05-22** (~03:15 KST, autonomous block continued) **Music-
  video quality bar — refinements + QA gate + Phase 2 follow-ons**
  (commits `9057d77` A.3 injection 25→33%, `77535c3` C.1 Phase 2
  phrase_climax gate mode, `7e52ab8` A.3 QA gate scoring, `a2e2d3f`
  A.2 LRC marker leak fix, `ea6d5d0` qb-demo verification doc,
  `7d8e9b1` shader Stage-2 + Stage-3 = catalog now 23).
  - A.3 anchor injection rate bumped to 33% (every 3rd seg) after
    demo-frame sampling showed only 25% coverage.
  - C.1 Phase 2 `phrase_climax` gate mode: shader fires only in the
    center `RATIO × duration` window with trapezoid fade.  Activate
    via `MUSIC_VIDEO_SHADER_GATE=phrase_climax`.
  - A.3 QA gate `scripts/music-video-qa-anchor.sh`: scores B-roll
    keywords against the genre's lang_anchor, emits JSON verdict +
    exit code (0 PASS / 1 WARN / 2 FAIL).  First test on demo:
    3/8 = 0.38 → PASS.
  - A.2 LRC marker leak fix `a2e2d3f`: autofill comment moved from
    inline text suffix to a separate `# line N` comment line.
  - 8 additional shaders shipped across Stage-2 (paper_grain,
    dust_speck, posterize) and Stage-3 (trail_echo, soft_bloom);
    catalog now 23.

- **2026-05-22** (~04:00 KST) **38th + 39th cycle audit clearing arc
  + site refresh + daily report + §8 registry structural fix**
  (commits `cce3f40`, `e8c5e47`, `3b8ac7d`, `fef5752`, `bf3cb36`,
  `be3ca98`, `f49d4b0`, `b7f2209`).  Closes the audit arc:
  36-37th cycle's 10 findings cleared by `e8c5e47`; 38th cycle's
  5 new findings cleared by `3b8ac7d` + `fef5752`; 39th cycle's
  cosmetic §8 coordinate staleness fixed structurally — registry
  rewritten to drop line numbers entirely.  Entries now name file
  + pattern, auditor verifies via `grep -n "§8 exception" <file>`.
  This prevents the coordinate-staleness from recurring whenever
  parallel sessions insert lines before a §8 comment.  Also: 3
  undocumented scripts (`music-video-genre-detect.sh`,
  `music-video-fetch-still.sh`, `log-decision.sh`) added to
  `docs/for-analysts.md` inventory.  Site refresh per
  [[readme-cadence]] "contract/architecture change" trigger —
  case studies 6→8, operator tooling card expanded with
  statusline + log-decision + review-queue, intervention chart
  refreshed to 2-panel.  Daily report at
  `docs/daily/2026-05-22-overnight-intervention.md`.

- **2026-05-22** (~02:50 KST, autonomous music-video block)
  **Music-video quality bar — 5 phases shipped** (commits `05e6c2a`
  A.1 B-roll dedup, `fa1ec72` A.2 lyric sync, `cce3f40` A.3 lang
  anchor, `52b6eb4` C.1 shader ratio, `1c87377` B.1 Stage-1 shader
  expand).  Operator-stated six 2026-05-22 quality directives
  decomposed in `docs/research/2026-05-22-music-video-quality-bar.md`;
  shader research in `docs/research/2026-05-22-shader-vocabulary.md`.
  - **A.1**: `records/youtube/broll-used.txt` (gitignored), 196 ids
    seeded by `scripts/broll-history-backfill.sh`.  Both Pexels
    callers consult + append.  `BROLL_HISTORY=off` per-render escape.
  - **A.2**: `scripts/music-video-lyric-align.sh` derives LRC from
    plain text + audio via whisper (word-level for KR, segment-level
    for EN).  Confidence reported; sub-floor lines marked autofilled.
    Integrated via `--align-to-audio` + `--with-lyrics` flags.
  - **A.3**: `lang_anchor: ko|en|mixed|neutral` on every preset.
    Person-anchored keywords injected at every 4th segment for vocal
    genres.  Pollinations.ai prompt template updated.
  - **C.1**: `shader_active_ratio` per preset (1.0 ambient, 0.35
    kpop_ballad).  Blend-back-to-original via final ffmpeg pass when
    ratio < 1.0.  Time-windowed gating deferred.
  - **B.1**: three new shaders (`light_leak`, `duotone`,
    `vignette_pulse`).  geq trap documented (uppercase T for time,
    pow() not `^`).  Stage-2/3 queued in suggest block above.
  Followups in suggest comment above.

- **2026-05-22** (~01:25 KST) **YT stats Phase 1 + daily scheduler
  + dreampop drafted** (commits `a43057f`, `ec5a5bb`).
  `scripts/yt-stats-collect.sh` (videos.list per uploads playlist,
  channels.list?mine=true auto-discovery, no channel id hardcoded) +
  `yt-stats-diff.sh` (per-video view/like/comment deltas).  Outputs
  to `records/youtube/stats/<date>.{csv,raw.json}`.  Daily 09:00
  launchd job (`com.melons.agents.yt-stats`) installed.  Side
  action: dreampop `KirKdDUWOpc` moved to privacyStatus=private via
  videos.update — the 5/24 21:00 publish that would have shipped a
  known-broken render is now defused; re-render decision deferred.

- **2026-05-22** (~03:35 KST) **Phase 3-6 + case study #8 of overnight
  autonomous run** (commits `8377ac9`, `b2bb0f6`, `e8cb7bf`, `65a917b`,
  `a9bbd31`).  Phase 3: `docs/autonomous-decisions.md` +
  `scripts/log-decision.sh` (lever 9 — one-page wake-up summary).
  Phase 4: statusline gains `goal:N/M` subgoal progress flag from
  `skills/goal-lock/scripts/check-done.sh --json` (lever 10).
  Phase 5: intervention chart manually regenerated mid-session
  (overnight delta captured before next phase).  Phase 6: doctor.sh
  + statusline gain `actionable_warn` classification (excludes opt-in
  env keys + git-tree informational signal); statusline shows
  `doctor:⚠3` instead of `doctor:⚠7` on current operator's machine.
  Case study #8: intervention-as-unmeasured-axis written EN+KO for
  `docs/engineering-case-studies.md` (problem → constraint → decision
  → artifact → result format consistent with #1-7).

- **2026-05-22** (~02:50 KST) **Phase 1+2 of overnight autonomous
  run — review queue + audit drift cleanup**.  Operator directive
  "유저 개입 관련 개선해야 할것들 자율로 내일 오전 11시까지 진행해".
  (a) Lever 3 shipped: `outputs/review-queue/` + 3 scripts
  (`review-queue-add.sh` / `-digest.sh` / `-decide.sh`) + wired
  into `agents/missions/music-video/run.sh` post-render so new
  renders auto-enqueue (soft-fail, idempotent).  Renders no longer
  ping the operator per-mp4 — batched contact-sheet drain on
  operator's own cadence.  Commits `9462552`, `f3d7781`.  (b)
  Audit drift cleanup: 7 §8 comment gaps closed across
  `ffmpeg-throttled.sh`, `music-video-lyrics.sh`,
  `music-video-stillzoom.sh`, `music-video-canvas.sh`,
  `music-video-audio-reactive.sh`, `music-video-typography.sh`,
  `doctor.sh`; §8 exception registry in
  `docs/operator-contract.md` rewritten with correct line numbers
  + 7 new entries; `docs/architecture.md` Layers table updated to
  document `outputs/publish/upload-meta-v2/*.json` v2 exception
  and the new review-queue row; `docs/for-analysts.md:93` date
  refreshed to 2026-05-22; library-audit + fetch-ai-still scripts
  added to for-analysts inventory.  Expected: 37th audit DRIFT_DETECTED
  → CLEAN, statusline `audit⚠` flag clears, doctor:⚠ count drops
  by 4-5.

- **2026-05-22** (~02:00 → 03:00 KST, parallel music-video session)
  **Phase A.2 lyric vocal-onset alignment** (commit `fa1ec72`,
  parallel work).  Companion phase A.1 in `05e6c2a` already in Done.

- **2026-05-22 (bulk reconciliation)** Backlog of ~40 commits between
  `a05c12b` (2026-05-21 ~05:30 KST) and `f3d7781` (2026-05-22 ~02:45
  KST) appended below in compressed form, grouped by theme.  Per
  operator-contract §9, every commit deserves a Done entry; the
  bulk-reconciliation format trades narrative detail for completeness.

  **Skill #2 `job-hunt` cluster (10 commits, 5/21 afternoon → evening)**:
  `5ea09dd` 31st+32nd audit trail; `f4881cf` orchestrator region
  filter (KR↔EN city mapping); `6225afa` digest role_fit + hire_prob
  breakdown; `eaafcdc` worktree-workflow doc; `6eaf9be` company-tier
  table + gitignore guard; `f9426ed` kr-theteams 강소기업 plugin;
  `7b68a83` orchestrator --sort=fit + --top=N; `e289c8a` KR self-
  hosted careers probe research (no scrape); `904e3bb` Forward
  Deployment synonym + Korea region mapping; `29e4363` kr-rallit
  랄릿 KR IT-specialist plugin.

  **Music-video cluster (8 commits, 5/21 evening → 5/22 dawn)**:
  `a35ac4a` morning brief vocal pivot; `82cb15d` 4 vocal-centric
  presets (kpop_ballad / kpop_dance / rnb / uspop) + --full-length;
  `6ddf4a8` 5 per-genre lyrics .txt assets; `d9c7c06` lyrics overlay
  apostrophe escape fix; `4cddc09` library-audit utility; `8370c4b`
  morning brief 29 demos finalized.

  **Repo ops / project meta (10 commits, 5/21 evening → 5/22)**:
  `14bf885` audit-stale note + afternoon-continued; `5200df6` user-
  feedback infra (Discussions templates + CONTRIBUTING) PR #1;
  `c5a3619` legal privacy + ToS for TikTok app review; `40240aa`
  revert privacy/terms after TikTok automation abandoned;
  `7b58656` log TikTok automation deferral decision; `0655866`
  skill-activation manifest + status dashboard; `93a042b` external
  skill libraries ecosystem survey; `9c10dc7` contract split —
  operator-style → ~/.claude/CLAUDE.md; `a122f99` skill-goal-lock
  list unchecked deliverable subgoals; `c04d371` audit skill-
  activation drift check wired into audit-run.sh.

  **Operator tooling + portability (6 commits, 5/22 morning)**:
  `96b3e00` 2026-05-22 context snapshot pre-compression;
  `1863c48` doctor.sh repo-wide runtime health check;
  `400746d` repo-tracked template for ~/.claude/CLAUDE.md operator-
  style block; `0bb1470` README B1 contract split + operator
  tooling; `ad36ff7` site refresh stats + skill counts + operator
  tooling card; `c04d371` 13th audit rule (skill-activation drift).

  **Intervention monitoring (4 commits, 5/22 ~01:30 → ~02:50 KST,
  this autonomous session)**: `d0afd03` two-panel intervention
  tracker + daily launchd regen + README restore; `7594684`
  statusline surfaces doctor health + audit alert (lever 4);
  `9462552` review-queue batch taste-decision infra (lever 3);
  `f3d7781` music-video auto-enqueue renders to review queue.

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
