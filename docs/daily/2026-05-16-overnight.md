# 야간 자율 브리핑 — 2026-05-15 → 2026-05-16

## TL;DR (한 문장)

사용자 요청 3건은 docs/script-level까지 다 처리해서 푸시 완료
(agent 정의 수정이 필요한 2건만 proposal로 morning OK 대기 중), 그 다음
야간 자율로 copyright-policy "Still TODO" 1건 출하 + README Status
정합화 + auditor surface 회귀 테스트 + audit 디렉토리 README + README
아키텍처 다이어그램 깨짐 수정 + docs/architecture.md drift 수정 + stale
TODO 정리 + policies.yaml vs operator-contract 정합화 — **여기서 사용자가
"결과물 나왔냐"고 짚어줘서** 실제로 한 번도 안 뽑힌 진짜 결과물(실 CC
Sintel → 9:16 short)을 01:52 KST에 출하(`highlight-015213` PASS),
근본 원인 ("큐 비었다=완료"로 잘못 판단)에 대한 구조적 픽스로
`docs/goal.md` outcome 레이어 신설 + CLAUDE.md 세션-시작 protocol에
박음.  agent 정의 수정은 user OK 받은 CLAUDE.md만, working tree clean.

---

## 야간에 처리된 것 (자율 권한 안)

| Commit | 내용 | 안전한 이유 |
|--------|------|------------|
| `71d785f` | `docs/ideas.md` 생성 | docs-only, agent 로직 무관 |
| `a37d37f` | `scripts/audit-run.sh` 능동화 + proposal 문서 | script-level + docs/, agent 정의 무변경 |
| `4579c16` | `docs/roadmap.md` Done 갱신 + Now 제안 블록 | Done은 Claude 권한, Now는 HTML 주석으로만 제안 (rewrite 아님) |
| `8340470` | 이 브리핑 초안 작성 | docs/daily — 운영 권한 |
| `ef0f825` | **per-platform 재이용 규칙** — `guard_publish`가 `publish_rules` 4개 필드 모두 소비 (`internal-demo` / `public` / `youtube` / `instagram` / `tiktok`); 16/16 PASS | lib + script level, agent 정의 무관 |
| `d547a32` | **README Status 정합화** — stale 미체크 1건(per-platform) 체크 + 미체크 5건 인라인 사유 + "Status는 인벤토리, 우선순위는 roadmap.md" 명시 | docs-only |
| `bc2381b` | **`scripts/test-audit-parser.sh`** (6 케이스 회귀 테스트, 6/6 PASS) + **`docs/audit/README.md`** (감사 트레일 오리엔테이션 + 알림 라이프사이클 + playbook) | test/docs only |
| `cc1c4ca` | 브리핑 + roadmap Done에 후속 커밋들 반영 (4-커밋 시점 → 7-커밋 시점) | docs/daily, roadmap Done |
| `bb89a26` | **README 아키텍처 다이어그램 깨짐 수정** (5 화살표 vs 4 박스 → 선형 파이프라인 + auditor sidecar), auditor 행 추가, 모델 라벨, EN/KO 미러; `docs/architecture.md`의 Tier 1 도 병렬 fan-out → 순차로 정합화 + autonomous flow에 auditor launchd 추가 | docs only |
| `b706bc7` | stale TODO 1건 정리 (`copyright-allowlist.yaml`) + **`config/policies.yaml` ↔ `operator-contract.md` 정합화** (autonomous.allowed.run_tools에 whisper/yt-dlp/ffprobe/jq/python3 추가, external_publish를 paid_external + external_message로 분리, 모든 룰에 op-contract 섹션 cross-ref) | config docs-as-code |

푸시 완료: `origin/main` 동기 상태.  워킹트리 clean.

24개 shell script `bash -n` syntax sweep 통과 (0 fail).

---

## 아침에 user 결정 필요한 것

### A. Auditor agent 정의에 능동화 문단 추가 (1-paragraph 편집)

**파일**: `.claude/agents/auditor.md`
**위치**: "Principles" 섹션 안
**추가할 내용**:

> **Alerting**: your report is post-processed by `scripts/audit-run.sh`,
> which extracts the Verdict line and writes `docs/audit/CURRENT-ALERT.md`
> whenever the verdict is non-CLEAN.  The alert auto-clears when the
> next run returns CLEAN.  You don't write `CURRENT-ALERT.md` — the
> wrapper does.  Just keep the report structure exact, especially the
> `**Verdict**: …` line, so the parser finds it.

**왜 gated**: `.claude/agents/*.md` 편집은 [logic changes need OK] 룰.

### B. CLAUDE.md session-start protocol에 alert 체크 추가

**파일**: `CLAUDE.md`
**위치**: "Session-start protocol" 섹션
**추가할 내용**:

> If `docs/audit/CURRENT-ALERT.md` exists, read it before picking up
> the roadmap "Now" item.  It means the last audit run flagged drift
> or a critical issue.  Resolving the alert may bump roadmap priority.

**왜 gated**: 프로젝트 운영 룰 변경이라 conservatively logic-change로 분류.

### 적용 방법 (한 번의 "OK"로)

