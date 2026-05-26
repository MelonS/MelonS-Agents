# Operator decision queue

> Decisions agent could not make autonomously, accumulated for operator
> on next return.  Append-only.  Operator removes / answers / annotates
> entries as they're addressed.
>
> Latest entries at the bottom.  Each entry has:
> - **id**: stable reference
> - **when**: KST timestamp the question arose
> - **context**: what triggered it
> - **default**: what the agent did in the meantime (so progress isn't blocked)
> - **decision needed**: precise question
> - **options**: enumerated choices
> - **impact**: what changes based on the answer

---

## OPQ-001 · sprite asset licensing direction

- **when**: 2026-05-26 ~21:30 KST (post Day 7 sprint)
- **context**: Operator reviewed `Assets/Sprites/` (SDXL-Turbo generated)
  and called them "너무 구림".  Suggested fetching from asset sites
  with proper licensing.
- **default (in flight)**: Switching to **Kenney.nl** as primary source.
  Kenney's packs are **CC0** (public domain dedication, no attribution
  required) and visually consistent (top-down pixel art).
  Specifically replacing pawn / grass tile / tree with Kenney's
  "Tiny Town" or "Tiny Battle" or "Roguelike Caves & Dungeons" pack.
- **decision needed**:
  1. Approve Kenney.nl CC0 as primary asset source?
  2. Allow CC-BY (with attribution recording in `ATTRIBUTIONS.md`) as
     fallback when Kenney lacks coverage?
  3. Reject CC-BY-SA / GPL (viral) and any non-commercial-restricted
     licenses?
- **options**:
  - (a) **CC0 only** — strictest, safest for commercial use, may lack variety
  - (b) **CC0 + CC-BY** — broader pool, just need attribution file in repo
  - (c) **CC0 + CC-BY + CC-BY-SA** — wider still, but Steam-release-risky
  - (d) **Custom (paid licensing later)** — defer commercial sourcing till Steam ramp
- **impact**: Determines which asset pools agent's `resourcer` can pull from.
  Default (a) chosen autonomously since it's least risky.

---

## OPQ-002 · multi-agent code generation backend

- **when**: 2026-05-26 ~21:35 KST
- **context**: Multi-agent refactor adds a `coder` subagent that
  generates C# from natural-language spec.  Needs an LLM backend.
- **default**: Wrapping the **Anthropic Python SDK** (`anthropic`)
  with `ANTHROPIC_API_KEY` env var.  Falls back to no-op if key not set.
  Pattern matches Skill #1 / #2 design intent.
- **decision needed**:
  1. Operator's `ANTHROPIC_API_KEY` set on this machine? (currently no
     env var detected at agent init)
  2. Per Claude Max $200/mo plan, API calls are absorbed — confirm OK
     to consume from this plan?
- **options**:
  - (a) **Use ANTHROPIC_API_KEY** — operator sets env var on machine,
        coder subagent calls Claude API for C# generation
  - (b) **Use local LLM (Ollama)** — slower but no API cost, runs
        wherever Ollama is set up
  - (c) **Defer** — coder remains a manual template-filling subagent
        for now (less impressive but still functional)
- **impact**: Determines whether subsequent prototypes can be built
  end-to-end without Claude in this conversation session.

---

## OPQ-003 · second prototype target after RimWorld-lite — Suika lite (default in flight, Day 1 SHIPPED)

- **when**: 2026-05-26 ~21:40 KST · Day 1 status update 2026-05-26 ~22:02 KST
- **context**: Operator's strategic priority = multi-agent framework
  validated by *multiple* prototypes (not just RimWorld-lite).
  Need to pick next target after framework refactor.
- **default executed**: **Suika Game clone** — Day 1 shipped end-to-end
  in **~8 min wall-clock** using the new game-dev-agent pipeline
  (`gen_fruits.py` → `agent.py code` × 9 → `agent.py integrate` ×2 →
  `agent.py qa`).  Build at
  `skills/game-prototype-suika/builds/day-1-2026-05-26/SuikaLite.exe`
  (84 MB).  Screenshot in same folder: 3 fruits resting on floor with
  walls + drop-line + score UI.  Framework speedup ≈ **15×** vs PawnSim
  Day 1 baseline (~2 h).  Hypothesis "next prototype = faster" =
  EMPIRICALLY CONFIRMED.
- **decision needed**: which prototype after Suika Day 2?
- **options**:
  - (a) **Suika Game clone** — IN FLIGHT (Day 1 shipped, Day 2 queued)
  - (b) **Vampire Survivors lite** — 3-4 days, action-driven
  - (c) **Brotato lite** — 3-4 days, similar genre to (b)
  - (d) **대항해시대 라이트 prototype** — operator's passion project,
        2-4 weeks (too big for framework-validation purposes)
  - (e) **Skip** — focus on improving RimWorld-lite instead
- **impact**: After Suika Day 2, agent will start whichever option
  the operator picks (or default = (b) for next genre stress-test).

---

## OPQ-004 · YouTube Mix #2 / Mix #3 disposition

- **when**: 2026-05-26 ~22:00 KST (carried over from earlier session)
- **context**: Mix #2 published auto on YouTube channel ToddStudio
  (video id `7f7PeuNuIfI`).  Mix #3 hero-loop test mp4 ready on disk
  but not uploaded.
- **default**: No change — Mix #2 live, Mix #3 stays on disk.
- **decision needed**:
  1. Take down Mix #2?
  2. Upload Mix #3 hero-loop (snow + rhythm) as Mix #3 or v2 of Mix #2?
  3. Both stay (no further music work)?
- **options**:
  - (a) **Mix #2 stays public, Mix #3 stays local** — drift-data collection
  - (b) **Replace Mix #2 with Mix #3 hero-loop** — operator preferred direction
  - (c) **Upload Mix #3 alongside Mix #2** — A/B test
  - (d) **Both down, focus 100% on Skill #3** — clean cut
