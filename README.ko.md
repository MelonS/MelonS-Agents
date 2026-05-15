<div align="center">

# MelonS-Agents

**한국어** | [English](./README.md)

![AI-Powered](https://img.shields.io/badge/AI--Powered-FF6B35?style=for-the-badge&logo=anthropic&logoColor=white)
![Self-Evolving](https://img.shields.io/badge/Self--Evolving-8B5CF6?style=for-the-badge&logo=git&logoColor=white)
![Autonomous](https://img.shields.io/badge/Autonomous-10B981?style=for-the-badge&logo=robotframework&logoColor=white)

![macOS](https://img.shields.io/badge/macOS-000000?style=for-the-badge&logo=apple&logoColor=white)
![Shell](https://img.shields.io/badge/Shell-4EAA25?style=for-the-badge&logo=gnu-bash&logoColor=white)
![FFmpeg](https://img.shields.io/badge/FFmpeg-007808?style=for-the-badge&logo=ffmpeg&logoColor=white)
![Ollama](https://img.shields.io/badge/Ollama-000000?style=for-the-badge&logo=ollama&logoColor=white)
![Claude](https://img.shields.io/badge/Claude-D97757?style=for-the-badge&logo=anthropic&logoColor=white)

</div>

## 개요

> 숏폼 영상 제작을 위한 효율적인 macOS 기반 멀티 에이전트 시스템.
> 단 하나의 원칙 위에 만들어졌습니다 — **제작 파이프라인을 자동화하고,
> 시스템이 자신의 로직을 스스로 진화시키게 한다.** 이 저장소의 모든
> 커밋은 그 진화의 한 단계입니다. 히스토리는 산출물의 기록이 아니라,
> 에이전트 시스템 자체가 성장해 온 궤적입니다.

## 아키텍처

```
                       ┌───────────────────┐
                       │   Orchestrator    │
                       │      (opus)       │
                       └────────┬──────────┘
                                │ 위임
            ┌──────────┬────────┼────────┬──────────┐
            ▼          ▼        ▼        ▼          ▼
       ┌─────────┐┌─────────┐┌─────────┐┌──────────┐
       │ Planner ││Resourcer││ Editor  ││    QA    │
       └─────────┘└─────────┘└─────────┘└──────────┘
```

| 에이전트 | 책임 | 산출물 |
|----------|------|--------|
| 🤖 **Orchestrator** (opus) | 미션 분해, 위임, 최종 통합 | 태스크 리스트 · `summary.md` |
| 🧠 **Planner** | 전략 수립, 작업 분해, 수락 기준 정의 | `plan.md` |
| 📦 **Resourcer** | 자산 수집, 외부 도구 실행 (ffmpeg / yt-dlp / whisper) | `resources/MANIFEST.md` |
| 🎞️ **Editor** | 출력 렌더링, 산출물 조립 | `outputs/CHANGELOG.md` |
| ✅ **QA** | 계획 기준 대비 검증, 회귀 감지 | `qa-report.md` |

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

## 이식성

모든 도구 경로와 엔드포인트는 환경 변수로 관리됩니다. macOS ↔ Linux
이전은 `.env` 교체만으로 충분하며, 코드 수정은 필요하지 않습니다.

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

`ffmpeg` (libass 정적 빌드) · `yt-dlp` · `whisper.cpp` (small, 다국어)
· `ollama` (`llama3.2:3b`) · 오케스트레이션용 Claude API.

## 빠른 시작

```bash
git clone git@github.com:MelonS/MelonS-Agents.git
cd MelonS-Agents
cp .env.example .env
./scripts/bootstrap.sh
./agents/missions/highlight/run.sh <URL 또는 로컬 경로>
```

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
- [x] 계층형 에이전트 구조 (오케스트레이터 + 서브 에이전트 4종)
- [x] 코드 / 데이터 분리 강제 (records/ gitignore)
- [x] 환경 변수 기반 도구 경로 (.env / .env.example)
- [x] PoC 엔드투엔드: 하이라이트 추출 (EN + KO)
- [x] libass 자막 번인 (ffmpeg 정적 빌드)
- [x] 다국어 whisper.cpp (small) + 언어 인식 하이라이트 프롬프트
- [x] 배치 실행기 (scripts/batch-mission.sh)
- [x] 로직 변경 시 origin/main 자동 커밋 + 자동 푸시
- [ ] 실제 사용자 URL fixture
- [x] 자율 모드용 야간 launchd 스케줄러
- [ ] editor 내부의 반복 QA 피드백 루프
- [x] 하이라이트 외 다른 미션 타입
- [x] 미션별 비용 / 실행 시간 메트릭
- [ ] QA 피드백 재시도 루프 (실패 실행을 QA 메모로 자동 재시도)
- [x] 미션별 비용 / 실행 시간 메트릭
- [x] 이중 언어 요약 미션 (전사 → 구조화된 EN+KO 요약)
- [x] 단일 패스 ffmpeg 렌더링 (~3× 렌더 속도 향상)
- [x] 세 번째 미션 타입: shorts-batch (긴 영상 1편 → 자막 포함 숏폼 N편)
<!-- status:end -->

## 라이선스

MIT. [`LICENSE`](LICENSE) 파일을 참고하세요.
