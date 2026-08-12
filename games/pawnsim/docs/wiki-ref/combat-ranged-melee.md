# wiki-ref: Combat — Ranged / Melee / Weapon Damage (canonical facts)

출처: 콜로니심 장르 위키 , /wiki/Cover , /wiki/Combat ,
       /wiki/Melee_Cooldown , /wiki/Melee_DPS , /wiki/Weapons
(직접 fetch 403 → WebSearch 스니펫 추출, 2026-06-14)

## 사격 명중 (ranged hit chance) — 곱셈 모델
- Acc = ShooterAccuracy × WeaponAccuracy(at range) × Weather × Smoke × Cover (+ darkness offset).
- 거리 밴드(타일): Touch=4, Short=15, Medium=30, Long=50. 무기마다 밴드별 accuracy% 상이.
- 사격 정확도는 사수의 Shooting 스킬에 의존(레벨↑ → 거리당 명중% 곡선 상승, 표 기반).

## Cover (엄폐)
- 벽 = 최대 엄폐, 탄환 75% 차단. 일반 엄폐물은 사격 각도에 따라 효율 변동.
- smoke 가 사선 위 타일에 있으면 명중 ×0.30, 날씨 안개 ×0.50, 맑음 ×1.00.

## Melee (근접)
- 근접 명중 = 공격자 Melee 스킬 + manipulation/sight 보정 − 상대 dodge.
- 무기 데미지는 품질에 비례(저품질↓ 고품질↑). 맨손은 무기보다 약함.
- Melee cooldown = 공격 간 딜레이(무기·부위 가중 평균, 예 2.2/1.6/2.0 → ~2.1초).
- DPS(근접) = Damage / Cooldown. 사격 DPS 는 burst·warm-up·cooldown(ticks) 포함식.

## 핵심 시사점
- 명중은 단일 확률이 아니라 거리×엄폐×환경의 곱 → 전술(엄폐·거리)이 의미.
- 근접/사격 모두 스킬·부위 건강이 명중·속도에 곱셈으로 작용.
