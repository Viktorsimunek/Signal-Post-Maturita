Shader "/SignalPost/CRTThreshold"
{
    Properties
    {
        [HideInInspector] _MainTex ("Source", 2D) = "white" {}
        [HideInInspector] _UseMainTex ("Use MainTex Source", Float) = 0
        _ColorTint ("Color Tint", Color) = (0.55, 1.0, 0.65, 1.0)
        _Threshold ("Threshold", Range(0, 1)) = 0.45
        _Softness ("Threshold Softness", Range(0.001, 0.5)) = 0.08
        _Darken ("Dark Region Multiplier", Range(0, 1)) = 0.35
        _BayerStrength ("Bayer Dither Strength", Range(0, 0.25)) = 0.08
        _BayerScale ("Bayer Dither Scale", Range(0.5, 4)) = 1.0

        _ScanlineDensity ("Scanline Density", Range(60, 600)) = 260
        _ScanlineStrength ("Scanline Strength", Range(0, 1)) = 0.16

        _NoiseStrength ("Noise Strength", Range(0, 0.35)) = 0.05
        _FlickerStrength ("Flicker Strength", Range(0, 0.2)) = 0.03
        _GhostStrength ("Ghost Strength", Range(0, 0.2)) = 0.05

        _SyncDriftStrength ("Sync Drift Strength", Range(0, 0.02)) = 0.0022
        _SyncDriftSpeed ("Sync Drift Speed", Range(0, 10)) = 2.1
        _LineJitterStrength ("Line Jitter Strength", Range(0, 0.02)) = 0.0016
        _LineJitterDensity ("Line Jitter Density", Range(10, 320)) = 96
        _VerticalJitterStrength ("Vertical Jitter Strength", Range(0, 0.02)) = 0.0009
        _VerticalJitterSpeed ("Vertical Jitter Speed", Range(0, 10)) = 1.35

        _Curvature ("Screen Curvature", Range(0, 0.2)) = 0.06
        _Vignette ("Vignette", Range(0, 1)) = 0.22
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Overlay" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "CRTThreshold"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _ColorTint;
            half _Threshold;
            half _Softness;
            half _Darken;
            half _BayerStrength;
            half _BayerScale;
            half _ScanlineDensity;
            half _ScanlineStrength;
            half _NoiseStrength;
            half _FlickerStrength;
            half _GhostStrength;
            half _SyncDriftStrength;
            half _SyncDriftSpeed;
            half _LineJitterStrength;
            half _LineJitterDensity;
            half _VerticalJitterStrength;
            half _VerticalJitterSpeed;
            float _UnscaledTime;
            half _Curvature;
            half _Vignette;
            half _UseMainTex;

            // Cheap hash for subtle film/static noise.
            inline half Hash21(half2 p)
            {
                p = frac(p * half2(123.34h, 345.45h));
                p += dot(p, p + 34.345h);
                return frac(p.x * p.y);
            }

            // 4x4 Bayer matrix normalized to 0..1 for retro ordered dithering.
            inline half Bayer4x4(float2 pixelCoord)
            {
                int2 cell = int2(floor(pixelCoord)) & 3;
                int index = cell.x + (cell.y << 2);

                if (index == 0) return 0.0h / 16.0h;
                if (index == 1) return 8.0h / 16.0h;
                if (index == 2) return 2.0h / 16.0h;
                if (index == 3) return 10.0h / 16.0h;
                if (index == 4) return 12.0h / 16.0h;
                if (index == 5) return 4.0h / 16.0h;
                if (index == 6) return 14.0h / 16.0h;
                if (index == 7) return 6.0h / 16.0h;
                if (index == 8) return 3.0h / 16.0h;
                if (index == 9) return 11.0h / 16.0h;
                if (index == 10) return 1.0h / 16.0h;
                if (index == 11) return 9.0h / 16.0h;
                if (index == 12) return 15.0h / 16.0h;
                if (index == 13) return 7.0h / 16.0h;
                if (index == 14) return 13.0h / 16.0h;
                return 5.0h / 16.0h;
            }

            inline half3 SampleSource(float2 uv)
            {
                if (_UseMainTex > 0.5h)
                {
                    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
                }

                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
            }

            half Luma(half3 color)
            {
                return dot(color, half3(0.299h, 0.587h, 0.114h));
            }

            half3 SampleGhostTrail(float2 uv, float2 texelSize)
            {
                half3 accum = 0.0h;

                const half weightA = 0.55h;
                const half weightB = 0.28h;
                const half weightC = 0.17h;

                half3 sampleA = SampleSource(saturate(uv + float2(texelSize.x * 2.0, 0.0)));
                half3 sampleB = SampleSource(saturate(uv + float2(texelSize.x * 5.0, texelSize.y * 0.35)));
                half3 sampleC = SampleSource(saturate(uv + float2(texelSize.x * 10.0, -texelSize.y * 0.3)));

                accum += sampleA * Luma(sampleA) * weightA;
                accum += sampleB * Luma(sampleB) * weightB;
                accum += sampleC * Luma(sampleC) * weightC;

                return accum;
            }

            float2 ApplyCrtUv(float2 uv, float2 textureSize)
            {
                float2 centered = uv * 2.0 - 1.0;
                float r2 = dot(centered, centered);
                centered *= 1.0 + (_Curvature * r2);
                uv = centered * 0.5 + 0.5;

                float time = _UnscaledTime;
                float lineNoise = Hash21(half2(floor(uv.y * textureSize.y * 0.35), floor(time * 18.0)));
                float lineWave = sin((uv.y * _LineJitterDensity) + time * _SyncDriftSpeed);
                float syncBand = sin((uv.y * 32.0) - time * (_SyncDriftSpeed * 2.3));
                float verticalWave = sin((uv.x * 18.0) + time * _VerticalJitterSpeed);

                uv.x += lineWave * _LineJitterStrength;
                uv.x += (lineNoise - 0.5) * _SyncDriftStrength * (0.4 + 0.6 * saturate(syncBand));
                uv.y += verticalWave * _VerticalJitterStrength;

                return uv;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 textureSize = _ScreenParams.xy;
                float2 uv = ApplyCrtUv(input.texcoord, textureSize);

                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                {
                    return half4(0.0h, 0.0h, 0.0h, 1.0h);
                }

                half3 color = SampleSource(uv);
                half3 ghostTrail = SampleGhostTrail(uv, 1.0 / textureSize);

                half luma = dot(color, half3(0.299h, 0.587h, 0.114h));
                float2 ditherCoord = (uv * textureSize) / max(_BayerScale, 0.001h);
                half bayer = Bayer4x4(ditherCoord) - 0.5h;
                half ditheredLuma = saturate(luma + bayer * _BayerStrength);
                half gate = smoothstep(_Threshold - _Softness, _Threshold + _Softness, ditheredLuma);
                color = lerp(color * _Darken, color, gate);

                half scan = 1.0h - (_ScanlineStrength * (0.5h + 0.5h * sin((uv.y + _UnscaledTime * 0.2h) * _ScanlineDensity)));
                half flicker = 1.0h + ((frac(_UnscaledTime * 120.0h)) - 0.5h) * _FlickerStrength;
                half noise = (Hash21(uv * _ScreenParams.xy + float2(_UnscaledTime, _UnscaledTime) * 73.1h) - 0.5h) * _NoiseStrength;

                half2 v = uv * (1.0h - uv.yx);
                half vignette = pow(saturate(v.x * v.y * 16.0h), 0.18h);
                vignette = lerp(1.0h, vignette, _Vignette);

                color += ghostTrail * _GhostStrength;
                color *= scan * flicker * vignette;
                color += noise;
                color *= _ColorTint.rgb;

                return half4(saturate(color), 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
