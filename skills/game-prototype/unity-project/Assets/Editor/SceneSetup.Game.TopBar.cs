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
            topRt.sizeDelta = new Vector2(0, 60);
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
            uiFont = UITheme.LoadKoreanFont(28);
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
            clockText.fontSize = 28;
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

            // TopBar CENTER - TimeUI "▶ 1x"
            GameObject timeGo = new GameObject("TimeUI");
            timeGo.transform.SetParent(parent.transform, false);
            Text timeText = timeGo.AddComponent<Text>();
            timeText.text = "▶ 1x";
            timeText.font = uiFont;
            timeText.fontSize = 28;
            timeText.fontStyle = FontStyle.Bold;
            timeText.color = colTextPrimary;
            timeText.alignment = TextAnchor.MiddleCenter;
            RectTransform timeRt = timeGo.GetComponent<RectTransform>();
            timeRt.anchorMin = new Vector2(0.5f, 0f);
            timeRt.anchorMax = new Vector2(0.5f, 1f);
            timeRt.pivot = new Vector2(0.5f, 0.5f);
            timeRt.sizeDelta = new Vector2(220, 0);
            timeRt.anchoredPosition = new Vector2(0, 0);
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
            const float kChipW   = 156f;  // equal cell width (≥150 per §3.1), holds icon + "OO: NNN"
            const float kChipGap = 8f;    // equal gap between chips (divider sits in the gap)
            const float kRightInset = 16f; // group's right edge inset from the bar's right edge

            // Right-anchored layout container; HorizontalLayoutGroup lays the chips
            // right→left with equal spacing.  Width is driven by the layout (child
            // controlled), so we give it a generous fixed extent and right-align children.
            GameObject resRowGo = new GameObject("ResourceRow");
            resRowGo.transform.SetParent(parent.transform, false);
            RectTransform resRowRt = resRowGo.AddComponent<RectTransform>();
            resRowRt.anchorMin = new Vector2(1f, 0f);
            resRowRt.anchorMax = new Vector2(1f, 1f);
            resRowRt.pivot = new Vector2(1f, 0.5f);
            // 4 chips + 3 inter-chip gaps, anchored to the right edge.
            float rowW = 4f * kChipW + 3f * kChipGap;
            resRowRt.sizeDelta = new Vector2(rowW, 0);
            resRowRt.anchoredPosition = new Vector2(-kRightInset, 0);
            var hlg = resRowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.spacing = kChipGap;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(0, 0, 8, 8);

            // Chips in screen order (left→right): food → meal → wood → stone.
            Text foodText  = MakeResChip(resRowGo, "FoodText",  "식량: 0", "food",  uiFont, colAccentFood,                               kChipW);
            Text mealsText = MakeResChip(resRowGo, "MealsText", "식사: 0", "meal",  uiFont, new Color(0.93f, 0.81f, 0.45f, 1f),          kChipW);
            Text woodText  = MakeResChip(resRowGo, "WoodText",  "목재: 0", "wood",  uiFont, colAccentWood,                               kChipW);
            // #119 - 석재 (회색)
            Text stoneText = MakeResChip(resRowGo, "StoneText", "석재: 0", "stone", uiFont, new Color(0.78f, 0.78f, 0.80f, 1f),          kChipW);

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
            const float kIconPx = 36f;   // distinct pictogram at 1920+ capture res
            const float kIconGap = 6f;   // breathing room between icon and first glyph
            const float kDividerW = 2f;  // thin Divider rule on the chip's leading edge

            // Chip cell — a fixed-width container the parent layout treats as one element.
            GameObject chipGo = new GameObject($"ResChip_{iconKey}");
            chipGo.transform.SetParent(row.transform, false);
            RectTransform chipRt = chipGo.AddComponent<RectTransform>();
            var chipLe = chipGo.AddComponent<LayoutElement>();
            chipLe.preferredWidth = chipW;       // equal width for every chip
            chipLe.minWidth = chipW;
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
            divLe.preferredHeight = 32f;

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
            t.fontSize = 28;
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
