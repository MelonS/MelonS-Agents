# 엔지니어링 케이스 스터디

**한국어** | [English](engineering-case-studies.md)

각 항목은 화이트보드에서 그린 설계가 아니라 **실제 프로덕션에서
드러난 문제로부터 도출된 결정**입니다. 포맷: **문제 → 제약 → 결정
→ 산출물(파일 + 커밋)**. 파이프라인 흐름 순으로 위에서 아래로
읽거나, 헤딩만 훑어 주제별로 보면 됩니다.

---

## 1. 로컬 LLM의 품질 천장 — Tier-1/Tier-2 라우팅

**문제.** faceless-short 파이프라인 초기 안정 버전은 모든 단계를
로컬 모델로 돌렸습니다. 내레이션 스크립트 생성은 `llama3.2:3b`
(이후 `qwen2.5:7b`). 기계적인 단계는 무난했지만 스크립트 품질이
정체됐습니다 — 백과사전 같은 산문, 약한 5초 후크, 비슷하지만
실제로는 다른 사실을 한 호흡에 섞어서 말함 (예: 수소가 인체에서
차지하는 비율 — 질량 기준과 원자 수 기준이 다른 수치인데 같은
사실인 양 한 문장에 묶어서 말함).

**제약.** "Tier 2 (로컬) = 기본" 룰은 외부 의존성 없이 파이프라인을
재현 가능하게 만들기 위한 것이었습니다. 이 룰을 모든 단계에 일률
적용하면 로컬이 못 따라가는 한 단계에서 품질 천장이 생깁니다. 그렇다고
대량의 토큰을 결제해 푸는 것도 옵션이 아니었습니다 — 고볼륨 단계가
즉시 예산을 다 태웁니다.

**결정.** 단계별로 룰을 분리. **기계적·고볼륨 단계**(트랜스크립트,
렌더, B-roll fetch)는 로컬 — 규모가 커지면 토큰 비용이 지배합니다.
**원샷 창작 단계**(스크립트 후크, 사실 프레이밍)는 Sonnet으로 라우팅
— 호출당 ~500 토큰, Max 구독 쿼터 대비 운영상 무시 가능한 양이고,
이후 60초 시청 품질이 이 한 단계에서 복리로 증가합니다. 이 라우팅
룰은 이제 `docs/cost-model.md`에 명시적이며, "로컬 기본"에서 벗어나는
단계는 이 축에서 명시적 사유가 있어야 합니다.

**산출물.**
- `scripts/gen-script-claude.sh` — Sonnet 전용 스크립트 생성
- `docs/cost-model.md` § "When Tier 2 is the wrong default"
- 커밋 `d205b15` (Sonnet 라우팅 + v6 trial)

**스코어 임팩트.** Hittites EN: v5 → v6에서 같은 B-roll, 같은 TTS,
같은 렌더 환경에서 Hook 축이 3 → 9, Factual 축이 4 → 9로 상승 —
스크립트 단계가 회귀의 원인이었음을 분리해서 입증.

---

## 2. 단순 병렬화는 호스트를 OOM — 세마포어 기반 배치 러너

**문제.** `faceless-short/run.sh`를 순차 실행하면 느립니다 (~5분/렌더).
셸 파이프라인으로 전부 백그라운드에 던지면 호스트가 OOM. 각 미션이
ffmpeg + whisper.cpp + Ollama 피크를 동시에 찍고, 100% 동시 피크에서
추론된 워킹 메모리는 ~16 GB + Metal GPU 압력 — M2 16 GB에서는 OOM 보장.

**제약.** macOS 기본 bash 3.2에는 `wait -n`이 없습니다. 파이썬이나
외부 스케줄러를 도입하기에는 본질적으로 작은 bash 제어 루프인데
런타임 의존성을 추가하게 됩니다.

