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

            // Day 38 / #UI-restyle U4 (Round 5): 우측 리소스 영역.
            //  [icon|식량: N] │ [icon|식사: N] │ [icon|목재: N] │ [icon|석재: N]  16px from right.
            //  점(·) 구분자 → 얇은 Divider 세로선(MakeVDivider 스타일)으로 교체해 제어바와 통일.
            //  각 readout 앞에 24x24 ICON SLOT(빈 자리)을 둠 → 다음 art round 에서
            //  여기에 wood/food/meal/stone 아이콘 스프라이트를 넣으면 됨.
            //  (ICON SLOT 은 "ResIcon_<key>" 이름의 빈 Image, 현재 alpha 0).
            //  각 텍스트 width 120, 세로선 2px, 텍스트간 28px 간격. 총 너비 ~580.
            // topbar-icons FINAL-FIX: icons grew 24→36px and now hug the visible text
            //   (icon left edge ≈ readoutX - 96 - 6 - 36 = readoutX - 138).  Re-spaced the
            //   readouts to ~172px pitch and pushed each divider clear of the next icon so
            //   the larger pictograms don't render on top of the thin divider rules.
            Text foodText  = MakeResText(parent, "FoodText",  "식량: 0", "food",  uiFont, colAccentFood, -16);
            MakeResSeparator(parent, "ResSep2", -164);
            Text mealsText = MakeResText(parent, "MealsText", "식사: 0", "meal",  uiFont, new Color(0.93f, 0.81f, 0.45f, 1f), -188);
            MakeResSeparator(parent, "ResSep1", -336);
            Text woodText  = MakeResText(parent, "WoodText",  "목재: 0", "wood",  uiFont, colAccentWood, -360);
            MakeResSeparator(parent, "ResSep3", -508);
            // #119 - 석재 (회색)
            Text stoneText = MakeResText(parent, "StoneText", "석재: 0", "stone", uiFont, new Color(0.78f, 0.78f, 0.80f, 1f), -532);

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

        private static Text MakeResText(GameObject parent, string name, string label,
                                        string iconKey, Font uiFont, Color col, float anchoredX)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            Text t = go.AddComponent<Text>();
            t.text = label;
            t.font = uiFont;
            t.fontSize = 28;
            t.fontStyle = FontStyle.Bold;
            t.color = col;
            t.alignment = TextAnchor.MiddleRight;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(120, 0);
            rt.anchoredPosition = new Vector2(anchoredX, 0);

            // ICON SLOT — Image placed just LEFT of this readout's VISIBLE text.
            //   Round 7: art landed.  Load Assets/Sprites/icon_<key>.png (point-filtered,
            //   force-imported by ForceImportAllSprites) and make it visible (alpha 1).
            //   Map is 1:1 — iconKey is stone/wood/meal/food → icon_<key>.png.
            //   Named "ResIcon_<key>" so future passes can still Find() each slot.
            //
            //   FINAL-FIX (Day, topbar-icons): QA read the bar as "text-only" — icons were
            //   24px (too small vs the 28px Bold Korean text) AND anchored to the readout
            //   BLOCK's left edge (anchoredX-120).  Because each "식량: 0" label is
            //   right-aligned and only ~95px wide, that left-block-edge sat ~25px to the
            //   LEFT of the first glyph, dumping the icon next to the PREVIOUS readout's
            //   divider — so it read as belonging to the wrong number, hence "ambiguous".
            //   Fixes: (1) icon 24→36px so it reads as a distinct pictogram at capture res;
            //   (2) anchor to the VISIBLE text left edge (anchoredX - kLabelWidth) with a
            //   tight 6px gap so each icon hugs ITS number; (3) raise icon a hair off-center
            //   is not needed — text is MiddleRight, icon MiddleY → both vertically centered.
            const float kIconPx    = 36f;   // distinct pictogram at 1920+ capture res
            const float kLabelWidth = 96f;  // measured visible width of "OO: N" @ font28 Bold
            const float kIconGap    = 6f;   // breathing room between icon and first glyph
            GameObject iconGo = new GameObject($"ResIcon_{iconKey}");
            iconGo.transform.SetParent(parent.transform, false);
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
            RectTransform irt = iconGo.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(1f, 0.5f);
            irt.anchorMax = new Vector2(1f, 0.5f);
            irt.pivot = new Vector2(1f, 0.5f);
            irt.sizeDelta = new Vector2(kIconPx, kIconPx);
            // Place icon's RIGHT edge just left of the visible text's first glyph:
            //   readout right edge = anchoredX; visible text spans ~kLabelWidth to its left.
            irt.anchoredPosition = new Vector2(anchoredX - kLabelWidth - kIconGap, 0);
            return t;
        }

        // #UI-restyle U4 — slim Divider-colored vertical rule between readouts
        //   (replaces the old "·" debug-text separator), matching the control bar's group lines.
        private static void MakeResSeparator(GameObject parent, string name, float anchoredX)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            Image img = go.AddComponent<Image>();
            img.color = UITheme.Divider;
            img.raycastTarget = false;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(2f, 32f);
            rt.anchoredPosition = new Vector2(anchoredX, 0);
        }
    }
}
