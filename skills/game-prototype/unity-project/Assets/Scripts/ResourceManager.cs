using System;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Global resource state.  Day 3 = wood only.  Day 4+ adds food.
    /// Singleton pattern via static instance, bound on Awake.
    /// </summary>
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance { get; private set; }

        public int wood = 0;
        public int food = 0;

        public event Action OnChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
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
    }
}