**결정.** `jobs -r | wc -l`로 폴링하는 작업 개수 세마포어 — bash 3.2
호환, 외부 도구 무필요. 기본 `MAX_PARALLEL=1` (순차 — M2 안전 선택),
`=2`는 다른 GPU 부하 없는 M2 16 GB+에서 OK 표시, `=3+`는 명시적
고위험 경고 출력. 작업별 rc/소요시간/시작/종료를 TSV 요약에 기록해
재시도/트리아지를 스크롤백 대신 grep으로 처리.

**산출물.**
- `scripts/batch-faceless.sh` — 스로틀된 러너
- `records/batch-faceless/summary-<ts>.tsv` — 실행별 요약
- 커밋 `4b5bbee`

**운영 메모.** 첫 music-trial 배치 실행은 1개 mp4 뒤 종료된 일이
있었음 — 원인은 `set -uo pipefail`과 자식 파이프 상호작용이었지,
세마포어가 아니었음. `docs/pilots/music-trial/README.md`에 후속 작업
으로 정리하고, 남은 렌더는 foreground 순차 모드로 워크어라운드.

---

## 3. LLM 드래프트는 여전히 임계치 미만으로 출고될 수 있음 — 콘텐츠 품질 피드백 루프

**문제.** 케이스 #1의 Sonnet 라우팅은 평균을 끌어올렸습니다. 드리프트를
완전히 없앤 건 아닙니다 — 일부 드래프트는 여전히 사실 프레임을 섞거나
약한 동사로 시작했습니다. 파이프라인은 스크립트 생성과 다운스트림
TTS / B-roll / 렌더 사이에 품질 게이트가 없었습니다 — 나쁜 스크립트가
끝까지 흘러가 mp4가 렌더된 *후에야* 스코어카드에서 잡혔습니다.

**제약.** 매 드래프트를 사람이 읽어야 하는 QA 단계는 Sonnet으로 얻은
처리량 이득을 다 까먹습니다. 드래프트를 쓴 모델과 같은 모델로 채점
시키는 QA는 자기채점 리스크가 명백합니다.

**결정.** 별도의 Sonnet 호출로 채점 (다른 프롬프트, 다른 역할 —
"evaluator는 부풀리지 말고 인색하게 채점"), 두 축(hook strength,
factual coherence — v5→v6에서 3→9로 상승한 두 축, 즉 LLM이 컨트롤
하는 두 축)에 대해 strict JSON 출력. 두 축 모두 임계치 이상(기본 7)
이면 채택. 아니면 **축별 피드백을 프롬프트에 prepend해서** 재생성,
최대 N회 재시도(기본 2). 시도마다 스크립트 옆 JSONL 스코어링 로그에
append돼 재현성 보장.

**산출물.**
- `scripts/score-content.sh` — 코드펜스 스트리핑까지 처리하는 strict-JSON 스코러
- `scripts/gen-script-claude.sh` — 축별 피드백을 가진 재시도 루프
- `<out>.scoring.log` — 시도 흔적
- 커밋 `2217bce`

**검증.** AutoTune 스크립트 9/9 (첫 시도 채택). Hydrogen v5
(픽스 이전 회귀 채점)는 2/3에서 잡혔고, reasoning 필드에 운영자가
원래 지적한 "10% vs 60%" 프레임 충돌이 정확히 명시됨 — 스코러가
원래 운영자가 손으로 잡던 실패 모드를 감지할 수 있음.

---

## 4. 며칠 뒤 발견된 드리프트는 비쌈 — 3-레이어 리액티브 감사

**문제.** 야간 감사(03:00 launchd)가 문서 드리프트, 컨트랙트 위반
편집, 스테일된 로드맵 항목을 잡긴 했지만 — 때때로 드리프트가
들어간 지 18–24시간 뒤에 잡았습니다. 그때쯤이면 다운스트림 작업이
이미 드리프트된 상태 위에 쌓여 있어, 수정 비용이 곱해집니다.

