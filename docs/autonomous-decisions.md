# Autonomous decisions log

운영자 부재(2026-05-31, 6~10시간) 자율 작업.  "순서대로 전부"(코어검증→장기생존→폴리시)
지시.  gated 신기능(#4)은 승인 사안이라 미착수.  매 항목 관찰→수정→검증(isolated/integration
+LongPlay)→독립 커밋.  [[observe-dont-speculate]] + [[no-sloppy-shortcuts]] 적용.

## 2026-05-31 자율 세션 (라이브 피드백 직후 이어서)

먼저 라이브 세션에서 운영자가 직접 지적해 고친 것 (재현·로그 기반, 추측 아님):
- #216 창 설정: productName PawnSim + 1280x720 윈도우 + 리사이즈 + runInBackground
- #217 새게임 씬 2중 로드 fix (시간멈춤/목재0 근본원인 — 버튼 리스너 중복→싱글톤 중복)
- #218 목재 순간이동 진짜 fix — TryReserveWorkStandPos fast-path 가 타깃변경 시 옛 stand
  cell 재사용 → AtStandCell 즉시 true → 안 걷고 저장고 드롭.  WOODTRACE 로그로 확정.
- #219 시간속도(Game.unity 직렬화 60→6, 하루 4분) / 작물→고기(농작물 식량더미로) /
  우클릭 강제지정(작물·광맥·나무 림이 가서 작업)

이후 자율 QA 로 로그 샅샅이 훑어 발견·수정:
- #220 석재 광맥 12→20 (운영자 '맵에 석재 없음' — 광맥은 재생 안 됨)
- #221 채광 무한 포기 루프 (로그 give-up vein 358회) — 도달불가 광맥 쿨다운(20→60s),
  MineStoneAction 이 쿨다운 광맥 스킵
- #222 휴식 도달 stuck (LongPlay '민지 휴식이동 no-move 60s') — restTarget 경로에
  도달 timeout 15s 추가 (자율취침엔 이미 있었음)

검증 상태: isolated 76/76, integration 42/42, LongPlay 250~300s 3명 전원 생존,
불변식 위반 0, 자원 물리운반(텔레포트 없음) wood~525 stone~204 meals~49.

## 남은 것 / 운영자 결정거리
- give-up vein 아직 ~0.3-1.0/s (광맥이 rock 클러스터에 둘러싸여 도달불가 스폰되는 경우).
  쿨다운으로 churn 완화했으나 근본은 스폰 시 reachability 보장 필요 — 추후.
- 목재 운반 우선순위(#2): 현재 haul>chop (부패방지).  '벌목>운반'으로 바꿀지는 운영자 선택.
- gated 신기능(작업우선순위 그리드/기분 thought/지형 이동비용): 승인 필요.
