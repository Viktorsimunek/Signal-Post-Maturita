using UnityEngine;

[ExecuteAlways]
public class TimeOfDayController : MonoBehaviour
{
    private static readonly int Time01Id = Shader.PropertyToID("_Time01");
    private static readonly int StormId = Shader.PropertyToID("_Storm");
    private static readonly int LightningId = Shader.PropertyToID("_Lightning");
    private static readonly int TwilightId = Shader.PropertyToID("_Twilight");
    private static readonly int ZenithDayId = Shader.PropertyToID("_ZenithDay");
    private static readonly int HorizonDayId = Shader.PropertyToID("_HorizonDay");
    private static readonly int ZenithNightId = Shader.PropertyToID("_ZenithNight");
    private static readonly int HorizonNightId = Shader.PropertyToID("_HorizonNight");
    private static readonly int StormDayDesaturateId = Shader.PropertyToID("_StormDayDesaturate");
    private static readonly int StormDayToNightId = Shader.PropertyToID("_StormDayToNight");
    private static readonly int StormDayTintId = Shader.PropertyToID("_StormDayTint");
    private static readonly int StormCloudColorId = Shader.PropertyToID("_StormCloudColor");
    private static readonly int StormDarkenSkyId = Shader.PropertyToID("_StormDarkenSky");
    private static readonly int SunColorId = Shader.PropertyToID("_SunColor");
    private static readonly int LightningTintId = Shader.PropertyToID("_LightningTint");
    private static readonly int LightningFreqId = Shader.PropertyToID("_LightningFreq");
    private static readonly int LightningDurationId = Shader.PropertyToID("_LightningDuration");
    private static readonly int LightningDoubleId = Shader.PropertyToID("_LightningDouble");
    private static readonly int LightningIntensityId = Shader.PropertyToID("_LightningIntensity");

    [Header("Skybox")]
    [SerializeField] private Material skyboxMaterial;
    [SerializeField] private bool useRenderSettingsSkybox = true;
    [SerializeField] private bool updateEnvironmentLighting = false;

    [Header("Time")]
    [Range(0f, 1f)]
    [SerializeField] private float timeOfDay = 0.4f;
    [SerializeField] private bool autoAdvanceTime = true;
    [SerializeField] private bool runInEditMode = true;
    [Min(1f)]
    [SerializeField] private float dayLengthInSeconds = 240f;

    [Header("Weather")]
    [Range(0f, 1f)]
    [SerializeField] private float stormAmount;
    [Range(0f, 1f)]
    [SerializeField] private float lightningAmount;

    [Header("Optional Sun Light")]
    [SerializeField] private Light sunLight;
    [SerializeField] private bool syncSunLight = false;
    [SerializeField] private float sunMaxAltitude = 55f;
    [SerializeField] private float sunAzimuthOffset = 90f;
    [SerializeField] private float dayLightIntensity = 1f;
    [SerializeField] private float nightLightIntensity = 0f;
    [Range(0f, 1f)]
    [SerializeField] private float stormSunDimmer = 0.65f;

    [Header("Storm Light")]
    [SerializeField] private bool syncStormLight = true;
    [SerializeField] private Light stormLight;
    [SerializeField] private float stormLightBaseIntensity = 0.18f;
    [SerializeField] private float stormLightFlashIntensity = 1.4f;
    [Range(0f, 1f)]
    [SerializeField] private float stormLightSkyAlignment = 0.7f;

    [Header("Fog")]
    [SerializeField] private bool syncFogSettings = true;
    [Range(0f, 1f)]
    [SerializeField] private float fogColorBlend = 0.2f;
    [ColorUsage(false, true)]
    [SerializeField] private Color sunsetFogTint = new Color(1f, 0.58f, 0.28f, 1f);
    [Range(0f, 1f)]
    [SerializeField] private float sunsetFogStrength = 0.45f;
    [Range(0f, 1f)]
    [SerializeField] private float horizonFogColorWeight = 0.78f;
    [Min(0f)]
    [SerializeField] private float dayFogDensity = 0.00035f;
    [Min(0f)]
    [SerializeField] private float nightFogDensity = 0.0009f;
    [Min(0f)]
    [SerializeField] private float stormFogDensity = 0.003f;
    [Range(0f, 2f)]
    [SerializeField] private float lightningFogBoost = 0.08f;
    [Range(0f, 1f)]
    [SerializeField] private float skyFogInfluence = 0.65f;

