using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class Buttons : MonoBehaviour
{
    public enum ButtonType
    {
        Allow,
        Deny
    }

    [Header("Power")]
    [SerializeField] private FuseSwitch powerFuse;
    [SerializeField] private string noPowerPrompt = "Bez proudu";

    [Header("Buttons")]
    [SerializeField] private Transform allowButton;
    [SerializeField] private Transform denyButton;
    [SerializeField] private string allowPrompt = "Povolit";
    [SerializeField] private string denyPrompt = "Zamitnout";

    [Header("Button Lights")]
    [SerializeField] private Light allowLight;
    [SerializeField] private Light denyLight;
    [SerializeField, Min(0f)] private float litIntensity = 1.6f;

    [Header("Movement")]
    [SerializeField] private Vector3 pressedOffset = new Vector3(0f, -0.03f, 0f);
    [SerializeField] private float pressTime = 0.08f;
    [SerializeField] private float cooldownTime = 0.5f;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip buttonSound;
    [SerializeField] private AudioClip allowSound;
    [SerializeField] private AudioClip denySound;

    [Header("Events")]
    [SerializeField] private UnityEvent onAllow;
    [SerializeField] private UnityEvent onDeny;

    public event UnityAction<ButtonType> ButtonPressed;

    private bool isPressed;
    private bool allowDecisionActive;
    private bool denyDecisionActive;
    private float allowLightStartIntensity;
    private float denyLightStartIntensity;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        SetupButton(allowButton, ButtonType.Allow);
        SetupButton(denyButton, ButtonType.Deny);
        CacheLightIntensity(allowLight, ref allowLightStartIntensity);
        CacheLightIntensity(denyLight, ref denyLightStartIntensity);
        SetDecisionLights(false);
    }

    private void SetupButton(Transform button, ButtonType type)
    {
        if (button == null)
        {
            return;
        }

        ButtonHitbox hitbox = button.GetComponent<ButtonHitbox>();
        if (hitbox == null)
        {
            hitbox = button.gameObject.AddComponent<ButtonHitbox>();
        }

        if (button.GetComponent<Collider>() == null)
        {
            button.gameObject.AddComponent<BoxCollider>();
        }

        hitbox.Setup(this, type);
    }

    public bool CanPress()
    {
        return CanPress(ButtonType.Allow) || CanPress(ButtonType.Deny);
    }

    public bool CanPress(ButtonType type)
    {
        return HasPower() && !isPressed && IsDecisionActive(type);
    }

    public bool CanInteractWithButton()
    {
        return !isPressed;
    }

    public bool CanInteractWithButton(ButtonType type)
    {
        return !isPressed && IsDecisionActive(type);
    }

    public void SetDecisionLights(bool lit)
    {
        SetDecisionAvailability(lit, lit);
    }

    public void SetDecisionAvailability(bool allowActive, bool denyActive)
    {
        allowDecisionActive = allowActive;
        denyDecisionActive = denyActive;

        SetButtonLight(allowLight, allowLightStartIntensity, allowDecisionActive);
        SetButtonLight(denyLight, denyLightStartIntensity, denyDecisionActive);
    }

    public string GetPrompt(ButtonType type)
    {
        if (!HasPower())
        {
            return noPowerPrompt;
        }

        return type == ButtonType.Allow ? allowPrompt : denyPrompt;
    }

    public void Press(ButtonType type)
    {
        if (!CanPress(type))
        {
            return;
        }

        Transform button = type == ButtonType.Allow ? allowButton : denyButton;
        if (button == null)
        {
            return;
        }

        StartCoroutine(PressButton(button, type));
    }

    private IEnumerator PressButton(Transform button, ButtonType type)
    {
        isPressed = true;
        Vector3 startPos = button.localPosition;
        Vector3 pressPos = startPos + pressedOffset;

        PlaySound(buttonSound);

        yield return StartCoroutine(MoveButton(button, startPos, pressPos));

        if (type == ButtonType.Allow)
        {
            PlaySound(allowSound);
            ButtonPressed?.Invoke(type);
            onAllow?.Invoke();
        }
        else
        {
            PlaySound(denySound);
            ButtonPressed?.Invoke(type);
            onDeny?.Invoke();
        }

        yield return StartCoroutine(MoveButton(button, pressPos, startPos));

        if (cooldownTime > 0f)
        {
            yield return new WaitForSeconds(cooldownTime);
        }

        isPressed = false;
    }

    private IEnumerator MoveButton(Transform button, Vector3 from, Vector3 to)
    {
        if (button == null)
        {
            yield break;
        }

        if (pressTime <= 0f)
        {
            button.localPosition = to;
            yield break;
        }

        float timer = 0f;
        while (timer < pressTime)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / pressTime);
            button.localPosition = Vector3.Lerp(from, to, t);
            yield return null;
        }

        button.localPosition = to;
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private bool HasPower()
    {
        return (powerFuse == null || powerFuse.IsOn) && LightsPowerGrid.IsPowered;
    }

    private bool IsDecisionActive(ButtonType type)
    {
        return type == ButtonType.Allow ? allowDecisionActive : denyDecisionActive;
    }

    private void CacheLightIntensity(Light targetLight, ref float intensity)
    {
        if (targetLight == null)
        {
            return;
        }

        intensity = targetLight.intensity > 0f ? targetLight.intensity : litIntensity;
    }

    private void SetButtonLight(Light targetLight, float startIntensity, bool lit)
    {
        if (targetLight == null)
        {
            return;
        }

        bool shouldLight = lit && HasPower();
        targetLight.enabled = shouldLight;
        targetLight.intensity = shouldLight ? Mathf.Max(startIntensity, litIntensity) : 0f;
    }
}

public class ButtonHitbox : MonoBehaviour, IInteractable
{
    private Buttons panel;
    private Buttons.ButtonType type;

    public void Setup(Buttons owner, Buttons.ButtonType buttonType)
    {
        panel = owner;
        type = buttonType;
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        return panel != null && panel.CanInteractWithButton(type);
    }

    public string GetInteractionPrompt(PlayerInteractor interactor)
    {
        if (panel == null)
        {
            return "Button";
        }

        return panel.GetPrompt(type);
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (panel != null)
        {
            panel.Press(type);
        }
    }
}
