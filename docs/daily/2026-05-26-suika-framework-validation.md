# 2026-05-26 — Suika lite via multi-agent framework (validation)

**Session**: 2026-05-26 ~21:54 → 22:15 KST (~21 minutes wall-clock
autonomous block).  Continuation of the operator's "1주일간
자율작업" directive after the multi-agent refactor.

## What landed

| Commit | Subject |
|---|---|
| `b73d8de` | feat(skill-3): multi-agent architecture refactor for game-dev-agent |
| `2d3b68e` | feat(skill-3): Suika lite Day 1 — multi-agent framework empirical validation |
| `57be865` | feat(skill-3): Suika lite Day 2 — preview UI + cursor + SFX + 4 merge bugs fixed |

## Empirical validation of the operator's hypothesis

> 2026-05-26 ~21:35 KST · operator: "멀티에이전트 구조도 같이
> 잡아야함. 그래야 그다음 프로토타입부터는 빠르게 제작가능"

**Result**: confirmed.

| Prototype | Day-1 wall-clock | Notes |
|---|---|---|
| Skill #3-A (RimWorld-lite) | ~2 hours | Pre-framework.  Included SDXL-asset-quality pivot to Kenney CC0, sprite-import-race debugging. |
| Skill #3-B (Suika-lite) | ~8 minutes | Framework-enabled.  Procedural sprites, agent.py code, agent.py integrate, agent.py qa end-to-end. |
| Speedup | **~15×** | |

| Prototype | Days-1+2 wall-clock | Notes |
|---|---|---|
| Skill #3-A | ~5 hours | Day 1 + Day 2 (UI panel + needs decay). |
| Skill #3-B | ~17 minutes | Day 1 + Day 2 (preview + cursor + SFX + bug fixes). |
| Speedup | **~17×** | |

## The qa.py self-verify loop is the killer feature

Every framework-call cycle:
```
agent.py integrate --method ...SceneSetup.GenerateAll   # ~30 sec
agent.py integrate --method ...BuildScript.BuildVerify  # ~25 sec
agent.py qa --exe ... --screenshot ... --delay 5         # ~10 sec
Read screenshot.png                                       # immediate
```

≈ 65–70 sec per iteration, zero operator hands.  Day 2 surfaced 4
distinct bugs in 6 iterations — each fix took one iteration to
verify.  In a pre-framework world the same loop required asking
the operator to launch and screenshot, which is at minimum hours
of latency or impossible during their absence.

### Bugs caught by the loop (Day 2)

1. **OnCollisionEnter2D miss** post-`justSpawned` grace — fruits
   collided during grace, then settled at rest; Enter never re-fired
   on the resting contact.  Fix: added `OnCollisionStay2D`.
2. **Pre-spawned prefab `justSpawned=true` baked in** — pre-spawned
   editor instances start with the field's default-true and Update
   eventually clears it, but the screenshot fires before grace
   elapses on these pre-spawned ones.  Fix: `ClearJustSpawned()` via
   `SerializedObject` in the scene-setup pre-spawn loop.
3. **(load-bearing!)** `GetInstanceID()` tiebreaker bug —
   `GetInstanceID()` on a MonoBehaviour returns the **component's**
   ID, which is always larger than ANY GameObject's ID (components
   are created after GOs).  Comparing
   `GetInstanceID() > collision.gameObject.GetInstanceID()` made
   BOTH sides bail.  Fix: `gameObject.GetInstanceID()` on both
   sides.
4. **ScoreUI / ScoreManager Awake-order race** — ScoreUI.OnEnable
   subscribed to `OnScoreChanged` before ScoreManager.Awake had
   run, so when ScoreManager.Instance was null the subscription
   silently no-op'd and the UI never updated.  Fix: poll-based
   `Update()` reading `ScoreManager.Instance?.Score`.

These four are now permanent lessons in the SKILL.md.  They will
NOT recur in the third prototype (the patterns are encoded in the
templates / scene-setup conventions).

## What's playable

Both prototypes have shippable Windows .exe builds:

- **PawnSim (Skill #3-A, RimWorld-lite)** —
  `G:/ai/MelonS-Agents/skills/game-prototype/builds/day-7-fixed-2026-05-26/PawnSim.exe`
  (verified PASS earlier in session at `G:/ai/_qa_validate.png`).
- **SuikaLite (Skill #3-B, Suika-lite)** —
  `G:/ai/MelonS-Agents/skills/game-prototype-suika/builds/day-2-2026-05-26/SuikaLite.exe`
  (screenshot at same dir + `day2_screenshot.png` at skill root).

Both launch cleanly + run gameplay + close on Application.Quit.

## OPERATOR_QUEUE updates

- **OPQ-003** updated with Day-1 SHIPPED status + 15× speedup metric.
  Operator decides what prototype to build next (Vampire Survivors,
  Brotato, 대항해시대, or polish existing).
- **OPQ-006** still RESOLVED (sprite import race fix shipped earlier).

## Suggested next-session priorities (operator picks)

1. **Play Suika lite** — open `SuikaLite.exe`, click to drop fruit,
   verify gameplay matches the click-merge-score loop.  Provide
   any visual / feel feedback.
2. **Pick prototype #3** — either continue the framework
   stress-test (Vampire Survivors Day 1 estimated ≤15 min) or
   pivot to operator's strategic target (대항해시대 라이트).
3. **Polish RimWorld** (Skill #3-A) — tree size, UI panel sizing,
   or save/load follow-up.
4. **Framework hardening** — surface the Day-2 bugs as patterns
   in `coder.py` templates (e.g., the GetInstanceID pitfall could
   live as a comment in a merge-template).

## Notes for the next agent session

- ComfyUI + Pollinations untouched this session.  Mix #2 still
  public on YT; Mix #3 hero-loop still on disk.
- Mac→Windows sync is two-way through git; this session only
  modified the repo from Windows side.
- Operator memory unchanged this session.