    [Header("Ambient")]
    [SerializeField] private bool syncAmbientLighting = true;
    [SerializeField] private float dayAmbientIntensity = 1f;
    [SerializeField] private float nightAmbientIntensity = 0.35f;
    [Range(0f, 1f)]
    [SerializeField] private float stormAmbientDimmer = 0.45f;
    [Range(0f, 2f)]
    [SerializeField] private float lightningAmbientBoost = 0.3f;

    private Material targetMaterial;
    private float lastAppliedTime = -1f;
    private float lastAppliedStorm = -1f;
    private float lastAppliedLightning = -1f;

    public float Time01 => timeOfDay;
    public float StormAmount01 => stormAmount;
    public float LightningAmount01 => lightningAmount;
    public float DayWeight01 => EvaluateCurrentDayWeight();
    public float NightWeight01 => 1f - DayWeight01;

    private void Reset()
    {
        skyboxMaterial = RenderSettings.skybox;
    }

    private void OnEnable()
    {
        ResolveMaterial();
        ApplyAll(forceEnvironmentUpdate: true);
    }

    private void Update()
    {
        bool shouldAdvance = autoAdvanceTime && (Application.isPlaying || runInEditMode);
        float deltaTime = Application.isPlaying ? Time.deltaTime : Time.unscaledDeltaTime;

        if (shouldAdvance)
        {
            float cycleDuration = Mathf.Max(1f, dayLengthInSeconds);
            timeOfDay = Mathf.Repeat(timeOfDay + deltaTime / cycleDuration, 1f);
        }

        ApplyAll(forceEnvironmentUpdate: false);
    }

    private void OnValidate()
    {
        dayLengthInSeconds = Mathf.Max(1f, dayLengthInSeconds);
        timeOfDay = Mathf.Repeat(timeOfDay, 1f);
        stormAmount = Mathf.Clamp01(stormAmount);
        lightningAmount = Mathf.Clamp01(lightningAmount);

        ResolveMaterial();
        ApplyAll(forceEnvironmentUpdate: true);
    }

    [ContextMenu("Apply Sky Settings")]
    public void ApplySkySettings()
    {
        ResolveMaterial();
        ApplyAll(forceEnvironmentUpdate: true);
    }

    public void SetTime01(float value)
    {
        timeOfDay = Mathf.Repeat(value, 1f);
        ApplySkySettings();
    }

    public void SetStorm(float value)
    {
        stormAmount = Mathf.Clamp01(value);
        ApplySkySettings();
    }

    public void SetLightning(float value)
    {
        lightningAmount = Mathf.Clamp01(value);
        ApplySkySettings();
    }

    private void ResolveMaterial()
    {
        targetMaterial = useRenderSettingsSkybox ? RenderSettings.skybox : skyboxMaterial;

        if (targetMaterial == null && skyboxMaterial != null)
        {
            targetMaterial = skyboxMaterial;
        }
    }

    private void ApplyAll(bool forceEnvironmentUpdate)
    {
        if (targetMaterial == null)
        {
            return;
        }

        bool changed = false;

        if (!Mathf.Approximately(lastAppliedTime, timeOfDay))
        {
            targetMaterial.SetFloat(Time01Id, timeOfDay);
            lastAppliedTime = timeOfDay;
            changed = true;
        }

        if (!Mathf.Approximately(lastAppliedStorm, stormAmount))
        {
            targetMaterial.SetFloat(StormId, stormAmount);
            lastAppliedStorm = stormAmount;
            changed = true;
        }

        if (!Mathf.Approximately(lastAppliedLightning, lightningAmount))
        {
            targetMaterial.SetFloat(LightningId, lightningAmount);
            lastAppliedLightning = lightningAmount;
            changed = true;
        }

        if (useRenderSettingsSkybox && RenderSettings.skybox != targetMaterial)
        {
            RenderSettings.skybox = targetMaterial;
            changed = true;
        }

        SyncSunLight();
        SyncFogSettings();
        SyncAmbientLighting();
        SyncStormLight();

        if ((changed || forceEnvironmentUpdate) && updateEnvironmentLighting)
        {
            DynamicGI.UpdateEnvironment();
        }
    }

