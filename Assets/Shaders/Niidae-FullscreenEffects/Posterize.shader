Shader "Hidden/Custom/URP/PosterizeVolume"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 1)) = 1
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
            Name "PosterizeVolume"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Intensity;
            float _NumColors;
            float _ColorOffset;
            float _DitherStrength;
            float _DitherScale;

            half Bayer4x4(uint2 pos)
            {
                uint x = pos.x & 3;
                uint y = pos.y & 3;
                uint idx = y * 4 + x;

                static const half bayer[16] =
                {
                    0.0, 8.0, 2.0, 10.0,
                    12.0, 4.0, 14.0, 6.0,
                    3.0, 11.0, 1.0, 9.0,
                    15.0, 7.0, 13.0, 5.0
                };

                return bayer[idx];
            }

            float DitherValue(float2 pixelPos)
            {
                float safeScale = max(_DitherScale, 1.0);
                uint2 cellCoord = (uint2)round(pixelPos / safeScale);
                return Bayer4x4(cellCoord) * _DitherStrength / _NumColors;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 resolutionUv = _ScreenParams.xy * uv;

                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                half3 newColor = floor(color.rgb * _NumColors + _ColorOffset + DitherValue(resolutionUv)) / _NumColors;

                newColor = lerp(color.rgb, newColor.rgb, _Intensity);
                return half4(newColor, color.a);
            }
            ENDHLSL
        }
    }
}
