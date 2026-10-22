Shader "Hidden/Custom/URP/PixelizationVolume"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 1)) = 1
        _PixelSize ("Pixel Size", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "PixelizationVolume"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Intensity;
            float _PixelSize;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 srcResolution = max(_ScreenParams.xy, float2(1.0, 1.0));
                float safePixelSize = max(_PixelSize, 1.0);

                float2 pixelPos = uv * srcResolution;
                float2 cellOrigin = round(pixelPos / safePixelSize) * safePixelSize;
                float2 pixelatedUv = (cellOrigin + 0.5) / srcResolution;

                half4 srcColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half4 pixColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, pixelatedUv);

                return lerp(srcColor, pixColor, saturate(_Intensity));
            }
            ENDHLSL
        }
    }
}
