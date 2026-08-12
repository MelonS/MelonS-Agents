# 외부 레포 리서치 — OpenMMO (Julian-adv) 분석 · 이식 가능성 · 라이선스 — 2026-08-12

운영자가 발견해 분석 요청. GitHub 저장소를 직접 열어 코드·문서·라이선스를 읽고
정리했다. 링크: https://github.com/Julian-adv/OpenMMO

## 결론부터

1. **라이선스**: 코드를 그대로 복사해도 지금(포트폴리오·해커톤용, 상업 의도 없음)
   당장은 위반이 아니다. 그래도 **복사하지 말고 기법만 배워서 새로 짜는 걸 권한다**
   — 이유는 §2.
2. **OpenMMO는 실시간 멀티플레이 MMORPG, 우리 게임(PawnSim/colony-sim-lite)은
   싱글플레이 로컬 시뮬레이션이다.** 장르가 다르면 "MMORPG 필수요소" 대부분이
   애초에 우리 쪽에 적용 대상이 아니다 — 무리하게 끌어오면 스코프만 커진다.
3. **실제로 확실히 쓸 만한 것은 1개**: OpenMMO의 `agent-client`(LLM이 텍스트로
   게임을 관찰하고 행동을 결정하는 실행 루프)가, 우리 `game-dev-agent` 스킬에
   설계만 있고 실제로는 안 만들어진 `runtime_director` 모듈의 참고 아키텍처로
   딱 맞는다. §4.
4. **G1 사회 상호작용은 우리가 이미 더 적절한 자체 스펙을 갖고 있다**
   (`design-social-2026-07-24.md`) — OpenMMO의 NPC 스케줄/메모리 시스템은
   오버스펙이라 참고할 필요 없다. §5.

---

## 1. 저장소 정보 (확인된 것)

| 항목 | 내용 |
|---|---|
| 제작자 | 송재경(Jake Song) — 본인 X 계정(@appledelhi)에서 직접 공개한 개인 프로젝트로
확인됨. 리니지 공동 개발자로 알려진 국내 MMORPG 1세대 개발자. |
| 라이선스 | **PolyForm Noncommercial License 1.0.0** |
| 규모 | 커밋 1,719개 · star 1.7k · fork 232, 2025-08 시작해 1년 안 된 활발한 프로젝트 |
| 핵심 아이디어 | 인간 플레이어와 AI 에이전트가 **완전히 같은 WebSocket 프로토콜**로
접속 — 서버가 둘을 구분 못 함. 특권 API 없음. |
| 스택 | 서버 Rust(Tokio) · 클라이언트 Svelte+Three.js · 에이전트 클라이언트 별도
Rust 크레이트 |

## 2. 라이선스 검토

`LICENSE` 원문을 직접 읽었다. 핵심 조항:

> **Personal Uses**: Personal use for research, experiment, and testing for
> the benefit of public knowledge, personal study, private entertainment,
> hobby projects, amateur pursuits, ... **without any anticipated commercial
> application**, is use for a permitted purpose.

우리 `game-prototype`은 지금 포트폴리오·해커톤(NAN2026) 제출용이고 판매 계획이
없다 — 이 조항의 "hobby project, no anticipated commercial application"에
해당한다. 그래서 **지금 코드를 가져다 써도 이 조항만 보면 위반이 아니다.**

그런데도 **복사는 권하지 않는다**, 이유 셋:

1. **라이선스 혼합 문제** — `game-dev-agent`의 `SKILL.md`엔 `license: MIT`가
   박혀 있다(`skills/game-dev-agent/SKILL.md:4`). MIT로 공개한 저장소 안에
   Noncommercial 조각이 섞이면, 이 저장소를 받아가는 사람은 그 부분만 다른
   조건(상업 이용 불가)을 떠안게 된다 — "이 저장소는 MIT다"라는 전제가 깨진다.
2. **나중에 상업 이용 가능성을 스스로 막는다** — 지금은 "상업 의도 없음"이
   맞지만, 나중에 이 게임을 스팀에 내거나 유료화하면 그 순간부터 Noncommercial
   코드는 다 걷어내야 한다. 처음부터 기법만 배워서 우리 코드로 새로 짜면 이
   리스크 자체가 없다.
3. **Required Notice 의무** — 코드를 가져오면 `Required Notice: Copyright (c)
   2025 Julian Adv <...>`를 계속 달고 다녀야 한다. 공개 레포 IP 흔적 최소화
   방침([[avoid-ip-terms-public-repo]] 메모리)과 결이 안 맞는다.

**결론**: 코드·자산 복사는 안 함. 아래 §4~6은 전부 "구조/개념을 보고 우리 코드로
독립적으로 재구현"하라는 뜻이지, 이식(포팅)이 아니다.

## 3. 우리 게임과의 구조적 차이

