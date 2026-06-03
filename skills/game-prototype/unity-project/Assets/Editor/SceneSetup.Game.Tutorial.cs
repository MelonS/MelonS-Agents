using UnityEngine;
using UnityEngine.UI;
using MelonS.GameProto;
using MelonS.GameProto.Core;

namespace MelonS.GameProto.EditorTools
{
    // R10e - SceneSetup.cs Tutorial overlay 블록 extract.
    //   원본 SceneSetup.cs L578-613 (36 LOC).
    public static partial class SceneSetup
    {
        private static void GenerateTutorialOverlay(GameObject canvasGo, Font uiFont, Image resProg)
        {
            // #UI-restyle U8 (Round 5) — tutorial banner = warm bordered panel (was a cold
            //   flat near-black box).  The root TutorialBg Image becomes the Divider BORDER;
            //   MakeBorderedPanel adds a PanelBg fill child; a CanvasGroup on TutorialBg lets
            //   TutorialOverlay fade the WHOLE bordered panel (border + fill + text) together
            //   instead of tweening a single Image alpha.  Keeps the same GameObject names so
            //   SetRefs / any lookups stay valid.
            GameObject tutBgGo = new GameObject("TutorialBg");
            tutBgGo.transform.SetParent(canvasGo.transform, false);
            RectTransform tutBgRt = tutBgGo.AddComponent<RectTransform>();
            tutBgRt.anchorMin = new Vector2(0.5f, 1f);
            tutBgRt.anchorMax = new Vector2(0.5f, 1f);
            tutBgRt.pivot = new Vector2(0.5f, 1f);
            tutBgRt.sizeDelta = new Vector2(720, 100);
            // #64 운영자 "12시 메뉴 UI 겹침": 튜토리얼(y-80, 100h)이 ColonistBar(상단 76+8=84 아래
            //  ~54h, 즉 -84~-138)와 정면 겹쳤음.  콜로니스트 바 아래(-160)로 내려 겹침 제거.
            tutBgRt.anchoredPosition = new Vector2(0, -160);
            // bordered panel (returns padded inner content RT; TutorialText parents here)
            RectTransform tutContent = UITheme.MakeBorderedPanel(
                tutBgRt, UITheme.BorderPx, UITheme.PanelBg, UITheme.PadOuter);
            // CanvasGroup drives the whole-panel fade (border Image is the root TutorialBg).
            Image tutBg = tutBgGo.GetComponent<Image>();   // the Divider border Image
            CanvasGroup tutCg = tutBgGo.AddComponent<CanvasGroup>();
            tutCg.alpha = 0f;   // start hidden

            GameObject tutTextGo = new GameObject("TutorialText");
            tutTextGo.transform.SetParent(tutContent, false);
            Text tutText = tutTextGo.AddComponent<Text>();
            tutText.text = "";
            tutText.font = UITheme.LoadKoreanFont(28);
            tutText.fontSize = 28;
            tutText.alignment = TextAnchor.MiddleCenter;
            tutText.color = UITheme.TextPrimary;   // cream body text
            tutText.supportRichText = true;
            tutText.horizontalOverflow = HorizontalWrapMode.Wrap;
            tutText.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform tutTextRt = tutTextGo.GetComponent<RectTransform>();
            tutTextRt.anchorMin = Vector2.zero;
            tutTextRt.anchorMax = Vector2.one;
            tutTextRt.sizeDelta = Vector2.zero;
            tutTextRt.anchoredPosition = Vector2.zero;

            GameObject tutHost = new GameObject("TutorialOverlayHost");
            tutHost.transform.SetParent(canvasGo.transform, false);
            tutHost.AddComponent<RectTransform>();
            TutorialOverlay tutOv = tutHost.AddComponent<TutorialOverlay>();
            tutOv.SetRefs(tutBg, tutText, tutCg);
        }
    }
}
