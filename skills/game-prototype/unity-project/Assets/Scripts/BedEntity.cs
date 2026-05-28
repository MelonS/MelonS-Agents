using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>#151 - 침대 quality (wiki: sleeping spot 0.8 / wood bed 1.0 / fine 1.4).</summary>
    public enum BedQuality { SleepingSpot, Wood, Fine }

    /// <summary>
    /// 운영자 fb - 침대 (#107) + quality 시스템 (#151).
    /// 림월드: quality 별 rest multiplier 다름.
    ///   SleepingSpot 0.8x (자재 0)
    ///   Wood        1.0x (목재 8) - default
    ///   Fine        1.4x (목재 30 + 건축 skill 5+)
    /// PawnNeeds.IsOnBed() 가 OverlapBox 검사 후 RestMul() 사용.
    /// </summary>
    public class BedEntity : MonoBehaviour
    {
        [SerializeField] private BedQuality quality = BedQuality.Wood;

        public BedQuality Quality => quality;
        public string QualityKr => quality switch
        {
            BedQuality.SleepingSpot => "수면 자리",
            BedQuality.Wood => "목재 침대",
            BedQuality.Fine => "고급 침대",
            _ => "침대",
        };

        public static readonly (float restMul, float moodBonus, Color tint)[] QualityStats = {
            (0.80f, 0f, new Color(0.65f, 0.55f, 0.45f, 1f)),
            (1.00f, 3f, new Color(0.95f, 0.95f, 0.95f, 1f)),
            (1.40f, 8f, new Color(1.00f, 0.95f, 0.70f, 1f)),
        };

        public float RestMul => QualityStats[(int)quality].restMul;
        public float MoodBonus => QualityStats[(int)quality].moodBonus;

        // #193 - RimWorld vanilla 침대 크기.  wiki:
        //   Sleeping spot  1x1  (자재 0)
        //   Wood bed       1x2  (목재 45 wiki / 우리 8 단순화)
        //   Fine bed       1x2  (목재 + quality)
        //   Royal bed      2x2  (현재는 Fine 으로 단순화)
        public static Vector2Int SizeFor(BedQuality q) => q switch
        {
            BedQuality.SleepingSpot => new Vector2Int(1, 1),
            BedQuality.Wood         => new Vector2Int(1, 2),
            BedQuality.Fine         => new Vector2Int(1, 2),
            _ => new Vector2Int(1, 1),
        };

        public Vector2Int Size => SizeFor(quality);

        public void SetQuality(BedQuality q)
        {
            quality = q;
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = QualityStats[(int)q].tint;
            // #193 - 1x2 침대 → sprite 가 16x32 가 아니어도 transform.localScale 로 강제 (PPU 자동 detect 안 될 시 fallback)
            //  bed_wood.png 가 16x32 면 PPU 16 자동 = world 1x2.  16x16 PNG 면 scale (1,2) 보정.
            ApplyVisualSize();
        }

        private void ApplyVisualSize()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return;
            // sprite.bounds.size = world unit (PPU 반영).  target = Size (Vector2Int).
            //  ratio 로 transform.localScale 보정 → 결과 world bound = target.
            Vector2 worldSize = sr.sprite.bounds.size;
            if (worldSize.x < 0.01f || worldSize.y < 0.01f) return;
            transform.localScale = new Vector3(Size.x / worldSize.x, Size.y / worldSize.y, 1f);
        }

        private void Start()
        {
            ApplyVisualSize();
        }
    }
}
