Shader "Hidden/Custom/URP/Scanlines"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Scanlines"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Strength;
            float _Frequency;
            float _Dispersion;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 resolutionUv = _ScreenParams.xy * uv;
                float3 scanlines = 1. - float3(
                    sin((uv.y) * _Frequency + _Dispersion*1.571),
                    sin(uv.y * _Frequency),
                    sin((uv.y) * _Frequency - _Dispersion*1.571)
                ) * pow(_Strength, 2);
                float4 output = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                return output * float4(scanlines, 1.0);
            }
            ENDHLSL
        }
    }
}
