Shader "Hidden/Custom/URP/VHSGhosting"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "VHSGhosting"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Strength;
            float _XOffset;
            float _Radius;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float3 ghosting = 0.;
                for (int i = -_Radius; i <= _Radius; i++)
                {
                    ghosting += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-_XOffset + i/_ScreenParams.x*4., 0)).rgb;
                }
                ghosting /= _Radius * 2 + 1; // because for loop is -_Rad to +_Rad
                float4 output = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                output.rgb += ghosting * _Strength;
                return output;
            }
            ENDHLSL
        }
    }
}
