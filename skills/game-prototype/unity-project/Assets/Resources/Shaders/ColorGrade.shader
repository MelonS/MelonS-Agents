// 룩앤필 배치1 (2026-07-24 리서치 톱1): 전역 컬러 그레이딩 — 빌트인 파이프라인
// OnRenderImage Blit 용 풀스크린 셰이더.  "탁한 머드 톤"의 근본 원인이 전역 컬러
// 레이어 부재였으므로, 채도/대비/틴트를 시간대별로 걸어 화면 전체를 한 번에 조율.
// WebGL(GLES3) 호환 — 텍스처 1장 + 산술만, LUT 텍스처 샘플 없음.
Shader "MelonS/ColorGrade"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Saturation ("Saturation", Float) = 1.0
        _Contrast ("Contrast", Float) = 1.0
        _Tint ("Tint", Color) = (1,1,1,1)
        _Lift ("Lift", Color) = (0,0,0,0)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Saturation;
            float _Contrast;
            fixed4 _Tint;
            fixed4 _Lift;

            fixed4 frag (v2f_img i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                // 채도 (Rec.709 luma 기준)
                fixed luma = dot(c.rgb, fixed3(0.2126, 0.7152, 0.0722));
                c.rgb = lerp(fixed3(luma, luma, luma), c.rgb, _Saturation);
                // 대비 (0.5 피벗)
                c.rgb = (c.rgb - 0.5) * _Contrast + 0.5;
                // 틴트(곱) + 리프트(섀도 들어올림/내림)
                c.rgb = c.rgb * _Tint.rgb + _Lift.rgb;
                return saturate(c);
            }
            ENDCG
        }
    }
    Fallback Off
}
