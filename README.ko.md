<div align="center">

# MelonS-Agents

**한국어** | [English](./README.md) · [**라이브 사이트 →**](https://melons.github.io/MelonS-Agents/)

**콜로니심 게임을 직접 만들고 — 그걸 플레이해서 자기 작업을 검증하는 AI 에이전트.**  실제 플레이어 클릭을 합성해 각 클릭이 (단지 클릭이 도달한 게 아니라) 실제로 게임 상태를 바꿨는지 어서트하고, 길게 무인으로 돌린 소크는 스크린샷과 로그만 보고 — 작성자의 의도는 절대 보지 않고 — 판정하는 별도 서브에이전트가 채점합니다.

이 자기 검증 루프가 MelonS-Agents 의 척추입니다 — [Claude Code](https://docs.anthropic.com/claude-code) 로 구동되는 멀티 에이전트 시스템.  오늘 세 트랙이 돌아갑니다: 콜로니심 **PawnSim**(Windows + Unity, *개발 중*), 그리고 두 프로덕션 파이프라인 — **music-video** 메이커(음악 → 60초 9:16 쇼츠, Mac/Linux 에서 ~60초)와 **job-hunt** 다이제스트(키워드 하나 → 중복 제거된 한국 채용공고 요약, ~5초).  로컬 오픈소스 도구(ffmpeg / whisper.cpp / ollama / aubio)가 기계적 작업을 하고 Claude 는 오케스트레이션 + 창작 판단을 맡아 — 미션당 **런타임 API 토큰 0개**.  첫날부터 영어 + 한국어.

[![main-protection](https://github.com/MelonS/MelonS-Agents/actions/workflows/main-protection.yml/badge.svg?branch=main)](https://github.com/MelonS/MelonS-Agents/actions/workflows/main-protection.yml)
![GitHub last commit](https://img.shields.io/github/last-commit/MelonS/MelonS-Agents?style=flat-square)
![Runtime API tokens](https://img.shields.io/badge/runtime%20API%20tokens-0-10B981?style=flat-square)
![Built with Claude Code](https://img.shields.io/badge/built%20with-Claude%20Code-D97757?style=flat-square&logo=anthropic&logoColor=white)
![License](https://img.shields.io/github/license/MelonS/MelonS-Agents?style=flat-square)

![PawnSim 콜로니 타임랩스 (2026-06-12 빌드, 게임 내 16일) — 콜로니스트 셋이 맨땅에서 시작해 저장 구역과 농장을 지정하고, 벽·침대·화덕·연구대를 갖춘 집을 지으며, 습격을 막아내고 성장하는 과정.  모든 프레임은 사람 개입 없는 자동 플레이 기록이며, 화면에 보이는 기본 루프(저장→집짓기→농사→벌목→채광)는 전부 효과 어서션 + 격리 채점 루브릭 판정으로 기계 검증된 상태](docs/demo/pawnsim-2026-06-12-colony-timelapse.gif)

*사람 개입 없는 16 게임일 콜로니 소크 — 에이전트가 이 게임을 만들고 **검증까지** 했습니다.*

</div>

![MelonS-Agents — 숫자로 보기: 출력 100+, 프로덕션 스킬 2개, 쉐이더 23개, 런타임 API 토큰 0개, 15시나리오 게이트, 서브에이전트 19개, 감사 레이어 3개, MIT](docs/visuals/01-hero-stats-ko.png)

## 지금 무엇이 되는가

| 트랙 | 하는 일 | 상태 | 지금 실행 가능? |
|------|---------|------|-----------------|
| **music-video** | 음악 → 60초 9:16 쇼츠 (비트 정렬 컷, 빈티지 ffmpeg 쉐이더) | 프로덕션\* | ✅ Mac/Linux — `./scripts/first-touch.sh`, ~60초 |
| **job-hunt** | 시드 키워드 하나 → 중복 제거된 한국 채용공고 다이제스트 (소스 11개) | 프로덕션\* | ✅ ~5초, 네트워크·키 불필요 |
| **PawnSim** · `game-dev-agent` 가 제작 | 자기 검증형 콜로니심 게임 프로토타입 | 개발 중 | ⚠️ Windows + Unity 6000.0.75f1 |
| **product-cf** | 제품 사진 → CF 스타일 쇼츠 | 보류 | ❌ 정직한 부정 결론으로 보류 |

<sub>\*"프로덕션" = 정해진 주기로 실제 결과물을 출하 (이 둘이 핵심 카운트).  `game-dev-agent` 는 PawnSim 을 만드는 메타 스킬 — PawnSim 이 출하 주기에 도달하면 프로덕션 카운트로 승격됩니다.</sub>

**핵심 용어** — *재현 게이트(repro gate)*: 에이전트가 실제 플레이어 클릭을 재생하고, 클릭이 도달했는지가 아니라 각 클릭이 효과를 냈는지를 어서트합니다.  *격리 채점(isolated grader)*: 작성자의 의도가 아니라 스크린샷 + 로그만으로 실행을 판정하는 별도 서브에이전트.  *소크(soak)*: 길게 돌리는 무인 테스트 런.

## 60초 안에 시작

> **사전 요구:** `ffmpeg`, `ollama`, `aubio` 가 PATH 에 있는 Mac 또는 Linux — 마법사가 먼저 점검하고 빠진 항목에 대해 정확한 `brew` / `apt` 설치 명령을 출력합니다 (clone-and-go 는 macOS 에서 검증됨).

```bash
git clone --depth 1 https://github.com/MelonS/MelonS-Agents.git
cd MelonS-Agents
./scripts/first-touch.sh        # 가이드 데모: 도구 점검, 9:16 쇼츠 렌더, 결과 열기
```

Pexels 가입 없음, Suno 왕복 없음, `.env` 편집 없음 — 마법사가 데모 캐시를 받아 번들된 CC-BY 클립 + 음악으로 60초 쇼츠를 렌더합니다.  수동·고급·스킬별 경로는 아래 **실행 경로**에 접혀 있습니다.

## 검증 — 에이전트가 자기 작업을 확인하는 방식

![검증 — 두 게이트: 커밋마다 15시나리오 입력 레벨 재현 게이트 + 장시간 소크에 격리 채점 서브에이전트](docs/visuals/14-verification-loop-ko.png)

대부분의 에이전트 데모가 건너뛰는 부분입니다.  PawnSim 은 들어오는 길에 두 게이트를 통과합니다:

- **커밋마다 15시나리오 입력 레벨 재현 게이트.**  에이전트가 플레이어와 똑같은 UI 경로로 실제 클릭을 합성하고, 각 클릭이 (단지 도달한 게 아니라) *효과*를 냈는지 — "그 클릭이 지정(designation)을 놓았는지" — 어서트합니다.
- **장시간 소크에 격리 채점 서브에이전트.**  증거(스크린샷 + 원시 로그)만 보고 작성자의 의도는 절대 보지 않으며, 작성된 루브릭에 따라 실행을 채점합니다.

이 채점기는 셀프 리뷰가 놓친 것을 반복해서 잡아냈습니다: 모든 지정을 조용히 무효화하던 하네스 사각지대, "식량이 넘치는데 콜로니가 굶어 죽는" 기분 게이트 함정, 영구 정신 붕괴로 인한 콜로니 프리즈.  기본 콜로니 루프(저장 → 집짓기 → 농사 → 벌목 → 채광)는 이제 엔드투엔드 기계 검증되며, 루브릭 판정이 수정과 함께 커밋됩니다.  *문제 → 제약 → 결정 → 산출물* 형식으로 정리한 9개 프로덕션 인시던트: [`docs/engineering-case-studies.ko.md`](docs/engineering-case-studies.ko.md).

## 움직이는 PawnSim

![PawnSim 2026-06-12 — 풀밭 위 초기 콜로니: 체력·기분 바를 단 이름표 콜로니스트 셋, 우상단에 올라가는 목재 벽 골조(32px 톱다운 블록 벽), 모은 목재와 흩어진 광맥·돌, 실시간 자원 카운터, 하단에 열린 건축 메뉴](docs/demo/pawnsim-2026-06-12-built-house.png)

콜로니스트는 유틸리티 AI 아래에서 벌목 / 채광 / 농사 / 요리 / 운반 / 건축 / 연구 / 전투를 하고, AI 디렉터가 지터를 준 시계로 위협을 스케줄하며, 플레이어는 폰을 징발하고 건축 + 지정 명령을 칠합니다.  모든 스프라이트(완전한 **32px 아트 생성**), 모든 씬, 모든 C# 시스템이 [`game-dev-agent`](skills/game-dev-agent/) 에 의해 CLI 로 스캐폴딩되며 **수동 Unity 에디터 작업은 전혀 없습니다**.  전체 기능 목록 + 정직한 검증 상태(알려진 갭 포함): [`skills/game-prototype/README.md`](skills/game-prototype/README.md).

## 샘플 출력 — music-video

![2026-05-22 noir-detective 렌더의 5초 애니메이션 프리뷰 — 9:16 세로 쇼츠, 스모키 바 인테리어, pink-magenta rnb_low_key 그레이드 프로필의 파이프를 문 수염 남자.  phrase-aware 쉐이더 + 장르별 컬러 그레이드가 평범한 Pexels B-roll 을 장르 코드가 입혀진 룩으로 변환](docs/demo/music-video-noir-detective-2026-05-24-preview.gif)

음악 → 음악이 메인 오디오인 9:16 쇼츠: 비트 정렬 컷, onset 정렬 글리치 마이크로 에디트, 그리고 일곱 가지 장르별 컬러 그레이드 중 하나가 평범한 스톡 B-roll 을 장르 코드가 입혀진 룩으로 만듭니다.  2026-05-17 에 이전의 내레이션 기반 포맷을 밀어내고 채택됐습니다.  전체 파이프라인 — 23 쉐이더, 장르 카탈로그, v1→v6 진화: [`docs/music-video-pipeline-reference.md`](docs/music-video-pipeline-reference.md).

## 아키텍처

![3-shape 스킬 모델 — Shape A 미션 라우팅 5에이전트 파이프라인, Shape B 독립형, Shape ? 미래 스킬](docs/visuals/05-three-shapes-ko.png)

시스템은 모든 스킬을 하나의 형태로 강제하지 않습니다.  **Shape A** 는 5에이전트 미션 파이프라인(orchestrator + planner / resourcer / editor / qa)으로 라우팅하고, **Shape B** 는 planner/qa 단계가 거의 빌 때 쓰는 독립 스크립트입니다.  서브에이전트는 대화 기록을 공유하지 않고 — 커밋된 파일(`plan.md` / `MANIFEST.md` / `qa-report.md`)로 핸드오프하므로 각자의 컨텍스트와 비용이 제한됩니다.  역할별 모델 라우팅(planner/resourcer = opus, editor/qa = sonnet)과 비용 방화벽이 런타임 API 토큰을 0으로 유지합니다.  `.claude/agents/` 에는 정의 **19개**(미디어 6 + 게임 13)가 있습니다.  전체 데이터 흐름 맵 + 게임 프로토타입 빌드 체인: [`docs/architecture.md`](docs/architecture.md).

## 자율성 신호 — 주장이 아니라 측정

![2패널 개입 추세 — 패널 A(일별 커밋 귀속)는 일별 커밋 수를 개시자별(에이전트 자율=파랑 vs 사용자 개시=빨강)로 쌓고 사용자 개시 비율선과 일별 비율 라벨을 표시; 패널 B(운영자 관여)는 로컬 Claude Code 세션 JSONL 에서 추출한 일별 운영자 프롬프트와 활성 세션 분을 차트로.  한국어 미러는 docs/metrics/intervention-ko.png.](docs/metrics/intervention-ko.png)

끊임없는 조종이 필요한 멀티 에이전트 시스템은 대체하려던 그 수고를 벗어나지 못한 것입니다.  그래서 `main` 의 모든 커밋을 **사용자 개시** vs **에이전트 자율**로 분류하고, 운영자의 Claude Code 세션 로그에서 프롬프트 수 + 활성 분을 추출합니다 — 목표는 시스템이 더 많은 결정을 흡수하면서 두 패널이 모두 하향하는 것.  분류 휴리스틱 + 감축 분석: [`docs/research/2026-05-22-intervention-reduction.md`](docs/research/2026-05-22-intervention-reduction.md).

## 정직성을 설계에 (Honest by design)

공개해 두는, 문서화된 부정 결과 — 정직한 범위 설정이 나머지 전부가 기대는 신뢰의 근거이기 때문입니다:

- **`product-cf` 는 보류** — 실제 부정 결론 때문입니다.  무료 / 로컬로 "진짜 3D 처럼 만들기"(depth-parallax, 실린더 래핑 턴테이블, 로컬 image-to-video) 접근은 16 GB 머신에서 진짜 CF 품질 기준을 넘지 못했습니다; 설득력 있는 결과엔 유료 클라우드 image-to-video 또는 더 큰 GPU 가 필요합니다.  트리에 게이트 오프 상태로 보존, 결정 보류.
- **셀 셰이딩은 의도적으로 연기** — ffmpeg 의 한계가 어디인지 아는 것이 결과를 가짜로 만드는 것보다 낫습니다.

더 많은 트레이드오프와 알려진 갭: [`docs/known-limitations.md`](docs/known-limitations.md).

<details>
<summary><b>설계 노트 — 흔한 에이전트 데모와 다른 선택들</b></summary>

- **결과 레이어 vs 작업 큐, 분리해서 유지.**  [`docs/goal.md`](docs/goal.md) 는 활성 목표를 구체적 산출물로 담고, [`docs/roadmap.md`](docs/roadmap.md) 는 일 단위 작업 큐를 담습니다.  큐가 비었다 ≠ 목표 달성 — 이 분리는 과거 24시간 동안 큐가 0인 채 인프라 커밋 11개에 산출물 0개가 나왔던 사고 때문에 존재합니다.
- **라이브 알림 표면을 가진 비대역(out-of-band) 감사기.**  [`auditor`](.claude/agents/auditor.md) 서브에이전트가 매일 03:00 에 `launchd` 로 돌며 레포 전체를 읽기 전용으로 훑고, 최신 판정이 non-CLEAN 일 때만 [`docs/audit/CURRENT-ALERT.md`](docs/audit/) 를 씁니다; 다음 세션은 목표를 잡기 전에 이를 읽어야 할 계약 의무가 있습니다.
- **오케스트레이션과 실행 사이의 비용 방화벽.**  Anthropic 토큰은 오케스트레이션(Tier 1)에서만 쓰이고, 미션 실행(transcribe → select → render → QA)은 전부 로컬 도구로 돌아 토큰 0개입니다.
- **상태 점검 프롬프트를 흡수하는 운영자 툴링.**  `scripts/doctor.sh`(Claude 없이 ~2초 헬스 체크), `scripts/statusline.sh`, `scripts/morning-brief.sh` 가 "지금 상태는 / 밤새 무슨 일이?"를 운영자가 타이핑하지 않아도 답합니다.  전체 카탈로그: [`docs/operator-tooling.md`](docs/operator-tooling.md).

전체 운영 계약(12개 하드 룰 + 자율 모드)은 [`docs/operator-contract.md`](docs/operator-contract.md) 와 [`CLAUDE.md`](CLAUDE.md) 에 있습니다.

</details>

<details>
<summary><b>60초 이후의 실행 경로 — 수동 music-video · job-hunt · PawnSim 빌드</b></summary>

**수동 music-video** (클론 이후)
```bash
./scripts/bootstrap.sh                       # 도구 검증, 빠진 항목에 brew/apt 힌트 출력
MUSIC_VIDEO_DEMO_MODE=1 ./agents/missions/music-video/run.sh demo
```
출력은 `records/missions/<date>/music-video-demo-<HHMMSS>/outputs/short.mp4`.  모든 env 변수, 플래그, 쉐이더 카탈로그, 풀 Pexels + 운영자 음악 경로: [`docs/music-video-pipeline-reference.md`](docs/music-video-pipeline-reference.md).

**job-hunt** (네트워크·키 불필요)
```bash
skills/job-hunt/scripts/run.sh --seed "Problem Solver" --dry-run
```
`global-*` 플러그인을 라이브 모드(`JH_GLOBAL_ATS_LIVE=1 …`)로 켜면 키 없이 실제 공고를 받습니다.  소스별 활성화 + Claude 보강 유틸리티 4종: [`docs/skills/job-hunt.ko.md`](docs/skills/job-hunt.ko.md) · 샘플 다이제스트: [`docs/samples/job-hunt-digest-mock.md`](docs/samples/job-hunt-digest-mock.md).

**PawnSim** (Windows + Unity 6000.0.75f1 LTS)
```bash
cd skills/game-prototype
python ../game-dev-agent/scripts/agent.py integrate --project unity-project --method scenes
python ../game-dev-agent/scripts/agent.py integrate --project unity-project --method build --day PLAY
"$(ls -dt builds/day-*/ | head -1)PawnSim.exe"   # 항상 최신 빌드를 동적으로 해석
```
사전 빌드된 `.exe` 는 커밋되지 않습니다 (`builds/` 는 gitignore).  전체 조작 + 플래그: [`skills/game-prototype/README.md`](skills/game-prototype/README.md).

두 미디어 스킬의 전체 레시피 모음: [`EXAMPLES.md`](EXAMPLES.md).

</details>

<details>
<summary><b>셋업 &amp; 플랫폼 — 지원 OS, 사전 요구사항, Claude Code 비용</b></summary>

**플랫폼.**  미디어 파이프라인은 macOS 가 주력·엔드투엔드 테스트 플랫폼이고, PawnSim 빌드 체인은 Windows 주력(Unity batchmode)이며, Linux 는 미션 실행은 되지만 스케줄러에 OS별 적응이 필요합니다.  clone-and-go 는 macOS 에서 검증됨.  Windows 셋업: [`docs/platform-windows.md`](docs/platform-windows.md).

**사전 요구사항.**  macOS 14+ / Linux / Windows 11 · [Claude Code](https://docs.anthropic.com/claude-code)(에이전트 구동 경로에만 필요; 스크립트는 없이도 실행) · Homebrew 또는 `apt` · Apple Silicon 권장(`h264_videotoolbox`, libx264 폴백) · ~3 GB 여유 디스크 · B-roll 용 무료 [Pexels API 키](https://www.pexels.com/api/).  `scripts/bootstrap.sh` 가 모든 도구(`ffmpeg`/`ffprobe`, `whisper.cpp`, `ollama`, `yt-dlp`, `aubio`, `jq`)를 점검하고 빠진 것에 정확한 `brew` / `apt` 설치 명령을 출력해 — 도구 누락이 조용한 실패가 되지 않습니다.

**Claude Code 비용.**  에이전트 구동 경로는 오케스트레이션 중에만 Anthropic 토큰을 쓰고, 미션 스크립트는 독립 실행되어 토큰 **0개**입니다.  보통 운영자 채팅이 미션 자체보다 비용을 더 좌우합니다.  Tier-1 / Tier-2 방화벽(로컬 vs Anthropic): [`docs/cost-model.md`](docs/cost-model.md).

</details>

## 문서

| 영역 | 문서 |
|------|------|
| 엔지니어링 사례 연구 — 9개 인시던트, *문제 → 제약 → 결정 → 산출물* | [`docs/engineering-case-studies.ko.md`](docs/engineering-case-studies.ko.md) |
| 아키텍처 + 전체 데이터 흐름 맵 | [`docs/architecture.md`](docs/architecture.md) |
| music-video 파이프라인 레퍼런스 (쉐이더, 장르, env 변수) | [`docs/music-video-pipeline-reference.md`](docs/music-video-pipeline-reference.md) |
| 알려진 한계 + 부정 결과 | [`docs/known-limitations.md`](docs/known-limitations.md) |
| 비용 모델 — Anthropic vs 로컬 | [`docs/cost-model.md`](docs/cost-model.md) |
| 플랫폼 / Windows 셋업 | [`docs/platform-windows.md`](docs/platform-windows.md) |
| 운영 계약 — 자율 규칙 | [`docs/operator-contract.md`](docs/operator-contract.md) |
| 파일럿 결정 로그 | [`docs/pilots/decision-log.md`](docs/pilots/decision-log.md) |

읽기 전용 리뷰를 하시나요? [`docs/for-analysts.md`](docs/for-analysts.md) 에서 시작하세요 — 1차 진단에 최적화된 단일 진입점입니다.

## 코드 / 데이터 분리

| 레이어 | 경로 | 추적 |
|--------|------|------|
| 코드 (로직) | `.claude/agents/`, `agents/`, `config/`, `scripts/` | ✓ |
| 스킬 (agentskills.io 스펙 패키지) | `skills/<name>/` | ✓ |
| 데이터 (출력) | `records/missions/<date>/<id>/` | ✗ (gitignore) |
| 시크릿 | `.env` | ✗ (gitignore) |

레포에는 에이전트 시스템 자체만 들어 있습니다 — 미션 출력(비디오, 트랜스크립트, 생성 에셋)은 `records/` 아래 로컬에 남습니다.  GitHub 에 보이는 것은 시스템의 산출물이 아니라 시스템 자체의 진화입니다.

## 라이선스

MIT. [`LICENSE`](LICENSE) 참조.
