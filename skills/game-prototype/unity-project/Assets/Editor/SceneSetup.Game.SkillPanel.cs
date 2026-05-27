using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    // R10i - SceneSetup.cs SkillPanel (Day 21 채집/벌목/건축/전투 Lv) extract.
    //   원본 SceneSetup.cs L498-542 (45 LOC).
    public static partial class SceneSetup
    {
        private static void GenerateSkillPanel(
            GameObject canvasGo, ClickSelector cs,
            Color colPanel, Color colTextPrimary,
            Color colAccentFood, Color colAccentWood, Color colAccentWarn,
            Font uiFont)
        {
            GameObject skillPanelGo = new GameObject("SkillPanel");
            skillPanelGo.transform.SetParent(canvasGo.transform, false);
            Image skillBg = skillPanelGo.AddComponent<Image>();
            skillBg.color = colPanel;
            RectTransform skRt = skillPanelGo.GetComponent<RectTransform>();
            skRt.anchorMin = new Vector2(0f, 0f);
            skRt.anchorMax = new Vector2(0f, 0f);
            skRt.pivot = new Vector2(0f, 0f);
            skRt.sizeDelta = new Vector2(180, 180);
            skRt.anchoredPosition = new Vector2(260, 60);

            Text MkSkillRow(string label, float yOffset, Color accent)
            {
                GameObject g = new GameObject(label + "Text");
                g.transform.SetParent(skillPanelGo.transform, false);
                Text t = g.AddComponent<Text>();
                t.text = $"{label}: Lv 0";
                t.font = uiFont;
                t.fontSize = 20;
                t.color = colTextPrimary;
                t.alignment = TextAnchor.MiddleLeft;
                RectTransform rt = g.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(-16, 24);
                rt.anchoredPosition = new Vector2(8, -yOffset);
                return t;
            }
            Text gatherT = MkSkillRow("채집", 14, colAccentFood);
            Text chopT   = MkSkillRow("벌목", 42, colAccentWood);
            Text buildT  = MkSkillRow("건축", 70, colTextPrimary);
            Text combatT = MkSkillRow("전투", 98, colAccentWarn);

            SkillUI skUI = skillPanelGo.AddComponent<SkillUI>();
            SerializedObject skSo = new SerializedObject(skUI);
            skSo.FindProperty("selector").objectReferenceValue = cs;
            skSo.FindProperty("gatherText").objectReferenceValue = gatherT;
            skSo.FindProperty("chopText").objectReferenceValue = chopT;
            skSo.FindProperty("buildText").objectReferenceValue = buildT;
            skSo.FindProperty("combatText").objectReferenceValue = combatT;
            skSo.FindProperty("container").objectReferenceValue = skillPanelGo;
            skSo.ApplyModifiedProperties();
            skillPanelGo.SetActive(false);  // start hidden until pawn selected
        }
    }
}
