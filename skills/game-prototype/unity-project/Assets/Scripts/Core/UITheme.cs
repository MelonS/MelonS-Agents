using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto.Core
{
    /// <summary>
    /// #185 - UI 톤 통일 (운영자 fb "디자인 구려" 응답).
    ///
    /// Kenney Tiny Town 의 warm earth tone 과 매치되는 palette.
    /// 모든 panel/button 이 이 색을 사용해야 the reference sim vibe 통일.
    ///
    /// 전체 톤: dark warm brown (배경) + warm gold (제목) + cream (본문) + warm orange (active).
    /// Korean 한글 가독성 위해 대비 충분.
    /// </summary>
    public static class UITheme
    {
        // ── UI 팔레트 시스템 (2026-07-25 운영자 "옵션에서 변경 + F 크림 디폴트") ──
        //  4팔레트: 0=크림(F, 디폴트) 1=웜앰버(A, 구 기본) 2=미드나잇(D) 3=차콜(E).
        //  기존 readonly 필드를 프로퍼티로 전환 — 호출부 소스 호환.  선택은
        //  PlayerPrefs("ui_palette"), 씬 베이커는 ForcePalette 로 디폴트 고정
        //  (베이크 결정론 — 로컬 pref 가 씬 해시에 새지 않게).
        //  알려진 한계: 베이크 시점 색은 재시작/씬 재입장 시 완전 적용.
        private class Pal
        {
            public Color panelBg, panelBgLight, headerBg, accentGold, accentOrange, accentTan,
                         textPrimary, textSecondary, textDark, textDanger, btnHover, divider, shadowBg;
        }

        public static readonly string[] PaletteNames = { "크림", "웜 앰버", "미드나잇", "차콜" };

        private static readonly Pal[] Pals =
        {
            new Pal {   // 0 크림 (F — 보드게임 라이트)
                panelBg = new Color(0.949f, 0.910f, 0.835f, 0.94f),
                panelBgLight = new Color(0.898f, 0.835f, 0.722f, 0.93f),
                headerBg = new Color(0.871f, 0.796f, 0.655f, 0.96f),
                accentGold = new Color(0.620f, 0.443f, 0.129f, 1f),
                accentOrange = new Color(0.769f, 0.478f, 0.173f, 1f),
                accentTan = new Color(0.663f, 0.510f, 0.345f, 1f),
                textPrimary = new Color(0.227f, 0.184f, 0.133f, 1f),
                textSecondary = new Color(0.541f, 0.463f, 0.345f, 1f),
                textDark = new Color(0.988f, 0.957f, 0.898f, 1f),   // 액센트 버튼 위 = 밝은 크림
                textDanger = new Color(0.753f, 0.290f, 0.204f, 1f),
                btnHover = new Color(0.835f, 0.761f, 0.629f, 0.95f),
                divider = new Color(0.722f, 0.624f, 0.471f, 1f),
                shadowBg = new Color(0f, 0f, 0f, 0.25f),
            },
            new Pal {   // 1 웜 앰버 (A — 구 기본 브라운)
                panelBg = new Color(0.165f, 0.122f, 0.094f, 0.86f),
                panelBgLight = new Color(0.220f, 0.165f, 0.125f, 0.85f),
                headerBg = new Color(0.286f, 0.196f, 0.137f, 0.95f),
                accentGold = new Color(0.957f, 0.843f, 0.541f, 1f),
                accentOrange = new Color(0.910f, 0.710f, 0.376f, 1f),
                accentTan = new Color(0.784f, 0.584f, 0.420f, 1f),
                textPrimary = new Color(0.949f, 0.894f, 0.816f, 1f),
                textSecondary = new Color(0.733f, 0.667f, 0.580f, 1f),
                textDark = new Color(0.118f, 0.086f, 0.063f, 1f),
                textDanger = new Color(0.953f, 0.439f, 0.357f, 1f),
                btnHover = new Color(0.310f, 0.231f, 0.176f, 0.95f),
                divider = new Color(0.353f, 0.255f, 0.180f, 1f),
                shadowBg = new Color(0f, 0f, 0f, 0.45f),
            },
            new Pal {   // 2 미드나잇 (D — 네이비+골드)
                panelBg = new Color(0.102f, 0.125f, 0.173f, 0.91f),
                panelBgLight = new Color(0.149f, 0.180f, 0.243f, 0.90f),
                headerBg = new Color(0.173f, 0.212f, 0.286f, 0.95f),
                accentGold = new Color(0.910f, 0.710f, 0.302f, 1f),
                accentOrange = new Color(0.910f, 0.710f, 0.302f, 1f),
                accentTan = new Color(0.604f, 0.659f, 0.753f, 1f),
                textPrimary = new Color(0.910f, 0.925f, 0.957f, 1f),
                textSecondary = new Color(0.604f, 0.659f, 0.753f, 1f),
                textDark = new Color(0.071f, 0.086f, 0.118f, 1f),
                textDanger = new Color(0.953f, 0.439f, 0.357f, 1f),
                btnHover = new Color(0.216f, 0.259f, 0.349f, 0.95f),
                divider = new Color(0.290f, 0.353f, 0.455f, 1f),
                shadowBg = new Color(0f, 0f, 0f, 0.45f),
            },
            new Pal {   // 3 차콜 (E — 무채+세이지)
                panelBg = new Color(0.141f, 0.149f, 0.157f, 0.91f),
                panelBgLight = new Color(0.192f, 0.204f, 0.216f, 0.90f),
                headerBg = new Color(0.235f, 0.247f, 0.259f, 0.95f),
                accentGold = new Color(0.639f, 0.773f, 0.522f, 1f),
                accentOrange = new Color(0.639f, 0.773f, 0.522f, 1f),
                accentTan = new Color(0.549f, 0.600f, 0.522f, 1f),
                textPrimary = new Color(0.925f, 0.925f, 0.918f, 1f),
                textSecondary = new Color(0.659f, 0.675f, 0.659f, 1f),
                textDark = new Color(0.098f, 0.110f, 0.098f, 1f),
                textDanger = new Color(0.953f, 0.439f, 0.357f, 1f),
                btnHover = new Color(0.271f, 0.290f, 0.302f, 0.95f),
                divider = new Color(0.322f, 0.337f, 0.353f, 1f),
                shadowBg = new Color(0f, 0f, 0f, 0.45f),
            },
        };

        private static int _cur = -1;
        private static Pal P
        {
            get
            {
                if (_cur < 0) _cur = PlayerPrefs.GetInt("ui_palette", 0);
                return Pals[Mathf.Clamp(_cur, 0, Pals.Length - 1)];
            }
        }

        public static int CurrentPalette => _cur < 0 ? PlayerPrefs.GetInt("ui_palette", 0) : _cur;

        /// <summary>설정 메뉴에서 호출 — 저장 + 즉시 전환(런타임 생성 UI 부터 반영).</summary>
        public static void SetPalette(int id)
        {
            _cur = Mathf.Clamp(id, 0, Pals.Length - 1);
            PlayerPrefs.SetInt("ui_palette", _cur);
            PlayerPrefs.Save();
        }

        /// <summary>씬 베이커 전용 — pref 무시하고 고정 (베이크 결정론).</summary>
        public static void ForcePalette(int id) { _cur = Mathf.Clamp(id, 0, Pals.Length - 1); }

        public static Color PanelBg       => P.panelBg;
        public static Color PanelBgLight  => P.panelBgLight;
        public static Color HeaderBg      => P.headerBg;
        public static Color AccentGold    => P.accentGold;
        public static Color AccentOrange  => P.accentOrange;
        public static Color AccentTan     => P.accentTan;
        public static Color TextPrimary   => P.textPrimary;
        public static Color TextSecondary => P.textSecondary;
        public static Color TextDark      => P.textDark;
        public static Color TextDanger    => P.textDanger;
        public static Color BtnInactiveBg => P.panelBgLight;
        public static Color BtnActiveBg   => P.accentOrange;
        public static Color BtnHover      => P.btnHover;
        public static Color Divider       => P.divider;
        public static Color ShadowBg      => P.shadowBg;

        /// <summary>Korean 폰트 fallback chain.  순서대로 system 에서 사용 가능한 첫 거.</summary>
        public static readonly string[] KoreanFontCandidates = {
            "Malgun Gothic", "Nanum Gothic", "Nanum Square",
            "Gulim", "Dotum", "Batang", "Arial Unicode MS"
        };

        public static Font LoadKoreanFont(int size = 16)
        {
            // WebGL 은 OS 폰트 접근이 없어 번들 폰트(Noto Sans KR, OFL)가 유일한 한글 경로.
            // 2단 폰트 (운영자 2026-07-25 "너무 두꺼워, 폰트 2개 써야 하나"):
            //  본문 기본 = 고운돋움(라운드·가벼움), 타이틀·로고 = LoadDisplayFont(BitBit).
            var bundled = Resources.Load<Font>("Fonts/GowunDodum")
                          ?? Resources.Load<Font>("Fonts/DNFBitBit")
                          ?? Resources.Load<Font>("Fonts/NotoSansKR");
            if (bundled != null) return bundled;
            foreach (var name in KoreanFontCandidates)
            {
                var f = Font.CreateDynamicFontFromOSFont(name, size);
                if (f != null) return f;
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>디스플레이(타이틀·로고) 폰트 — BitBit.  본문에 쓰지 말 것 (두께).</summary>
        public static Font LoadDisplayFont()
        {
            return Resources.Load<Font>("Fonts/DNFBitBit") ?? LoadKoreanFont();
        }

        // ── Padding rhythm (one scale everywhere) ────────────────────────────
        public const float PadOuter = 12f;   // panel inner padding
        public const float RowGap   = 6f;    // gap between rows
        public const float BorderPx = 2f;    // border thickness on every panel

        /// <summary>
        /// #UI-restyle — ONE shared panel system.  The root Image is the BORDER
        /// (Divider color); an inset child Image is the PanelBg fill.  This gives
        /// every panel (control bar, tooltip, inspector, name plate) the same
        /// "warm dark-brown panel + 1-2px lighter border" the reference sim treatment
        /// without a 9-slice sprite.  Returns the INNER content RectTransform —
        /// parent your text/rows to it so they sit inside the border + padding.
        /// </summary>
        /// <param name="root">RectTransform that becomes the bordered frame.</param>
        /// <param name="border">border thickness (defaults BorderPx).</param>
        /// <param name="bg">fill color (defaults PanelBg).</param>
        /// <param name="pad">extra inner padding inset for the content child (0 = flush to fill).</param>
        public static RectTransform MakeBorderedPanel(RectTransform root, float border = -1f,
                                                      Color? bg = null, float pad = 0f)
        {
            if (border < 0f) border = BorderPx;
            Color fill = bg ?? PanelBg;

            // root Image == the border color (shows on all 4 edges)
            var borderImg = root.GetComponent<Image>();
            if (borderImg == null) borderImg = root.gameObject.AddComponent<Image>();
            borderImg.color = Divider;
            borderImg.raycastTarget = false;

            // inset fill child
            var fillGo = new GameObject("PanelFill");
            fillGo.transform.SetParent(root, false);
            var fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(border, border);
            fillRt.offsetMax = new Vector2(-border, -border);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = fill;
            fillImg.raycastTarget = false;

            // content child (padded inside the fill)
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(fillRt, false);
            var contentRt = contentGo.AddComponent<RectTransform>();
            contentRt.anchorMin = Vector2.zero;
            contentRt.anchorMax = Vector2.one;
            contentRt.offsetMin = new Vector2(pad, pad);
            contentRt.offsetMax = new Vector2(-pad, -pad);
            return contentRt;
        }

        /// <summary>
        /// Adds a thin Divider-colored vertical line as a child of <paramref name="parent"/>.
        /// Used for group separators in the control bar.
        /// </summary>
        public static Image MakeVDivider(RectTransform parent, float anchoredX, float height, float thickness = 2f)
        {
            var go = new GameObject("VDivider");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(thickness, height);
            rt.anchoredPosition = new Vector2(anchoredX, 0f);
            var img = go.AddComponent<Image>();
            img.color = Divider;
            img.raycastTarget = false;
            return img;
        }
    }
}
