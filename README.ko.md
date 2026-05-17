<div align="center">

# MelonS-Agents

**한국어** | [English](./README.md)

**토픽 프롬프트 → 60초 9:16 세로 쇼츠.**

**기계적인 단계는 로컬, 창작 단계는 Claude.**  세 가지 감사 트리거 — 커밋·이상·스케줄 — 으로 시스템이 자신의 드리프트를 스스로 잡습니다.  영어 + 한국어 듀얼 트랙.

`미션 32회 · 런타임 API 토큰 0개 · 감사 레이어 3개 · v6 scorecard 44 / 50 · MIT`

![AI-Powered](https://img.shields.io/badge/AI--Powered-FF6B35?style=for-the-badge&logo=anthropic&logoColor=white)
![Self-Evolving](https://img.shields.io/badge/Self--Evolving-8B5CF6?style=for-the-badge&logo=git&logoColor=white)
![Autonomous](https://img.shields.io/badge/Autonomous-10B981?style=for-the-badge&logo=robotframework&logoColor=white)

![macOS](https://img.shields.io/badge/macOS-000000?style=for-the-badge&logo=apple&logoColor=white)
![Shell](https://img.shields.io/badge/Shell-4EAA25?style=for-the-badge&logo=gnu-bash&logoColor=white)
![FFmpeg](https://img.shields.io/badge/FFmpeg-007808?style=for-the-badge&logo=ffmpeg&logoColor=white)
![Ollama](https://img.shields.io/badge/Ollama-000000?style=for-the-badge&logo=ollama&logoColor=white)
![Claude](https://img.shields.io/badge/Claude-D97757?style=for-the-badge&logo=anthropic&logoColor=white)

![GitHub stars](https://img.shields.io/github/stars/MelonS/MelonS-Agents?style=for-the-badge)
![GitHub forks](https://img.shields.io/github/forks/MelonS/MelonS-Agents?style=for-the-badge)
![GitHub last commit](https://img.shields.io/github/last-commit/MelonS/MelonS-Agents?style=for-the-badge)
![License](https://img.shields.io/github/license/MelonS/MelonS-Agents?style=for-the-badge)

![faceless-short v6 파이프라인 출력의 5초 애니메이션 프리뷰 — 히타이트 토픽, 9:16 세로 쇼츠, 역사 전투 재연 B-roll 위에 영어 자막 "Scholars called the Hittites fiction" 번인, 좌측 상단 Pexels 라이선스 표기](docs/demo/v6-hittites-preview.gif)

</div>

## 개요

> macOS 기반 멀티 에이전트 시스템입니다.  **현재 초점** — 위 데모에
> 보이는 — 은 faceless 숏폼 영상 생성.  **하지만 시스템 자체는
> 숏폼 전용이 아닙니다.**  스캐폴드 — orchestrator + 4개 미션
> 서브에이전트 + 파일 기반 핸드오프 + 3-layer 반응형 감사 +
> Tier-1/Tier-2 비용 라우팅 — 은 범용으로 설계되었으며, 숏폼
> 영상은 *시각적으로 검증 가능한 구체적 산출물*에 대해 아키텍처를
> 시험해 본 v1 미션 타입일 뿐입니다.  추가 미션 타입 (리서치
> 워크플로우, 다단계 데이터 파이프라인, 운영자가 다음으로 집어 올
> 자동화 작업 등) 은 프로젝트가 성숙하면서 같은 스캐폴드 위에
> 얹힐 예정입니다.
>
> 단 하나의 원칙 위에 만들어졌습니다 — **제작 파이프라인을
> 자동화하고, 시스템이 자신의 로직을 스스로 진화시키게 한다.**
> 이 저장소의 모든 커밋은 그 진화의 한 단계입니다.  히스토리는
> 산출물의 기록이 아니라, 에이전트 시스템 자체가 성장해 온
> 궤적입니다.

## 설계 노트

일반적인 에이전트 데모와 차별화되는 설계 선택들:

- **목표 계층과 작업 큐의 분리.** [`docs/goal.md`](docs/goal.md)는
  활성 목표를 구체적 산출물로 정의; [`docs/roadmap.md`](docs/roadmap.md)는
  일별 작업 큐. 큐가 비었다고 목표가 달성된 것은 **아님** — 목표의
  "Done when" 조건만이 달성을 정의함. 분리 이유: 이전 24시간 구간이
  인프라 커밋 11개를 쌓는 동안 큐는 0건이었고 실제 산출물도 0건이었던
  사고를 다시 막기 위함.
- **운영 계약은 커밋된 단일 출처.**
  [`docs/operator-contract.md`](docs/operator-contract.md) — 12개
  하드 룰 + 컨벤션. 에이전트의 로컬 메모리는 이 파일을 가리키는
  빠른 캐시; 두 곳이 어긋나면 이 파일이 이김.
- **오케스트레이션과 실행 사이의 비용 방화벽.** Anthropic API 토큰은
  Tier 1 오케스트레이션에서만 소비. 미션 실행(전사 → 선택 → 렌더
  → QA)은 `whisper.cpp` + `ollama` + `ffmpeg` 로컬 실행이며
  토큰 비용 0. [`docs/cost-model.md`](docs/cost-model.md) 참조.
- **별도 트랙 auditor + 능동 알림 surface.**
  [`auditor`](.claude/agents/auditor.md) 서브에이전트는 launchd로
  매일 03:00 발화, 저장소 전체를 읽기 전용 순회, 안정 채널에 기록:
  [`docs/audit/CURRENT-ALERT.md`](docs/audit/)는 최근 verdict이
  비-CLEAN일 때만 존재 — 다음 인터랙티브 세션은 목표를 잡기 전
  이 파일을 의무적으로 읽음.
- **파일 기반 서브에이전트 핸드오프.** 서브에이전트들은 대화
  히스토리를 공유하지 않음. 커밋되는 파일(`plan.md` / `MANIFEST.md`
  / `qa-report.md`)을 통해 통신. 각 서브에이전트의 컨텍스트는 자신의
  프롬프트 + 자신이 읽는 매니페스트로 한정됨 — 예측 가능한 토큰
  비용, 예측 가능한 실패 모드.

## 샘플 출력

지금까지 4가지 미션 타입에 걸쳐 32건의 출력이 생성되었습니다.
프로젝트의 최근 포커스는 `faceless-short` 미션이며 (아래 쇼케이스),
v1 파이프라인 출력 (단일-클립 highlight + shorts-batch)은 기준점
참고용으로 그 아래에 유지됩니다.

### Faceless 파일럿 (영어 + 한국어 A/B)

`faceless-short` 미션은 토픽 프롬프트만으로 60초 완성본을 산출합니다 — 입력 영상 없음.  파이프라인: ollama가 내레이션 스크립트 초안 → Kokoro-ONNX (`am_michael`, 한국어는 macOS `Yuna`) 음성 합성 → whisper.cpp 타이밍 전사 → 스크립트 정합 캡션 교정 (고유명사를 원본 스크립트 텍스트로 복원) → SRT 큐를 자연 구두점에서 단일 라인으로 분할 (모바일 2줄 박스 오버랩 차단) → ollama가 내레이션 시간 윈도우(8개) 마다 Pexels 검색어 1개씩 추출 → Pexels Videos API에서 윈도우당 B-roll 1개 수집 → ffmpeg가 각 클립을 윈도우 길이로 트림·9:16 풀화면 크롭·libass 자막 번인·출처 오버레이까지 완성.

같은 토픽을 두 가지 언어 버전으로 렌더해 음성+자막 차이를 나란히 비교:

| | 히타이트 (역사 × 성경) | 수소 (과학) |
|---|---|---|
| EN | ![히타이트 EN — 9:16 풀화면, 'and siege warfare.' 단일 라인 영어 자막이 하투샤 고고학 항공 샷 위에 올라간 상태](docs/pilots/screens/hittites-en-caption-verify.jpg) | ![수소 EN — 9:16 풀화면, 'The human body's reliance' 단일 라인 영어 자막이 파스타 매크로 B-roll 위에](docs/pilots/screens/hydrogen-en-caption-verify.jpg) |
| KO | ![히타이트 KO — '도시의 모습이 드러났습니다.' 단일 라인 한국어 자막이 하투샤 고고학 항공 샷 위에, AppleGothic, macOS Yuna 음성](docs/pilots/screens/hittites-ko-caption-verify.jpg) | ![수소 KO — '평균적으로 사람 몸무게의' 단일 라인 한국어 자막이 올리브 오일 방울 매크로 위에, Yuna 음성](docs/pilots/screens/hydrogen-ko-caption-verify.jpg) |

각 언어 버전은 자기 캡션에서 윈도우당 Pexels 검색어를 *자체적으로* 추출 — 그래서 EN과 KO는 스크립트 구조는 공유하지만 동일한 클립을 항상 쓰지는 않습니다 (v3/v4 설계: 윈도우별 키워드로 내레이션 비트와 정렬 우선).  "동일 영상, 음성만 교체" 비교가 필요하면 `FACELESS_REUSE_BROLL=<en_mission_dir>`로 KO 렌더가 EN의 이어붙인 B-roll을 강제 재사용하게 할 수 있습니다.

A/B 제작 노트, 플랫폼별 업로드 메타데이터, 다음 10개 토픽 큐는 모두 [`docs/pilots/`](docs/pilots/) 아래에 있습니다.  파일럿당 한계 비용: **$0** (Pexels 무료 티어, 그 외 단계는 모두 로컬).

### v1 파이프라인 (단일 클립 highlight / shorts-batch)

원조 v1 미션 — `highlight`, `summarize`, `shorts-batch` — 은 실제 소스 URL (예: Creative Commons 영상)을 받아 9:16 출력을 만들면서 출처 워터마크 + 자막을 번인합니다.  `faceless-short` 이전의 설계이며, 토픽이 아니라 영상에서 *부분 발췌*가 필요할 때 여전히 활용됩니다.

![highlight-015213의 6초 애니메이션 프리뷰 — 9:16 letterbox-blur 레이아웃, 좌측 상단 출처 오버레이, 하단 libass 자막 번인이 보임](docs/demo/highlight-015213-preview.gif)

`highlight-015213/outputs/short.mp4`의 6초 발췌 — Sintel 트레일러 (CC-BY-3.0, © Blender Foundation), 39초 9:16 워터마크 + 자막.  전체 mp4는 `records/` 아래에 (gitignored); 위 GIF는 크기 최적화 발췌 (가로 360 px, 12 fps, ≈ 2.8 MB) — ffmpeg + palette dither로 생성하여 `docs/demo/`에 v1 파이프라인의 영구 증거로 유지.

| 단일 하이라이트 | 숏츠 배치 |
|----------------|----------|
| ![Sintel 단일 하이라이트, 자막 번인과 좌측 상단 출처 오버레이가 적용된 9:16 숏](docs/caption-verify/highlight-015213-sintel-cap.jpg) | ![Sintel 숏츠 배치 첫 번째 컷, 자막 번인 9:16 숏](docs/caption-verify/shorts-batch-024840-short-01-cap.jpg) |
| `highlight-015213` · 39 초 · 첫 시도 PASS | `shorts-batch-024840 / short-01` · 44 초 · 첫 시도 PASS |

둘 다 *Sintel* 트레일러 (CC-BY-3.0, © Blender Foundation — `durian.blender.org`)에서 추출.  공통 요소: 좌측 상단 출처 어트리뷰션 오버레이, 9:16 letterbox-blur 배경, 하단 safe-zone 박스 안의 libass 번인 자막.

### 최근 미션

| 미션 | 타입 | 소스 | 출력 | 총 소요 | QA |
|------|------|------|------|---------|----|
| `faceless-hittites-032538` | faceless-short | 토픽 프롬프트 + Pexels B-roll (8 windows) | 62.7 초 9:16 숏 (49 MB) | ~75 초 | 첫 시도 PASS |
| `faceless-hittites-ko-032653` | faceless-short | 한국어 스크립트 + Yuna 음성 | 60.3 초 9:16 숏 (35 MB) | ~49 초 | 첫 시도 PASS |
| `faceless-hydrogen-032742` | faceless-short | 토픽 프롬프트 + Pexels B-roll | 59.7 초 9:16 숏 (21 MB) | ~64 초 | 첫 시도 PASS |
| `faceless-hydrogen-ko-032846` | faceless-short | 한국어 스크립트 + Yuna 음성 | 38.9 초 9:16 숏 (14 MB) | ~33 초 | 첫 시도 PASS |
| `highlight-032405` | highlight | 한국어 CC-BY-3.0 인터뷰 클립 | 60 초 9:16 숏 | — | 첫 시도 PASS |
| `summarize-025121` | summarize | Sintel 1080p · Blender CC-BY-3.0 | EN + KO `summary.md` (551 B) | — | 첫 시도 PASS |
| `highlight-203219` | highlight | 초기 개발 fixture (2026-05-15) | 30 초 숏 | 73.2 초 | **FAIL** — QA 게이트, 재시도 소진 (블로커 파일 생성됨) |

FAIL 행은 보존: QA 게이트는 형식 검사가 아닙니다.  `QA_RETRY_MAX` 소진 시 `records/blockers/<date>/<mission-id>.md`에 블로커 파일이 기록되고 미션이 정지합니다.  전체 미션 원장: [`docs/metrics-dashboard.md`](docs/metrics-dashboard.md).

### 파일럿 점수표 — 어느 버전에서 무엇이 좋아졌나

운영자 질문: *"썸네일만으로는 뭐가 좋아지는지 안 보인다."*
정직한 답은 쇼트폼 시청 유지율에 매핑되는 다섯 가지 차원에 걸친
구조화된 자체 평가입니다.

![누적 가로 막대 차트, 파일럿 점수표 — Hittites EN v4 26/50, v5 32/50, v6 44/50, Hydrogen EN v5 28/50, v6 43/50; 막대당 다섯 색 세그먼트 (후크, 영상-자막 매칭, 가독성, 사실 일관성, 마감)](docs/metrics/scorecard.png)

v5 → v6 상승폭 (단일 라인 자막은 v5에서 이미 적용 완료, v6는
스크립트 생성 단계만 로컬 `llama3.2:3b`에서 Claude Sonnet으로
교체)은 Hittites EN에서 +12점, Hydrogen EN에서 +15점.  v5→v6
델타 대부분이 **후크**와 **사실 일관성** 차원 — 운영자가 v5에서
지적한 바로 그 차원입니다 ("초반 5초에 시선 끌만한 게 없음",
"10%인지 60%인지 헷갈리네").

투명성 고지: 점수는 시청자 패널이 아니라 Claude가 매긴 자체
평가입니다.  실제 플랫폼 시청 시간 데이터가 들어오기 전까지의
구조화된 진행 신호로만 사용합니다.  버전별 상세 + 추론 + 차원
정의: [`docs/pilots/scorecard.md`](docs/pilots/scorecard.md).
원본 데이터: [`docs/pilots/scorecard.json`](docs/pilots/scorecard.json).
JSON 수정 후 차트 재생성:
`.venv/bin/python scripts/generate-scorecard-chart.py`.

### 운영자 개입 추이 — 시스템이 점점 자율적이 되고 있는가?

사람이 계속 키 결정을 해줘야 하는 멀티에이전트 시스템은 사실
대체하려던 노동을 그대로 옮긴 것뿐입니다.  이 차트는 운영자가
명시적으로 지시한 커밋 비율과 에이전트가 로드맵에서 스스로 집어
온 커밋 비율을 매일 추적합니다.

![2026-05-14부터 2026-05-17까지의 일별 커밋 수 누적 막대 차트, 시작 주체별 분할 (파랑 = 에이전트 자율, 빨강 = 사용자 시작), 오른쪽 축에 사용자 시작 비율 검은 라인](docs/metrics/intervention.png)

솔직한 읽기: 라인이 아직 **원하는 방향**으로 가지 않고 있습니다.
05-14 (사용자 0%)는 셋업 자동화.  05-15~16은 에이전트가 로드맵을
스스로 굴린 야간 인프라 작업.  05-17 (42%)은 niche 결정 날이라
운영자가 의도적으로 루프 안에 있었던 — 토픽 고르기, 모델 고르기,
품질 갭 지적.  이런 *결정 페이즈*의 높은 개입은 정상.  목표는
*생산 페이즈* 진입 후 (니치 확정, 정기 토픽 큐, 스케줄 업로드)
라인을 15% 아래로 내리는 것.

`Requested-by: user` 커밋 푸터 컨벤션은 2026-05-17 `7c6ff4f`에서
도입됨 (차트의 점선) — 분류기에게 strict한 시그널을 향후로 줍니다.
이전 커밋은 본문 텍스트 휴리스틱 매칭에 의존 → 과거 데이터의
"사용자 시작" 리콜은 실제보다 낮을 가능성.

원본 데이터 + 커밋별 분류: [`docs/metrics/intervention.json`](docs/metrics/intervention.json).  차트 재생성: `.venv/bin/python scripts/generate-intervention-chart.py`.

### 미션별 타이밍 (v1 highlight 미션 한정)

![미션별 시간 분해 — v1 highlight 미션, 단계(전사 + 선택 + 렌더 + 기타)별 스택 막대](docs/metrics/per-mission-time.png)

![처리량 — 총 컴퓨트 시간 1초당 생성된 출력 초, v1 highlight 미션별 막대](docs/metrics/throughput-realtime.png)

> **범위 안내**: 위 차트는 **`highlight-*` 미션만** 포함합니다 — `metrics.json`이 단계별 `stages_s` 분해를 포함하는 유일한 미션 타입이기 때문.  `faceless-short` 미션 타이밍은 미션별 `metrics.json`에 단일 `total_s`로 기록되며 (파이프라인이 단일 bash 스크립트 → 단계 분해 없음) 위 차트에 표시되지 않습니다.  차트 생성기 v2가 두 종류를 통합할 예정입니다.  새 미션이 추가된 후 차트 재생성: [`scripts/setup-venv.sh`](scripts/setup-venv.sh)을 한 번 실행 (`.venv/` 생성 + matplotlib 설치) → `.venv/bin/python scripts/generate-charts.py`.

위 그래프의 모든 초는 로컬 CPU / GPU 시간 — 전사는 `whisper.cpp`, 선택은 `ollama` (`llama3.2:3b`), 렌더는 `ffmpeg`. **위 단계 동안 소비된 Anthropic API 토큰: 0.** Tier 1 / Tier 2 비용 방화벽은 [`docs/cost-model.md`](docs/cost-model.md) 참조.

## 분석가/리뷰어를 위한 안내

이 저장소에 대한 읽기 전용 분석을 시작한다면
[`docs/for-analysts.md`](docs/for-analysts.md)부터 보세요 — 1차
진단 정확도를 위한 단일 진입점입니다. [`docs/cost-model.md`](docs/cost-model.md)
(Anthropic 대 로컬 비용 구분)과 [`docs/architecture.md`](docs/architecture.md)
(전체 데이터 흐름)과 함께 보면 됩니다.

## 아키텍처

```
              ┌───────────────────┐
              │   Orchestrator    │   model: opus
              └─────────┬─────────┘
                        │ 미션을 순서대로 위임
                        ▼
              ┌───────────────────┐
              │      Planner      │   model: sonnet
              └─────────┬─────────┘
                        ▼
              ┌───────────────────┐
              │     Resourcer     │   model: sonnet
              └─────────┬─────────┘
                        ▼
              ┌───────────────────┐
              │       Editor      │   model: sonnet
              └─────────┬─────────┘
                        ▼
              ┌───────────────────┐
              │         QA        │   model: sonnet
              └───────────────────┘

              ┌───────────────────┐
              │      Auditor      │   model: sonnet  (별도 트랙)
              └───────────────────┘   read-only, 매일 03:00
                                       launchd 발화
```

| 에이전트 | 책임 | 산출물 |
|----------|------|--------|
| 🤖 **Orchestrator** (opus) | 미션 분해, 위임, 최종 통합 | 태스크 리스트 · `summary.md` |
| 🧠 **Planner** (sonnet) | 전략 수립, 작업 분해, 수락 기준 정의 | `plan.md` |
| 📦 **Resourcer** (sonnet) | 자산 수집, 외부 도구 실행 (ffmpeg / yt-dlp / whisper) | `resources/MANIFEST.md` |
| 🎞️ **Editor** (sonnet) | 출력 렌더링, 산출물 조립 | `outputs/CHANGELOG.md` |
| ✅ **QA** (sonnet) | 계획 기준 대비 검증, 회귀 감지 | `qa-report.md` |
| 🔍 **Auditor** (sonnet) | 저장소 전체 drift / contract / cost / security 감사 (별도 트랙, 매일 03:00) | `docs/audit/<date>-<focus>.md` + 비-CLEAN 시 `docs/audit/CURRENT-ALERT.md` |

서브 에이전트 정의: [`.claude/agents/`](.claude/agents/) · 미션 템플릿과 공용 셸 라이브러리: [`agents/`](agents/)

## 코드 / 데이터 분리

| 계층 | 경로 | 추적 여부 |
|------|------|-----------|
| 코드 (로직) | `.claude/agents/`, `agents/`, `config/`, `scripts/` | ✓ |
| 데이터 (산출물) | `records/missions/<date>/<id>/` | ✗ (gitignore) |
| 시크릿 | `.env` | ✗ (gitignore) |

저장소는 에이전트 시스템 자체만 보관합니다. 미션 산출물 — 영상,
전사, 생성된 자산 — 은 모두 로컬 `records/`에만 남습니다. GitHub에
드러나는 것은 산출물이 아니라 시스템의 진화 과정입니다.

## 플랫폼 지원

| 영역 | macOS 14+ | Linux |
|------|-----------|-------|
| 미션 실행 (전사 → 선택 → 렌더 → QA) | ✓ | ✓ (`ffmpeg` / `whisper.cpp` / `ollama` 모두 사용 가능) |
| 하드웨어 가속 렌더 (`h264_videotoolbox`) | ✓ Apple Silicon | n/a — `-allow_sw 1`로 libx264 폴백 |
| `bootstrap.sh` 합성 fixture (macOS `say`-기반 TTS) | ✓ | 스킵 — `scripts/fetch-fixtures.sh`로 실제 CC fixture 사용 |
| `launchd` 스케줄러 (야간 자동 실행, 일일 감사) | ✓ | systemd timers 또는 cron으로 대체 — `scripts/com.melons.agents.*.plist` 일정을 참고 |

macOS가 **주 검증 플랫폼** (엔드투엔드 테스트 완료). Linux는 미션
실행에는 동작하지만 스케줄러와 합성 fixture 생성은 OS별 적응이
필요합니다. 크로스 플랫폼 CI는 아직 없고, clone-and-go 흐름은
Darwin에서만 검증되어 있음.

모든 도구 경로와 엔드포인트는 환경 변수로 관리됩니다 —
`agents/lib/env.sh`가 빈 `*_BIN` 변수를 `command -v`로 자동 해석하므로
PATH에 도구가 설치되어 있으면 충분. 필요할 때만 `.env`에서 override.

## 자율 모드

[`config/policies.yaml`](config/policies.yaml)에 정의됩니다.

| 모드 | 플래그 | 동작 |
|------|--------|------|
| ⚙️ **Interactive** (기본값) | `AUTONOMY_MODE=false` | 로직 변경·파괴적 작업·외부 게시 전에 사용자 확인을 받습니다. |
| 🌙 **Autonomous** | `AUTONOMY_MODE=true` | `AUTONOMY_BUDGET_USD` 범위 안에서 무인 실행. 로직 파일(`agents/`, `.claude/agents/`)은 불변입니다. |

## 미션 흐름

1. 사용자가 미션을 지시합니다.
2. `orchestrator`가 `records/missions/<date>/<id>/`와 태스크 리스트를 생성합니다.
3. `planner` → 수락 기준이 포함된 `plan.md`.
4. `resourcer` → 자산과 `resources/MANIFEST.md`.
5. `editor` → 산출물과 `outputs/CHANGELOG.md`.
6. `qa` → 항목별 PASS / FAIL이 적힌 `qa-report.md`.
7. PASS 시 `orchestrator`가 `summary.md`를 작성합니다.

## 툴체인

`ffmpeg` (libass 포함 빌드 — macOS는 `brew install ffmpeg-full`,
Linux는 `apt install ffmpeg`) · `yt-dlp` · `whisper.cpp` (`small`,
다국어) · `ollama` (`llama3.2:3b`) · `Kokoro-ONNX` (TTS, Apache 2.0 —
faceless-short 내레이션) · macOS `say` (한국어 + fallback 음성) ·
Pexels Videos API (무료 티어 — faceless-short B-roll) · 오케스트레이션용
Claude API.

## 사전 요구사항

- **macOS 14+** (주 검증 플랫폼) 또는 **Linux** (best-effort —
  위 [플랫폼 지원](#플랫폼-지원) 참조)
- macOS는 **Homebrew**, Linux는 `apt` / `pacman` / 동등 패키지 매니저
- **Apple Silicon 권장** — 렌더 가속에 `h264_videotoolbox` 사용,
  `-allow_sw 1`로 Intel / Linux에서 libx264 자동 폴백
- **여유 디스크 ~3 GB** — whisper.cpp `small` 모델 (~150 MB),
  Sintel CC-BY-3.0 트레일러 fixture, `bootstrap.sh`의 합성 fixture 2개
- **도구**: `ffmpeg` (libass 포함 빌드), `ffprobe`, `whisper.cpp`,
  `ollama`, `yt-dlp`. `scripts/bootstrap.sh`가 모두 점검하고 누락된
  도구별로 OS에 맞는 `brew install …` / `apt install …` 명령을 정확히
  출력 — 도구 누락이 침묵 실패로 끝나지 않음.

## 빠른 시작

```bash
# 클론 — 두 방식 모두 가능
git clone https://github.com/MelonS/MelonS-Agents.git    # HTTPS
# git clone git@github.com:MelonS/MelonS-Agents.git      # SSH
cd MelonS-Agents

# 부트스트랩: .env.example을 .env로 복사, 도구 점검, whisper 모델
# (~150 MB) + ollama 하이라이트 모델 (llama3.2:3b) 자동 다운로드,
# macOS 전용 합성 fixture 2개 생성.
./scripts/bootstrap.sh

# Sintel 트레일러 (CC-BY-3.0)로 9:16 숏 생성
./agents/missions/highlight/run.sh https://download.blender.org/durian/trailer/sintel_trailer-1080p.mp4
```

미션은 산출물을
`records/missions/<date>/highlight-<HHMMSS>/outputs/short.mp4`에
저장 (gitignore — 산출물은 본인 머신에만 남고 GitHub에는 에이전트
시스템 자체만 올라감).

여러 소스를 한 번에 처리:

```bash
./scripts/batch-mission.sh -f sources.txt
```

큐 기반 자율 실행 (launchd 스케줄러가 사용):

```bash
echo 'https://example.com/long.mp4' >> records/queue/pending.txt
./scripts/mission-queue.sh
```

야간 스케줄러 설치:

```bash
./scripts/install-scheduler.sh install
```

## 운영 계약

이 저장소는 전적으로 에이전트가 운영합니다. 일상 규칙:

- **에이전트가 모든 작업을 수행** — 설치, 편집, 설정, 커밋, 푸시, 스케줄링. 사용자는 터미널에서 명령을 실행하지 않습니다.
- 사용자는 **에이전트가 하드 가드레일에 막힐 때만** 개입 (예: 본인 권한 자체 수정, `main`에 강제 푸시) — 그 경우에도 클릭 한 번의 승인만, 절대 다단계 레시피 따라하기 아님.
- **현재 활성 목표**는 [`docs/roadmap.md`](docs/roadmap.md)에 있습니다. 아래의 "상태" 목록은 평면적 기능 원장 — TODO 리스트로 읽지 마세요. 로드맵의 *Now* 섹션이 "다음에 무엇을 할지"의 단일 출처입니다.
- **결제 방화벽**: 유료 API, SaaS 구독, 클라우드 리소스 생성은 사용자의 명시적 확인이 필요. 로컬 자원(Ollama, FFmpeg, whisper.cpp, brew)은 완전 자율.

전체 계약: [`CLAUDE.md`](CLAUDE.md) 및 [`config/policies.yaml`](config/policies.yaml) 자율 모드 규칙 참조.

## 상태

<!-- status:start -->
- [x] 계층형 에이전트 구조 (오케스트레이터 + 미션 서브 에이전트 4종 + 읽기 전용 auditor 1종)
- [x] 코드 / 데이터 분리 강제 (records/ gitignore)
- [x] 환경 변수 기반 도구 경로 (.env / .env.example)
- [x] PoC 엔드투엔드: 하이라이트 추출 (EN + KO)
- [x] libass 자막 번인 (`agents/lib/env.sh`가 libass 포함 ffmpeg 자동 감지; macOS에서는 `ffmpeg-full` keg로 폴백)
- [x] 다국어 whisper.cpp (small) + 언어 인식 하이라이트 프롬프트
- [x] 배치 실행기 (scripts/batch-mission.sh)
- [x] 로직 변경 시 origin/main 자동 커밋 + 자동 푸시
- [x] 자율 모드용 야간 launchd 스케줄러
- [x] 4종 미션 운영 가능: highlight, summarize, shorts-batch, faceless-short
- [x] Faceless-short 파이프라인 — 토픽 프롬프트 → ollama 스크립트 → Kokoro-ONNX TTS (Apache 2.0) → whisper.cpp 타이밍 + 스크립트 정합 캡션 교정 → Pexels B-roll → 9:16 풀화면 렌더; 한국어는 macOS Yuna + AppleGothic.  파일럿 증거: [`docs/pilots/`](docs/pilots/)
- [x] 단일 패스 ffmpeg 렌더링 (~3× 렌더 속도 향상)
- [x] 이중 언어 요약 미션 (전사 → 구조화된 EN+KO 요약)
- [x] 미션별 비용 / 실행 시간 메트릭
- [x] 실 CC 라이선스 fixture (Blender 오픈 무비) + 다운로더
- [x] 표준 9:16 레이아웃 엔진 — safe zone 마진, 반투명 자막 박스, 상단 좌측 출처 오버레이
- [x] 3종 미션 전반에 source-attribution 와이어링 (`outputs/SOURCES.txt` + 번인 워터마크 + `summary.md` 푸터)
- [x] QA 피드백 재시도 루프 (실패 미션 `QA_RETRY_MAX`까지 자동 재시도, 이후 `records/blockers/`로)
- [x] 저작권 필터 v1 — 도메인 허용목록, 게시 게이트, 스트라이크 로그, 스트라이크 인지 거부
- [x] License-string probe — archive.org + commons.wikimedia.org
- [x] 일별 로드맵 [`docs/roadmap.md`](docs/roadmap.md) ("다음에 무엇을" 단일 출처)
- [x] 플랫폼별 재이용 규칙 — `scripts/publish-gate.sh` (`internal-demo` / `public` / `youtube` / `instagram` / `tiktok`, 4개 `publish_rules` 필드 모두 소비)
- [x] 저장소 auditor 서브에이전트 + 능동 surface (`docs/audit/CURRENT-ALERT.md` 자동 유지, `scripts/audit-run.sh`)
- [x] **반응형 auditor — L1**: git post-commit 훅 (`scripts/hooks/post-commit.sh`)이 드리프트 위험 경로(`.claude/agents/`, `agents/`, `config/`, `CLAUDE.md`, `docs/operator-contract.md`, `scripts/audit-run.sh`, `.claude/settings.json`)를 건드린 커밋 직후 `audit-run.sh contract`을 백그라운드 실행. `scripts/install-hooks.sh install`로 설치.
- [x] **반응형 auditor — L2**: 15분 간격 미션 이상 폴 (`scripts/audit-poll.sh` via `com.melons.agents.audit-poll.plist`)이 새 블로커 + QA-FAIL 클러스터를 감지하면 포커스된 audit 발화; 정상 시 no-op로 저렴. `scripts/install-scheduler.sh install audit-poll`로 설치.
- [x] Clone-and-go 재현성 — 호스트 비종속 `.env.example`, OS 인식 `scripts/bootstrap.sh` (설치 명령 안내), `scripts/fetch-whisper-model.sh` 모델 자동 다운로드, `scripts/test-fresh-clone.sh` 시뮬레이터 + PASS 증거 [`docs/onboarding/fresh-clone-log.txt`](docs/onboarding/fresh-clone-log.txt)
- [x] 미션별 메트릭 차트 (v1 highlight 미션 한정) — [`docs/metrics/per-mission-time.png`](docs/metrics/per-mission-time.png) + [`docs/metrics/throughput-realtime.png`](docs/metrics/throughput-realtime.png), `.venv/bin/python scripts/generate-charts.py`로 재생성 (venv는 `scripts/setup-venv.sh`)
- [x] **단일 라인 자막 강제** — `scripts/split-long-captions.py`가 캡션 교정과 ASS 렌더 사이에 실행, 28자 초과 큐를 자연 구두점에서 분할. 모바일 2줄 박스 오버랩 차단.
- [ ] 실제 사용자 URL fixture — _차단, 사용자 URL 대기 중_
- [ ] 다른 호스트용 License probe 추가 (Vimeo CC 채널 등) — _보류, Vimeo는 item별 license endpoint 없음; 필요 시 재검토_
- [ ] Audio fingerprint 체크 (chromaprint / `fpcalc`) — _보류, 비교용 fingerprint dataset 없음; 첫 takedown 이후 재검토_
- [ ] 소스 프레임 로고 / 워터마크 검출 — _보류, OCR 또는 학습 모델 필요; 실패 모드 관측 시 재검토_
- [ ] editor 내부 반복 QA-피드백 루프 (transcribe/select 재실행 없이 단일 출력 재컷) — _파킹, coarse retry가 compute 낭비할 때만 유용; 아직 미관측_
<!-- status:end -->

> 위 미체크 항목은 **모두 의도된 보류**입니다 — 각 항목에 사유가 인라인으로 적혀 있습니다. 일별 우선순위 큐는 여기가 아니라 [`docs/roadmap.md`](docs/roadmap.md)에 있습니다.

## 라이선스

MIT. [`LICENSE`](LICENSE) 파일을 참고하세요.
