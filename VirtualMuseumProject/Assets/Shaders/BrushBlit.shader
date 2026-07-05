// 발굴 브러시가 RenderTexture 마스크에 원형 브러시 자국을 "깎아내는" Blit 셰이더.
// ExcavationBrush.cs가 매 페인트 호출마다 이 셰이더로 이전 프레임 마스크를 읽어
// 새 마스크에 브러시 자국을 반영한 뒤 원본에 다시 Blit한다.
Shader "Hidden/VirtualMuseum/BrushBlit"
{
    Properties
    {
        _MainTex ("Mask (prev frame)", 2D) = "white" {}
        _BrushShape ("Brush Shape (soft circle, alpha)", 2D) = "white" {}
        _BrushUV ("Brush UV", Vector) = (0,0,0,0)
        _BrushRadius ("Brush Radius (UV space)", Float) = 0.03
        _Strength ("Strength", Float) = 0.3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _BrushShape;
            float4 _BrushUV;
            float _BrushRadius;
            float _Strength;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 prevMask = tex2D(_MainTex, i.uv);

                float2 delta = i.uv - _BrushUV.xy;
                float dist = length(delta);

                if (dist > _BrushRadius)
                    return prevMask; // 브러시 반경 밖 -> 기존 값 유지

                float2 brushSampleUV = (delta / _BrushRadius) * 0.5 + 0.5;
                fixed brushAlpha = tex2D(_BrushShape, brushSampleUV).a;

                // 흙(흰색=1)에서 Strength만큼 깎아낸다. 0 이하로는 내려가지 않음.
                fixed newValue = saturate(prevMask.r - brushAlpha * _Strength);
                return fixed4(newValue, newValue, newValue, 1);
            }
            ENDCG
        }
    }
}
