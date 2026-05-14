**한국어** | [English](./README.md)

# MelonS-Agents

숏폼 영상 제작을 위한 효율적인 macOS 기반 멀티 에이전트 시스템.
환경 변수로 도구 경로를 추상화해 두어 리눅스로도 그대로 이식 가능.
저장소에 커밋이 쌓이며 시스템 자체의 로직이 점진적으로 진화하도록
설계되었습니다.

## 아키텍처

```
                       ┌───────────────────┐
                       │   Orchestrator    │
                       │     (opus)        │
                       └────────┬──────────┘
                                │ 위임
            ┌──────────┬────────┼────────┬──────────┐
            ▼          ▼        ▼        ▼          ▼
       ┌─────────┐┌─────────┐┌─────────┐┌──────────┐
       │ Planner ││Resourcer││ Editor  ││    QA    │
       └─────────┘└─────────┘└─────────┘└──────────┘
            │          │        │           │
            ▼          ▼        ▼           ▼
       plan.md   resources/  outputs/   qa-report.md
```

서브 에이전트 정의는 [`.claude/agents/`](.claude/agents/)에 있습니다.
미션 템플릿과 공용 셸 라이브러리는 [`agents/`](agents/) 아래에 위치합니다.

## 코드 / 데이터 분리

| 계층 | 경로 | 추적 여부 |
|------|------|-----------|
| 코드 (로직) | `.claude/agents/`, `agents/`, `config/`, `scripts/` | ✓ |
| 데이터 (산출물) | `records/missions/<date>/<id>/` | ✗ (gitignore) |
| 시크릿 | `.env` | ✗ (gitignore) |

저장소는 에이전트 시스템 자체만 보관합니다. 미션 산출물 — 영상,
전사, 생성된 자산 — 은 모두 로컬 `records/`에만 남습니다. GitHub에
보이는 것은 산출물이 아니라 시스템의 진화 과정입니다.

## 이식성

모든 도구 경로와 엔드포인트는 환경 변수로 관리됩니다. macOS ↔ Linux
이전은 `.env` 교체만으로 충분하며, 코드 수정은 필요하지 않습니다.

## 자율 모드

[`config/policies.yaml`](config/policies.yaml)에 정의됩니다.

- **Interactive** (`AUTONOMY_MODE=false`, 기본값) — 로직 변경,
  파괴적 작업, 외부 게시 전에 사용자 확인을 받습니다.
- **Autonomous** (`AUTONOMY_MODE=true`) — `AUTONOMY_BUDGET_USD`
  범위 안에서 무인 실행. 이 모드에서는 로직 파일(`agents/`,
  `.claude/agents/`)이 불변입니다.

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
./scripts/bootstrap.sh   # /tmp/smoke/ 아래에 EN+KO 합성 fixture도 함께 생성
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
