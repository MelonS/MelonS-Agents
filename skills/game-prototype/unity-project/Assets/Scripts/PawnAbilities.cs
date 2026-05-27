using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// #120 - 림월드 stats 시스템 (per-pawn 능력치).
    ///  RimWorld vanilla 의 Move Speed, Work Speed (Construction/Mining/Cooking/Plants),
    ///  Manipulation, Combat (Melee/Shooting), Carrying Capacity 단순화 버전.
    ///
    /// Awake 에 0.85~1.15 사이 랜덤 분포로 초기화 (Trait 가 추가 보정).
    /// PawnMovement, PawnChopper, PawnMiner, PawnBuilder, PawnCook 등이 polling 으로 multiplier 적용.
    /// PawnInfoPanel 에 표시.
    /// </summary>
    public class PawnAbilities : MonoBehaviour
    {
        // 1.0 = 평균, 위/아래로 분포.  Trait 가 *= 0.8 ~ *= 1.2 추가 보정.
        public float moveSpeedMul = 1f;       // 이동
        public float constructionMul = 1f;    // 건설
        public float miningMul = 1f;          // 채광
        public float chopMul = 1f;            // 벌목
        public float plantsMul = 1f;          // 농경 (씨/수확)
        public float cookingMul = 1f;         // 요리
        public float manipulation = 1f;       // 손 사용 (모든 작업 영향)
        public float meleeMul = 1f;           // 근접 데미지
        public float shootingAccuracy = 1f;   // 사격 정확도
        public float carryCapacity = 75f;     // 운반 용량 (kg)
        public float socialMul = 1f;          // 사회성 (거래 가격 등)

        // 한글 이름 (PawnInfoPanel 표시용)
        public static readonly (string key, string label)[] DisplayMap = new[] {
            ("moveSpeedMul",    "이동"),
            ("manipulation",    "조작"),
            ("chopMul",         "벌목"),
            ("miningMul",       "채광"),
            ("constructionMul", "건설"),
            ("plantsMul",       "농경"),
            ("cookingMul",      "요리"),
            ("meleeMul",        "근접"),
            ("shootingAccuracy","사격"),
            ("socialMul",       "사회성"),
        };

        private void Awake()
        {
            // 림 vanilla 같이 약간의 분포 (0.85~1.15).  너무 극단적이지 않게.
            moveSpeedMul     = Random.Range(0.85f, 1.15f);
            constructionMul  = Random.Range(0.85f, 1.15f);
            miningMul        = Random.Range(0.85f, 1.15f);
            chopMul          = Random.Range(0.85f, 1.15f);
            plantsMul        = Random.Range(0.85f, 1.15f);
            cookingMul       = Random.Range(0.85f, 1.15f);
            manipulation     = Random.Range(0.90f, 1.10f);  // manipulation 좁게 (림처럼)
            meleeMul         = Random.Range(0.80f, 1.20f);
            shootingAccuracy = Random.Range(0.80f, 1.20f);
            carryCapacity    = Random.Range(60f, 90f);
            socialMul        = Random.Range(0.80f, 1.20f);
        }

        /// <summary>특정 work 의 effective multiplier (manipulation 곱).</summary>
        public float EffectiveWorkMul(WorkKind kind)
        {
            float baseMul = kind switch
            {
                WorkKind.Chop     => chopMul,
                WorkKind.Gather   => plantsMul,
                WorkKind.Hunt     => shootingAccuracy * 0.5f + meleeMul * 0.5f,
                WorkKind.Cook     => cookingMul,
                WorkKind.Research => manipulation,
                _ => 1f,
            };
            return baseMul * manipulation;
        }

        public float GetByKey(string key) => key switch
        {
            "moveSpeedMul"     => moveSpeedMul,
            "constructionMul"  => constructionMul,
            "miningMul"        => miningMul,
            "chopMul"          => chopMul,
            "plantsMul"        => plantsMul,
            "cookingMul"       => cookingMul,
            "manipulation"     => manipulation,
            "meleeMul"         => meleeMul,
            "shootingAccuracy" => shootingAccuracy,
            "carryCapacity"    => carryCapacity,
            "socialMul"        => socialMul,
            _ => 0f,
        };
    }
}
