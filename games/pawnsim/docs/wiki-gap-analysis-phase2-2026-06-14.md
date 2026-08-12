# Wiki ↔ PawnSim Gap Analysis — Phase 2 (2026-06-14)

Phase 1(growing/needs/mood/work/mining/raids/temp) 이후 **미커버 5계통**을 위키 지식베이스로 정리하고
우리 구현과 대조한 우선순위 갭표. 코드 변경 없음(KB 확장 전용).

- 정본 출처: `docs/wiki-ref/{combat-ranged-melee,recreation-joy,animals-taming,medicine-health,construction-power}.md`
  (각 노트에 위키 URL 인용, 직접 fetch 403 → WebSearch 스니펫).
- 파일 경로 베이스: `unity-project/Assets/Scripts/`
- RISK L = 상수/표시·작은 값 조정(신규 시스템 없음). M = 작은 동작 추가. H = 신규 시스템/코어 플로 → **보류 후보**.
- "구현 상태": [부분] = 일부 존재, [없음] = 부재.
- 정렬: **RISK 오름차순** → 임팩트 내림차순.

---

## TIER L — LOW RISK, 먼저 적용 (값/표시·harness 검증)

| # | system | wiki canonical | our current (file:line) | 상태 | proposed change | RISK | how to verify (harness probe) |
|---|---|---|---|---|---|---|---|
| 1 | melee 데미지 = 무기품질·스킬·부위 | Melee 명중/데미지 = 무기·Melee스킬·manipulation·dodge | 자동공격 `(attackDamage+wpnBonus)×meleeMul×skillFactor` (PawnEntity.cs:296-300) — **이미 곱셈 모델** | [부분] | 변경 없음(검증만). dodge 미반영은 #11(M) | L | harness: 고Combat vs 저Combat 림 동일 표적 N타 평균 데미지가 스킬 비례인지 `[Combat]`/TakeDamage 로그 |
| 2 | 사격 정확도 = 사수스킬 함수 | 거리당 명중%가 Shooting 스킬 표로 상승 | ArrowProjectile spread = (1.2−shootingAccuracy)×π/8 (ArrowProjectile.cs:25-37); abil 만, 스킬 무관 | [부분] | spread 계산에 Combat skill 1줄 가중(예: spread ×(1−lvl·0.03)). **값/공식만** | L | harness: 고스킬 vs 저스킬 사수 명중률(hit/shot) 차이 count |
| 3 | building HP 자재 tier | 벽 HP 자재 의존(목<석<강) | WallEntity MaterialStats 100/280/300 (WallEntity.cs:31-35) — **이미 tier화** | [부분] | 변경 없음(검증만/회귀가드). 정본은 더 높은 절대값이나 프로토 스케일 일관 | L | 회귀: WallEntity.MaterialStats 3값 단위테스트 어서션 |
| 4 | construction 속도 = 스킬 가산 | 0스킬 50%, 레벨당 +15% | build mul = construction×manip ×(1+lvl·0.04) (PawnBuilder.cs:105-111) — 스킬 스케일 존재(배율만 상이) | [부분] | +0.04/lvl 톤만 정본(+15%p) 감각에 맞춰 점검. **값만** | L | harness: 고Build vs 저Build 림 동일 청사진 완료시간 비교 |
| 5 | 종별 taming 난이도 | tame = (4%+3%·skill)×2×(1−wildness) | 종별 flat tameSuccessRate 0.15~0.60 (AnimalEntity.cs:39-44,77) | [부분] | flat rate 를 (1−wildness) 톤으로 재라벨링(멧돼지 낮게/토끼 높게 = 이미 정합). **표시·값만**, 스킬연동은 #12(M) | L | harness: 종별 TryTame N회 성공률이 데이터 비율인지 `[Tame]` 로그 count |

---

## TIER M — 작은 동작 추가 (운영자 1줄 승인 권장)

