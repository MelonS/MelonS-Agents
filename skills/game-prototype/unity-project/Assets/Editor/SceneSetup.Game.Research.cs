using UnityEngine;
using UnityEngine.UI;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    // R10g - SceneSetup.cs Research strip + picker UI 블록 extract.
    //   원본 SceneSetup.cs L484-576 (~93 LOC).
    //   returns resProg.sprite 가 Tutorial overlay 가 재사용함.
    public static partial class SceneSetup
    {
        private static Image GenerateResearchStripAndPicker(
            GameObject canvasGo, Color colPanel, Color colTextPrimary, Font uiFont)
        {
            GameObject resStripGo = new GameObject("ResearchStrip");
            resStripGo.transform.SetParent(canvasGo.transform, false);
            Image resStripBg = resStripGo.AddComponent<Image>();
            resStripBg.color = colPanel;
            RectTransform resStripRt = resStripGo.GetComponent<RectTransform>();
            // #ui백로그 5.1 — 하단중앙 y40 은 GuiControlBar 탭바(y24~96)에 완전히 가려져
            //  연구 진행을 볼 수단이 없었다.  우하단 속도/시계 클러스터(y24~96) 위로 이동.
            resStripRt.anchorMin = new Vector2(1f, 0f);
            resStripRt.anchorMax = new Vector2(1f, 0f);
            resStripRt.pivot = new Vector2(1f, 0f);
            resStripRt.sizeDelta = new Vector2(420, 36);
            //  주의: 우하단 시계(ClockCluster: x-16, y120, 170x52)와 같은 밴드 — 시계 왼쪽에 나란히.
            resStripRt.anchoredPosition = new Vector2(-198, 120);

            GameObject resStatusGo = new GameObject("ResearchStatusText");
            resStatusGo.transform.SetParent(resStripGo.transform, false);
            Text resStatus = resStatusGo.AddComponent<Text>();
            resStatus.text = "연구: 없음 (N=선택)";
            resStatus.font = uiFont;
            resStatus.fontSize = 22;
            resStatus.color = colTextPrimary;
            resStatus.alignment = TextAnchor.MiddleCenter;
            RectTransform resStatusRt = resStatusGo.GetComponent<RectTransform>();
            resStatusRt.anchorMin = Vector2.zero;
            resStatusRt.anchorMax = Vector2.one;
            resStatusRt.sizeDelta = new Vector2(-12, -8);
            resStatusRt.anchoredPosition = new Vector2(0, 2);

            // Progress bar background under strip
            GameObject resProgBgGo = new GameObject("ResearchProgressBg");
            resProgBgGo.transform.SetParent(resStripGo.transform, false);
            Image resProgBg = resProgBgGo.AddComponent<Image>();
            resProgBg.color = new Color(0.10f, 0.10f, 0.10f, 0.7f);
            RectTransform resProgBgRt = resProgBgGo.GetComponent<RectTransform>();
            resProgBgRt.anchorMin = new Vector2(0f, 0f);
            resProgBgRt.anchorMax = new Vector2(1f, 0f);
            resProgBgRt.pivot = new Vector2(0.5f, 0f);
            resProgBgRt.sizeDelta = new Vector2(-12, 4);
            resProgBgRt.anchoredPosition = new Vector2(0, 2);

            GameObject resProgGo = new GameObject("ResearchProgressFill");
            resProgGo.transform.SetParent(resProgBgGo.transform, false);
            Image resProg = resProgGo.AddComponent<Image>();
            resProg.color = new Color(0.45f, 0.85f, 0.50f, 1f);
            resProg.type = Image.Type.Filled;
            resProg.fillMethod = Image.FillMethod.Horizontal;
            resProg.fillOrigin = (int)Image.OriginHorizontal.Left;
            resProg.fillAmount = 0f;
            // Use a 2x2 white sprite (procedurally - Image needs Sprite even for solid fill)
            var rpTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            rpTex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            rpTex.Apply();
            resProg.sprite = Sprite.Create(rpTex, new Rect(0,0,2,2), new Vector2(0.5f,0.5f), 2f);
            RectTransform resProgRt = resProgGo.GetComponent<RectTransform>();
            resProgRt.anchorMin = Vector2.zero;
            resProgRt.anchorMax = Vector2.one;
            resProgRt.sizeDelta = Vector2.zero;
            // Also assign sprite to resProgBg so Unity Image renders fill
            resProgBg.sprite = resProg.sprite;

            // Popup picker - wide center panel, hidden by default
            GameObject pickerGo = new GameObject("ResearchPicker");
            pickerGo.transform.SetParent(canvasGo.transform, false);
            Image pickerBg = pickerGo.AddComponent<Image>();
            pickerBg.color = new Color(0.08f, 0.10f, 0.13f, 0.95f);
            pickerBg.sprite = resProg.sprite;
            RectTransform pickerRt = pickerGo.GetComponent<RectTransform>();
            pickerRt.anchorMin = new Vector2(0.5f, 0.5f);
            pickerRt.anchorMax = new Vector2(0.5f, 0.5f);
            pickerRt.pivot = new Vector2(0.5f, 0.5f);
            pickerRt.sizeDelta = new Vector2(680, 280);
            pickerRt.anchoredPosition = Vector2.zero;

            GameObject pickerTextGo = new GameObject("PickerText");
            pickerTextGo.transform.SetParent(pickerGo.transform, false);
            Text pickerText = pickerTextGo.AddComponent<Text>();
            pickerText.text = "";
            pickerText.font = uiFont;
            pickerText.fontSize = 22;
            pickerText.color = colTextPrimary;
            pickerText.alignment = TextAnchor.UpperLeft;
            pickerText.horizontalOverflow = HorizontalWrapMode.Wrap;
            pickerText.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform pickerTextRt = pickerTextGo.GetComponent<RectTransform>();
            pickerTextRt.anchorMin = Vector2.zero;
            pickerTextRt.anchorMax = Vector2.one;
            pickerTextRt.sizeDelta = new Vector2(-24, -16);
            pickerTextRt.anchoredPosition = Vector2.zero;
            pickerGo.SetActive(false);

            GameObject rUIHost = new GameObject("ResearchUIHost");
            rUIHost.transform.SetParent(canvasGo.transform, false);
            rUIHost.AddComponent<RectTransform>();
            ResearchUI rUI = rUIHost.AddComponent<ResearchUI>();
            rUI.SetRefs(resStatus, resProg, pickerRt, pickerText);

            return resProg;  // Tutorial overlay 가 resProg.sprite 재사용
        }
    }
}
