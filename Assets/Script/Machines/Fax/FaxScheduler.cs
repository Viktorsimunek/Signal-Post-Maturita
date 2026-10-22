using System.Collections.Generic;
using System;
using UnityEngine;

public class FaxScheduler : MonoBehaviour
{
    [SerializeField] private FaxMachine faxMachine;
    [SerializeField] private FuseSwitch powerFuse;
    [SerializeField] private TextAsset scheduleJson;
    [SerializeField] private string resourcesJsonName = "FaxSchedule";
    [SerializeField] private string textureFolder = "Faxes/";

    private FaxMessage[] faxes;
    private bool[] sent;
    private bool[] pending;
    private HashSet<string> sentIds = new HashSet<string>();
    private readonly HashSet<string> readyFaxIds = new HashSet<string>();

    public event Action<string> FaxReadyToTake;

    private void Awake()
    {
        EnsureFaxMachine();
        LoadSchedule();
    }

    private void OnEnable()
    {
        EnsureFaxMachine();

        if (faxMachine != null)
        {
            faxMachine.FaxReadyToTake += HandleFaxReadyToTake;
        }
    }

    private void OnDisable()
    {
        if (faxMachine != null)
        {
            faxMachine.FaxReadyToTake -= HandleFaxReadyToTake;
        }
    }

    private void Update()
    {
        if (faxes == null || faxMachine == null || !HasPower())
        {
            return;
        }

        float time = Time.timeSinceLevelLoad;
        for (int i = 0; i < faxes.Length; i++)
        {
            if (!sent[i] && pending[i])
            {
                SendFax(i);
            }
            else if (!sent[i] && IsTimedFaxReady(faxes[i], time))
            {
                SendFax(i);
            }
        }
    }

    public void SendTrigger(string trigger)
    {
        QueueTriggeredFaxes(trigger);
    }

    public void SendFaxById(string id)
    {
        QueueFaxById(id);
    }

    public List<string> QueueTriggeredFaxes(string trigger)
    {
        List<string> triggeredFaxIds = new List<string>();
        if (faxes == null)
        {
            return triggeredFaxIds;
        }

        for (int i = 0; i < faxes.Length; i++)
        {
            if (!sent[i] && faxes[i].trigger == trigger)
            {
                triggeredFaxIds.Add(GetFaxSaveId(faxes[i], i));
                SendFax(i);
            }
        }

        return triggeredFaxIds;
    }

    public string QueueFaxById(string id)
    {
        if (faxes == null)
        {
            return string.Empty;
        }

        for (int i = 0; i < faxes.Length; i++)
        {
            if (!sent[i] && faxes[i].id == id)
            {
                SendFax(i);
                return GetFaxSaveId(faxes[i], i);
            }
        }

        return string.Empty;
    }

    public bool IsFaxReadyToTake(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && readyFaxIds.Contains(id);
    }

    private void LoadSchedule()
    {
        TextAsset json = scheduleJson != null ? scheduleJson : Resources.Load<TextAsset>(resourcesJsonName);
        if (json == null)
        {
            faxes = new FaxMessage[0];
            sent = new bool[0];
            pending = new bool[0];
            return;
        }

        FaxSchedule schedule = JsonUtility.FromJson<FaxSchedule>(json.text);
        faxes = schedule != null && schedule.faxes != null ? schedule.faxes : new FaxMessage[0];
        sent = new bool[faxes.Length];
        pending = new bool[faxes.Length];
        sentIds = new HashSet<string>(GameProgress.LoadBoardFaxIds());
        for (int i = 0; i < faxes.Length; i++)
        {
            if (sentIds.Contains(GetFaxSaveId(faxes[i], i)))
            {
                sent[i] = true;
            }
        }
    }

    private void SendFax(int index)
    {
        if (faxes == null || index < 0 || index >= faxes.Length || sent[index])
        {
            return;
        }

        if (faxMachine == null || !HasPower())
        {
            pending[index] = true;
            return;
        }

        pending[index] = false;
        sent[index] = true;
        FaxMessage fax = faxes[index];

        if (fax.texture == null && !string.IsNullOrWhiteSpace(fax.textureName))
        {
            fax.texture = Resources.Load<Texture2D>(textureFolder + fax.textureName);
        }

        faxMachine.ReceiveFax(fax);
    }

    private static bool IsTimedFaxReady(FaxMessage fax, float time)
    {
        return fax != null &&
            string.IsNullOrWhiteSpace(fax.trigger) &&
            fax.time > 0f &&
            time >= fax.time;
    }

    private void HandleFaxReadyToTake(FaxMessage fax)
    {
        string id = fax != null ? fax.id : string.Empty;
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        readyFaxIds.Add(id);
        FaxReadyToTake?.Invoke(id);
    }

    private void EnsureFaxMachine()
    {
        if (faxMachine == null)
        {
            faxMachine = FindFirstObjectByType<FaxMachine>();
        }
    }

    private static string GetFaxSaveId(FaxMessage fax, int index)
    {
        if (fax != null && !string.IsNullOrWhiteSpace(fax.id))
        {
            return fax.id;
        }

        return "fax_index_" + index;
    }

    private bool HasPower()
    {
        return (powerFuse == null || powerFuse.IsOn) && LightsPowerGrid.IsPowered;
    }
}
