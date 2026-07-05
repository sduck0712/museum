// Interactive Masking Shader (Built-in RP Surface Shader).
// _DirtMaskTex(ExcavationBrush가 실시간으로 칠하는 RenderTexture)를 알파 마스크로 사용해
// 흙 텍스처와 유물 원본 텍스처를 블렌딩한다.
// _Tint: 복원(재조립) 완료 후 옅은 균열/복원 흔적 표현용 영구 틴트.
//
// [Shader Graph(URP/HDRP)로 동일하게 구성할 경우 노드 셋업]
// 1) Sample Texture 2D (DirtMaskTex, UV0) -> R 채널(Split) 추출
// 2) Smoothstep(0.5 - EdgeSoftness, 0.5 + EdgeSoftness, maskR) -> blend factor
// 3) Sample Texture 2D (ArtifactAlbedo, UV0), Sample Texture 2D (DirtAlbedo, UV0)
// 4) Lerp(A=ArtifactAlbedo, B=DirtAlbedo, T=blend) * Tint -> Base Color 입력
// 5) Sample Texture 2D (DirtNormal, Normal 타입) -> Normal Blend 노드(또는 Lerp)로
//    기본 노멀(0,0,1)과 blend 비율로 섞어 -> Normal 입력
// 6) Lerp(A=0.5, B=0.1, T=blend) -> Smoothness 입력
// 7) Fragment 스테이지의 Master Stack(Lit)에 Base Color / Normal / Smoothness 연결
Shader "VirtualMuseum/ExcavationSurface"
{
    Properties
    {
        _ArtifactAlbedo ("Artifact Albedo", 2D) = "white" {}
        _DirtAlbedo ("Dirt Albedo", 2D) = "white" {}
        _DirtNormal ("Dirt Normal", 2D) = "bump" {}
        _DirtMaskTex ("Dirt Mask (RenderTexture, R=흙있음)", 2D) = "white" {}
        _EdgeSoftness ("Edge Softness", Range(0,0.2)) = 0.02
        _Tint ("Tint (restored crack tint)", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _ArtifactAlbedo;
        sampler2D _DirtAlbedo;
        sampler2D _DirtNormal;
        sampler2D _DirtMaskTex;
        float _EdgeSoftness;
        fixed4 _Tint;

        struct Input
        {
            float2 uv_ArtifactAlbedo;
            float2 uv_DirtAlbedo;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed maskValue = tex2D(_DirtMaskTex, IN.uv_ArtifactAlbedo).r;
            float blend = smoothstep(0.5 - _EdgeSoftness, 0.5 + _EdgeSoftness, maskValue);

            fixed3 artifactColor = tex2D(_ArtifactAlbedo, IN.uv_ArtifactAlbedo).rgb;
            fixed3 dirtColor = tex2D(_DirtAlbedo, IN.uv_DirtAlbedo).rgb;

            o.Albedo = lerp(artifactColor, dirtColor, blend) * _Tint.rgb;
            o.Normal = lerp(fixed3(0,0,1), UnpackNormal(tex2D(_DirtNormal, IN.uv_DirtAlbedo)), blend);
            o.Smoothness = lerp(0.5, 0.1, blend);
            o.Metallic = 0.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
