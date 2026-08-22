using UnityEngine;

namespace MelonS.GameProto.Naval
{
    /// <summary>
    /// 화면 좌하단에 조작법을 상시 표시한다. 프로토타입 단계라 Canvas/TMP
    /// 설정 없이 OnGUI 로 가볍게 — 이동감 검증이 목적이지 UI 완성도가
    /// 목적이 아니다.
    /// </summary>
    public class ControlsHintUI : MonoBehaviour
    {
        private GUIStyle boxStyle;
        private GUIStyle textStyle;

        private void OnGUI()
        {
            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.box);
                Texture2D bg = new Texture2D(1, 1);
                bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
                bg.Apply();
                boxStyle.normal.background = bg;

                textStyle = new GUIStyle(GUI.skin.label);
                textStyle.fontSize = 16;
                textStyle.normal.textColor = Color.white;
            }

            float w = 260f, h = 90f;
            float pad = 14f;
            GUI.Box(new Rect(pad, Screen.height - h - pad, w, h), GUIContent.none, boxStyle);
            GUI.Label(
                new Rect(pad + 12f, Screen.height - h - pad + 8f, w - 24f, h - 16f),
                "W/S  전진 · 후진\nA/D  방향타 좌 · 우\nTab  카메라 2.5D / 3D\n우클릭 드래그  3D 시점 회전",
                textStyle);
        }
    }
}
