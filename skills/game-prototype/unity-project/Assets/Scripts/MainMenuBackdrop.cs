using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// 메인메뉴 키아트 배경 (2026-07-24 운영자 "초기화면 개선").
    /// FLUX.1-schnell 로컬 생성 키아트(Resources/UI/menu_bg)를 메뉴 뒤에 풀스크린으로 깐다.
    /// 씬 베이크 수정 없이 self-bootstrap — MainMenu 씬 로드 시 1회 생성, 이미지 없으면 무동작.
    /// </summary>
    public static class MainMenuBackdrop
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded += (s, _) => { if (s.name == "MainMenu") Build(); };
            if (SceneManager.GetActiveScene().name == "MainMenu") Build();
        }

        private static void Build()
        {
            if (GameObject.Find("MenuBackdrop") != null) return;
            var tex = Resources.Load<Texture2D>("UI/menu_bg");
            if (tex == null) return;

            var canvasGo = new GameObject("MenuBackdrop", typeof(Canvas));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100;   // 기존 메뉴 캔버스(0) 뒤

            var imgGo = new GameObject("BG", typeof(RawImage));
            imgGo.transform.SetParent(canvasGo.transform, false);
            var img = imgGo.GetComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            // 하단 어둠 오버레이 — 타이틀/버튼 가독성 (키아트 상단 하늘은 살림)
            var dimGo = new GameObject("Dim", typeof(Image));
            dimGo.transform.SetParent(canvasGo.transform, false);
            var dim = dimGo.GetComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.30f);
            dim.raycastTarget = false;
            var drt = dim.rectTransform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = new Vector2(1f, 0.55f);
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
        }
    }
}
