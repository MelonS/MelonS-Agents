using UnityEngine;

namespace MelonS.GameProto.Core
{
    /// <summary>
    /// #185 - UI 톤 통일 (운영자 fb "디자인 구려" 응답).
    ///
    /// Kenney Tiny Town 의 warm earth tone 과 매치되는 palette.
    /// 모든 panel/button 이 이 색을 사용해야 RimWorld vibe 통일.
    ///
    /// 전체 톤: dark warm brown (배경) + warm gold (제목) + cream (본문) + warm orange (active).
    /// Korean 한글 가독성 위해 대비 충분.
    /// </summary>
    public static class UITheme
    {
        // Background tones (warm browns)
        public static readonly Color PanelBg       = new Color(0.165f, 0.122f, 0.094f, 0.94f);  // #2A1F18 alpha 0.94 - dark warm brown
        public static readonly Color PanelBgLight  = new Color(0.220f, 0.165f, 0.125f, 0.92f);  // #382A20 alpha 0.92 - lighter brown
        public static readonly Color HeaderBg      = new Color(0.286f, 0.196f, 0.137f, 0.95f);  // #493223 alpha 0.95 - header brown

        // Accent tones (gold/orange)
        public static readonly Color AccentGold    = new Color(0.957f, 0.843f, 0.541f, 1f);     // #F4D78A - 제목 (밝은 골드)
        public static readonly Color AccentOrange  = new Color(0.910f, 0.710f, 0.376f, 1f);     // #E8B560 - active button (warm orange)
        public static readonly Color AccentTan     = new Color(0.784f, 0.584f, 0.420f, 1f);     // #C8956B - secondary accent

        // Text tones
        public static readonly Color TextPrimary   = new Color(0.949f, 0.894f, 0.816f, 1f);     // #F2E4D0 - 본문 (cream)
        public static readonly Color TextSecondary = new Color(0.733f, 0.667f, 0.580f, 1f);     // #BBAA94 - 부제목/hint (muted cream)
        public static readonly Color TextDark      = new Color(0.118f, 0.086f, 0.063f, 1f);     // #1E1610 - active 버튼 위 검정에 가까운 텍스트
        public static readonly Color TextDanger    = new Color(0.953f, 0.439f, 0.357f, 1f);     // #F37058 - 빨강 경고

        // Button states
        public static readonly Color BtnInactiveBg = PanelBgLight;
        public static readonly Color BtnActiveBg   = AccentOrange;
        public static readonly Color BtnHover      = new Color(0.310f, 0.231f, 0.176f, 0.95f);  // #4F3B2D

        // Misc UI
        public static readonly Color Divider       = new Color(0.353f, 0.255f, 0.180f, 1f);     // #5A412E
        public static readonly Color ShadowBg      = new Color(0.0f, 0.0f, 0.0f, 0.45f);

        /// <summary>Korean 폰트 fallback chain.  순서대로 system 에서 사용 가능한 첫 거.</summary>
        public static readonly string[] KoreanFontCandidates = {
            "Malgun Gothic", "Nanum Gothic", "Nanum Square",
            "Gulim", "Dotum", "Batang", "Arial Unicode MS"
        };

        public static Font LoadKoreanFont(int size = 16)
        {
            foreach (var name in KoreanFontCandidates)
            {
                var f = Font.CreateDynamicFontFromOSFont(name, size);
                if (f != null) return f;
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
