# wiki-ref: Construction / Power (canonical facts)

출처: https://rimworldwiki.com/wiki/Construction_Speed , /wiki/Construct_Success_Chance ,
       /wiki/Work_To_Build , /wiki/Wall , /wiki/Power , /wiki/Battery ,
       /wiki/Power_conduit , /wiki/Wood-fired_generator , /wiki/Geothermal_generator
(직접 fetch 403 → WebSearch 스니펫 추출, 2026-06-14)

## Construction 스킬
- 성공확률: 베이스 **75%**, 레벨당 +~3%, lvl **8** 에서 100%(실패=자재 일부 손실).
- 건설속도: 0스킬 50%, 레벨당 **+15% 가산** (work-to-build 시간에 곱).
- 벽 work-to-build ≈ 135 ticks(2.25s) × 속도 × 자재 work 배율. 자재 5 stuff.

## Building HP (예시)
- 벽 HP 는 자재 의존(나무<석재<강철<plasteel). 매끈한 화강암 벽 ~900 HP 등.
- 건물 HP=0 → 파괴. 공격·열화로 감소, 수리로 회복.

## Power (전력) — 별도 시스템
- 단위 = **Wd(Watt-day)**. 생산(generator) − 소비(가전/조명) = 순부하.
- **Battery**: 1개당 최대 **600 Wd** 저장(잉여 충전, 부족 시 방전). 효율 손실 있음.
- **Conduit(전선)**: Electricity 연구 후. 1 steel/35 ticks. hidden conduit 은 공격불가·단락면역.
- 발전기 예: 목재발전(steel 100·comp 2·건설4) / 지열발전(steel 340·comp 8·건설8).
- 정전/단락 시 연결 가전 정지 → 조명·냉난방·작업대 의존성 발생.

## 핵심 시사점
- Construction 스킬은 속도뿐 아니라 **성공확률(자재낭비)** 도 좌우.
- Power 는 생산/저장/배선/소비의 그래프 시스템 → 조명·냉난방·연구가 전력에 종속.
