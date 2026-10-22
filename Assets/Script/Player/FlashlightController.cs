using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class FlashlightController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private Light flashlight;
    [SerializeField] private Transform holdPoint;
    [SerializeField] private InputActionReference toggleActionReference;
    [SerializeField] private AudioSource audioSource;

    [Header("Toggle")]
    [SerializeField] private bool startsEnabled;
    [SerializeField] private float fadeInDuration = 0.16f, fadeOutDuration = 0.12f;
    [SerializeField] private AudioClip switchOnClip, switchOffClip;
    [SerializeField] private float switchVolume = 0.9f;

    [Header("Follow")]
    [SerializeField] private float followSmoothness = 8f, aimDistance = 40f;

    [Header("Handheld")]
    [SerializeField] private float shakeSpeed = 1.4f, positionShake = 0.006f, rotationShake = 0.35f;

    [Header("Flicker")]
    [SerializeField] private bool enableFlicker = true;
    [SerializeField] private float flickerIntensity = 0.025f, flickerSpeed = 7.5f;
    [SerializeField] private float rareBlinkChance = 0.02f, rareBlinkDuration = 0.045f, rareBlinkDrop = 0.3f;

    private InputAction toggleAction;
    private InputAction fallbackToggleAction;
    private Vector3 startLocalPosition;
    private Quaternion startLocalRotation;
    private bool isOn, inputEnabled = true;
    private float baseIntensity, currentIntensity, blinkTimer;

    public bool IsOn => isOn;
    public bool IsInputEnabled => inputEnabled;

    public void Configure(Transform target, Light targetLight) { followTarget = target; flashlight = targetLight; }

    public void SetInputEnabled(bool enabled) => inputEnabled = enabled;

    public void SetFlashlightEnabled(bool enabled, bool immediate)
    {
        isOn = enabled;
        if (!isOn) blinkTimer = 0f;
        if (!immediate) return;

        currentIntensity = isOn ? baseIntensity : 0f;
        ApplyLight(currentIntensity);
    }

    private void Awake()
    {
        if (followTarget == null && Camera.main != null) followTarget = Camera.main.transform;
        if (flashlight == null) flashlight = GetComponentInChildren<Light>(true);
        if (flashlight == null) return;
        if (followTarget == null) followTarget = transform;

        if (audioSource == null) audioSource = flashlight.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = flashlight.gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        baseIntensity = flashlight.intensity;
        currentIntensity = startsEnabled ? baseIntensity : 0f;
        isOn = startsEnabled;

        bool followsAsChild = flashlight.transform.IsChildOf(followTarget);
        startLocalPosition = followsAsChild ? flashlight.transform.localPosition : followTarget.InverseTransformPoint(flashlight.transform.position);
        startLocalRotation = followsAsChild ? flashlight.transform.localRotation : Quaternion.Inverse(followTarget.rotation) * flashlight.transform.rotation;
        if (followsAsChild && holdPoint == null) flashlight.transform.SetParent(null, true);
        toggleAction = GetToggleAction();
        ApplyLight(currentIntensity);
        MoveFlashlight(true);
    }

    private void OnEnable() { toggleAction?.Enable(); fallbackToggleAction?.Enable(); }
    private void OnDisable() { toggleAction?.Disable(); fallbackToggleAction?.Disable(); }

    private void OnDestroy() => fallbackToggleAction?.Dispose();

    private void Update()
    {
        if (inputEnabled && WasTogglePressed())
        {
            isOn = !isOn;
            PlaySound(isOn ? switchOnClip : switchOffClip);
            if (!isOn) blinkTimer = 0f;
        }

        UpdateLight();
    }

    private void LateUpdate() { if (flashlight != null) MoveFlashlight(false); }

    private void MoveFlashlight(bool instant)
    {
        float time = Time.time;
        bool isChildOfTarget = flashlight.transform.IsChildOf(followTarget) && holdPoint == null;

        if (isChildOfTarget)
        {
            MoveLocal(time, instant);
            return;
        }

        MoveWorld(time, instant);
    }

    private void MoveLocal(float time, bool instant)
    {
        Vector3 wantedPosition = startLocalPosition;
        Quaternion wantedRotation = startLocalRotation * Quaternion.Euler(Noise(time, rotationShake, 1.17f));

        if (instant || followSmoothness <= 0f)
        {
            flashlight.transform.localPosition = wantedPosition;
            flashlight.transform.localRotation = wantedRotation;
            return;
        }

        float step = 1f - Mathf.Exp(-followSmoothness * Time.deltaTime);
        flashlight.transform.localPosition = Vector3.Lerp(flashlight.transform.localPosition, wantedPosition, step);
        flashlight.transform.localRotation = Quaternion.Slerp(flashlight.transform.localRotation, wantedRotation, step);
    }

    private void MoveWorld(float time, bool instant)
    {
        Vector3 wantedPosition = GetHoldPosition();
        Vector3 aimPoint = followTarget.position + followTarget.forward * aimDistance;
        Quaternion wantedRotation = Quaternion.LookRotation(aimPoint - wantedPosition, followTarget.up);
        wantedRotation *= Quaternion.Euler(Noise(time, rotationShake, 1.17f));

        if (instant || followSmoothness <= 0f)
        {
            flashlight.transform.SetPositionAndRotation(wantedPosition, wantedRotation);
            return;
        }

        float step = 1f - Mathf.Exp(-followSmoothness * Time.deltaTime);
        flashlight.transform.SetPositionAndRotation(
            Vector3.Lerp(flashlight.transform.position, wantedPosition, step),
            Quaternion.Slerp(flashlight.transform.rotation, wantedRotation, step));
    }

    private void UpdateLight()
    {
        if (flashlight == null) return;

        float target = isOn ? baseIntensity : 0f;
        float duration = isOn ? fadeInDuration : fadeOutDuration;
        float speed = duration <= 0f ? float.MaxValue : Mathf.Max(baseIntensity, 0.01f) / duration;
        currentIntensity = Mathf.MoveTowards(currentIntensity, target, speed * Time.deltaTime);

        float finalIntensity = currentIntensity;
        if (enableFlicker && currentIntensity > 0.001f)
        {
            blinkTimer -= Time.deltaTime;
            if (blinkTimer <= 0f && Random.value < rareBlinkChance * Time.deltaTime) blinkTimer = rareBlinkDuration;

            float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, 0.41f) - 0.5f;
            finalIntensity *= 1f + noise * flickerIntensity * 2f;
            if (blinkTimer > 0f) finalIntensity *= 1f - rareBlinkDrop;
        }

        ApplyLight(Mathf.Max(0f, finalIntensity));
    }

    private bool WasTogglePressed()
    {
        bool keyboard = GameSettings.AllowsKeyboardMouseInput() && GameSettings.WasKeyboardBindingPressed(GameSettings.RebindableAction.Flashlight);
        bool gamepad = GameSettings.AllowsGamepadInput() && GameSettings.WasGamepadBindingPressed(GameSettings.RebindableAction.Flashlight);
        if (keyboard) InputDeviceTracker.MarkKeyboardMouse();
        if (gamepad) InputDeviceTracker.MarkGamepad();
        return keyboard || gamepad;
    }

    private Vector3 GetHoldPosition() => holdPoint != null ? holdPoint.position : followTarget.TransformPoint(startLocalPosition);

    private Vector3 Noise(float time, float amount, float seed)
    {
        float t = time * shakeSpeed;
        float x = Mathf.PerlinNoise(t, seed) - 0.5f;
        float y = Mathf.PerlinNoise(seed, t) - 0.5f;
        float z = Mathf.PerlinNoise(t, seed + 0.56f) - 0.5f;
        return new Vector3(x, y, z) * amount * 2f;
    }

    private InputAction GetToggleAction()
    {
        if (toggleActionReference != null && toggleActionReference.action != null) return toggleActionReference.action;

        PlayerInput playerInput = GetComponentInParent<PlayerInput>();
        InputAction playerAction = playerInput != null ? playerInput.actions?.FindAction("Flashlight", false) : null;
        if (playerAction != null) return playerAction;

        fallbackToggleAction = new InputAction("FlashlightFallback", InputActionType.Button);
        fallbackToggleAction.AddBinding("<Keyboard>/f");
        fallbackToggleAction.AddBinding("<Gamepad>/rightShoulder");
        return fallbackToggleAction;
    }

    private void ApplyLight(float intensity) { flashlight.enabled = intensity > 0.001f; flashlight.intensity = intensity; }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, switchVolume);
    }
}
