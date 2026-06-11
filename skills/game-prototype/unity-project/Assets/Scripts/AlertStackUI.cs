using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// Wave W-M3-01 Lane B / wiki #22 (Dim6, the one HIGH UI gap) — the missing
    /// top-right alert/letter stack.
    ///
    /// the reference sim convention ([User interface]): alerts/letters stack TOP-RIGHT,
    /// persistent + clickable, and clicking jumps the camera to the source.
    /// Today PawnSim fires events ONLY into the bottom-center scrolling EventLog
    /// with no persistent, clickable, jump-to-source card — so threats are easy
    /// to miss (loop-legibility gap, overlaps Dim5 trust).
    ///
    /// Wiki acceptance criterion (#22):
    ///   "A raid creates a persistent top-right card; clicking it pans the
    ///    camera to the bandits."
    ///
    /// Lane contract (STRICT file isolation): this wave may add ONLY this one
    /// file.  It MUST NOT edit AIDirector.cs (it only += subscribes to the
    /// existing OnEventFired event), EventLogUI.cs, or any SceneSetup*.cs.  So
    /// AlertStackUI is SELF-BOOTSTRAPPING, exactly like EventLogUI is a separate
    /// canvas object and like FlickerLight self-attaches:
    ///   - a [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] spawns one persistent
    ///     manager GameObject (a SEPARATE object from EventLogUI), which builds
    ///     its own top-right Canvas in the shared UITheme bordered-panel style;
    ///   - the manager polls for the live AIDirector each frame until it finds one
    ///     and subscribes READ-ONLY to OnEventFired (lesson #7 firewall: we do not
    ///     subscribe to a singleton in OnEnable with no live ref — we poll-find
    ///     via Update and re-find if the director is replaced/destroyed).
    ///
    /// runInBackground note (bug-pattern #9): expiry uses Time.unscaledTime
    /// deltas checked inside Update (NOT a WaitForSeconds heartbeat coroutine),
    /// so this introduces NO new always-on background timer.  AutoScreenshotter
    /// already owns Application.runInBackground for CLI QA paths; nothing here
    /// adds a background-only heartbeat that would freeze on focus loss.
    ///
    /// >>> QA FLAG (camera-pan target precision): GameEvent carries NO source
    ///     position field (id/title/description/flavor/threatTier only), and we
    ///     may not edit AIDirector to add one this lane.  So the pan target is a
    ///     best-effort heuristic, NOT the exact bandit spawn point:
    ///       1. the nearest live BanditEnemy to the colony (best proxy for "the
    ///          bandits" — covers the raid acceptance case), else
    ///       2. the centroid of living PawnMovement colonists (the colony), else
    ///       3. the first PawnMovement, else
    ///       4. world center (0,0).
    ///     This satisfies "pans the camera toward the threat" but is approximate.
    ///     If exact per-event source panning is required, AIDirector.GameEvent
    ///     needs a source-position field — flag for a follow-up wave (out of lane).
    /// </summary>
    public class AlertStackUI : MonoBehaviour
    {
        // ---- tunables (SerializeField so designer can tweak without code) ----
        [Header("Stack feel")]
        [SerializeField] private float cardLifetimeSec = 30f;   // auto-expire window
        [SerializeField] private int maxCards = 3;              // hard cap (oldest evicted) — #ui백로그 7.0: 카드 밴드 -88~-232 예약(아래 EventLog 와 좌표 계약)
        [SerializeField] private float cardWidth = 230f;
        [SerializeField] private float cardHeight = 44f;
        [SerializeField] private float cardGap = 6f;
        [SerializeField] private float edgeMargin = 12f;        // inset from right edge
        // #275 실제 TopBar 높이는 76px(SceneSetup.Game.TopBar.cs:27, ColonistBar 와 동일).
        //  이전 60f 는 과소평가라 알림 카드가 상단바 하단 16px 와 겹쳤다.  76 으로 교정 →
        //  첫 카드가 y = -(76+12) = -88 에서 시작해 상단 자원바 아래로 내려간다.
        [SerializeField] private float topBarHeight = 76f;      // TopBar header height (검증됨)
        [SerializeField] private int fontSize = 15;

        [Header("Camera pan")]
        [SerializeField] private float panDurationSec = 0.6f;   // matches CameraController follow feel
        [SerializeField] private float panSpeedUnitsPerSec = 30f;

        // ---- runtime state ----
        private AIDirector director;          // live ref, poll-found (lesson #7)
        private Canvas canvas;
        private RectTransform stackRoot;      // top-right anchored column container
        private Font font;

        private readonly List<AlertCard> cards = new List<AlertCard>(8);

        // active camera pan
        private Camera panCam;
        private Vector3 panTarget;
        private float panEndTime = -1f;

        // ============================================================
        // Self-bootstrap (no SceneSetup edit) — one persistent manager.
        // ============================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Game-scene gate: never spawn on MainMenu (operator 2026-05-30).
            GameSceneGate.RunWhenGameScene(() =>
            {
                // Idempotent: never spawn a second stack on scene/domain reload.
                if (FindExisting() != null) return;
                var go = new GameObject("~AlertStackUI");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<AlertStackUI>();
                    });
        }

        private static AlertStackUI FindExisting()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<AlertStackUI>();
