// 적 테두리 (수학 모드 가시성)
//  1) Outline: 뒷면을 법선 방향으로 화면상 _OutlineWidth 픽셀만큼 부풀려 그린다 → 거리와 상관없이 같은 두께의 테두리
//  2) XRay   : 벽 뒤에 가려진 부분만 반투명하게 그린다 → 벽 너머의 적도 실루엣으로 보인다
Shader "MathMode/Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.2, 0.2, 1)
        _OutlineWidth ("Outline Width (px)", Float) = 3
        _XRayColor ("X-Ray Color", Color) = (1, 0.2, 0.2, 0.3)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _OutlineColor;
            float _OutlineWidth;
            half4 _XRayColor;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
        };
        ENDHLSL

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            Varyings vert(Attributes input)
            {
                Varyings output;
                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float2 normalCS = mul((float3x3)GetWorldToHClipMatrix(), normalWS).xy;
                float2 dir = normalize(normalCS + float2(1e-5, 1e-5));
                positionCS.xy += dir * (_OutlineWidth * 2.0 / _ScreenParams.xy) * positionCS.w;
                output.positionCS = positionCS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        Pass
        {
            Name "XRay"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite Off
            ZTest Greater
            Offset -1, -1
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _XRayColor;
            }
            ENDHLSL
        }
    }
}
