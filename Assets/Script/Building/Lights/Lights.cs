using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class Lights : MonoBehaviour
{
    [System.Serializable]
    private struct EmissionTarget
    {
        public Renderer renderer;
        [Min(0)] public int materialIndex;
    }

    private struct CachedEmission
    {
        public Renderer renderer;
        public int materialIndex;
    }

    private static readonly int DefaultEmissionId = Shader.PropertyToID("_EmissionColor");
    private static Transform mainCam;

    [Header("References")]
    [SerializeField] private Light[] controlledLights;
    [SerializeField] private EmissionTarget[] emissionTargets;
    [SerializeField] private AudioSource oneShotAudioSource;
    [SerializeField] private AudioSource ambientAudioSource;

    [Header("Light")]
    [SerializeField] private bool startPowered;
    [SerializeField] private float onIntensity = 1f;
    [SerializeField] private float offIntensity = 0f;
    [SerializeField] private float fadeOutDuration = 0.12f;

    [Header("Fluorescent Start")]
    [SerializeField] private bool playStartupFlicker = true;
    [SerializeField] private Vector2 startupDelayRange = new Vector2(0f, 0.35f);
    [SerializeField] private Vector2 flickerDurationRange = new Vector2(0.35f, 1.1f);
    [SerializeField] private Vector2 flickerSpeedRange = new Vector2(18f, 34f);
    [SerializeField] private Vector2 blinkChanceRange = new Vector2(0.08f, 0.2f);
    [SerializeField] private Vector2 blinkLengthRange = new Vector2(0.02f, 0.08f);
    [SerializeField] private Vector2 startupOvershootRange = new Vector2(1.02f, 1.16f);

    [Header("Emission")]
    [SerializeField] private string emissionColorProperty = "_EmissionColor";
    [SerializeField] private Color emissionOffColor = Color.black;
    [SerializeField] private Color emissionOnColor = Color.white;
    [SerializeField] private bool readEmissionFromSharedMaterial = true;
    [SerializeField] private float emissionIntensityMultiplier = 1f;
    [SerializeField] private string emissionKeyword = "_EMISSION";

    [Header("Optimization")]
    [SerializeField] private bool disableRealtimeShadows = true;
    [SerializeField] private bool turnOffRealLightsFarAway = true;
    [SerializeField] private float realLightDistance = 22f;
    [SerializeField, Range(10, 60)] private int maxVisualUpdatesPerSecond = 30;

    [Header("Audio")]
    [SerializeField] private AudioClip[] startupClips;
    [SerializeField] private AudioClip shutdownClip;
    [SerializeField] private AudioClip ambientLoopClip;
    [SerializeField] private float startupVolume = 1f;
    [SerializeField] private float shutdownVolume = 1f;
    [SerializeField] private float ambientVolume = 0.22f;
    [SerializeField] private Vector2 startupPitchRange = new Vector2(0.94f, 1.08f);
    [SerializeField] private Vector2 shutdownPitchRange = new Vector2(0.96f, 1.04f);
    [SerializeField] private float ambientFadeInDuration = 0.35f;
    [SerializeField] private float ambientFadeOutDuration = 0.18f;
    [SerializeField, Range(0f, 1f)] private float audioSpatialBlend = 1f;

    [Header("Debug")]
    [SerializeField] private bool useGlobalPower = true;

    private MaterialPropertyBlock block;
    private CachedEmission[] cachedEmission;
    private Coroutine powerRoutine;
    private Coroutine ambientRoutine;
    private Color realEmissionOnColor;
    private int emissionId;
    private bool isPowered;
    private bool realLightsAllowed = true;
    private float currentIntensity;
    private float lastApplied = -999f;
    private float distanceCheckTimer;
    private float seed;

    public bool IsPowered => isPowered;

    private void Awake()
    {
        FindMissingReferences();
        SetupAudio();
        CacheEmission();
        SetupOptimization();

        seed = Random.Range(0f, 999f);
        isPowered = useGlobalPower ? LightsPowerGrid.IsPowered : startPowered;
        Apply(isPowered ? onIntensity : offIntensity, true);
        SetAmbientNow(isPowered);
    }

    private void OnEnable()
    {
        LightsPowerGrid.PowerChanged += OnGlobalPowerChanged;
        if (useGlobalPower) OnGlobalPowerChanged(LightsPowerGrid.IsPowered);
    }

    private void OnDisable()
    {
        LightsPowerGrid.PowerChanged -= OnGlobalPowerChanged;
        StopPowerRoutine();
        StopAmbientRoutine();
    }

    private void Update()
    {
        if (!isPowered || !turnOffRealLightsFarAway || controlledLights.Length == 0) return;

        distanceCheckTimer -= Time.deltaTime;
        if (distanceCheckTimer > 0f) return;

        distanceCheckTimer = 0.35f;
        if (RefreshDistanceState()) Apply(currentIntensity, true);
    }

    private void OnValidate()
    {
        startupDelayRange = Sort(startupDelayRange);
        flickerDurationRange = Sort(flickerDurationRange);
        flickerSpeedRange = Sort(flickerSpeedRange);
        blinkChanceRange = Sort(blinkChanceRange);
        blinkLengthRange = Sort(blinkLengthRange);
        startupOvershootRange = Sort(startupOvershootRange);
        startupPitchRange = Sort(startupPitchRange);
        shutdownPitchRange = Sort(shutdownPitchRange);

        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
        onIntensity = Mathf.Max(0f, onIntensity);
        offIntensity = Mathf.Max(0f, offIntensity);
        emissionIntensityMultiplier = Mathf.Max(0f, emissionIntensityMultiplier);
        realLightDistance = Mathf.Max(0f, realLightDistance);

        if (!Application.isPlaying) FindMissingReferences();
    }

    [ContextMenu("Turn On")] public void TurnOn() => SetPowered(true);
    [ContextMenu("Turn Off")] public void TurnOff() => SetPowered(false);
    [ContextMenu("Toggle")] public void TogglePowered() => SetPowered(!isPowered);

    public void SetPowered(bool powered)
    {
        float target = powered ? onIntensity : offIntensity;
        if (powered == isPowered && powerRoutine == null && Mathf.Abs(currentIntensity - target) < 0.001f) return;

        StopPowerRoutine();
        StopAmbientRoutine();
        powerRoutine = StartCoroutine(PowerChange(powered));
    }

    private IEnumerator PowerChange(bool powered)
    {
        isPowered = powered;

        if (!powered)
        {
            PlaySound(shutdownClip, shutdownVolume, shutdownPitchRange);
            FadeAmbient(false, ambientFadeOutDuration);
            yield return FadeTo(offIntensity, fadeOutDuration);
            powerRoutine = null;
            yield break;
        }

        PlayRandomStartupSound();
        RefreshDistanceState();

        if (!playStartupFlicker)
        {
            Apply(onIntensity, true);
            FadeAmbient(true, ambientFadeInDuration);
            powerRoutine = null;
            yield break;
        }

        float delay = Rand(startupDelayRange);
        if (delay > 0f)
        {
            Apply(offIntensity);
            yield return new WaitForSeconds(delay);
        }

        float duration = Rand(flickerDurationRange);
        float speed = Rand(flickerSpeedRange);
        float blinkChance = Rand(blinkChanceRange);
        float blinkLength = Rand(blinkLengthRange);
        float overshoot = Rand(startupOvershootRange);
        float blinkTimer = 0f;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (blinkTimer > 0f) blinkTimer -= Time.deltaTime;
            else if (Random.value < blinkChance * Time.deltaTime * 10f) blinkTimer = blinkLength;

            float progress = Mathf.Clamp01(t / duration);
            float noise = Mathf.PerlinNoise(seed, t * speed) * 1.2f;
            float value = onIntensity * Mathf.Clamp01(Mathf.Lerp(0.15f, overshoot, progress) * noise);
            if (blinkTimer > 0f) value *= Random.Range(0.02f, 0.25f);

            Apply(value);
            yield return new WaitForSeconds(1f / Mathf.Max(1, maxVisualUpdatesPerSecond));
        }

        Apply(onIntensity, true);
        FadeAmbient(true, ambientFadeInDuration);
        powerRoutine = null;
    }

    private IEnumerator FadeTo(float target, float time)
    {
        float start = currentIntensity;
        if (time <= 0.0001f)
        {
            Apply(target, true);
            yield break;
        }

        for (float t = 0f; t < time; t += 1f / Mathf.Max(1, maxVisualUpdatesPerSecond))
        {
            Apply(Mathf.Lerp(start, target, Mathf.Clamp01(t / time)));
            yield return new WaitForSeconds(1f / Mathf.Max(1, maxVisualUpdatesPerSecond));
        }

        Apply(target, true);
    }

    private void Apply(float intensity, bool force = false)
    {
        currentIntensity = intensity;
        if (!force && Mathf.Abs(lastApplied - intensity) < 0.003f) return;
        lastApplied = intensity;

        bool lightsOn = intensity > 0.001f && realLightsAllowed;
        for (int i = 0; i < controlledLights.Length; i++)
        {
            if (controlledLights[i] == null) continue;
            controlledLights[i].enabled = lightsOn;
            controlledLights[i].intensity = intensity;
        }

        ApplyEmission(Mathf.InverseLerp(offIntensity, Mathf.Max(onIntensity, 0.0001f), intensity));
    }

    private void ApplyEmission(float power)
    {
        if (cachedEmission == null || cachedEmission.Length == 0) return;
        if (block == null) block = new MaterialPropertyBlock();

        Color color = Color.Lerp(emissionOffColor, realEmissionOnColor, Mathf.Clamp01(power));
        for (int i = 0; i < cachedEmission.Length; i++)
        {
            Renderer rend = cachedEmission[i].renderer;
            if (rend == null) continue;

            int index = cachedEmission[i].materialIndex;
            rend.GetPropertyBlock(block, index);
            block.SetColor(emissionId, color);
            rend.SetPropertyBlock(block, index);
        }
    }

    private void FindMissingReferences()
    {
        if (controlledLights == null || controlledLights.Length == 0)
            controlledLights = GetComponentsInChildren<Light>(true);

        if (emissionTargets != null && emissionTargets.Length > 0) return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        emissionTargets = new EmissionTarget[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            emissionTargets[i] = new EmissionTarget { renderer = renderers[i], materialIndex = 0 };
    }

    private void CacheEmission()
    {
        emissionId = emissionColorProperty == "_EmissionColor" ? DefaultEmissionId : Shader.PropertyToID(emissionColorProperty);
        realEmissionOnColor = emissionOnColor * emissionIntensityMultiplier;
        cachedEmission = new CachedEmission[emissionTargets == null ? 0 : emissionTargets.Length];

        for (int i = 0; i < cachedEmission.Length; i++)
        {
            Renderer rend = emissionTargets[i].renderer;
            int index = rend != null ? SafeMaterialIndex(rend, emissionTargets[i].materialIndex) : 0;
            cachedEmission[i] = new CachedEmission { renderer = rend, materialIndex = index };

            Material mat = GetSharedMaterial(rend, index);
            if (mat == null || !mat.HasProperty(emissionColorProperty)) continue;

            if (!string.IsNullOrWhiteSpace(emissionKeyword)) mat.EnableKeyword(emissionKeyword);
            if (readEmissionFromSharedMaterial && mat.GetColor(emissionColorProperty).maxColorComponent > 0.0001f)
                realEmissionOnColor = mat.GetColor(emissionColorProperty) * emissionIntensityMultiplier;
        }
    }

    private void SetupOptimization()
    {
        if (controlledLights == null) return;

        for (int i = 0; i < controlledLights.Length; i++)
            if (controlledLights[i] != null && disableRealtimeShadows)
                controlledLights[i].shadows = LightShadows.None;

        RefreshDistanceState();
    }

    private bool RefreshDistanceState()
    {
        bool old = realLightsAllowed;
        realLightsAllowed = true;

        if (turnOffRealLightsFarAway && realLightDistance > 0f)
        {
            if (mainCam == null && Camera.main != null) mainCam = Camera.main.transform;
            if (mainCam != null)
                realLightsAllowed = (mainCam.position - transform.position).sqrMagnitude <= realLightDistance * realLightDistance;
        }

        return old != realLightsAllowed;
    }

    private void SetupAudio()
    {
        if (oneShotAudioSource == null) oneShotAudioSource = GetComponent<AudioSource>();
        if (oneShotAudioSource == null) oneShotAudioSource = gameObject.AddComponent<AudioSource>();
        if (ambientAudioSource == null) ambientAudioSource = gameObject.AddComponent<AudioSource>();

        oneShotAudioSource.playOnAwake = false;
        oneShotAudioSource.loop = false;
        oneShotAudioSource.spatialBlend = audioSpatialBlend;

        ambientAudioSource.playOnAwake = false;
        ambientAudioSource.loop = true;
        ambientAudioSource.clip = ambientLoopClip;
        ambientAudioSource.volume = 0f;
        ambientAudioSource.spatialBlend = audioSpatialBlend;
    }

    private void PlayRandomStartupSound()
    {
        if (startupClips == null || startupClips.Length == 0) return;
        PlaySound(startupClips[Random.Range(0, startupClips.Length)], startupVolume, startupPitchRange);
    }

    private void PlaySound(AudioClip clip, float volume, Vector2 pitchRange)
    {
        if (oneShotAudioSource == null || clip == null) return;
        oneShotAudioSource.pitch = Rand(pitchRange);
        oneShotAudioSource.PlayOneShot(clip, volume);
    }

    private void SetAmbientNow(bool play)
    {
        if (ambientAudioSource == null) return;

        ambientAudioSource.clip = ambientLoopClip;
        ambientAudioSource.volume = play && ambientLoopClip != null ? ambientVolume : 0f;

        if (play && ambientLoopClip != null && !ambientAudioSource.isPlaying) ambientAudioSource.Play();
        if (!play && ambientAudioSource.isPlaying) ambientAudioSource.Stop();
    }

    private void FadeAmbient(bool play, float time)
    {
        if (ambientAudioSource == null) return;
        StopAmbientRoutine();
        ambientRoutine = StartCoroutine(FadeAmbientRoutine(play, time));
    }

    private IEnumerator FadeAmbientRoutine(bool play, float time)
    {
        ambientAudioSource.clip = ambientLoopClip;
        if (play && ambientLoopClip == null) yield break;
        if (play && !ambientAudioSource.isPlaying) ambientAudioSource.Play();

        float start = ambientAudioSource.volume;
        float target = play ? ambientVolume : 0f;

        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            ambientAudioSource.volume = Mathf.Lerp(start, target, Mathf.Clamp01(t / time));
            yield return null;
        }

        ambientAudioSource.volume = target;
        if (!play) ambientAudioSource.Stop();
        ambientRoutine = null;
    }

    private void OnGlobalPowerChanged(bool powered)
    {
        if (useGlobalPower) SetPowered(powered);
    }

    private void StopPowerRoutine()
    {
        if (powerRoutine != null) StopCoroutine(powerRoutine);
        powerRoutine = null;
    }

    private void StopAmbientRoutine()
    {
        if (ambientRoutine != null) StopCoroutine(ambientRoutine);
        ambientRoutine = null;
    }

    private static Material GetSharedMaterial(Renderer rend, int index)
    {
        if (rend == null || rend.sharedMaterials == null || rend.sharedMaterials.Length == 0) return null;
        return rend.sharedMaterials[Mathf.Clamp(index, 0, rend.sharedMaterials.Length - 1)];
    }

    private static int SafeMaterialIndex(Renderer rend, int index)
    {
        int count = rend.sharedMaterials == null ? 0 : rend.sharedMaterials.Length;
        return count <= 0 ? 0 : Mathf.Clamp(index, 0, count - 1);
    }

    private static float Rand(Vector2 range) => Random.Range(range.x, range.y);
    private static Vector2 Sort(Vector2 value) => value.x <= value.y ? value : new Vector2(value.y, value.x);
}