| # | system | wiki canonical | our current (file:line) | 상태 | proposed change | RISK | how to verify |
|---|---|---|---|---|---|---|---|
| 11 | dodge / 근접 회피 | melee 명중 − 상대 dodge chance | dodge 개념 없음(항상 명중) | [없음] | 표적별 dodge 1필드 + 명중 roll. 기존 자동공격 경로에 게이트 | M | harness: 고dodge 표적이 일부 타격 miss 하는지 count |
| 12 | taming/hunting 스킬 연동 | tame=스킬함수, 사냥 revenge 확률 스킬 감소 | 길들이기 flat(스킬무관), 사냥 revenge 존재하나 스킬감소 없음 (AnimalEntity.cs:265-271, PawnHunter) | [부분] | tameSuccessRate 에 handler skill 가중 + revenge 확률에 hunter Combat 스킬 감소 1줄 | M | harness: 고스킬 사냥꾼 revenge 빈도↓, 고스킬 길들이기 성공률↑ |
| 13 | tend 품질 / 약 등급 | 약 등급별 tend quality 상한, 품질→회복·감염 | 치료=전부위 출혈0 + flat +5 HP (PawnDoctor.cs:73-83), 품질·약 없음 | [부분] | 의사 스킬 → tend 품질 → 회복량/감염확률 스케일(약 등급은 후순위). 출혈정지는 유지 | M | harness: 고스킬 의사 회복량/감염방지 > 저스킬인지 |
| 14 | construct 성공확률(자재낭비) | 75% + ~3%/lvl, lvl8=100%, 실패=자재손실 | 성공확률 없음(항상 성공) (PawnBuilder.cs) | [없음] | 저스킬 시 완료 roll 실패→자재 일부 손실. spec 참조 | M | harness: 저Build 림 청사진 N개 중 일부 자재낭비 발생 count |
| 15 | infection / 상처 타입 | 물림/화상 25%·기타 10% 감염, tend품질 배율 | 감염 시스템 없음(부위 HP/출혈만) (PawnHealth.cs) | [없음] | 상처에 감염확률 필드 + 진행 타이머 + 치료품질 감소. 사망경로 추가 | M | harness: 미치료 림에 감염 발생/진행, 치료 시 확률↓ |

---

## TIER H — 신규 시스템 / 코어 플로 → 보류 후보 (운영자 확인)

| # | system | wiki canonical | our current | 상태 | proposed change | RISK | note |
|---|---|---|---|---|---|---|---|
| 20 | recreation(여가) need | 시간당 2.5% 하강, 톨러런스 50/30, 활동별 power | 여가 need bar 없음(Joy 스케줄 슬롯 라벨 + 의자 comfort thought 만) (PawnSchedule.cs:12-34, ChairEntity.cs:27) | [없음] | 여가 게이지 + 활동 종류 2~3 + 톨러런스 + "지루함" thought. **하루 3축 중 1축 결손 메움** | H | mood 곡선·스케줄·가구 연쇄 다수. Phase1 #15(M 최소안)과 중복 — 운영자 평결 |
| 21 | cover / range-band 사격 | 벽 75% 차단, Touch4/Short15/Med30/Long50 밴드별 무기 acc | 엄폐·거리밴드·날씨 전무(직선탄+각도 spread만) (ArrowProjectile.cs) | [없음] | 사선 엄폐 판정 + 거리밴드 명중곡선 + 날씨 배율. 전투 코어 변경 | H | 전술 깊이↑이나 AI 타게팅·엄폐 path 연쇄. 보류 후보 |
| 22 | Power(전력) 시스템 | Wd 단위, generator/battery 600Wd/conduit, 정전→가전정지 | 전력 개념 전무(조명·작업대 무전력 동작; grep power 무관매치만) | [없음] | 생산/저장/배선/소비 그래프 + 정전. 조명·냉난방·연구 종속화 | H | 신규 코어 시스템(다중 의존). 온도(#20 Phase1)와 난방으로 연쇄. 평결 전 착수 금지 |

---

## 적용 순서 권고 (orchestrator)
TIER L 1·3 은 검증/회귀가드(이미 구현됨), 2·4·5 는 값/공식 1줄 → 독립 출하 가능.
TIER M 는 운영자 1줄 승인 후 13(tend품질)→12(스킬연동)→11(dodge) 순(부분구현 확장이 신규보다 저리스크).
TIER H(20/21/22)는 신규 시스템 → 운영자 평결 전 착수 금지. 특히 22(Power)는 온도/난방과 연쇄.

## 정본 노트(RAG, Phase 2 신규)
- `docs/wiki-ref/combat-ranged-melee.md` · `recreation-joy.md` · `animals-taming.md`
- `docs/wiki-ref/medicine-health.md` · `construction-power.md`