**제약.** 장기 실행 옵저버 프로세스는 과합니다 — 하루 대부분은
관찰할 게 없습니다. 30초마다 폴링하는 건 토큰 낭비입니다(그리고
프로젝트에는 머니 파이어월이 있음). 클래식한 옵저버 패턴은
long-lived subject를 가정하는데, 이 레포의 서브에이전트들은
on-demand로 트리거되는 short-lived 프로세스 — 그런 의미의
subject가 아닙니다.

**결정.** "Observer"를 **Reactor + Hook**으로 교체 — 파일을 이벤트로,
3개의 트리거 레이어:

- **L1 — post-commit 훅 (~30초 반응시간).** 커밋이 드리프트 위험
  경로(`agents/`, `.claude/agents/`, `config/`, `CLAUDE.md`, 운영자
  컨트랙트)를 건드렸을 때만 `audit-run.sh contract` 발화. 무관한
  변경에는 아무것도 안 함.
- **L2 — 15분 미션-이상 폴.** 신규 blocker 파일이나 QA-FAIL 버스트를
  체크. 이상 없으면 no-op (제로 토큰). 첫 실행은 발화 없이 상태만
  시드 — 기존 blocker가 false alert을 트리거하지 않게.
- **L3 — 일일 03:00 베이스라인.** launchd가 풀스윕 발화. L1 + L2가
  놓친 것을 잡음 (예: 훅이 설치되기 전에 들어간 드리프트).

세 레이어는 독립적 — 하나가 죽어도 트리거 경로 둘이 살아 있음.

**산출물.**
- `scripts/hooks/post-commit.sh` + `scripts/install-hooks.sh` (L1)
- `scripts/audit-poll.sh` + `scripts/com.melons.agents.audit-poll.plist` (L2)
- `scripts/audit-run.sh` + 기존 launchd 작업 (L3)
- 커밋 `785dafd` (L1), `de71875` (L2)

**검증.** L1은 `7c6ff4f`(운영자 컨트랙트 편집)과 `764f3f0`(`.claude/agents/`
편집)에서 정확히 발화, `ac8c02a`(스코어카드 데이터, 위험 regex에 없음)
에서는 정확히 불발화. L2는 첫 실행에서 기존 3개 blocker를 발화 없이
시드 처리.

---

## 5. ffmpeg 안의 쉐이더 효과 — 벽이 어디인지 아는 것

**문제.** 운영자가 music-video 출력에 쉐이더 스타일 효과를 요청 — 물
표면 ripple, breathing zoom, 따뜻한 halation, 그리고 cel-shading
(카툰).  베이스 파이프라인은 이미 grain + vignette + glitch zoom-pulse
를 ffmpeg 필터로 처리 중. 질문은 ffmpeg-만 경로가 어디까지 확장
가능한가, 어디부터 진짜 셰이더 스택 (GLSL / mpv + libplacebo / GPU
compute) 이 필수가 되는가.

**제약.** 파이프라인은 단일 ffmpeg 패스.  GLSL 툴체인 없음, 두번째
렌더러 없음, AI 스타일라이즈 서비스 없음.  shipping 되는 것은 같은
`ffmpeg` 바이너리가 읽을 수 있는 필터 그래프로 표현 가능해야 함.
운영자 품질 기준은 효과별 이진법 — 결과가 분위기 있고 의도적으로
보이거나, 컷.

**결정.** 세 가지 효과 landing. 하나 deferred.

1. **Pond surface (`displace` + 절차적 `geq` wave 맵).** 첫 시도는
   discrete "drop" — phrase 경계에 시간-게이트 `scale` 표현으로 radial
   bulge 펄스 세 개.  운영자 반응 "먼가 떨어지긴하는데 좁쌀만함" —
   drop 은 보이지만 작은 점 같고 물 같지 않음.  재구성: discrete drop
   을 시뮬레이션하지 말고, 화면 전체를 *연못 표면* 으로 시뮬레이션.
   `displace` 와 두 개의 애니메이션 그레이스케일 맵 (X / Y) 으로 재구현,
   각 맵은 540×960 (풀해상도 대비 4× 빠름) 에서 `geq` 가 3-컴포넌트 sin
   wave 필드로 생성 → 1080×1920 으로 scale up → `displace=edge=smear` 에
   투입.  최대 ±13 px (~1.2 % 너비) — 화면 전체에서 보이지만 거슬리지
   않음.  운영자 확정 "완전 잘되고".

