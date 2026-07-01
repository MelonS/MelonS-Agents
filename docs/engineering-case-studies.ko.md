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

## 8. 측정 안 되던 축 — 자율성 신호 + 개입 감소 lever

**문제.** 사람이 *계속* 조타해야 하는 멀티 에이전트 시스템은
사실 자기가 대체하려던 그 노력을 그대로 다시 들이고 있을 뿐입니다.
하지만 "이 시스템에 사람 손이 얼마나 필요한가?" 는 한번도 측정된
적이 없었습니다.  2026-05-17 에 `docs/metrics/intervention.png`
차트가 추가됐고, README 리라이트 한 번 (`aa10ba0`, 2026-05-18
music-video-first 갱신) 만에 README 에서 조용히 빠져버렸습니다.
2026-05-22 시점에 그 데이터는 2일 stale 상태였고 아무도 눈치
못 챘습니다.  측정하지 않는 축은 drift 합니다.

**제약.** 신호는 정직하고, 다차원이고, 사적이어야 했습니다:

- **정직** — 에이전트가 점수를 조작할 수 없어야 함 (예: 10개 커밋을
  squash 해서 "user-initiated" 카운트를 낮추는 식).  해결: 커밋
  본문에서 명시적 사용자 방향 마커 (`Requested-by: user`,
  "Operator surfaced", 한국어 직접인용) 를 읽는 라인별 분류기.
- **다차원** — 커밋 카운트만으로는 운영자의 *시간* 참여도가 빠집니다.
  레버리지 높은 날 (자율 오버나잇) 과 손-많이-가는 날 (라이브
  코딩 세션) 둘 다 10 커밋이 나올 수 있음.  세션 분 (minutes)
  도 같이 잡아야 함.
- **사적** — `~/.claude/projects/-Users-melons-ai/` 의 세션 JSONL
  은 운영자의 verbatim 프롬프트를 담고 있고 종종 personal 컨텍스트
  를 포함합니다.  마이닝 스크립트는 이를 로컬에 묶어 두고 절대
  업로드 안 함 — 집계 카운트만 커밋된 JSON 에 들어감.

**결정.** 운영자 액션 없이 매일 갱신되는 **2-source 2-panel 신호**:

- **Panel A** — `git log` 커밋 attribution.  user-initiated vs
  agent-autonomous 일별 카운트 + ratio + leverage
  (`agent / max(1,user)`) + longest autonomous gap (h).
- **Panel B** — 로컬 Claude Code 세션 JSONL 마이닝.  일별
  operator-prompt 카운트 (텍스트 콘텐트 user 메시지만; `tool_result`
  자동 응답은 제외) + 활성 세션 분 (세션당 60분 cap — idle 노트북이
  신호를 부풀리지 않게).
- 차트는 매일 02:00 KST `com.melons.agents.intervention-chart`
  launchd 잡으로 자동 재생성.

신호가 정직해진 다음, 동반 reduction memo
(`docs/research/2026-05-22-intervention-reduction.md`) 가 5 개의
우선순위 매겨진 **lever** 를 추세에 *작용하기 위해* 정리:

1. 분류기 false-positive scrub — 5 개의 플래그된 commit 을 스팟체크
   한 결과 모두 정당하게 user-initiated 인 것으로 확인되어 **무효화**.
   교훈: 그럴듯해 보이는 가설이 한 라운드 검증으로 무너질 수 있음.
2. 추천 옵션을 기본으로 (`[[minimize-intervention]]`) — 이미 메모리에
   저장된 룰, 지속 강화.
3. **테이스트 리뷰 batch** — `outputs/review-queue/` + 3 개 스크립트
   (`review-queue-add.sh` / `-digest.sh` / `-decide.sh`).  새 렌더는
   `agents/missions/music-video/run.sh` 에서 자동 enqueue; 운영자는
   per-render 핑 대신 자신의 cadence 로 contact-sheet markdown 을
   drain.  10× fewer intervention events, 같은 total decision count.
4. **statusline 이 status ping 흡수** — `scripts/statusline.sh` 가
   `scripts/doctor.sh --json` (60s background-regen 캐시) 과
   goal-lock skill 의 진행도 카운트를 읽음.  운영자는
   `doctor:✓/⚠N/✗N · goal:N/M · audit⚠` 를 상시 봄 → "지금
   상태가 뭐임?" 프롬프트 부류 제거.  Companion: `actionable_warn`
   분류로 opt-in env-key gap 이 카운트를 부풀리지 않게.
5. permission bootstrap — 이미 v0.3.0 에 ship
   (`feat/permission-bootstrap`, fresh-clone 첫 세션에서
   ~30 prompt/session 감소).

