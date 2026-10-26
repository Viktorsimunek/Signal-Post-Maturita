// Derived from URP 17.3 Terrain/Lit. Local copy for terrain anti-repetition.
#ifndef UNIVERSAL_TERRAIN_LIT_INPUT_INCLUDED
#define UNIVERSAL_TERRAIN_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"

CBUFFER_START(UnityPerMaterial)
    float _AntiTileStrength;
    float _VariationSize;
    float _MacroStrength;
    half4 _PerlinColor;
    float _PerlinStrength;
    float _PerlinSize;
    float4 _MainTex_ST;
    half4 _BaseColor;
    half _Cutoff;

    UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(_Control);
    float4 _Splat0_TexelSize, _Splat1_TexelSize, _Splat2_TexelSize, _Splat3_TexelSize;
    UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(_Splat0);
    UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(_Splat1);
    UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(_Splat2);
    UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(_Splat3);
CBUFFER_END

#define _Surface 0.0 // Terrain is always opaque

CBUFFER_START(_Terrain)
    half _NormalScale0, _NormalScale1, _NormalScale2, _NormalScale3;
    half _Metallic0, _Metallic1, _Metallic2, _Metallic3;
    half _Smoothness0, _Smoothness1, _Smoothness2, _Smoothness3;
    half4 _DiffuseRemapScale0, _DiffuseRemapScale1, _DiffuseRemapScale2, _DiffuseRemapScale3;
    half4 _MaskMapRemapOffset0, _MaskMapRemapOffset1, _MaskMapRemapOffset2, _MaskMapRemapOffset3;
    half4 _MaskMapRemapScale0, _MaskMapRemapScale1, _MaskMapRemapScale2, _MaskMapRemapScale3;

    float4 _Control_ST;
    float4 _Control_TexelSize;
    half _DiffuseHasAlpha0, _DiffuseHasAlpha1, _DiffuseHasAlpha2, _DiffuseHasAlpha3;
    half _LayerHasMask0, _LayerHasMask1, _LayerHasMask2, _LayerHasMask3;
    half4 _Splat0_ST, _Splat1_ST, _Splat2_ST, _Splat3_ST;
    half _HeightTransition;
    half _NumLayersCount;

    #ifdef UNITY_INSTANCING_ENABLED
    float4 _TerrainHeightmapRecipSize;   // float4(1.0f/width, 1.0f/height, 1.0f/(width-1), 1.0f/(height-1))
    float4 _TerrainHeightmapScale;       // float4(hmScale.x, hmScale.y / (float)(kMaxHeight), hmScale.z, 0.0f)
    #endif
    #ifdef SCENESELECTIONPASS
    int _ObjectId;
    int _PassValue;
    #endif
CBUFFER_END


TEXTURE2D(_Control);    SAMPLER(sampler_Control);
TEXTURE2D(_Splat0);     SAMPLER(sampler_Splat0);
TEXTURE2D(_Splat1);     SAMPLER(sampler_Splat1);
TEXTURE2D(_Splat2);     SAMPLER(sampler_Splat2);
TEXTURE2D(_Splat3);     SAMPLER(sampler_Splat3);

TEXTURE2D(_Normal0);     SAMPLER(sampler_Normal0);
TEXTURE2D(_Normal1);     SAMPLER(sampler_Normal1);
TEXTURE2D(_Normal2);     SAMPLER(sampler_Normal2);
TEXTURE2D(_Normal3);     SAMPLER(sampler_Normal3);

TEXTURE2D(_Mask0);      SAMPLER(sampler_Mask0);
TEXTURE2D(_Mask1);      SAMPLER(sampler_Mask1);
TEXTURE2D(_Mask2);      SAMPLER(sampler_Mask2);
TEXTURE2D(_Mask3);      SAMPLER(sampler_Mask3);

