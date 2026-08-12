// SpriteShadowShear — 2D 탑다운 그림자용 전단(shear) 셰이더.
//
// 왜 전단인가 (2026-07-31 리서치):
//   운영자가 세 번 지적했다 — "축이 안 맞는다", "옆으로 평행이동된 느낌",
//   "나무 기준으로 봐도 아직 축이 완벽하지 않다".  회전(Transform.rotation)으로
//   그림자를 눕히고 있었기 때문이다.
//
//   표준 기법은 **전단**이다.  2D 탑다운 그림자는
//     · 밑변(BL/BR)은 **고정** — 발/밑동에 붙어 있어야 한다
//     · 윗변(TL/TR)만 광원 반대쪽으로 밀어낸다
//   회전은 밑변까지 돌려 버려 물체가 '쓰러진' 모양이 되고, 밑변이 접지점에서
//   떨어져 축이 어긋나 보인다.  전단은 밑을 붙인 채 위만 늘리므로 그림자답다.
//
// 구현: 버텍스 셰이더에서 y(밑변=0, 윗변=1)에 비례해 x/y 를 민다.
//   _ShearX : 윗변을 가로로 미는 양 (그림자 방향의 x 성분 x 길이)
//   _ShearY : 윗변을 세로로 미는 양 (탑다운 투영 보정 후 y 성분)
//   _PivotY : 스프라이트 로컬 y 중 **밑변**의 값 (보통 -extents.y).  이 선이 고정축.
// 색은 _Color 로 통째 덮어쓴다(실루엣).  알파는 텍스처 알파 x _Color.a.
Shader "MelonS/SpriteShadowShear"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _Color   ("Tint", Color) = (0,0,0,0.35)
        _ShearX  ("Shear X", Float) = 0
        _ShearY  ("Shear Y", Float) = 0
        _PivotY  ("Pivot Y (base line)", Float) = -0.5
        _Height  ("Sprite Height", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent"
               "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _ShearX, _ShearY, _PivotY, _Height;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                // t = 밑변 0 → 윗변 1.  밑변은 어떤 값을 넣어도 움직이지 않는다.
                float t = saturate((v.vertex.y - _PivotY) / max(0.0001, _Height));
                v.vertex.x += _ShearX * t;
                v.vertex.y += _ShearY * t;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                // 실루엣 — 원본 색은 버리고 알파만 쓴다.
                return fixed4(_Color.rgb, c.a * _Color.a);
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
