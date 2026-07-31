using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    // R10h - SceneSetup.cs PawnInfoPanel (Day 55 health + needs) + SkillPanel (Day 21) extract.
    //   원본 SceneSetup.cs L490-630 (~140 LOC).
    public static partial class SceneSetup
    {
        private static void GeneratePawnInfoPanel(
            GameObject canvasGo, ClickSelector cs,
            Color colPanel, Color colTextPrimary, Color colTextMuted,
            Color colAccentFood, Color colAccentWood,
            Font uiFont)
        {
            GameObject panelGo = new GameObject("PawnInfoPanel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            Image panelBg = panelGo.AddComponent<Image>();
            panelBg.color = colPanel;
            RectTransform panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0f, 0f);
            panelRt.anchorMax = new Vector2(0f, 0f);
            panelRt.pivot = new Vector2(0f, 0f);
            panelRt.sizeDelta = new Vector2(380, 200);
            panelRt.anchoredPosition = new Vector2(12, 64); // leave room for save/load row below

            // Title text (visible when pawn selected)
            GameObject titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panelGo.transform, false);
            Text title = titleGo.AddComponent<Text>();
            title.text = "Colonist";
            title.alignment = TextAnchor.UpperLeft;
            title.font = uiFont;
            title.fontSize = 22;
            title.color = colTextPrimary;
            RectTransform titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.sizeDelta = new Vector2(-20, 26);
            titleRt.anchoredPosition = new Vector2(10, -8);

            // 3 need bars - accent palette
            Image foodBar  = CreateNeedBar(panelGo.transform, "식량", new Vector2(10, 105), colAccentFood, uiFont, colTextPrimary);
            Image sleepBar = CreateNeedBar(panelGo.transform, "수면", new Vector2(10, 65),  new Color(0.4f, 0.6f, 0.9f, 1f), uiFont, colTextPrimary);
            Image moodBar  = CreateNeedBar(panelGo.transform, "기분", new Vector2(10, 25),  colAccentWood, uiFont, colTextPrimary);

            // Empty-state hint - replaces the panel content when no pawn is selected.
            GameObject emptyGo = new GameObject("EmptyText");
            emptyGo.transform.SetParent(panelGo.transform, false);
            Text empty = emptyGo.AddComponent<Text>();
            empty.text = "주민을 클릭하세요";
            empty.alignment = TextAnchor.MiddleCenter;
            empty.font = uiFont;
            empty.fontSize = 12;
            empty.color = colTextMuted;
            RectTransform emptyRt = emptyGo.GetComponent<RectTransform>();
            emptyRt.anchorMin = Vector2.zero;
            emptyRt.anchorMax = Vector2.one;
            emptyRt.sizeDelta = Vector2.zero;
            emptyRt.anchoredPosition = Vector2.zero;

            // Day 55: 부위별 health text (panel 안쪽 우측 영역)
            GameObject healthGo = new GameObject("HealthText");
            healthGo.transform.SetParent(panelGo.transform, false);
            Text healthText = healthGo.AddComponent<Text>();
            healthText.text = "";
            healthText.font = uiFont;
            healthText.fontSize = 13;
            healthText.color = colTextPrimary;
            healthText.alignment = TextAnchor.UpperLeft;
            healthText.supportRichText = true;
            healthText.horizontalOverflow = HorizontalWrapMode.Wrap;
            healthText.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform healthRt = healthGo.GetComponent<RectTransform>();
            healthRt.anchorMin = new Vector2(0.55f, 0f);
            healthRt.anchorMax = new Vector2(1f, 1f);
            healthRt.pivot = new Vector2(0.5f, 0.5f);
            healthRt.sizeDelta = new Vector2(-8, -38);
            healthRt.anchoredPosition = new Vector2(0, -8);

            // Controller wiring
            PawnInfoPanel panel = panelGo.AddComponent<PawnInfoPanel>();
            SerializedObject pso = new SerializedObject(panel);
            pso.FindProperty("selector").objectReferenceValue = cs;
            pso.FindProperty("titleText").objectReferenceValue = title;
            pso.FindProperty("foodBar").objectReferenceValue = foodBar;
            pso.FindProperty("sleepBar").objectReferenceValue = sleepBar;
            pso.FindProperty("moodBar").objectReferenceValue = moodBar;
            pso.FindProperty("emptyText").objectReferenceValue = empty;
            pso.FindProperty("panelBg").objectReferenceValue = panelBg;
            pso.FindProperty("healthText").objectReferenceValue = healthText;
            pso.ApplyModifiedProperties();
        }
    }
}
