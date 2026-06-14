# wiki-ref: Mood / Mental Break — canonical facts

출처: https://rimworldwiki.com/wiki/Mental_Break_Threshold , /wiki/Mood ,
       /wiki/Mental_break , /wiki/Thoughts
(직접 fetch 403 → WebSearch 스니펫, 2026-06-14)

## Mood 모델
- Mood = 모든 active thought offset 의 합, 0~100% clamp. 감쇠 timer 없음(thought 합으로 settle).
- thought 2계열: ① need-driven(허기/탈진/추위/지루함 — 상태 지속 동안 on)
                  ② event-driven(좋은 식사/좋은 침대/시신 목격 — 시한부 감쇠).

## Mental break 임계 (기본 콜로니스트)
- Minor:   mood < 35%
- Major:   mood < 20%   (= minor 의 4/7)
- Extreme: mood < 5%    (= minor 의 1/7)
- 임계 아래에서 즉발 아님 → **평균 시간(mean-time-to-break) 확률 발동**. 낮은 티어일수록 드묾/심함.
- minor threshold 는 trait 로 1%~50% 조정. major/extreme 는 minor 의 4/7·1/7 비율 고정.

## 대표 thought offset (예시, 기억 기반·검증용)
- "ate without table" ≈ -3, "slept on the floor" ≈ -4, "slept outside" ≈ -4
- "ate fine meal" ≈ +5, "ate lavish meal" ≈ +12, "saw corpse" ≈ -6, "colonist died" ≈ -6~-12

## 핵심 시사점
- 3티어(35/20/5) + 확률 발동이 정본. 우리는 단일 임계 20 + 확률롤(8%/s) 1티어만 있음.
