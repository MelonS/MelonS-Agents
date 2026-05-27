using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 피드백 (2026-05-27): "ui들 그냥 표시만해주는 키보드 의존도 너무 높음. gui가 전혀 되질 않음."
    /// → 화면 하단 중앙에 클릭 가능한 버튼 바.  모든 기존 hotkey 와 동일 효과.
    /// 키보드 의존 제거.  Self-bootstrapping (Canvas 찾아서 자기가 UI 생성).
    ///
    /// 버튼 라인업 (왼쪽 → 오른쪽):
    ///   [ ⏸ 멈춤 ] [ 1x ] [ 2x ] [ 4x ]   [ 징집(R) ]   [ 벽(B) ] [ 바닥(F) ] [ 문(G) ] [ 화덕(T) ]   [ 연구(N) ]
    ///
    /// 각 버튼 60x56, gap 4.  active mode 인 build 버튼은 노란 highlight.
    /// </summary>
    public class GuiControlBar : MonoBehaviour
    {
        private const float BtnW = 76f;
        private const float BtnH = 56f;
        private const float Gap = 4f;
        private const float GroupGap = 16f;

        private Button pauseBtn, speed1Btn, speed2Btn, speed4Btn;
        private Button draftBtn;
        private Button wallBtn, floorBtn, doorBtn, stoveBtn, bedBtn;
        private Button researchBtn;

        // 상태 색 — active 면 노란, 아니면 panel 색
        private static readonly Color InactiveBg = new Color(0.12f, 0.14f, 0.16f, 0.92f);
        private static readonly Color ActiveBg   = new Color(0.95f, 0.78f, 0.20f, 0.95f);
        private static readonly Color TextNormal = new Color(0.94f, 0.94f, 0.92f, 1f);
        private static readonly Color TextActive = new Color(0.10f, 0.10f, 0.08f, 1f);

        private Font font;
        // Lesson #4 - FindFirstObjectByType per-Update 비쌈.
        private ClickSelector cachedCs;

        private static GuiControlBar _instance;

        public static void EnsureInScene()
        {
            if (_instance != null) return;
            // Find Canvas
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[GuiControlBar] Canvas 없음 - skip");
                return;
            }
            var go = new GameObject("GuiControlBar");
            go.transform.SetParent(canvas.transform, false);
            _instance = go.AddComponent<GuiControlBar>();
        }

        private void Start()
        {
            font = LoadKoreanFont();
            BuildLayout();
            HideOldHintIfPresent();
        }

        private Font LoadKoreanFont()
        {
            // SceneSetup 과 같은 fallback chain
            string[] candidates = { "Malgun Gothic", "NanumGothic", "Gulim", "Dotum", "Arial Unicode MS" };
            foreach (var name in candidates)
            {
                var f = Font.CreateDynamicFontFromOSFont(name, 18);
                if (f != null) return f;
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void HideOldHintIfPresent()
        {
            // SceneSetup 의 ControlHint 텍스트가 키보드 의존 안내라서, 버튼 바가 있으면 가림.
            // (사용자가 옛 hint 와 새 bar 중복 보면 혼란)
            var hint = GameObject.Find("ControlHint");
            if (hint != null)
            {
                var t = hint.GetComponent<Text>();
                if (t != null) t.text = "🖱 좌클릭=선택 · 우클릭=이동/작업 · ESC=빌드취소";
            }
        }

        private void BuildLayout()
        {
            // 부모 panel — 하단 중앙
            var rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            // 11 buttons + 2 group gaps + 8 normal gaps  (#107 - bed 추가)
            float totalW = 11 * BtnW + 8 * Gap + 2 * GroupGap;
            rt.sizeDelta = new Vector2(totalW, BtnH);
            rt.anchoredPosition = new Vector2(0, 40);  // 화면 하단에서 40px

            // Background panel
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.06f, 0.08f, 0.85f);
            // padding 그리기 위해 살짝 키움
            rt.sizeDelta = new Vector2(totalW + 16, BtnH + 12);

            float x = -totalW * 0.5f;

            // Speed group
            pauseBtn  = MakeBtn("멈춤", "(Space)",   x, ()=>{ if (TimeController.Instance!=null) TimeController.Instance.TogglePause(); }); x += BtnW + Gap;
            speed1Btn = MakeBtn("1x",  "(1)",       x, ()=>{ if (TimeController.Instance!=null) TimeController.Instance.SetScale(1f); });   x += BtnW + Gap;
            speed2Btn = MakeBtn("2x",  "(2)",       x, ()=>{ if (TimeController.Instance!=null) TimeController.Instance.SetScale(2f); });   x += BtnW + Gap;
            speed4Btn = MakeBtn("4x",  "(3)",       x, ()=>{ if (TimeController.Instance!=null) TimeController.Instance.SetScale(4f); });   x += BtnW + GroupGap;

            // Draft
            draftBtn  = MakeBtn("징집",  "(R)",      x, ToggleDraft);                                                                       x += BtnW + GroupGap;

            // Build group
            wallBtn   = MakeBtn("벽",    "(B) 5",   x, ()=>SetBuildMode(BuildManager.Mode.Wall));   x += BtnW + Gap;
            floorBtn  = MakeBtn("바닥",  "(F) 1",   x, ()=>SetBuildMode(BuildManager.Mode.Floor)); x += BtnW + Gap;
            doorBtn   = MakeBtn("문",    "(G) 3",   x, ()=>SetBuildMode(BuildManager.Mode.Door));  x += BtnW + Gap;
            stoveBtn  = MakeBtn("화덕",  "(T) 10",  x, ()=>SetBuildMode(BuildManager.Mode.Stove)); x += BtnW + Gap;
            bedBtn    = MakeBtn("침대",  "(Y) 8",   x, ()=>SetBuildMode(BuildManager.Mode.Bed));   x += BtnW + GroupGap;

            // Research
            researchBtn = MakeBtn("연구", "(N)",     x, OpenResearchPicker);
        }

        private Button MakeBtn(string label, string hint, float x, System.Action onClick)
        {
            var go = new GameObject($"Btn_{label}");
            go.transform.SetParent(transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(BtnW, BtnH);
            rt.anchoredPosition = new Vector2(x, 0);
            var img = go.AddComponent<Image>();
            img.color = InactiveBg;
            var btn = go.AddComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            cb.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            btn.colors = cb;
            btn.onClick.AddListener(()=>onClick?.Invoke());

            // 주 라벨
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var lbl = labelGo.AddComponent<Text>();
            lbl.text = label;
            lbl.font = font;
            lbl.fontSize = 20;
            lbl.fontStyle = FontStyle.Bold;
            lbl.color = TextNormal;
            lbl.alignment = TextAnchor.MiddleCenter;
            var lrt = lbl.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 0.4f);
            lrt.anchorMax = new Vector2(1, 1);
            lrt.sizeDelta = Vector2.zero;
            lrt.anchoredPosition = Vector2.zero;

            // hint (작은 글씨)
            var hintGo = new GameObject("Hint");
            hintGo.transform.SetParent(go.transform, false);
            var ht = hintGo.AddComponent<Text>();
            ht.text = hint;
            ht.font = font;
            ht.fontSize = 11;
            ht.color = new Color(0.75f, 0.75f, 0.7f, 0.85f);
            ht.alignment = TextAnchor.MiddleCenter;
            var hrt = ht.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0, 0);
            hrt.anchorMax = new Vector2(1, 0.4f);
            hrt.sizeDelta = Vector2.zero;
            hrt.anchoredPosition = Vector2.zero;

            return btn;
        }

        private void ToggleDraft()
        {
            var cs = Object.FindFirstObjectByType<ClickSelector>();
            if (cs == null || cs.CurrentSelection == null)
            {
                Debug.Log("[Gui] 징집 - 선택된 콜로니스트 없음");
                return;
            }
            cs.CurrentSelection.SetDrafted(!cs.CurrentSelection.IsDrafted);
            Debug.Log($"[Gui] {cs.CurrentSelection.PawnName} 징집 → {cs.CurrentSelection.IsDrafted}");
        }

        private void SetBuildMode(BuildManager.Mode m)
        {
            if (BuildManager.Instance == null) return;
            // 같은 mode 누르면 off (toggle) — Update 의 hotkey 와 동일 동작
            var newMode = (BuildManager.Instance.CurrentMode == m) ? BuildManager.Mode.Off : m;
            BuildManager.Instance.SetMode(newMode);
        }

        private void OpenResearchPicker()
        {
            var ru = Object.FindFirstObjectByType<ResearchUI>();
            if (ru != null) ru.TogglePicker();
        }

        private void Update()
        {
            // 매 프레임 active build mode 따라 button 색 갱신 (가벼움 - 5개 비교)
            if (BuildManager.Instance != null)
            {
                RefreshBuildHighlight(wallBtn,  BuildManager.Instance.CurrentMode == BuildManager.Mode.Wall);
                RefreshBuildHighlight(floorBtn, BuildManager.Instance.CurrentMode == BuildManager.Mode.Floor);
                RefreshBuildHighlight(doorBtn,  BuildManager.Instance.CurrentMode == BuildManager.Mode.Door);
                RefreshBuildHighlight(stoveBtn, BuildManager.Instance.CurrentMode == BuildManager.Mode.Stove);
                RefreshBuildHighlight(bedBtn,   BuildManager.Instance.CurrentMode == BuildManager.Mode.Bed);
            }
            // Speed highlight
            if (TimeController.Instance != null)
            {
                float s = TimeController.Instance.CurrentScale;
                RefreshBuildHighlight(pauseBtn,  s == 0f);
                RefreshBuildHighlight(speed1Btn, Mathf.Approximately(s, 1f));
                RefreshBuildHighlight(speed2Btn, Mathf.Approximately(s, 2f));
                RefreshBuildHighlight(speed4Btn, Mathf.Approximately(s, 4f));
            }
            // Draft highlight (selection 의 IsDrafted) - cachedCs lesson #4
            if (draftBtn != null)
            {
                if (cachedCs == null) cachedCs = Object.FindFirstObjectByType<ClickSelector>();
                bool drafted = cachedCs != null && cachedCs.CurrentSelection != null && cachedCs.CurrentSelection.IsDrafted;
                RefreshBuildHighlight(draftBtn, drafted);
            }
        }

        private void RefreshBuildHighlight(Button btn, bool active)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img == null) return;
            var targetBg = active ? ActiveBg : InactiveBg;
            if (img.color != targetBg) img.color = targetBg;
            // 텍스트 색 (label/hint 둘 다)
            foreach (var t in btn.GetComponentsInChildren<Text>())
            {
                t.color = active ? TextActive : (t.fontSize >= 18 ? TextNormal : new Color(0.75f, 0.75f, 0.7f, 0.85f));
            }
        }
    }
}
