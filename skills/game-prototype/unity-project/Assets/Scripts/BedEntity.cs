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

        public void SetQuality(BedQuality q)
        {
            quality = q;
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = QualityStats[(int)q].tint;
        }
    }
}
