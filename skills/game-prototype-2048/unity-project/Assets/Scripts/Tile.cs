using UnityEngine;

namespace MelonS.GameProto
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class Tile : MonoBehaviour
    {
        public int Value { get; private set; }
        private SpriteRenderer sr;
        private TextMesh tm;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            // Ensure child TextMesh for the number
            var textGo = new GameObject("Value");
            textGo.transform.SetParent(transform, false);
            textGo.transform.localPosition = new Vector3(0, 0, -0.01f);
            tm = textGo.AddComponent<TextMesh>();
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.characterSize = 0.08f;
            tm.fontSize = 80;
            tm.color = new Color(0.15f, 0.15f, 0.15f);
            // Force renderer over sprite
            var mr = textGo.GetComponent<MeshRenderer>();
            if (mr != null) { mr.sortingOrder = 12; }
            sr.sortingOrder = 10;
        }

        public void SetValue(int v)
        {
            Value = v;
            if (tm != null) tm.text = v.ToString();
            if (sr != null) sr.color = ColorForValue(v);
            if (tm != null) tm.color = v >= 8 ? new Color(0.97f, 0.97f, 0.97f) : new Color(0.15f, 0.15f, 0.15f);
        }

        private static Color ColorForValue(int v)
        {
            switch (v)
            {
                case 2:    return new Color(0.93f, 0.89f, 0.85f);
                case 4:    return new Color(0.93f, 0.87f, 0.78f);
                case 8:    return new Color(0.95f, 0.69f, 0.47f);
                case 16:   return new Color(0.96f, 0.58f, 0.39f);
                case 32:   return new Color(0.96f, 0.49f, 0.37f);
                case 64:   return new Color(0.96f, 0.37f, 0.23f);
                case 128:  return new Color(0.93f, 0.81f, 0.45f);
                case 256:  return new Color(0.93f, 0.80f, 0.38f);
                case 512:  return new Color(0.93f, 0.78f, 0.31f);
                case 1024: return new Color(0.93f, 0.77f, 0.25f);
                case 2048: return new Color(0.93f, 0.76f, 0.18f);
                default:   return new Color(0.24f, 0.22f, 0.20f);
            }
        }
    }
}