TEXTURE2D(_MainTex);       SAMPLER(sampler_MainTex);
TEXTURE2D(_SpecGlossMap);  SAMPLER(sampler_SpecGlossMap);
TEXTURE2D(_MetallicTex);   SAMPLER(sampler_MetallicTex);

#if defined(UNITY_INSTANCING_ENABLED) && defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL)
#define ENABLE_TERRAIN_PERPIXEL_NORMAL
#endif

#ifdef UNITY_INSTANCING_ENABLED
TEXTURE2D(_TerrainHeightmapTexture);
TEXTURE2D(_TerrainNormalmapTexture);
SAMPLER(sampler_TerrainNormalmapTexture);
#endif

UNITY_INSTANCING_BUFFER_START(Terrain)
UNITY_DEFINE_INSTANCED_PROP(float4, _TerrainPatchInstanceData)  // float4(xBase, yBase, skipScale, ~)
UNITY_INSTANCING_BUFFER_END(Terrain)

#ifdef _ALPHATEST_ON
TEXTURE2D(_TerrainHolesTexture);
SAMPLER(sampler_TerrainHolesTexture);

float SampleTerrainHolesTexture(float2 uv)
{
    return SAMPLE_TEXTURE2D(_TerrainHolesTexture, sampler_TerrainHolesTexture, uv).r;
}

void ClipHoles(float2 uv)
{
    float hole = SampleTerrainHolesTexture(uv);
    // Fixes bug where compression is enabled and 0 isn't actually 0 but low like 1/2047. (UUM-61913)
    float epsilon = 0.0005f;
    clip(hole < epsilon ? -1 : 1);
}
#endif


// Translation-only blending keeps tangent-space normals aligned with the albedo.
// Explicit gradients avoid mip seams where the noise changes the blend weight.
float TerrainVariationHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}
float TerrainVariationNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(TerrainVariationHash(cell), TerrainVariationHash(cell + float2(1,0)), f.x),
                lerp(TerrainVariationHash(cell + float2(0,1)), TerrainVariationHash(cell + 1), f.x), f.y);
}
// Gradient Perlin noise with quintic interpolation (not value noise).
float2 TerrainPerlinGradient(float2 cell)
{
    float angle = TerrainVariationHash(cell) * 6.28318530718;
    return float2(cos(angle), sin(angle));
}
float TerrainPerlinNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    float2 fade = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    float a = dot(TerrainPerlinGradient(cell), f);
    float b = dot(TerrainPerlinGradient(cell + float2(1, 0)), f - float2(1, 0));
    float c = dot(TerrainPerlinGradient(cell + float2(0, 1)), f - float2(0, 1));
    float d = dot(TerrainPerlinGradient(cell + 1.0), f - 1.0);
    return lerp(lerp(a, b, fade.x), lerp(c, d, fade.x), fade.y);
}
half3 TerrainPerlinOverlay(half3 albedo, float2 terrainUV)
{
    // Shared terrain UVs keep the overlay continuous across painted layers and
    // identical in near, additive and generated distant terrain passes.
    float2 p = terrainUV / max(_PerlinSize, 0.001) + float2(13.17, 7.93);
    float noise = TerrainPerlinNoise(p) * 0.75
                + TerrainPerlinNoise(p * 2.03 + 19.7) * 0.25;
    half mask = smoothstep(0.28, 0.72, saturate(noise * 0.9 + 0.5));
    half amount = saturate(mask * _PerlinStrength * _PerlinColor.a);
    return lerp(albedo, _PerlinColor.rgb, amount);
}