- **impact**: Channel positioning + future mix work.

---

## OPQ-006 · Day 7 build was broken on operator's machine (RESOLVED)

- **when**: 2026-05-26 ~22:00 KST (operator-on-return)
- **context**: Operator launched `day-7-fixed-2026-05-26\PawnSim.exe`,
  reported "pawn 안 보임", "이상한 사운드만 나옴", "쓰레기임".
  Screenshot shared: UI rendered but world (pawn/tile/tree) entirely missing.
- **diagnosis**: SceneSetup loaded sprite via `AssetDatabase.LoadAssetAtPath<Sprite>`
  before any TextureImporter existed.  When .meta files were missing or
  stale, sprite references resolved to null in the saved scene.
- **fix** (auto-applied):
  1. Added `ForceImportAllSprites()` to SceneSetup, called BEFORE
     prefab+scene generation.  Calls `AssetDatabase.ImportAsset(path,
     ForceUpdate)` to ensure importer exists, then sets `textureType =
     Sprite`, `PPU = 16`, `filterMode = Point`, `SaveAndReimport()`,
     `AssetDatabase.Refresh + SaveAssets`.
  2. SDXL sprites replaced with Kenney CC0 (Tiny Town grass+tree,
     Tiny Dungeon peasant) — operator's earlier ask "어디 에셋사이트에서
     좀 괜찮은거 라이센스 문제 없는걸".
  3. Built self-verification pipeline: `AutoScreenshotter.cs`
     (in-game), `BuildScript.BuildGameOnlyVerify` (skips MainMenu so
     screenshot captures Game scene directly).  Agent can now launch
     .exe, wait for auto-screenshot, read PNG, verify content without
     operator help.
- **verified**: screenshots at `G:/ai/_screenshots/day7_v3.png` show
  7 trees + 2-3 pawns + grass + auto-chop (Wood: 5) + UI all working.
- **decision needed**: none (resolved autonomously).  Operator may
  want to comment on visual polish (tree size, UI panel sizing).

## OPQ-007 · 게임 개발 직군 8개 agent 정의 (`.claude/agents/game-*.md`)

- **when**: 2026-05-27 ~00:30 KST · queued during Phase 1 refactor
- **context**: Operator's "게임회사 직군 구조" + "디렉터도 필요할 수도"
  통찰 (2026-05-27 ~00:15-00:25 KST).  현재 `.claude/agents/` 6개
  (auditor / editor / orchestrator / planner / qa / resourcer) 는
  Skill #1 (music-video) / #2 (job-hunt) 용 일반론.  게임 개발 도메인
  특화 agent 정의 부재.  장르 YAML의 `team:` 필드에 직군 이름은 적혀
  있지만, 그 직군의 SOP / 결정 권한 / 자주 빠지는 함정은 어디에도 없음.
- **default (in flight)**: `skills/game-dev-agent/genres/*.yaml` `team:`
  필드에 직군 이름 문자열만 enumerate.  실제 subagent definition 파일은
  미작성 (운영자 OK 필요).
- **decision needed**: `.claude/agents/` 게임 도메인 agent 8개 (+α)
  추가해도 되는가? 어느 방식?
- **options**:
  - (a) **새 파일 8개** at `.claude/agents/game-director.md`,
    `game-pm.md`, `game-designer.md`, `game-programmer.md`,
    `game-artist.md`, `game-sound-designer.md`, `game-qa.md`,
    `game-build-engineer.md`.  기존 6개는 유지 (Skill #1/#2 용).
  - (b) **기존 6개를 게임 도메인까지 확장** — planner.md에 "게임 도메인
    시 다음 추가 규칙..." 섹션을 덧붙이는 식.  파일 수 증가 없음.
  - (c) **장르마다 동적 team 구성** (장르 YAML team 필드에 따라
    어떤 agent 활성화) 우선 + agent 정의는 점진적 추가.
  - (d) **운영자 더 큰 단위 재구성 의견**.
- **추가 직군 (장르마다 동적 활성화)**:
  - `combat-designer` (VS / Brotato), `level-designer` (액션 / TD),
    `systems-designer` (시뮬 / RPG), `ai-designer` (콜로니 / RTS),
    `narrative-designer` (RPG / 어드벤처), `localization` (다국어).
- **impact**: 운영자 OK 받기 전까지는 장르 YAML에 직군 이름만 있고
  실제 SOP는 없음.  자율 작업 중 "기획자 관점에서 이 결정이 맞나?"
  같은 self-check는 못 함.
- **memory rule**: `.claude/agents/*.md` 수정 시 운영자 OK 필요 (영구
  규칙).  이 큐 entry가 그 OK 요청.

---

## OPQ-005 · machine 24/7 power + agent uptime expectations

- **when**: 2026-05-26 ~21:45 KST
- **context**: Operator stated 1-month full uptime planned.
- **default**: Continuing 24/7 power settings as configured 2026-05-25
  evening (sleep/hibernate off, no auto-reboot, Windows Update during
  off-hours).
- **decision needed**:
  1. Auto-restart Claude Code (this agent's runtime) on machine reboot?
     Currently NO — operator manually opens terminal + claude code.
  2. Run a watchdog that pings ComfyUI / Unity / agent and restarts on
     failure?
- **options**:
  - (a) **No automation** — operator manually opens claude session each
        morning
  - (b) **Auto-start watchdog** — agent runs every N minutes via Task
        Scheduler, restarts crashed services
  - (c) **Full autonomous mode** — agent picks up tasks from a queue
        file automatically (closer to a true production system)
- **impact**: How much of "1 month uptime" actually translates to
  agent productivity vs idle machine.
