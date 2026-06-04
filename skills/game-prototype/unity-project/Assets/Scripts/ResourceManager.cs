using System;
using UnityEngine;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// Global resource state.  Day 3 = wood only.  Day 4+ adds food.
    /// R6: Instance property routes to Services.Get (caller compat).
    /// </summary>
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance => Services.Get<ResourceManager>();

        public int wood = 0;
        public int food = 0;
        // Day 27: cooked meals (better than raw food)
        public int meals = 0;
        // #119 - 석재 (벽 짓기/연구대 업그레이드 용도)
        public int stone = 0;
        // #131 - 고급 식사 (요리 skill 5+ pawn 이 만든 fine meal).  mood +20.
        public int fineMeals = 0;

        public event Action OnChanged;

        private void Awake()
        {
            // R6: ServiceLocator register
            if (Services.Has<ResourceManager>() && Services.Get<ResourceManager>() != this)
            { Destroy(gameObject); return; }
            Services.Register<ResourceManager>(this);
        }

        public void AddWood(int amount)
        {
            wood = Mathf.Max(0, wood + amount);
            OnChanged?.Invoke();
        }

        public void AddFood(int amount)
        {
            food = Mathf.Max(0, food + amount);
            OnChanged?.Invoke();
        }

        public void AddMeals(int amount)
        {
            meals = Mathf.Max(0, meals + amount);
            OnChanged?.Invoke();
        }

        public void AddStone(int amount)
        {
            stone = Mathf.Max(0, stone + amount);
            OnChanged?.Invoke();
        }

        public void AddFineMeals(int amount)
        {
            fineMeals = Mathf.Max(0, fineMeals + amount);
            OnChanged?.Invoke();
        }

        // #자원모델 단일화(2026-06-04): stockpile 보관 raw 식량 소비 — 카운터 −amount + 물리
        //  InStockpile MeatPile 을 같은 양만큼 decrement/destroy (cook 의 재료 소비, 저장고
        //  직접 섭취).  이전엔 카운터만 줄이고 물리 더미가 그대로 남아 '카운터 0인데 화면엔
        //  식량 더미'(#2) desync 였다.  불변식 '카운터 = Σ InStockpile MeatPile 식량' 유지.
        //  MeatPileEntity.OnDestroy 는 카운터를 안 건드리므로 여기서만 1회 차감(이중차감 없음).
        public void SpendStockpiledFood(int amount)
        {
            if (amount <= 0) return;
            AddFood(-amount);
            int remain = amount;
            foreach (var m in UnityEngine.Object.FindObjectsByType<MeatPileEntity>(FindObjectsSortMode.None))
            {
                if (remain <= 0) break;
                if (m == null || !m.InStockpile) continue;
                int take = Mathf.Min(remain, m.Food);
                remain -= take;
                int left = m.Food - take;
                if (left <= 0) UnityEngine.Object.Destroy(m.gameObject);
                else m.SetFood(left);
            }
        }

        // #자원모델 단일화(2026-06-04): stockpile 보관 목재/석재 소비 — 카운터 −amount + 물리
        //  InStockpile 더미 decrement/destroy (trader 판매 등).  food 와 동일 패턴.
        public void SpendStockpiledWood(int amount)
        {
            if (amount <= 0) return;
            AddWood(-amount);
            int remain = amount;
            foreach (var p in UnityEngine.Object.FindObjectsByType<WoodPileEntity>(FindObjectsSortMode.None))
            {
                if (remain <= 0) break;
                if (p == null || !p.InStockpile) continue;
                int take = Mathf.Min(remain, p.Wood);
                remain -= take;
                int left = p.Wood - take;
                if (left <= 0) UnityEngine.Object.Destroy(p.gameObject);
                else p.SetWood(left);
            }
        }

        public void SpendStockpiledStone(int amount)
        {
            if (amount <= 0) return;
            AddStone(-amount);
            int remain = amount;
            foreach (var c in UnityEngine.Object.FindObjectsByType<StoneChunkEntity>(FindObjectsSortMode.None))
            {
                if (remain <= 0) break;
                if (c == null || !c.InStockpile) continue;
                int take = Mathf.Min(remain, c.Stone);
                remain -= take;
                int left = c.Stone - take;
                if (left <= 0) UnityEngine.Object.Destroy(c.gameObject);
                else c.SetStone(left);
            }
        }
    }
}