2. **Breathing zoom — libx264 stride-mismatch 버그.** 첫 시도:
   `scale=w='1080*(1+0.015*sin(2*PI*t/5))'` 으로 ±1.5 % 연속 wave.
   3.5초에 `Input picture width (1080) is greater than stride (1072)`
   로 libx264 크래시.  원인: `sin` 이 음수 갈 때 multiplier 가 1.0 미만,
   `scale` 이 crop 타겟 (1080×1920) 보다 *작은* 프레임 생성.  뒤따르는
   `crop=1080:1920` 이 1064-wide 이미지에서 1080 픽셀 읽으려다 코덱
   choke.  픽스는 wave 를 `(0.5 + 0.5*sin(...))` 으로 재구성, multiplier 가
   항상 ≥ 1.0 이 되도록 — 프레임이 crop 대비 항상 upscale 만 됨, 절대
   downscale 안 됨.  교훈: 고정 사이즈 `crop` 앞의 시간변화 `scale` 은
   one-sided 여야 함.

3. **Halation — bright-bloom screen-blend.** 소스 split, 밝기-임계
   + 22 px `gblur` 사본, 원본 위에 screen blend 0.30 opacity.  필터
   그래프 ~60 줄, 표현식 마법 없음.  첫 시도에서 작동 ("확실히 티남").
   구현 디테일 주목할 것: `blend=all_expr='A + (255-A)*B/255 * OPACITY_EXPR'`
   form 이 opacity 를 시간 변화 표현식으로 만들어줌 — 이게 아래 phrase-
   aware combo 의 빌딩 블록.

4. **Cel-shading (카툰) — 어디서 멈춰야 하는지 아는 것.** 이게 케이스
   스터디의 payload.  두 시도 실패, 실패 모드가 교훈적.  첫 시도:
   `bilateral` 필터로 색 평탄화 + `eq` saturation 부스트 + `edgedetect`
   윤곽선 + `multiply` blend.  결과는 "사람이 반만 심슨 된거 같은데" —
   shading variation 이 여전히 살아있어서 진짜 카툰 룩 아님.  근본
   원인: posterize 단계 없음.  두번째 시도는 `lutyuv` 로 luma / chroma
   독립 양자화 (luma 는 `round(val/51)*51`, U/V 는 `round(val/64)*64`)
   추가.  결과는 "완전 그냥 초록색만 나옴" — 모든 게 초록으로 바뀜.
   근본 원인: luma 와 chroma 를 독립된 양자화 그리드로 양자화 시 U/V
   분포가 작은 수의 stepped pair 로 collapse, 소스 hue 분포를
   충실히 인코딩하지 못함 — 대부분 픽셀이 같은 (U, V) pair 에 landing,
   YUV 공간에서 이는 단일 hue 에 해당.  진짜 cel-shading 은 RGB 공간
   posterize (또는 HSV 공간에서 luma 는 부드러운 신호로 보존) + 두꺼운
   anti-aliased 윤곽선 필요 — 둘 다 1080p 실시간에서 `lutyuv` 또는
   `geq` 안에 깔끔하게 표현 불가.

   결정은 **세번째 시도를 더 많은 knob 으로 ship 하지 않는 것**.  실패
   모드의 정직한 해석은 ffmpeg 의 필터 primitive 가 obvious 하게 broken
   인 아티팩트를 받아들이지 않고는 cel-shading 으로 composition 안 됨.
   진짜 cel-shading 은 세 가지 중 하나에 살아있음: GLSL 쉐이더 (mpv +
   libplacebo, 프로젝트가 ~200-500 줄 쉐이더 코드 + mpv-render 어댑터
   픽업), EbSynth (1 keyframe 페인팅, 모션 따라 전파 — 절차적 셰이딩
   완전 우회), 또는 AI 스타일라이즈 (Stable Diffusion + AnimateDiff,
   ComfyUI, RunwayML).  세 가지 모두 production 퀄리티 비디오 ship 하는
   사람들이 쓰는 진짜 도구 — 어느 것도 music-video 미션의 단일-ffmpeg-
   패스 아키텍처 안에 안 맞음.  그래서 카툰은 별도 R&D 분기로 park,
   review 에서 방어해야 하는 네번째 ffmpeg variant 로 half-implemented
   하지 않음.

