using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// 콜로니심식 콜로니스트 바 (화면 TOP-CENTER HUD).  Self-bootstrapping —
    /// GameManager.Start 가 EnsureInScene() 호출 → 기존 Canvas 를 찾아(GuiControlBar 등이
    /// 만든 것 재사용) 거기에 자기 root 를 붙인다.  Canvas 가 없으면(headless/-batchmode
    /// 또는 MainMenu) 조용히 skip → null throw 없음 (GuiControlBar.EnsureInScene 패턴 미러).
    ///
    /// 각 entry (콜로니스트 1명당, compact):
    ///   - 이름 (PawnEntity.PawnName, "(Clone)" suffix 제거)
    ///   - 작은 HP 바 (fill = Hp / MaxHp, 매 frame 갱신)
    ///   - 작은 mood 바 (PawnNeeds.mood 0..100, 있으면)
    /// 클릭 시: (1) ClickSelector.SimulateSelect 로 선택, (2) CameraController.FocusOn 으로
    ///   카메라를 그 pawn 으로 중앙 정렬.
    ///
    /// 갱신 비용:
    ///   - 전체 entry 리스트 재구축은 콜로니스트 수가 바뀌었을 때 OR 0.5s 간격으로만 (cheap).
    ///   - HP/mood fill 은 캐시한 entity ref 로 매 frame 값만 갱신 (재구축 X, alloc X).
    /// </summary>
    public class ColonistBar : MonoBehaviour
    {
        // ── layout 상수 ──────────────────────────────────────────────────────────
        // #238b 가독성 ↑ (작아서 "테스트 빌드 수준" 회피) — the reference sim 콜로니스트 바처럼 더 큼.
        private const float EntryW = 104f;
        private const float EntryH = 54f;
        private const float Gap = 6f;
        // 운영자 fb: "12시에 림얼굴 나오는거랑 그 뒤 UI 겹침" — 콜로니스트 바(portrait/림얼굴)가
        //   상단 자원 readout(TopBar)과 겹쳤음.  TopBar 는 화면 최상단 full-width 높이 76px
        //   (SceneSetup.Game.TopBar.cs: topRt.sizeDelta y=76, anchor top, y=0)에 clock/time/
        //   자원 chip 을 그린다.  그래서 콜로니스트 바를 TopBar 바로 아래로 내려 절대 안 겹치게 함.
        //   콜로니심식: 바는 최상단 중앙이지만, 이 프로토타입은 최상단을 자원 바가 점유하므로
        //   "자원 바 바로 아래 중앙" 이 깔끔한 동치 위치.
        private const float TopBarH = 76f;     // SceneSetup TopBar 높이와 일치(변경 시 같이 갱신)
        private const float TopBarGap = 8f;    // TopBar 와 콜로니스트 바 사이 숨 쉴 틈
        private const float TopMargin = TopBarH + TopBarGap;  // TopBar 아래로 내림(겹침 방지)
        private const float BarH = 5f;         // HP/mood 바 두께
        // #240 초상화 (the reference sim 콜로니스트 바 시그니처) — entry 좌측 정사각, pawn body sprite.
        private const float PortraitW = 46f;   // 좌측 초상화 폭(정사각)
        private const float PortraitPad = 4f;  // 초상화 ↔ 이름/바 간격

        // ── 색 (UITheme 통일) ──────────────────────────────────────────────────────
        private static readonly Color EntryBg   = MelonS.GameProto.Core.UITheme.PanelBgLight;
        private static readonly Color BorderCol = MelonS.GameProto.Core.UITheme.Divider;
        private static readonly Color TextCol   = MelonS.GameProto.Core.UITheme.TextPrimary;
        private static readonly Color HpTrack   = new Color(0.10f, 0.08f, 0.07f, 0.9f);
        private static readonly Color HpFillCol  = new Color(0.45f, 0.82f, 0.40f, 1f);  // green
        private static readonly Color HpLowCol   = new Color(0.90f, 0.36f, 0.30f, 1f);  // red (저체력)
        private static readonly Color MoodTrack  = new Color(0.10f, 0.08f, 0.07f, 0.9f);
        private static readonly Color MoodFillCol = new Color(0.46f, 0.70f, 0.95f, 1f); // blue

        // ── 한 콜로니스트 entry 의 캐시된 ref (매 frame fill 갱신용) ───────────────
        private class Entry
        {
            public PawnEntity pawn;
            public PawnNeeds needs;   // null 가능 (mood 바 skip)
            public Image hpFill;
            public RectTransform hpFillRt;
            public Image moodFill;    // null 가능
            public RectTransform moodFillRt;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private RectTransform rootRt;
        private Font font;
        private int lastColonistCount = -1;
        private float nextRebuildTime = -1f;
        private const float RebuildInterval = 0.5f;

        // ClickSelector / CameraController 캐시 (lesson #4 — FindFirstObjectByType per-frame 비쌈).
        private ClickSelector cachedCs;
        private CameraController cachedCam;

        private static ColonistBar _instance;

        public static void EnsureInScene()
        {
            if (_instance != null) return;
            // GuiControlBar 와 동일하게 기존 Canvas 재사용 (HUD 가 만든 것).  없으면 skip.
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[ColonistBar] Canvas 없음 - skip");
                return;
            }
            var go = new GameObject("ColonistBar");
            go.transform.SetParent(canvas.transform, false);
            _instance = go.AddComponent<ColonistBar>();
        }

        private void Start()
        {
            font = MelonS.GameProto.Core.UITheme.LoadKoreanFont(16);

            // root panel — 화면 상단 중앙 (anchor top-center).  너비는 rebuild 때 콜로니스트
            //  수에 맞춰 갱신, 높이는 entry 높이로 고정.
            rootRt = gameObject.AddComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.5f, 1f);
            rootRt.anchorMax = new Vector2(0.5f, 1f);
            rootRt.pivot = new Vector2(0.5f, 1f);
            rootRt.anchoredPosition = new Vector2(0, -TopMargin);
            rootRt.sizeDelta = new Vector2(0, EntryH);

            RebuildEntries();
        }

        private void Update()
        {
            // 콜로니스트 수가 바뀌었거나(죽음/스폰) 0.5s 경과 시에만 전체 재구축 (cheap).
            int count = CountColonists();
            if (count != lastColonistCount || Time.unscaledTime >= nextRebuildTime)
            {
                RebuildEntries();
                nextRebuildTime = Time.unscaledTime + RebuildInterval;
            }

            // HP/mood fill 은 매 frame 캐시 ref 로 값만 갱신 (재구축 X, alloc X).
            RefreshFills();
        }

        private int CountColonists()
        {
            // 이 프로토타입엔 PawnEntity 에 faction flag 가 없음 → 모든 PawnEntity = 콜로니스트.
            //  죽은(IsDead) pawn 은 바에서 제외 (the reference sim 바도 시체는 안 보임).
            var all = Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None);
            int n = 0;
            foreach (var p in all)
                if (p != null && !p.IsDead) n++;
            return n;
        }

        private void RebuildEntries()
        {
            // 기존 entry GameObject 제거 후 재생성 (수 변화는 드물어 비용 OK — 매 frame 아님).
            foreach (Transform child in transform) Destroy(child.gameObject);
            entries.Clear();

            var all = Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None);
            // 결정적 순서 (instanceID 정렬) — 매 rebuild 마다 자리 안 바뀌게.
            System.Array.Sort(all, (a, b) =>
            {
                if (a == null || b == null) return 0;
                return a.GetInstanceID().CompareTo(b.GetInstanceID());
            });

            var alive = new List<PawnEntity>();
            foreach (var p in all)
                if (p != null && !p.IsDead) alive.Add(p);

            lastColonistCount = alive.Count;

            int n = alive.Count;
            float totalW = n > 0 ? (n * EntryW + (n - 1) * Gap) : 0f;
            rootRt.sizeDelta = new Vector2(totalW, EntryH);

            float x = -totalW * 0.5f;  // 첫 entry 의 왼쪽 가장자리 (pivot center 기준)
            for (int i = 0; i < n; i++)
            {
                var e = BuildEntry(alive[i], x);
                if (e != null) entries.Add(e);
                x += EntryW + Gap;
            }
        }

        private Entry BuildEntry(PawnEntity pawn, float x)
        {
            var needs = pawn.GetComponent<PawnNeeds>();

            // entry root (border image + inset fill child) — 버튼화.
            var go = new GameObject($"Entry_{StripClone(pawn.name)}");
            go.transform.SetParent(transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(EntryW, EntryH);
            rt.anchoredPosition = new Vector2(x, 0);

            var border = go.AddComponent<Image>();
            border.color = BorderCol;
            // 2px 테두리 안쪽 fill (entry 본체).  MakeBorderedPanel 이 inner content RT 반환.
            var contentRt = MelonS.GameProto.Core.UITheme.MakeBorderedPanel(rt, 2f, EntryBg);
            var fillImg = contentRt.parent.GetComponent<Image>();  // PanelFill image (Button target)
            // MakeBorderedPanel 은 border/fill 의 raycastTarget 을 false 로 둠 → root border 만
            //  클릭 raycast 를 받게 다시 켠다 (이게 없으면 entry 클릭이 안 먹음).
            border.raycastTarget = true;

            // ── 초상화 (좌측 정사각, pawn body sprite) — the reference sim 콜로니스트 바 시그니처 ──
            var portraitGo = new GameObject("Portrait");
            portraitGo.transform.SetParent(contentRt, false);
            var portraitBg = portraitGo.AddComponent<Image>();
            portraitBg.color = new Color(0.08f, 0.06f, 0.05f, 0.95f);  // 어두운 액자 배경
            portraitBg.raycastTarget = false;
            var pRt = portraitBg.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0f, 0f);
            pRt.anchorMax = new Vector2(0f, 1f);   // 좌측 가장자리, 높이 stretch
            pRt.offsetMin = new Vector2(0f, 0f);
            pRt.offsetMax = new Vector2(PortraitW, 0f);  // 폭 = PortraitW
            // pawn body sprite 를 액자 안에 표시 (preserveAspect — 픽셀 비율 유지).
            var psr = pawn.GetComponentInChildren<SpriteRenderer>();
            if (psr != null && psr.sprite != null)
            {
                var portSpriteGo = new GameObject("PortraitSprite");
                portSpriteGo.transform.SetParent(pRt, false);
                var portImg = portSpriteGo.AddComponent<Image>();
                portImg.sprite = psr.sprite;
                portImg.preserveAspect = true;
                portImg.raycastTarget = false;
                var psRt = portImg.GetComponent<RectTransform>();
                psRt.anchorMin = Vector2.zero;
                psRt.anchorMax = Vector2.one;
                psRt.offsetMin = new Vector2(3f, 3f);
                psRt.offsetMax = new Vector2(-3f, -3f);
            }

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = fillImg;
            var cb = btn.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            cb.pressedColor     = new Color(0.72f, 0.72f, 0.72f, 1f);
            cb.selectedColor    = Color.white;
            cb.fadeDuration     = 0.06f;
            btn.colors = cb;

            var pawnCap = pawn;
            btn.onClick.AddListener(() => OnEntryClicked(pawnCap));

            // ── 이름 텍스트 (상단) ──
            string displayName = pawn.PawnName;
            if (string.IsNullOrEmpty(displayName)) displayName = StripClone(pawn.name);
            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(contentRt, false);
            var nameTxt = nameGo.AddComponent<Text>();
            nameTxt.text = displayName;
            nameTxt.font = font;
            nameTxt.fontSize = 15;
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.color = TextCol;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            var nameRt = nameTxt.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 0.42f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.offsetMin = new Vector2(PortraitW + PortraitPad, 0f);  // #240 초상화 우측으로 inset
            nameRt.offsetMax = Vector2.zero;

            // ── HP 바 (이름 아래) ──
            bool hasMood = needs != null;
            // mood 바가 있으면 HP 바를 살짝 위로, 없으면 더 아래 중앙.
            float hpY = hasMood ? 0.30f : 0.18f;
            var (hpFill, hpFillRt) = MakeMiniBar(contentRt, hpY, HpTrack, HpFillCol);

            Image moodFill = null; RectTransform moodFillRt = null;
            if (hasMood)
            {
                var (mf, mfRt) = MakeMiniBar(contentRt, 0.10f, MoodTrack, MoodFillCol);
                moodFill = mf; moodFillRt = mfRt;
            }

            var e = new Entry
            {
                pawn = pawn,
                needs = needs,
                hpFill = hpFill,
                hpFillRt = hpFillRt,
                moodFill = moodFill,
                moodFillRt = moodFillRt,
            };
            // 즉시 1회 값 반영 (rebuild 직후 빈 바 한 frame 방지).
            UpdateEntryFill(e);
            return e;
        }

        // 미니 바 (track + fill).  fill 의 width 를 0..1 비율로 RefreshFills 에서 조절.
        //  반환: (fill Image, fill RectTransform).  track 은 anchored 고정.
        private (Image, RectTransform) MakeMiniBar(RectTransform parent, float anchorY,
            Color trackCol, Color fillCol)
        {
            float pad = 6f;  // 우측 여백
            float leftPad = PortraitW + PortraitPad;  // #240 좌측은 초상화 우측부터 시작
            // track
            var trackGo = new GameObject("BarTrack");
            trackGo.transform.SetParent(parent, false);
            var trackImg = trackGo.AddComponent<Image>();
            trackImg.color = trackCol;
            var trackRt = trackImg.GetComponent<RectTransform>();
            trackRt.anchorMin = new Vector2(0f, anchorY);
            trackRt.anchorMax = new Vector2(1f, anchorY);
            trackRt.pivot = new Vector2(0.5f, 0.5f);
            trackRt.offsetMin = new Vector2(leftPad, -BarH * 0.5f);
            trackRt.offsetMax = new Vector2(-pad, BarH * 0.5f);

            // fill (track 의 왼쪽 정렬, width = 비율 * trackWidth — RefreshFills 에서 anchorMax.x 조절)
            var fillGo = new GameObject("BarFill");
            fillGo.transform.SetParent(trackGo.transform, false);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = fillCol;
            var fillRt = fillImg.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, 1f);  // 비율은 anchorMax.x 로 조절
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            fillRt.pivot = new Vector2(0f, 0.5f);

            return (fillImg, fillRt);
        }

        private void RefreshFills()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                // pawn 이 파괴됐으면 다음 rebuild 가 정리 — 이 frame 은 skip (NRE 방지).
                if (e.pawn == null || e.pawn.gameObject == null) continue;
                UpdateEntryFill(e);
            }
        }

        private void UpdateEntryFill(Entry e)
        {
            // HP
            if (e.hpFillRt != null && e.pawn != null)
            {
                int maxHp = Mathf.Max(1, e.pawn.MaxHp);
                float ratio = Mathf.Clamp01((float)e.pawn.Hp / maxHp);
                var am = e.hpFillRt.anchorMax;
                am.x = ratio;
                e.hpFillRt.anchorMax = am;
                // 저체력(<30%) 이면 빨강.
                if (e.hpFill != null)
                    e.hpFill.color = ratio < 0.30f ? HpLowCol : HpFillCol;
            }
            // mood (있을 때만)
            if (e.moodFillRt != null && e.needs != null)
            {
                float ratio = Mathf.Clamp01(e.needs.mood / 100f);
                var am = e.moodFillRt.anchorMax;
                am.x = ratio;
                e.moodFillRt.anchorMax = am;
            }
        }

        private void OnEntryClicked(PawnEntity pawn)
        {
            if (pawn == null || pawn.gameObject == null) return;
            // (1) 선택 — ClickSelector.SimulateSelect (Select 를 public 노출한 진입점, 카메라
            //  부드러운 follow 도 그 안에서 RequestFocus 로 트리거됨).
            if (cachedCs == null) cachedCs = Object.FindFirstObjectByType<ClickSelector>();
            if (cachedCs != null) cachedCs.SimulateSelect(pawn);

            // (2) 카메라 즉시 중앙 정렬 (bar 클릭 = "지금 바로 보여달라").
            if (cachedCam == null) cachedCam = Object.FindFirstObjectByType<CameraController>();
            if (cachedCam != null) cachedCam.FocusOn(pawn.transform.position);
        }

        private static string StripClone(string n)
        {
            if (string.IsNullOrEmpty(n)) return n;
            int idx = n.IndexOf("(Clone)");
            return idx >= 0 ? n.Substring(0, idx).TrimEnd() : n;
        }
    }
}
