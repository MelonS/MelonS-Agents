using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using MelonS.GameProto;
using MelonS.GameProto.Core;

namespace MelonS.GameProto.EditorTools
{
    // R10j - SceneSetup.cs TopBar (ClockUI + TimeUI + Wood/Food/Meals resource counters) extract.
    //   원본 SceneSetup.cs L334-479 (~145 LOC).
    public static partial class SceneSetup
    {
        private static void GenerateTopBar(
            GameObject canvasGo, Color colPanel, Color colTextPrimary, Color colTextMuted,
            Color colAccentFood, Color colAccentWood, Font uiFont)
        {
            GameObject topBarGo = new GameObject("TopBar");
            topBarGo.transform.SetParent(canvasGo.transform, false);
            RectTransform topRt = topBarGo.AddComponent<RectTransform>();
            topRt.anchorMin = new Vector2(0f, 1f);
            topRt.anchorMax = new Vector2(1f, 1f);
            topRt.pivot = new Vector2(0.5f, 1f);
            // Day 39: 1920 ref 기준 topbar 48px (이전 32은 800 ref 기준이라 너무 작음)
            // 운영자 피드백 polish: font 22→28 키움 → topbar 도 48→60 확보
            // 시각 QA(2026-06-01): 아이콘/라벨이 작아 가독성 부족 → bar 60→76 확보해
            //   아이콘 48px + 폰트 32px 가 세로로 충분히 들어가게.
            topRt.sizeDelta = new Vector2(0, 76);
            topRt.anchoredPosition = new Vector2(0, 0);

            // #UI-restyle U4 (Round 5) — bring the top bar onto the global bordered-panel
            //   system (same MakeBorderedPanel the control bar / inspector use): warm-brown
            //   HeaderBg fill + Divider border.  The bar spans full width and anchors to the
            //   top, so the border's BOTTOM edge reads as the bar's defined bottom rule.
            //   We parent every readout to the returned inner Content RT so text sits inside
            //   the border (the SerializedObject text refs below are unaffected — they bind
            //   by component reference, not by name/path).
            RectTransform topContent = UITheme.MakeBorderedPanel(topRt, UITheme.BorderPx, UITheme.HeaderBg);
            // Use the canonical UITheme font + colors instead of the legacy passed-in Color args.
            // 시각 QA: 폰트 28→32 로 키워 라벨 가독성 확보 (bar 76px 안에 여유).
            uiFont = UITheme.LoadKoreanFont(32);
            colTextPrimary = UITheme.TextPrimary;
            colTextMuted   = UITheme.Divider;          // separators tint with the border tone
            colAccentWood  = UITheme.AccentTan;         // 목재 → warm tan
            colAccentFood  = new Color(0.478f, 0.604f, 0.302f, 1f);  // 식량 → olive-green (matches food bars)
            GameObject parent = topContent.gameObject;

            // TopBar LEFT - ClockUI "Day 1 - 06:00"  (gold clock readout, matches panel titles)
            GameObject clockGo = new GameObject("ClockUI");
            clockGo.transform.SetParent(parent.transform, false);
            Text clockText = clockGo.AddComponent<Text>();
            clockText.text = "Day 1 - 06:00";
            clockText.font = uiFont;
            clockText.fontSize = 32;
            clockText.fontStyle = FontStyle.Bold;
            clockText.color = UITheme.AccentGold;   // clock is the "title" of the bar → gold
            clockText.alignment = TextAnchor.MiddleLeft;
            RectTransform clockRt = clockGo.GetComponent<RectTransform>();
            clockRt.anchorMin = new Vector2(0f, 0f);
            clockRt.anchorMax = new Vector2(0f, 1f);
            clockRt.pivot = new Vector2(0f, 0.5f);
            clockRt.sizeDelta = new Vector2(220, 0);
            clockRt.anchoredPosition = new Vector2(16, 0);
            clockGo.AddComponent<ClockUI>();

            // TopBar RIGHT - TimeUI "▶ 1x" (속도 표시기).  #41 로 자원을 좌상단으로 옮긴 뒤
            //  상단바 우측이 비어 있었음 → TimeUI 자체 주석의 원래 의도("top-right speed
            //  indicator")대로 우측으로 옮겨 바를 균형(날짜 좌 ↔ 속도 우)있게 채운다.
            GameObject timeGo = new GameObject("TimeUI");
            timeGo.transform.SetParent(parent.transform, false);
            Text timeText = timeGo.AddComponent<Text>();
            timeText.text = "▶ 1x";
            timeText.font = uiFont;
            timeText.fontSize = 32;
            timeText.fontStyle = FontStyle.Bold;
            timeText.color = colTextPrimary;
            timeText.alignment = TextAnchor.MiddleRight;
            RectTransform timeRt = timeGo.GetComponent<RectTransform>();
            timeRt.anchorMin = new Vector2(1f, 0f);
            timeRt.anchorMax = new Vector2(1f, 1f);
            timeRt.pivot = new Vector2(1f, 0.5f);
            timeRt.sizeDelta = new Vector2(220, 0);
            timeRt.anchoredPosition = new Vector2(-16, 0);
            timeGo.AddComponent<TimeUI>();

            // ui-audit P2 / §3.1 — RIGHT resource chips, layout-driven (no magic X chain).
            //   Root cause of the old code: each readout was placed by a hand-tuned
            //   anchoredX (-16/-164/-188/-336/-360/-508/-532) and each icon by
            //   (anchoredX - kLabelWidth - kIconGap) where kLabelWidth=96 was a GUESSED
            //   pixel width of the Korean string.  When a value grew to 2-3 digits the
            //   real text widened past 96px, so the icon no longer hugged its number and
            //   the next divider drifted into the neighbouring icon — the "icon next to
            //   the wrong number" symptom the file's own comment admitted to.
            //
            //   §3.1 fix: a RIGHT-anchored HorizontalLayoutGroup of N IDENTICAL chips,
            //   one per resource, order food → meal → wood → stone.  Each chip is a
            //   self-contained [icon][value] cell of equal width (kChipW≥150) and equal
            //   gap (kChipGap), with a thin Divider drawn between cells by the layout.
            //   Because the layout group sizes/positions every cell, a value growing a
            //   digit can NEVER desync the icon from its number or shove a divider into a
            //   neighbour: the icon lives INSIDE its own chip, left-anchored, the value
            //   fills the rest of the same chip.  No per-resource anchoredX exists anymore.
            //   Preserved for wiring/tests: Text names FoodText/MealsText/WoodText/StoneText,
            //   icon names ResIcon_<key>, and the ResourceCounterUI SerializedObject refs
            //   (which bind by component reference, not by name/path).
            // 시각 QA: 아이콘 48px + 폰트 32 라벨이 한 칩에 안 겹치게 156→184 로 확장.
            const float kChipW   = 184f;  // equal cell width, holds 48px icon + "식량: NNN"

            // #41 운영자 "자원표시는 좌상단에 RimWorld처럼": 상단바 우측 가로행 → 캔버스
            //   좌상단(상단바 아래) 세로 목록으로 이전(RimWorld ResourceReadout 정합).
            //   캔버스에 직접 붙여 바 영역 밖(맵 위)에 떠 있게 한다.  반투명 배경 + 세로 스택.
            GameObject resRowGo = new GameObject("ResourceRow");
            resRowGo.transform.SetParent(canvasGo.transform, false);
            RectTransform resRowRt = resRowGo.AddComponent<RectTransform>();
            resRowRt.anchorMin = new Vector2(0f, 1f);
            resRowRt.anchorMax = new Vector2(0f, 1f);
            resRowRt.pivot = new Vector2(0f, 1f);
            resRowRt.sizeDelta = new Vector2(kChipW + 12f, 0);  // 한 열 폭(+패딩), 높이는 레이아웃이 결정
            resRowRt.anchoredPosition = new Vector2(8f, -(76f + 8f));  // 좌상단, 상단바(76) 아래
            // 반투명 어두운 배경(맵 위에서 가독성).
            var resBg = resRowGo.AddComponent<Image>();
            resBg.color = new Color(0.07f, 0.07f, 0.09f, 0.62f);
            resBg.raycastTarget = false;
            var vlg = resRowGo.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.spacing = 4f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(6, 6, 6, 6);
            // 높이를 자식(칩 4개)에 맞춰 자동 조절.
            var resFitter = resRowGo.AddComponent<ContentSizeFitter>();
            resFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            resFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // Chips in screen order (left→right): food → meal → wood → stone.
            // 시각 QA(2026-06-01): 식사(pale-gold)/목재(tan) 가 비슷해 구분 약함 → 식사를
            //   따뜻한 amber-orange 로, 목재를 좀 더 진한 갈색으로 바꿔 4색 대비 명확화.
            Text foodText  = MakeResChip(resRowGo, "FoodText",  "식량: 0", "food",  uiFont, new Color(0.52f, 0.74f, 0.34f, 1f),          kChipW); // 선명 녹색
            Text mealsText = MakeResChip(resRowGo, "MealsText", "식사: 0", "meal",  uiFont, new Color(0.98f, 0.66f, 0.24f, 1f),          kChipW); // amber-orange
            Text woodText  = MakeResChip(resRowGo, "WoodText",  "목재: 0", "wood",  uiFont, new Color(0.74f, 0.52f, 0.30f, 1f),          kChipW); // 진한 목재 갈색
            // #119 - 석재 (밝은 청회색 — 갈색 계열과 확실히 분리)
            Text stoneText = MakeResChip(resRowGo, "StoneText", "석재: 0", "stone", uiFont, new Color(0.70f, 0.76f, 0.84f, 1f),          kChipW);

            // ResourceCounterUI host (no longer has its own panel image; just script)
            GameObject resHostGo = new GameObject("ResourceCounter");
            resHostGo.transform.SetParent(canvasGo.transform, false);
            resHostGo.AddComponent<RectTransform>();
            ResourceCounterUI resCounter = resHostGo.AddComponent<ResourceCounterUI>();
            SerializedObject rcSo = new SerializedObject(resCounter);
            rcSo.FindProperty("woodText").objectReferenceValue = woodText;
            rcSo.FindProperty("foodText").objectReferenceValue = foodText;
            rcSo.FindProperty("mealsText").objectReferenceValue = mealsText;
            rcSo.FindProperty("stoneText").objectReferenceValue = stoneText;
            rcSo.ApplyModifiedProperties();
        }

