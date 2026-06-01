using UnityEngine;

namespace MelonS.GameProto.Data
{
    /// <summary>
    /// R3 - PawnHealth 의 6 body parts 데이터 외부화.
    /// 이전: PawnHealth.Awake 에 하드코딩 (머리 20/몸통 40/팔 18/다리 20, #200 fidelity)
    /// 이후: 이 SO array 에서 읽음 - 향후 종족별 또는 레퍼런스 콜로니심 처럼 캐릭터별
    ///       바디 파트 다양화 가능 (예: 거대-맘무스 = 다리 4, 더 큰 몸통).
    ///
    /// PartId enum 은 PawnHealth.cs 정의된 것 그대로 참조.
    /// </summary>
    [CreateAssetMenu(fileName = "HealthPartsConfig", menuName = "MelonS/HealthPartsConfig")]
    public class HealthPartsConfig : ScriptableObject
    {
        [System.Serializable]
        public class PartDef
        {
            public PawnHealth.PartId id;
            public string nameKr;
            public int maxHp;
            public bool isVital;  // 0 이 되면 사망
        }

        public PartDef[] parts = new PartDef[]
        {
            // #200 genre fidelity: head 10→20, torso 30→40 (torso 40 is wiki-
            //  confirmed; head 20 is a conservative best-estimate — the reference sim head ~25,
            //  flagged for operator review).  Was head 10 = decapitation far too easy.
            new PartDef { id = PawnHealth.PartId.Head,     nameKr = "머리",     maxHp = 20, isVital = true  },
            new PartDef { id = PawnHealth.PartId.Torso,    nameKr = "몸통",     maxHp = 40, isVital = true  },
            new PartDef { id = PawnHealth.PartId.LeftArm,  nameKr = "왼팔",     maxHp = 18, isVital = false },
            new PartDef { id = PawnHealth.PartId.RightArm, nameKr = "오른팔",   maxHp = 18, isVital = false },
            new PartDef { id = PawnHealth.PartId.LeftLeg,  nameKr = "왼다리",   maxHp = 20, isVital = false },
            new PartDef { id = PawnHealth.PartId.RightLeg, nameKr = "오른다리", maxHp = 20, isVital = false },
        };

        [Header("Damage weights (sum should = 100)")]
        public int weightHead = 5;
        public int weightTorso = 35;
        public int weightLeftLeg = 20;
        public int weightRightLeg = 20;
        public int weightLeftArm = 10;
        public int weightRightArm = 10;

        public static HealthPartsConfig CreateDefault()
        {
            var c = ScriptableObject.CreateInstance<HealthPartsConfig>();
            c.name = "HealthPartsConfig(default)";
            return c;
        }
    }
}
