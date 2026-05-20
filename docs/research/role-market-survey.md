# Market survey — the "AI-native product builder" role family

**Last updated**: 2026-05-20 KST.

Companion to `skills/job-hunt/config/role-synonyms.yaml`.  Each entry
in that file's `problem-solver` family is a job title used by at
least one company for the same underlying role.  This doc explains
what that underlying role is, why it varies in name across
companies, which companies hire for it (where known), and what
distinguishes it from adjacent role families.

**Scope**: 2025-2026 hiring patterns in Korea + global; emphasis on
KR market.  Updated when new patterns emerge.

---

## The role itself

The "AI-native product builder" sits between three traditional
roles and doesn't cleanly belong to any of them:

- **Engineer** (writes code, ships features)
- **Product Manager** (discovers what to build, prioritizes)
- **Data Analyst** (measures, derives insight from user behavior)

The role's actual work cycle:

1. **Discover** — interview users, analyze data, identify the
   real underlying problem (not the surface request).
2. **Frame** — turn the problem into a structured prompt /
   specification that an LLM agent (or a human team) can execute.
3. **Build** — ship a web/app MVP solving it.  Often LLM-assisted
   coding speeds this stage 5-10×.
4. **Measure** — observe real-world use, instrument retention /
   PMF signals.
5. **Iterate** — adjust framing, ship next version.

The distinguishing capability is **framing**.  Engineers
implement framed problems.  PMs framed problems but often
delegated implementation.  This role does both, fast, repeatedly.
LLM coding tools (Claude Code, Cursor) make the engineering side
tractable for a non-pure-engineer; LLM agents (Claude, GPT-4-class,
agentic frameworks) make the building side a different
deliverable than 2019-era PM work.

This is why the role didn't really exist as a job title before
~2023.  The capability multiplier didn't exist.  Companies that
hire for it explicitly tend to be either:

- AI-native startups (their whole product is an LLM agent)
- B2B SaaS where customer integration of LLM solutions is
  the differentiator
- Established companies experimenting with "internal LLM
  product teams" (search, recommendation, customer-support
  automation)

---

## Why the title varies so much

No standard title yet.  Each company invents or borrows one:

| Title family | Tone | Common at |
|---|---|---|
| **Problem Solver** | Outcome-focused, deliberately non-specialist | 레브잇 (올웨이즈), 모두닥, 도슨티, 포트로직스, 핵클 (and counting; see Korean Wanted "problem solver" search) |
| **Forward Deployed Engineer / FDE** | Frontier-AI startups; emphasizes shipping customer integrations | Anthropic, OpenAI, Scale AI, Palantir (where the term originated), some KR offices |
| **Applied AI Engineer** | Library/framework companies | Cohere, Mistral, Anthropic (lower-volume than FDE) |
| **Solutions Engineer** | B2B SaaS, established term, now AI-flavored | Cohere, Hugging Face, many enterprise AI vendors |
| **Founding Engineer** | Early-stage startups, broader than role | YC-stage startups |
| **Product Engineer** | Modern startups (Linear, Vercel, Anthropic) | Product-focused engineering teams |
| **Growth PM** | Late 2010s metric-driven PM evolution | Korean startups (Toss, etc.) often use this for the same work |
| **Generalist** | Single-word title, top KR tech companies | Toss in Korea; informal usage at multi-stage startups globally |
| **AI Product Manager** | PM-leaning end of the family | Large enterprises Korean and Global |
| **AI Solution Architect** | Sales-engineering-leaning end | Enterprise vendors (Microsoft, Salesforce, Naver Cloud, KT) |

Why no convergence: the role's domain context changes the
emphasis.  A medical-platform "Problem Solver" needs HIPAA / KIPA
intuition; a frontier-AI "FDE" needs model-API depth; a B2B
"Solutions Engineer" needs enterprise-integration patterns.
Calling them all "Problem Solver" or all "FDE" would erase those
flavors.  So companies pick a title that signals their context.

The candidate, however, is often interchangeable — someone who
thrived in a Problem Solver role at 레브잇 could likely thrive in
an FDE role at Anthropic, modulo the domain learning curve.

---

## Identifying real openings (independent of title)

Title is a poor filter alone.  Description signals are
stronger.  A posting is likely this role family when it
combines (each bullet is necessary, not sufficient on its own):

1. **Ownership across functions** — "discovery + build + ship"
   in one role description, not "design specs that engineers
   build."
2. **MVP / PMF / iteration vocabulary** — "ship a prototype to
   customers in 1-2 weeks", "find PMF", "kill what doesn't work."
