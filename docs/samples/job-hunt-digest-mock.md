<!--
Sample output from `skills/job-hunt/scripts/run.sh` exercising the
v2 short-keyword UX across all five default mock-fallback sources:

  skills/job-hunt/scripts/run.sh \
    --seed "Problem Solver" \
    --sources=_mock,kr-wanted,kr-programmers,kr-jobkorea,kr-saramin \
    --dry-run

The `--seed "Problem Solver"` matched the `problem-solver` family
in config/role-synonyms.yaml and expanded to 24 include keywords.
Each of the four kr-* mock-fallback fixtures now ships one Problem
Solver-family entry (under a different title — AI Solutions
Engineer at Wanted, Founding Engineer at Programmers, AI Product
Manager at JobKorea, 문제 해결사 at Saramin) to demonstrate the
v2 UX's cross-source coverage even before live HTTP is flipped on.

7 of 21 raw mock postings match the family — 3 from _mock + 1
from each kr-*.

Real digests land under records/jobs/<date>/digest.md (gitignored).
-->

# Job-hunt digest — 2026-05-20

> **Generated**: 2026-05-20T14:11:24+09:00
> **Locale**: `kr`
> **Sources**: _mock, kr-wanted, kr-programmers, kr-jobkorea, kr-saramin
> **Seed**: `Problem Solver` → role family `problem-solver` ("Problem Solver"), 24 synonym keywords expanded.
> **Filter**: 직군: 백엔드 개발자,AI 엔지니어,풀스택 개발자 · 지역: 서울,경기 성남,원격 · include=[Problem Solver,Problem-Solver,문제 해결사,AI Product Manager,AI Product Engineer,AI 솔루션 엔지니어,AI Solutions Engineer,AI Solution Engineer,AI Solutions Architect,AI 통합 엔지니어,AI Integration Engineer,LLM Application Engineer,LLM 애플리케이션 개발자,Forward Deployed Engineer,Forward-Deployed Engineer,FDE,Applied AI Engineer,Solutions Engineer,Founding Engineer,Product Engineer,Growth PM,Generalist,AI Builder,Customer Problem Solver] exclude=[SI,파견,단순 운영]
> **Total postings**: 7 — **0 new since last digest**

## All postings (this run)

### _mock (3)

- **Problem Solver (AI Agent)** · MockRebeatLike
  - 지역: 서울 강남구 · 게시: 2026-05-20
  - 요약: 쇼핑 AI Agent 기획+개발+배포까지 직접 담당. PMF 탐색 사이클 주도. Python/FastAPI MVP 빌드.
  - [posting](https://mock.example.com/jobs/107) · [apply](https://mock.example.com/apply/107)

- **Forward Deployed Engineer** · MockFrontierAI
  - 지역: 원격 · 게시: 2026-05-20
  - 요약: Build AI agent solutions for enterprise customers; framing problems → shipping working LLM prototypes within weeks.
  - [posting](https://mock.example.com/jobs/108) · [apply](https://mock.example.com/apply/108)

- **Generalist** · MockKRStartup
  - 지역: 서울 마포구 · 게시: 2026-05-19
  - 요약: PM+Engineer+Data Analyst 하이브리드. Ship MVPs, iterate to PMF. AI 도메인 깊이 우대.
  - [posting](https://mock.example.com/jobs/109) · [apply](https://mock.example.com/apply/109)

### kr-jobkorea (1)

- **AI Product Manager** · JobKorea Mock C
  - 지역: 서울 강남구 · 게시: 2026-05-19
  - 요약: AI 제품 기획 + 빌드 hybrid. LLM 통합 제품 발견 → MVP 출시 → PMF iteration. (mock)
  - [posting](https://www.jobkorea.co.kr/Recruit/MOCK_JK_402) · [apply](https://www.jobkorea.co.kr/Recruit/MOCK_JK_402?action=apply)

### kr-programmers (1)

- **Founding Engineer** · Programmers Mock C
  - 지역: 서울 강남구 · 게시: 2026-05-19
  - 요약: Early-stage AI 스타트업 첫 엔지니어.  Ship MVPs, iterate to PMF, agent 시스템 빌드 + 운영 전부 owns. (mock)
  - [posting](https://career.programmers.co.kr/job_positions/MOCK_P_302) · [apply](https://career.programmers.co.kr/job_positions/MOCK_P_302/apply)

### kr-saramin (1)

- **문제 해결사 (AI Agent)** · Saramin Mock C
  - 지역: 서울 강남구 · 게시: 2026-05-20
  - 요약: 쇼핑/이커머스 AI 에이전트 기획+개발+배포까지 직접 담당. PMF 탐색 cycle 주도. (mock)
  - [posting](https://www.saramin.co.kr/job/MOCK_SR_502)

### kr-wanted (1)

- **AI Solutions Engineer** · Wanted Mock D
  - 지역: 서울 강남구 · 게시: 2026-05-19
  - 요약: Enterprise customers를 위한 LLM agent 솔루션 빌드 + 통합 + 배포. Forward Deployed engineering 성격. (mock)
  - [posting](https://www.wanted.co.kr/wd/MOCK_W_203) · [apply](https://www.wanted.co.kr/wd/MOCK_W_203/apply)


---

_Digest produced by `skills/job-hunt` orchestrator._
_Raw fetch JSON per source: see `raw/<source>.json` in this same digest directory._