#else
            return Object.FindObjectOfType<AlertStackUI>();
#endif
        }

        private void Awake()
        {
            font = UITheme.LoadKoreanFont(fontSize);
            BuildCanvas();
        }

        private void BuildCanvas()
        {
            // Own Canvas (separate object from EventLogUI's) so we never touch
            // any existing UI hierarchy.  Screen-space overlay, high sort order
            // so alerts sit above world + other panels.
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // Top-right anchored column root.  Pivot top-right so cards grow
            // downward from the top-right corner.
            var rootGo = new GameObject("AlertStackRoot");
            rootGo.transform.SetParent(transform, false);
            stackRoot = rootGo.AddComponent<RectTransform>();
            stackRoot.anchorMin = new Vector2(1f, 1f);
            stackRoot.anchorMax = new Vector2(1f, 1f);
            stackRoot.pivot = new Vector2(1f, 1f);
            // §3.5 top inset: drop below the 60px top resource bar (was -edgeMargin).
            stackRoot.anchoredPosition = new Vector2(-edgeMargin, -(topBarHeight + edgeMargin));
            stackRoot.sizeDelta = new Vector2(cardWidth, 0f);
        }

        private void OnDestroy()
        {
            if (director != null) director.OnEventFired -= HandleEvent;
        }

        private void Update()
        {
            EnsureDirector();
            ExpireCards();
            RelayoutCards();
            TickPan();
        }

        // ---- director binding (lesson #7: poll-find, never blind OnEnable) ----
        private void EnsureDirector()
        {
            if (director != null) return;
            var live = FindDirector();
            if (live == null) return;  // retry next frame rather than crash
            director = live;
            director.OnEventFired += HandleEvent;
        }

        private static AIDirector FindDirector()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<AIDirector>();
#else
            return Object.FindObjectOfType<AIDirector>();