**Phrase-aware combo** (실제 ship 된 deliverable — `scripts/music-video-shaders.sh`
의 `combo` 모드) 가 pond + halation 을 95.8 BPM 레퍼런스 cadence 에 묶인
envelope 로 glue.  Pond amplitude 는 `clip()` 기반 게이트로 곱셈됨,
인트로 (0–15 s) 에 0, 빌드 (15–22.5 s) 에 1로 ramp, 클라이맥스 (22.5–45 s)
에 풀, 그 뒤 taper.  Halation opacity 가 비슷한 커브, 0.10 → 0.35 → 0.20.
전체가 하나의 `ffmpeg -filter_complex` invocation 안에서 끝남; envelope
는 Python frame-stitcher 가 아니라 필터 표현식 안에 살아있음.

**결정 산출물:** [`scripts/music-video-shaders.sh`](../scripts/music-video-shaders.sh)
(`23832fa` 에서 커밋) 에 네 효과 + deferred-카툰의 무엇이 안 맞는지
설명하는 docstring.

**보존된 교훈:** 도구가 *거의* 도달하기 때문에 쉐이더 효과를
half-implementing 하는 게 ship 안 하는 것보다 나쁨.  ffmpeg-can-do-
everything 가정은 안 그럴 때까지 괜찮음 — 정직한 move 는 벽을 이름
짓고 올바른 도구로 라우팅 (그 도구가 아직 프로젝트에 없어도).

---

## 6. 온보딩 마찰이 첫 접점을 죽임 — 제로-계정 데모 경로

**문제.** 보안 분야 종사자가 커피숍 세션에서 레포 README "Quick start"를
처음부터 따라가다 음악 영상 미션이 한 번도 돌기 전에 세 군데 막힘:

1. `PEXELS_API_KEY` 필수.  Pexels 가입은 Google / Apple / Facebook OAuth
   강제 — 이메일 경로 없음.  Naver / Kakao 가 주된 KR 유저는 쓸 만한
   OAuth 제공자가 없음; Google 계정이 있는 유저도 신원 상관 surface 가
   가볍지 않음.
2. "Get API key" UI 가 Pexels 대시보드 메뉴 두 단계 안에 묻혀 있어
   외부 가이드 없이는 대부분의 유저가 찾지 못함.
3. music-video 미션은 운영자 공급 음악 파일을 필요로 함.  표준 소스는
   Suno — 수동 6단계 왕복 (가입 → 커스텀 모드 프롬프트 → 대기 →
   N개 중 베스트 선택 → mp3 다운로드 → `assets/music/`에 드롭).
   Suno API 없음; 매 트랙이 별도 UI 세션.

첫 출력 전 이탈률 누적 ≈ 높음.  추가로:
처음 `.env`에 API 키를 편집하는 행위 자체가 전형적 자격증명 유출
벡터 (GitHub 의 자동 revoke 로그가 매일 수천 건의 키-인-커밋 사건
기록).  `.env` 를 한 번도 안 여는 데모 경로는 그 공격 표면을 통째로
제거.

