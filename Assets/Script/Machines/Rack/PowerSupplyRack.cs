using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PowerSupplyRack : MonoBehaviour, IInteractable
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Interact")]
    [SerializeField] private bool canInteract = true;
    [SerializeField] private bool addColliderIfMissing = true;
    [SerializeField] private string turnOnPrompt = "Zapnout zdroj";
    [SerializeField] private string turnOffPrompt = "Vypnout zdroj";
    [SerializeField] private string hotPrompt = "Prehraty zdroj";
    [SerializeField] private string noFusePowerPrompt = "Bez proudu";

    [Header("Rack")]
    [SerializeField] private EletricRack rack;
    [SerializeField] private bool startRunning;

    [Header("Heat")]
    [SerializeField, Range(0f, 1f)] private float heat;
    [SerializeField] private float heatUpSpeed = 0.06f;
    [SerializeField] private float coolSpeed = 0.06f;
    [SerializeField, Range(0f, 1f)] private float warningHeat = 0.79f;
    [SerializeField, Range(0f, 1f)] private float overheatAt = 1f;
    [SerializeField, Range(0f, 1f)] private float canStartAgainAt;

    [Header("Light")]
    [SerializeField] private Renderer diode;
    [SerializeField] private Renderer[] extraDiodes;
    [SerializeField] private Light diodeLight;
    [SerializeField] private Color offColor = Color.black;
    [SerializeField] private Color normalColor = Color.green;
    [SerializeField] private Color warningColor = new Color(1f, 0.55f, 0f);
    [SerializeField] private Color overheatColor = Color.red;
    [SerializeField] private float emissionPower = 2.4f;
    [SerializeField] private float blinkSpeed = 9f;
    [SerializeField] private Color overheatFlashColor = Color.white;
    [SerializeField] private float overheatFlashIntensity = 55f;
    [SerializeField] private float overheatFlashTime = 0.12f;

    [Header("Button")]
    [SerializeField] private Transform button;
    [SerializeField] private Vector3 offButtonPosition;
    [SerializeField] private Vector3 onButtonPosition = new Vector3(0f, -0.025f, 0f);
    [SerializeField] private float buttonSpeed = 12f;

    [Header("Sparks")]
    [SerializeField] private GameObject sparksPrefab;
    [SerializeField] private Transform sparksPoint;
    [SerializeField] private float sparksLifeTime = 2f;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioSource hotAudioSource;
    [SerializeField] private AudioClip switchSound;
    [SerializeField] private AudioClip humSound;
    [SerializeField] private AudioClip hotSound;
    [SerializeField] private AudioClip coolingSound;
    [SerializeField] private AudioClip warningBeep;
    [SerializeField] private AudioClip overheatSound;
    [SerializeField] private float switchVolume = 0.8f;
    [SerializeField] private float humVolume = 0.35f;
    [SerializeField] private float hotVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float hotSoundStartHeat = 0.2f;
    [SerializeField] private float coolingVolume = 0.45f;
    [SerializeField] private float warningVolume = 0.75f;
    [SerializeField] private float loopFadeSpeed = 3f;
    [SerializeField] private Vector2 beepDelay = new Vector2(0.18f, 0.9f);

    [Header("Events")]
    [SerializeField] private UnityEvent onTurnOn;
    [SerializeField] private UnityEvent onTurnOff;
    [SerializeField] private UnityEvent onWarning;
    [SerializeField] private UnityEvent onOverheat;

    private Material[] diodeMaterials;
    private bool isRunning;
    private bool isOverheated;
    private bool readyAfterCooling;
    private bool warningWasSent;
    private float nextBeepTime;
    private float flashTimer;
    private float humFade;
    private float hotFade;
    private float coolingFade;
    private float humPitch = 1f;

    public bool IsRunning
    {
        get { return isRunning; }
    }

    public bool IsOverheated
    {
        get { return isOverheated; }
    }

    public float Heat
    {
        get { return heat; }
    }

    private void Reset()
    {
        rack = GetComponentInParent<EletricRack>();
        diode = GetComponentInChildren<Renderer>(true);
        diodeLight = GetComponentInChildren<Light>(true);
        audioSource = GetComponent<AudioSource>();
    }

    private void Awake()
    {
        if (rack == null)
        {
            rack = GetComponentInParent<EletricRack>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (hotAudioSource == null && hotSound != null)
        {
            hotAudioSource = gameObject.AddComponent<AudioSource>();
            hotAudioSource.playOnAwake = false;
            hotAudioSource.spatialBlend = audioSource != null ? audioSource.spatialBlend : 1f;
        }

        if (addColliderIfMissing && GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<BoxCollider>();
        }

        MakeDiodeMaterials();
        isRunning = startRunning;
        UpdateVisuals(true);
    }

    private void OnValidate()
    {
        heat = Mathf.Clamp01(heat);
        heatUpSpeed = Mathf.Max(0f, heatUpSpeed);
        coolSpeed = Mathf.Max(0f, coolSpeed);
        warningHeat = Mathf.Clamp(warningHeat, 0.01f, 0.99f);
        overheatAt = Mathf.Clamp(overheatAt, warningHeat + 0.01f, 1f);
        canStartAgainAt = Mathf.Clamp(canStartAgainAt, 0f, warningHeat);
        hotSoundStartHeat = Mathf.Clamp(hotSoundStartHeat, 0f, warningHeat);
        emissionPower = Mathf.Max(0f, emissionPower);
        blinkSpeed = Mathf.Max(0f, blinkSpeed);
        overheatFlashIntensity = Mathf.Max(0f, overheatFlashIntensity);
        overheatFlashTime = Mathf.Max(0.02f, overheatFlashTime);
        buttonSpeed = Mathf.Max(0f, buttonSpeed);
        sparksLifeTime = Mathf.Max(0.1f, sparksLifeTime);
        switchVolume = Mathf.Max(0f, switchVolume);
        humVolume = Mathf.Max(0f, humVolume);
        hotVolume = Mathf.Max(0f, hotVolume);
        coolingVolume = Mathf.Max(0f, coolingVolume);
        warningVolume = Mathf.Max(0f, warningVolume);
        loopFadeSpeed = Mathf.Max(0.1f, loopFadeSpeed);
        beepDelay = Sort(beepDelay);
    }

    private void Update()
    {
        UpdateHeat();
        UpdateVisuals(false);
        UpdateLoopSounds();
        UpdateWarningBeep();
    }

    public void SetRack(EletricRack owner)
    {
        rack = owner;
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        return canInteract && isActiveAndEnabled && (rack == null || !rack.IsBlackout);
    }

    public string GetInteractionPrompt(PlayerInteractor interactor)
    {
        if (rack != null && !rack.HasFusePower)
        {
            return noFusePowerPrompt;
        }

        if (isOverheated)
        {
            return hotPrompt;
        }

        return isRunning ? turnOffPrompt : turnOnPrompt;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        if (rack != null && !rack.HasFusePower)
        {
            PlayOneShot(warningBeep, warningVolume);
            return;
        }

        if (isRunning)
        {
            TurnOff();
        }
        else
        {
            TurnOn();
        }
    }

    [ContextMenu("Turn On")]
    public void TurnOn()
    {
        if (isOverheated)
        {
            PlayOneShot(warningBeep, warningVolume);
            return;
        }

        if (rack != null && !rack.TryTurnOn(this))
        {
            PlayOneShot(warningBeep, warningVolume);
            return;
        }

        isRunning = true;
        readyAfterCooling = false;
        warningWasSent = heat >= warningHeat;
        PlayOneShot(switchSound, switchVolume);
        onTurnOn?.Invoke();
    }

    [ContextMenu("Turn Off")]
    public void TurnOff()
    {
        if (!isRunning)
        {
            return;
        }

        isRunning = false;
        if (rack != null)
        {
            rack.TurnOff(this);
        }

        PlayOneShot(switchSound, switchVolume);
        onTurnOff?.Invoke();
    }

    public void ForceOffFromRack()
    {
        bool wasRunning = isRunning;
        isRunning = false;

        if (wasRunning)
        {
            onTurnOff?.Invoke();
        }
    }

    private void UpdateHeat()
    {
        if (isRunning)
        {
            heat += heatUpSpeed * Time.deltaTime;
        }
        else
        {
            heat -= coolSpeed * Time.deltaTime;
        }

        heat = Mathf.Clamp01(heat);

        if (isOverheated && heat <= canStartAgainAt)
        {
            isOverheated = false;
            readyAfterCooling = true;
            warningWasSent = false;
        }

        if (isRunning && !isOverheated && heat >= overheatAt)
        {
            Overheat();
        }

        if (isRunning && !warningWasSent && heat >= warningHeat)
        {
            warningWasSent = true;
            onWarning?.Invoke();
        }
    }

    private void Overheat()
    {
        isRunning = false;
        isOverheated = true;
        readyAfterCooling = false;
        flashTimer = overheatFlashTime;

        SpawnSparks();
        PlayOneShot(overheatSound, 1f);
        onOverheat?.Invoke();

        if (rack != null)
        {
            rack.SupplyOverheated(this);
        }
    }

    private void UpdateVisuals(bool instant)
    {
        Color color = GetDiodeColor();
        SetDiodeColor(color);
        UpdateDiodeLight(color);
        UpdateButton(instant);
    }

    private Color GetDiodeColor()
    {
        if (rack != null && !rack.HasFusePower)
        {
            return offColor;
        }

        if (isOverheated)
        {
            float t = Mathf.InverseLerp(canStartAgainAt, overheatAt, heat);
            return Color.Lerp(normalColor, overheatColor, t);
        }

        if (readyAfterCooling)
        {
            return normalColor;
        }

        if (!isRunning)
        {
            return Color.Lerp(offColor, warningColor, Mathf.Clamp01(heat / warningHeat) * 0.35f);
        }

        if (heat < warningHeat)
        {
            return Color.Lerp(normalColor, warningColor, Mathf.InverseLerp(0f, warningHeat, heat));
        }

        Color hotColor = Color.Lerp(warningColor, overheatColor, Mathf.InverseLerp(warningHeat, overheatAt, heat));
        float blink = Mathf.PingPong(Time.time * blinkSpeed, 1f);
        return hotColor * Mathf.Lerp(0.45f, 1.25f, blink);
    }

    private void UpdateDiodeLight(Color color)
    {
        if (diodeLight == null)
        {
            return;
        }

        if (rack != null && !rack.HasFusePower)
        {
            diodeLight.enabled = false;
            diodeLight.intensity = 0f;
            return;
        }

        float flash = GetFlash();
        if (flash > 0f)
        {
            diodeLight.enabled = true;
            diodeLight.color = overheatFlashColor;
            diodeLight.intensity = overheatFlashIntensity * flash;
            return;
        }

        bool shouldShine = isRunning || isOverheated || readyAfterCooling || heat > 0.05f;
        diodeLight.enabled = shouldShine;
        diodeLight.color = color;
        diodeLight.intensity = shouldShine ? Mathf.Lerp(0.3f, 2f, heat) : 0f;
    }

    private float GetFlash()
    {
        if (flashTimer <= 0f)
        {
            return 0f;
        }

        flashTimer -= Time.deltaTime;
        float t = Mathf.Clamp01(flashTimer / overheatFlashTime);
        return t * t;
    }

    private void UpdateButton(bool instant)
    {
        if (button == null)
        {
            return;
        }

        Vector3 target = isRunning ? onButtonPosition : offButtonPosition;
        if (instant || buttonSpeed <= 0f)
        {
            button.localPosition = target;
        }
        else
        {
            button.localPosition = Vector3.Lerp(button.localPosition, target, Time.deltaTime * buttonSpeed);
        }
    }

    private void SpawnSparks()
    {
        if (sparksPrefab == null)
        {
            return;
        }

        Transform point = sparksPoint != null ? sparksPoint : transform;
        GameObject sparks = Instantiate(sparksPrefab, point.position, point.rotation);
        sparks.SetActive(true);

        ParticleSystem[] particles = sparks.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].Play(true);
        }

        Destroy(sparks, sparksLifeTime);
    }

    private void SetDiodeColor(Color color)
    {
        if (diodeMaterials == null)
        {
            return;
        }

        for (int i = 0; i < diodeMaterials.Length; i++)
        {
            Material material = diodeMaterials[i];
            if (material == null)
            {
                continue;
            }

            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, color);
            }
            else if (material.HasProperty(ColorId))
            {
                material.SetColor(ColorId, color);
            }

            if (material.HasProperty(EmissionColorId))
            {
                material.SetColor(EmissionColorId, color * emissionPower);
            }
        }
    }

    private void UpdateWarningBeep()
    {
        if ((rack != null && !rack.HasFusePower) || !isRunning || heat < warningHeat || warningBeep == null || Time.time < nextBeepTime)
        {
            return;
        }

        float hotPart = Mathf.InverseLerp(warningHeat, overheatAt, heat);
        nextBeepTime = Time.time + Mathf.Lerp(beepDelay.y, beepDelay.x, hotPart);
        PlayOneShot(warningBeep, warningVolume);
    }

    private void UpdateLoopSounds()
    {
        UpdateHumAndCooling();
        UpdateHotSound();
    }

    private void UpdateHumAndCooling()
    {
        if (audioSource == null)
        {
            return;
        }

        bool hasPower = rack == null || rack.HasFusePower;
        bool wantsHum = hasPower && isRunning && humSound != null;
        bool wantsCooling = hasPower && isOverheated && coolingSound != null;

        humFade = Mathf.MoveTowards(humFade, wantsHum ? 1f : 0f, loopFadeSpeed * Time.deltaTime);
        coolingFade = Mathf.MoveTowards(coolingFade, wantsCooling ? 1f : 0f, loopFadeSpeed * Time.deltaTime);

        if (wantsHum)
        {
            PlayLoop(audioSource, humSound);
            audioSource.volume = humVolume * humFade;
            audioSource.pitch = humPitch;
            return;
        }

        if (wantsCooling)
        {
            PlayLoop(audioSource, coolingSound);
            audioSource.volume = coolingVolume * coolingFade;
            audioSource.pitch = Mathf.Lerp(0.85f, 1.08f, heat);
            return;
        }

        FadeOutSource(audioSource);
    }

    private void UpdateHotSound()
    {
        if (hotAudioSource == null || hotSound == null)
        {
            return;
        }

        bool hasPower = rack == null || rack.HasFusePower;
        float hotPart = hasPower && isRunning ? Mathf.InverseLerp(hotSoundStartHeat, overheatAt, heat) : 0f;
        hotPart = Mathf.Clamp01(hotPart);
        hotFade = Mathf.MoveTowards(hotFade, isRunning ? 1f : 0f, loopFadeSpeed * Time.deltaTime);

        if (hotPart <= 0.001f)
        {
            FadeOutSource(hotAudioSource);
            return;
        }

        PlayLoop(hotAudioSource, hotSound);
        hotAudioSource.volume = hotVolume * hotPart * hotFade;
        hotAudioSource.pitch = Mathf.Lerp(0.9f, 1.18f, hotPart);

        if (audioSource != null && audioSource.clip == humSound)
        {
            audioSource.volume = Mathf.Lerp(humVolume, humVolume * 0.65f, hotPart) * humFade;
            audioSource.pitch = Mathf.Lerp(humPitch, humPitch + 0.14f, hotPart);
        }
    }

    private void PlayLoop(AudioSource source, AudioClip clip)
    {
        if (source.clip == clip && source.isPlaying)
        {
            return;
        }

        source.clip = clip;
        source.loop = true;
        source.volume = 0f;

        if (clip == humSound)
        {
            humPitch = Random.Range(0.94f, 1.02f);
        }

        source.Play();
    }

    private void FadeOutSource(AudioSource source)
    {
        if (source == null || !source.isPlaying)
        {
            return;
        }

        source.volume = Mathf.MoveTowards(source.volume, 0f, loopFadeSpeed * Time.deltaTime);
        if (source.volume <= 0.001f)
        {
            source.Stop();
            source.clip = null;
        }
    }

    private void MakeDiodeMaterials()
    {
        int count = diode != null ? 1 : 0;
        count += extraDiodes != null ? extraDiodes.Length : 0;

        diodeMaterials = new Material[count];
        int index = 0;

        if (diode != null)
        {
            diodeMaterials[index] = diode.material;
            diodeMaterials[index].EnableKeyword("_EMISSION");
            index++;
        }

        if (extraDiodes == null)
        {
            return;
        }

        for (int i = 0; i < extraDiodes.Length; i++)
        {
            if (extraDiodes[i] == null)
            {
                continue;
            }

            diodeMaterials[index] = extraDiodes[i].material;
            diodeMaterials[index].EnableKeyword("_EMISSION");
            index++;
        }
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (audioSource == null || clip == null)
        {
            return;
        }

        audioSource.pitch = Random.Range(0.92f, 1.08f);
        audioSource.PlayOneShot(clip, volume);
    }

    private static Vector2 Sort(Vector2 value)
    {
        return value.x <= value.y ? value : new Vector2(value.y, value.x);
    }
}
