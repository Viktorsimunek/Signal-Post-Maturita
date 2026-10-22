Shader "Custom/Foliage/Tree Leaves Wind Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.33

        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0.0, 2.0)) = 1.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.05
        _SpecColor("Specular", Color) = (0.08, 0.12, 0.08, 1.0)
        [HDR] _EmissionColor("Emission", Color) = (0,0,0,0)
        _EmissionMap("Emission Map", 2D) = "black" {}
        _AmbientStrength("Ambient Strength", Range(0.0, 1.0)) = 0.45
        _DirectLightStrength("Direct Light Strength", Range(0.0, 2.0)) = 1.0
        _BackLightStrength("Back Light Strength", Range(0.0, 1.5)) = 0.35
        _ShadowStrength("Shadow Strength", Range(0.0, 1.0)) = 0.8
        _MinNdotL("Minimum Light", Range(0.0, 1.0)) = 0.2
        _CameraFacingNormal("Camera Facing Normal", Range(0.0, 1.0)) = 0.75
        _NormalDirection("Fixed Normal Direction", Vector) = (0, 1, 0, 0)

        [Header(Wind)]
        _WindDirection("Wind Direction", Vector) = (1, 0, 0.25, 0)
        _WindStrength("Wind Strength", Range(0.0, 0.4)) = 0.08
        _WindSpeed("Wind Speed", Range(0.0, 6.0)) = 1.35
        _WindSpatialFrequency("Wind Spatial Frequency", Range(0.01, 2.0)) = 0.22
        _GustStrength("Gust Strength", Range(0.0, 0.3)) = 0.045
        _GustFrequency("Gust Frequency", Range(0.0, 4.0)) = 0.55
        _EdgeFlutter("Edge Flutter", Range(0.0, 0.15)) = 0.02
        _VerticalInfluence("Vertical Lift", Range(0.0, 0.2)) = 0.025
        _WindMaskPower("UV Wind Mask Power", Range(0.25, 4.0)) = 1.5
        _VertexColorInfluence("Vertex Color Influence", Range(0.0, 1.0)) = 0.5

        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _Cull("__cull", Float) = 0.0
        [HideInInspector] _AlphaClip("__clip", Float) = 1.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _BlendModePreserveSpecular("_BlendModePreserveSpecular", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 1.0
        [HideInInspector] _QueueOffset("Queue offset", Float) = 0.0
        [HideInInspector] _ReceiveShadows("Receive Shadows", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        HLSLINCLUDE
        #pragma target 2.0

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"
        #if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
        #endif

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _SpecColor;
            half4 _EmissionColor;
            half _Cutoff;
            half _BumpScale;
            half _Smoothness;
            half _AmbientStrength;
            half _DirectLightStrength;
            half _BackLightStrength;
            half _ShadowStrength;
            half _MinNdotL;
            half _CameraFacingNormal;
            float4 _NormalDirection;
            float4 _WindDirection;
            half _WindStrength;
            half _WindSpeed;
            half _WindSpatialFrequency;
            half _GustStrength;
            half _GustFrequency;
            half _EdgeFlutter;
            half _VerticalInfluence;
            half _WindMaskPower;
            half _VertexColorInfluence;
        CBUFFER_END

        float3 _LightDirection;
        float3 _LightPosition;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float4 color : COLOR;
            float2 texcoord : TEXCOORD0;
            float2 staticLightmapUV : TEXCOORD1;
            float2 dynamicLightmapUV : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float2 uv : TEXCOORD0;
            half3 lighting : TEXCOORD1;
            half fogFactor : TEXCOORD2;
            float4 positionCS : SV_POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        float3 ApplyLeafWind(float3 positionOS, float2 uv, float4 color)
        {
            float3 positionWS = TransformObjectToWorld(positionOS);

            float2 dir = _WindDirection.xz;
            float dirLengthSq = max(dot(dir, dir), 1e-4);
            dir *= rsqrt(dirLengthSq);
            float2 sideDir = float2(-dir.y, dir.x);

            float uvMask = pow(saturate(uv.y), _WindMaskPower);
            float vertexMask = saturate(max(color.r, color.a));
            float windMask = saturate(max(uvMask, vertexMask * _VertexColorInfluence));

            float time = _Time.y * _WindSpeed;
            float phase = dot(positionWS.xz, dir * _WindSpatialFrequency) + dot(positionWS.xz, float2(0.17, -0.11));

            float swayA = sin(time + phase);
            float swayB = sin(time * 1.73 - phase * 1.37) * 0.5;
            float gust = (sin(time * _GustFrequency + phase * 0.43) * 0.5 + 0.5) * _GustStrength;
            float edgeMask = saturate(abs(uv.x - 0.5) * 2.0);
            float flutter = sin(time * 2.9 + uv.x * 11.0 + positionWS.y * 0.65 + phase * 1.8) * _EdgeFlutter * edgeMask;

            float mainDisplacement = (swayA + swayB) * (_WindStrength + gust) * windMask;
            float sideDisplacement = flutter * windMask;
            float verticalDisplacement = (swayA * 0.35 + swayB * 0.2 + flutter * 0.5) * _VerticalInfluence * windMask;

            positionWS.xz += dir * mainDisplacement;
            positionWS.xz += sideDir * sideDisplacement;
            positionWS.y += verticalDisplacement;
            return positionWS;
        }

        float3 GetBillboardLightingNormal(float3 positionWS)
        {
            float3 fixedNormalWS = _NormalDirection.xyz;
            if (dot(fixedNormalWS, fixedNormalWS) < 1e-4)
            {
                fixedNormalWS = float3(0.0, 1.0, 0.0);
            }

            fixedNormalWS = normalize(fixedNormalWS);
            float3 cameraNormalWS = SafeNormalize(GetCameraPositionWS() - positionWS);
            return normalize(lerp(fixedNormalWS, cameraNormalWS, _CameraFacingNormal));
        }

        Varyings LeafVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float3 deformedPositionWS = ApplyLeafWind(input.positionOS.xyz, input.texcoord, input.color);
            float4 positionCS = TransformWorldToHClip(deformedPositionWS);
            float3 lightingNormalWS = GetBillboardLightingNormal(deformedPositionWS);
            float4 shadowCoord = TransformWorldToShadowCoord(deformedPositionWS);
            Light mainLight = GetMainLight(shadowCoord);
            half3 bakedGI = SampleSH(lightingNormalWS);
            half3 vertexAdditional = VertexLighting(deformedPositionWS, lightingNormalWS) * 0.5h;
            half ndotl = max(abs(dot(lightingNormalWS, mainLight.direction)), _MinNdotL);
            half shadowAtten = lerp(1.0h, mainLight.shadowAttenuation, _ShadowStrength);
            half3 directLighting = mainLight.color * (ndotl * _DirectLightStrength + _BackLightStrength) * shadowAtten;
            half3 ambientLighting = bakedGI * _AmbientStrength;

            output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
            output.positionCS = positionCS;
            output.lighting = max(ambientLighting + directLighting + vertexAdditional, _AmbientStrength.xxx * 0.35h);
            output.fogFactor = ComputeFogFactor(positionCS.z);
            return output;
        }

        half4 LeafFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        #if defined(LOD_FADE_CROSSFADE)
            LODFadeCrossFade(input.positionCS);
        #endif

            half4 albedoAlpha = SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
            half alpha = AlphaDiscard(albedoAlpha.a * _BaseColor.a, _Cutoff);
            half3 albedo = albedoAlpha.rgb * _BaseColor.rgb;
            half3 emission = SampleEmission(input.uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));
            half3 color = albedo * input.lighting + emission;
            color = MixFog(color, input.fogFactor);
            return half4(color, alpha);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend One Zero
            ZWrite On
            Cull Off
            AlphaToMask On

            HLSLPROGRAM
            #pragma vertex LeafVertex
            #pragma fragment LeafFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_instancing
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
