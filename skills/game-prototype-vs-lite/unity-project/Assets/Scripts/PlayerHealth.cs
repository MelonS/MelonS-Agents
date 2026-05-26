using System;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>Player HP + iframes (invulnerability after hit).</summary>
    public class PlayerHealth : MonoBehaviour
    {
        public static PlayerHealth Instance { get; private set; }

        [SerializeField] private int maxHp = 100;
        [SerializeField] private float iframeSeconds = 0.6f;

        public int Hp { get; private set; }
        public bool IsDead => Hp <= 0;
        public float lastHitTime = -10f;
        public bool InIframe => Time.time - lastHitTime < iframeSeconds;
        public event Action<int> OnHpChanged;

        private SpriteRenderer sr;
        private Color baseColor;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Hp = maxHp;
            sr = GetComponent<SpriteRenderer>();
            if (sr != null) baseColor = sr.color;
        }

        private void Update()
        {
            if (sr != null)
                sr.color = InIframe ? new Color(1f, 0.5f, 0.5f, 0.7f) : baseColor;
        }

        public void TakeDamage(int d)
        {
            if (IsDead || InIframe) return;
            Hp = Mathf.Max(0, Hp - d);
            lastHitTime = Time.time;
            OnHpChanged?.Invoke(Hp);
            if (Hp <= 0 && AudioBank.Instance != null) AudioBank.Instance.PlayHit();
        }
    }
}
