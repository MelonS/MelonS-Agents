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
            // TOP-7 (visual-polish-backlog 2026-06-11): 상단 중앙 720x100 상주 박스가
            //  모든 스크린샷의 초점을 점유 — '미완성 빌드' 인상의 주범.  하단 중앙
            //  토스트(BuildClickToast y110) 위 1줄짜리 작은 힌트로 강등.
            tutBgRt.anchorMin = new Vector2(0.5f, 0f);
            tutBgRt.anchorMax = new Vector2(0.5f, 0f);
            tutBgRt.pivot = new Vector2(0.5f, 0f);
            tutBgRt.sizeDelta = new Vector2(560, 64);   // UI겹침 P2-2 — 시프트 인포패널과 슬리버
            tutBgRt.anchoredPosition = new Vector2(0, 170);
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
            tutText.font = UITheme.LoadKoreanFont(20);
            tutText.fontSize = 20;   // TOP-7 — 힌트 타이포 (배너→토스트 강등에 맞춤)
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

            // #38 플레이키 진범(2026-06-10) — 안내 배너(720x100, y-160~-260)가 alpha 0 에서도
            //  월드 클릭을 raycast 차단(첫 ~18초 상단 밴드 클릭 무음 무효).  배너 서브트리는
            //  어떤 입력도 소비하지 않는다 (TutorialOverlay.Start 의 런타임 sweep 과 이중 방어).
            tutCg.blocksRaycasts = false;
            tutCg.interactable = false;
            foreach (var g in tutBgGo.GetComponentsInChildren<Graphic>(true))
                g.raycastTarget = false;

            GameObject tutHost = new GameObject("TutorialOverlayHost");
            tutHost.transform.SetParent(canvasGo.transform, false);
            tutHost.AddComponent<RectTransform>();
            TutorialOverlay tutOv = tutHost.AddComponent<TutorialOverlay>();
            tutOv.SetRefs(tutBg, tutText, tutCg);
        }
    }
}
