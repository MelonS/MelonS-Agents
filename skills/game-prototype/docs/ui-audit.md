# PawnSim UI/UX Full Audit + Unified Layout Spec

Date: 2026-05-30. Author: Game Director (read-only pass over all UI code + `_ui_check.png` + `genre-comparison.md` Dim 6).
Operator trigger: "UI쪽 이상해, 전체적으로 다시 체크" — autonomous waves each self-positioned a UI system with NO unified layout owner → clutter / overlap / inconsistency.

> **2026-06-10 현행화**: §2 의 P1(멈춤 슬롯)·P3(지정 토글)·P4(기즈모바)는 해결 확인 — 각 항목에
> RESOLVED 표기.  §3.1/§3.2/§3.3/§3.5 는 이후 랜딩된 변경(자원 좌상단 이동 4af6ae4, 속도 우하단
> 분리, 지정 토글의 ArchitectMenu 흡수, #232 기즈모바 비활성, 우상단 밴드 계약 #ui백로그 7.0)으로
> 재기술됨.  전수 재검토 결과는 `ui-backlog-2026-06-10.md` (82건) 참조.

This document is the SINGLE SOURCE OF TRUTH for PawnSim screen layout. Every UI fix subtask must read it and conform. It does two things:
1. **Audit** — every concrete problem found, with file + line evidence.
2. **Layout spec** — the one canonical the reference sim-convention layout all panels must obey, with exact anchors / offsets / a shared bottom-bar geometry contract that ends the position guessing.

---

## 1. Root cause

There is **no layout owner**. The screen is assembled by ~10 independent scripts, most self-bootstrapping (`[RuntimeInitializeOnLoadMethod]`), each picking its own anchor + a HARD-CODED pixel offset that silently assumes the size/position of siblings it never measures.

The bottom-center is the disaster zone. Five systems all anchor `(0.5, 0)` bottom-center and lay themselves out with magic numbers:

| System | File | Anchor | Offset | Width |
|---|---|---|---|---|
| Main control bar (9 buttons) | `GuiControlBar.cs` | (0.5,0) | y=40 | `9*76 + 4*4 + 4*16 = 764`px + padding → ~**790px wide** (half-width ≈ 395) |
| Deconstruct toggle (해체) | `DeconstructDesignation.cs:546` | (0.5,0) | x=**−360**, y=104 | 96px |
| Mine toggle (채광) | `MineDesignation.cs:392` | (0.5,0) | x=**+360**, y=104 | 96px |
| Grow toggle (경작) | `GrowZoneDesignation.cs:695` | (0.5,0) | x=**+462**, y=104 | 96px |
| Selection gizmo bar (징집/취소) | `SelectionGizmoBar.cs:287` | (0.5,0) | y=16 | ~216px |

**Consequences (all confirmed in `_ui_check.png`):**
- The control bar's half-width is ~395px, so the 해체 toggle at x=−360 and 채광 at x=+360 sit basically ON the bar's left/right ends, not clear of it. The screenshot shows 채광(M)/경작(R)... wait — the screenshot bottom-right shows "재배(M)" + "경작(R)"-style toggles floating loose at the far-right with NO shared panel, visibly detached from the main bar and at a different vertical band.
- `SelectionGizmoBar` (y=16) is directly UNDER / overlapping the main control bar (y=40) — both bottom-center. When a pawn is selected the 징집/취소 gizmo renders on top of / behind the speed buttons.
- The three designation toggles are at y=104 (a third vertical band), the gizmo at y=16, the main bar at y=40 → three different bottom rows that don't align to any grid.

This is the "끊임없이 파이프라인 최적화" failure mode for UI: each wave was file-isolated (good for merge safety) but NOBODY owned the composite result, so positions are guesses layered on guesses.

---

## 2. Concrete problems (the audit)

### P1 — Bottom-bar leftmost reads "없음(0)" instead of a clean pause control  **[HIGH]**  ✅ RESOLVED (속도 클러스터 최좌측=멈춤(Space) 복구, mode-readout 코드 제거 확인 2026-06-10)
`_ui_check.png` bottom bar reads: `없음(0) | 1x | 2x | 4x | 징집 | 작업 | 일정 | 건축 | 연구`.

- `GuiControlBar.cs` builds the leftmost slot as `MakeBtn("멈춤", "(Space)", …)` (line 123) and the tab as `"직업"` (line 134). The screenshot shows `없음(0)` and `작업` instead.
- `없음(0)` is a **BuildManager mode readout** ("현재 모드: 없음", count 0) that an earlier wave jammed into the leftmost bar slot — it is NOT the pause button. A mode-indicator label is masquerading as the first command button.
- DELTA: the screenshot is from a build where the first slot was a build-mode indicator, not `멈춤`. Whether it is a stale capture or a live stray label, the bar's leftmost element MUST be the pause control, never a mode-readout string like "없음(0)". The build-mode active state belongs on the **건축** button highlight (already done at `GuiControlBar.cs:244 RefreshBuildHighlight(architectBtn, …)`), not as a separate left-edge text.
- FIX OWNER: `GuiControlBar.cs` (and verify no other script writes a "없음"/mode-count Text into the bottom bar). The leftmost slot is `멈춤(Space)`, full stop. If a build-mode/selected-count readout is wanted it goes elsewhere per §3 (not the bottom command bar's first slot).

### P2 — Top resource bar: icons + readouts loosely scattered, panel treatment regressed  **[HIGH]**
`_ui_check.png` top bar: `석재: 0   목재: 55   식사: 2   식량:` — readouts float with crate-like icons spaced unevenly, and the right cluster reads as loose text on the background rather than a contained, bordered top bar.

- `SceneSetup.Game.TopBar.cs` DOES wrap the bar in `MakeBorderedPanel` (line 35) with `HeaderBg` — so a panel exists. But the resource readouts are positioned with a fragile chain of hand-tuned magic offsets: `−16, −164, −188, −336, −360, −508, −532` (lines 91-98) plus per-icon math `anchoredX − kLabelWidth − kIconGap` (line 177) where `kLabelWidth=96` is a GUESSED width of the Korean string "식량: 0". When the number grows to 2-3 digits the label widens and the icon no longer "hugs" its number — exactly the "icons next to the wrong number / ambiguous" symptom the code's own comment (lines 142-147) admits to and never truly fixed.
- The four readouts use four DIFFERENT hand-picked colors (olive, gold-ish, tan, grey) but no shared "resource chip" container — each is naked right-aligned text with a separate icon Image floating to its left. There is no per-resource grouped cell, so spacing reads random.
- FIX OWNER: `SceneSetup.Game.TopBar.cs`. Replace the magic-offset chain with a right-anchored **HorizontalLayoutGroup** (or evenly-pitched cells) of identical "icon + value" chips, one per resource, equal width, equal gap, so adding a digit can't desync the icon. Keep the bordered HeaderBg panel. Keep the `ResourceCounterUI` SerializedObject wiring (it binds by reference, not name) and keep the Text object names (`WoodText/FoodText/MealsText/StoneText`) and icon names (`ResIcon_<key>`).

### P3 — Designation toggles (해체/채광/경작) float at conflicting anchors, no shared panel  **[HIGH]**  ✅ RESOLVED (2026-05-31 운영자 fb — standalone 토글 제거, ArchitectMenu 지시/구역 카테고리로 흡수. 핫키 M/X/P 보존)
Three buttons, three files, three magic X offsets (−360 / +360 / +462) all at y=104, none measuring the actual control-bar width.

- They are conceptually part of the **command/architect bar** (the reference sim: designation tools live in the architect/command cluster, not floating mid-air).
- `_ui_check.png` confirms they render as loose, unconnected buttons at the bottom-right, detached from the main bar, no enclosing frame.
- Each builds its toggle independently (`EnsureToggleButton` in all three) with copy-pasted geometry.
- FIX OWNER: the **designation cluster** — `MineDesignation.cs` / `GrowZoneDesignation.cs` / `DeconstructDesignation.cs`. They must STOP using personal magic offsets and instead place themselves into ONE shared designation row defined by the layout contract in §3 (a left-anchored or bar-adjacent group), so the three sit in a single bordered strip with consistent gaps. They keep their `Btn_해체`/`Btn_채광`/`Btn_경작` names.

### P4 — SelectionGizmoBar overlaps the main control bar  **[HIGH]**  ✅ RESOLVED (#232 운영자 fb — SelectionGizmoBar 통째 비활성. 파일은 보존)
`SelectionGizmoBar.cs:287` anchors bottom-center y=16; `GuiControlBar` is bottom-center y=40. The gizmo (징집/취소, ~216px wide) renders on top of / immediately under the speed buttons whenever a pawn is selected. Two bottom-center bars stacked 24px apart = visual collision.

- the reference sim convention: the contextual gizmo row sits ABOVE the persistent command bar, clearly separated, and only appears on selection.
- FIX OWNER: `SelectionGizmoBar.cs`. Re-anchor to the dedicated gizmo band defined in §3 (a row sitting clearly ABOVE the main command bar with a gap, not 24px into it). Keep `Btn`-style behavior; no test references its GO names (verify), but preserve `DraftBtn`/`CancelBtn` names.

### P5 — Two competing "selected object" inspectors with inconsistent empty-state copy  **[MED]**
- `EntityInspectorPanel.cs` anchors RIGHT-center `(1,0.5)` 360×380, empty title "선택된 오브젝트 없음" (line 82/124).
- `PawnInfoPanel.cs` anchors bottom-LEFT (per SceneSetup), empty hint "오브젝트를 선택하세요" (line 87).
- `_ui_check.png` shows the right-center panel reading "선택한 오브젝트 없음".
- So there are TWO inspectors, on two different edges, and `EntityInspectorPanel.Describe()` (line 137) even DUPLICATES pawn info that `PawnInfoPanel` already shows on the left — by design ("#128 일관성") but in practice it means selecting a pawn lights up BOTH a left panel AND a right panel with overlapping data. Cluttered.
- Copy is inconsistent ("선택된" vs "선택한" vs "선택하세요").
- FIX OWNER: the **inspector** lane — `EntityInspectorPanel.cs` + `PawnInfoPanel.cs`. Per §3: ONE inspector edge. the reference sim puts the selected-thing inspector bottom-LEFT. Decision (§3.4): keep `PawnInfoPanel` bottom-left as the pawn inspector; keep `EntityInspectorPanel` right side ONLY for non-pawn entities (trees/walls/veins) and STOP it re-describing pawns (let the left panel own pawns) to kill the double-panel. Unify empty-state copy to one string. Both share the bordered-panel style (they already call UITheme; verify border edges present).

### P6 — Inconsistent canvas sort orders / multiple canvases  **[MED]**
AlertStackUI canvas sortingOrder=200, SelectionGizmoBar=200, HotkeyCheatSheet=250. The designation toggles + GuiControlBar live on the SHARED scene Canvas (whatever its sort is). FloatingText is world-space sortingOrder 50. This is mostly fine, but the gizmo bar (200) and the main bar (scene canvas) being on DIFFERENT canvases means their relative draw order isn't guaranteed across resolutions — contributing to the P4 overlap reading badly. Document the intended z-stack (§3.6) so future waves don't invent new sort numbers.

### P7 — Bottom-left save/load (S/L) buttons are bare and tiny  **[LOW]**
`SceneSetup.UI.cs CreateIconButton` builds 40×40 S/L buttons. `_ui_check.png` bottom-left shows two tiny "S" "L" glyphs with minimal framing. They DO now use the bordered-panel button style (U8, lines 78-103) so style is OK, but they sit alone with no group label and are easy to miss. LOW priority — they conform to style; just confirm they're inside the layout's bottom-left zone and not overlapped by the inspector.

### P8 — Floating-world elements are fine; do not touch for layout  **[INFO]**
`PawnNameLabel.cs`, `PawnFloatingBars.cs`, `FloatingText.cs` are WORLD-SPACE (TextMesh / SpriteRenderer), not screen UI. Their stacking (name 0.98 > status 0.80 > HP 0.68 > mood 0.55 > head 0.5) is internally consistent and point-filtered. They are NOT part of the screen-layout overlap problem. The only screen-relevant note: keep their sortingOrders out of the UI canvas range — they already are (29-50 world vs 200+ overlay). No layout change; this lane only ensures internal consistency (plate fits text, no drift).

---

## 3. UNIFIED LAYOUT SPEC (the contract)

All screen UI uses the 1920×1080 reference (CanvasScaler ScaleWithScreenSize, match 0.5 — already standard across the self-boot canvases). All panels use `UITheme.MakeBorderedPanel` (Divider border + PanelBg/HeaderBg fill), `UITheme.LoadKoreanFont`, `PadOuter=12`, `RowGap=6`, `BorderPx=2`. No script invents its own colors/fonts/padding.

### 3.1 TOP — date + speed readout (full-width header)  *(2026-06-10 재기술)*
- Anchor: top, full-width. `HeaderBg` bordered panel, **height 76**.
- LEFT: ClockUI 날짜 ("봄 N일, YYYY년" — gold, fontSize 32, **horizontalOverflow=Overflow, 폭 380** — #ui백로그 2.0).
- RIGHT: TimeUI ("▶ 1x") — 우하단 속도 버튼과 중복 표시 문제는 백로그 2.3 참조.
- 자원 readout 은 상단바가 아니라 **좌상단 세로 칩 리스트** (`ResourceCounterUI`, RimWorld ResourceReadout 모사, 4af6ae4). 식량→식사→목재→석재, 칩 = [아이콘][값].

### 3.2 BOTTOM — the command stack (three clearly separated bands)
This is the contract that ends the bottom-center collision. Define ONE shared geometry so every bottom system places relative to it, not relative to guesses.

Reference bottom-up bands (y = anchored px above screen bottom, anchor (0.5,0) unless noted):
- **Band A — 탭 바** (`GuiControlBar`): anchor (0.5,0) y=24, 중앙. [징집|직업|일정|건축|연구|⚙설정]
  6버튼, 폭 = 6*76 + **5***16 갭 (#ui백로그 0.3).  **속도/시계 클러스터는 분리** — 우하단
  anchor (1,0) x=-16 y=24 에 [⏸멈춤|1x|2x|4x] + 시계 (RimWorld 'Time speed control — Bottom
  right corner' 컨벤션 일치).  *(2026-06-10 재기술 — 종전 '한 바에 속도+탭' 기술은 폐기)*
- **Band B — 지정 토글**: ~~standalone strip~~ → **ArchitectMenu 지시/구역 카테고리 소속**
  (2026-05-31).  하단에 standalone 지정 버튼을 만들지 말 것.
- **Band C — 기즈모바**: **disabled (#232)** — 재활성 시 y=112 / sort 150 준수.
- **우하단 위 밴드**: ResearchStrip (1,0) x=-16 **y=112** 420×36 (#ui백로그 5.1 — 속도 패널
  y24~96 위 16px 갭).

### 3.3 Designation-row shared origin — **OBSOLETE** (토글이 ArchitectMenu 로 흡수됨. 아래 수식은 역사 기록)
The three designation managers MUST NOT each pick a personal X. Define the row in the designation lane: place a left-anchored container (or compute each button's X from a shared base):
- base anchor (0,0), x0 = 16 (left edge inset), y = 24.
- 해체 at x0, 채광 at x0 + (96+4), 경작 at x0 + 2*(96+4). All anchor (0,0), pivot (0,0).
- Result: a tidy left-edge group of three equal toggles, never near the centered command bar. (If the lane prefers one owner builds the strip frame and the other two parent into it, that is allowed AS LONG AS file isolation per §4 holds — simplest correct version: each computes its own X from the shared `x0 + i*(w+gap)` formula above with i = 0/1/2 fixed per file, so no cross-file dependency and no overlap.)

### 3.4 RIGHT — inspector
- Selected-thing inspector. the reference sim puts it bottom-left, but PawnSim already splits: pawn inspector bottom-LEFT (`PawnInfoPanel`), entity inspector right-center (`EntityInspectorPanel`).
- Rule: **pawns → left panel only; non-pawn entities → right panel only.** `EntityInspectorPanel.Describe()` must STOP returning pawn descriptions (remove the `PawnEntity` branch at lines 137-160 so it no longer double-shows pawn data the left panel owns). Right panel hint when nothing selected: keep one consistent empty string.
- Both panels: bordered (`MakeBorderedPanel` / border edges), gold bold header, cream body, PadOuter padding. Unify empty-state copy to exactly **"선택된 오브젝트 없음"** (pick one; this one matches EntityInspectorPanel's existing title) across both panels.

### 3.5 TOP-RIGHT — 우상단 컬럼 밴드 계약  *(2026-06-10 #ui백로그 7.0 — 5개 시스템 좌표 계약)*

| 밴드 | 시스템 | y (anchored, anchor(1,1)) | 비고 |
|---|---|---|---|
| TopBar | (헤더) | 0 ~ -76 | height 76 |
| 알림 카드 | `AlertStackUI` | **-88 ~ -232** | maxCards **3** (44px + 6 gap), 밴드 예약 |
| 이벤트 로그 | `EventLogPanel` | **-244 ~ -404** | 240×160 |
| 빌드 토스트 | `BuildClickToast` | **-416 ~ -454** | 38px |
| 자원 부족 경고 | `ResourceLowAlert` | **-462 ~ -526** | 64px |

- `ThreatAlertUI` 는 비활성 (#ui백로그 7.2 — AlertStack 카드와 이중 표시였음. 파일 보존, 부트 제거).
- 새 우상단 요소는 이 테이블에 행을 추가하고 아래 밴드부터 쌓을 것 — 개인 y 오프셋 발명 금지.

### 3.6 Z-stack (canvas sort order) — canonical, do not invent new numbers
- World sprites/pawns: 0–11. Floating bars: 29–31. FloatingText: 50. (world space, untouched)
- Scene HUD canvas (TopBar, GuiControlBar, designation toggles, inspectors, S/L): scene Canvas default (~0/100). 
- Contextual gizmo bar: **150** (above HUD, below alerts/cheatsheet). (Currently 200 — lower it to 150 so the cheat-sheet and alerts stay on top.)
- Alert stack: **200**.
- Hotkey cheat-sheet (modal overlay): **250**. (already)

### 3.7 BOTTOM-LEFT — save/load
- S/L buttons bottom-left, bordered-button style (already U8). Keep at the very bottom-left corner, clear of the designation row (which starts at x=16,y=24); place S/L at x=16, y=4 OR move designation row up if they collide. Confirm no overlap with the left inspector panel (which is higher up the left edge).

---

## 4. Fix lanes (file-disjoint)

| Lane | Files (exclusive) | Mandate |
|---|---|---|
| **gui-bar** | `Assets/Scripts/GuiControlBar.cs` | P1: leftmost = 멈춤, kill any "없음(0)" mode-readout in the bar; set Band-A geometry y=24 per §3.2. |
| **top-bar** | `Assets/Editor/SceneSetup.Game.TopBar.cs` | P2: replace magic-offset resource chain with even right-anchored chips per §3.1. |
| **inspector** | `Assets/Scripts/EntityInspectorPanel.cs`, `Assets/Scripts/PawnInfoPanel.cs` | P5: pawns→left only, entities→right only, unify empty copy, confirm borders per §3.4. |
| **designation-cluster** | `Assets/Scripts/MineDesignation.cs`, `Assets/Scripts/GrowZoneDesignation.cs`, `Assets/Scripts/DeconstructDesignation.cs`, `Assets/Scripts/SelectionGizmoBar.cs`, `Assets/Scripts/AlertStackUI.cs` | P3+P4+P6+§3.5: designation toggles into the left-edge shared row (§3.3); gizmo bar to Band C y=112 sort 150; alert stack top-inset −72. |
| **float** | `Assets/Scripts/PawnNameLabel.cs`, `Assets/Scripts/PawnFloatingBars.cs` | P8: world-space only; verify plate fits text + no drift; NO screen-layout change. (Lowest priority — likely already fine; touch only if a concrete defect remains.) |

Lane rules (all): read THIS file; stay in your files; do NOT run Unity; write `.claude/wb/<label>.json`; no `// TODO`; preserve every test-referenced `Btn_` GameObject name (`Btn_멈춤`, `Btn_1x`, `Btn_2x`, `Btn_4x`, `Btn_징집`, `Btn_직업`, `Btn_일정`, `Btn_건축`, `Btn_연구`, `Btn_해체`, `Btn_채광`, `Btn_경작`) and the resource Text names (`WoodText/FoodText/MealsText/StoneText`) + `ResIcon_<key>`.

---

## 5. Verdict
NUDGE → the system is cohesive in STYLE (UITheme is good and widely adopted) but BROKEN in COMPOSITION (no layout owner → bottom-center overlap, top-bar drift, double inspector, stray "없음(0)"). Not a REJECT (don't rebuild the theme); a targeted re-layout against this contract. Five file-disjoint lanes, no Unity run required.
