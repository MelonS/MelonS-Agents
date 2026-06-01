using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 42 — Always-on floating mini-bars above pawn head:
    ///   - HP bar (red) — top
    ///   - Mood bar (yellow) — below HP
    /// Built procedurally (4 SpriteRenderers per pawn: 2 backgrounds + 2 fills).
    /// Position auto-tracks transform with a fixed world-space offset.
    ///
    /// Color rules (colony-sim-style):
    ///   HP   < 30%: dark red,   < 70%: orange-red, else bright red
    ///   Mood < 25%: red (breaking), < 50%: orange, else yellow
    /// </summary>
    public class PawnFloatingBars : MonoBehaviour
    {
        // #199 A2 ortho 3.5 + 1x1 pawn — 머리 ≈ +0.5 (예전 2-unit pawn 은 +1.0).
        //  바를 머리 바로 위로 끌어내리고, 카메라가 ~1.7x 더 줌-인 됐으므로 너비/높이를
        //  ~40% 축소해도 화면 상 동일하게 읽힘 (plan-199 §5).
        //  yOffset = HP(상단) 바, mood 바는 그 아래로 (bottom = yOffset - barHeight - gap).
        //  bottom = 0.68 - 0.10 - 0.03 = 0.55 → 머리(0.5) 바로 위, 몸통과 겹치지 않음.
        [SerializeField] private float yOffset = 0.68f;          // HP 바 (상단) — 머리(0.5) 바로 위
        [SerializeField] private float barWidth = 0.66f;         // 풀 너비 (1.1 → 0.66, ~40% 축소)
        [SerializeField] private float barHeight = 0.10f;        // 한 바 높이 (0.16 → 0.10)
        [SerializeField] private float gap = 0.03f;              // 두 바 사이 간격
        [SerializeField] private int sortingOrder = 30;

        private SpriteRenderer hpBg, hpFill, moodBg, moodFill;
        private PawnEntity entity;
        private PawnNeeds  needs;
        private PawnHealth health;  // Day 45 — body part total ratio

        // 1x1 white sprite reused across all bars (created once)
        private static Sprite _whiteSprite;
        private static Sprite WhiteSprite
        {
            get
            {
                if (_whiteSprite == null)
                {
                    // 2x2 white at PPU 2 → 1 world unit per side.  localScale = ratio.
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                    // #UI-restyle U2 — Point filter (was Bilinear → soft/blurry against
                    //   crisp pixel-art).  Sharp bar edges now match the sprite style.
                    tex.filterMode = FilterMode.Point;
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.Apply();
                    _whiteSprite = Sprite.Create(tex, new Rect(0,0,2,2), new Vector2(0.5f,0.5f), 2f);
                    _whiteSprite.name = "FloatingBarWhite";
                }
                return _whiteSprite;
            }
        }

        private void Awake()
        {
            entity = GetComponent<PawnEntity>();
            needs  = GetComponent<PawnNeeds>();
            health = GetComponent<PawnHealth>();
            BuildBars();
            // diagnostic log dropped post Day 42 verify — bars confirmed visible.
        }

        // #UI-restyle U2 — dark outline frame color (warm near-black, matches
        //   OUTLINE_OBJ).  bg sits a hair wider/taller than the fill so it reads
        //   as a crisp 1px-style frame around each bar instead of a flat backing.
        private static readonly Color FrameColor = new Color(0.106f, 0.082f, 0.063f, 0.95f); // #1B1510
        private float FrameMarginX => barWidth  * 0.06f;
        private float FrameMarginY => barHeight * 0.30f;

        private void BuildBars()
        {
            float top    = yOffset;
            float bottom = yOffset - barHeight - gap;

            // backgrounds = dark outline frame (slightly larger than the fill)
            hpBg   = MakeBar("HpBg",   FrameColor, top,    barWidth + FrameMarginX, barHeight + FrameMarginY);
            hpFill = MakeBar("HpFill", new Color(0.95f, 0.30f, 0.25f, 1.00f), top,    barWidth);
            moodBg   = MakeBar("MoodBg",   FrameColor, bottom, barWidth + FrameMarginX, barHeight + FrameMarginY);
            moodFill = MakeBar("MoodFill", new Color(1.00f, 0.85f, 0.30f, 1.00f), bottom, barWidth);
            // pivot fills to left so scaleX = ratio looks correct
            hpFill.transform.localPosition = new Vector3(-barWidth * 0.5f, top, 0);
            hpFill.transform.localScale    = new Vector3(barWidth, barHeight, 1f);
            hpFill.GetComponent<SpriteRenderer>(); // ensure
            // Fills use a custom 1-unit-wide sprite + adjusted pivot for left-anchored shrink.
            // Simpler: use transform.localScale.x to set ratio (0..1).
            moodFill.transform.localPosition = new Vector3(-barWidth * 0.5f, bottom, 0);
            moodFill.transform.localScale    = new Vector3(barWidth, barHeight, 1f);

            // re-anchor fill pivots so x-scale-from-left looks correct
            ReanchorFillPivot(hpFill);
            ReanchorFillPivot(moodFill);
        }

        private SpriteRenderer MakeBar(string name, Color color, float yLocal, float width, float height = -1f)
        {
            if (height < 0f) height = barHeight;
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, yLocal, 0);
            go.transform.localScale    = new Vector3(width, height, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = WhiteSprite;
            sr.color = color;
            // frames render 1 below the fills so the fill sits ON TOP of the frame.
            sr.sortingOrder = name.EndsWith("Bg") ? sortingOrder : sortingOrder + 1;
            return sr;
        }

        // Fill bars are scaled along X from 0..1 to show ratio.
        // Default sprite pivot center → ratio shrink looks centered.
        // To shrink from left we offset the GameObject by -(1-ratio)*width/2.
        private void ReanchorFillPivot(SpriteRenderer sr)
        {
            // No-op marker — actual logic handled per-frame in UpdateBars.
            // (Kept as a hook for future readability.)
        }

        private void Update()
        {
            if (entity == null && needs == null && health == null) return;
            float hpRatio;
            // Day 45 우선순위: health.TotalHpRatio (body part 합산) > entity.Hp
            if (health != null) hpRatio = Mathf.Clamp01(health.TotalHpRatio);
            else if (entity != null) hpRatio = Mathf.Clamp01(entity.Hp / 30f);
            else hpRatio = 1f;
            float moodRatio = needs != null ? Mathf.Clamp01(needs.mood / 100f) : 1f;
            UpdateFill(hpFill,   hpRatio,   ColorForHp(hpRatio));
            UpdateFill(moodFill, moodRatio, ColorForMood(moodRatio));
        }

        private void UpdateFill(SpriteRenderer fill, float ratio, Color color)
        {
            if (fill == null) return;
            // shrink-from-left: scale.x = barWidth*ratio, position.x offset so left edge stays fixed
            float leftX = -barWidth * 0.5f;
            float w     = barWidth * ratio;
            var t = fill.transform;
            t.localScale    = new Vector3(w, barHeight, 1f);
            // Centered pivot of WhiteSprite: position center at leftX + w/2
            t.localPosition = new Vector3(leftX + w * 0.5f, t.localPosition.y, 0);
            fill.color = color;
        }

        private Color ColorForHp(float r)
        {
            if (r < 0.30f) return new Color(0.55f, 0.10f, 0.10f, 1f);
            if (r < 0.70f) return new Color(0.95f, 0.40f, 0.20f, 1f);
            return new Color(0.95f, 0.30f, 0.25f, 1f);
        }

        private Color ColorForMood(float r)
        {
            if (r < 0.25f) return new Color(0.95f, 0.25f, 0.25f, 1f); // 정신붕괴 임박 — 빨강
            if (r < 0.50f) return new Color(0.95f, 0.55f, 0.20f, 1f); // 주의 — 주황
            return new Color(1.00f, 0.85f, 0.30f, 1f);                 // 안정 — 노랑
        }
    }
}
