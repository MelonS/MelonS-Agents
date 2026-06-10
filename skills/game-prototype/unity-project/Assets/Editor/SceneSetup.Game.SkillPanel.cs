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
            // #ui백로그 5.0 — PawnInfoPanel(x12..392) 우측 8px 갭으로 이동 (이전 260,60 겹침).
            skRt.anchoredPosition = new Vector2(400, 58);

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

            // #ui백로그 5.0 — SkillUI 를 skillPanelGo 자신에 붙이면 SetActive(false) 와 함께
            //  Update 가 영원히 죽어 패널이 절대 안 켜졌다(죽은 UI).  ResearchUIHost 패턴대로
            //  상시-활성 호스트 GO 에 컴포넌트를 두고 container 만 패널로 배선한다.
            GameObject skHost = new GameObject("SkillUIHost");
            skHost.transform.SetParent(canvasGo.transform, false);
            skHost.AddComponent<RectTransform>();
            SkillUI skUI = skHost.AddComponent<SkillUI>();
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
