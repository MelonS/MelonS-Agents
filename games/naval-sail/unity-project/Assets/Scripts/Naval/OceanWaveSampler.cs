using UnityEngine;

namespace MelonS.GameProto.Naval
{
    /// <summary>
    /// CPU 쪽 파도 높이 샘플링. Assets/Shaders/OceanGerstner.shader 의 GerstnerHeight()
    /// 와 반드시 같은 공식이어야 한다 — 값이 갈라지면 배가 화면상 파도와 다른
    /// 높이에서 떠다닌다. 드리프트를 막기 위해 파라미터는 하드코딩하지 않고
    /// 바다 Renderer 의 Material 에서 직접 읽는다.
    /// </summary>
    public class OceanWaveSampler : MonoBehaviour
    {
        public static OceanWaveSampler Instance { get; private set; }

        [SerializeField] private Renderer oceanRenderer;

        private readonly float[] lengths = new float[3];
        private readonly float[] amplitudes = new float[3];
        private readonly float[] dirsDeg = new float[3];

        private void Awake()
        {
            Instance = this;
            if (oceanRenderer == null) oceanRenderer = GetComponent<Renderer>();
            Material m = oceanRenderer.sharedMaterial;
            lengths[0] = m.GetFloat("_WaveLength1");
            lengths[1] = m.GetFloat("_WaveLength2");
            lengths[2] = m.GetFloat("_WaveLength3");
            amplitudes[0] = m.GetFloat("_Amplitude1");
            amplitudes[1] = m.GetFloat("_Amplitude2");
            amplitudes[2] = m.GetFloat("_Amplitude3");
            dirsDeg[0] = m.GetFloat("_Dir1");
            dirsDeg[1] = m.GetFloat("_Dir2");
            dirsDeg[2] = m.GetFloat("_Dir3");
        }

        /// <summary>worldXZ 위치·시각(t)의 파도 높이(Y). 셰이더의 vert() 와 동일 공식.</summary>
        public float SampleHeight(Vector2 worldXZ, float time)
        {
            float h = 0f;
            for (int i = 0; i < 3; i++)
            {
                float k = 6.2831853f / Mathf.Max(lengths[i], 0.01f);
                float rad = dirsDeg[i] * Mathf.Deg2Rad;
                Vector2 d = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                float speed = Mathf.Sqrt(9.8f / k);
                h += amplitudes[i] * Mathf.Sin(k * Vector2.Dot(d, worldXZ) + time * speed * k);
            }
            return h;
        }
    }
}
