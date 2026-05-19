<!--
Sample output from `skills/job-hunt/scripts/run.sh` against the
default sources in `config/filters.example.yaml` (kr-wanted +
kr-programmers + kr-jobkorea + kr-saramin — all in mock-fallback
mode, no live HTTP, no API keys consumed).  Captured 2026-05-20
~01:14 KST, post digest-UX-tweak (apply links suppressed when
they equal the posting URL — visible on kr-saramin entries).

This file is a committed reference — useful when reviewing the
job-hunt skill without having to clone + run yourself.  Real
digests land under `records/jobs/<date>/digest.md` (gitignored).

Reproduce: `skills/job-hunt/scripts/run.sh --dry-run`
-->

# Job-hunt digest — 2026-05-20

> **Generated**: 2026-05-20T01:14:42+09:00
> **Locale**: `kr`
> **Sources**: kr-wanted, kr-programmers, kr-jobkorea, kr-saramin
> **Filter**: 직군: 백엔드 개발자,AI 엔지니어,풀스택 개발자 · 지역: 서울,경기 성남,원격 · include=[Python,AI,LLM,agent] exclude=[SI,파견,단순 운영]
> **Total postings**: 9 — **0 new since last digest**

## All postings (this run)

### kr-jobkorea (2)

- **AI 백엔드 엔지니어** · JobKorea Mock A
  - 지역: 서울 영등포구 · 게시: 2026-05-19
  - 요약: Python/Django 또는 FastAPI 기반 AI 백엔드. (mock — JH_JOBKOREA_LIVE=1 for live HTML scrape.)
  - [posting](https://www.jobkorea.co.kr/Recruit/MOCK_JK_400) · [apply](https://www.jobkorea.co.kr/Recruit/MOCK_JK_400?action=apply)

- **풀스택 (LLM 통합)** · JobKorea Mock B
  - 지역: 서울 송파구 · 게시: 2026-05-18
  - 요약: Node.js + Python LLM agent layer. (mock)
  - [posting](https://www.jobkorea.co.kr/Recruit/MOCK_JK_401) · [apply](https://www.jobkorea.co.kr/Recruit/MOCK_JK_401?action=apply)

### kr-programmers (2)

- **AI Engineer — LLM Agent Systems** · Programmers Mock A
  - 지역: 서울 마포구 · 게시: 2026-05-19
  - 요약: LLM agent orchestration, RAG pipelines, Python. 시니어. (mock)
  - [posting](https://career.programmers.co.kr/job_positions/MOCK_P_300) · [apply](https://career.programmers.co.kr/job_positions/MOCK_P_300/apply)

- **Backend Developer (Python/FastAPI)** · Programmers Mock B
  - 지역: 원격 · 게시: 2026-05-20
  - 요약: Python FastAPI, PostgreSQL, AI 통합 백엔드. 재택 가능. (mock)
  - [posting](https://career.programmers.co.kr/job_positions/MOCK_P_301) · [apply](https://career.programmers.co.kr/job_positions/MOCK_P_301/apply)

### kr-saramin (2)

- **[Saramin OpenAPI mock] AI 엔지니어** · Saramin Mock A
  - 지역: 서울 강남구 · 게시: 2026-05-19
  - 요약: Python/PyTorch.  LLM serving + agent infra.  (mock — needs SARAMIN_KEY + JH_SARAMIN_LIVE=1 for live OpenAPI.)
  - [posting](https://www.saramin.co.kr/job/MOCK_SR_500)

- **백엔드 개발자 (Python)** · Saramin Mock B
  - 지역: 서울 강남구 · 게시: 2026-05-20
  - 요약: FastAPI / PostgreSQL.  AI 데이터 파이프라인 운영. (mock)
  - [posting](https://www.saramin.co.kr/job/MOCK_SR_501)

### kr-wanted (3)

- **Backend Engineer (AI Platform)** · Wanted Mock A
  - 지역: 서울 강남구 · 게시: 2026-05-19
  - 요약: AI 플랫폼 백엔드 — Python/FastAPI, LLM 통합, multi-agent orchestration. (mock data — JH_WANTED_LIVE=1 with WANTED_API_KEY needed for live)
  - [posting](https://www.wanted.co.kr/wd/MOCK_W_200) · [apply](https://www.wanted.co.kr/wd/MOCK_W_200/apply)

- **AI 엔지니어 (Agent 시스템)** · Wanted Mock B
  - 지역: 서울 강남구 · 게시: 2026-05-18
  - 요약: LangGraph/agent 시스템, RAG, 벡터 DB 운영 경험 우대. (mock)
  - [posting](https://www.wanted.co.kr/wd/MOCK_W_201) · [apply](https://www.wanted.co.kr/wd/MOCK_W_201/apply)

- **Senior Software Engineer (Game Client + AI)** · Wanted Mock C
  - 지역: 경기 성남 · 게시: 2026-05-17
  - 요약: Unity 클라이언트 + AI 통합. C#/Lua/Python. (mock)
  - [posting](https://www.wanted.co.kr/wd/MOCK_W_202) · [apply](https://www.wanted.co.kr/wd/MOCK_W_202/apply)


---

_Digest produced by `skills/job-hunt` orchestrator._
_Raw fetch JSON per source: see `raw/<source>.json` in this same digest directory._