| | OpenMMO | 우리 게임 (colony-sim-lite) |
|---|---|---|
| 플레이 방식 | 실시간 멀티플레이(서버 권위, WebSocket 동기화) | 싱글플레이 로컬 시뮬 |
| 규모 | 32km×32km 대륙, 강·삼각주 절차 생성 | 타일맵 기반, "lite" 명시(7일 견적) |
| 소셜 | 계정·친구·파티·거래·채팅 전부 필수 | 사회 상호작용 의도적으로 최소(§5) |
| 하우징 | 플레이어 건설 다층 주택 시스템 | 건설은 있으나 정통 콜로니 심 스타일 건물(§8) — 성격이 다름 |

그래서 OpenMMO 문서에 나온 "MMORPG 필수요소"(계정 인증, 실시간 동기화, 파티,
거래소, 다층 주택)의 상당수는 **장르가 달라서 우리 쪽에 옮길 대상 자체가 아니다.**
억지로 맞추려 하지 않는 게 맞는 판단이다.

## 4. 실제로 쓸 만한 것 — agent-client 관찰→행동 루프

OpenMMO의 `agent-client`는 LLM이 게임을 플레이하게 하는 3계층 구조다.

```
LLM (전략만 결정) → agent-client (pathfinding·상태관리, A*로 좌표 계산) → 서버 (검증)
```

루프: `world_update 수신 → 텍스트로 상황 묘사 → LLM에 "What do you do?" 질문 →
행동 파싱 → 실행 → 이벤트 수집 → 반복`. 중요한 설계: LLM은 매 프레임 좌표를
정하지 않는다. `{"action": "move_to", "destination": "대장간"}`처럼 목적지 이름만
주면 클라이언트가 A*로 경로를 풀어 순차 이동시킨다 — "LLM은 판단만, 실행기는
저수준만" 분업이 명확하다.

**왜 우리한테 맞는가**: `skills/game-dev-agent/SKILL.md`엔 `runtime_director`
모듈이 "LLM-driven event generation for the game itself"로 설계돼 있는데,
**실제로는 `scripts/modules/`에 이 파일이 없다** — 직접 확인함, 설계만 있고
안 만들어진 상태다. OpenMMO의 agent-client가 이 개념의 실제로 동작하는 참고
구현이다.

부수적으로 QA에도 응용 여지가 있다: 지금 `game-qa`는 스크린샷을 읽어 판정한다
(비전 모델 필요, 비용 큼). OpenMMO식 텍스트 관찰 루프는 로직 버그 검증엔 스크린샷
없이 더 싸고 빠를 수 있다 — 단, 비주얼/아트 버그는 텍스트로 못 잡으니 **대체가
아니라 보완**이다.

## 5. 안 맞는 것 — 가져올 필요 없음

- **NPC 스케줄/메모리/개성 시스템** (`npcs.csv` + `instance.txt` + `memory.txt`):
  우리는 이미 `design-social-2026-07-24.md`에 "잡담" 수준의 최소 스펙을 따로
  갖고 있다 — 의도적으로 "관계 시뮬레이션이 아니라 살아있음의 증거"로 스코프를
  좁혀놨다(문서 원문). OpenMMO의 무거운 스케줄/메모리 시스템은 오버스펙이다.
- **절차적 대륙 생성**(강·삼각주·침식·도로 A*): `shared/src/worldgen/`은 Rust
  크레이트 단위 엔지니어링 투자다. colony-sim-lite는 애초에 "타일맵 렌더링"
  수준으로 스코프가 잡혀 있다(`genres/colony-sim-lite.yaml:78`) — 안 맞는다.
- **멀티플레이 네트워킹**: 우리 게임은 싱글플레이가 전제다. 해당 없음.

## 6. 참고할 만한 사소한 습관

OpenMMO는 `doc/ASSETS.md` 하나에 모든 외부 에셋의 출처·라이선스·생성일을
기록하고 미사용 항목엔 `[미사용]` 표시를 남긴다. 우리는 `submission-checklist.md`
에 "외부 에셋 출처·라이선스 명시"가 체크돼 있어 제출 시점엔 챙겼지만, OpenMMO처럼
**상시 갱신되는 단일 원장 파일**은 없다. 다음 라운드(있다면)에 도입을 검토할
가치는 있음 — 지금 급한 건 아니다.

## 7. 다음 행동 (결정 필요, 지금 착수 안 함)

- `game-prototype`은 어제(2026-08-11) "제출 완료, 활성 작업 없음"으로 세션이
  닫혔다. G1 사회 상호작용도 "운영자 픽 대기" 상태 그대로다. 이 문서는 **다음
  라운드를 위한 참고 자료**로 남기는 것이지, 지금 `runtime_director` 구현에
  바로 착수하는 건 아니다 — 착수하려면 운영자 픽이 먼저 필요하다.
