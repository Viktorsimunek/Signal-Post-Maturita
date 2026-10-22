Shader "SignalPost/Fog Piercing Billboard Glow"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.78, 0.38, 1)
        _Intensity ("Intensity", Float) = 2
        _Softness ("Softness", Range(0.2, 8)) = 2.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "FogPiercingGlow"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 centeredUv = input.uv * 2.0 - 1.0;
                half radial = saturate(1.0 - length(centeredUv));
                half glow = pow(radial, max(0.001, _Softness));

                return half4(_Color.rgb * (_Color.a * _Intensity * glow), glow * _Color.a);
            }
            ENDHLSL
        }
    }
}
