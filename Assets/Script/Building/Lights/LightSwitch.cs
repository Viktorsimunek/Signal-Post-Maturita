using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LightSwitch : MonoBehaviour, IInteractable
{
    private static readonly List<LightSwitch> AllSwitches = new List<LightSwitch>();

    [Header("Power")]
    [SerializeField] private FuseSwitch powerFuse;

    [Header("Interaction")]
    [SerializeField] private bool canInteract = true;
    [SerializeField] private string interactionPrompt = "Prepnout svetla";

    [Header("Lights")]
    [SerializeField] private Lights[] lights;
    [SerializeField] private bool startOn;

    [Header("Switch Object")]
    [SerializeField] private Transform switchObject;
    [SerializeField] private Vector3 offRotation;
    [SerializeField] private Vector3 onRotation = new Vector3(25f, 0f, 0f);
    [SerializeField] private float rotateSpeed = 12f;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip turnOnSound;
    [SerializeField] private AudioClip turnOffSound;
    [SerializeField] private float volume = 1f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    private bool isOn;

    private void OnEnable()
    {
        if (!AllSwitches.Contains(this))
        {
            AllSwitches.Add(this);
        }

        LightsPowerGrid.PowerChanged -= OnBuildingPowerChanged;
        LightsPowerGrid.PowerChanged += OnBuildingPowerChanged;

        if (powerFuse != null)
        {
            powerFuse.PowerChanged -= OnFuseChanged;
            powerFuse.PowerChanged += OnFuseChanged;
        }
    }

    private void OnDisable()
    {
        AllSwitches.Remove(this);
        LightsPowerGrid.PowerChanged -= OnBuildingPowerChanged;

        if (powerFuse != null)
        {
            powerFuse.PowerChanged -= OnFuseChanged;
        }
    }

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void Start()
    {
        isOn = HasPower() ? GetLightState(startOn) : startOn;
        ApplyState(true);
        UpdateLinkedSwitches();
    }

    private void OnValidate()
    {
        rotateSpeed = Mathf.Max(0f, rotateSpeed);
        volume = Mathf.Max(0f, volume);
        pitchRange = Sort(pitchRange);

        if (!Application.isPlaying && switchObject != null)
        {
            switchObject.localRotation = Quaternion.Euler(startOn ? onRotation : offRotation);
        }
    }

    private void Update()
    {
        if (switchObject == null)
        {
            return;
        }

        Quaternion target = Quaternion.Euler(isOn ? onRotation : offRotation);
        switchObject.localRotation = Quaternion.Lerp(switchObject.localRotation, target, Time.deltaTime * rotateSpeed);
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        return canInteract && isActiveAndEnabled;
    }

    public string GetInteractionPrompt(PlayerInteractor interactor)
    {
        return interactionPrompt;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        Toggle();
    }

    [ContextMenu("Toggle")]
    public void Toggle()
    {
        isOn = !isOn;
        ApplyState(false);
        UpdateLinkedSwitches();
        PlaySound(isOn ? turnOnSound : turnOffSound);
    }

    public void SetState(bool state)
    {
        isOn = state;
        ApplyState(false);
        UpdateLinkedSwitches();
    }

    [ContextMenu("Turn On")]
    public void TurnOn()
    {
        SetState(true);
    }

    [ContextMenu("Turn Off")]
    public void TurnOff()
    {
        SetState(false);
    }

    private void ApplyState(bool instant)
    {
        bool lightsShouldBeOn = isOn && HasPower();

        if (lights != null)
        {
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                {
                    lights[i].SetPowered(lightsShouldBeOn);
                }
            }
        }

        if (instant && switchObject != null)
        {
            switchObject.localRotation = Quaternion.Euler(isOn ? onRotation : offRotation);
        }
    }

    private void SetVisualState(bool state)
    {
        isOn = state;
    }

    private bool GetLightState(bool fallback)
    {
        if (lights == null)
        {
            return fallback;
        }

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null)
            {
                return lights[i].IsPowered;
            }
        }

        return fallback;
    }

    private void UpdateLinkedSwitches()
    {
        for (int i = 0; i < AllSwitches.Count; i++)
        {
            LightSwitch other = AllSwitches[i];
            if (other != null && other != this && SharesLightsWith(other))
            {
                other.SetVisualState(isOn);
            }
        }
    }

    private void OnBuildingPowerChanged(bool powered)
    {
        ApplyState(false);
    }

    private void OnFuseChanged(FuseSwitch fuse, bool powered)
    {
        ApplyState(false);
    }

    private bool SharesLightsWith(LightSwitch other)
    {
        if (other == null || lights == null || other.lights == null)
        {
            return false;
        }

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null)
            {
                continue;
            }

            for (int j = 0; j < other.lights.Length; j++)
            {
                if (lights[i] == other.lights[j])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource == null || clip == null)
        {
            return;
        }

        audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        audioSource.PlayOneShot(clip, volume);
    }

    private bool HasPower()
    {
        return (powerFuse == null || powerFuse.IsOn) && LightsPowerGrid.IsPowered;
    }

    private static Vector2 Sort(Vector2 value)
    {
        return value.x <= value.y ? value : new Vector2(value.y, value.x);
    }
}