추가로 **autonomous-decisions log** (`docs/autonomous-decisions.md`
+ `scripts/log-decision.sh`) — 오버나잇 작업 중 에이전트가
unilateral 결정을 내리면 한 줄 append.  운영자는 아침에 한 페이지를
60초 이내로 읽고 무엇이 결정됐고 *무엇을 하지 않기로 결정됐는지*
(lever dismissal 도 기록되니까 다음 세션에 같은 가설이 재탐색되지
않음) 를 파악.

**산출물.**

- `docs/metrics/intervention.png` — 2-panel 차트.
- `docs/metrics/intervention.json` — 일별 raw 데이터 (`user_initiated`,
  `agent_autonomous`, `user_ratio_pct`, `leverage_ratio`,
  `longest_autonomous_gap_h`, `operator_prompts`,
  `active_session_minutes`, `session_count`).
- `scripts/generate-intervention-chart.py` — 분류기 + 마이너.
- `scripts/intervention-chart-collect.sh` — matplotlib venv bootstrap
  포함 runner.
- `scripts/com.melons.agents.intervention-chart.plist.template` —
  매일 02:00 KST launchd.
- `docs/research/2026-05-22-intervention-reduction.md` — 우선순위 +
  lever 별 상태가 정리된 메모.
- `docs/autonomous-decisions.md` + `scripts/log-decision.sh` — 한
  페이지짜리 wake-up 요약.
- `outputs/review-queue/` + 3 개 스크립트 — batched taste-decision
  큐.
- `scripts/statusline.sh` + `scripts/doctor.sh` actionable_warn —
  status 가 상시 보이는 UI 에 흡수됨.

**결과 (9일 window, 2026-05-14 → 2026-05-22 partial):**
median user-ratio ≈ 19%, range 0%–69% (5/17 스파이크는 chart + site
+ scorecard 가 처음 landing 한 날 — 무거운 taste-call density).
최고 leverage 날은 2026-05-20 (7.7×, 11% ratio) — Skill #2 v0.4.0
를 출고한 자율 오버나잇.  2026-05-22 partial (03:00 KST 까지) 은
그것을 넘을 추세: 8% ratio, 11.5× leverage, 9 operator prompts
across 8 sessions for ~99 active minutes — 이 케이스 스터디 자체
의 작업 대부분이 그 신호 안에서 돌았습니다.

정직한 disclosure: 이건 운영자 한 명의 일일 신호이지 통계적 연구가
아닙니다.  하지만 *그게* 정직한 신호입니다 — 신호 없는 것보다
노이즈가 있는 신호가 낫습니다.

