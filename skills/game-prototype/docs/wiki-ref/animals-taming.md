# wiki-ref: Animals — Taming / Training / Wildness / Hunting (canonical facts)

출처: https://rimworldwiki.com/wiki/Tame_Animal_Chance , /wiki/Train_Animal_Chance ,
       /wiki/Minimum_Handling_Skill , /wiki/Property:Wildness , /wiki/Animal_husbandry ,
       /wiki/Skills (Animals)
(직접 fetch 403 → WebSearch 스니펫 추출, 2026-06-14)

## 길들이기 확률 (Tame Animal Chance)
- 베이스 = 4% + 3%×handlingSkill. (예 스킬 13 → 4+39 = 43%, 위키 예시 42%.)
- wildness 로 감소: chance × 2 × (1 − wildness). (wolf 85% wildness → 42%×2×0.15 ≈ 12.9%.)
- 실패해도 시도 자체는 진행(식량 소모 모델은 게임마다 상이).

## Wildness (야생성) → Minimum Handling Skill
- wildness <15% → 최소 핸들링 0. 15~98% → 1~10 선형보간. 98~99% → 10~14.
- 즉 야생성 높은 종은 높은 핸들링 스킬 폰만 길들이기 가능.

## 훈련 (Training)
- Train Animal Chance = 10% + 5%×스킬. 1회 시도 후 **6 게임시간** 재시도 쿨다운.
- 스킬↑ → 시도당 성공률↑ → 필요 시도 횟수 자체 감소.

## Animals 스킬 효과
- 야생·가축 핸들링 품질, 훈련 성공률을 결정.
- **사냥 중 동물 반격(revenge) 확률 감소** (저스킬 사냥꾼 = 역습 위험↑).

## 핵심 시사점
- 길들이기는 단일 확률이 아니라 (스킬, wildness, 최소 핸들링 게이트)의 함수.
- 사냥꾼 스킬이 revenge 확률을 좌우 → 사냥은 무료가 아니라 리스크 있는 작업.
