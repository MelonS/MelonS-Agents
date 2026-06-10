using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    // R10d - SceneSetup.cs GenerateGame 의 EventLog UI 블록 extract.
    //   원본 SceneSetup.cs L481-518 (38 LOC).
    public static partial class SceneSetup
    {
        private static void GenerateEventLogPanel(
            GameObject canvasGo, Color colPanel, Color colTextPrimary,
            Font uiFont, AIDirector director)
        {
            GameObject logPanelGo = new GameObject("EventLogPanel");
            logPanelGo.transform.SetParent(canvasGo.transform, false);
            Image logBg = logPanelGo.AddComponent<Image>();
            logBg.color = colPanel;
            RectTransform logRt = logPanelGo.GetComponent<RectTransform>();
            logRt.anchorMin = new Vector2(1f, 1f);
            logRt.anchorMax = new Vector2(1f, 1f);
            logRt.pivot = new Vector2(1f, 1f);
            logRt.sizeDelta = new Vector2(240, 160);
            // #ui백로그 7.0 우상단 컬럼 밴드 계약: TopBar(0~-76) > AlertStack 카드 3장
            //  예약(-88~-232) > EventLog(-244~-404) > BuildClickToast(-416~) > ResourceLowAlert(-462~).
            //  이전 -72 는 AlertStack 카드와 같은 사각형을 공유해 카드가 로그 위에 포개졌다.
            logRt.anchoredPosition = new Vector2(-12, -244);

            GameObject logTextGo = new GameObject("LogText");
            logTextGo.transform.SetParent(logPanelGo.transform, false);
            Text logText = logTextGo.AddComponent<Text>();
            logText.text = "";
            logText.font = uiFont;
            logText.fontSize = 20;
            logText.color = colTextPrimary;
            logText.alignment = TextAnchor.UpperLeft;
            logText.supportRichText = true;
            logText.verticalOverflow = VerticalWrapMode.Truncate;
            logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            RectTransform logTextRt = logTextGo.GetComponent<RectTransform>();
            logTextRt.anchorMin = Vector2.zero;
            logTextRt.anchorMax = Vector2.one;
            logTextRt.sizeDelta = new Vector2(-16, -12);
            logTextRt.anchoredPosition = new Vector2(0, -2);

            EventLogUI logUI = logPanelGo.AddComponent<EventLogUI>();
            SerializedObject logSo = new SerializedObject(logUI);
            logSo.FindProperty("director").objectReferenceValue = director;
            logSo.FindProperty("logText").objectReferenceValue = logText;
            logSo.FindProperty("panelBg").objectReferenceValue = logBg;
            logSo.FindProperty("maxEntries").intValue = 3;
            logSo.ApplyModifiedProperties();
            // Day 15: start hidden until first event
            logBg.enabled = false;
        }
    }
}
