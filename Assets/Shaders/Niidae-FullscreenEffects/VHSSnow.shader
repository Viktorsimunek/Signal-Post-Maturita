Shader "Hidden/Custom/URP/VHSSnow"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "VHSSnow"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "niidae_helpers.cginc"

            float _Strength;
            float2 _Scale; // x - grain scale, y - aspect ratio
            float _Threshold;
            float _FooterMultiplier;
            float _FooterHeight;
            float _FPS;

            float Snow(float2 uv)
            {
                float grainScale = max(_Scale.x, 0.001);
                float aspectRatio = max(_Scale.y, 0.001);
                float2 snowUv = uv * grainScale;
                snowUv.y *= aspectRatio;
                float frameTime = QuantizedTime(_FPS);
                float noise = Fractal2(snowUv + fmod(frameTime * 1000.0, 1048.0));
                float threshold = saturate(_Threshold);
                float snow = noise * _Strength;
                snow *= (1.-smoothstep(0.0, _FooterHeight, uv.y)) * (_FooterMultiplier) + 1.;
                snow = max(0.0, snow - threshold) / max(1.0 - threshold, 0.0001);
                return saturate(snow);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                col.rgb += Snow(uv);
                return col;
            }
            ENDHLSL
        }
    }
}
