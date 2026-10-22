Shader "Hidden/Custom/URP/VHSGrain"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "VHSGrain"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "niidae_helpers.cginc"

            float _FPS;

            float _GrainStrength;
            float _GrainScale;
            float _GrainAspect;
            float _ChromaStrength;
            float _ChromaScale;
            float _ChromaAspect;


            half Grain(float2 uv)
            {
                float2 grainUv = uv * max(_GrainScale, 0.001);
                grainUv.x *= 1.0 - saturate(-_GrainAspect);
                grainUv.y *= 1.0 - saturate(_GrainAspect);

                float t = QuantizedTime(_FPS);
                float fps = max(_FPS, 1.0);
                float frameSeed = floor(t * fps);

                float2 drift = float2(6.2 * t, -4.9 * t) + Hash22(float2(frameSeed, frameSeed + 5.0)) * 5.0;
                float2 p = grainUv * 3.0 + drift;

                float n = Fractal2(p);
                float sparkle = Hash21(floor(p * 12.0) + frameSeed * 0.13) * 2.0 - 1.0;
                n = lerp(n, sparkle, 0.30);

                return n * pow(_GrainStrength, 2) * 0.8;
            }

            half3 ChromaNoise(float2 uv)
            {
                float2 chromaUv = uv * max(_ChromaScale, 0.001);
                chromaUv.x *= 1.0 - saturate(-_ChromaAspect);
                chromaUv.y *= 1.0 - saturate(_ChromaAspect);

                float t = QuantizedTime(_FPS);
                float fps = max(_FPS, 1.0);
                float frameSeed = floor(t * fps);

                float2x2 rotR = float2x2(0.90, -0.43, 0.43, 0.90);
                float2x2 rotG = float2x2(0.56, -0.83, 0.83, 0.56);
                float2x2 rotB = float2x2(0.19, -0.98, 0.98, 0.19);

                float2 base = chromaUv * 2.0;
                float2 pR = mul(rotR, base) + float2(2.8 * t, -1.9 * t) + Hash22(float2(frameSeed + 1.0, 7.0)) * 3.0;
                float2 pG = mul(rotG, base) + float2(-2.1 * t, 2.5 * t) + Hash22(float2(frameSeed + 11.0, 17.0)) * 3.0;
                float2 pB = mul(rotB, base) + float2(1.3 * t, 3.1 * t) + Hash22(float2(frameSeed + 23.0, 29.0)) * 3.0;

                float r = Fractal2(pR * 2.1 + 5.1);
                float g = Fractal2(pG * 2.1 + 11.3);
                float b = Fractal2(pB * 2.1 + 17.9);

                return float3(r, g, b) * pow(_ChromaStrength, 2) * 0.45;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                color.rgb += Grain(uv) + ChromaNoise(uv);
                return color;
            }
            ENDHLSL
        }
    }
}