이 케이스 스터디가 #4 (감사) 와 별도로 의미 있는 이유: 감사는
**시스템이 계약을 지키는가?** 를 측정합니다.  이건 **운영자가
loop 안에 있어야 시스템이 작동하는가?** 를 측정합니다.  다른
질문, 다른 메커니즘, 둘 다 필요.  코스트 라우팅 룰 (#1) 처럼
답은 minimum mechanism — 차트와 메모, 프레임워크가 아니라 —
이고 각 lever 는 독립적 (다른 것 깨지 않고 어느 하나 drop 또는
swap 가능).

---

## 10. Windows 네이티브 ffmpeg는 셸이 변환 안 한 POSIX 경로를 못 읽음 — 어디서나 basename + cd

**문제.** faceless-short 렌더 체인(concat → 9:16 필 → 캡션 번인 →
출처 오버레이)이 macOS에선 깨끗이 돌았는데 Windows/Git-Bash에서 세
가지로 실패 — 근본 원인은 하나. concat이 `Impossible to open
'trimmed-0.mp4'`, 캡션/오버레이가 drawtext 폰트에서 `rc=127`,
오버레이 `text='NEWS:LIVE'`가 필터 파서를 깨뜨림.

**제약.** 파이프라인은 Git-Bash에서 호출하는 단일 네이티브
`ffmpeg.exe`(BtbN 빌드). MSYS는 POSIX 경로(`/g/...` → `G:\...`)를
자동 변환하지만 **명령 인자(argv)**로 인식한 것만. 변환 안 하는 것:
(a) 도구가 읽는 **파일 내용** 속 경로(concat 리스트의
`file '/g/ai/.../trimmed-0.mp4'` 줄), (b) **필터그래프 문자열 내부**
경로(`drawtext=fontfile=/c/Windows/Fonts/malgun.ttf`,
`ass=/g/.../captions.ass`). 네이티브 ffmpeg는 `/g/...`를 *상대경로*
`G:\g\...`로 재해석해 못 엶. 도구 수정 불가, `G:\` 하드코딩은
macOS를 깸.

**결정.** POSIX 절대경로가 파일 내용·필터 문자열을 통해 ffmpeg에
도달하지 못하게. 모든 ffmpeg 경계에 기계적 규칙 둘:

1. **concat 리스트 → 상대 basename + `cd`.** `file 'trimmed-0.mp4'`
   (basename만) 쓰고 resources 디렉토리 안에서 concat 실행
   (`( cd "$MDIR/resources" && ffmpeg ... -f concat -safe 0 -i
   "$(basename "$list")" ... )`). 상대명은 변환 불필요, 작업
   디렉토리가 모든 OS에서 해석.
2. **필터 문자열 자산 → basename 스테이징, `cd`, basename 참조.**
   ASS 자막·오버레이 폰트·출처 textfile을 한 디렉토리에 스테이징,
   렌더가 거기로 `cd` 후 필터는 `ass=captions.ass:fontsdir=.`,
   `drawtext=fontfile=<font>:textfile=<attr>`. `fontsdir=.`는
   fontconfig 없는 호스트(Windows)에서 libass가 스테이징된 폰트를
   패밀리명으로 찾게 함.

세 번째 버그가 딸려옴: `drawtext=text='NEWS:LIVE'` — 콜론이 필터
옵션 구분자라 ffmpeg가 `SCENE'`을 엉뚱한 옵션으로 파싱. 해결:
캡션/오버레이 텍스트를 인라인 금지, 파일에 써서 `textfile=`
(따옴표·쉼표·한글 이스케이프도 회피).

**산출물.**
- `agents/missions/faceless-short/run.sh` — concat basename+cd
  (Stage 6); 필터 전 ASS/폰트/attr을 basename으로 스테이징.
- `scripts/subject-overlay.sh` — 네 텍스트 요소 전부
  `text=`→`textfile=`; 폰트 `_ov_font.<ext>`로 스테이징; 스테이지
  디렉토리에서 실행.
- `docs/platform-windows.md` § "ffmpeg + Git-Bash 경로 변환".

**교훈.** MSYS 경로 변환은 새는 추상화 — argv만 덮고 거기서 끝.
네이티브 도구에 *두 번째 채널*(읽는 파일, 파싱하는 문자열)로
도달하는 경로는 무방비. 이식 가능한 형태는 "OS 감지 후 경로
재작성"이 아니라 "애초에 그 채널에 절대경로를 안 넣기". basename +
`cd`는 OS 무관 + 분기 불필요.

---

## 11. Mac YouTube 토큰이 죽었고 redirect 포트가 틀렸음 — 무인 업로드 부활

**문제.** 자동 업로드(Mac에서 매 렌더를 올리던 워크플로)가
Windows에서 즉시 실패: `oauth2: "invalid_grant"`. 새로 동의를 받은
뒤엔 *두 번째* 실패 — 브라우저 동의는 끝났는데 `youtubeuploader`가
콜백을 못 받고 영원히 멈춤, 토큰 미기록.

**제약.** 업로드는 `youtubeuploader`(네이티브 바이너리, 업로드 전용
— auth-only 모드 없음) + Mac에서 가져온 OAuth 토큰. 인터랙티브
재동의는 대신 눌러줄 수 없는 실제 브라우저 클릭 필요 — 운영자가
3클릭으로 끝내는 흐름이어야. 크리덴셜은 도구 기본값 `~/.config/`가
아니라 `/g/config/youtubeuploader/`에 있음.

**결정.** 별개의 근본 원인 둘, 수정 둘:

1. **죽은 refresh 토큰 = 테스트 모드 만료.** 토큰이 36일 지남.
   구글은 OAuth 앱이 **"테스트(Testing)"** 게시 상태면 refresh
   토큰을 **7일** 뒤 만료. 즉시 수정: stale `request.token` 제거 후
   재동의 — 래퍼 `yt-batch-upload.sh`는 설계상 첫-실행 동의를 거부
   (cache-not-found 가드)하므로 *첫* 동의는 `youtubeuploader`를
   **직접** 실행해야. 근본 수정: Cloud Console에서 앱을
   **Production**으로 게시 → 7일 상한 제거. (민감 범위라 "미확인 앱"
   경고는 남지만 그건 동의 시점 화면이지 토큰 수명 문제 아님.)
2. **콜백 미도착 = redirect 포트 불일치.** `client_secrets.json`의
   `redirect_uris: ["http://localhost"]`(포트 80)인데
   `youtubeuploader`는 루프백 콜백을 **:8080**에서 대기. 구글이
   동의를 `localhost:80`으로 리다이렉트 → 아무도 안 들음, 도구는
   :8080에서 영영 대기(netstat로 8080 인바운드 0 확인). 수정: 등록
   redirect를 서버에 맞춤 —
   `jq '.installed.redirect_uris = ["http://localhost:8080"]'`(백업
   보존). Desktop("installed") 클라이언트는 구글이 임의 루프백 포트를
   허용하므로 정렬 후 바로 동작.

**산출물.**
- `/g/config/youtubeuploader/client_secrets.json` — `redirect_uris`
  → `http://localhost:8080`(`.bak` 보존).
- `docs/platform-windows.md` § "YouTube 업로드 OAuth
  (youtubeuploader)" — 포트 규칙 + 테스트-vs-Production 만료 +
  직접-vs-래퍼 첫-동의 노트.

**검증.** 두 수정 후 `youtubeuploader`가 동의 완료 + 업로드
(`Upload successful!`); 새 토큰으로 `videos.list` 호출해 채널·공개
상태 확인, 수동 `refresh_token` grant로 동의창 없는 무인 재인증
작동 확인.

**교훈.** "invalid_grant"가 별개 실패 둘을 숨김: *만료된* 크리덴셜
+ *잘못 설정된* redirect. 두 번째의 단서는 에러 텍스트가 아니라
구조적 — 서버는 8080에서 듣고, 등록 redirect는 80이라, 콜백이
엉뚱한 문으로 감. OAuth 루프백이 "승인 후 멈추면" 토큰 의심 전에
도구의 리스닝 포트와 클라이언트 `redirect_uri`부터 비교.

---

## 12. "단일줄 자막"이 한글에선 두 줄이었음 — 코드포인트 vs 렌더 폭

**문제.** 한글 자막이 겹침: 큐가 두 줄로 wrap되면 줄별 불투명
박스(libass BorderStyle=3)가 서로 닿아 눈에 띄게 겹침. 파이프라인엔
*이미* 이걸 막는 가드가 있었음 — `split-long-captions.py`가
`CHAR_MAX`(28) 초과 큐를 단일줄로 분할 — 그런데도 출고된 한글
렌더에서 15개 중 14개가 여전히 wrap.

**제약.** 가드가 큐 길이를 `len(text)` — **코드포인트** 수 — 로
잼. 한글 글리프는 라틴 문자의 ~2배 폭 렌더(~50px vs ~22px, ~880px
자막 안전영역 대비), 그래서 한글 28 코드포인트 = ~1400px(거의 두
줄 꽉)인데 `len ≤ 28`은 통과. 스플리터 docstring이 "28자면 어느
언어든 한 줄"이라 주장했는데 전각 문자엔 산술적으로 거짓. 폰트
축소나 `WrapStyle` off는 가독성을 해치거나 프레임 밖으로 넘침.

**결정.** 예산을 문자 수가 아니라 **렌더 폭**으로.
`visual_width(text)`가 East-Asian Wide/Fullwidth 문자
(`unicodedata.east_asian_width in {W,F}`)를 2로, 나머지를 1로 셈;
스플리터의 모든 폭 비교(`split_text`, `greedy_word_split`)가 이걸
사용. `char_max`는 28 유지하되 이제 *반각 폭 단위* 의미: 영문은
불변(28자=28단위), 한글은 ~14글리프/줄로 정확히 제한. 검증 중 더
미묘한 두 번째 버그: `merge_short_neighbours`가 blip 방지로 1초
미만 큐를 재결합 → 방금 분할한 짧은 한글 꼬리 조각을 넓은
줄로 도로 붙임 — 그래서 merge도 같은 폭 가드를 달아 `char_max`
초과 결합을 거부(짧은 단일줄 blip이 두 줄 겹침보다 나음).

**산출물.**
- `scripts/split-long-captions.py` — `visual_width()`; 폭 인식
  `split_text`/`greedy_word_split`; 폭 가드
  `merge_short_neighbours`; docstring 교정.

**검증.** 출고된 한글 SRT 기준: 이전 14/15 큐 예산 초과(최대
57단위), 이후 33/33 큐 단일줄(최대 27단위, 초과 0), 멱등(재실행 시
추가 분할 0). 영문은 바이트 동일 — 폭-1 문자엔 무영향.

**교훈.** 검증 임계치는 단위가 맞아야만 옳음. `len()`은 "몇 글자"와
"얼마나 넓은가"를 조용히 뒤섞음 — ASCII에선 괜찮고 전각 문자가
나타나는 순간 틀림. 라틴 기준 상수가 거짓말하는 단서: 명시된 글리프
폭이 반박하는 주장("28이면 어느 언어든")으로 정당화돼 있었음.
"단일줄 강제기"가 여전히 두 줄을 내면 메커니즘 전에 지표를 의심.

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
