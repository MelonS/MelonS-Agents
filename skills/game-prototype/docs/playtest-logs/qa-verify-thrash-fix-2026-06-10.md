# QA 독립 검증 — 벌목 thrash/제자리벌목/작업마비 수정 Claim (2026-06-10)

검증자: 독립 QA (수정 내용 비공개 상태에서 블라인드 검증)
대상 빌드: `day-X-2026-06-10/PawnSim.exe` (러너 STALE 거부 없음 = 빌드가 소스 최신)

## 1. 재현 시나리오 3건

| 시나리오 | 결과 | exit | 핵심 수치 |
|---|---|---|---|
| p0-remote-chop (원거리 벌목 도달) | **PASS** (9/9 스텝) | 0 | 배정 dist=3.03 → 벌목 시작 dist=1.49 atStand=True, trees 45→44 in 5.5s |
| p0-pawn-move (기본 이동) | **PASS** (8/8 스텝) | 0 | 선택='민지', 우클릭(3.5,1.5) 도달 closest 0.44 (need ≤0.8) in 0.3s |
| p0-chop-menu (나무 클릭→벌목 메뉴) | **PASS** (8/8 스텝) | 0 | contextMenu open=True, 'label:벌목' 실클릭 → chop designations=1 |

세 시나리오 모두 OVERALL: PASS, STALE 거부 없음.

### remote-chop 스텝별
- assert:chopDesignations: PASS — designations=1
- assert:anyPawnActivity: PASS — '지훈' activity contains '벌목' at 0.8s
- assert:activityNearClick: PASS — closest-while-'벌목' **1.49** to rclick (need ≤1.7) → "도달 안 하고 제자리 벌목" 반증
- assert:treeCountBelow: PASS — trees 45→44 in 5.5s → 나무 실제 파괴

## 2. 스크린샷 육안 소견 (`G:\ai\_repro_shots\p0-remote-chop\`)

- **00_start.png**: 우클릭 대상 큰 참나무가 화면 상단 중앙(우클릭 지점)에 서 있음. 지훈은 좌측 멀리(dist≈3) '떠도는중'.
- **01_chopping.png**: 지훈이 나무 인접 위치까지 **실제로 이동**해 있음 (시작 위치에서 우측으로 이동 확인). 벌목 중 거리 1.49 로그와 일치.
- **02_final.png**: 우클릭했던 나무 스프라이트 **완전히 사라짐**, 그 자리에 통나무(목재 드랍) 더미가 생성됨. 지훈이 바로 옆에 서 있음. 쓰러짐+드랍 정상.

육안 판정: 제자리 벌목 아님 — 이동→인접 벌목→파괴→드랍 흐름이 시각적으로 확인됨.

## 3. thrash 패턴 로그 검사 (`G:\ai\_repro_run_p0-remote-chop.log`)

- `ClearTask` 총 **1회**: `[Chopper] Pawn(Clone) ClearTask tree=Tree_Oak_1_2 by=PawnChopper.Update` — 나무 파괴 직전/완료 시점의 정상 cleanup. **`by=PawnUtilityAI.Update` 반복 0회** → thrash(벌목↔떠도는중 번갈이) 패턴 미검출.
- 흐름: `배정 dist=3.03` → `벌목 시작 dist=1.49 atStand=True cell=(0,2)` → (ClearTask 끼어들기 없음) → `trees 45→44` 파괴. OK 패턴 일치.
- `NullReferenceException` 0건, `error CS` 0건.

판정: **PASS** (thrash 해소 증거 명확)

## 4. 회귀 sanity (`refactor_check.py --tag qa-verify`)

- (1/5) scenes regen: **OK**
- (2/5) build verify: **OK**
- (3/5) QA screenshot: **OK** (229 KB 캡처)
- (4/5) Player.log error scan: **OK** — no runtime errors
- (4.5) REAL QA 30s 플레이: **OK** — wood 40→70 (**+30**), 자원 정상 증가 → 작업마비 아님
- (4.6) Build Click QA 6-mode chain: **OK** — case **9/9** OVERALL PASS
- (5/5) visual diff: FAIL (baseline 1920x1080 vs current 2901x1697 해상도 불일치) — 지시대로 **판정 제외** (기능 무관)

기능 단계 전부 PASS.

## 최종 판정: **VERIFIED**

이유:
1. 운영자 보고 3개 증상 모두 직접 반증됨 — 제자리 벌목(이동 dist 3.03→1.49 + 스크린샷), thrash(PawnUtilityAI.Update ClearTask 0회), 작업마비(나무 파괴 + 30s wood +30).
2. 기본 이동·벌목 컨텍스트 메뉴도 실클릭 기반 시나리오로 PASS.
3. 예외/컴파일 에러 0건, 회귀 기능 단계 전부 PASS. 유일한 FAIL 은 사전 고지된 해상도 visual diff 뿐.