아침에 "둘 다 OK" 한 마디면 양쪽 편집 + commit + push를 1턴에 완료.
proposal 전문은 [`docs/proposals/2026-05-15-auditor-active.md`](../proposals/2026-05-15-auditor-active.md).

---

## 검토 의견 정리 (어젯밤 user에게 보낸 것 압축)

### TODO1 (auditor 능동 감시)

4개 결정 항목 모두 답 정했음:

1. **감시 패턴**: 기존 6 dimension 그대로 유지. auditor.md 이미 잘 작성됨.
2. **LLM 정책**: 현재 Sonnet 단일 단계 유지.  일일 추정 $0.05/run = $1.5/월,
   noise floor 아래.  Haiku 분리는 v2 최적화로 보류.
3. **보고 채널**: `docs/audit/CURRENT-ALERT.md` (committed, 안정 경로,
   자동 생성/삭제).  → **이미 구현 완료, 푸시됨**.
4. **처리 vs 보고**: 보고만.  처리는 user 승인 게이트 유지.  
   [logic changes need OK] 룰과 충돌 안 함.

### ideas.md (심플 vs 상세)

- **상세 채택, 카테고리는 3개로 축소** (Agents / Pipeline+Infra / Intelligence+Misc).
  5개는 빈 섹션이 더 보임.
- Scout 항목 등록.  "self-evolving 재료" 표현은 `writing_tone` 룰과
  살짝 부딪혀서 "외부 정보 수집 → 다음 미션 후보로 활용"으로 톤다운.

### 다른 LLM의 sycophancy 비판

대부분 동의:
- "self-evolving" 마케팅 갭 → 사실. 현재 코드는 auto-commit/auto-push이지
  self-modifying 아님.
- shell 94.7% → 동의하지만 지금은 shell이 맞음, Python 이관 트리거는
  "동일 패턴 3개 스크립트에서 반복될 때".
- Planner/Resourcer 합치기 → 가능하지만 지금 추상화 회귀 비용.  v2 후보.

부당하다고 본 부분:
- "Status 체크리스트 평면적" → 본인이 이미 roadmap.md를 source of truth로
  격상시켜서 README Status는 의도적 보조.  그 LLM이 운영 룰을 몰랐음.
- "2일차에 README 정성껏 쓴 거 의심" → 다국어 README 스타일은 `readme_structure`
  로 명시한 본인 정체성.  회피 행동 아님.

자기점검: 이번 야간 작업에서 칭찬 톤 없이 결정 + 비판 + 권한 게이팅
중심으로 처리.

---

## 야간 launchd 03:00 audit 예정

`com.melons.agents.auditor` plist가 03:00 KST에 `audit-run.sh all` 실행
예정.  새로 추가된 verdict-parsing 로직이 처음으로 실제 audit 보고에
대해 돌게 됨.

**예상 결과**:
- 어제 + 오늘 야간 작업 모두 docs/code 정합성 OK 상태 → verdict CLEAN
  가능성 큼.  이 경우 `CURRENT-ALERT.md` 생성 안 됨.
- 다만 새로 추가한 `docs/ideas.md`, `docs/proposals/`, 수정된 `audit-run.sh`
  를 auditor가 처음 보는 거라 drift로 잡힐 수도.  그러면 morning에
  `CURRENT-ALERT.md` 존재 + 그 안에 finding이 보임.

어느 쪽이든 정상.  CLEAN이면 잘 동작, non-CLEAN이면 능동화 surface가
실제로 작동하는 것의 증거.

---

## 메트릭

- 야간 커밋: 10건 (`71d785f` → `b706bc7`)
- 새 파일: `docs/ideas.md`, `docs/proposals/2026-05-15-auditor-active.md`,
  `docs/daily/2026-05-16-overnight.md`, `scripts/test-audit-parser.sh`,
  `docs/audit/README.md`
- 수정 파일: `scripts/audit-run.sh`, `scripts/publish-gate.sh`,
  `agents/lib/copyright.sh`, `docs/roadmap.md`, `docs/copyright-policy.md`,
  `README.md`, `README.ko.md`, `docs/architecture.md`,
  `config/policies.yaml`, `config/copyright-allowlist.yaml`
- **agent 정의 수정: 0건** (logic-changes-need-OK 룰 준수)
- 테스트 통과: publish-gate 16/16, audit-parser 6/6, shell syntax 24/24
- 토큰 비용: 야간 작업은 본 세션 비용만 (auditor 미실행, 03:00 launchd 발화 대기)

---

## 아침 첫 액션 추천 순서

1. 이 파일 (`docs/daily/2026-05-16-overnight.md`) 읽기.
2. `docs/audit/CURRENT-ALERT.md` 존재 여부 확인 — 있으면 우선 처리.
3. `docs/proposals/2026-05-15-auditor-active.md` 훑어보고 Part A + B 승인 여부 결정.
4. 승인 시 → 두 편집 적용 + commit/push (한 턴에 완료 가능).
5. 그 다음 새 focus 지정 또는 `docs/copyright-policy.md` "Still TODO"에서 끌어올림.
