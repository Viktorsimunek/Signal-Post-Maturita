Shader "SignalPost/CRTThresholdMaterial"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (1, 1, 1, 1)

        _ColorTint ("Color Tint", Color) = (0.55, 1.0, 0.65, 1.0)
        _GlassTint ("Glass Tint", Color) = (0.08, 0.13, 0.10, 1.0)
        _EmissionStrength ("Emission Strength", Range(0, 8)) = 1.4
        _Threshold ("Threshold", Range(0, 1)) = 0.45
        _Softness ("Threshold Softness", Range(0.001, 0.5)) = 0.08
        _Darken ("Dark Region Multiplier", Range(0, 1)) = 0.35

        _ScanlineDensity ("Scanline Density", Range(60, 600)) = 260
        _ScanlineStrength ("Scanline Strength", Range(0, 1)) = 0.16

        _NoiseStrength ("Noise Strength", Range(0, 0.35)) = 0.05
        _FlickerStrength ("Flicker Strength", Range(0, 0.2)) = 0.03

        _SyncDriftStrength ("Sync Drift Strength", Range(0, 0.02)) = 0.0022
        _SyncDriftSpeed ("Sync Drift Speed", Range(0, 10)) = 2.1
        _LineJitterStrength ("Line Jitter Strength", Range(0, 0.02)) = 0.0016
        _LineJitterDensity ("Line Jitter Density", Range(10, 320)) = 96
        _VerticalJitterStrength ("Vertical Jitter Strength", Range(0, 0.02)) = 0.0009
        _VerticalJitterSpeed ("Vertical Jitter Speed", Range(0, 10)) = 1.35

        _Curvature ("Screen Curvature", Range(0, 0.2)) = 0.06
        _Vignette ("Vignette", Range(0, 1)) = 0.22

        _LightResponse ("Light Response", Range(0, 2)) = 0.3
        _SpecularStrength ("Specular Strength", Range(0, 4)) = 1.2
        _Smoothness ("Smoothness", Range(0, 1)) = 0.88
        _FresnelStrength ("Fresnel Strength", Range(0, 2)) = 0.65

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2

        [HideInInspector] _MainTex ("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color ("Base Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _EmissionColor;
            half4 _ColorTint;
            half4 _GlassTint;
            half _EmissionStrength;
            half _Threshold;
            half _Softness;
            half _Darken;
            half _ScanlineDensity;
            half _ScanlineStrength;
            half _NoiseStrength;
            half _FlickerStrength;
            half _SyncDriftStrength;
            half _SyncDriftSpeed;
            half _LineJitterStrength;
            half _LineJitterDensity;
            half _VerticalJitterStrength;
            half _VerticalJitterSpeed;
            float _UnscaledTime;
            half _Curvature;
            half _Vignette;
            half _LightResponse;
            half _SpecularStrength;
            half _Smoothness;
            half _FresnelStrength;
            CBUFFER_END

            float4 _BaseMap_TexelSize;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            inline half Hash21(half2 p)
            {
                p = frac(p * half2(123.34h, 345.45h));
                p += dot(p, p + 34.345h);
                return frac(p.x * p.y);
            }

            inline half SignedTriangle(half phase)
            {
                return (abs(frac(phase) - 0.5h) * 4.0h) - 1.0h;
            }

            inline half SoftVignette(half value)
            {
                value = saturate(value);
                return sqrt(sqrt(value));
            }

            float2 ApplyCrtUv(float2 uv, float2 textureSize)
            {
                float2 centered = uv * 2.0 - 1.0;
                float r2 = dot(centered, centered);
                centered *= 1.0 + (_Curvature * r2);
                uv = centered * 0.5 + 0.5;

                half time = _UnscaledTime;

                UNITY_BRANCH
                if (_LineJitterStrength > 0.00001h)
                {
                    half lineWave = SignedTriangle((uv.y * _LineJitterDensity + time * _SyncDriftSpeed) * 0.15915494h);
                    uv.x += lineWave * _LineJitterStrength;
                }

                UNITY_BRANCH
                if (_SyncDriftStrength > 0.00001h)
                {
                    half lineNoise = Hash21(half2(floor(uv.y * textureSize.y * 0.35), floor(time * 18.0h)));
                    half syncBand = saturate(SignedTriangle((uv.y * 32.0h - time * (_SyncDriftSpeed * 2.3h)) * 0.15915494h));
                    uv.x += (lineNoise - 0.5h) * _SyncDriftStrength * (0.4h + 0.6h * syncBand);
                }

                UNITY_BRANCH
                if (_VerticalJitterStrength > 0.00001h)
                {
                    half verticalWave = SignedTriangle((uv.x * 18.0h + time * _VerticalJitterSpeed) * 0.15915494h);
                    uv.y += verticalWave * _VerticalJitterStrength;
                }

                return uv;
            }

            half3 ApplyCrtColor(float2 uv, float2 textureSize, half3 color)
            {
                half luma = dot(color, half3(0.299h, 0.587h, 0.114h));
                half gate = smoothstep(_Threshold - _Softness, _Threshold + _Softness, luma);
                color = lerp(color * _Darken, color, gate);

                half scanPhase = (uv.y + _UnscaledTime * 0.2h) * (_ScanlineDensity * 0.15915494h);
                half scanWave = 0.5h + 0.5h * SignedTriangle(scanPhase);
                half scan = 1.0h - (_ScanlineStrength * scanWave);
                half flicker = 1.0h + ((frac(_UnscaledTime * 120.0h)) - 0.5h) * _FlickerStrength;
                half noise = 0.0h;

                UNITY_BRANCH
                if (_NoiseStrength > 0.0001h)
                {
                    noise = (Hash21(uv * textureSize + half2(_UnscaledTime, _UnscaledTime) * 73.1h) - 0.5h) * _NoiseStrength;
                }

                half2 vignetteUV = uv * (1.0h - uv.yx);
                half vignette = SoftVignette(vignetteUV.x * vignetteUV.y * 16.0h);
                vignette = lerp(1.0h, vignette, _Vignette);

                color *= scan * flicker * vignette;
                color += noise;
                return max(color, 0.0h);
            }

            half3 EvaluateLighting(half3 normalWS, half3 viewDirWS, half3 surfaceColor)
            {
                half3 lighting = SampleSH(normalWS) * surfaceColor * (_LightResponse * 0.35h);

                Light mainLight = GetMainLight();
                half3 mainRadiance = mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation);
                half mainNdotL = saturate(dot(normalWS, mainLight.direction));
                half3 mainHalfDir = SafeNormalize(mainLight.direction + viewDirWS);
                half specBase = saturate(dot(normalWS, mainHalfDir));
                half spec2 = specBase * specBase;
                half spec8 = spec2 * spec2;
                spec8 *= spec8;
                half mainSpec = lerp(spec2, spec8, _Smoothness) * _SpecularStrength * mainNdotL;

                lighting += surfaceColor * mainRadiance * mainNdotL * _LightResponse;
                lighting += mainRadiance * mainSpec;

                half fresnelBase = 1.0h - saturate(dot(normalWS, viewDirWS));
                half fresnel = fresnelBase * fresnelBase;
                fresnel *= fresnel * _FresnelStrength;
                lighting += fresnel * (_GlassTint.rgb + 0.15h) * _SpecularStrength;
                return lighting;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.normalWS = normalInputs.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(positionInputs.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 textureSize = max(_BaseMap_TexelSize.zw, float2(1.0, 1.0));
                float2 uv = ApplyCrtUv(input.uv, textureSize);

                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                {
                    return half4(0.0h, 0.0h, 0.0h, _BaseColor.a);
                }

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;

                half3 crtColor = ApplyCrtColor(uv, textureSize, baseSample.rgb);

                half3 emissive = crtColor * _ColorTint.rgb;
                emissive *= max(baseSample.rgb, half3(0.25h, 0.25h, 0.25h));
                emissive *= _EmissionColor.rgb * _EmissionStrength;

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = SafeNormalize(input.viewDirWS);
                half3 surfaceColor = lerp(_GlassTint.rgb, crtColor * 0.4h + _GlassTint.rgb, 0.35h) * _BaseColor.rgb;
                half3 lighting = EvaluateLighting(normalWS, viewDirWS, surfaceColor);

                half3 finalColor = lighting + emissive;
                return half4(max(finalColor, 0.0h), baseSample.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