**제약.** 기존 풀 패스를 깰 수 없음 — Suno 트랙 + Pexels 키를
*가진* 운영자는 여전히 키워드별 무드 매칭 B-roll 을 원함.  저작권
정책 위반 불가 — 모든 소스가 실제 allowlist 엔트리 + `outputs/SOURCES.txt`
의 중복 제거된 attribution 크레딧을 요구함.  Anthropic 비용 추가 불가
— 이건 제로-계정 *데모*, 큐레이션된 경로가 아님.

**결정.** 미션이 이미 체크하는 `$CLIPS_DIR/raw-<keyword>.mp4`
경로를 per-segment Pexels fetch 루프가 돌기 전에 사전 채우는 병렬
데모 경로 추가.  기존의 `if [[ ! -f "$RAW" ]]` 체크가 API 호출을
짧게 끊어버림 — 핫패스에 새 코드 없음.

메커니즘:

- `scripts/fetch-demo-broll.sh` — Blender Foundation CDN 의
  큐레이션된 CC-BY-3.0 클립.  HEAD-체크된 URL, Pexels 용으로 이미
  `agents/lib/attribution.sh` 가 읽는 모양의 사이드카 JSON.
- `scripts/fetch-demo-music.sh` — Kevin MacLeod 의 Incompetech
  카탈로그에서 큐레이션된 CC-BY-4.0 트랙.  미션이 이해하는
  키워드 카테고리에 걸쳐 다섯 가지 무드.  `incompetech.com` 을
  `config/copyright-allowlist.yaml` 에 추가 + 누락되어 있던
  CC-BY-4.0 publish_rule 추가 필요했음.
- `MUSIC_VIDEO_DEMO_MODE=1` env 스위치 in
  `agents/missions/music-video/run.sh` —
  `require_env PEXELS_API_KEY` 스킵, 인자 없을 때 `MUSIC_FILE` 을
  캐시된 첫 데모 트랙으로 디폴트, 데모 캐시에서 `$CLIPS_DIR` 사전
  채움.  핫패스에 새 라인 20개; 기존 비-데모 흐름은 바이트
  단위로 동일.
- `scripts/bootstrap.sh` UX —
  no-key-AND-no-music 상태 감지, 두 개의 경고 블록 대신 정확히
  `MUSIC_VIDEO_DEMO_MODE=1 …` 커맨드를 추천된 Next Step 으로 출력.

**검증.** `scripts/test-demo-mode.sh` 가 새로 클론된 트리에 대해
전체 경로 실행: `git clone` → `bootstrap.sh` →
`MUSIC_VIDEO_DEMO_MODE=1 ./run.sh demo` → `short.mp4` ≥ 1 MB +
지속 시간 ≥ 50 s + `SOURCES.txt` 에 CC-BY 크레딧 ≥ 2 라인을
assert.  첫 PASS 2026-05-19 01:25 KST 에 로컬 feat 브랜치 대상으로
기록: 81 MB, 60 s, 3개 중복 제거된 크레딧 라인.  콜드 스타트
clone → 재생 가능한 mp4 까지 wall-time ≈ 2 분 30 초 (테스트 머신).

**교훈 보존:** "마찰이 큰 경로는 고급 케이스용 opt-in 으로
만들고, 디폴트로는 두지 마라."  풀 Pexels + Suno 흐름은 여전히
존재 — 시스템에 commit 한 유저용 업그레이드 경로로 문서화됨.
다만 게이트키퍼는 아님.  기존 인프라 (allowlist + 사이드카
attribution + 파일시스템 캐시 short-circuit) 가 *거의* 이걸 위해
설계되어 있었음; 데모 모드 변경은 대부분 이미 작동하던 조각의
컴포지션이지 새 메커니즘이 아니었음.

**결정 산출물:** [`scripts/fetch-demo-broll.sh`](../scripts/fetch-demo-broll.sh)
+ [`scripts/fetch-demo-music.sh`](../scripts/fetch-demo-music.sh)
+ [`scripts/test-demo-mode.sh`](../scripts/test-demo-mode.sh)
+ [`docs/onboarding/demo-mode.md`](onboarding/demo-mode.md).
`v0.2.0` 에서 main 머지 완료.

