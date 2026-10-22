Shader "Hidden/Custom/URP/CcdOverflow"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "CcdOverflow"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Strength;
            float _Threshold;
            float _Gamma;
            float _PurpleTint;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 resolutionUv = _ScreenParams.xy * uv;
                int iterations = 64;
                float luma = 0;
                for (int i = 0; i < iterations; i++) {
                    float4 current = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, float2(uv.x, (float)i / (iterations-1)));
                    float brightness = current.r * 0.299 + current.g * 0.587 + current.b * 0.114;
                    if (brightness > _Threshold)
                        luma += brightness - _Threshold;
                }
                float4 output = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                luma = pow(luma / (float)iterations * 4., _Gamma);
                output.rgb += clamp(luma * pow(_Strength * 2, 2) * (1. - float3(0.0, 1.0, 0.0) * _PurpleTint), 0.0, 1.2);
                return output;
            }
            ENDHLSL
        }
    }
}
