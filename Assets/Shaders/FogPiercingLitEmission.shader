Shader "SignalPost/Fog Piercing Lit Emission"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _EmissionMap ("Emission Mask", 2D) = "black" {}
        [HDR]_EmissionColor ("Emission Color", Color) = (1, 0.78, 0.42, 1)
        _EmissionIntensity ("Emission Intensity", Float) = 8
        _LightOnAmount ("Light On Amount", Range(0, 1)) = 1
        _LightRandomness ("Light Randomness", Range(0, 1)) = 0.75
        _LightRandomGrid ("Light Voronoi Scale", Float) = 36
        _LightEdgeSoftness ("Light Edge Softness", Range(0.001, 0.4)) = 0.08
        _LightOffEmission ("Light Off Emission", Range(0, 1)) = 0.03
        _LightRandomSeed ("Light Random Seed", Float) = 0
        _EmissionShadowDimmer ("Emission Shadow Dimmer", Range(0, 1)) = 0.15
        _FogAffectsEmission ("Fog Affects Emission", Range(0, 1)) = 0.15
        _AlphaClip ("Alpha Clip", Range(0, 1)) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EmissionColor;
                half _EmissionIntensity;
                half _LightOnAmount;
                half _LightRandomness;
                half _LightRandomGrid;
                half _LightEdgeSoftness;
                half _LightOffEmission;
                half _LightRandomSeed;
                half _EmissionShadowDimmer;
                half _FogAffectsEmission;
                half _AlphaClip;
                half _Cutoff;
            CBUFFER_END

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            float2 Hash22(float2 value)
            {
                float x = Hash21(value);
                float y = Hash21(value + 19.19);
                return float2(x, y);
            }

            float VoronoiLightThreshold(float2 uv)
            {
                float seed = _LightRandomSeed;
                float2 uvSeedOffset = Hash22(float2(seed, seed + 31.17)) * 19.0;
                float2 scaledUv = (uv * max(1.0, _LightRandomGrid)) + uvSeedOffset;
                float2 baseCell = floor(scaledUv);
                float2 localUv = frac(scaledUv);

                float nearestDistance = 8.0;
                float2 nearestCell = baseCell;

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbor = float2(x, y);
                        float2 cell = baseCell + neighbor;
                        float2 feature = Hash22(cell);
                        float2 offset = neighbor + feature - localUv;
                        float distanceToFeature = dot(offset, offset);

                        if (distanceToFeature < nearestDistance)
                        {
                            nearestDistance = distanceToFeature;
                            nearestCell = cell;
                        }
                    }
                }

                return Hash21(nearestCell + seed + 7.73);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = GetShadowCoord(positionInputs);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                if (_AlphaClip > 0.5h)
                {
                    clip(baseSample.a - _Cutoff);
                }

                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                Light mainLight = GetMainLight(input.shadowCoord);
                half mainLightAmount = saturate(dot(normalWS, mainLight.direction)) * mainLight.shadowAttenuation;
                half3 ambient = SampleSH(normalWS);
                half3 litColor = baseSample.rgb * (ambient + mainLight.color * mainLightAmount);

                half emissionMask = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).r;
                half randomThreshold = VoronoiLightThreshold(input.uv);
                half edgeSoftness = max(0.001h, _LightEdgeSoftness);
                half lightOnAmount = saturate(_LightOnAmount);
                half sequentialLights = smoothstep(randomThreshold - edgeSoftness, randomThreshold + edgeSoftness, lightOnAmount);
                half lightOn = lerp(lightOnAmount, sequentialLights, saturate(_LightRandomness));
                lightOn = max(lightOn, saturate(_LightOffEmission));
                half emissionShadow = lerp(1.0h, mainLight.shadowAttenuation, _EmissionShadowDimmer);
                half3 emission = _EmissionColor.rgb * (_EmissionIntensity * emissionMask * lightOn * emissionShadow);

                half fogVisibility = ComputeFogIntensity(input.fogFactor);
                half emissionFogVisibility = lerp(1.0h, fogVisibility, saturate(_FogAffectsEmission));
                half3 foggedLitColor = MixFog(litColor, input.fogFactor);
                half3 foggedEmission = emission * emissionFogVisibility;

                return half4(foggedLitColor + foggedEmission, baseSample.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EmissionColor;
                half _EmissionIntensity;
                half _LightOnAmount;
                half _LightRandomness;
                half _LightRandomGrid;
                half _LightEdgeSoftness;
                half _LightOffEmission;
                half _LightRandomSeed;
                half _EmissionShadowDimmer;
                half _FogAffectsEmission;
                half _AlphaClip;
                half _Cutoff;
            CBUFFER_END

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
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                if (_AlphaClip > 0.5h)
                {
                    clip(baseSample.a - _Cutoff);
                }

                return 0;
            }
            ENDHLSL
        }
    }
}
