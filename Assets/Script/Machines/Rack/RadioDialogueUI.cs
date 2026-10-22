using System;
using System.Collections;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RadioDialogueUI : MonoBehaviour
{
    private static readonly string[] AcceptWords = { "let ", "povolit", "prijmout" };
    private static readonly string[] RejectWords = { "do not", "dont", "don't", "deny", "zamitnout", "odmitnout" };
    private static readonly string[] PlayerNameMarkers =
    {
        "{PLAYER_NAME}",
        "{player_name}",
        "{{PLAYER_NAME}}",
        "{{player_name}}",
        "[PLAYER_NAME]",
        "[player_name]",
        "{OPERATOR_NAME}",
        "{operator_name}"
    };

    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text radioText;
    [SerializeField] private Transform answerButtonPanel;
    [SerializeField] private Transform buttonParent;
    [SerializeField] private Button answerButtonPrefab;
    [SerializeField] private Buttons decisionButtons;

    [Header("UI Feel")]
    [SerializeField, Min(0.01f)] private float fadeTime = 0.2f;
    [SerializeField, Min(0.01f)] private float textShuffleDuration = 0.18f;
    [SerializeField, Min(0.001f)] private float talkingLetterDelay = 0.025f;
    [SerializeField, Min(0.05f)] private float tuneTime = 1.2f;
    [SerializeField] private string shuffleCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789#$%/\\=-_";

    [Header("Tuning")]
    [SerializeField, Range(0f, 1f)] private float normalStationChance = 0.25f;
    [SerializeField, Min(8)] private int tuningCharacterCount = 32;

    [Header("Radio Sound")]
    [SerializeField] private AudioSource openStaticSource;
    [SerializeField] private AudioSource incomingCallSource;
    [SerializeField] private AudioSource tuningSource;
    [SerializeField] private AudioClip openStaticLoop;
    [SerializeField] private AudioClip incomingCallLoop;
    [SerializeField] private AudioClip tuningLoop;
    [SerializeField, Range(0f, 1f)] private float openStaticVolume = 0.25f;
    [SerializeField, Range(0f, 1f)] private float incomingCallVolume = 0.55f;
    [SerializeField, Range(0f, 1f)] private float tuningVolume = 0.45f;
    [SerializeField, Min(0.01f)] private float audioFadeTime = 0.35f;

    [Header("Data")]
    [SerializeField] private string peopleFolder = "RadioPeople/";
    [SerializeField] private string tuneJsonFileName = "RadioStations";
    [SerializeField] private string debugPersonId = "farMan";
    [SerializeField] private string incomingText = "Incoming Transmission";
    [SerializeField] private string noHelpText = "No one needs my assistance right now.";
    [SerializeField] private string noPowerText = "Radio is dead. No power.";
    [SerializeField] private string powerRestoredText = "You're back. I thought the line had gone dead.";
    [SerializeField] private string defaultTalkButtonText = "Tune around";
    [SerializeField] private string waitingForDecisionText = "Waiting for your decision...";
    [SerializeField] private string earlyAcceptReplyText = "What? You are not even going to verify me?";

    [Header("Only 4 Voice Sounds")]
    [SerializeField] private AudioSource voiceSource;
    [SerializeField] private AudioClip womanVoice;
    [SerializeField] private AudioClip farManVoice;
    [SerializeField] private AudioClip manVoice;
    [SerializeField] private AudioClip childVoice;
    [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.55f;
    [SerializeField] private Vector2 voicePitchRange = new Vector2(0.95f, 1.05f);
    [SerializeField, Min(1)] private int voiceSoundEveryLetters = 2;
    [SerializeField, Min(0.01f)] private float voiceBitLength = 0.05f;
    [SerializeField, Min(0.001f)] private float voiceBitFadeTime = 0.006f;

    private RadioPerson currentPerson;
    private string currentPersonId = "";
    private string currentNodeId = "";
    private int noiseIndex;
    private Coroutine fadeRoutine;
    private Coroutine radioTextRoutine;
    private Coroutine talkRoutine;
    private Coroutine openStaticRoutine;
    private Coroutine incomingCallRoutine;
    private Coroutine tuningRoutine;
    private Coroutine voiceRoutine;
    private RadioStationData stationData;
    private bool radioVisible;
    private bool hasPower = true;
    private bool callInterruptedByPowerLoss;
    private bool waitingForDecision;
    private bool tuningSoundActive;
    private RadioAnswer acceptAnswer;
    private RadioAnswer rejectAnswer;
    private Buttons subscribedDecisionButtons;
    private bool currentCallHadPlayerAnswer;
    public event Action<string> PersonCompleted;
    public event Action<string> PersonStarted;

    private Transform ButtonParent => answerButtonPanel != null ? answerButtonPanel : buttonParent;
    public bool HasActivePerson => currentPerson != null;
    public string CurrentVoice => currentPerson != null ? currentPerson.voice : "";
    public AudioClip CurrentVoiceClip => GetVoiceClip(CurrentVoice);

    private void Awake()
    {
        ResolveDecisionButtons();
        SetupAudioSources();
        LoadStations();
        HideRadioImmediately();
        ShowStartScreen();
        RefreshDecisionLights();
        RefreshRadioSound();
        SubscribeDecisionButtons();
    }

    private void OnEnable()
    {
        SubscribeDecisionButtons();
    }

    private void OnDisable()
    {
        UnsubscribeDecisionButtons();
        StopTalking();
        StopRadioTextRoutine();
        StopAudioRoutine(ref openStaticRoutine);
        StopAudioRoutine(ref incomingCallRoutine);
        StopAudioRoutine(ref tuningRoutine);
        StopAudioRoutine(ref voiceRoutine);
        StopAudioSource(openStaticSource);
        StopAudioSource(incomingCallSource);
        StopAudioSource(tuningSource);
        StopAudioSource(voiceSource);
        SetDecisionAvailability(false, false);
    }

    public void ShowRadio()
    {
        radioVisible = true;
        if (!hasPower)
        {
            ShowPowerOffScreen();
            FadeRadio(true);
            RefreshRadioSound();
            return;
        }

        FadeRadio(true);
        RefreshRadioSound();

        if (HasActiveRadioWork()) RefreshDecisionLights();
        else ShowStartScreen();
    }

    public void HideRadio()
    {
        radioVisible = false;
        FadeRadio(false);
        RefreshRadioSound();
    }

    public void PersonCame(string personId)
    {
        currentPersonId = personId;
        currentNodeId = "";
        currentPerson = LoadPerson(personId);
        currentCallHadPlayerAnswer = false;

        if (!hasPower)
        {
            ShowPowerOffScreen();
            RefreshRadioSound();
            return;
        }

        if (currentPerson == null)
        {
            ShowNoOne();
            RefreshRadioSound();
            return;
        }

        PersonStarted?.Invoke(currentPersonId);
        speakerText.text = incomingText;
        ShowNode(currentPerson.startNode);
        RefreshRadioSound();
    }

    [ContextMenu("Debug Radio/Woman")] public void DebugWoman() => PersonCame("woman");
    [ContextMenu("Debug Radio/Far Man")] public void DebugFarMan() => PersonCame("farMan");
    [ContextMenu("Debug Radio/Man")] public void DebugMan() => PersonCame("man");
    [ContextMenu("Debug Radio/Child")] public void DebugChild() => PersonCame("child");
    [ContextMenu("Debug Radio/Operator")] public void DebugOperator() => PersonCame("operator");
    [ContextMenu("Debug Radio/Selected Person Id")] public void DebugSelectedPerson() => PersonCame(debugPersonId);
    [ContextMenu("Debug Radio/No One")] public void DebugNoOne() => PersonLeft();

    public void PersonLeft()
    {
        string completedPersonId = currentPersonId;
        bool completedCall = currentPerson != null && currentCallHadPlayerAnswer;

        currentPersonId = "";
        currentNodeId = "";
        currentPerson = null;
        currentCallHadPlayerAnswer = false;
        ShowStartScreen();
        RefreshRadioSound();

        if (completedCall)
        {
            PersonCompleted?.Invoke(completedPersonId);
        }
    }

    public void PressAccept()
    {
        if (!hasPower) return;
        if (acceptAnswer != null) PickDecision(acceptAnswer, !waitingForDecision);
    }

    public void PressReject()
    {
        if (!hasPower) return;
        if (rejectAnswer != null) PickDecision(rejectAnswer, false);
    }

    public void SetPowerAvailable(bool powered)
    {
        if (hasPower == powered)
        {
            return;
        }

        hasPower = powered;

        if (!hasPower)
        {
            callInterruptedByPowerLoss = currentPerson != null;
            ShowPowerOffScreen();
            RefreshRadioSound();
            return;
        }

        if (callInterruptedByPowerLoss && currentPerson != null)
        {
            callInterruptedByPowerLoss = false;
            ShowPowerRestoredMessage();
        }
        else if (radioVisible)
        {
            ShowStartScreen();
        }

        RefreshRadioSound();
    }

    public void ShowStartScreen()
    {
        if (!hasPower)
        {
            ShowPowerOffScreen();
            return;
        }

        if (string.IsNullOrEmpty(currentPersonId) || currentPerson == null)
        {
            ShowNoOne();
            return;
        }

        speakerText.text = incomingText;
        ShowNode(string.IsNullOrEmpty(currentNodeId) ? currentPerson.startNode : currentNodeId);
    }

    private void ResolveDecisionButtons()
    {
        if (decisionButtons != null) return;

        decisionButtons = GetComponentInParent<Buttons>();
        if (decisionButtons != null) return;

        decisionButtons = GetComponentInChildren<Buttons>();
        if (decisionButtons != null) return;

        Buttons[] buttonPanels = FindObjectsByType<Buttons>(FindObjectsSortMode.None);
        if (buttonPanels.Length == 1) decisionButtons = buttonPanels[0];
        else if (buttonPanels.Length > 1) Debug.LogWarning("RadioDialogueUI found multiple Buttons objects. Assign Decision Buttons manually in the inspector.", this);
    }

    private void OnDecisionButtonPressed(Buttons.ButtonType type)
    {
        if (type == Buttons.ButtonType.Allow) PressAccept();
        else PressReject();
    }

    private void ShowNoOne()
    {
        if (!hasPower)
        {
            ShowPowerOffScreen();
            return;
        }

        StopTalking();
        ClearDecisionState();
        RefreshDecisionLights();
        speakerText.text = "Radio";
        SetRadioText(noHelpText);

        ClearButtons();
        AddButton(defaultTalkButtonText, ShowRandomNoise);
    }

    private bool HasActiveRadioWork()
    {
        return hasPower && (currentPerson != null || talkRoutine != null || radioTextRoutine != null || waitingForDecision);
    }

    private void ShowRandomNoise()
    {
        if (!hasPower) return;

        ClearButtons();
        StopTalking();
        ClearDecisionState();
        RefreshDecisionLights();
        StopRadioTextRoutine();
        radioTextRoutine = StartCoroutine(TuneAroundRoutine());
    }

    private void ShowNode(string nodeId)
    {
        if (!hasPower)
        {
            ShowPowerOffScreen();
            return;
        }

        StopRadioTextRoutine();
        RadioNode node = FindNode(currentPerson, nodeId);

        if (node == null)
        {
            ShowNoOne();
            return;
        }

        bool isNewNode = currentNodeId != node.id;
        currentNodeId = node.id;
        speakerText.text = incomingText;
        string nodeText = ResolveRadioVariables(node.text);
        if (isNewNode) SignalHistory.RecordIncoming(nodeText);

        ClearButtons();
        SetupDecisionAnswers(node.answers);
        RefreshDecisionLights();
        ShowTalkingText(nodeText, node.answers);
    }

    private void ShowAnswers(RadioAnswer[] answers)
    {
        waitingForDecision = true;
        RefreshDecisionLights();

        if (answers == null || answers.Length == 0)
        {
            AddButton("End call", PersonLeft);
            return;
        }

        foreach (RadioAnswer answer in answers)
        {
            if (answer == null) continue;
            if (!IsDecisionAnswer(answer)) AddButton(ResolveRadioVariables(answer.text), () => PickAnswer(answer, false));
        }

        if (acceptAnswer != null || rejectAnswer != null)
        {
            string oldText = radioText != null ? radioText.text : "";
            SetRadioTextImmediately(string.IsNullOrEmpty(oldText) ? waitingForDecisionText : oldText + "\n\n" + waitingForDecisionText);
        }
    }

    private void PickDecision(RadioAnswer answer, bool earlyAccept)
    {
        PickAnswer(answer, true, earlyAccept);
    }

    private void PickAnswer(RadioAnswer answer, bool isDecision, bool earlyAccept = false)
    {
        if (!hasPower) return;
        if (answer == null || (!isDecision && IsDecisionAnswer(answer))) return;

        string answerText = ResolveRadioVariables(answer.text);
        string reply = ResolveRadioVariables(earlyAccept && !string.IsNullOrEmpty(earlyAcceptReplyText) ? earlyAcceptReplyText : answer.reply);
        currentCallHadPlayerAnswer = true;
        SignalHistory.RecordOutgoing(answerText);
        SignalHistory.RecordIncoming(reply);
        ClearButtons();
        ClearDecisionState();
        RefreshDecisionLights();
        ShowTalkingReply(answer, reply);
    }

    private void AddButton(string text, UnityEngine.Events.UnityAction action)
    {
        if (answerButtonPrefab == null || ButtonParent == null) return;

        Button button = Instantiate(answerButtonPrefab, ButtonParent);
        TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>();
        if (buttonText != null) buttonText.text = ResolveRadioVariables(text);

        button.onClick.AddListener(action);
    }

    private void ClearButtons()
    {
        if (ButtonParent == null) return;

        for (int i = ButtonParent.childCount - 1; i >= 0; i--)
        {
            Destroy(ButtonParent.GetChild(i).gameObject);
        }
    }

    private void FadeRadio(bool visible)
    {
        if (canvasGroup == null) return;

        StopRoutine(ref fadeRoutine);
        fadeRoutine = StartCoroutine(FadeCanvasGroup(visible));
    }

    private void HideRadioImmediately()
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private IEnumerator FadeCanvasGroup(bool visible)
    {
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;

        float from = canvasGroup.alpha;
        float to = visible ? 1f : 0f;

        for (float timer = 0f; timer < fadeTime; timer += Time.unscaledDeltaTime)
        {
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(timer / fadeTime));
            yield return null;
        }

        canvasGroup.alpha = to;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;
        fadeRoutine = null;
    }

    private void SetRadioText(string text)
    {
        if (radioText == null) return;

        StopRadioTextRoutine();
        radioTextRoutine = StartCoroutine(ShuffleText(radioText, ResolveRadioVariables(text)));
    }

    private void SetRadioTextImmediately(string text)
    {
        if (radioText == null) return;

        StopRadioTextRoutine();
        radioText.text = ResolveRadioVariables(text);
    }

    private void ShowTalkingText(string text, RadioAnswer[] answers)
    {
        if (!hasPower) return;

        StopTalking();
        talkRoutine = StartCoroutine(TalkAndThen(text, () => ShowAnswers(answers)));
    }

    private void ShowTalkingReply(RadioAnswer answer, string reply)
    {
        if (!hasPower) return;

        StopTalking();
        ClearDecisionState();
        RefreshDecisionLights();

        talkRoutine = StartCoroutine(TalkAndThen(reply, () =>
        {
            if (answer == null || string.IsNullOrEmpty(answer.nextNode)) AddButton("End call", PersonLeft);
            else AddButton("Continue", () => ShowNode(answer.nextNode));
        }));
    }

    private void StopTalking()
    {
        StopRoutine(ref talkRoutine);
        StopVoiceSound();
    }

    private void SetupDecisionAnswers(RadioAnswer[] answers)
    {
        ClearDecisionState();
        if (answers == null) return;

        foreach (RadioAnswer answer in answers)
        {
            if (answer == null) continue;

            if (rejectAnswer == null && IsRejectAnswer(answer)) rejectAnswer = answer;
            else if (acceptAnswer == null && IsAcceptAnswer(answer)) acceptAnswer = answer;
        }
    }

    private void ClearDecisionState()
    {
        waitingForDecision = false;
        acceptAnswer = null;
        rejectAnswer = null;
    }

    private bool IsDecisionAnswer(RadioAnswer answer)
    {
        return IsAcceptAnswer(answer) || IsRejectAnswer(answer);
    }

    private bool IsAcceptAnswer(RadioAnswer answer)
    {
        string text = NormalizeDecisionText(answer != null ? answer.text : "");
        return !ContainsAny(text, RejectWords) && ContainsAny(text, AcceptWords);
    }

    private bool IsRejectAnswer(RadioAnswer answer)
    {
        return ContainsAny(NormalizeDecisionText(answer != null ? answer.text : ""), RejectWords);
    }

    private static bool ContainsAny(string text, string[] words)
    {
        if (string.IsNullOrEmpty(text)) return false;

        foreach (string word in words)
        {
            if (text.Contains(word)) return true;
        }

        return false;
    }

    private string NormalizeDecisionText(string text)
    {
        string normalizedText = (text ?? "").Normalize(NormalizationForm.FormD);
        string result = "";

        for (int i = 0; i < normalizedText.Length; i++)
        {
            char character = normalizedText[i];
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                result += char.ToLowerInvariant(character);
            }
        }

        return result;
    }

    private string ResolveRadioVariables(string text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";

        string playerName = PlayerProfile.PlayerName;
        if (string.IsNullOrWhiteSpace(playerName)) playerName = "OPERATOR";

        string resolvedText = text;
        foreach (string marker in PlayerNameMarkers)
        {
            resolvedText = resolvedText.Replace(marker, playerName);
        }

        return resolvedText;
    }

    private void RefreshDecisionLights()
    {
        bool hasPerson = hasPower && currentPerson != null;
        SetDecisionAvailability(hasPerson && acceptAnswer != null, hasPerson && rejectAnswer != null);
    }

    private void SetDecisionAvailability(bool allowReady, bool denyReady)
    {
        if (decisionButtons != null) decisionButtons.SetDecisionAvailability(allowReady, denyReady);
    }

    private void SubscribeDecisionButtons()
    {
        if (decisionButtons == null || subscribedDecisionButtons == decisionButtons) return;

        UnsubscribeDecisionButtons();
        decisionButtons.ButtonPressed += OnDecisionButtonPressed;
        subscribedDecisionButtons = decisionButtons;
    }

    private void UnsubscribeDecisionButtons()
    {
        if (subscribedDecisionButtons == null) return;

        subscribedDecisionButtons.ButtonPressed -= OnDecisionButtonPressed;
        subscribedDecisionButtons = null;
    }

    private void StopRadioTextRoutine()
    {
        StopRoutine(ref radioTextRoutine);
        StopTuningSound();
    }

    private void StopRoutine(ref Coroutine routine)
    {
        if (routine == null) return;

        StopCoroutine(routine);
        routine = null;
    }

    private IEnumerator TalkAndThen(string text, Action afterText)
    {
        if (radioText == null)
        {
            afterText?.Invoke();
            yield break;
        }

        SetRadioTextImmediately("");
        text = ResolveRadioVariables(text);

        for (int i = 0; i < text.Length; i++)
        {
            radioText.text = text.Substring(0, i + 1);
            PlayVoiceSound(text[i], i);
            yield return new WaitForSecondsRealtime(talkingLetterDelay);
        }

        StopVoiceSound();
        talkRoutine = null;
        afterText?.Invoke();
    }

    private IEnumerator ShuffleText(TMP_Text label, string targetText)
    {
        targetText = targetText ?? "";

        for (float timer = 0f; timer < textShuffleDuration; timer += Time.unscaledDeltaTime)
        {
            label.text = BuildScrambledText(targetText, Mathf.Clamp01(timer / textShuffleDuration));
            yield return null;
        }

        label.text = targetText;
        radioTextRoutine = null;
    }

    private IEnumerator TuneAroundRoutine()
    {
        PlayTuningSound();

        for (float timer = 0f; timer < tuneTime; timer += Time.unscaledDeltaTime)
        {
            radioText.text = BuildTuningText(timer);
            yield return null;
        }

        StopTuningSound();

        RadioStation station = ShouldFindNormalStation() ? GetNextStation() : null;
        radioTextRoutine = null;

        if (station == null)
        {
            SetRadioTextImmediately(BuildTuningText(tuneTime));
        }
        else
        {
            string stationText = ResolveRadioVariables(station.text);
            SignalHistory.RecordIncoming(stationText);
            SetRadioText(stationText);
        }

        ClearButtons();
        AddButton(defaultTalkButtonText, ShowRandomNoise);
    }

    private string BuildTuningText(float timer)
    {
        if (string.IsNullOrEmpty(shuffleCharacters)) return "TUNING...";

        string text = "";

        for (int i = 0; i < tuningCharacterCount; i++)
        {
            text += shuffleCharacters[UnityEngine.Random.Range(0, shuffleCharacters.Length)];
        }

        int noiseCount = 1 + Mathf.FloorToInt(timer * 7f) % 4;
        for (int i = 0; i < noiseCount; i++) text += " .";

        return text;
    }

    private bool ShouldFindNormalStation()
    {
        return stationData != null &&
            stationData.stations != null &&
            stationData.stations.Length > 0 &&
            UnityEngine.Random.value <= normalStationChance;
    }

    private void LoadStations()
    {
        TextAsset file = Resources.Load<TextAsset>(tuneJsonFileName);

        if (file == null)
        {
            Debug.LogWarning("Radio stations JSON not found: " + tuneJsonFileName);
            return;
        }

        stationData = JsonUtility.FromJson<RadioStationData>(file.text);
    }

    private RadioStation GetNextStation()
    {
        if (stationData == null || stationData.stations == null || stationData.stations.Length == 0) return null;

        RadioStation station = stationData.stations[noiseIndex % stationData.stations.Length];
        noiseIndex++;
        return station;
    }

    private string BuildScrambledText(string targetText, float progress)
    {
        if (string.IsNullOrEmpty(targetText) || progress >= 1f || string.IsNullOrEmpty(shuffleCharacters)) return targetText;

        int revealCount = Mathf.FloorToInt(targetText.Length * progress);
        string text = "";

        for (int i = 0; i < targetText.Length; i++)
        {
            char targetCharacter = targetText[i];
            if (i < revealCount || ShouldKeepCharacterVisible(targetCharacter))
            {
                text += targetCharacter;
            }
            else
            {
                text += shuffleCharacters[Mathf.Abs(Time.frameCount * 17 + i * 31) % shuffleCharacters.Length];
            }
        }

        return text;
    }

    private static bool ShouldKeepCharacterVisible(char value)
    {
        return char.IsWhiteSpace(value) || char.IsPunctuation(value) || char.IsSymbol(value);
    }

    private RadioPerson LoadPerson(string personId)
    {
        TextAsset file = Resources.Load<TextAsset>(peopleFolder + personId);

        if (file == null)
        {
            Debug.LogWarning("Radio person JSON not found: " + peopleFolder + personId);
            return null;
        }

        return JsonUtility.FromJson<RadioPerson>(file.text);
    }

    private AudioClip GetVoiceClip(string voice)
    {
        if (voice == "Woman") return womanVoice;
        if (voice == "FarMan") return farManVoice;
        if (voice == "Man") return manVoice;
        if (voice == "Child") return childVoice;
        return null;
    }

    private void SetupAudioSources()
    {
        if (openStaticSource == null && openStaticLoop != null) openStaticSource = gameObject.AddComponent<AudioSource>();
        if (incomingCallSource == null && incomingCallLoop != null) incomingCallSource = gameObject.AddComponent<AudioSource>();
        if (tuningSource == null && tuningLoop != null) tuningSource = gameObject.AddComponent<AudioSource>();
        if (voiceSource == null && HasAnyVoiceClip()) voiceSource = gameObject.AddComponent<AudioSource>();

        if (openStaticSource != null && incomingCallSource == openStaticSource && openStaticLoop != null && incomingCallLoop != null)
        {
            incomingCallSource = CreateMatchingAudioSource(openStaticSource);
        }

        if (openStaticSource != null && tuningSource == openStaticSource && openStaticLoop != null && tuningLoop != null)
        {
            tuningSource = CreateMatchingAudioSource(openStaticSource);
        }

        if (incomingCallSource != null && tuningSource == incomingCallSource && incomingCallLoop != null && tuningLoop != null)
        {
            tuningSource = CreateMatchingAudioSource(incomingCallSource);
        }

        if (voiceSource != null && voiceSource == openStaticSource && HasAnyVoiceClip())
        {
            voiceSource = CreateMatchingAudioSource(openStaticSource);
        }

        if (voiceSource != null && voiceSource == incomingCallSource && HasAnyVoiceClip())
        {
            voiceSource = CreateMatchingAudioSource(incomingCallSource);
        }

        if (voiceSource != null && voiceSource == tuningSource && HasAnyVoiceClip())
        {
            voiceSource = CreateMatchingAudioSource(tuningSource);
        }

        ConfigureLoopSource(openStaticSource, openStaticLoop);
        ConfigureLoopSource(incomingCallSource, incomingCallLoop);
        ConfigureLoopSource(tuningSource, tuningLoop);
        ConfigureOneShotSource(voiceSource);
    }

    private AudioSource CreateMatchingAudioSource(AudioSource template)
    {
        AudioSource source = (template != null ? template.gameObject : gameObject).AddComponent<AudioSource>();
        if (template == null) return source;

        source.outputAudioMixerGroup = template.outputAudioMixerGroup;
        source.priority = template.priority;
        source.spatialBlend = template.spatialBlend;
        source.minDistance = template.minDistance;
        source.maxDistance = template.maxDistance;
        source.rolloffMode = template.rolloffMode;
        source.dopplerLevel = template.dopplerLevel;
        source.spread = template.spread;
        return source;
    }

    private static void ConfigureLoopSource(AudioSource source, AudioClip clip)
    {
        if (source == null) return;

        source.playOnAwake = false;
        source.loop = true;
        source.clip = clip;
        source.volume = 0f;
    }

    private static void ConfigureOneShotSource(AudioSource source)
    {
        if (source == null) return;

        source.playOnAwake = false;
        source.loop = false;
    }

    private void StopAudioRoutine(ref Coroutine routine)
    {
        StopRoutine(ref routine);
    }

    private static void StopAudioSource(AudioSource source)
    {
        if (source == null) return;

        source.volume = 0f;
        source.Stop();
    }

    private void RefreshRadioSound()
    {
        openStaticRoutine = FadeAudio(openStaticSource, openStaticLoop, openStaticVolume, hasPower && radioVisible, openStaticRoutine);
        incomingCallRoutine = FadeAudio(incomingCallSource, incomingCallLoop, incomingCallVolume, hasPower && !radioVisible && currentPerson != null, incomingCallRoutine);
    }

    private void ShowPowerOffScreen()
    {
        StopTalking();
        StopRadioTextRoutine();
        ClearButtons();
        ClearDecisionState();
        RefreshDecisionLights();
        StopAudioRoutine(ref openStaticRoutine);
        StopAudioRoutine(ref incomingCallRoutine);
        StopAudioRoutine(ref tuningRoutine);
        StopAudioRoutine(ref voiceRoutine);
        StopAudioSource(openStaticSource);
        StopAudioSource(incomingCallSource);
        StopAudioSource(tuningSource);
        StopAudioSource(voiceSource);

        if (speakerText != null)
        {
            speakerText.text = "Radio";
        }

        SetRadioTextImmediately(noPowerText);
    }

    private void ShowPowerRestoredMessage()
    {
        StopTalking();
        ClearButtons();
        ClearDecisionState();
        RefreshDecisionLights();

        if (speakerText != null)
        {
            speakerText.text = incomingText;
        }

        talkRoutine = StartCoroutine(TalkAndThen(powerRestoredText, ShowStartScreen));
    }

    private bool HasAnyVoiceClip()
    {
        return womanVoice != null || farManVoice != null || manVoice != null || childVoice != null;
    }

    private void PlayVoiceSound(char character, int letterIndex)
    {
        if (voiceSource == null || char.IsWhiteSpace(character) || letterIndex % voiceSoundEveryLetters != 0) return;

        AudioClip clip = CurrentVoiceClip;
        if (clip == null) return;

        StopAudioRoutine(ref voiceRoutine);
        voiceRoutine = StartCoroutine(VoiceBitRoutine(clip));
    }

    private IEnumerator VoiceBitRoutine(AudioClip clip)
    {
        float bitLength = GetVoiceBitLength();

        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.pitch = UnityEngine.Random.Range(voicePitchRange.x, voicePitchRange.y);
        voiceSource.volume = 0f;
        voiceSource.time = UnityEngine.Random.Range(0f, Mathf.Max(0f, clip.length - bitLength));
        voiceSource.Play();

        float fadeTime = Mathf.Min(voiceBitFadeTime, bitLength * 0.45f);
        float holdTime = Mathf.Max(0f, bitLength - fadeTime * 2f);

        yield return FadeVoiceVolume(0f, voiceVolume, fadeTime);

        if (holdTime > 0f)
        {
            yield return new WaitForSecondsRealtime(holdTime);
        }

        yield return FadeVoiceVolume(voiceVolume, 0f, fadeTime);

        voiceSource.Stop();
        voiceRoutine = null;
    }

    private float GetVoiceBitLength()
    {
        float nextBitTime = talkingLetterDelay * voiceSoundEveryLetters;
        return Mathf.Min(voiceBitLength, nextBitTime * 0.85f);
    }

    private IEnumerator FadeVoiceVolume(float from, float to, float time)
    {
        if (time <= 0f)
        {
            voiceSource.volume = to;
            yield break;
        }

        for (float timer = 0f; timer < time; timer += Time.unscaledDeltaTime)
        {
            voiceSource.volume = Mathf.Lerp(from, to, Mathf.Clamp01(timer / time));
            yield return null;
        }

        voiceSource.volume = to;
    }

    private void StopVoiceSound()
    {
        StopAudioRoutine(ref voiceRoutine);
        if (voiceSource != null) voiceSource.Stop();
    }

    private void PlayTuningSound()
    {
        if (tuningSource == null || tuningLoop == null) return;

        tuningSoundActive = true;
        tuningRoutine = FadeAudio(tuningSource, tuningLoop, tuningVolume, true, tuningRoutine);
    }

    private void StopTuningSound()
    {
        if (!tuningSoundActive) return;

        tuningSoundActive = false;
        tuningRoutine = FadeAudio(tuningSource, tuningLoop, tuningVolume, false, tuningRoutine);
    }

    private Coroutine FadeAudio(AudioSource source, AudioClip clip, float targetVolume, bool play, Coroutine routine)
    {
        if (source == null) return null;
        if (routine != null) StopCoroutine(routine);
        return StartCoroutine(FadeAudioRoutine(source, clip, targetVolume, play));
    }

    private IEnumerator FadeAudioRoutine(AudioSource source, AudioClip clip, float targetVolume, bool play)
    {
        if (play && clip == null) yield break;

        source.clip = clip;
        source.loop = true;

        if (play && !source.isPlaying)
        {
            source.volume = 0f;
            source.Play();
        }

        float startVolume = source.volume;
        float endVolume = play ? targetVolume : 0f;

        for (float timer = 0f; timer < audioFadeTime; timer += Time.unscaledDeltaTime)
        {
            source.volume = Mathf.Lerp(startVolume, endVolume, Mathf.Clamp01(timer / audioFadeTime));
            yield return null;
        }

        source.volume = endVolume;
        if (!play && source.isPlaying) source.Stop();
    }

    private static RadioNode FindNode(RadioPerson person, string id)
    {
        if (person == null || person.nodes == null) return null;

        foreach (RadioNode node in person.nodes)
        {
            if (node.id == id) return node;
        }

        return null;
    }
}
