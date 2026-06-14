# wiki-ref: Work / Skills — canonical facts

출처: https://rimworldwiki.com/wiki/Work , /wiki/Skills , /wiki/Training ,
       /wiki/Global_Learning_Factor
(직접 fetch 403 → WebSearch 스니펫, 2026-06-14)

## Work priority
- 수동 모드: work type 당 1(최우선)~4(최하위), 빈칸=금지(disabled).
- 모드 무관: 동일 우선순위 작업을 모두 끝낸 뒤 다음 우선순위로.

## Skills (0~20)
- 레벨 높을수록 속도·성공률↑ (건축 실패율, 수확량, 제작 품질, 수술 성공 등).
- 레벨별 효과는 비선형: 올라갈수록 다음 레벨 XP 비용 증가.

## Passion / 학습률
- 무열정 33% · 단열정(🔥) 100% · 쌍열정(🔥🔥) 150% XP 배율.
- XP 는 Passion × Global Learning Factor 곱.
- 하루 net 4000 XP soft cap, 초과분 20% 만 반영.
- 레벨 10 초과부터 XP 가 서서히 감소(decay).

## 핵심 시사점
- 우리는 1~4 priority grid 가 코드에 존재(PawnWorkSettings). passion/learning-factor 는 없음.
- 우리 XP 커브 200×L^1.5 (정본 ~1000/lvl 의 1/5 — 프로토 가속). disabled(0) 도 존재.
