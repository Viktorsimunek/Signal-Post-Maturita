using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class TransmissionSequenceController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MessageTransmitter transmitter;
    [SerializeField] private RadioDialogueUI radioUI;
    [SerializeField] private FaxScheduler faxScheduler;

    [Header("Data")]
    [SerializeField] private TextAsset sequenceJson;
    [SerializeField] private string resourcesJsonName = "TransmissionSequence";
    [SerializeField] private bool startOnAwake = true;
    [SerializeField] private string startAtObjectiveId;

    [Header("Message Matching")]
    [SerializeField] private bool ignoreBigSmallLetters = true;
    [SerializeField] private bool trimSpaces = true;
    [SerializeField, Min(0)] private int defaultAllowedMessageMistakes = 2;

    [Header("Sequence")]
    [SerializeField] private bool allowOutOfOrderObjectiveCompletion = true;

    [Header("Radio Calls")]
    [SerializeField, Min(0f)] private float minRadioCallDelay = 10f;
    [SerializeField, Min(0f)] private float maxRadioCallDelay = 20f;
    [SerializeField, Min(0f)] private float minFollowUpRadioCallDelay = 1f;
    [SerializeField, Min(0f)] private float maxFollowUpRadioCallDelay = 3f;

    [Header("Radio Call Failsafe")]
    [SerializeField, Min(0f)] private float objectiveAutoAdvanceDelay = 45f;
    [SerializeField] private bool autoAdvanceIfRadioCallsStall = true;

    [Header("Ending")]
    [SerializeField] private EndingSequenceController endingSequence;
    [SerializeField] private bool waitForEndingSequenceBeforeCompletedEvent = true;

    [Header("Fallback")]
    [SerializeField] private TransmissionObjective[] fallbackObjectives =
    {
        new TransmissionObjective
        {
            id = "intro_test",
            wantedMessage = "CIVIL HARMONY TEST",
            wantedFrequency = 104.6f,
            frequencyTolerance = 0.15f,
            requireCorrectFrequency = true,
            faxTriggerOnComplete = "intro_test_complete"
        }
    };

    [Header("Events")]
    [SerializeField] private UnityEvent onSequenceStarted;
    [SerializeField] private UnityEvent onObjectiveCompleted;
    [SerializeField] private UnityEvent onSequenceCompleted;

    private TransmissionObjective[] objectives;
    private int currentIndex = -1;
    private bool sequenceRunning;
    private bool currentMessageSent;
    private int currentRadioCallsCompleted;
    private int currentRadioCallsStarted;
    private Coroutine radioCallRoutine;
    private Coroutine objectiveFailsafeRoutine;
    private Coroutine objectiveAdvanceRoutine;
    private Coroutine endingRoutine;
    private readonly List<int> usedRadioCallIndices = new List<int>();

    public event Action<TransmissionObjective> ObjectiveCompleted;
    public event Action<TransmissionObjective> EndingStarted;

    public TransmissionObjective CurrentObjective
    {
        get
        {
            if (objectives == null || currentIndex < 0 || currentIndex >= objectives.Length)
            {
                return null;
            }

            return objectives[currentIndex];
        }
    }

    private void Awake()
    {
        if (transmitter == null)
        {
            transmitter = FindFirstObjectByType<MessageTransmitter>();
        }

        if (faxScheduler == null)
        {
            faxScheduler = FindFirstObjectByType<FaxScheduler>();
        }

        if (radioUI == null)
        {
            radioUI = FindFirstObjectByType<RadioDialogueUI>();
        }

        if (endingSequence == null)
        {
            endingSequence = FindFirstObjectByType<EndingSequenceController>();
        }

        LoadSequence();
    }

    private void OnEnable()
    {
        if (transmitter != null)
        {
            transmitter.SendFinished += HandleSendFinished;
        }

        if (radioUI != null)
        {
            radioUI.PersonCompleted += HandleRadioPersonCompleted;
        }

        if (startOnAwake)
        {
            StartSequence();
        }
    }

    private void OnDisable()
    {
        if (transmitter != null)
        {
            transmitter.SendFinished -= HandleSendFinished;
        }

        if (radioUI != null)
        {
            radioUI.PersonCompleted -= HandleRadioPersonCompleted;
        }

        StopRadioCall();
        StopObjectiveFailsafe();
        StopObjectiveWait();
        StopEnding();
    }

    public void StartSequence()
    {
        if (objectives == null || objectives.Length == 0 || transmitter == null)
        {
            return;
        }

        if (GameProgress.HasSave && GameProgress.TransmissionCompleted)
        {
            currentIndex = Mathf.Clamp(GameProgress.TransmissionIndex, 0, objectives.Length - 1);
            sequenceRunning = false;
            RestoreFaxes();
            return;
        }

        currentIndex = GameProgress.HasSave
            ? Mathf.Clamp(GameProgress.TransmissionIndex, 0, objectives.Length - 1)
            : GetStartIndex();
        currentIndex = FixSaveIndex(currentIndex);
        sequenceRunning = currentIndex < objectives.Length;
        StopObjectiveWait();
        ResetObjectiveState();
        RestoreFaxes();
        SetTerminalCode();
        onSequenceStarted?.Invoke();
    }

    public void StopSequence()
    {
        sequenceRunning = false;
        StopRadioCall();
        StopObjectiveFailsafe();
        StopObjectiveWait();
        StopEnding();
    }

    public void RestartSequence()
    {
        currentIndex = -1;
        StartSequence();
    }

    public void StartDebugEnding()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        StopObjectiveWait();
        FinishSequence();
    }

    private void HandleSendFinished(MessageTransmitter source)
    {
        if (!sequenceRunning || objectiveAdvanceRoutine != null || source != transmitter)
        {
            return;
        }

        TransmissionObjective objective = CurrentObjective;
        if (objective == null)
        {
            return;
        }

        if (!MessageFitsObjective(objective, source))
        {
            TryLaterObjective(source);
            return;
        }

        currentMessageSent = true;
        CheckObjective();
        ScheduleObjectiveFailsafe();
        ScheduleRadioCall();
    }

    private bool TryLaterObjective(MessageTransmitter source)
    {
        if (!allowOutOfOrderObjectiveCompletion || objectives == null)
        {
            return false;
        }

        int startIndex = Mathf.Max(currentIndex + 1, 0);
        for (int i = startIndex; i < objectives.Length; i++)
        {
            TransmissionObjective objective = objectives[i];
            if (objective == null || !MessageFitsObjective(objective, source))
            {
                continue;
            }

            StopRadioCall();
            StopObjectiveWait();
            currentIndex = i;
            sequenceRunning = true;
            currentMessageSent = true;
            currentRadioCallsCompleted = objective.requiredRadioCalls;
            currentRadioCallsStarted = objective.requiredRadioCalls;
            usedRadioCallIndices.Clear();
            FinishObjective(objective);
            return true;
        }

        return false;
    }

    private void HandleRadioPersonCompleted(string personId)
    {
        if (!sequenceRunning || objectiveAdvanceRoutine != null || CurrentObjective == null)
        {
            return;
        }

        currentRadioCallsCompleted++;
        CheckObjective();
        ScheduleRadioCall();
    }

    private void CheckObjective()
    {
        TransmissionObjective completed = CurrentObjective;
        if (completed == null || !ObjectiveDone(completed))
        {
            return;
        }

        FinishObjective(completed);
    }

    private bool ObjectiveDone(TransmissionObjective objective)
    {
        return currentMessageSent && currentRadioCallsCompleted >= objective.requiredRadioCalls;
    }

    private void FinishObjective(TransmissionObjective completed)
    {
        StopObjectiveFailsafe();
        GameProgress.MarkTransmissionObjectiveCompleted(completed.id);
        List<string> completionFaxIds = SendFaxes(completed);
        onObjectiveCompleted?.Invoke();
        ObjectiveCompleted?.Invoke(completed);

        if (completed.endSequence || currentIndex >= objectives.Length - 1)
        {
            FinishSequence();
            return;
        }

        if (completionFaxIds.Count > 0)
        {
            sequenceRunning = false;
            StopRadioCall();
            objectiveAdvanceRoutine = StartCoroutine(WaitForFaxesThenNext(completionFaxIds));
            return;
        }

        NextObjective();
    }

    private void NextObjective()
    {
        currentIndex++;
        sequenceRunning = currentIndex < objectives.Length;
        ResetObjectiveState();
        GameProgress.SaveTransmissionProgress(currentIndex, false);
        SetTerminalCode();
    }

    private void FinishSequence()
    {
        sequenceRunning = false;
        StopRadioCall();
        StopObjectiveFailsafe();
        GameProgress.SaveTransmissionProgress(currentIndex, true);
        EndingStarted?.Invoke(CurrentObjective);

        EndingSequenceController resolvedEnding = GetEnding();
        if (resolvedEnding == null)
        {
            onSequenceCompleted?.Invoke();
            return;
        }

        if (!waitForEndingSequenceBeforeCompletedEvent)
        {
            resolvedEnding.Play();
            onSequenceCompleted?.Invoke();
            return;
        }

        endingRoutine = StartCoroutine(CompleteAfterEndingSequence());
    }

    private IEnumerator CompleteAfterEndingSequence()
    {
        EndingSequenceController resolvedEnding = GetEnding();
        if (resolvedEnding != null)
        {
            yield return resolvedEnding.PlayRoutine();
        }

        endingRoutine = null;
        onSequenceCompleted?.Invoke();
    }

    private EndingSequenceController GetEnding()
    {
        if (endingSequence != null)
        {
            return endingSequence;
        }

        endingSequence = FindFirstObjectByType<EndingSequenceController>();
        return endingSequence;
    }

    private void SetTerminalCode()
    {
        TransmissionObjective objective = CurrentObjective;
        if (objective == null)
        {
            return;
        }

        transmitter.SetConfirmationCode(objective.confirmationCode);
    }

    private List<string> SendFaxes(TransmissionObjective objective)
    {
        List<string> faxIds = new List<string>();
        if (faxScheduler == null)
        {
            return faxIds;
        }

        if (!string.IsNullOrWhiteSpace(objective.faxIdOnComplete))
        {
            string faxId = faxScheduler.QueueFaxById(objective.faxIdOnComplete);
            if (!string.IsNullOrWhiteSpace(faxId))
            {
                faxIds.Add(faxId);
            }
        }

        if (!string.IsNullOrWhiteSpace(objective.faxTriggerOnComplete))
        {
            faxIds.AddRange(faxScheduler.QueueTriggeredFaxes(objective.faxTriggerOnComplete));
        }

        return faxIds;
    }

    private void RestoreFaxes()
    {
        if (!GameProgress.HasSave || faxScheduler == null || objectives == null)
        {
            return;
        }

        HashSet<string> completedObjectiveIds = GameProgress.LoadCompletedTransmissionObjectiveIds();
        if (completedObjectiveIds.Count == 0)
        {
            return;
        }

        int completedBeforeCurrent = Mathf.Clamp(currentIndex, 0, objectives.Length);

        List<string> currentCompletionFaxIds = new List<string>();
        bool shouldAdvanceCompletedCurrentObjective = false;

        for (int i = 0; i < objectives.Length; i++)
        {
            TransmissionObjective objective = objectives[i];
            if (objective == null)
            {
                continue;
            }

            bool completedObjective = completedObjectiveIds.Contains(objective.id);
            if (completedObjective)
            {
                List<string> faxIds = SendFaxes(objective);
                if (i >= completedBeforeCurrent && i == currentIndex)
                {
                    currentCompletionFaxIds.AddRange(faxIds);
                    shouldAdvanceCompletedCurrentObjective = true;
                }
            }
        }

        if (!shouldAdvanceCompletedCurrentObjective || GameProgress.TransmissionCompleted)
        {
            return;
        }

        sequenceRunning = false;
        StopRadioCall();

        if (currentCompletionFaxIds.Count > 0)
        {
            objectiveAdvanceRoutine = StartCoroutine(WaitForFaxesThenNext(currentCompletionFaxIds));
            return;
        }

        NextObjective();
    }

    private int FixSaveIndex(int savedIndex)
    {
        if (!GameProgress.HasSave || GameProgress.TransmissionCompleted || objectives == null)
        {
            return savedIndex;
        }

        HashSet<string> completedObjectiveIds = GameProgress.LoadCompletedTransmissionObjectiveIds();
        if (completedObjectiveIds.Count == 0)
        {
            return savedIndex;
        }

        for (int i = 0; i < objectives.Length; i++)
        {
            TransmissionObjective objective = objectives[i];
            if (objective != null && !completedObjectiveIds.Contains(objective.id))
            {
                return Mathf.Min(savedIndex, i);
            }
        }

        return savedIndex;
    }

    private void LoadSequence()
    {
        TextAsset json = sequenceJson != null ? sequenceJson : Resources.Load<TextAsset>(resourcesJsonName);
        if (json != null)
        {
            TransmissionSequence sequence = JsonUtility.FromJson<TransmissionSequence>(json.text);
            if (sequence != null && sequence.objectives != null && sequence.objectives.Length > 0)
            {
                objectives = sequence.objectives;
                FixObjectives();
                return;
            }
        }

        objectives = fallbackObjectives != null ? fallbackObjectives : new TransmissionObjective[0];
        FixObjectives();
    }

    private void OnValidate()
    {
        defaultAllowedMessageMistakes = Mathf.Max(0, defaultAllowedMessageMistakes);
        minRadioCallDelay = Mathf.Max(0f, minRadioCallDelay);
        maxRadioCallDelay = Mathf.Max(minRadioCallDelay, maxRadioCallDelay);
        minFollowUpRadioCallDelay = Mathf.Max(0f, minFollowUpRadioCallDelay);
        maxFollowUpRadioCallDelay = Mathf.Max(minFollowUpRadioCallDelay, maxFollowUpRadioCallDelay);
        objectiveAutoAdvanceDelay = Mathf.Max(0f, objectiveAutoAdvanceDelay);
        FixObjectives();
    }

    private void FixObjectives()
    {
        if (objectives == null)
        {
            return;
        }

        for (int i = 0; i < objectives.Length; i++)
        {
            TransmissionObjective objective = objectives[i];
            if (objective == null)
            {
                continue;
            }

            objective.frequencyTolerance = Mathf.Max(0f, objective.frequencyTolerance);
            objective.allowedMessageMistakes = Mathf.Max(-1, objective.allowedMessageMistakes);
            objective.requiredRadioCalls = Mathf.Max(0, objective.requiredRadioCalls);

            int validPersonCount = CountRadioPeople(objective);
            if (objective.requiredRadioCalls == 0 && objective.radioCallPersonIds != null)
            {
                objective.requiredRadioCalls = validPersonCount;
            }

            objective.requiredRadioCalls = Mathf.Min(objective.requiredRadioCalls, validPersonCount);
        }
    }

    private static int CountRadioPeople(TransmissionObjective objective)
    {
        if (objective == null || objective.radioCallPersonIds == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < objective.radioCallPersonIds.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(objective.radioCallPersonIds[i]))
            {
                count++;
            }
        }

        return count;
    }

    private void ResetObjectiveState()
    {
        currentMessageSent = false;
        currentRadioCallsCompleted = 0;
        currentRadioCallsStarted = 0;
        usedRadioCallIndices.Clear();
        StopRadioCall();
        StopObjectiveFailsafe();
    }

    private void ScheduleObjectiveFailsafe()
    {
        TransmissionObjective objective = CurrentObjective;
        if (!autoAdvanceIfRadioCallsStall ||
            objectiveAutoAdvanceDelay <= 0f ||
            objectiveFailsafeRoutine != null ||
            objective == null ||
            !currentMessageSent ||
            ObjectiveDone(objective))
        {
            return;
        }

        objectiveFailsafeRoutine = StartCoroutine(AutoAdvanceObjectiveAfterDelay(currentIndex, objectiveAutoAdvanceDelay));
    }

    private IEnumerator AutoAdvanceObjectiveAfterDelay(int objectiveIndex, float delay)
    {
        yield return new WaitForSeconds(delay);
        objectiveFailsafeRoutine = null;

        TransmissionObjective objective = CurrentObjective;
        if (!sequenceRunning ||
            currentIndex != objectiveIndex ||
            objective == null ||
            !currentMessageSent ||
            ObjectiveDone(objective))
        {
            yield break;
        }

        currentRadioCallsCompleted = objective.requiredRadioCalls;
        currentRadioCallsStarted = Mathf.Max(currentRadioCallsStarted, objective.requiredRadioCalls);
        StopRadioCall();
        CheckObjective();
    }

    private void ScheduleRadioCall()
    {
        TransmissionObjective objective = CurrentObjective;
        if (!CanStartRadioCall(objective) || radioCallRoutine != null)
        {
            return;
        }

        int objectiveIndex = currentIndex;
        float delay = GetRadioDelay();
        radioCallRoutine = StartCoroutine(RadioCallAfterDelay(objectiveIndex, delay));
    }

    private float GetRadioDelay()
    {
        if (currentRadioCallsStarted <= 0)
        {
            return UnityEngine.Random.Range(minRadioCallDelay, maxRadioCallDelay);
        }

        return UnityEngine.Random.Range(minFollowUpRadioCallDelay, maxFollowUpRadioCallDelay);
    }

    private IEnumerator RadioCallAfterDelay(int objectiveIndex, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        while (sequenceRunning && currentIndex == objectiveIndex && radioUI != null && radioUI.HasActivePerson)
        {
            yield return null;
        }

        radioCallRoutine = null;

        TransmissionObjective objective = CurrentObjective;
        if (!CanStartRadioCall(objective) || currentIndex != objectiveIndex)
        {
            yield break;
        }

        string personId = PickRadioPerson(objective);
        currentRadioCallsStarted++;

        if (!string.IsNullOrWhiteSpace(personId))
        {
            radioUI.PersonCame(personId);
            if (sequenceRunning && currentIndex == objectiveIndex && !radioUI.HasActivePerson)
            {
                currentRadioCallsCompleted++;
                CheckObjective();
                ScheduleRadioCall();
            }
        }
        else if (sequenceRunning && currentIndex == objectiveIndex)
        {
            currentRadioCallsCompleted++;
            CheckObjective();
            ScheduleRadioCall();
        }
    }

    private bool CanStartRadioCall(TransmissionObjective objective)
    {
        return sequenceRunning &&
            currentMessageSent &&
            objective != null &&
            radioUI != null &&
            !radioUI.HasActivePerson &&
            objective.radioCallPersonIds != null &&
            currentRadioCallsStarted < objective.requiredRadioCalls &&
            currentRadioCallsStarted < objective.radioCallPersonIds.Length;
    }

    private string PickRadioPerson(TransmissionObjective objective)
    {
        int availableCount = 0;

        for (int i = 0; i < objective.radioCallPersonIds.Length; i++)
        {
            if (!usedRadioCallIndices.Contains(i) && !string.IsNullOrWhiteSpace(objective.radioCallPersonIds[i]))
            {
                availableCount++;
            }
        }

        if (availableCount == 0)
        {
            return string.Empty;
        }

        int pickedAvailableIndex = UnityEngine.Random.Range(0, availableCount);

        for (int i = 0; i < objective.radioCallPersonIds.Length; i++)
        {
            if (usedRadioCallIndices.Contains(i) || string.IsNullOrWhiteSpace(objective.radioCallPersonIds[i]))
            {
                continue;
            }

            if (pickedAvailableIndex == 0)
            {
                usedRadioCallIndices.Add(i);
                return objective.radioCallPersonIds[i];
            }

            pickedAvailableIndex--;
        }

        return string.Empty;
    }

    private void StopRadioCall()
    {
        if (radioCallRoutine == null)
        {
            return;
        }

        StopCoroutine(radioCallRoutine);
        radioCallRoutine = null;
    }

    private void StopObjectiveFailsafe()
    {
        if (objectiveFailsafeRoutine == null)
        {
            return;
        }

        StopCoroutine(objectiveFailsafeRoutine);
        objectiveFailsafeRoutine = null;
    }

    private IEnumerator WaitForFaxesThenNext(List<string> faxIds)
    {
        while (!FaxesReady(faxIds))
        {
            yield return null;
        }

        objectiveAdvanceRoutine = null;
        NextObjective();
    }

    private bool FaxesReady(List<string> faxIds)
    {
        if (faxScheduler == null || faxIds == null || faxIds.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < faxIds.Count; i++)
        {
            if (!faxScheduler.IsFaxReadyToTake(faxIds[i]))
            {
                return false;
            }
        }

        return true;
    }

    private void StopObjectiveWait()
    {
        if (objectiveAdvanceRoutine == null)
        {
            return;
        }

        StopCoroutine(objectiveAdvanceRoutine);
        objectiveAdvanceRoutine = null;
    }

    private void StopEnding()
    {
        if (endingRoutine != null)
        {
            StopCoroutine(endingRoutine);
            endingRoutine = null;
        }

        if (endingSequence != null)
        {
            endingSequence.Stop();
        }
    }

    private int GetStartIndex()
    {
        if (!string.IsNullOrWhiteSpace(startAtObjectiveId))
        {
            for (int i = 0; i < objectives.Length; i++)
            {
                if (objectives[i] != null && objectives[i].id == startAtObjectiveId)
                {
                    return i;
                }
            }
        }

        return 0;
    }

    private bool MessageFitsObjective(TransmissionObjective objective, MessageTransmitter source)
    {
        if (!TextMatches(source.LastSentMessage, objective))
        {
            return false;
        }

        if (!objective.requireCorrectFrequency)
        {
            return true;
        }

        return Mathf.Abs(source.LastSendFrequency - objective.wantedFrequency) <= objective.frequencyTolerance;
    }

    private bool TextMatches(string sentMessage, TransmissionObjective objective)
    {
        string wantedMessage = objective != null ? objective.wantedMessage : string.Empty;
        string sent = CleanText(sentMessage);
        string wanted = CleanText(wantedMessage);

        if (ignoreBigSmallLetters)
        {
            sent = sent.ToUpperInvariant();
            wanted = wanted.ToUpperInvariant();
        }

        if (string.Equals(sent, wanted, StringComparison.Ordinal))
        {
            return true;
        }

        int allowedMistakes = objective != null && objective.allowedMessageMistakes >= 0
            ? objective.allowedMessageMistakes
            : defaultAllowedMessageMistakes;

        return CountMistakes(sent, wanted, allowedMistakes) <= allowedMistakes;
    }

    private static int CountMistakes(string a, string b, int maxDistance)
    {
        if (Mathf.Abs(a.Length - b.Length) > maxDistance)
        {
            return maxDistance + 1;
        }

        int[] previous = new int[b.Length + 1];
        int[] current = new int[b.Length + 1];

        for (int j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            int rowMinimum = current[0];

            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Mathf.Min(
                    Mathf.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
                rowMinimum = Mathf.Min(rowMinimum, current[j]);
            }

            if (rowMinimum > maxDistance)
            {
                return maxDistance + 1;
            }

            int[] swap = previous;
            previous = current;
            current = swap;
        }

        return previous[b.Length];
    }

    private string CleanText(string text)
    {
        if (text == null)
        {
            return string.Empty;
        }

        return trimSpaces ? text.Trim() : text;
    }
}

[Serializable]
public class TransmissionSequence
{
    public TransmissionObjective[] objectives;
}

[Serializable]
public class TransmissionObjective
{
    public string id;
    [TextArea(2, 5)] public string wantedMessage;
    public int allowedMessageMistakes = -1;
    public string confirmationCode;
    public float wantedFrequency;
    public bool requireCorrectFrequency = true;
    public float frequencyTolerance = 0.15f;
    public string faxTriggerOnComplete;
    public string faxIdOnComplete;
    public int requiredRadioCalls;
    public string[] radioCallPersonIds;
    public bool endSequence;
}
