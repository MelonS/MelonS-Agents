using System;
using UnityEngine;

namespace MelonS.GameProto
{
    public class XPManager : MonoBehaviour
    {
        public static XPManager Instance { get; private set; }
        public int XP { get; private set; }
        public int Kills { get; private set; }
        public event Action<int> OnXPChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void AddXP(int n)
        {
            XP += n;
            OnXPChanged?.Invoke(XP);
        }

        public void IncrementKills() { Kills++; }
    }
}
