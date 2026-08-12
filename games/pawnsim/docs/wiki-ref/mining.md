# wiki-ref: Mining — canonical facts

출처: 콜로니심 장르 위키 , /wiki/Mining_Speed ,
       /wiki/Deep_drill , /wiki/Steel/Calculations
(직접 fetch 403 → WebSearch 스니펫, 2026-06-14)

## Mining Yield (광물 수율)
- 광물 셀 1칸 = 총 300 resource 매장. 채굴 시 채굴자 Mining Yield(%)로 곱해 실제 획득.
- 100% yield = 만큼 획득. yield 100% 미만이면 일부 손실(낭비).

## 돌(Stone) 채굴
- 벽/바위 채굴 시 chunk drop (stone chunk). chunk 는 hauler 가 운반 후 가공(절단)해야 블록화.
- 즉시 카운터 적립 아님 — 물리 chunk → 운반 → 가공 사슬.

## Mining Speed
- Mining 8 ≈ 100% speed. 레벨에 비례해 채굴 속도 증가.
- Deep drill: 1 cycle = 14,000 ticks (3.89분) @ 100% speed, 35 steel/cycle.

## 핵심 시사점
- 광물에 "매장량(유한)" 개념 + 채굴자 yield 곱. 우리는 vein 당 1~3 chunk 고정 랜덤.
- 우리는 매장량/skill-yield 곱 없음(StoneVeinEntity stoneYieldMin/Max=1/3 고정).
