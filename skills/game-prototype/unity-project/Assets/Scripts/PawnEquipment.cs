using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// #123 - 레퍼런스 콜로니심 의류/무기 equip (단순화).
    ///  Per-pawn 슬롯: 셔츠/바지/모자/무기. 각 item 이 stat modifier 보유.
    ///  시각화 1차 skip (32x32 sprite 위에 layered render 추후), stats only.
    ///
    /// Awake 시 random starter 의류 + 무기.
    /// PawnInfoPanel "장비:" 섹션 표시.
    /// </summary>
    public class PawnEquipment : MonoBehaviour
    {
        public enum Slot { Shirt, Pants, Hat, Weapon }

        [System.Serializable]
        public class Item
        {
            public string nameKr;
            public Slot slot;
            public float moodBonus;     // 매 frame thoughts 가 아니라 직접 PawnNeeds.mood + 효과
            public float melArmor;      // 0.0~1.0 (melee 데미지 감소율)
            public float meleeDamageAdd;
            public bool rangedEnabled;  // true 면 활/총 = arrow shoot 가능
        }

        // 카탈로그 - #133 확장 (10→17 items)
        public static readonly Item[] Catalog = new[] {
            // 셔츠 (4종)
            new Item { nameKr = "기본 셔츠",     slot = Slot.Shirt,  moodBonus = 2f, melArmor = 0.10f },
            new Item { nameKr = "가죽 외투",     slot = Slot.Shirt,  moodBonus = 4f, melArmor = 0.20f },
            new Item { nameKr = "양털 외투",     slot = Slot.Shirt,  moodBonus = 5f, melArmor = 0.15f },
            new Item { nameKr = "체인메일",      slot = Slot.Shirt,  moodBonus = 1f, melArmor = 0.45f },
            // 바지 (3종)
            new Item { nameKr = "기본 바지",     slot = Slot.Pants,  moodBonus = 1f, melArmor = 0.05f },
            new Item { nameKr = "가죽 바지",     slot = Slot.Pants,  moodBonus = 2f, melArmor = 0.12f },
            new Item { nameKr = "철판 바지",     slot = Slot.Pants,  moodBonus = 0f, melArmor = 0.25f },
            // 모자 (3종)
            new Item { nameKr = "천 모자",       slot = Slot.Hat,    moodBonus = 1f, melArmor = 0.10f },
            new Item { nameKr = "철 헬멧",       slot = Slot.Hat,    moodBonus = 0f, melArmor = 0.30f },
            new Item { nameKr = "후드",          slot = Slot.Hat,    moodBonus = 3f, melArmor = 0.08f },
            // 무기 (7종) - 근접 5 + 원거리 2
            new Item { nameKr = "주먹 (맨손)",   slot = Slot.Weapon, moodBonus = 0f, meleeDamageAdd = 0f },
            new Item { nameKr = "곤봉",          slot = Slot.Weapon, moodBonus = 0f, meleeDamageAdd = 1f },
            new Item { nameKr = "단검",          slot = Slot.Weapon, moodBonus = 0f, meleeDamageAdd = 1f },
            new Item { nameKr = "도끼",          slot = Slot.Weapon, moodBonus = 0f, meleeDamageAdd = 2f },
            new Item { nameKr = "롱소드",        slot = Slot.Weapon, moodBonus = 1f, meleeDamageAdd = 3f },
            new Item { nameKr = "활",            slot = Slot.Weapon, moodBonus = 0f, rangedEnabled = true },
            new Item { nameKr = "석궁",          slot = Slot.Weapon, moodBonus = 0f, rangedEnabled = true, meleeDamageAdd = 1f },
        };

        public Dictionary<Slot, Item> equipped = new Dictionary<Slot, Item>();

        public Item GetEquipped(Slot s) => equipped.TryGetValue(s, out var it) ? it : null;

        public void Equip(Item item)
        {
            if (item == null) return;
            equipped[item.slot] = item;
        }

        public void Unequip(Slot s)
        {
            if (equipped.ContainsKey(s)) equipped.Remove(s);
        }

        public float TotalMoodBonus()
        {
            float sum = 0f;
            foreach (var kv in equipped) sum += kv.Value.moodBonus;
            return sum;
        }

        public float TotalMelArmor()
        {
            float sum = 0f;
            foreach (var kv in equipped) sum += kv.Value.melArmor;
            return Mathf.Min(0.85f, sum);  // 85% 상한
        }

        public float TotalMeleeDamageBonus()
        {
            float sum = 0f;
            foreach (var kv in equipped) sum += kv.Value.meleeDamageAdd;
            return sum;
        }

        public bool HasRangedWeapon()
        {
            return equipped.TryGetValue(Slot.Weapon, out var w) && w.rangedEnabled;
        }

        private void Awake()
        {
            // 시작 의류 - 셔츠 + 바지는 모두 가짐, 모자/무기는 50%.
            var shirts = System.Array.FindAll(Catalog, c => c.slot == Slot.Shirt);
            var pants  = System.Array.FindAll(Catalog, c => c.slot == Slot.Pants);
            var hats   = System.Array.FindAll(Catalog, c => c.slot == Slot.Hat);
            var weps   = System.Array.FindAll(Catalog, c => c.slot == Slot.Weapon);
            Equip(shirts[Random.Range(0, shirts.Length)]);
            Equip(pants[Random.Range(0, pants.Length)]);
            if (Random.value < 0.5f) Equip(hats[Random.Range(0, hats.Length)]);
            if (Random.value < 0.7f) Equip(weps[Random.Range(0, weps.Length)]);

            // mood thought - 옷 입은 보너스
            var th = GetComponent<PawnThoughts>();
            if (th != null)
            {
                float bonus = TotalMoodBonus();
                if (bonus > 0f) th.AddThought("좋은 옷차림", bonus, 600f);
            }
        }
    }
}
