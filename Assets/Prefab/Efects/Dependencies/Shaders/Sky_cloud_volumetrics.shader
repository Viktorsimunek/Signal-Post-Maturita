Shader "Custom/VolumetricClouds"
{
    Properties
    {
        _NoiseTex ("3D Noise", 3D) = "" {}

        _NoiseScale ("Noise Scale", Float) = 2.0
        _CloudScale ("Cloud Scale (larger = bigger clouds)", Float) = 1.0
        [Toggle] _WorldSpace ("World Space (no stretching)", Float) = 1
        _Density ("Density", Range(0, 5)) = 1.5
        _Coverage ("Coverage", Range(0, 1)) = 0.45
        _Softness ("Edge Softness", Range(0.001, 0.5)) = 0.08

        _WindDirection ("Wind Direction", Vector) = (1, 0, 0.3, 0)
        _WindSpeed ("Wind Speed", Float) = 0.02

        _CloudColor ("Cloud Color", Color) = (0.8, 0.82, 0.85, 1)
        _ShadowColor ("Shadow Color", Color) = (0.18, 0.2, 0.22, 1)

        _Brightness ("Brightness", Range(0, 3)) = 1
        _Absorption ("Absorption", Range(0.1, 10)) = 2

        _HeightBottom ("Bottom Fade (fraction of height)", Range(0, 2)) = 0.1
        [Toggle] _BottomCutEnabled ("Enable Flat Bottom Cut", Float) = 0
        _BottomCutHeight ("Bottom Cut Height (0 = box bottom, 1 = top)", Range(0, 1)) = 0.1
        _BottomCutFade ("Bottom Cut Fade (fraction of height)", Range(0, 0.1)) = 0.01
        _HeightTop ("Top Fade (fraction of height)", Range(0, 2)) = 0.15
        _EdgeFade ("Edge Fade (fraction of width / depth)", Range(0, 1)) = 0.15
        [Enum(Both,0,Back Faces,1,Front Faces,2)] _Cull ("Render Faces", Float) = 1

        [Header(Atmosphere)]
        _DistanceFadeStart ("Distance Fade Start", Float) = 600
        _DistanceFadeEnd ("Distance Fade End (0 = off)", Float) = 0
        [Toggle] _UseCloudWeather ("Follow Time Of Day", Float) = 0
        _AtmosphereBlend ("Distant Haze", Range(0, 1)) = 0.65

        [Header(Cloud Lighting)]
        _HeightShading ("Darker Underside", Range(0, 1)) = 0
        _SilverLining ("Sunlit Edges", Range(0, 1)) = 0
        _BodyContrast ("Body Contrast", Range(0, 1)) = 0

        _Steps ("Raymarch Steps", Range(8, 128)) = 32
        [Enum(White Noise,0,Bayer 4x4,1)] _DitherMode ("Smoothing Dither", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend One OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull [_Cull]

        Pass
        {
            Name "CloudPass"

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE3D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)

                float _NoiseScale;
                float _CloudScale;
                float _WorldSpace;
                float _Cull;
                float _Density;
                float _Coverage;
                float _Softness;

                float4 _WindDirection;
                float _WindSpeed;

                float4 _CloudColor;
                float4 _ShadowColor;

                float _Brightness;
                float _Absorption;

                float _HeightBottom;
                float _BottomCutEnabled;
                float _BottomCutHeight;
                float _BottomCutFade;
                float _HeightTop;
                float _EdgeFade;

                float _DistanceFadeStart;
                float _DistanceFadeEnd;
                float _UseCloudWeather;
                float _AtmosphereBlend;
                float _HeightShading;
                float _SilverLining;
                float _BodyContrast;
                float _Steps;
                float _DitherMode;

            CBUFFER_END

            // Set by the player's lantern. W stores inverse squared glow radius.
            // X: enabled, Y: density multiplier, Z: coverage offset.
            float4 _CloudWeather;
            float4 _CloudHazeColor;
            float4 _LanternFogPosition;
            float4 _LanternFogColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };


            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs pos =
                    GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = pos.positionCS;
                // The cube is a raymarch proxy, not the visible cloud surface.
                // Keep its exit faces rasterized when they cross the far plane.
                // Preserve XY/W and world position so the viewing ray stays intact;
                // the fragment shader still clips integration to scene depth.
                if (output.positionCS.w > 0.0)
                {
                    #if UNITY_REVERSED_Z
                        output.positionCS.z = max(output.positionCS.z, 0.0);
                    #else
                        output.positionCS.z = min(output.positionCS.z, output.positionCS.w);
                    #endif
                }
                output.positionWS = pos.positionWS;

                return output;
            }


            // ---------------------------------------------------------
            // Ray / Box intersection
            // Cube local bounds: -0.5 ... +0.5
            // ---------------------------------------------------------

            bool RayBoxIntersection(
                float3 rayOrigin,
                float3 rayDirection,
                out float entryDistance,
                out float exitDistance
            )
            {
                float3 boxMin = float3(-0.5, -0.5, -0.5);
                float3 boxMax = float3( 0.5,  0.5,  0.5);
                // Spend raymarch samples only on the remaining cloud volume.
                if (_BottomCutEnabled > 0.5)
                    boxMin.y += saturate(_BottomCutHeight);

                float3 safeDir = rayDirection;

                safeDir.x =
                    abs(safeDir.x) < 0.00001
                    ? 0.00001
                    : safeDir.x;

                safeDir.y =
                    abs(safeDir.y) < 0.00001
                    ? 0.00001
                    : safeDir.y;

                safeDir.z =
                    abs(safeDir.z) < 0.00001
                    ? 0.00001
                    : safeDir.z;


                float3 invDir = 1.0 / safeDir;

                float3 t0 =
                    (boxMin - rayOrigin) * invDir;

                float3 t1 =
                    (boxMax - rayOrigin) * invDir;


                float3 tMin = min(t0, t1);
                float3 tMax = max(t0, t1);


                entryDistance =
                    max(
                        max(tMin.x, tMin.y),
                        tMin.z
                    );

                exitDistance =
                    min(
                        min(tMax.x, tMax.y),
                        tMax.z
                    );


                return exitDistance >= max(entryDistance, 0.0);
            }


            // ---------------------------------------------------------
            // Height profile
            // Makes clouds fade at bottom and top of volume
            // ---------------------------------------------------------

            float GetHeightMask(float height01)
            {
                // Zero disables a fade; widths above one soften the entire volume.
                float bottom =
                    _HeightBottom <= 0.0 ? 1.0 : smoothstep(
                        0.0,
                        max(_HeightBottom, 0.001),
                        height01
                    );

                // In flat-bottom mode replace the broad bottom fade with a
                // narrow transition above the cut plane. Zero gives a hard cut.
                if (_BottomCutEnabled > 0.5)
                {
                    float heightAboveCut = height01 - saturate(_BottomCutHeight);
                    bottom = heightAboveCut <= 0.0 ? 0.0
                        : (_BottomCutFade <= 0.0 ? 1.0
                        : smoothstep(0.0, max(_BottomCutFade, 0.000001), heightAboveCut));
                }

                float top =
                    _HeightTop <= 0.0 ? 1.0 : smoothstep(
                        0.0,
                        max(_HeightTop, 0.001),
                        1.0 - height01
                    );

                return bottom * top;
            }


            // ---------------------------------------------------------
            // Density sampling
            // ---------------------------------------------------------

            float SampleDensity(float3 positionOS)
            {
                // Cube local coordinates → 0...1
                float3 uvw = positionOS + 0.5;


                // Moving 3D noise
                float3 wind =
                    _WindDirection.xyz *
                    _Time.y *
                    _WindSpeed;


                float3 noisePosition = _WorldSpace > 0.5
                    ? TransformObjectToWorld(positionOS) : uvw;
                float3 noiseUV =
                    noisePosition * max(_NoiseScale, 0.0001) / max(_CloudScale, 0.0001) +
                    wind;


                float noise =
                    SAMPLE_TEXTURE3D(
                        _NoiseTex,
                        sampler_NoiseTex,
                        noiseUV
                    ).r;


                // Weather / cloud coverage
                float density =
                    smoothstep(
                        _Coverage + _CloudWeather.z * _UseCloudWeather,
                        _Coverage + _CloudWeather.z * _UseCloudWeather + _Softness,
                        noise
                    );


                // Flatten / shape vertically
                density *= GetHeightMask(uvw.y);
                if (_EdgeFade > 0.0)
                {
                    float2 edgeDistance = max(0.0, 0.5 - abs(positionOS.xz));
                    float2 edgeMask = smoothstep(0.0, max(_EdgeFade, 0.0001), edgeDistance);
                    density *= edgeMask.x * edgeMask.y;
                }


                float weatherDensity = lerp(1.0, _CloudWeather.y,
                    saturate(_CloudWeather.x * _UseCloudWeather));
                return density * _Density * weatherDensity;
            }


            // White-noise hash, stable per screen pixel (no animated flicker).
            float GetRandomValue(float2 coord)
            {
                uint2 bits = asuint(coord);
                // Keep the seed arithmetic integer instead of a float dot product.
                uint rng = bits.x * 19349669u + bits.y * 83492837u;
                rng = rng * 747796405u + 2891336453u;
                uint result = ((rng >> ((rng >> 28u) + 4u)) ^ rng) * 277803737u;
                result = (result >> 22u) ^ result;
                // Use 24 bits so conversion to float stays strictly below one.
                return (result >> 8u) * (1.0 / 16777216.0);
            }


            // Ordered 4x4 Bayer pattern, evaluated without texture lookups.
            float GetBayerValue(float2 coord)
            {
                uint2 pixel = (uint2)coord & uint2(3u, 3u);
                uint xorXY = pixel.x ^ pixel.y;
                uint lowBits = ((xorXY & 1u) << 1u) | (pixel.y & 1u);
                uint highBits = (((xorXY >> 1u) & 1u) << 1u) | ((pixel.y >> 1u) & 1u);
                return (float(lowBits * 4u + highBits) + 0.5) / 16.0;
            }


            // ---------------------------------------------------------
            // Main fragment
            // ---------------------------------------------------------

            half4 frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                // In Both mode march once per pixel, using only the exit face.
                if (_Cull < 0.5 && IS_FRONT_VFACE(face, true, false))
                    discard;
                float3 cameraWS =
                    GetCameraPositionWS();


                float3 rayDirectionWS =
                    normalize(
                        input.positionWS - cameraWS
                    );


                // Convert ray into cube local space.
                //
                // IMPORTANT:
                // Do NOT normalize rayDirectionOS.
                // Keeping its scale makes t still correspond to
                // distance along the world-space ray.
                float3 cameraOS =
                    TransformWorldToObject(cameraWS);

                float3 rayDirectionOS =
                    mul(
                        (float3x3)unity_WorldToObject,
                        rayDirectionWS
                    );


                float entryDistance;
                float exitDistance;


                if (!RayBoxIntersection(
                    cameraOS,
                    rayDirectionOS,
                    entryDistance,
                    exitDistance
                ))
                {
                    discard;
                }


                entryDistance =
                    max(entryDistance, 0.0);

                // Clip the volume to opaque geometry, including in back-face mode.
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);
                #if !UNITY_REVERSED_Z
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif
                float3 sceneWS = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                float sceneDistance = dot(sceneWS - cameraWS, rayDirectionWS);
                exitDistance = min(exitDistance, sceneDistance);


                float distanceInside =
                    exitDistance - entryDistance;


                if (distanceInside <= 0.0)
                    discard;


                int steps =
                    clamp((int)_Steps, 8, 128);


                float stepLength =
                    distanceInside / steps;


                // Offset the sampling lattice independently for each pixel.
                // This breaks coherent raymarch bands using the selected pattern,
                // keeping one sample per interval and the same integration weight.
                float2 pixelCoord = floor(input.positionCS.xy);
                float jitter = _DitherMode > 0.5
                    ? GetBayerValue(pixelCoord) : GetRandomValue(pixelCoord);
                float currentDistance =
                    entryDistance +
                    stepLength * jitter;


                // Fade before the camera clips the march, even with a shorter far plane.
                float viewDepthPerMeter = max(0.0001,
                    -TransformWorldToView(cameraWS + rayDirectionWS).z);
                float cameraLimit = _ProjectionParams.z / viewDepthPerMeter;
                float fadeEnd = min(_DistanceFadeEnd, cameraLimit * 0.98);
                float fadeStart = min(_DistanceFadeStart, fadeEnd * 0.65);
                fadeStart = max(0.0, fadeStart);
                fadeEnd = max(fadeStart + 0.01, fadeEnd);

                float accumulatedDistance = 0.0;
                float accumulatedAlpha = 0.0;
                float accumulatedLantern = 0.0;
                float3 lanternToCamera = cameraWS - _LanternFogPosition.xyz;

                float3 accumulatedColor =
                    float3(0, 0, 0);


                [loop]
                for (int i = 0; i < 128; i++)
                {
                    if (i >= steps)
                        break;


                    float3 samplePositionOS =
                        cameraOS +
                        rayDirectionOS *
                        currentDistance;


                    float density =
                        SampleDensity(samplePositionOS);
                    if (_DistanceFadeEnd > 0.0)
                        density *= 1.0 - smoothstep(fadeStart, fadeEnd, currentDistance);


                    if (density > 0.001)
                    {
                        // Beer-Lambert style absorption
                        float sampleAlpha =
                            1.0 -
                            exp(
                                -density *
                                stepLength *
                                _Absorption
                            );


                        // Cheap internal shading: denser areas become darker.
                        // Scene illumination is applied once after integration.
                        float lightAmount =
                            saturate(
                                1.0 -
                                density * 0.35
                            );


                        float3 sampleColor =
                            lerp(
                                _ShadowColor.rgb,
                                _CloudColor.rgb,
                                lightAmount
                            );

                        // Front-to-back compositing
                        float contribution =
                            sampleAlpha *
                            (1.0 - accumulatedAlpha);

                        // Reuse density and transmittance from the existing march.
                        // Only distance arithmetic: no noise or shadow samples.
                        if (_LanternFogColor.a > 0.0)
                        {
                            float3 fromLantern = lanternToCamera + rayDirectionWS * currentDistance;
                            float falloff = saturate(1.0 - dot(fromLantern, fromLantern)
                                * _LanternFogPosition.w);
                            accumulatedLantern += falloff * falloff * contribution;
                        }


                        accumulatedDistance += currentDistance * contribution;
                        accumulatedColor +=
                            sampleColor *
                            contribution;


                        accumulatedAlpha +=
                            contribution;


                        // Early exit
                        if (accumulatedAlpha > 0.98)
                            break;
                    }


                    currentDistance += stepLength;
                }


                if (accumulatedAlpha < 0.001)
                    discard;

                // Evaluate scene lighting once per visible pixel, not per march step.
                // The time-of-day controller already drives the sun and ambient light.
                // No extra noise samples, shadow lookups or secondary light marches.
                Light mainLight = GetMainLight();
                float3 ambientLight = SampleSH(float3(0.0, 1.0, 0.0));
                // Mild forward scattering brightens clouds when looking toward the sun.
                float towardsLight = saturate(dot(rayDirectionWS, mainLight.direction));
                float scattering = 0.75 + 0.5 * towardsLight * towardsLight;
                float3 illumination = max(ambientLight, 0.0)
                    + mainLight.color * mainLight.distanceAttenuation * scattering;
                accumulatedColor *= illumination * _Brightness;

                // Reuse the march result. All shaping happens once per pixel.
                float averageDistance = accumulatedDistance / max(accumulatedAlpha, 0.001);
                float averageHeight = saturate(cameraOS.y + rayDirectionOS.y * averageDistance + 0.5);
                float underside = (1.0 - averageHeight) * (1.0 - averageHeight);
                float body = accumulatedAlpha * accumulatedAlpha;
                accumulatedColor *= (1.0 - underside * _HeightShading)
                    * (1.0 - body * _BodyContrast * 0.35);

                // A soft rim facing the sun, strongest in partially transparent edges.
                float rim = accumulatedAlpha * (1.0 - accumulatedAlpha) * 4.0;
                float sunGlow = towardsLight * towardsLight;
                sunGlow *= sunGlow;
                accumulatedColor += _CloudColor.rgb * mainLight.color
                    * mainLight.distanceAttenuation * _Brightness
                    * rim * sunGlow * _SilverLining;

                // Blend distant clouds into the same atmosphere as the horizon.
                if (_DistanceFadeEnd > 0.0 && _UseCloudWeather > 0.5 && _CloudWeather.x > 0.5)
                {
                    float haze = smoothstep(0.0, fadeEnd, averageDistance) * _AtmosphereBlend;
                    accumulatedColor = lerp(accumulatedColor,
                        _CloudHazeColor.rgb * accumulatedAlpha, haze);
                }
                // Local light remains visible even when the night ambient is dark.
                accumulatedColor += _LanternFogColor.rgb * accumulatedLantern;


                return half4(
                    accumulatedColor,
                    accumulatedAlpha
                );
            }

            ENDHLSL
        }
    }
}
