Shader "SignalPost/CCTVSignal"
{
    Properties
    {
        [MainTexture] _BaseMap ("Camera Feed", 2D) = "black" {}
        _Damage ("Damage - clean / interference / signal lost", Range(0,1)) = 0.14
        _Brightness ("Screen Brightness", Range(0,3)) = 1.1
        [HDR] _EmissionColor ("Emission Color", Color) = (1,1,1,1)
        _EmissionIntensity ("Emission Intensity", Range(0,10)) = 0
        _Contrast ("Contrast", Range(0.5,2)) = 1.12
        _BlackLevel ("Black Level", Range(0,0.1)) = 0.012
        _Saturation ("Saturation", Range(0,1)) = 0.03
        _ScanlineStrength ("Scanlines", Range(0,0.5)) = 0.16
        _ScanlineDensity ("Scanline Count", Range(60,500)) = 240
        _NoiseStrength ("CRT Snow", Range(0,0.3)) = 0.045
        _Curvature ("Glass Curvature", Range(0,0.1)) = 0.018
        _Vignette ("Edge Darkening", Range(0,0.6)) = 0.18
        _Seed ("Channel Seed", Float) = 1
        [HideInInspector] _ScreenPower ("Power", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "CCTV"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ SIGNALPOST_CRT_VERY_LOW SIGNALPOST_CRT_LOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            float4 _BaseMap_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _EmissionColor;
                half _EmissionIntensity;
                float _Seed;
                half _Damage, _Brightness, _Contrast, _BlackLevel, _Saturation;
                half _ScanlineStrength, _ScanlineDensity, _NoiseStrength;
                half _Curvature, _Vignette, _ScreenPower;
            CBUFFER_END
            // Float hash avoids half precision overflow and needs no noise texture or sine.
            float Hash(float2 p)
            {
                float3 q = frac(float3(p.xyx) * 0.1031);
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 signal:TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                float tick = floor(_Time.y * 9.0);
                float burst = Hash(float2(tick, _Seed));
                float probability = smoothstep(0.18, 1.0, _Damage) * 0.82;
                o.signal.x = max(step(1.0 - probability, burst), smoothstep(0.9, 1.0, _Damage));
                o.signal.y = (Hash(float2(tick + 31.0, _Seed)) - 0.5) * _Damage;
                // Uniform per-screen bursts, evaluated only at vertices.
                // Mapping glitches start before complete signal dropouts.
                float mappingProbability = lerp(0.05, 0.75, _Damage);
                o.signal.z = step(1.0 - mappingProbability, frac(burst * 7.13)) * _Damage * _Damage;
                o.signal.w = o.signal.y * o.signal.z;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 uv = i.uv;
                float2 centered = uv * 2.0 - 1.0;
                half edge = saturate(1.0 - _Vignette * dot(centered, centered));
                half loss = i.signal.x;
                half band = 0;
                #if !defined(SIGNALPOST_CRT_VERY_LOW)
                    uv += centered * dot(centered, centered) * (_Curvature * 0.5);
                    float phase = frac(uv.y - _Time.y * 0.23 + _Seed * 0.17);
                    band = saturate(1.0 - abs(phase - 0.5) * 30.0) * _Damage;
                #endif
                // Distort only feed sampling: CRT raster and glass stay on the screen.
                float2 feedUV = uv;
                #if defined(SIGNALPOST_CRT_VERY_LOW)
                    // Tiny whole-frame kick, no per-pixel random work.
                    feedUV.x += i.signal.w * 0.025;
                #elif defined(SIGNALPOST_CRT_LOW)
                    half split = step(0.5, frac(uv.y * 7.0 + _Seed));
                    feedUV.x += (split - 0.5) * i.signal.w * 0.18;
                    feedUV.y += i.signal.w * 0.12;
                #else
                    float row = floor(uv.y * lerp(6.0, 22.0, _Damage));
                    float jitter = Hash(float2(row + _Seed, floor(_Time.y * 18.0))) - 0.5;
                    feedUV.x += jitter * i.signal.z * 0.5 + band * i.signal.y * 0.45;
                    feedUV.x += (uv.y - 0.5) * i.signal.w * 0.15;
                    feedUV.y += i.signal.w * 0.6 + band * i.signal.z * 0.06;
                #endif
                // Repeat the displaced feed instead of stretching border texels.
                feedUV = frac(feedUV + 1.0);
                half3 source = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, feedUV).rgb;
                half grey = dot(source, half3(0.299, 0.587, 0.114));
                half3 screen = saturate((lerp(grey.xxx, source, _Saturation) - 0.5) * _Contrast + 0.5);
                screen = lerp(_BlackLevel.xxx, 1.0, screen);
                // Fade scanlines at distances where the raster cannot resolve them.
                float raster = uv.y * _ScanlineDensity;
                half resolved = saturate(1.0 - fwidth(raster));
                half scan = 1.0 - _ScanlineStrength * resolved * step(0.5, frac(raster));
                #if defined(SIGNALPOST_CRT_VERY_LOW)
                    screen *= (1.0 - loss * 0.96);
                #elif defined(SIGNALPOST_CRT_LOW)
                    screen *= (1.0 - loss * 0.96) * (1.0 - band * 0.6);
                #else
                    // Analog interference is correlated along each raster line:
                    // broken horizontal streaks, slightly wavy, with moving tracking bars.
                    float frame = floor(_Time.y * 20.0);
                    float wave = abs(frac(uv.x * 21.0 + _Time.y * 0.7) - 0.5) * 2.0;
                    float lineCoord = uv.y * 220.0 + wave * 0.32;
                    float rasterRow = floor(lineCoord);
                    float rowNoise = Hash(float2(rasterRow + _Seed * 19.0, frame));
                    float across = uv.x * lerp(24.0, 88.0, rowNoise) + rowNoise * 91.0 + frame * 2.7;
                    float cell = floor(across);
                    float blend = frac(across);
                    blend = blend * blend * (3.0 - 2.0 * blend);
                    float a = Hash(float2(cell + frame * 7.0, rasterRow + _Seed * 43.0));
                    float b = Hash(float2(cell + 1.0 + frame * 7.0, rasterRow + _Seed * 43.0));
                    float streak = smoothstep(0.42, 0.72, lerp(a,b,blend));
                    float width = max(fwidth(lineCoord), 0.08);
                    float ridge = 1.0 - smoothstep(0.22, 0.22 + width, abs(frac(lineCoord) - 0.5));
                    half snow = saturate(streak * ridge * (0.55 + rowNoise * 0.65));
                    half noise = _NoiseStrength + _Damage * 0.16 + band * 0.2;
                    screen = screen * (1.0 - noise * streak * 0.6) + snow * noise;
                    half tracking = saturate(1.0 - abs(frac(uv.y + uv.x * 0.045 - _Time.y * 0.19) - 0.5) * 65.0);
                    half3 lostSignal = lerp(snow.xxx * 0.85, half3(0.65,0.65,0.65), tracking * 0.8);
                    screen = lerp(screen, lostSignal, loss);
                    screen *= 1.0 - band * 0.35;
                #endif
                half inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
                half3 outputColor = max(screen, 0.0) * scan * edge * _Brightness * _ScreenPower * inside;
                outputColor += outputColor * _EmissionColor.rgb * _EmissionIntensity;
                return half4(outputColor, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

