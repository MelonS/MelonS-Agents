// OceanGerstner — 3파 합성 sum-of-sines 수면 셰이더.
// Y축 변위만 준다 (X/Z 이동 없음) — OpenMMO(doc/WATER_SYSTEM.md) 가 타일 이음새를
// 안 깨뜨리려고 택한 설계를 그대로 채택. OceanWaveSampler.cs 의 SampleHeight() 와
// 반드시 같은 공식이어야 한다 — 다르면 배가 화면상 파도와 다른 높이에 뜬다.
Shader "MelonS/Naval/OceanGerstner"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.20, 0.55, 0.55, 1)
        _DeepColor    ("Deep Color", Color)    = (0.02, 0.12, 0.22, 1)
        _WaveLength1 ("Wave 1 Length (m)", Float) = 20
        _WaveLength2 ("Wave 2 Length (m)", Float) = 14
        _WaveLength3 ("Wave 3 Length (m)", Float) = 9
        _Amplitude1 ("Wave 1 Amplitude (m)", Float) = 0.45
        _Amplitude2 ("Wave 2 Amplitude (m)", Float) = 0.28
        _Amplitude3 ("Wave 3 Amplitude (m)", Float) = 0.15
        _Dir1 ("Wave 1 Dir (deg)", Float) = 0
        _Dir2 ("Wave 2 Dir (deg)", Float) = 55
        _Dir3 ("Wave 3 Dir (deg)", Float) = -35
        _Glossiness ("Smoothness", Range(0,1)) = 0.7
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows addshadow
        #pragma target 3.0

        struct Input
        {
            float3 worldPos;
        };

        fixed4 _ShallowColor;
        fixed4 _DeepColor;
        float _WaveLength1, _WaveLength2, _WaveLength3;
        float _Amplitude1, _Amplitude2, _Amplitude3;
        float _Dir1, _Dir2, _Dir3;
        half _Glossiness;

        float GerstnerHeight(float2 pos, float t)
        {
            float h = 0;
            float lens[3]  = { _WaveLength1, _WaveLength2, _WaveLength3 };
            float amps[3]  = { _Amplitude1, _Amplitude2, _Amplitude3 };
            float dirs[3]  = { _Dir1, _Dir2, _Dir3 };
            [unroll]
            for (int i = 0; i < 3; i++)
            {
                float k = 6.2831853 / max(lens[i], 0.01);
                float rad = radians(dirs[i]);
                float2 d = float2(cos(rad), sin(rad));
                float speed = sqrt(9.8 / k); // deep-water dispersion, 그래픽 관행값
                h += amps[i] * sin(k * dot(d, pos) + t * speed * k);
            }
            return h;
        }

        void vert(inout appdata_full v)
        {
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float h = GerstnerHeight(wp.xz, _Time.y);
            v.vertex.y += h;

            // 근사 노멀 — 인접 유한차분 (해석적 미분 대신 프로토타입 수준 근사)
            float eps = 0.5;
            float hX = GerstnerHeight(wp.xz + float2(eps, 0), _Time.y);
            float hZ = GerstnerHeight(wp.xz + float2(0, eps), _Time.y);
            float3 tangentX = float3(eps, hX - h, 0);
            float3 tangentZ = float3(0, hZ - h, eps);
            v.normal = normalize(cross(tangentZ, tangentX));
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float depthBlend = saturate(IN.worldPos.y * 0.5 + 0.5);
            o.Albedo = lerp(_DeepColor.rgb, _ShallowColor.rgb, depthBlend);
            o.Smoothness = _Glossiness;
            o.Metallic = 0.05;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
