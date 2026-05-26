using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>Floating +N text that rises briefly then fades.</summary>
    public class ScorePopup : MonoBehaviour
    {
        public float lifetime = 0.8f;
        public float rise = 1.2f;
        private float age = 0f;
        private TextMesh tm;
        private SpriteRenderer sr;

        public void Setup(int score, Vector3 world)
        {
            transform.position = world;
            tm = GetComponent<TextMesh>();
            if (tm == null) tm = gameObject.AddComponent<TextMesh>();
            tm.text = "+" + score;
            tm.color = new Color(1f, 0.9f, 0.4f);
            tm.characterSize = 0.1f;
            tm.fontSize = 64;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
        }

        private void Update()
        {
            age += Time.deltaTime;
            transform.position += Vector3.up * (rise * Time.deltaTime);
            if (tm != null)
            {
                Color c = tm.color;
                c.a = Mathf.Clamp01(1f - age / lifetime);
                tm.color = c;
            }
            if (age >= lifetime) Destroy(gameObject);
        }
    }
}
