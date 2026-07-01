# wiki-ref: Needs (Food / Rest / Recreation) — canonical facts

출처: 콜로니심 장르 위키 , /wiki/Saturation , /wiki/Recreation ,
       /wiki/Eating , /wiki/Food
(직접 fetch 403 → WebSearch 스니펫, 2026-06-14)

## Food (포만도, Saturation)
- 성인 1명: 하루 1.6 nutrition 소모, 동시 저장 1.0 nutrition.
- 림은 saturation 30% 도달 시 다음 행동으로 식사 시도(자율 섭취 임계).
- 4단계 임계: Fed → Hungry(~35% 이하) → Ravenously Hungry(~20% 이하)
  → Malnourished/Starving(0% 도달 시 영양실조 HP 손상 시작).

## Rest (수면)
- 자연 falling meter (먹/자/놀 외에는 0으로 하강).
- 수면은 밤 1회 6~8시간 권장(낮에 졸리면 효율 저하 thought).
- 회복: 침대 위에서 잠. 침대 quality(RestMul) 가 회복속도에 곱.

## Recreation (여가/오락)
- 별도 need bar. 0으로 자연 하강하며 여가활동으로 회복.
- 여가활동 게이트: rest > 33% AND food > 29% 일 때만 자율 여가 추구.
- 여가 결손 지속 → 부정 thought(지루함) → mood 하락.

## 핵심 시사점
- 3 need (food/rest/recreation) 가 모두 0으로 자연 하강하는 "fuel meter".
- 자율 식사는 ~30% 에서. 여가가 빠진 우리 구현은 하루 3축 중 1축 결손.
