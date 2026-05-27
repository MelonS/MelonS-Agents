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
    }
}
