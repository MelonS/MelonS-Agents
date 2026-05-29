using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto.EditorTools
{
    // R4: SceneSetup partial - UI builder helpers (CreateNeedBar / CreateIconButton).
    //  원본 SceneSetup.cs(1266-1363)에서 이동.
    public static partial class SceneSetup
    {
        private static Image CreateNeedBar(Transform parent, string label, Vector2 pos, Color fillColor, Font font = null, Color? labelColor = null)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Color lblCol = labelColor ?? Color.white;

            // Row container
            GameObject row = new GameObject(label + "Row");
            row.transform.SetParent(parent, false);
            RectTransform rt = row.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(220, 28);
            rt.anchoredPosition = pos;

            // Label
            GameObject lblGo = new GameObject("Label");
            lblGo.transform.SetParent(row.transform, false);
            Text lbl = lblGo.AddComponent<Text>();
            lbl.text = label;
            lbl.font = font;
            lbl.fontSize = 14;
            lbl.color = lblCol;
            lbl.alignment = TextAnchor.MiddleLeft;
            RectTransform lblRt = lblGo.GetComponent<RectTransform>();
            lblRt.anchorMin = new Vector2(0f, 0f);
            lblRt.anchorMax = new Vector2(0f, 1f);
            lblRt.pivot = new Vector2(0f, 0.5f);
            lblRt.sizeDelta = new Vector2(50, 0);
            lblRt.anchoredPosition = new Vector2(0, 0);

            // Bar bg
            GameObject bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(row.transform, false);
            Image bg = bgGo.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.05f, 0.7f);
            RectTransform bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0f);
            bgRt.anchorMax = new Vector2(1f, 1f);
            bgRt.pivot = new Vector2(0f, 0.5f);
            bgRt.sizeDelta = new Vector2(-55, -6);
            bgRt.anchoredPosition = new Vector2(55, 0);

            // Fill
            GameObject fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(bgGo.transform, false);
            Image fill = fillGo.AddComponent<Image>();
            fill.color = fillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0.8f;
            // Use a built-in white sprite so Image actually fills
            fill.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            RectTransform fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.sizeDelta = Vector2.zero;
            fillRt.anchoredPosition = Vector2.zero;

            return fill;
        }

        // #UI-restyle U8 (Round 5) — S/L corner buttons now match the control-bar button look:
        //   root Image = Divider border; inset PanelFill = body; Button targets the FILL so
        //   hover/pressed tint the body while the border stays consistent.  bgColor arg is
        //   ignored in favor of the canonical UITheme button palette (kept in the signature so
        //   the SaveHint caller stays unchanged).  The Button + GameObject name are preserved
        //   so GameSaveButtons' SerializedObject wiring (by Button reference) is unaffected.
        private static GameObject CreateIconButton(Transform parent, string name, string label, Vector2 pos, Color bgColor, Color textColor, Font font)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(40, 40);
            rt.anchoredPosition = pos;

            Image img = go.AddComponent<Image>();
            img.color = MelonS.GameProto.Core.UITheme.Divider;   // border edge
            var fillRt = MelonS.GameProto.Core.UITheme.MakeBorderedPanel(
                rt, 2f, MelonS.GameProto.Core.UITheme.BtnInactiveBg);
            var fillImg = fillRt.parent.GetComponent<Image>();   // PanelFill image

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = fillImg;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            cb.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.06f;
            btn.colors = cb;

            GameObject txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            Text txt = txtGo.AddComponent<Text>();
            txt.text = label;
            txt.font = MelonS.GameProto.Core.UITheme.LoadKoreanFont(18);
            txt.fontSize = 18;
            txt.fontStyle = FontStyle.Bold;
            txt.color = MelonS.GameProto.Core.UITheme.TextPrimary;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;
            RectTransform txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;
            txtRt.anchoredPosition = Vector2.zero;
            return go;
        }
    }
}