#endif
        }

        // ---- event handling -------------------------------------------------
        private void HandleEvent(GameEvent ev)
        {
            if (ev == null) return;
            // Only threats deserve a persistent letter (raid / wolf_pack /
            // mental-break / siege / infestation / large_raid).  Tier 0 ambient
            // flavor stays in the scrolling EventLog only.
            if (ev.threatTier < 1) return;

            string title = string.IsNullOrEmpty(ev.title) ? "위협" : ev.title;
            Color tierColor = TierColor(ev.threatTier);
            PushCard(title, tierColor, ev.threatTier);
        }

        /// <summary>tier1 amber (UI_ORANGE), tier3 DANGER_RED; tier2 between.</summary>
        private static Color TierColor(int tier)
        {
            if (tier >= 3) return UITheme.TextDanger;                 // DANGER_RED
            if (tier == 2) return Color.Lerp(UITheme.AccentOrange, UITheme.TextDanger, 0.5f);
            return UITheme.AccentOrange;                              // tier1 amber/orange
        }

        /// <summary>r2 #3 (2026-06-12) — 디렉터 외 발신자(니즈/헬스)용 단일 진입점.
        ///  인스턴스 없으면 무음 no-op (헤드리스/V-scene 안전).</summary>
        public static void Notify(string title, int tier)
        {
            var inst = Object.FindFirstObjectByType<AlertStackUI>();
            if (inst == null) return;
            inst.PushCard(title, TierColor(tier), tier);
        }

        private void PushCard(string title, Color accent, int tier)
        {
            // Evict oldest if over the cap so the corner never overflows.
            while (cards.Count >= maxCards)
            {
                var oldest = cards[0];
                cards.RemoveAt(0);
                if (oldest != null && oldest.root != null) Destroy(oldest.root.gameObject);
            }

            var card = AlertCard.Build(stackRoot, title, accent, font, fontSize,
                                       cardWidth, cardHeight);
            card.spawnTime = Time.unscaledTime;
            card.tier = tier;
            card.onClick = () => OnCardClicked(card);
            cards.Add(card);
            RelayoutCards();
        }

        private void OnCardClicked(AlertCard card)
        {
            // Click pans the camera toward the threat, then dismisses the card.
            BeginPanToThreat();
            DismissCard(card);
        }

        private void DismissCard(AlertCard card)
        {
            int idx = cards.IndexOf(card);
            if (idx >= 0) cards.RemoveAt(idx);
            if (card != null && card.root != null) Destroy(card.root.gameObject);
        }

        private void ExpireCards()
        {
            float now = Time.unscaledTime;
            for (int i = cards.Count - 1; i >= 0; i--)
            {
                var c = cards[i];
                if (c == null || c.root == null) { cards.RemoveAt(i); continue; }
                // #ui백로그 7.4 — tier2+ (습격/대형 위협) 카드는 시간 만료 제외: 클래스 doc
                //  수용 기준 'persistent card' 대로 클릭 dismiss 만 허용.  진행 중인 레이드
                //  알림이 30초 만에 사라져 미대응 위협을 다시 놓치던 회귀 차단.  tier1 잡사건
                //  은 30s 유지, maxCards eviction 은 그대로라 오버플로 안전.
                if (c.tier >= 2) continue;
                if (now - c.spawnTime >= cardLifetimeSec)
                {
                    cards.RemoveAt(i);
                    Destroy(c.root.gameObject);
                }
            }
        }

        private void RelayoutCards()
        {
            // Stack downward from the top-right corner; newest on top.
            float y = 0f;
            for (int i = cards.Count - 1; i >= 0; i--)
            {
                var c = cards[i];
                if (c == null || c.root == null) continue;
                c.root.anchoredPosition = new Vector2(0f, y);
                y -= (cardHeight + cardGap);
            }
        }

        // ---- camera pan -----------------------------------------------------
        private void BeginPanToThreat()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 t = ResolveThreatTarget(cam);
            t.z = cam.transform.position.z;  // keep camera depth
            panCam = cam;
            panTarget = t;
            panEndTime = Time.unscaledTime + panDurationSec;
        }

        /// <summary>
        /// Best-effort threat position (see QA FLAG in class doc): nearest bandit
        /// to colony -> colony centroid -> first pawn -> world center.
        /// </summary>
        private Vector3 ResolveThreatTarget(Camera cam)
        {
            // Colony centroid first (used both as a result and as the bandit
            // proximity anchor).
            Vector3 colony;
            bool haveColony = TryColonyCentroid(out colony);

            // 1) 콜로니 최근접 적 (산적 ∪ 늑대).  #ui백로그 7.3 — 이전엔 BanditEnemy 만
            //  검색해 wolf_pack 카드 클릭이 엉뚱한 곳(산적 또는 콜로니 중심)으로 팬했다.
            //  ThreatAlertUI.CheckThreats 와 동일하게 WolfEnemy 도 위협 후보에 포함.
            {
                Vector3 anchor = haveColony ? colony : Vector3.zero;
                Transform nearest = null;
                float best = float.MaxValue;
                BanditEnemy[] bandits = FindBandits();
                if (bandits != null)
                    for (int i = 0; i < bandits.Length; i++)
                    {
                        var b = bandits[i];
                        if (b == null) continue;
                        float d = (b.transform.position - anchor).sqrMagnitude;
                        if (d < best) { best = d; nearest = b.transform; }
                    }
                WolfEnemy[] wolves = FindWolves();
                if (wolves != null)
                    for (int i = 0; i < wolves.Length; i++)
                    {
                        var w = wolves[i];
                        if (w == null) continue;
                        float d = (w.transform.position - anchor).sqrMagnitude;
                        if (d < best) { best = d; nearest = w.transform; }
                    }
                if (nearest != null) return nearest.position;
            }

            // 2) colony centroid.
            if (haveColony) return colony;

            // 3) first pawn.
            var firstPawn = FindFirstPawn();
            if (firstPawn != null) return firstPawn.transform.position;

            // 4) world center.
            return Vector3.zero;
        }

        private bool TryColonyCentroid(out Vector3 centroid)
        {
            centroid = Vector3.zero;
            var pawns = FindPawns();
            if (pawns == null || pawns.Length == 0) return false;
            Vector3 sum = Vector3.zero;
            int n = 0;
            for (int i = 0; i < pawns.Length; i++)
            {
                var p = pawns[i];
                if (p == null) continue;
                sum += p.transform.position;
                n++;
            }
            if (n == 0) return false;
            centroid = sum / n;
            return true;
        }

        private static BanditEnemy[] FindBandits()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<BanditEnemy>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<BanditEnemy>();