    private void SyncSunLight()
    {
        if (!syncSunLight || sunLight == null)
        {
            return;
        }

        float sunAltitude;
        float sunAzimuth;
        Vector3 sunDirection = CalculateSunDirection(timeOfDay, out sunAltitude, out sunAzimuth);
        float dayWeight = EvaluateDayWeight(sunAltitude);
        Color skyColor = EvaluateSkyLightingColor(dayWeight);
        Color sunColor = Color.Lerp(GetMaterialColor(SunColorId, Color.white), skyColor, stormAmount * 0.25f);
        float lightningPulse = EvaluateLightningPulse();

        sunLight.transform.rotation = Quaternion.LookRotation(-sunDirection, Vector3.up);

        float stormDim = Mathf.Lerp(1f, 1f - stormSunDimmer, stormAmount);
        float lightningBoost = 1f + lightningPulse * 0.35f;

        sunLight.color = sunColor;
        sunLight.intensity = Mathf.Lerp(nightLightIntensity, dayLightIntensity, dayWeight) * stormDim * lightningBoost;
        sunLight.enabled = sunLight.intensity > 0.001f;
    }

    private void SyncFogSettings()
    {
        if (!syncFogSettings || targetMaterial == null)
        {
            return;
        }

        float sunAltitude;
        float sunAzimuth;
        CalculateSunDirection(timeOfDay, out sunAltitude, out sunAzimuth);

        float dayWeight = EvaluateDayWeight(sunAltitude);
        Color fogColor = EvaluateFogColor(dayWeight, sunAltitude);
        float fogDensity = EvaluateFogDensity(dayWeight);

        RenderSettings.fog = true;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
    }

    private void SyncAmbientLighting()
    {
        if (!syncAmbientLighting)
        {
            return;
        }

        float sunAltitude;
        float sunAzimuth;
        CalculateSunDirection(timeOfDay, out sunAltitude, out sunAzimuth);

        float dayWeight = EvaluateDayWeight(sunAltitude);
        float lightningPulse = EvaluateLightningPulse();
        Color ambientColor = EvaluateSkyLightingColor(dayWeight);
        float baseIntensity = Mathf.Lerp(nightAmbientIntensity, dayAmbientIntensity, dayWeight);
        float stormDim = Mathf.Lerp(1f, 1f - stormAmbientDimmer, stormAmount);
        float lightningBoost = 1f + lightningPulse * lightningAmbientBoost;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor * (baseIntensity * stormDim * lightningBoost);
    }

    private void SyncStormLight()
    {
        if (!syncStormLight || stormLight == null)
        {
            return;
        }

        float sunAltitude;
        float sunAzimuth;
        Vector3 sunDirection = CalculateSunDirection(timeOfDay, out sunAltitude, out sunAzimuth);
        float dayWeight = EvaluateDayWeight(sunAltitude);
        float lightningPulse = EvaluateLightningPulse();

        Color lightningTint = GetMaterialColor(LightningTintId, new Color(1f, 1.02f, 1.1f, 1f));
        Color skyColor = EvaluateSkyLightingColor(dayWeight);
        Color stormColor = Color.Lerp(skyColor, lightningTint, Mathf.Clamp01(stormLightSkyAlignment + lightningPulse * 0.35f));

        if (stormLight.type == LightType.Directional)
        {
            Vector3 stormDirection = Vector3.Slerp(-sunDirection, Vector3.down, 0.6f).normalized;
            stormLight.transform.rotation = Quaternion.LookRotation(-stormDirection, Vector3.up);
        }

        stormLight.color = stormColor;
        stormLight.intensity = (stormAmount * stormLightBaseIntensity) + (lightningPulse * stormLightFlashIntensity);
        stormLight.enabled = stormLight.intensity > 0.001f;
    }

