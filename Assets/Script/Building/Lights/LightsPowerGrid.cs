using System;
using UnityEngine;

[DisallowMultipleComponent]
public class LightsPowerGrid : MonoBehaviour
{
    public static event Action<bool> PowerChanged;

    public static bool IsPowered { get; private set; }

    [Header("Power")]
    [SerializeField] private bool startsPowered;
    [SerializeField] private bool applyStateOnEnable = true;

    [Header("Inspector Test")]
    [SerializeField] private bool allowInspectorTesting = true;
    [SerializeField] private bool inspectorPowerState;

    private void OnEnable()
    {
        if (applyStateOnEnable)
        {
            SetPower(startsPowered);
        }
        
        inspectorPowerState = IsPowered;
    }

    [ContextMenu("Power On")]
    public void PowerOn()
    {
        SetPower(true);
    }

    [ContextMenu("Power Off")]
    public void PowerOff()
    {
        SetPower(false);
    }

    [ContextMenu("Toggle Power")]
    public void TogglePower()
    {
        SetPower(!IsPowered);
    }

    public void SetPower(bool powered)
    {
        IsPowered = powered;
        inspectorPowerState = IsPowered;
        PowerChanged?.Invoke(IsPowered);
    }

    private void OnValidate()
    {
        if (!Application.isPlaying || !allowInspectorTesting)
        {
            return;
        }

        if (inspectorPowerState != IsPowered)
        {
            SetPower(inspectorPowerState);
        }
    }
}
