# wiki-ref: Raids / Threats — canonical facts

출처: 콜로니심 장르 위키 , /wiki/Wealth ,
       /wiki/AI_director modes , /wiki/Raider
(직접 fetch 403 → WebSearch 스니펫, 2026-06-14)

## Raid points (습격 규모)
- 입력: 콜로니 wealth + 콜로니스트 수(+전투동물) + threat scale + 시작계수 + adaptation.
- 범위: 최소 35, 최대 10,000 points.
- Wealth→Points 선형보간: 14,000 wealth = 0 points, 400,000 wealth = 2,400 points
  (구간당 약 1/160.83 per wealth). 1,000,000 wealth = 4,200 points (이후 포화).
- 건물(바닥 설치)은 raid 목적 wealth 에서 절반만 계산.
- threat scale 배율: 200% = raid points 2배(=wealth 영향 2배).

## Adaptation (적응 계수)
- 림을 잃거나 고전하면 디렉터 모드가 다음 위협을 일시 완화(adaptation factor).

## 위협 종류 (가중 추첨)
- 습격(인간)/맨헌터 팩/광기 동물/기계군체 등 다종. 위협마다 대응이 다름.
- 발생 시점은 무작위(사이클+확률, 밤 습격 포함) — "언제 올지 모름"이 긴장의 본체.

## 핵심 시사점
- 우리: wealth proxy(자원+구조물 가중+림×30)→banditCount, day<8 ×0.7, 적응계수 일부.
- 정본은 wealth 가 raid의 주 동인. 우리 proxy 의 가중치/곡선이 정본과 정렬되는지 검토 대상.
