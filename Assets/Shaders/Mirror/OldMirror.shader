Shader "Signal Post/Old Mirror"
{
    Properties
    {
        [HideInInspector] _ReflectionTex ("Reflection", 2D) = "black" {}
        _ReflectionIntensity ("Reflection Intensity", Range(0, 1)) = 0.88
        _ReflectionShadowDepth ("Reflection Shadow Depth", Range(0, 1)) = 0.45
        _ReflectionTint ("Reflection Tint", Color) = (1, 1, 1, 1)
        _OverlayTex ("Overlay Texture (RGBA)", 2D) = "black" {}
        _OverlayIntensity ("Overlay Intensity", Range(0, 1)) = 0.35
        _OverlaySaturation ("Overlay Saturation", Range(0, 1)) = 0
        _DistortionTex ("Distortion Texture (RG)", 2D) = "gray" {}
        _DistortionStrength ("Distortion Strength", Range(0, 0.03)) = 0
        _SurfaceLightStrength ("Surface Light Strength", Range(0, 1)) = 0.25
        _SurfaceSpecular ("Surface Specular", Range(0, 1)) = 0.12
        _SurfaceSmoothness ("Surface Smoothness", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Mirror"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_ReflectionTex); SAMPLER(sampler_ReflectionTex);
            TEXTURE2D(_OverlayTex); SAMPLER(sampler_OverlayTex);
            TEXTURE2D(_DistortionTex); SAMPLER(sampler_DistortionTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _OverlayTex_ST;
                float4 _DistortionTex_ST;
                half4 _ReflectionTint;
                half _ReflectionIntensity;
                half _ReflectionShadowDepth;
                half _OverlayIntensity;
                half _OverlaySaturation;
                float _DistortionStrength;
                half _SurfaceLightStrength;
                half _SurfaceSpecular;
                half _SurfaceSmoothness;
            CBUFFER_END
            float4x4 _ReflectionVP;
            float _PlanarReflectionRendering;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 reflected : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half3 normalWS : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.reflected = mul(_ReflectionVP, float4(world, 1));
                output.uv = input.uv;
                output.positionWS = world;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.shadowCoord = GetShadowCoord(GetVertexPositionInputs(input.positionOS.xyz));
                return output;
            }

            // Simple diffuse + Blinn-Phong highlight; reuses URP's existing lights and shadows.
            void AddSurfaceLight(Light light, half3 normal, half3 viewDirection, half exponent,
                inout half3 diffuse, inout half3 specular)
            {
                half facing = saturate(dot(normal, light.direction));
                half3 radiance = light.color * (light.distanceAttenuation * light.shadowAttenuation) * facing;
                diffuse += radiance;
                half highlight = pow(saturate(dot(normal, SafeNormalize(light.direction + viewDirection))), exponent);
                specular += radiance * highlight;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // All mirror surfaces disappear during a reflection render, including other mirrors.
                clip(0.5 - _PlanarReflectionRendering);
                float2 uv = input.reflected.xy / max(input.reflected.w, 0.0001) * 0.5 + 0.5;
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1 - uv.y;
                #endif
                float2 distortion = SAMPLE_TEXTURE2D(_DistortionTex, sampler_DistortionTex,
                    input.uv * _DistortionTex_ST.xy + _DistortionTex_ST.zw).rg * 2 - 1;
                half3 reflection = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, saturate(uv + distortion * _DistortionStrength)).rgb;
                // Cheap tonal approximation of deeper shading; preserves bright highlights.
                reflection = lerp(reflection, reflection * reflection, _ReflectionShadowDepth);
                half4 dirt = SAMPLE_TEXTURE2D(_OverlayTex, sampler_OverlayTex,
                    input.uv * _OverlayTex_ST.xy + _OverlayTex_ST.zw);
                dirt.rgb = lerp(dot(dirt.rgb, half3(0.299, 0.587, 0.114)).xxx, dirt.rgb, _OverlaySaturation);
                half3 glass = reflection * _ReflectionTint.rgb * _ReflectionIntensity;
                half dirtAlpha = saturate(dirt.a * _OverlayIntensity);
                half3 color = lerp(glass, dirt.rgb, dirtAlpha);

                half3 diffuse = 0;
                half3 specular = 0;
                half3 normal = normalize(input.normalWS);
                half3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half exponent = exp2(lerp(3.0h, 9.0h, _SurfaceSmoothness));
                Light mainLight = GetMainLight(input.shadowCoord, input.positionWS, half4(1, 1, 1, 1));
                AddSurfaceLight(mainLight, normal, viewDirection, exponent, diffuse, specular);

                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    #if USE_CLUSTER_LIGHT_LOOP
                        [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                        {
                            CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                            Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                            AddSurfaceLight(light, normal, viewDirection, exponent, diffuse, specular);
                        }
                    #endif
                    uint lightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                        AddSurfaceLight(light, normal, viewDirection, exponent, diffuse, specular);
                    LIGHT_LOOP_END
                #endif

                // Keep even a strong flashlight subtle instead of washing out the reflection.
                diffuse = diffuse / (1.0h + diffuse);
                specular = specular / (1.0h + specular);
                color += diffuse * lerp(half3(0.12h, 0.12h, 0.12h), dirt.rgb, dirtAlpha) * _SurfaceLightStrength;
                color += specular * _SurfaceSpecular * (1.0h - dirtAlpha * 0.6h);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