half4 TerrainAntiRepeatSample(TEXTURE2D_PARAM(tex, texSampler), float2 uv)
{
    float2 dx = ddx(uv);
    float2 dy = ddy(uv);
    float blend = TerrainVariationNoise(uv / max(_VariationSize, 1.0));
    half4 original = SAMPLE_TEXTURE2D_GRAD(tex, texSampler, uv, dx, dy);
    half4 shifted = SAMPLE_TEXTURE2D_GRAD(tex, texSampler, uv + float2(0.37, 0.61), dx, dy);
    return lerp(original, shifted, smoothstep(0.15, 0.85, blend) * _AntiTileStrength);
}
half TerrainMacroVariation(float2 uv)
{
    float2 p = uv / max(_VariationSize, 1.0);
    float noise = TerrainVariationNoise(p * 0.47 + 17.3) * 0.65
                + TerrainVariationNoise(p * 1.31 + 4.7) * 0.35;
    return 1.0h + (noise * 2.0h - 1.0h) * _MacroStrength;
}

#define SampleLayerAlbedo(i) (TerrainAntiRepeatSample(TEXTURE2D_ARGS(_Splat##i, sampler_Splat0), splat##i##uv) * half4(_DiffuseRemapScale##i.rgb * TerrainMacroVariation(splat##i##uv), 1.0h))

#ifdef _NORMALMAP
    #define SampleLayerNormal(i) UnpackNormalScale(TerrainAntiRepeatSample(TEXTURE2D_ARGS(_Normal##i, sampler_Normal0), splat##i##uv), _NormalScale##i)
#else
    #define SampleLayerNormal(i) half3(0.0, 0.0, 1.0)
#endif

#ifdef _MASKMAP
    #define SampleLayerMasks(i) (_MaskMapRemapOffset##i + _MaskMapRemapScale##i * lerp(0.5h, TerrainAntiRepeatSample(TEXTURE2D_ARGS(_Mask##i, sampler_Mask0), splat##i##uv), _LayerHasMask##i));
#else
    #define SampleLayerMasks(i) (_MaskMapRemapOffset##i + _MaskMapRemapScale##i * 0.5h);
#endif

half4 SampleMetallicSpecGloss(float2 uv, half albedoAlpha)
{
    half4 specGloss;
    specGloss = SAMPLE_TEXTURE2D(_MetallicTex, sampler_MetallicTex, uv);
    specGloss.a = albedoAlpha;
    return specGloss;
}

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    outSurfaceData = (SurfaceData)0;
    half4 albedoSmoothness = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
    outSurfaceData.alpha = 1;

    half4 specGloss = SampleMetallicSpecGloss(uv, albedoSmoothness.a);
    outSurfaceData.albedo = albedoSmoothness.rgb;

    outSurfaceData.metallic = specGloss.r;
    outSurfaceData.specular = half3(0.0h, 0.0h, 0.0h);

    outSurfaceData.smoothness = specGloss.a;
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap));
    outSurfaceData.occlusion = 1;
    outSurfaceData.emission = 0;
}

void TerrainInstancing(inout float4 positionOS, inout float3 normal, inout float2 uv)
{
#ifdef UNITY_INSTANCING_ENABLED
    float2 patchVertex = positionOS.xy;
    float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(Terrain, _TerrainPatchInstanceData);

    float2 sampleCoords = (patchVertex.xy + instanceData.xy) * instanceData.z; // (xy + float2(xBase,yBase)) * skipScale
    float height = UnpackHeightmap(_TerrainHeightmapTexture.Load(int3(sampleCoords, 0)));

    positionOS.xz = sampleCoords * _TerrainHeightmapScale.xz;
    positionOS.y = height * _TerrainHeightmapScale.y;

#ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
    normal = float3(0, 1, 0);
#else
    normal = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb * 2 - 1;
#endif
    uv = sampleCoords * _TerrainHeightmapRecipSize.zw;
#endif
}

void TerrainInstancing(inout float4 positionOS, inout float3 normal)
{
    float2 uv = { 0, 0 };
    TerrainInstancing(positionOS, normal, uv);
}

void TerrainInstancing(inout float4 positionOS)
{
    float3 normal = { 0, 0, 0 };
    TerrainInstancing(positionOS, normal);
}
#endif
