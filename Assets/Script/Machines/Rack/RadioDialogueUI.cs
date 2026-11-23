using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RadioEffects))]
public class RadioDialogueUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private Transform answerButtonPanel;
    [SerializeField] private Transform buttonParent;
    [SerializeField] private Button answerButtonPrefab;
    [SerializeField] private Buttons decisionButtons;

    [Header("Data")]
    [SerializeField] private string peopleFolder = "RadioPeople/";
    [SerializeField] private string tuneJsonFileName = "RadioStations";
    [SerializeField, Range(0f, 1f)] private float normalStationChance = 0.25f;
    [SerializeField] private string debugPersonId = "farMan";
    [SerializeField] private string incomingText = "Incoming Transmission";
    [SerializeField] private string noHelpText = "No one needs my assistance right now.";
    [SerializeField] private string noPowerText = "Radio is dead. No power.";
    [SerializeField] private string powerRestoredText = "You're back. I thought the line had gone dead.";
    [SerializeField] private string defaultTalkButtonText = "Tune around";
    [SerializeField] private string waitingForDecisionText = "Waiting for your decision...";
    [SerializeField] private string earlyAcceptReplyText = "What? You are not even going to verify me?";

    private RadioEffects effects;
    private RadioPerson currentPerson;
    private RadioStationData stationData;
    private string currentPersonId = "";
    private string currentNodeId = "";
    private RadioAnswer acceptAnswer;
    private RadioAnswer rejectAnswer;
    private Action resumeDialogue;
    private bool radioVisible;
    private bool hasPower = true;
    private bool interruptedByPowerLoss;
    private bool callStarted;
    private bool answered;
    private bool waitingForDecision;
    private int stationIndex;

    public event Action<string> PersonCompleted;
    public event Action<string> PersonStarted;
    public bool HasActivePerson => currentPerson != null;
    public bool IsSpeaking => effects != null && effects.IsSpeaking;
    public string CurrentVoice => currentPerson != null ? currentPerson.voice : "";
    public AudioClip CurrentVoiceClip => effects != null ? effects.GetVoice(CurrentVoice) : null;
    private Transform ButtonParent => answerButtonPanel != null ? answerButtonPanel : buttonParent;

    private void Awake()
    {
        effects = GetComponent<RadioEffects>();
        if (decisionButtons == null) decisionButtons = GetComponentInParent<Buttons>();
        if (decisionButtons == null) decisionButtons = GetComponentInChildren<Buttons>();
        if (decisionButtons == null)
        {
            var panels = FindObjectsByType<Buttons>(FindObjectsSortMode.None);
            if (panels.Length == 1) decisionButtons = panels[0];
        }
        var file = Resources.Load<TextAsset>(tuneJsonFileName);
        if (file != null) stationData = JsonUtility.FromJson<RadioStationData>(file.text);
        HideRadio();
    }

    private void OnEnable()
    {
        if (decisionButtons != null) decisionButtons.ButtonPressed += OnDecision;
        ShowStartScreen();
        RefreshSound();
    }

    private void OnDisable()
    {
        if (decisionButtons != null) decisionButtons.ButtonPressed -= OnDecision;
        ResetScreen();
        if (effects != null) effects.SetSound(false, false, false);
    }

    public void ShowRadio()
    {
        radioVisible = true;
        effects.Show(true);
        RefreshSound();
    }

    public void HideRadio()
    {
        radioVisible = false;
        if (effects != null) effects.Show(false);
        RefreshSound();
    }

    public void PersonCame(string personId)
    {
        var file = Resources.Load<TextAsset>(peopleFolder + personId);
        currentPerson = file != null ? JsonUtility.FromJson<RadioPerson>(file.text) : null;
        currentPersonId = personId;
        currentNodeId = "";
        resumeDialogue = null;
        answered = false;
        callStarted = false;
        interruptedByPowerLoss = false;
        if (currentPerson == null) Debug.LogWarning("Radio person JSON not found: " + peopleFolder + personId, this);
        ShowStartScreen();
        RefreshSound();
    }

    public void PersonLeft()
    {
        string completedId = currentPersonId;
        bool completed = HasActivePerson && answered;
        currentPerson = null;
        currentPersonId = "";
        currentNodeId = "";
        resumeDialogue = null;
        answered = false;
        callStarted = false;
        interruptedByPowerLoss = false;
        ShowStartScreen();
        RefreshSound();
        if (completed) PersonCompleted?.Invoke(completedId);
    }

    public void SetPowerAvailable(bool powered)
    {
        // The fuse can report its state before this component's Awake.
        if (effects == null) effects = GetComponent<RadioEffects>();
        if (hasPower == powered) return;
        hasPower = powered;
        if (!powered)
        {
            interruptedByPowerLoss = HasActivePerson && callStarted;
            ShowStartScreen();
        }
        else if (interruptedByPowerLoss && HasActivePerson)
        {
            interruptedByPowerLoss = false;
            ResetScreen();
            Speak(powerRestoredText, ShowStartScreen);
        }
        else ShowStartScreen();
        RefreshSound();
    }

    public void ShowStartScreen()
    {
        ResetScreen();
        if (!hasPower)
        {
            SetText(noPowerText);
            return;
        }
        if (HasActivePerson)
        {
            if (!callStarted)
            {
                callStarted = true;
                PersonStarted?.Invoke(currentPersonId);
            }
            if (resumeDialogue != null) resumeDialogue();
            else ShowNode(currentPerson.startNode);
            return;
        }
        SetText(noHelpText, true);
        AddButton(defaultTalkButtonText, TuneAround);
    }

    private void ShowNode(string nodeId)
    {
        ResetScreen();
        var node = currentPerson?.nodes?.FirstOrDefault(item => item != null && item.id == nodeId);
        if (node == null)
        {
            Debug.LogWarning("Radio dialogue node not found: " + nodeId, this);
            PersonLeft();
            return;
        }
        string text = ResolveText(node.text);
        if (currentNodeId != node.id) SignalHistory.RecordIncoming(text);
        currentNodeId = node.id;
        resumeDialogue = () => ShowNode(nodeId);
        var answers = node.answers ?? Array.Empty<RadioAnswer>();
        acceptAnswer = answers.FirstOrDefault(answer => GetDecision(answer) == Buttons.ButtonType.Allow);
        rejectAnswer = answers.FirstOrDefault(answer => GetDecision(answer) == Buttons.ButtonType.Deny);
        RefreshDecisionLights();
        Speak(text, () => ShowAnswers(answers));
    }

    private void ShowAnswers(RadioAnswer[] answers)
    {
        waitingForDecision = true;
        if (answers.Length == 0)
        {
            AddButton("End call", PersonLeft);
            return;
        }
        foreach (var answer in answers)
        {
            if (answer != null && GetDecision(answer) == null)
                AddButton(answer.text, () => PickAnswer(answer));
        }
        if (acceptAnswer != null || rejectAnswer != null)
            SetText(effects.Text + "\n\n" + waitingForDecisionText);
    }

    public void PressAccept() => PickAnswer(acceptAnswer, !waitingForDecision);
    public void PressReject() => PickAnswer(rejectAnswer);

    private void OnDecision(Buttons.ButtonType type)
    {
        if (type == Buttons.ButtonType.Allow) PressAccept();
        else PressReject();
    }

    private void PickAnswer(RadioAnswer answer, bool earlyAccept = false)
    {
        if (!hasPower || answer == null) return;
        string reply = ResolveText(earlyAccept ? earlyAcceptReplyText : answer.reply);
        answered = true;
        SignalHistory.RecordOutgoing(ResolveText(answer.text));
        SignalHistory.RecordIncoming(reply);
        ShowReply(reply, answer.nextNode);
    }

    private void ShowReply(string reply, string nextNode)
    {
        ResetScreen();
        resumeDialogue = () => ShowReply(reply, nextNode);
        Speak(reply, () =>
        {
            if (string.IsNullOrEmpty(nextNode)) AddButton("End call", PersonLeft);
            else AddButton("Continue", () => ShowNode(nextNode));
        });
    }

    private void Speak(string text, Action onFinished)
    {
        if (speakerText != null) speakerText.text = incomingText;
        effects.Speak(ResolveText(text), CurrentVoiceClip, onFinished);
    }

    private void ResetScreen()
    {
        StopAllCoroutines();
        if (effects != null) effects.CancelText();
        waitingForDecision = false;
        acceptAnswer = null;
        rejectAnswer = null;
        if (ButtonParent != null)
        {
            foreach (Transform child in ButtonParent)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }
        RefreshDecisionLights();
    }

    private void SetText(string text, bool shuffle = false)
    {
        if (speakerText != null) speakerText.text = HasActivePerson && hasPower ? incomingText : "Radio";
        effects.SetText(ResolveText(text), shuffle);
    }

    private void AddButton(string text, UnityEngine.Events.UnityAction action)
    {
        if (answerButtonPrefab == null || ButtonParent == null) return;
        var button = Instantiate(answerButtonPrefab, ButtonParent);
        var label = button.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = ResolveText(text);
        button.onClick.AddListener(action);
    }

    private void RefreshDecisionLights()
    {
        if (decisionButtons != null)
            decisionButtons.SetDecisionAvailability(hasPower && acceptAnswer != null, hasPower && rejectAnswer != null);
    }

    // Existing JSON stores decisions as answer text.
    private static Buttons.ButtonType? GetDecision(RadioAnswer answer)
    {
        string text = (answer?.text ?? "").Normalize(NormalizationForm.FormD);
        var letters = text.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);
        text = new string(letters.ToArray()).ToLowerInvariant();
        string[] deny = { "do not", "dont", "don't", "deny", "zamitnout", "odmitnout" };
        string[] allow = { "let ", "povolit", "prijmout" };
        if (deny.Any(text.Contains)) return Buttons.ButtonType.Deny;
        if (allow.Any(text.Contains)) return Buttons.ButtonType.Allow;
        return null;
    }

    private static string ResolveText(string text)
    {
        string name = string.IsNullOrWhiteSpace(PlayerProfile.PlayerName) ? "OPERATOR" : PlayerProfile.PlayerName;
        string[] markers = { "{{PLAYER_NAME}}", "{{player_name}}", "{PLAYER_NAME}", "{player_name}",
            "[PLAYER_NAME]", "[player_name]", "{OPERATOR_NAME}", "{operator_name}" };
        foreach (string marker in markers) text = (text ?? "").Replace(marker, name);
        return text;
    }

    private void TuneAround()
    {
        if (!hasPower || HasActivePerson) return;
        ResetScreen();
        StartCoroutine(Tune());
    }

    private IEnumerator Tune()
    {
        yield return effects.TuneNoise();
        var stations = stationData?.stations;
        if (stations != null && stations.Length > 0 && UnityEngine.Random.value <= normalStationChance)
        {
            string text = ResolveText(stations[stationIndex++ % stations.Length].text);
            SignalHistory.RecordIncoming(text);
            SetText(text, true);
        }
        AddButton(defaultTalkButtonText, TuneAround);
    }

    private void RefreshSound()
    {
        if (effects != null) effects.SetSound(hasPower && isActiveAndEnabled, radioVisible, HasActivePerson);
    }

    [ContextMenu("Debug Radio/Selected Person Id")]
    public void DebugSelectedPerson() => PersonCame(debugPersonId);

    [ContextMenu("Debug Radio/No One")]
    public void DebugNoOne() => PersonLeft();
}
