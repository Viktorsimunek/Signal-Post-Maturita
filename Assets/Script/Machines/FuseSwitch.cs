using UnityEngine;
using UnityEngine.Events;

public class FuseSwitch : MonoBehaviour, IInteractable
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Interact")]
    [SerializeField] private bool canInteract = true;
    [SerializeField] private string turnOnText = "Nahodit pojistku";
    [SerializeField] private string turnOffText = "Shodit pojistku";

    [Header("Power")]
    [SerializeField] private bool startOn = true;
    [SerializeField] private bool isOn;
    [SerializeField] private LightsPowerGrid lightsPowerGrid;
    [SerializeField] private bool controlBuildingLights = true;

    [Header("Light")]
    [SerializeField] private Renderer diode;
    [SerializeField] private Light diodeLight;
    [SerializeField] private Color offColor = Color.red;
    [SerializeField] private Color onColor = Color.green;
    [SerializeField] private float emissionPower = 2f;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip switchOnSound;
    [SerializeField] private AudioClip switchOffSound;
    [SerializeField] private float volume = 0.8f;

    [Header("Events")]
    [SerializeField] private UnityEvent onTurnOn;
    [SerializeField] private UnityEvent onTurnOff;
    [SerializeField] private UnityEvent onPowerChanged;

    private Material diodeMaterial;

    public event System.Action<FuseSwitch, bool> PowerChanged;

    public bool IsOn
    {
        get { return isOn; }
    }

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (lightsPowerGrid == null)
        {
            lightsPowerGrid = FindFirstObjectByType<LightsPowerGrid>();
        }

        if (diode != null)
        {
            diodeMaterial = diode.material;
            diodeMaterial.EnableKeyword("_EMISSION");
        }

        isOn = startOn;
        SetBuildingLights();
        UpdateDiode();
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        return canInteract;
    }

    public string GetInteractionPrompt(PlayerInteractor interactor)
    {
        return isOn ? turnOffText : turnOnText;
    }

    public void Interact(PlayerInteractor interactor)
    {
        Toggle();
    }

    [ContextMenu("Toggle")]
    public void Toggle()
    {
        SetPower(!isOn);
    }

    [ContextMenu("Turn On")]
    public void TurnOn()
    {
        SetPower(true);
    }

    [ContextMenu("Turn Off")]
    public void TurnOff()
    {
        SetPower(false);
    }

    public void SetPower(bool value)
    {
        if (isOn == value)
        {
            return;
        }

        isOn = value;

        SetBuildingLights();
        UpdateDiode();
        PlaySound();

        if (isOn)
        {
            onTurnOn?.Invoke();
        }
        else
        {
            onTurnOff?.Invoke();
        }

        onPowerChanged?.Invoke();
        PowerChanged?.Invoke(this, isOn);
    }

    private void SetBuildingLights()
    {
        if (lightsPowerGrid == null)
        {
            lightsPowerGrid = FindFirstObjectByType<LightsPowerGrid>();
        }

        if (controlBuildingLights && lightsPowerGrid != null)
        {
            lightsPowerGrid.SetPower(isOn);
        }
    }

    private void UpdateDiode()
    {
        Color color = isOn ? onColor : offColor;

        if (diodeMaterial != null)
        {
            if (diodeMaterial.HasProperty(BaseColorId))
            {
                diodeMaterial.SetColor(BaseColorId, color);
            }
            else if (diodeMaterial.HasProperty(ColorId))
            {
                diodeMaterial.SetColor(ColorId, color);
            }

            if (diodeMaterial.HasProperty(EmissionColorId))
            {
                diodeMaterial.SetColor(EmissionColorId, color * emissionPower);
            }
        }

        if (diodeLight != null)
        {
            diodeLight.enabled = true;
            diodeLight.color = color;
        }
    }

    private void PlaySound()
    {
        if (audioSource == null)
        {
            return;
        }

        AudioClip clip = isOn ? switchOnSound : switchOffSound;
        if (clip == null)
        {
            return;
        }

        audioSource.pitch = Random.Range(0.95f, 1.05f);
        audioSource.PlayOneShot(clip, volume);
    }
}