**필드 관측 보충 (2026-05-19 ~14:00 KST)** — 동일한 보안
전문가가 후속 미팅에서 fresh-clone 으로 데모를 직접 돌림.
클론 → 렌더 경로는 작동.  그러나 테스트 게이트가 못 잡은
두번째 벽이 드러남: **Claude Code 도구별 권한 프롬프트**.
프로젝트 트래킹된 `.claude/settings.json` 은 70개 항목 allow
리스트가 있고 `install-claude-local.sh` 가 올바르게 렌더
하지만 — Claude Code 가 세션 시작 + 첫 디렉토리 신뢰 시점에
**유저 레벨** `~/.claude/settings.json` 도 함께 참조함, 프로젝트
파일만으로는 모든 프롬프트를 억제하지 못했음.  친구 경험:
데모 1회 실행 동안 ~30개의 "이 명령 허용할까요?" 다이얼로그.
운영자 프레이밍: "다 하나씩 승인하기에는 너무 장벽이커.. 첨에
권한관련해서도 승인하면 어느정도 넘어가게 되어야 할듯".

후속 픽스는 스크립트 하나:
[`scripts/install-claude-permissions.sh`](../scripts/install-claude-permissions.sh).
렌더된 프로젝트 allow 리스트를 읽고, 운영자에게 한 번 확인
("이걸 유저 레벨 설정에 머지할까요? Y/n"), 동의하면
`~/.claude/settings.json` 에 머지 — append + dedupe 만,
유저 기존 deny 리스트 절대 mutating 안 함, 덮어쓰기 전 JSON
검증, `_notes.melons_agents` 프로비넌스 블록 기록.
bootstrap.sh 가 interactive 모드 + TTY 체크와 함께 호출하므로
CI 실행에서 hang 안 함.

이 교훈은 테스트 게이트가 놓친 것:
**재현성 테스트는 "스크립트가 돌았다"를 검증함.
"한 인간의 온보딩 경험이 견딜만 했다"를 검증하지는 않음.**
test-demo-mode.sh 는 잘 돌았는데 Claude Code 를 통째로 건너뛰고
bash 를 직접 실행했기 때문임.  같은 흐름을 Claude Code 를
통해 돌리는 실제 첫 사용자는 테스트 시나리오가 본 적 없는 ~30개
프롬프트를 마주함.  픽스와 테스트 게이트 둘 다 이제 ship 되었지만,
필드 관측은 게이트가 아니라 사람한테서 왔음.

필드 관측 산출물:
[`scripts/install-claude-permissions.sh`](../scripts/install-claude-permissions.sh)
+ [`docs/onboarding/claude-permissions.md`](onboarding/claude-permissions.md).
`feat/permission-bootstrap` 에 있음, 다음 라운드 테스트 후 머지 대기.

---

## 공통점

- 각각 **구체적 관측된 실패**에서 출발했지, 이론적 우려에서 출발한 게 아님.
- 각 픽스는 **그 실패를 해결하는 최소 메커니즘** — 일반화된 프레임워크가 아님.
- 각각 **미래 운영자가 들여다볼 수 있는 산출물을 남김** — 스크립트, 컨피그,
  혹은 에이전트 시스템 자신이 읽는 문서.
- 각각 **되돌릴 수 있음** — 버전 관리되는 bash 스크립트, 외부 상태 없음,
  롤백할 DB 마이그레이션 없음.

코스트 라우팅 룰, 스로틀러, 피드백 루프, 감사 레이어는 서로 독립적입니다 —
어느 하나만 채택해도 다른 것 없이 작동합니다. 이들이 묶이는 이유는 같은
근본 질문에 답하기 때문입니다: *한 명의 운영자가 본인이 병목이 되지 않으면서
이 파이프라인을 돌리려면 최소한 어떤 메커니즘이 필요한가?*
