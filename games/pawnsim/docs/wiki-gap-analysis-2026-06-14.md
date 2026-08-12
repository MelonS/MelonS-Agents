# Wiki ↔ PawnSim Gap Analysis (2026-06-14)

레퍼런스 콜로니심 위키를 지식베이스(`docs/wiki-ref/*.md`)로 정리한 뒤 우리 구현과 대조한 우선순위 갭표.
무인 자율세션에 바로 적용할 **LOW-RISK·검증가능** 항목을 표 상단에 배치.

- 정본 출처: `docs/wiki-ref/*.md` (각 노트에 위키 URL 인용). 위키 직접 fetch 403 → WebSearch 스니펫 추출.
- 파일 경로 베이스: `unity-project/Assets/Scripts/`
- RISK L = 상수/값 조정·표시(코스메틱), 신규 시스템·게임플로 변경 없음.
  RISK M = 작은 동작 추가. RISK H = 신규 시스템/코어 플로 변경 → **보류 후보(운영자 확인)**.
- 정렬: RISK 오름차순 → 임팩트 내림차순. 상단 = 먼저 적용할 것.

> 주의(제약): 1:1 클론 금지(고유명/로어 복제 X, 메커닉·숫자만). PathGrid 통행성은
> 타일-참조 동일성 기반 → 지형 통행성 변경 캐주얼 제안 금지.

---

## TIER L — LOW RISK, 먼저 적용 (값/표시·harness 검증)