#endif
        }

        private static WolfEnemy[] FindWolves()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<WolfEnemy>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<WolfEnemy>();
#endif
        }

        private static PawnMovement[] FindPawns()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<PawnMovement>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<PawnMovement>();
#endif
        }

        private static PawnMovement FindFirstPawn()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<PawnMovement>();
#else
            return Object.FindObjectOfType<PawnMovement>();
#endif
        }

        private void TickPan()
        {
            if (panCam == null || Time.unscaledTime > panEndTime) { panCam = null; return; }
            // MoveTowards (fixed units/sec) is deterministic in batchmode where
            // unscaledDeltaTime is tiny — mirrors CameraController.RequestFocus.
            float maxStep = panSpeedUnitsPerSec * Time.unscaledDeltaTime;
            if (maxStep < 0.5f) maxStep = 0.5f;
            panCam.transform.position =
                Vector3.MoveTowards(panCam.transform.position, panTarget, maxStep);
            if ((panCam.transform.position - panTarget).sqrMagnitude < 0.0004f)
                panCam = null;  // arrived
        }

        // ============================================================
        // Card view + click handling (no per-frame allocation).
        // ============================================================
        private sealed class AlertCard
        {
            public RectTransform root;
            public float spawnTime;
            public int tier;           // #7.4 — tier2+ 는 시간 만료 제외(영속)
            public System.Action onClick;

            public static AlertCard Build(RectTransform parent, string title, Color accent,
                                          Font font, int fontSize, float w, float h)
            {
                var card = new AlertCard();

                var go = new GameObject("AlertCard");
                go.transform.SetParent(parent, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.sizeDelta = new Vector2(w, h);
                card.root = rt;

                // Shared bordered-panel treatment (warm dark-brown fill + 1-2px
                // border) so the alert matches every other panel.
                var content = UITheme.MakeBorderedPanel(rt, bg: UITheme.PanelBg,
                                                        pad: UITheme.PadOuter * 0.4f);

                // Tier-colored accent bar down the left edge — the threat color
                // (tier1 amber, tier3 DANGER_RED) reads at a glance.
                var barGo = new GameObject("AccentBar");
                barGo.transform.SetParent(content, false);
                var barRt = barGo.AddComponent<RectTransform>();
                barRt.anchorMin = new Vector2(0f, 0f);
                barRt.anchorMax = new Vector2(0f, 1f);
                barRt.pivot = new Vector2(0f, 0.5f);
                barRt.sizeDelta = new Vector2(4f, 0f);
                barRt.anchoredPosition = Vector2.zero;
                var barImg = barGo.AddComponent<Image>();
                barImg.color = accent;
                barImg.raycastTarget = false;

                // Title label (tier-tinted, slightly brighter than the bar).
                var txtGo = new GameObject("Title");
                txtGo.transform.SetParent(content, false);
                var txtRt = txtGo.AddComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = new Vector2(10f, 0f);
                txtRt.offsetMax = new Vector2(-4f, 0f);
                var txt = txtGo.AddComponent<Text>();
                txt.font = font;
                txt.fontSize = fontSize;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleLeft;
                txt.color = Color.Lerp(accent, UITheme.TextPrimary, 0.25f);
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                txt.verticalOverflow = VerticalWrapMode.Truncate;
                txt.raycastTarget = false;
                txt.text = title;

                // Transparent full-card click catcher (raycast target ON) so the
                // whole card is clickable, not just the text.
                var hitGo = new GameObject("Hit");
                hitGo.transform.SetParent(rt, false);
                var hitRt = hitGo.AddComponent<RectTransform>();
                hitRt.anchorMin = Vector2.zero;
                hitRt.anchorMax = Vector2.one;
                hitRt.offsetMin = Vector2.zero;
                hitRt.offsetMax = Vector2.zero;
                var hitImg = hitGo.AddComponent<Image>();
                hitImg.color = new Color(0f, 0f, 0f, 0f); // invisible but raycastable
                hitImg.raycastTarget = true;
                var click = hitGo.AddComponent<CardClick>();
                click.bind = () => card.onClick?.Invoke();

                return card;
            }
        }

        /// <summary>Pointer-click forwarder for a card's hit area.</summary>
        private sealed class CardClick : MonoBehaviour, IPointerClickHandler
        {
            public System.Action bind;
            public void OnPointerClick(PointerEventData eventData) => bind?.Invoke();
        }
    }
}