3. **AI / LLM context** — agent systems, LLM applications, AI
   integration, agentic workflows.  ML model training is a
   different family (see [Anti-pattern](#anti-pattern-pure-ml)).
4. **Direct customer contact** — interviews, observation,
   real-world deployment.  Not "internal stakeholder management."

Postings that hit 3-4 of these are this family regardless of
title.  Postings that hit 0-1 are probably a different role.

### Anti-pattern: pure ML

Postings emphasizing model training / fine-tuning / MLOps /
deep-learning research are a **distinct** family (`ai-engineer-ml`
in role-synonyms.yaml).  Candidate profile differs:

- Pure ML: PhD/MS often required; deep learning frameworks
  expertise; publication record; research thinking.
- Problem Solver family: shipping record; product instinct;
  cross-functional fluency; LLM-application-engineering depth
  but not research depth.

The titles can overlap ("AI Engineer" appears in both families
at different companies).  Description signals are decisive.

---

## Korean market specifics (2026)

Companies known to hire for this family in Korea:

**Direct "Problem Solver" title** (per Wanted search 2026-05-20):
- 레브잇 (올웨이즈) — origin of the title; e-commerce AI agents
- 모두닥 — medical platform
- 도슨티 — (limited public info; further investigation needed)
- 포트로직스 — B2B logistics SaaS
- 핵클 — A/B testing platform / developer tool

**Adjacent titles** (same role, different name):
- 토스 — "Generalist" (one of the original KR adopters)
- 쿠팡 — Senior Product Manager, AI Integration variants
- 카카오 — "AI Product Manager", "생성형 AI 기획"
- 네이버 — AI Service Planner / Search Solution Engineer
- 뤼튼, 업스테이지, 알라딘 (Allganize), 라이너, 매스프레소 —
  varies per company, often "AI Product Engineer" / "Solutions
  Engineer" / "Founding Engineer"
- Foreign companies with KR offices — Anthropic, OpenAI, Cohere,
  Scale, Palantir — typically FDE / Applied AI Engineer

**Where this role does NOT exist yet** (or is rare):
- 대기업 전통 사업부 (삼성, LG, SK, 현대 등 비-AI 부서) — still
  operate on engineer/PM/designer split.  Some are starting
  internal AI teams that approximate the role.
- 공공기관 / 정부 — title infrastructure doesn't support the
  cross-functional shape.
- 일반 SI — opposite of the role (specialization, not
  generalization).

### Salary / compensation calibration (anecdotal, 2026)

Public salary data is limited.  Anecdotal signal:

- 레브잇 / 핵클 / 토스-class startups: 70M-130M KRW base + equity,
  IC-track senior.
- Foreign companies (Anthropic / OpenAI KR) FDE roles: 130M-200M+
  KRW base + significant equity, IC-track.
- Large established Korean companies (네이버 / 카카오 / 쿠팡):
  band-driven, often 80M-150M KRW base; equity smaller.

These ranges are sketchy and vary heavily with seniority +
specific company + negotiation.  Update when concrete data
surfaces.

---

## Implications for `skills/job-hunt`

The current `skills/job-hunt` skill's `--seed "Problem Solver"`
flow handles the title variance well (24 synonyms expand from
that one seed).  Where it falls short — and what Phase 2.3+
would fix:

- **Description-signal scoring** — a posting matching one of the
  24 titles but failing the 4-signal check (ownership / MVP /
  AI / customer-facing) is currently included.  Phase 2.3 fit
  scoring via Claude could read the description and drop
  false-positives.
- **Pure-ML exclusion** — current exclusion list is title-based.
  A posting titled "AI Engineer" could be either family.
  Claude-based filtering would distinguish.
- **Company-context inference** — knowing that 모두닥 = medical
  platform shapes how to read the role.  Phase 2.5
  company-research module could pre-load each company's
  positioning before fit-scoring.

The synonym map (v2.1, shipped) is the necessary precursor.
Description-aware filtering (v2.3) is the next investment.

---

## Maintenance

When a new posting at a new-to-this-doc company is observed,
update both:

1. `skills/job-hunt/config/role-synonyms.yaml` — add the title
   to the appropriate family's `synonyms` list.
2. This doc — add the company name to the relevant table row.

If a posting represents a genuinely new role family (not just a
new title for an existing family), create a new family in the
synonyms file and a new section here.

---

## See also

- `skills/job-hunt/SKILL.md` — the skill contract.
- `skills/job-hunt/config/role-synonyms.yaml` — the data file
  this doc explains.
- `docs/skills/job-hunt.md` — operator walkthrough.
- `docs/research/agent-orchestration-patterns.md` — related
  research on multi-agent system patterns (this skill's
  authorship perspective).
