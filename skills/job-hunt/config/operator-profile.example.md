# Operator profile — example template

This is a documented template for `operator-profile.md` (operator-local,
gitignored).  Copy to `skills/job-hunt/config/operator-profile.md` and
edit with your own context.  The profile is the data input for v2.3+
intelligent matching (per-posting fit scoring, role discovery,
utility-module input).

The file ships as `*.example.md` (generic, no operator specifics).
Your real `operator-profile.md` is gitignored by default to keep
employment-angle content out of the public repo, matching the same
posture as `filters.yaml`.

---

## How this file is used

1. **`scripts/run.sh --seed <phrase>`** doesn't read this file (v2.1
   uses only the role-synonyms map).
2. **Phase 2.3 — per-posting fit scoring** will use this file as the
   "what does the operator look like" input to the Claude call that
   scores each posting against the profile.
3. **Phase 2.5 — utility modules** (cover-letter, interview-prep)
   will read this file as the operator-side context they need.

For now (Phase 2.1), this file is optional and unused at runtime.
Maintaining it is a low-effort investment that pays off when later
phases ship.

---

## Sections

Edit each section.  Length guideline: 1-3 short paragraphs each;
total file ~1-2 screens.  Prefer concrete facts over self-praise.

### Role target

_What kind of role are you looking for?  Use one or two sentence
phrasings, not a list of titles.  Examples below — replace with
your own:_

> AI-native product builder.  Frames problems for AI to execute,
> ships MVPs to real users, iterates against PMF.  Roughly maps to
> "Problem Solver / Forward Deployed Engineer / Founding Engineer"
> in different companies' lexicons.

If you want to target multiple role families, list them but mark
priority:

> 1. (primary) AI Solutions / Forward Deployed Engineer — direct customer
>    integration of LLM solutions
> 2. (secondary) AI Product Engineer / Builder-PM hybrid roles

### Location constraints

> Korea-based.  Seoul or remote-friendly preferred.  Open to hybrid
> with up to 2-day in-office expectation.  Visa: Korean citizen.

(Adapt for your situation.  Be specific — "remote-friendly" is more
useful than "open to anywhere.")

### Anti-targets

_What roles do you NOT want? — used both to filter postings and to
direct the cover-letter draft away from misleading framing._

> - Pure ML research / Research Scientist / PhD-required roles
> - SI / 파견 / 단순 운영 / 유지보수
> - Hardcoded enterprise legacy modernization (.NET migrations, etc.)

### Strengths (3-5 lines)

_What can you actually do well?  Prefer evidence (shipped artifacts,
years of experience, specific tools) over adjectives._

Example template:

> - 13 years of game-client engineering (Unity / C++ / C# / Lua).
>   Shipped live-service modules — auth, billing, SDK integration,
>   CI/CD pipelines.
> - Multi-agent system builder (this repo — orchestrator + planner +
>   editor + qa + auditor; 2 production skills; auto-commit/audit
>   feedback loop).
> - Recent: React/Next.js for AI-frontend builds; published Korean
>   lo-fi music shorts on YouTube using own AI tooling.

### Gaps / honest self-assessment

_What's missing in your background, so the cover-letter draft doesn't
overclaim and the fit-score is calibrated._

> - No formal startup-founder PMF experience.  This repo + ToddStudio
>   channel demonstrate PMF-iteration pattern but at side-project
>   scale, not company.
> - ML-research depth (model training / fine-tuning) is shallow —
>   target roles should be application/integration, not research.
> - Korean-language only for some KR-specific contexts (legal,
>   negotiation), English fluent for technical.

### Concrete artifacts to surface

_Links that recruiters or hiring managers can verify in <60 seconds.
The fit-score module uses these to validate your strengths claims._

> - Public repo: <github URL>
> - Live deployed artifact: <pages URL or YouTube channel>
> - LinkedIn: <linkedin URL>
> - Engineering case studies: <docs/engineering-case-studies.md URL>

### Hire-bar comfort (for the fit-score `hire_prob` dimension)

_Honest self-assessment of which company tiers you can plausibly
clear the interview process for, given your current background.
The fit-score utility uses this to compute "best company you can
plausibly get into" rather than just "company you'd want most"._

> - **High (70-90%)**: Korean-domestic mid-tier / non-FAANG /
>   stage-B–D startups where the role matches my background
>   directly (game-client + AI-tooling builder).  Hiring funnels
>   smaller, my profile is concrete + unusual enough to differentiate.
> - **Medium (40-60%)**: Korean unicorns (Toss / Naver / Kakao /
>   Coupang / 우아한형제) for AI-product or solutions-engineer roles.
>   Strong candidate pool, but my multi-agent system repo + shipped
>   shorts production are concrete differentiators.
> - **Low (15-30%)**: Global AI labs (Anthropic / OpenAI / Cohere /
>   Notion / etc.) at the Forward-Deployed / Applied-AI level —
>   thousands of applicants per posting, English-fluent CS-degree
>   norm.  Worth applying selectively where the role is *unusually*
>   matched, not as a default.
> - **Very low (<10%)**: pure ML-research / Research Scientist /
>   PhD-required postings — gap is honest, profile shouldn't
>   pretend otherwise.

(Adapt for your situation.  The fit-score model uses these tiers
*and* what the posting itself signals — a junior posting at a
FAANG will score higher hire_prob than a senior posting at the
same FAANG.)

### Application style preference

_How you want utility-module outputs (cover letters, interview prep)
tuned._

> - Cover letter: 200-300 words, evidence-first, no marketing
>   superlatives.  Match the company's stated tone (formal vs casual).
> - Interview prep: prioritize practical scenario questions
>   ("how would you ship X by Friday?") over CS-fundamentals.
> - Avoid: humble-brag, "passionate about", "team player"
>   stock phrases.

---

## Privacy

This template (`operator-profile.example.md`) is committed in-repo
as the generic starting point.  Your actual `operator-profile.md`
is gitignored.  Same posture as `filters.yaml`.

The fit-score Claude call (Phase 2.3+) reads this file content as
its profile input.  No content from this file leaves the operator's
machine except as part of an Anthropic API request you initiated —
keep that in mind if you put highly sensitive content here (specific
salary expectations, current-employer concerns, etc.).
