using UnityEngine;
using UnityEngine.UI;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    // R10e - SceneSetup.cs Tutorial overlay 블록 extract.
    //   원본 SceneSetup.cs L578-613 (36 LOC).
    public static partial class SceneSetup
    {
        private static void GenerateTutorialOverlay(GameObject canvasGo, Font uiFont, Image resProg)
        {
            GameObject tutBgGo = new GameObject("TutorialBg");
            tutBgGo.transform.SetParent(canvasGo.transform, false);
            Image tutBg = tutBgGo.AddComponent<Image>();
            tutBg.color = new Color(0.03f, 0.05f, 0.08f, 0f);  // start hidden
            tutBg.sprite = resProg.sprite;  // reuse white 2x2
            RectTransform tutBgRt = tutBgGo.GetComponent<RectTransform>();
            tutBgRt.anchorMin = new Vector2(0.5f, 1f);
            tutBgRt.anchorMax = new Vector2(0.5f, 1f);
            tutBgRt.pivot = new Vector2(0.5f, 1f);
            tutBgRt.sizeDelta = new Vector2(720, 100);
            tutBgRt.anchoredPosition = new Vector2(0, -80);

            GameObject tutTextGo = new GameObject("TutorialText");
            tutTextGo.transform.SetParent(tutBgGo.transform, false);
            Text tutText = tutTextGo.AddComponent<Text>();
            tutText.text = "";
            tutText.font = uiFont;
            tutText.fontSize = 28;
            tutText.alignment = TextAnchor.MiddleCenter;
            tutText.color = new Color(0.98f, 0.95f, 0.85f, 0f);
            tutText.supportRichText = true;
            tutText.horizontalOverflow = HorizontalWrapMode.Wrap;
            tutText.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform tutTextRt = tutTextGo.GetComponent<RectTransform>();
            tutTextRt.anchorMin = Vector2.zero;
            tutTextRt.anchorMax = Vector2.one;
            tutTextRt.sizeDelta = new Vector2(-24, -12);
            tutTextRt.anchoredPosition = Vector2.zero;

            GameObject tutHost = new GameObject("TutorialOverlayHost");
            tutHost.transform.SetParent(canvasGo.transform, false);
            tutHost.AddComponent<RectTransform>();
            TutorialOverlay tutOv = tutHost.AddComponent<TutorialOverlay>();
            tutOv.SetRefs(tutBg, tutText);
        }
    }
}
