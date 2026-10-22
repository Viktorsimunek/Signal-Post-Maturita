using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LookFocus))]
public class RadioInteractable : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private string prompt = "Use radio";
    [SerializeField] private string noPowerPrompt = "Radio has no power";
    [SerializeField] private Transform focusPoint;

    [Header("Power")]
    [SerializeField] private FuseSwitch powerFuse;

    [Header("Radio UI")]
    [SerializeField] private RadioDialogueUI radioUI;

    private LookFocus lookFocus;

    private void Reset()
    {
        powerFuse = FindFirstObjectByType<FuseSwitch>();
        radioUI = FindFirstObjectByType<RadioDialogueUI>();
        SetupLookFocus();
    }

    private void Awake()
    {
        if (powerFuse == null)
        {
            powerFuse = FindFirstObjectByType<FuseSwitch>();
        }

        SetupLookFocus();
        ApplyPowerState();
    }

    private void OnEnable()
    {
        SetupLookFocus();

        if (lookFocus != null)
        {
            lookFocus.FocusStarted += ShowRadio;
            lookFocus.FocusEnded += HideRadio;
        }

        if (powerFuse != null)
        {
            powerFuse.PowerChanged -= HandlePowerChanged;
            powerFuse.PowerChanged += HandlePowerChanged;
        }

        LightsPowerGrid.PowerChanged -= HandleGridPowerChanged;
        LightsPowerGrid.PowerChanged += HandleGridPowerChanged;

        ApplyPowerState();
    }

    private void OnDisable()
    {
        if (lookFocus != null)
        {
            lookFocus.FocusStarted -= ShowRadio;
            lookFocus.FocusEnded -= HideRadio;
        }

        if (powerFuse != null)
        {
            powerFuse.PowerChanged -= HandlePowerChanged;
        }

        LightsPowerGrid.PowerChanged -= HandleGridPowerChanged;

        HideRadio();
    }

    private void OnValidate()
    {
        SetupLookFocus();
    }

    public void ShowRadio()
    {
        if (!HasPower)
        {
            if (radioUI != null)
            {
                radioUI.SetPowerAvailable(false);
            }

            return;
        }

        if (radioUI != null)
        {
            radioUI.ShowRadio();
        }
    }

    public void HideRadio()
    {
        if (radioUI != null)
        {
            radioUI.HideRadio();
        }
    }

    private void SetupLookFocus()
    {
        if (lookFocus == null)
        {
            lookFocus = GetComponent<LookFocus>();
        }

        if (lookFocus == null)
        {
            return;
        }

        lookFocus.SetPrompt(prompt);
        lookFocus.SetFocusPoint(focusPoint);
        lookFocus.SetPowerGate(() => HasPower, noPowerPrompt, HideRadio);
    }

    private bool HasPower
    {
        get { return (powerFuse == null || powerFuse.IsOn) && LightsPowerGrid.IsPowered; }
    }

    private void HandlePowerChanged(FuseSwitch fuse, bool powered)
    {
        ApplyPowerState();
    }

    private void HandleGridPowerChanged(bool powered)
    {
        ApplyPowerState();
    }

    private void ApplyPowerState()
    {
        if (radioUI != null)
        {
            radioUI.SetPowerAvailable(HasPower);
        }
    }
}
