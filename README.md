# MelonS-Agents

An efficient multi-agent system for short-form video production.
macOS-first, Linux-portable. Designed to evolve its own logic over time
via commits to this repository.

## Architecture

```
                       ┌───────────────────┐
                       │   Orchestrator    │
                       │     (opus)        │
                       └────────┬──────────┘
                                │ delegates
            ┌──────────┬────────┼────────┬──────────┐
            ▼          ▼        ▼        ▼          ▼
       ┌─────────┐┌─────────┐┌─────────┐┌──────────┐
       │ Planner ││Resourcer││ Editor  ││    QA    │
       └─────────┘└─────────┘└─────────┘└──────────┘
            │          │        │           │
            ▼          ▼        ▼           ▼
       plan.md   resources/  outputs/   qa-report.md
```

Subagent definitions live in [`.claude/agents/`](.claude/agents/).
Mission templates and shared shell libs are under [`agents/`](agents/).

## Code / Data separation

| Layer | Path | Tracked |
|-------|------|---------|
| Code (logic) | `.claude/agents/`, `agents/`, `config/`, `scripts/` | ✓ |
| Data (outputs) | `records/missions/<date>/<id>/` | ✗ (gitignored) |
| Secrets | `.env` | ✗ (gitignored) |

The repository contains only the agent system itself. Mission outputs —
videos, transcripts, generated assets — stay local under `records/`.
What you see on GitHub is the system's own evolution, not its products.

## Portability

All tool paths and endpoints are env-managed. Swap `.env` to move
between macOS and Linux; no code changes required.

## Autonomy modes

Defined in [`config/policies.yaml`](config/policies.yaml).

- **Interactive** (`AUTONOMY_MODE=false`, default) — pauses for user
  confirmation on logic changes, destructive ops, external publishes.
- **Autonomous** (`AUTONOMY_MODE=true`) — runs unattended within
  `AUTONOMY_BUDGET_USD`. Logic files (`agents/`, `.claude/agents/`) are
  immutable in this mode.

## Mission flow

1. User states a mission.
2. `orchestrator` opens `records/missions/<date>/<id>/` + a task list.
3. `planner` → `plan.md` with acceptance criteria.
4. `resourcer` → assets + `resources/MANIFEST.md`.
5. `editor` → deliverables + `outputs/CHANGELOG.md`.
6. `qa` → `qa-report.md` with PASS / FAIL per criterion.
7. On PASS, `orchestrator` writes `summary.md`.

## Toolchain

`ffmpeg` (static libass build) · `yt-dlp` · `whisper.cpp` (small,
multilingual) · `ollama` (`llama3.2:3b`) · Claude API for orchestration.

## Quick start

```bash
git clone git@github.com:MelonS/MelonS-Agents.git
cd MelonS-Agents
cp .env.example .env
./scripts/bootstrap.sh
./agents/missions/highlight/run.sh <url_or_local_path>
```

Multi-source batch:

```bash
./scripts/batch-mission.sh -f sources.txt
```

## Status

<!-- status:start -->
- [x] Hierarchical agent scaffold (orchestrator + 4 subagents)
- [x] Code/Data separation enforced (records/ gitignored)
- [x] Env-driven tool paths (.env / .env.example)
- [x] PoC end-to-end: highlight extraction (EN + KO)
- [x] libass burned captions via static ffmpeg build
- [x] Multilingual whisper.cpp (small) + language-aware highlight prompt
- [x] Batch runner (scripts/batch-mission.sh)
- [x] Auto-commit + auto-push of every logic change to origin/main
- [ ] Real user-supplied URL fixture
- [ ] Nightly launchd scheduler for autonomous mode
- [ ] Iterative QA-feedback loop in editor
- [ ] Other mission types beyond highlight extraction
<!-- status:end -->

## License

MIT. See [`LICENSE`](LICENSE).

---

# MelonS-Agents (한국어)

짧은 영상 (TikTok / YouTube Shorts) 제작을 자동화하기 위한 멀티 에이전트
시스템. 맥OS에서 개발하지만, 환경 변수로 도구 경로를 추상화해 두었기
때문에 그대로 리눅스 서버로 이전 가능합니다.

## EPM 관점에서의 효율

- **단일 책임 + 위임**: 한 에이전트가 모든 일을 하지 않습니다. 오케스트레이터는
  분해와 위임만, 실제 작업은 4개의 서브 에이전트(planner / resourcer /
  editor / qa)가 나눠 맡습니다. 토큰과 컨텍스트 윈도우를 절약하고,
  실패 지점이 격리되어 디버깅 시간이 줄어듭니다.
- **Code vs Data 강제 분리**: 산출물(영상·전사·중간자료)은 `records/`에만,
  로직은 git 트래킹된 경로에만. GitHub 히스토리는 "시스템이 어떻게
  진화했는가"만 보여주고, 거대한 미디어 파일이 저장소를 부풀리지 않습니다.
- **환경 변수 기반 도구 경로**: `FFMPEG_BIN`, `OLLAMA_HOST`, `WHISPER_MODEL`
  등 모든 외부 의존성을 `.env`로 빼냈습니다. 맥OS → 리눅스 이전, 도구
  교체, 새 머신 셋업이 모두 `.env` 한 줄 수정으로 끝납니다.
- **자율 실행 + 결제 방화벽**: 로컬 자원(brew / ffmpeg / ollama / whisper)
  사용은 자동 승인. 유료 API · SaaS 구독 · 클라우드 리소스 생성만 명시
  확인을 요구합니다. 야간 무인 실행 시에도 사고가 발생하지 않습니다.
- **자가 진화**: 로직 변경마다 자동으로 커밋·푸시되어, README의 Status
  체크리스트와 함께 시스템의 발전 궤적이 GitHub에 그대로 남습니다.

## 미션 흐름 (요약)

`사용자 요청 → planner(계획) → resourcer(자원 수집) → editor(편집)
→ qa(검수) → summary.md`

각 단계의 출력 계약은 README 상단 영문 섹션의 표를 참고하세요.

## 빠른 실행

```bash
./scripts/bootstrap.sh              # 환경 점검
./agents/missions/highlight/run.sh <영상 URL 또는 로컬 경로>
```

여러 소스를 한 번에:

```bash
./scripts/batch-mission.sh -f sources.txt
```

## 자동 갱신되는 상태표

위 영문 **Status** 섹션은 미션이 성공하거나 새 기능이 추가될 때마다
`scripts/update-status.sh check "<항목>"`이 자동으로 체크 표시를
갱신합니다. README가 시스템과 함께 늙지 않도록 한 안전장치입니다.

## 라이선스

MIT. [`LICENSE`](LICENSE) 파일을 참고하세요.
