Shader "PSX/Skybox/TimeRampNoiseSun"
{
    Properties
    {
        // ====== POČASÍ (využívá původní clouds) ======
        _Storm ("Storm Amount", Range(0,1)) = 0

        _StormCloudTex ("Storm Detail Texture (tileable)", 2D) = "gray" {}
        _StormCloudScale ("Storm Detail Scale", Range(0.1, 12)) = 2.5
        _StormCloudSpeed ("Storm Detail Speed", Range(0, 1)) = 0.12
        _StormCloudDetail ("Storm Detail Amount", Range(0, 2)) = 1.0
        _StormCloudContrast ("Storm Contrast", Range(0.5, 8)) = 3.2
        _StormCloudGamma ("Storm Gamma", Range(0.2, 2)) = 0.85

        _StormUsesBaseClouds ("Storm Uses Base Clouds", Range(0,1)) = 0.0
        _StormSkyThrough ("Sky Through Storm", Range(0,1)) = 0.25
        _StormSideSpread ("Storm Side Spread", Range(0,1)) = 1.0
        _StormHorizonPower ("Storm Horizon Power", Range(0.5, 8)) = 1.6

        _StormDayDesaturate ("Storm Day Desaturate", Range(0,1)) = 0.7
        _StormDayToNight ("Storm Day→Night Blend", Range(0,1)) = 0.45
        _StormDayTint ("Storm Day Tint", Color) = (0.22, 0.24, 0.28, 1)

        _StormDarkenSky ("Storm Darken Sky", Range(0,1)) = 1.0
        _StormCloudColor ("Storm Cloud Color", Color) = (0.01, 0.01, 0.015, 1)
        _StormSunDim ("Storm Sun Dim", Range(0,1)) = 1.0

        _StormHardness ("Storm Hardness (max=black wall)", Range(0,3)) = 2.8
        _StormCloudBoost ("Storm Cloud Boost", Range(0,8)) = 8.0
        _StormSkyThroughMin ("Storm Sky Through @ Max", Range(0,1)) = 0.0
        _StormDayExtraDark ("Storm Extra Dark in Day", Range(0,2)) = 2.0

        // ====== BLESK ======
        _Lightning ("Lightning Amount", Range(0,1)) = 0
        _LightningTex ("Lightning Texture (bolt, grayscale)", 2D) = "white" {}
        _LightningScale ("Lightning Scale", Range(0.2, 12)) = 3.6
        _LightningIntensity ("Lightning Intensity", Range(0, 8)) = 6.0
        _LightningSoftness ("Lightning Softness", Range(0.01, 2)) = 0.35
        _LightningFreq ("Lightning Frequency", Range(0.1, 4)) = 0.9
        _LightningDuration ("Lightning Duration", Range(0.05, 1.2)) = 0.28
        _LightningDouble ("Lightning Double Flash", Range(0,1)) = 0.75
        _LightningTint ("Lightning Tint", Color) = (1.0, 1.02, 1.10, 1)
        _LightningDayBoost ("Lightning Day Boost", Range(0,3)) = 1.6

        // NOVÉ: realističtější flash + rozbití kruhu
        _LightningSkyFlash ("Lightning Sky Flash", Range(0,2)) = 0.45
        _LightningEdgeNoise ("Lightning Patch Edge Noise", Range(0,1)) = 0.18
        _LightningEdgeNoiseScale ("Lightning Edge Noise Scale", Range(0.5,20)) = 6.0

        // ČAS
        _Time01     ("Time of Day 0..1", Range(0,1)) = 0.4
        _Twilight   ("Twilight Width (0..0.25)", Range(0,0.25)) = 0.18

        // BARVY
        _ZenithDay    ("Zenith Day", Color)   = (0.25,0.40,0.70,1)
        _HorizonDay   ("Horizon Day", Color)  = (0.70,0.75,0.80,1)
        _ZenithNight  ("Zenith Night", Color) = (0.02,0.06,0.12,1)
        _HorizonNight ("Horizon Night", Color)= (0.06,0.08,0.12,1)

        // PS1 look
        _ColorSteps ("Color Steps", Range(2,512)) = 24
        _DitherAmt  ("Dither Amount", Range(0,1)) = 0.50
        _DitherSize ("Dither Pixel Size", Range(1,8)) = 1

        _NoiseScale ("Noise Scale", Range(0.1,10)) = 2.0
        _NoiseInt   ("Noise Intensity", Range(0,0.5)) = 0.04

        // Hvězdy + měsíc
        _StarsTex     ("Stars (tileable, grayscale)", 2D) = "black" {}
        _StarsInt     ("Stars Intensity", Range(0,2)) = 0.6
        _StarsScale   ("Stars Scale", Range(0.05,4)) = 0.25

        _MoonTex      ("Moon Texture (single)", 2D) = "white" {}
        _MoonColor    ("Moon Tint", Color) = (1,1,1,1)
        _MoonSizeDeg  ("Moon Size (deg)", Range(0.1,2.0)) = 0.8
        _MoonAltDeg   ("Moon Altitude (deg)", Range(-5,85)) = 25
        _MoonAzimDeg  ("Moon Azimuth (deg)", Range(0,360)) = 45
        _MoonNightMul ("Moon visible only at night", Range(0,1)) = 1

        // MRAKY (původní)
        _CloudMap ("Cloud map", 2D) = "white" {}
        _CloudScale ("Cloud scale", float) = 2
        _CloudSpeed ("Cloud speed", Range(0,1)) = 0.1
        _CloudGamma ("Cloud gamma", Range(0.001,2)) = 1.
        _CloudOpacity ("Cloud opacity", Range(0,1)) = 0.5
        _CloudCol ("Cloud col", Color) = (0.8,0.85,0.9,1)

        // SLUNCE (TEĎ TEXTURA)
        _SunTex         ("Sun/Moon Texture (single)", 2D) = "white" {}
        _SunColor       ("Sun Color", Color) = (1,0.96,0.85,1)
        _SunSizeDeg     ("Sun Disc (deg)", Range(0.1,2.0)) = 0.6
        _SunSizeMul     ("Sun Size Multiplier", Range(1,50)) = 12   // <-- NOVÉ (velké zvětšení)
        _SunGlowDeg     ("Sun Glow (deg)", Range(1,30))    = 9
        _SunGlowInt     ("Sun Glow Intensity", Range(0,5)) = 1.2
        _SunMaxAltDeg   ("Sun Max Alt @ noon", Range(5,85))= 55
        _SunAzimOffset  ("Sun Azimuth Offset", Range(0,360))= 90
        _SunHaze        ("Sun Haze Strength", Range(0,2))  = 0.25
        _SunHazeWarmth  ("Sun Haze Warmth", Color) = (1.08,0.85,0.7,1)
        _SunDayOnly     ("Sun visible only by day", Range(0,1)) = 1

        _FogSkyAmount ("Fog→Sky Amount", Range(0,1)) = 0.35
        _FogSkyPower  ("Fog→Sky Power",  Range(0,6)) = 2.0
        _FogSkyBias   ("Fog→Sky Vertical Bias", Range(-1,0.25)) = 0.0
    }

    SubShader
    {
        Tags{ "Queue"="Background" "RenderType"="Background" "RenderPipeline"="UniversalPipeline" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Lighting Off
        Fog{ Mode Off }

        Pass
        {
            Name "Skybox"
            Tags { "LightMode"="SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _Storm;
            float _StormCloudScale, _StormCloudSpeed, _StormCloudDetail, _StormCloudContrast, _StormCloudGamma;
            float _StormUsesBaseClouds, _StormSkyThrough, _StormSideSpread, _StormHorizonPower, _StormDarkenSky;
            float _StormDayDesaturate, _StormDayToNight, _StormSunDim;
            float4 _StormDayTint, _StormCloudColor;
            float _StormHardness, _StormCloudBoost, _StormSkyThroughMin, _StormDayExtraDark;

            float _Lightning;
            float _LightningScale, _LightningIntensity, _LightningSoftness, _LightningFreq, _LightningDuration, _LightningDouble;
            float4 _LightningTint;
            float _LightningDayBoost;

            float _LightningSkyFlash, _LightningEdgeNoise, _LightningEdgeNoiseScale;

            float  _Time01, _Twilight;
            float4 _ZenithDay, _HorizonDay, _ZenithNight, _HorizonNight;
            float  _ColorSteps, _DitherAmt, _DitherSize;
            float  _NoiseScale, _NoiseInt;

            float _CloudScale, _CloudSpeed, _CloudGamma, _CloudOpacity; float4 _CloudCol;
            float _StarsInt, _StarsScale;

            float4 _MoonColor;
            float  _MoonSizeDeg, _MoonAltDeg, _MoonAzimDeg, _MoonNightMul;

            float4 _SunColor, _SunHazeWarmth;
            float  _SunSizeDeg, _SunSizeMul, _SunGlowDeg, _SunGlowInt, _SunMaxAltDeg, _SunAzimOffset, _SunHaze, _SunDayOnly;

            float _FogSkyAmount, _FogSkyPower, _FogSkyBias;

            TEXTURE2D(_StormCloudTex); SAMPLER(sampler_StormCloudTex);
            TEXTURE2D(_LightningTex); SAMPLER(sampler_LightningTex);
            TEXTURE2D(_CloudMap); SAMPLER(sampler_CloudMap);
            TEXTURE2D(_StarsTex); SAMPLER(sampler_StarsTex);
            TEXTURE2D(_MoonTex); SAMPLER(sampler_MoonTex);
            TEXTURE2D(_SunTex); SAMPLER(sampler_SunTex);

            struct appdata { float4 vertex : POSITION; };
            struct v2f {
                float4 pos    : SV_POSITION;
                float4 scrPos : TEXCOORD0;
                float3 dir    : TEXCOORD1;
            };

            inline float bayer4_int(int2 ip){
                const float M[16] = {0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5};
                return (M[(ip.y & 3)*4 + (ip.x & 3)] + 0.5) / 16.0;
            }

            inline int2 pixelCoord(float4 scrPos, float ditherSize)
            {
                float2 uv01 = scrPos.xy / max(1e-6, scrPos.w);
                float2 px   = uv01 * _ScreenParams.xy;
                px = floor(px);
                ditherSize = max(1.0, ditherSize);
                px = floor(px / ditherSize);
                return int2(px);
            }

            inline float4 quantize_psx(float3 rgb, float4 scrPos)
            {
                int2 ip = pixelCoord(scrPos, _DitherSize);
                float d     = (bayer4_int(ip) - 0.5) * _DitherAmt;
                float steps = max(2.0, _ColorSteps);

                float3 t = saturate(rgb + d);
                t = floor(t * (steps - 1.0)) / (steps - 1.0);

                return float4(t, 1);
            }

            inline float hash21(float2 p){ return frac(sin(dot(p,float2(27.619,57.583)))*43758.5453); }
            inline float noise2(float2 p){
                float2 i = floor(p), f = frac(p);
                float a = hash21(i);
                float b = hash21(i+float2(1,0));
                float c = hash21(i+float2(0,1));
                float d = hash21(i+float2(1,1));
                float2 u = f*f*(3-2*f);
                return lerp(lerp(a,b,u.x), lerp(c,d,u.x), u.y);
            }

            inline float easeInOut(float x){
                x = saturate(x);
                return x*x*(3.0 - 2.0*x);
            }

            v2f vert(appdata v){
                v2f o;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(v.vertex.xyz);
                o.pos    = positionInputs.positionCS;
                o.scrPos = ComputeScreenPos(o.pos);
                o.dir    = normalize(v.vertex.xyz);
                return o;
            }

            inline void sunDirFromTime(float t01, out float3 sdir, out float sunAltDeg, out float sunAzDeg)
            {
                float ang = (t01 - 0.25) * 6.2831853;
                sunAltDeg = sin(ang) * _SunMaxAltDeg;
                sunAzDeg  = _SunAzimOffset + t01 * 360.0;
                float alt = radians(sunAltDeg);
                float az  = radians(sunAzDeg);
                sdir = normalize(float3(cos(alt)*sin(az), sin(alt), cos(alt)*cos(az)));
            }

            inline float dayWeightFromAlt(float altDeg, float twilightFrac, float maxAltDeg)
            {
                float twilightDeg = max(1.0, maxAltDeg * (twilightFrac*2.0));
                return saturate( smoothstep(-twilightDeg, twilightDeg, altDeg) );
            }

            inline void orthoBasis(float3 n, out float3 u, out float3 v){
                float3 up = abs(n.y) > 0.99 ? float3(1,0,0) : float3(0,1,0);
                u = normalize(cross(up, n));
                v = cross(n, u);
            }

            inline float TriplanarR(TEXTURE2D_PARAM(tex, texSampler), float3 n, float scale, float2 flow)
            {
                float3 an = abs(n);
                float3 w = pow(an, 4.0);
                w /= max(1e-5, (w.x + w.y + w.z));

                float2 uvX = n.yz * scale + flow;
                float2 uvY = n.xz * scale + flow;
                float2 uvZ = n.xy * scale + flow;

                float sx = SAMPLE_TEXTURE2D(tex, texSampler, uvX).r;
                float sy = SAMPLE_TEXTURE2D(tex, texSampler, uvY).r;
                float sz = SAMPLE_TEXTURE2D(tex, texSampler, uvZ).r;

                return sx*w.x + sy*w.y + sz*w.z;
            }

            inline float SampleBaseCloudForStorm(float3 dir)
            {
                float2 uvA = dir.xz * _CloudScale + _Time.x * float2(_CloudSpeed, 0);
                float2 uvB = dir.xz * _CloudScale + _Time.x * float2(0, _CloudSpeed);

                float a = SAMPLE_TEXTURE2D(_CloudMap, sampler_CloudMap, uvA).r;
                float b = SAMPLE_TEXTURE2D(_CloudMap, sampler_CloudMap, uvB).r;

                float m = a * b;

                float y01 = saturate(dir.y * 0.5 + 0.5);
                m *= lerp(0.9, 1.0, y01);

                m = pow(saturate(m), _CloudGamma);
                return saturate(m * _CloudOpacity);
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float  v   = saturate(dir.y * 0.5 + 0.5);

                float3 sdir; float sunAltDeg, sunAzDeg;
                sunDirFromTime(_Time01, sdir, sunAltDeg, sunAzDeg);

                float  wDay        = dayWeightFromAlt(sunAltDeg, _Twilight, _SunMaxAltDeg);
                float  nightFactor = 1.0 - wDay;

                float nn = noise2(dir.xz * _NoiseScale + v*5.0) * 2.0 - 1.0;
                v = saturate(v + nn * _NoiseInt);

                float3 dayCol   = lerp(_HorizonDay.rgb,   _ZenithDay.rgb,   v);
                float3 nightCol = lerp(_HorizonNight.rgb, _ZenithNight.rgb, v);

                float stormW = saturate(_Storm);

                float dayLum = dot(dayCol, float3(0.2126, 0.7152, 0.0722));
                float3 dayDesat  = lerp(dayCol, dayLum.xxx, _StormDayDesaturate * stormW);
                float3 dayTinted = lerp(dayDesat, dayDesat * _StormDayTint.rgb, stormW);
                float3 dayStorm  = lerp(dayTinted, nightCol, stormW * _StormDayToNight);

                float3 skyCol = lerp(nightCol, dayStorm, wDay);

                skyCol *= lerp(1.0, 0.18, stormW * wDay);

                float starTex = TriplanarR(TEXTURE2D_ARGS(_StarsTex, sampler_StarsTex), dir, _StarsScale, 0.0.xx);
                float stars = starTex * _StarsInt * nightFactor * pow(saturate(v), 2.0);
                stars *= (1.0 - saturate(stormW * 1.2));
                skyCol += stars;

                // ==========================================================
                // SUN (TEXTURE BILLBOARD) — s velkým multiplikátorem velikosti
                // ==========================================================
                float sdot = dot(dir, sdir);

                float sizeRad = radians(_SunSizeDeg * _SunSizeMul); // <-- HLAVNÍ FIX
                float glowRad = radians(_SunGlowDeg);

                float twilightDeg = max(1.0, _SunMaxAltDeg * (_Twilight*2.0));
                float sunAbove = (1.0 - _SunDayOnly) + _SunDayOnly * smoothstep(-twilightDeg*0.6, 0.0, sunAltDeg);

                float sunGeo = smoothstep(cos(sizeRad), cos(sizeRad*0.6), sdot) * sunAbove;

                float3 uSun, vSun; orthoBasis(sdir, uSun, vSun);
                float2 locS = float2(dot(dir, uSun), dot(dir, vSun));
                float  rS   = sin(sizeRad);
                float2 suv  = saturate(0.5 + (locS / max(1e-5, rS)) * 0.5);

                float4 sunTex = SAMPLE_TEXTURE2D(_SunTex, sampler_SunTex, suv);

                float sunDetailMask = max(sunTex.a, dot(sunTex.rgb, float3(0.2126, 0.7152, 0.0722)));
                float sunDisc = sunGeo * sunDetailMask;

                float sunGlow = saturate( smoothstep(cos(glowRad), cos(glowRad*0.25), sdot) - sunGeo ) * _SunGlowInt * sunAbove;

                float forward  = pow(saturate(sdot), 12.0);
                float horizonF = (1.0 - v);
                float haze     = _SunHaze * (forward * (0.4 + 0.6*horizonF));

                float sunMul = lerp(1.0, 1.0 - _StormSunDim, stormW);

                skyCol += (_SunHazeWarmth.rgb * haze) * sunMul;
                skyCol += (sunTex.rgb * _SunColor.rgb) * sunDisc * sunMul;
                skyCol += _SunColor.rgb * sunGlow * sunMul;
                // ==========================================================

                // měsíc (původní)
                float altM = radians(_MoonAltDeg);
                float azM  = radians(_MoonAzimDeg);
                float3 mdir = normalize(float3(cos(altM)*sin(azM), sin(altM), cos(altM)*cos(azM)));
                float  mdot = dot(dir, mdir);
                float  mSize = max(0.0001, radians(_MoonSizeDeg));
                float  moonMask = smoothstep(cos(mSize), cos(mSize*0.6), mdot) * lerp(1.0, nightFactor, _MoonNightMul);

                float3 uMoon,vMoon; orthoBasis(mdir,uMoon,vMoon);
                float2 loc = float2(dot(dir,uMoon), dot(dir,vMoon));
                float  r   = sin(mSize);
                float2 muv = saturate(0.5 + (loc / max(1e-5,r)) * 0.5);
                float3 moonTex = SAMPLE_TEXTURE2D(_MoonTex, sampler_MoonTex, muv).rgb * _MoonColor.rgb;
                skyCol = lerp(skyCol, skyCol + moonTex * lerp(1.0, 0.55, stormW), moonMask);

                // ==========================================================
                // CLOUDS (PŮVODNÍ BLOK – NESAHÁNO)
                // ==========================================================
                float3 cloudMap = SAMPLE_TEXTURE2D(_CloudMap, sampler_CloudMap, dir.xz * _CloudScale + _Time.x * float2(_CloudSpeed,0)).rgb;
                cloudMap *= SAMPLE_TEXTURE2D(_CloudMap, sampler_CloudMap, dir.xz * _CloudScale + _Time.x * float2(0,_CloudSpeed)).rgb;
                cloudMap *= pow(saturate(dir.y / 2), 2);
                cloudMap = pow(saturate(cloudMap), _CloudGamma) * saturate(skyCol.g * 20);

                skyCol = lerp(skyCol, _CloudCol.rgb, cloudMap * _CloudOpacity);
                // ==========================================================

                // ====== STORM mask (triplanar!) ======
                float baseCloudStorm = SampleBaseCloudForStorm(dir);

                float baseCoverage = baseCloudStorm;
                baseCoverage = pow(saturate(baseCoverage), lerp(1.0, 0.35, stormW));
                baseCoverage = saturate(baseCoverage * lerp(1.0, _StormCloudBoost, stormW));

                float2 flow = _Time.x * float2(_StormCloudSpeed, _StormCloudSpeed * 0.73);
                float st = TriplanarR(TEXTURE2D_ARGS(_StormCloudTex, sampler_StormCloudTex), dir, _StormCloudScale, flow);
                st = saturate((st - 0.5) * _StormCloudDetail + 0.5);
                st = pow(st, max(0.001, _StormCloudGamma));
                st = saturate((st - 0.5) * _StormCloudContrast + 0.5);

                float yy = saturate(dir.y * 0.5 + 0.5);
                float horizonMask = pow(saturate(1.0 - yy), _StormHorizonPower);
                float sideW = lerp(0.0, horizonMask, saturate(_StormSideSpread));

                float boundToCloud = lerp(1.0, baseCloudStorm, saturate(_StormUsesBaseClouds));

                float stormMask = saturate(st);
                stormMask = saturate(lerp(stormMask, 1.0, baseCoverage));
                stormMask = saturate(stormMask * boundToCloud + sideW * 0.8);

                float skyThrough = lerp(_StormSkyThrough, _StormSkyThroughMin, stormW);

                float alpha = stormW * pow(saturate(stormMask), max(0.001, _StormHardness));
                alpha *= (1.0 - skyThrough);
                alpha *= lerp(1.0, 1.0 + _StormDayExtraDark, wDay);

                skyCol = lerp(skyCol, _StormCloudColor.rgb, saturate(alpha));
                skyCol *= lerp(1.0, 1.0 - _StormDarkenSky, stormW);

                // ====== LIGHTNING ======
                float L = saturate(_Lightning) * stormW;

                float freq = max(0.01, _LightningFreq);
                float tt = _Time.y * freq;
                float k = floor(tt);
                float phase = frac(tt);

                float amp = hash21(float2(k, k * 1.97));
                amp = pow(amp, 3.1);

                float dur = max(0.02, _LightningDuration);
                float p = saturate(phase / dur);
                float attack = easeInOut(saturate(p * 3.0));
                float decay  = exp(-p * 6.0);
                float envelope = attack * decay;

                float dbl = 1.0;
                if (_LightningDouble > 0.001)
                {
                    float gap = 0.18;
                    float p2 = saturate((phase - dur*gap) / dur);
                    float env2 = easeInOut(saturate(p2 * 3.0)) * exp(-p2 * 6.0);
                    dbl = lerp(1.0, saturate(envelope + env2), _LightningDouble);
                }

                float flashCore = L * amp * envelope * dbl;

                float r1 = hash21(float2(k * 2.13, k * 0.77));
                float r2 = hash21(float2(k * 1.31, k * 3.71));
                float alt = radians(lerp(25.0, 86.0, r1));
                float az  = radians(360.0 * r2);
                float3 ldir = normalize(float3(cos(alt)*sin(az), sin(alt), cos(alt)*cos(az)));

                float3 uL, vL; orthoBasis(ldir, uL, vL);
                float2 locL = float2(dot(dir, uL), dot(dir, vL));

                float2 boltUV = 0.5 + locL * _LightningScale;

                float softness = max(0.001, _LightningSoftness);
                float rLen = length(locL);

                float nEdge = noise2(locL * _LightningEdgeNoiseScale + float2(k*1.37, k*2.11));
                nEdge = (nEdge * 2.0 - 1.0);

                float rWarp = rLen * (1.0 + nEdge * _LightningEdgeNoise * 0.25);

                float patch = smoothstep(0.0, 1.0, 1.0 - rWarp * (1.2 / softness));
                patch = saturate(patch);

                float bolt = SAMPLE_TEXTURE2D(_LightningTex, sampler_LightningTex, boltUV).r;
                bolt = smoothstep(0.15, 0.85, saturate(bolt));

                float topPref = smoothstep(-0.25, 0.65, dir.y);

                float lightningMask = flashCore * bolt * patch * topPref;

                float cloudInfluence = saturate(baseCloudStorm + alpha * 1.35);
                float lightBoost = lerp(0.35, 1.0, cloudInfluence);
                lightBoost *= lerp(1.0, 1.0 + _LightningDayBoost, wDay);

                float3 lcol = _LightningTint.rgb;

                skyCol += lcol * lightningMask * _LightningIntensity * lightBoost;

                float spill = flashCore * patch * topPref * lightBoost;
                spill *= 0.35 * _LightningIntensity;
                skyCol += lcol * spill;

                float skyWide = flashCore * _LightningSkyFlash;
                skyWide *= lerp(0.65, 1.0, cloudInfluence);
                skyWide *= lerp(0.75, 1.0, topPref);
                skyCol += lcol * skyWide;

                // fog->sky
                float horizon = saturate(1.0 - (v - _FogSkyBias));
                float fogMaskCore = pow(horizon, max(0.01, _FogSkyPower)) * _FogSkyAmount;
                float fogMaskSoft = smoothstep(0.0, 1.0, horizon) * saturate(_FogSkyAmount * 0.65);
                float fogMask = saturate(max(fogMaskCore, fogMaskSoft * fogMaskSoft));
                float3 fogBlendColor = lerp(skyCol, unity_FogColor.rgb, saturate(0.7 + horizon * 0.3));
                skyCol = lerp(skyCol, fogBlendColor, fogMask);

                return quantize_psx(skyCol, i.scrPos);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
