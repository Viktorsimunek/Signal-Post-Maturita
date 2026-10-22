Shader "Hidden/Custom/URP/VHSJitter"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "VHSJitter"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "niidae_helpers.cginc"

            float _FPS;

            float _Strength;
            float _Speed;
            float _Frequency;
            float _Gamma;
            float _FooterJitter;
            float _FooterHeight;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                float frameTime = QuantizedTime(_FPS);
                float t = fmod(frameTime * _Speed, 10000.0);
                
                float value = ValueNoise(float2((t + uv.y) * _Frequency, 0.0)) - 0.5;
                float jitter = pow(abs(value), _Gamma) * (value > 0 ? 1.0 : -1.0) * pow(_Strength, 2);

                float footerMask = pow(1. - saturate(uv.y / _FooterHeight), 2.0);
                float footer = (ValueNoise(float2(t, 1.0)) - 0.5) * pow(_FooterJitter, 2) * footerMask;

                float2 jitterUv = uv + float2(jitter * 0.02 + footer * 0.02, 0.0);
                jitterUv = clamp(jitterUv, 0.0, 1.0);
                float4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, jitterUv);
                return col;
            }
            ENDHLSL
        }
    }
}
