# 2026-05-27 — Pipeline Refactor Phase 1 (autonomous block)

**Trigger**: operator's core directive 2026-05-27 ~00:00-00:30 KST:

> 1. "프로토타입 하나만 잘만들면 의미가 없음 결국에는 최종에 만들고
>    싶은 게임은 다른 게임일수도 있고"
> 2. "끊임없이 파이프라인은 최적화 되어야함. 버그 없고 재밌는
>    게임을 만들 수 있도록"
> 3. "많은 게임을 만들수록 레포가 발전해야함"
> 4. "기본적으로 너 스스로 자율로 만들어야함. 아마도 99%는 니가
>    자율로 만들꺼야"
> 5. "게임회사 직군과 비슷해져야 하지 않을까? + 디렉터도 필요할 수도"

Empirical question being answered: **"림월드 코드가 다 없어져도
현재 레포만으로 다시 만들면 빠르게 제작이 가능한가?"**

Answer before Phase 1: ~partial (planner had genre, qa.py + integrator
worked, but boilerplate + bug-patterns + Editor scaffold all required
per-prototype hand-coding).

Answer after Phase 1: **substantially yes for genre + system layers**.
Editor scaffold + 15 C# template catalog covers most boilerplate.
Bug patterns from PawnSim + Suika now baked into templates so they
won't recur.

## What landed (7 commits)

| Commit | Phase | What |
|---|---|---|
| `e28c81f` | (operator fix) | PawnSim 5s auto-quit + chop SFX buzz |
| `2056814` | **P1.1** | Genre YAML externalization (3 → 4 genres, 2048-lite added by ONE file) |
| `ddc45cc` | **P1.2** | Editor scaffold generator (`agent.py gen-editor-scaffold`) |
| `c9afe95` | **P1.3** | Bug-pattern templates (7 lessons baked in) |
| `4837ad2` | **P1.4** | Sprite + SFX procedural CLI (`gen-sprite-proc`, `gen-sfx`) |
| `accc3d5` | P1.5 (1/2) | 5 system primitives (pawn/enemy/savestate/event-director/inventory) |
| `5469c9b` | P1.5 (2/2) | 3 more primitives (movement-wasd/wave-spawner/audio-bank) |
| `b740690` | **P1.6** | OPQ-007 queued (`.claude/agents/game-*.md` needs OK) |

## Template catalog evolution

Before Phase 1: 3 hard-coded templates in `coder.CSHARP_TEMPLATES` Python dict.

After Phase 1: **15 disk-loaded templates** at `skills/game-dev-agent/templates/cs/`.
Adding a 16th = adding a `.cs.tmpl` file in that dir.  No Python code change.

| Template | Phase | Encodes |
|---|---|---|
| `minimal-monobehaviour` | (was) | basic MonoBehaviour |
| `ui-button-handler` | (was) | UI Button onClick wiring |
| `singleton-manager` | (was) | Instance singleton |
| `audio-throttled-caller` | P1.3 | **lesson #4** — per-frame audio buzz |
| `physics-merger` | P1.3 | **lesson #5+#6** — GetInstanceID + Enter-vs-Stay |
| `singleton-subscriber` | P1.3 | **lesson #7** — Awake/OnEnable race |
| `spawned-entity` | P1.3 | **lesson #8** — justSpawned serialization |
| `pawn-entity` | P1.5 | selectable HP entity (RTS unit / colonist) |
| `enemy-entity` | P1.5 | HP enemy + contact dmg + hit-flash |
| `json-savestate` | P1.5 | persistent-data-path save/load |
| `event-director` | P1.5 | timed random event pool (AI Director) |
| `inventory-resource` | P1.5 | keyed singleton resource bag |
| `movement-wasd` | P1.5 | 8-way WASD/Arrow + optional grid snap |
| `wave-spawner` | P1.5 | escalating-rate horde spawner |
| `audio-bank` | P1.5 | keyed SFX bank with per-key throttle |

## Genre catalog evolution

Before: 3 hard-coded in Python dict.
After: 4 disk-loaded YAMLs at `skills/game-dev-agent/genres/`.

- `rimworld-lite.yaml` (10-role team incl. systems-designer + ai-designer)
- `vampire-survivors-lite.yaml` (10-role team incl. combat-designer + level-designer)
- `suika-game-lite.yaml` (8-role core team)
- `2048-lite.yaml` (8-role core team — added in P1.1 with ZERO code change, demonstrating the principle)

Each genre's `team:` field enumerates which game-dev agents are
activated for that genre.  Currently the agent definitions
themselves are pending operator OK (OPQ-007) since
`.claude/agents/*.md` edits require explicit approval per memory rule.

## What's still missing for "rebuild PawnSim from scratch"

Even with Phase 1 done, a full RimWorld-lite rebuild would still need:
1. Scene composition logic (where pawns spawn, how tilemap is drawn) —
   `SceneSetup.cs` skeleton provides the spot but the actual scene
   construction is per-game.  Could be encoded in genre YAML as a
   "scene_spec" field, future work.
2. The `WeaponUpgrade`, `LevelUpUI`, `PlayerHealth` (VS-lite) class
   bodies — primitives cover the patterns but not the specific game
   logic.  Phase 2 (Claude API) would close this.
3. Sprite asset specifics — Kenney CC0 fetch covers most needs but
   asset SELECTION (which exact tile from a pack) is per-game.

## Phase progress

| Phase | Status |
|---|---|
| **Phase 1** — local refactor (no API) | ✅ DONE (this session) |
| Phase 2 — Claude API natural-language → C# | ⏳ blocked on OPQ-002 (ANTHROPIC_API_KEY) |
| Phase 3 — empirical re-validation | 🔲 pending — next prototype build to measure |

## Operator queue items added this session

- **OPQ-007** — `.claude/agents/game-*.md` 8 game-dev role definitions.
  Memory rule says agent definition edits need OK.  Queued with 4
  options (new files / extend existing / defer / operator restructure).

## What I would NOT do without operator pick

- Touch `.claude/agents/*.md` (OPQ-007).
- Build a new prototype as Phase 3 validation without confirming
  which genre (could be Vampire Survivors lite, 2048 lite, or
  operator's strategic 대항해시대 라이트).
- Migrate PawnSim's existing 17 scripts to use the new templates —
  would risk breaking the verified-PASS build operator is currently
  evaluating.

## Suggested next-session priorities

1. **Phase 3 validation** — pick a small genre (2048 or VS-lite),
   build Day-1 from scratch using ONLY framework commands, measure
   wall-clock vs the 17-minute Suika baseline.  Target: <10 minutes.
2. **OPQ-007 decision** — operator picks option (a/b/c/d) so game-dev
   role SOPs can land.
3. **Optional Phase 4** — migrate one PawnSim system (e.g. AIDirector)
   to use the new `event-director` template as a real-world stress
   test of the primitive.
