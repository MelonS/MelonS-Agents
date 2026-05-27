using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using MelonS.GameProto;

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
            Image topBarBg = topBarGo.AddComponent<Image>();
            topBarBg.color = colPanel;
            RectTransform topRt = topBarGo.GetComponent<RectTransform>();
            topRt.anchorMin = new Vector2(0f, 1f);
            topRt.anchorMax = new Vector2(1f, 1f);
            topRt.pivot = new Vector2(0.5f, 1f);
            // Day 39: 1920 ref 기준 topbar 48px (이전 32은 800 ref 기준이라 너무 작음)
            // 운영자 피드백 polish: font 22→28 키움 → topbar 도 48→60 확보
            topRt.sizeDelta = new Vector2(0, 60);
            topRt.anchoredPosition = new Vector2(0, 0);

            // TopBar LEFT - ClockUI "Day 1 - 06:00"
            GameObject clockGo = new GameObject("ClockUI");
            clockGo.transform.SetParent(topBarGo.transform, false);
            Text clockText = clockGo.AddComponent<Text>();
            clockText.text = "Day 1 - 06:00";
            clockText.font = uiFont;
            clockText.fontSize = 28;
            clockText.color = colTextPrimary;
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
            timeGo.transform.SetParent(topBarGo.transform, false);
            Text timeText = timeGo.AddComponent<Text>();
            timeText.text = "▶ 1x";
            timeText.font = uiFont;
            timeText.fontSize = 28;
            timeText.color = colTextPrimary;
            timeText.alignment = TextAnchor.MiddleCenter;
            RectTransform timeRt = timeGo.GetComponent<RectTransform>();
            timeRt.anchorMin = new Vector2(0.5f, 0f);
            timeRt.anchorMax = new Vector2(0.5f, 1f);
            timeRt.pivot = new Vector2(0.5f, 0.5f);
            timeRt.sizeDelta = new Vector2(220, 0);
            timeRt.anchoredPosition = new Vector2(0, 0);
            timeGo.AddComponent<TimeUI>();

            // Day 38: 우측 리소스 영역 layout - overlap fix.
            //  [목재: N] · [식사: N] · [식량: N] · [석재: N]  16px padding from right
            //  각 텍스트 width 120, 점 width 16, 텍스트간 8px 간격. 총 너비 ~580.
            Text foodText = MakeResText(topBarGo, "FoodText", "식량: 0", uiFont, colAccentFood, -16);
            MakeResSeparator(topBarGo, "ResSep2", uiFont, colTextMuted, -144);
            Text mealsText = MakeResText(topBarGo, "MealsText", "식사: 0", uiFont, new Color(0.93f, 0.81f, 0.45f, 1f), -168);
            MakeResSeparator(topBarGo, "ResSep1", uiFont, colTextMuted, -296);
            Text woodText = MakeResText(topBarGo, "WoodText", "목재: 0", uiFont, colAccentWood, -320);
            MakeResSeparator(topBarGo, "ResSep3", uiFont, colTextMuted, -448);
            // #119 - 석재 (회색)
            Text stoneText = MakeResText(topBarGo, "StoneText", "석재: 0", uiFont, new Color(0.78f, 0.78f, 0.80f, 1f), -472);

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
                                        Font uiFont, Color col, float anchoredX)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            Text t = go.AddComponent<Text>();
            t.text = label;
            t.font = uiFont;
            t.fontSize = 28;
            t.color = col;
            t.alignment = TextAnchor.MiddleRight;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(120, 0);
            rt.anchoredPosition = new Vector2(anchoredX, 0);
            return t;
        }

        private static void MakeResSeparator(GameObject parent, string name,
                                             Font uiFont, Color col, float anchoredX)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            Text t = go.AddComponent<Text>();
            t.text = "·";
            t.font = uiFont;
            t.fontSize = 28;
            t.color = col;
            t.alignment = TextAnchor.MiddleCenter;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(16, 0);
            rt.anchoredPosition = new Vector2(anchoredX, 0);
        }
    }
}