| # | system | wiki canonical | our current (file:line) | proposed change | RISK | how to verify | effort |
|---|---|---|---|---|---|---|---|
| 1 | mood/break 자율식사 게이트 | 자율식사는 saturation **~30%** 도달 시 | `eatThreshold = 50f` (PawnNeeds.cs:64) | 50→ **35~40** 으로 하향(정본 30 근접·프로토 여유). 톱니 band 재확인 | L | harness: 림 1마리 food 곡선 로그, 식사 트리거 시점이 ~35~40 구간인지 count. `[Eat]` 로그 grep | S |
| 2 | mental break 임계 단일→표시 | Minor **35** / Major **20** / Extreme **5** | 단일 `moodBreakThreshold=20` (PawnNeeds.cs:290) | 상수 3개(35/20/5) **필드 추가 + Minor=35 발동 임계만 우선 적용**(동작은 기존 wander 유지). 동작 분기는 #14(M) | L | harness: mood 를 34로 떨어뜨려 break 진입 발생 count(현재는 20에서만). `[Mood] 정신붕괴 진입` 로그 | S |
| 3 | 허기 thought 임계 | Hungry ~35% 이하 / 굶주림 단계감 | "배고픔"-4 @food<25, "출출함"-2 @25~45 (PawnThoughts.cs:130-133) | 임계 25→ **35** 로(정본 Hungry 35 정렬), 굶주림 2티어 "극심한 배고픔"(-10 @food<12) 추가 | L | harness: food=30 인 림에 "배고픔" thought active 인지 PawnThoughts.active dump. food=10 시 -10 thought | S |
| 4 | skills passion XP 배율 부재 | 무열정 **33%** / 단열정 **100%** / 쌍열정 **150%** | passion 개념 없음, XP=고정 (PawnSkills.cs:66-89) | per-skill passion enum(0/1/2)+배율(0.33/1.0/1.5) `AddXP` 곱 1줄. 이름-시드 결정론(ReRoll 패턴) | L | harness: 동일 작업 N회 후 열정별 XP 누적이 33:100:150 비율인지 GetXP 비교 | S |
| 5 | mining yield skill 곱 | 셀당 매장 **300**, 채굴자 yield% 곱 | vein당 chunk 고정 1~3 랜덤 (StoneVeinEntity.cs:17-18,92) | yieldMax 를 채굴자 Mining skill 로 가중(예: +1 @lvl≥6). 매장량 개념은 M(아래 #16) | L | harness: 고스킬 vs 저스킬 채굴자 chunk drop 평균 비교(`[Mine]`/drop count) | S |
| 6 | 침대 RestMul 값 정렬 | sleeping spot **0.8** / wood **1.0** / fine **1.4** | 0.80/1.00/1.40 (BedEntity.cs:44-46) — **이미 정렬됨** | 변경 없음(검증만). MoodBonus 는 thought 경유로 이전됨 → wake thought offset(+1/+3/+6, PawnNeeds.cs:202-204) 만 정본 톤 점검 | L | 회귀 가드: BedEntity.RestMul 단위테스트(3 quality 값 어서션) | S |
| 7 | 작물 성장 온도/밤 게이트 | 정상성장 6~42°C, 밤·저광 성장 정지 | 밤 성장정지만 구현(CropEntity.cs:109-114), 온도 없음 | 온도 시스템은 H(보류). **밤 게이트 임계(0.25~0.83 DayProgress)** 만 정본 광량 50% 와 정렬 점검 | L | harness: 밤(t<0.25) 동안 growth 증가 0 인지 로그/Growth 델타 | S |
| 8 | raid wealth proxy 곡선 | wealth 가 raid 의 주동인(14k=0pt 선형) | proxy=자원+구조물가중+림×30, /500 → bonus (AIDirector.cs:752-763,338) | 구조물 가중을 정본 비율감(건물=½)에 맞춰 톤 조정 + 하한/상한 clamp 점검. **값만** | L | harness: 자원 비축 2배 시 banditCount 증가하는지 `[AIDirector] raid wealth proxy` 로그 | S |

---

## TIER M — 작은 동작 추가 (운영자 1줄 승인 동반 권장)

| # | system | wiki canonical | our current (file:line) | proposed change | RISK | how to verify | effort |
|---|---|---|---|---|---|---|---|
| 14 | mental break 3티어 동작 | Minor=비공격 / Major=기물파괴 / Extreme=공격 | 단일 wander (PawnUtilityAI IsBreaking 블록) | BreakTier enum + Major=구조물 데미지틱, Extreme=근접공격(기존 drafted-melee 재사용). spec-needs-mood §3b 참조 | M | harness: mood<5 림이 인접 대상 공격하는지 데미지 로그 | M |
| 15 | recreation(여가) need | 여가 0하강 fuel meter, rest>33%·food>29% 게이트 | 여가 need 없음(하루 3축 중 1축 결손) | 최소 루프: 여가 게이지 1개 + "지루함" thought(-N) + 의자곁 휴식. spec D-B 1안 | M | harness: 여가 0 림에 "지루함" thought active + mood 하락 | M |
| 16 | mining 매장량(유한) | 셀당 300 resource, 고갈 | vein chunk 무한(고정 랜덤) | vein 당 총 매장량 필드 → 0 도달 시 고갈/소멸 | M | harness: 한 vein 반복 채굴 시 N회 후 drop 0 | M |
| 17 | 작물 종 다양화 | 쌀/감자/옥수수 성장시간·수확량·비옥도 민감도 상이 | 단일 쌀 (CropEntity growthPerSecond/harvestFood 고정) | 작물 2~3종 데이터(성장속도·수확량) + grow-zone 선택. spec D-F/D5 | M | harness: 작물별 ripen 시간·수확량이 데이터대로 다른지 | M |

---

## TIER H — 신규 시스템 / 코어 플로 → 보류 후보 (운영자 확인)

| # | system | wiki canonical | our current | proposed change | RISK | note |
|---|---|---|---|---|---|---|
| 20 | 온도/계절 시스템 | 쾌적 16~26°C, 성장 6~42°C, 겨울 옥외성장 0 | 온도 시스템 전무(달력 장식) | 온도 필드+히트스트로크/저체온+겨울 성장0. **보류 후보** (spec D-C) | H | 식량 밸런스 직격·다중 시스템 연쇄(난방→전력). 운영자 평결 필요 |
| 21 | passion/learning-factor 풀모델 | XP soft cap 4000/day, lvl10+ decay | XP 커브만(200×L^1.5) | soft cap·decay·global learning factor 풀구현 | H | 코어 진행 곡선 변경. #4(passion 배율만)이 L 분리분 |
| 22 | raid points 정본 곡선 이식 | 35~10,000, wealth 선형보간(14k=0) | proxy/500 단순식 | 정본 wealth→points 선형식 이식 | H | 난이도 곡선 코어 변경. #8(값 톤 조정)이 L 분리분 |

---

## 적용 순서 권고 (orchestrator)
TIER L 1→2→3→4→5→8 먼저(전부 값/표시·harness 검증, 독립 출하 가능),
그다음 6·7 검증/회귀가드, 이후 TIER M 는 운영자 1줄 승인 후 14→15 순.
TIER H(20/21/22)는 운영자 평결 전 착수 금지.

## 정본 노트(RAG)
- `docs/wiki-ref/growing.md` · `needs.md` · `mood-mentalbreak.md` · `work-skills.md`
- `docs/wiki-ref/mining.md` · `raids-threats.md` · `temperature-seasons.md`