    private float EvaluateDayWeight(float sunAltitude)
    {
        float twilightFraction = GetMaterialFloat(TwilightId, 0.18f);
        float twilightDegrees = Mathf.Max(1f, sunMaxAltitude * (twilightFraction * 2f));
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-twilightDegrees, twilightDegrees, sunAltitude));
    }

    private float EvaluateCurrentDayWeight()
    {
        float sunAltitude;
        float sunAzimuth;
        CalculateSunDirection(timeOfDay, out sunAltitude, out sunAzimuth);
        return EvaluateDayWeight(sunAltitude);
    }

    private Color EvaluateFogColor(float dayWeight, float sunAltitude)
    {
        Color horizonDay = GetMaterialColor(HorizonDayId, new Color(0.70f, 0.75f, 0.80f, 1f));
        Color zenithDay = GetMaterialColor(ZenithDayId, new Color(0.25f, 0.40f, 0.70f, 1f));
        Color horizonNight = GetMaterialColor(HorizonNightId, new Color(0.06f, 0.08f, 0.12f, 1f));
        Color zenithNight = GetMaterialColor(ZenithNightId, new Color(0.02f, 0.06f, 0.12f, 1f));
        Color stormDayTint = GetMaterialColor(StormDayTintId, new Color(0.22f, 0.24f, 0.28f, 1f));
        Color stormCloudColor = GetMaterialColor(StormCloudColorId, new Color(0.01f, 0.01f, 0.015f, 1f));
        Color sunColor = GetMaterialColor(SunColorId, new Color(1f, 0.96f, 0.85f, 1f));
        Color lightningTint = GetMaterialColor(LightningTintId, new Color(1f, 1.02f, 1.1f, 1f));

        float stormDayDesaturate = GetMaterialFloat(StormDayDesaturateId, 0.7f);
        float stormDayToNight = GetMaterialFloat(StormDayToNightId, 0.45f);
        float stormDarkenSky = GetMaterialFloat(StormDarkenSkyId, 1f);

        float clampedHorizonWeight = Mathf.Clamp01(horizonFogColorWeight);
        Color lowSkyDay = Color.Lerp(horizonDay, zenithDay, 0.18f);
        Color lowSkyNight = Color.Lerp(horizonNight, zenithNight, 0.12f);
        Color baseDay = Color.Lerp(lowSkyDay, horizonDay, clampedHorizonWeight);
        Color baseNight = Color.Lerp(lowSkyNight, horizonNight, clampedHorizonWeight * 0.9f);

        float dayLuma = Mathf.Clamp01((baseDay.r * 0.2126f) + (baseDay.g * 0.7152f) + (baseDay.b * 0.0722f));
        Color dayDesat = Color.Lerp(baseDay, new Color(dayLuma, dayLuma, dayLuma, 1f), stormDayDesaturate * stormAmount);
        Color dayTinted = MultiplyRgb(dayDesat, stormDayTint);
        Color dayStorm = Color.Lerp(dayTinted, baseNight, stormAmount * stormDayToNight);

        Color skyColor = Color.Lerp(baseNight, dayStorm, dayWeight);
        Color stormFogTint = Color.Lerp(dayStorm, stormCloudColor, 0.35f);
        skyColor = Color.Lerp(skyColor, stormFogTint, stormAmount * fogColorBlend * skyFogInfluence);
        skyColor *= Mathf.Lerp(1f, 1f - (stormDarkenSky * 0.35f), stormAmount);
        skyColor = Color.Lerp(skyColor, Color.Lerp(horizonNight, horizonDay, dayWeight), (0.22f + clampedHorizonWeight * 0.28f) * Mathf.Lerp(0.55f, 1f, dayWeight));

        float warmSunBand = Mathf.Clamp01(1f - Mathf.Abs(dayWeight - 0.5f) * 2.35f);
        float sunsetPhase = 1f - Mathf.Clamp01(Mathf.Abs(Mathf.DeltaAngle(timeOfDay * 360f, 270f)) / 90f);
        float lowSunBand = 1f - Mathf.Clamp01(Mathf.Abs(sunAltitude) / 18f);
        float sunsetAmount = sunsetPhase * lowSunBand * (1f - stormAmount);
        Color warmSunColor = MultiplyRgb(sunColor, horizonDay);
        Color sunsetColor = Color.Lerp(warmSunColor, sunsetFogTint, 0.6f);
        skyColor = Color.Lerp(skyColor, warmSunColor, warmSunBand * (1f - stormAmount) * 0.22f);
        skyColor = Color.Lerp(skyColor, sunsetColor, sunsetAmount * sunsetFogStrength);

        float lightningFlash = EvaluateLightningPulse() * lightningFogBoost;
        skyColor = Color.Lerp(skyColor, lightningTint, Mathf.Clamp01(lightningFlash));
        skyColor.a = 1f;
        return skyColor;
    }

    private float EvaluateFogDensity(float dayWeight)
    {
        float baseDensity = Mathf.Lerp(nightFogDensity, dayFogDensity, dayWeight);
        float stormDensity = Mathf.Lerp(baseDensity, stormFogDensity, stormAmount);
        return stormDensity * (1f + EvaluateLightningPulse() * lightningFogBoost);
    }

    private Color EvaluateSkyLightingColor(float dayWeight)
    {
        float sunAltitude;
        float sunAzimuth;
        CalculateSunDirection(timeOfDay, out sunAltitude, out sunAzimuth);

        Color fogColor = EvaluateFogColor(dayWeight, sunAltitude);
        Color zenithDay = GetMaterialColor(ZenithDayId, new Color(0.25f, 0.40f, 0.70f, 1f));
        Color horizonDay = GetMaterialColor(HorizonDayId, new Color(0.70f, 0.75f, 0.80f, 1f));
        Color zenithNight = GetMaterialColor(ZenithNightId, new Color(0.02f, 0.06f, 0.12f, 1f));
        Color horizonNight = GetMaterialColor(HorizonNightId, new Color(0.06f, 0.08f, 0.12f, 1f));

        Color domeDay = Color.Lerp(horizonDay, zenithDay, 0.65f);
        Color domeNight = Color.Lerp(horizonNight, zenithNight, 0.55f);
        Color domeColor = Color.Lerp(domeNight, domeDay, dayWeight);
        domeColor = Color.Lerp(domeColor, fogColor, skyFogInfluence);
        domeColor.a = 1f;
        return domeColor;
    }

    private float EvaluateLightningPulse()
    {
        if (targetMaterial == null)
        {
            return 0f;
        }

        float stormLightning = Mathf.Clamp01(lightningAmount) * Mathf.Clamp01(stormAmount);
        if (stormLightning <= 0f)
        {
            return 0f;
        }

        float frequency = Mathf.Max(0.01f, GetMaterialFloat(LightningFreqId, 0.9f));
        float duration = Mathf.Max(0.02f, GetMaterialFloat(LightningDurationId, 0.28f));
        float doubleFlash = Mathf.Clamp01(GetMaterialFloat(LightningDoubleId, 0.75f));
        float intensity = Mathf.Max(0f, GetMaterialFloat(LightningIntensityId, 6f));
        float timeValue = Application.isPlaying ? Time.timeSinceLevelLoad : Time.realtimeSinceStartup;
        float scaledTime = timeValue * frequency;
        float flashIndex = Mathf.Floor(scaledTime);
        float phase = scaledTime - flashIndex;

        float amplitude = Mathf.Pow(Hash21(flashIndex, flashIndex * 1.97f), 3.1f);
        float normalizedPhase = Mathf.Clamp01(phase / duration);
        float envelope = EaseInOut(Mathf.Clamp01(normalizedPhase * 3f)) * Mathf.Exp(-normalizedPhase * 6f);

        float combinedFlash = envelope;
        if (doubleFlash > 0.001f)
        {
            float gap = 0.18f;
            float secondPhase = Mathf.Clamp01((phase - duration * gap) / duration);
            float secondEnvelope = EaseInOut(Mathf.Clamp01(secondPhase * 3f)) * Mathf.Exp(-secondPhase * 6f);
            combinedFlash = Mathf.Lerp(envelope, Mathf.Clamp01(envelope + secondEnvelope), doubleFlash);
        }

        float pulse = stormLightning * amplitude * combinedFlash;
        return pulse * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(intensity / 8f));
    }

    private Color GetMaterialColor(int propertyId, Color fallback)
    {
        return targetMaterial != null && targetMaterial.HasProperty(propertyId)
            ? targetMaterial.GetColor(propertyId)
            : fallback;
    }

    private float GetMaterialFloat(int propertyId, float fallback)
    {
        return targetMaterial != null && targetMaterial.HasProperty(propertyId)
            ? targetMaterial.GetFloat(propertyId)
            : fallback;
    }

    private static Color MultiplyRgb(Color a, Color b)
    {
        return new Color(a.r * b.r, a.g * b.g, a.b * b.b, 1f);
    }

    private static float EaseInOut(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - (2f * value));
    }

    private static float Hash21(float x, float y)
    {
        return Mathf.Repeat(Mathf.Sin((x * 27.619f) + (y * 57.583f)) * 43758.5453f, 1f);
    }

    private Vector3 CalculateSunDirection(float t01, out float altitudeDeg, out float azimuthDeg)
    {
        float angle = (t01 - 0.25f) * Mathf.PI * 2f;
        altitudeDeg = Mathf.Sin(angle) * sunMaxAltitude;
        azimuthDeg = sunAzimuthOffset + t01 * 360f;

        float altitudeRad = altitudeDeg * Mathf.Deg2Rad;
        float azimuthRad = azimuthDeg * Mathf.Deg2Rad;

        return new Vector3(
            Mathf.Cos(altitudeRad) * Mathf.Sin(azimuthRad),
            Mathf.Sin(altitudeRad),
            Mathf.Cos(altitudeRad) * Mathf.Cos(azimuthRad)
        ).normalized;
    }
}
