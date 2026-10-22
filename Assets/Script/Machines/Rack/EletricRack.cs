using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class EletricRack : MonoBehaviour
{
    [Header("Radar Power")]
    [SerializeField] private PowerSupplyRack[] powerSupplies;

    [Header("Building Fuses")]
    [SerializeField] private FuseSwitch mainFuse;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip breakerSound;
    [SerializeField] private AudioClip restoreSound;
    [SerializeField] private float volume = 1f;

    [Header("Events")]
    [SerializeField] private UnityEvent onBlackout;
    [SerializeField] private UnityEvent onPowerBack;
    [SerializeField] private UnityEvent onPowerChanged;

    private PowerSupplyRack activeSupply;
    private bool isBlackout;
    private string lastTripReason = "Rack fuse tripped.";
    private bool fuseTripFromRack;

    public bool IsBlackout
    {
        get { return isBlackout; }
    }

    public PowerSupplyRack ActiveSupply
    {
        get { return activeSupply; }
    }

    public bool HasRadarPower
    {
        get { return activeSupply != null && !isBlackout && HasFusePower; }
    }

    public bool HasFusePower
    {
        get { return (mainFuse == null || mainFuse.IsOn) && LightsPowerGrid.IsPowered; }
    }

    private void Reset()
    {
        powerSupplies = GetComponentsInChildren<PowerSupplyRack>(true);
        mainFuse = FindFirstObjectByType<FuseSwitch>();
    }

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (mainFuse == null)
        {
            mainFuse = FindFirstObjectByType<FuseSwitch>();
        }

        SetupSupplies();
        SubscribeFuse();
    }

    private void OnEnable()
    {
        SubscribeFuse();
    }

    private void OnDisable()
    {
        UnsubscribeFuse();
    }

    private void Start()
    {
        int running = 0;
        activeSupply = null;

        if (powerSupplies == null)
        {
            powerSupplies = new PowerSupplyRack[0];
        }

        for (int i = 0; i < powerSupplies.Length; i++)
        {
            PowerSupplyRack supply = powerSupplies[i];
            if (supply == null || !supply.IsRunning)
            {
                continue;
            }

            running++;
            activeSupply = supply;
        }

        if (!HasFusePower)
        {
            ApplyFuseOffState();
            onPowerChanged?.Invoke();
            return;
        }

        if (running > 1)
        {
            TripMainFuse("Two supplies started together.");
        }
    }

    private void OnValidate()
    {
        volume = Mathf.Max(0f, volume);

        if (mainFuse == null)
        {
            mainFuse = FindFirstObjectByType<FuseSwitch>();
        }
    }

    public bool TryTurnOn(PowerSupplyRack supply)
    {
        if (supply == null || isBlackout || !HasFusePower)
        {
            return false;
        }

        if (activeSupply != null && activeSupply != supply)
        {
            TripMainFuse("Second supply connected.");
            return false;
        }

        activeSupply = supply;
        onPowerChanged?.Invoke();
        return true;
    }

    public void TurnOff(PowerSupplyRack supply)
    {
        if (supply != null && supply == activeSupply)
        {
            activeSupply = null;
            onPowerChanged?.Invoke();
        }
    }

    public void SupplyOverheated(PowerSupplyRack supply)
    {
        if (supply != null && supply == activeSupply)
        {
            TripMainFuse("Supply overheated.");
        }
    }

    [ContextMenu("Blackout")]
    public void Blackout()
    {
        TripMainFuse("Manual blackout.");
    }

    private void TripMainFuse(string reason)
    {
        lastTripReason = reason;
        fuseTripFromRack = true;

        if (mainFuse == null)
        {
            mainFuse = FindFirstObjectByType<FuseSwitch>();
            SubscribeFuse();
        }

        if (mainFuse != null)
        {
            if (mainFuse.IsOn)
            {
                mainFuse.TurnOff();
                return;
            }

            HandleFusePowerChanged(mainFuse, false);
            return;
        }

        ApplyFuseOffState();
        fuseTripFromRack = false;
        PlaySound(breakerSound);
        onBlackout?.Invoke();
        onPowerChanged?.Invoke();
        Debug.LogWarning("Eletric rack has no mainFuse assigned, so it can only turn itself off.", this);
        Debug.Log("Eletric rack fuse tripped: " + reason, this);
    }

    private void ApplyFuseOffState()
    {
        isBlackout = true;
        activeSupply = null;

        if (powerSupplies == null)
        {
            return;
        }

        for (int i = 0; i < powerSupplies.Length; i++)
        {
            if (powerSupplies[i] != null)
            {
                powerSupplies[i].ForceOffFromRack();
            }
        }
    }

    private void SetupSupplies()
    {
        if (powerSupplies == null || powerSupplies.Length == 0)
        {
            powerSupplies = GetComponentsInChildren<PowerSupplyRack>(true);
        }

        for (int i = 0; i < powerSupplies.Length; i++)
        {
            if (powerSupplies[i] != null)
            {
                powerSupplies[i].SetRack(this);
            }
        }
    }

    private void SubscribeFuse()
    {
        if (mainFuse != null)
        {
            mainFuse.PowerChanged -= HandleFusePowerChanged;
            mainFuse.PowerChanged += HandleFusePowerChanged;
        }
    }

    private void UnsubscribeFuse()
    {
        if (mainFuse != null)
        {
            mainFuse.PowerChanged -= HandleFusePowerChanged;
        }
    }

    private void HandleFusePowerChanged(FuseSwitch fuse, bool powered)
    {
        if (powered)
        {
            isBlackout = false;
            PlaySound(restoreSound);
            onPowerBack?.Invoke();
            onPowerChanged?.Invoke();
            return;
        }

        string reason = fuseTripFromRack ? lastTripReason : "Fuse switched off.";
        fuseTripFromRack = false;

        ApplyFuseOffState();
        PlaySound(breakerSound);
        onBlackout?.Invoke();
        onPowerChanged?.Invoke();
        Debug.Log("Eletric rack fuse tripped: " + reason, this);
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource == null || clip == null)
        {
            return;
        }

        audioSource.pitch = Random.Range(0.92f, 1.05f);
        audioSource.PlayOneShot(clip, volume);
    }
}
