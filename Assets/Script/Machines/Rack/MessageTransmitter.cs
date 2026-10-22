using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class MessageTransmitter : MonoBehaviour
{
    [Header("Needed Stuff")]
    [SerializeField] private FuseSwitch powerFuse;
    [SerializeField] private EletricRack electricRack;
    [SerializeField] private SignalQuality signalQuality;
    [SerializeField] private FrequencySet frequencySet;

    [Header("Message")]
    [SerializeField] private string confirmationCode = "7421";
    [SerializeField] private bool ignoreBigSmallLetters = true;
    [SerializeField] private bool trimSpaces = true;

    [Header("Frequency")]
    [SerializeField] private bool failIfFrequencyChanges = true;
    [SerializeField] private float frequencyDriftTolerance = 0.15f;

    [Header("Sending")]
    [SerializeField] private float sendTime = 18f;
    [SerializeField, Range(0f, 1f)] private float neededSignalQuality = 0.7f;
    [SerializeField] private bool failIfSignalDrops = true;
    [SerializeField] private bool failIfPowerDrops = true;

    [Header("Sending Progress Feel")]
    [SerializeField] private bool randomizeProgress = true;
    [SerializeField] private Vector2 progressSpeedRange = new Vector2(0.55f, 1.45f);
    [SerializeField] private Vector2 progressSpeedChangeTime = new Vector2(0.35f, 1.2f);

    [Header("Events")]
    [SerializeField] private UnityEvent onSendStarted;
    [SerializeField] private UnityEvent onSendFinished;
    [SerializeField] private UnityEvent onSendFailed;

    private Coroutine sendRoutine;
    private float progress;
    private string lastStatus = "READY";
    private string lastSentMessage = string.Empty;
    private string lastCode = string.Empty;
    private float lastSendFrequency;

    public event Action<MessageTransmitter> Changed;
    public event Action<MessageTransmitter> SendFinished;

    public bool IsSending => sendRoutine != null;
    public float Progress => progress;
    public string ConfirmationCode => confirmationCode;
    public string LastStatus => lastStatus;
    public string LastSentMessage => lastSentMessage;
    public string LastCode => lastCode;
    public float LastSendFrequency => lastSendFrequency;
    public bool LastSendFinished => !IsSending && progress >= 1f;

    public bool HasPower
    {
        get
        {
            bool fuseOk = powerFuse == null || powerFuse.IsOn;
            bool rackOk = electricRack == null || electricRack.HasRadarPower;
            return fuseOk && rackOk && LightsPowerGrid.IsPowered;
        }
    }

    public bool HasGoodSignal
    {
        get { return signalQuality != null && signalQuality.HasPower && signalQuality.Quality >= neededSignalQuality; }
    }

    public float SignalValue
    {
        get { return signalQuality != null ? signalQuality.Quality : 0f; }
    }

    public float CurrentFrequency
    {
        get { return frequencySet != null && frequencySet.HasPower ? frequencySet.Frequency : 0f; }
    }

    public bool HasStableFrequency => frequencySet != null && frequencySet.HasPower;

    private void Reset()
    {
        electricRack = FindFirstObjectByType<EletricRack>();
        signalQuality = FindFirstObjectByType<SignalQuality>();
        frequencySet = FindFirstObjectByType<FrequencySet>();
    }

    private void Awake()
    {
        if (electricRack == null)
        {
            electricRack = FindFirstObjectByType<EletricRack>();
        }

        if (signalQuality == null)
        {
            signalQuality = FindFirstObjectByType<SignalQuality>();
        }

        if (frequencySet == null)
        {
            frequencySet = FindFirstObjectByType<FrequencySet>();
        }

        sendTime = Mathf.Max(0.1f, sendTime);
        neededSignalQuality = Mathf.Clamp01(neededSignalQuality);
        frequencyDriftTolerance = Mathf.Max(0f, frequencyDriftTolerance);
        ValidateProgressRandomization();
    }

    private void OnValidate()
    {
        sendTime = Mathf.Max(0.1f, sendTime);
        neededSignalQuality = Mathf.Clamp01(neededSignalQuality);
        frequencyDriftTolerance = Mathf.Max(0f, frequencyDriftTolerance);
        ValidateProgressRandomization();
    }

    public bool CanStartSend(out string reason)
    {
        if (IsSending)
        {
            reason = "ALREADY SENDING";
            return false;
        }

        if (!HasPower)
        {
            reason = "NO POWER FROM RACK";
            return false;
        }

        if (!HasGoodSignal)
        {
            reason = "SIGNAL QUALITY LOW";
            return false;
        }

        if (!HasStableFrequency)
        {
            reason = "NO FREQUENCY";
            return false;
        }

        reason = "OK";
        return true;
    }

    public bool TryStartSend(string message)
    {
        return TryStartSend(message, string.Empty);
    }

    public void SetConfirmationCode(string code)
    {
        if (!string.IsNullOrWhiteSpace(code))
        {
            confirmationCode = code;
            Changed?.Invoke(this);
        }
    }

    public bool TryStartSend(string message, string code)
    {
        if (!CanStartSend(out string reason))
        {
            SetStatus(reason);
            onSendFailed?.Invoke();
            return false;
        }

        lastSentMessage = message ?? string.Empty;
        lastCode = code ?? string.Empty;
        lastSendFrequency = CurrentFrequency;

        if (!IsCodeCorrect(lastCode))
        {
            SetStatus("CODE REJECTED");
            onSendFailed?.Invoke();
            return false;
        }

        sendRoutine = StartCoroutine(SendRoutine());
        return true;
    }

    public void CancelSend()
    {
        if (sendRoutine == null)
        {
            return;
        }

        StopCoroutine(sendRoutine);
        sendRoutine = null;
        progress = 0f;
        SetStatus("SEND CANCELED");
        onSendFailed?.Invoke();
    }

    public bool IsCodeCorrect(string code)
    {
        string a = CleanMessage(code);
        string b = CleanMessage(confirmationCode);
        return string.Equals(a, b, ignoreBigSmallLetters ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private IEnumerator SendRoutine()
    {
        progress = 0f;
        SetStatus("SENDING");
        onSendStarted?.Invoke();

        float timer = 0f;
        float speed = 1f;
        float nextSpeedChange = 0f;

        while (progress < 1f)
        {
            if (failIfPowerDrops && !HasPower)
            {
                FailSending("POWER LOST");
                yield break;
            }

            if (failIfSignalDrops && !HasGoodSignal)
            {
                FailSending("SIGNAL LOST");
                yield break;
            }

            if (failIfFrequencyChanges && !IsFrequencyStableForSend())
            {
                FailSending("FREQUENCY LOST");
                yield break;
            }

            timer += Time.deltaTime;

            if (randomizeProgress && timer >= nextSpeedChange)
            {
                speed = UnityEngine.Random.Range(progressSpeedRange.x, progressSpeedRange.y);
                nextSpeedChange = timer + UnityEngine.Random.Range(progressSpeedChangeTime.x, progressSpeedChangeTime.y);
            }

            progress = Mathf.Clamp01(progress + Time.deltaTime / sendTime * speed);
            Changed?.Invoke(this);
            yield return null;
        }

        progress = 1f;
        sendRoutine = null;
        SetStatus("MESSAGE SENT");

        onSendFinished?.Invoke();
        SendFinished?.Invoke(this);
    }

    private void FailSending(string status)
    {
        sendRoutine = null;
        progress = 0f;
        SetStatus(status);
        onSendFailed?.Invoke();
    }

    private void SetStatus(string status)
    {
        lastStatus = status;
        Changed?.Invoke(this);
    }

    private bool IsFrequencyStableForSend()
    {
        if (!HasStableFrequency)
        {
            return false;
        }

        return Mathf.Abs(CurrentFrequency - lastSendFrequency) <= frequencyDriftTolerance;
    }

    private void ValidateProgressRandomization()
    {
        progressSpeedRange = ValidatePositiveRange(progressSpeedRange, 0.01f);
        progressSpeedChangeTime = ValidatePositiveRange(progressSpeedChangeTime, 0.01f);
    }

    private static Vector2 ValidatePositiveRange(Vector2 range, float minimum)
    {
        range.x = Mathf.Max(minimum, range.x);
        range.y = Mathf.Max(minimum, range.y);

        if (range.y < range.x)
        {
            range.y = range.x;
        }

        return range;
    }

    private string CleanMessage(string text)
    {
        if (text == null)
        {
            return string.Empty;
        }

        return trimSpaces ? text.Trim() : text;
    }
}