        // ui-audit §3.1 — ONE identical "resource chip" cell: [divider][icon][value].
        //   Built as a single GameObject that the parent HorizontalLayoutGroup sizes and
        //   positions; the chip itself uses a nested HorizontalLayoutGroup to keep its own
        //   icon left-anchored and its value filling the remaining width.  Because the icon
        //   and the value live in the SAME fixed-width cell, a value growing a digit pushes
        //   nothing out of alignment and can never reach a neighbouring chip's icon.
        //
        //   The icon keeps name "ResIcon_<key>" and the value keeps the caller-supplied
        //   Text name (FoodText/MealsText/WoodText/StoneText) so existing Find()/test refs
        //   and the ResourceCounterUI SerializedObject wiring are untouched.
        private static Text MakeResChip(GameObject row, string textName, string label,
                                        string iconKey, Font uiFont, Color col, float chipW)
        {
            // 시각 QA(2026-06-01): 36px 아이콘이 캡처 해상도에서 ~10px 로 작게 보임 → 48px 로 확대.
            const float kIconPx = 48f;   // distinct pictogram at 1920+ capture res (36→48 가독)
            const float kIconGap = 8f;   // breathing room between icon and first glyph
            const float kDividerW = 2f;  // thin Divider rule on the chip's leading edge

            // Chip cell — a fixed-width container the parent layout treats as one element.
            GameObject chipGo = new GameObject($"ResChip_{iconKey}");
            chipGo.transform.SetParent(row.transform, false);
            RectTransform chipRt = chipGo.AddComponent<RectTransform>();
            var chipLe = chipGo.AddComponent<LayoutElement>();
            chipLe.preferredWidth = chipW;       // equal width for every chip
            chipLe.minWidth = chipW;
            // #41 세로 목록(좌상단 RimWorld식)에서도 각 칩이 높이를 가지도록 명시.
            //  가로 바에선 부모의 childForceExpandHeight 가 우선하므로 영향 없음.
            chipLe.preferredHeight = 42f;
            chipLe.minHeight = 38f;
            var chipLayout = chipGo.AddComponent<HorizontalLayoutGroup>();
            chipLayout.childAlignment = TextAnchor.MiddleLeft;
            chipLayout.spacing = kIconGap;
            chipLayout.childControlWidth = true;
            chipLayout.childControlHeight = true;
            chipLayout.childForceExpandWidth = false;
            chipLayout.childForceExpandHeight = true;

            // Leading Divider rule — separates this chip from the one to its left,
            //   matching the control bar's group lines (§3 shared style).
            GameObject divGo = new GameObject($"ResSep_{iconKey}");
            divGo.transform.SetParent(chipGo.transform, false);
            divGo.AddComponent<RectTransform>();
            var divImg = divGo.AddComponent<Image>();
            divImg.color = UITheme.Divider;
            divImg.raycastTarget = false;
            var divLe = divGo.AddComponent<LayoutElement>();
            divLe.preferredWidth = kDividerW;
            divLe.minWidth = kDividerW;
            divLe.preferredHeight = 44f;   // bar 76px 에 맞춰 divider 도 32→44 키움

            // ICON SLOT — left-anchored inside the chip, fixed 36px.
            //   Load Assets/Sprites/icon_<key>.png (point-filtered, force-imported by
            //   ForceImportAllSprites); map is 1:1 — iconKey stone/wood/meal/food.
            //   Named "ResIcon_<key>" so future passes / tests can still Find() the slot.
            GameObject iconGo = new GameObject($"ResIcon_{iconKey}");
            iconGo.transform.SetParent(chipGo.transform, false);
            iconGo.AddComponent<RectTransform>();
            Image icon = iconGo.AddComponent<Image>();
            string iconPath = $"Assets/Sprites/icon_{iconKey}.png";
            Sprite iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            if (iconSprite != null)
            {
                icon.sprite = iconSprite;
                icon.color = Color.white;             // visible (alpha 1)
                icon.preserveAspect = true;           // don't squash non-square art
            }
            else
            {
                // Art missing → keep the slot invisible rather than showing a
                // white box.  Warn so a broken import is caught in scene-gen logs.
                Debug.LogWarning($"[SceneSetup] top-bar icon missing: {iconPath}");
                icon.color = new Color(1f, 1f, 1f, 0f);
            }
            icon.raycastTarget = false;
            var iconLe = iconGo.AddComponent<LayoutElement>();
            iconLe.preferredWidth = kIconPx;
            iconLe.minWidth = kIconPx;
            iconLe.preferredHeight = kIconPx;

            // VALUE — fills the remaining chip width, left-aligned so it hugs its icon.
            GameObject txtGo = new GameObject(textName);
            txtGo.transform.SetParent(chipGo.transform, false);
            txtGo.AddComponent<RectTransform>();
            Text t = txtGo.AddComponent<Text>();
            t.text = label;
            t.font = uiFont;
            t.fontSize = 32;   // 시각 QA: 라벨 28→32 (가독)
            t.fontStyle = FontStyle.Bold;
            t.color = col;
            t.alignment = TextAnchor.MiddleLeft;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;  // 2-3 digit values never clip
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var txtLe = txtGo.AddComponent<LayoutElement>();
            txtLe.flexibleWidth = 1f;   // value cell takes the rest of the fixed chip width
            return t;
        }
    }
}
