using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class GameSoundtrackController : MonoBehaviour
{
    private const string OneShotPrefix = "soundtrack_event_";

    [Header("References")]
    [SerializeField] private MessageTransmitter transmitter;
    [SerializeField] private RadioDialogueUI radioUI;
    [SerializeField] private TransmissionSequenceController transmissionSequence;
    [SerializeField] private AudioMixerGroup outputMixerGroup;

    [Header("Audio")]
    [SerializeField] private AudioSource primarySource;
    [SerializeField] private AudioSource secondarySource;
    [SerializeField, Range(0f, 1f)] private float defaultVolume = 0.8f;
    [SerializeField, Min(0f)] private float defaultFadeDuration = 2f;
    [SerializeField] private bool persistAcrossScenes = true;

    [Header("Scene")]
    [SerializeField] private bool onlyPlayInScene = true;
    [SerializeField] private string playableSceneName = "Main";

    [Header("Soundtrack Events")]
    [SerializeField] private SoundtrackEvent[] soundtrackEvents;

    private Coroutine fadeRoutine;
    private AudioSource activeSource;
    private AudioSource inactiveSource;
    private bool firstMessageSentTriggered;
    private bool firstRadioConversationStartedTriggered;

    private void Awake()
    {
        if (persistAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }

        ResolveReferences();
        SetupAudioSources();
    }

    private void OnEnable()
    {
        Subscribe();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        if (CanPlayInCurrentScene())
        {
            Trigger(SoundtrackTrigger.SceneEntered);
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnValidate()
    {
        defaultVolume = Mathf.Clamp01(defaultVolume);
        defaultFadeDuration = Mathf.Max(0f, defaultFadeDuration);
    }

    public void PlayEventById(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return;
        }

        Trigger(SoundtrackTrigger.Custom, string.Empty, eventId);
    }

    public void PlayObjectiveEvent(string objectiveId)
    {
        Trigger(SoundtrackTrigger.ObjectiveCompleted, objectiveId);
    }

    public void StopMusic(float fadeDuration = -1f)
    {
        float duration = fadeDuration >= 0f ? fadeDuration : defaultFadeDuration;
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(StopRoutine(duration));
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!CanPlayInScene(scene))
        {
            StopMusic(defaultFadeDuration);
            return;
        }

        ResolveReferences();
        Subscribe();
        Trigger(SoundtrackTrigger.SceneEntered);
    }

    private void ResolveReferences()
    {
        if (transmitter == null)
        {
            transmitter = FindFirstObjectByType<MessageTransmitter>();
        }

        if (radioUI == null)
        {
            radioUI = FindFirstObjectByType<RadioDialogueUI>();
        }

        if (transmissionSequence == null)
        {
            transmissionSequence = FindFirstObjectByType<TransmissionSequenceController>();
        }
    }

    private void Subscribe()
    {
        Unsubscribe();

        if (transmitter != null)
        {
            transmitter.SendFinished += HandleSendFinished;
        }

        if (radioUI != null)
        {
            radioUI.PersonStarted += HandleRadioPersonStarted;
        }

        if (transmissionSequence != null)
        {
            transmissionSequence.ObjectiveCompleted += HandleObjectiveCompleted;
            transmissionSequence.EndingStarted += HandleEndingStarted;
        }
    }

    private void Unsubscribe()
    {
        if (transmitter != null)
        {
            transmitter.SendFinished -= HandleSendFinished;
        }

        if (radioUI != null)
        {
            radioUI.PersonStarted -= HandleRadioPersonStarted;
        }

        if (transmissionSequence != null)
        {
            transmissionSequence.ObjectiveCompleted -= HandleObjectiveCompleted;
            transmissionSequence.EndingStarted -= HandleEndingStarted;
        }
    }

    private void HandleSendFinished(MessageTransmitter source)
    {
        if (firstMessageSentTriggered)
        {
            return;
        }

        firstMessageSentTriggered = true;
        Trigger(SoundtrackTrigger.FirstMessageSent);
    }

    private void HandleRadioPersonStarted(string personId)
    {
        if (firstRadioConversationStartedTriggered)
        {
            return;
        }

        firstRadioConversationStartedTriggered = true;
        Trigger(SoundtrackTrigger.FirstRadioConversationStarted, personId);
    }

    private void HandleObjectiveCompleted(TransmissionObjective objective)
    {
        Trigger(SoundtrackTrigger.ObjectiveCompleted, objective != null ? objective.id : string.Empty);
    }

    private void HandleEndingStarted(TransmissionObjective objective)
    {
        Trigger(SoundtrackTrigger.EndingStarted, objective != null ? objective.id : string.Empty);
    }

    private void Trigger(SoundtrackTrigger trigger, string objectiveOrPersonId = "", string customEventId = "")
    {
        if (!CanPlayInCurrentScene())
        {
            return;
        }

        if (soundtrackEvents == null)
        {
            return;
        }

        for (int i = 0; i < soundtrackEvents.Length; i++)
        {
            SoundtrackEvent soundtrackEvent = soundtrackEvents[i];
            if (soundtrackEvent == null || !soundtrackEvent.Matches(trigger, objectiveOrPersonId, customEventId))
            {
                continue;
            }

            if (!CanPlay(soundtrackEvent))
            {
                continue;
            }

            MarkPlayed(soundtrackEvent);
            if (soundtrackEvent.delay > 0f)
            {
                StartCoroutine(PlayDelayed(soundtrackEvent, soundtrackEvent.delay));
            }
            else
            {
                Play(soundtrackEvent);
            }
        }
    }

    private IEnumerator PlayDelayed(SoundtrackEvent soundtrackEvent, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        if (soundtrackEvent != null && soundtrackEvent.clip != null)
        {
            Play(soundtrackEvent);
        }
    }

    private bool CanPlay(SoundtrackEvent soundtrackEvent)
    {
        if (soundtrackEvent.clip == null)
        {
            return false;
        }

        return !soundtrackEvent.rememberAcrossSave || PlayerPrefs.GetInt(GetPlayedKey(soundtrackEvent), 0) == 0;
    }

    private void MarkPlayed(SoundtrackEvent soundtrackEvent)
    {
        if (!soundtrackEvent.rememberAcrossSave)
        {
            return;
        }

        PlayerPrefs.SetInt(GetPlayedKey(soundtrackEvent), 1);
        PlayerPrefs.Save();
    }

    private void Play(SoundtrackEvent soundtrackEvent)
    {
        SetupAudioSources();

        AudioSource nextSource = inactiveSource;
        AudioSource previousSource = activeSource;
        float targetVolume = soundtrackEvent.overrideVolume ? soundtrackEvent.volume : defaultVolume;
        float fadeDuration = soundtrackEvent.overrideFadeDuration ? soundtrackEvent.fadeDuration : defaultFadeDuration;

        nextSource.clip = soundtrackEvent.clip;
        nextSource.loop = soundtrackEvent.loop;
        nextSource.volume = fadeDuration <= 0.0001f ? targetVolume : 0f;
        nextSource.Play();

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(CrossfadeRoutine(previousSource, nextSource, targetVolume, fadeDuration));
        activeSource = nextSource;
        inactiveSource = previousSource;
        soundtrackEvent.onPlayed?.Invoke();
    }

    private IEnumerator CrossfadeRoutine(AudioSource previousSource, AudioSource nextSource, float targetVolume, float duration)
    {
        float previousStartVolume = previousSource != null ? previousSource.volume : 0f;

        if (duration <= 0.0001f)
        {
            if (previousSource != null)
            {
                previousSource.Stop();
                previousSource.volume = 0f;
            }

            nextSource.volume = targetVolume;
            fadeRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            progress = progress * progress * (3f - 2f * progress);

            if (previousSource != null)
            {
                previousSource.volume = Mathf.Lerp(previousStartVolume, 0f, progress);
            }

            nextSource.volume = Mathf.Lerp(0f, targetVolume, progress);
            yield return null;
        }

        if (previousSource != null)
        {
            previousSource.Stop();
            previousSource.volume = 0f;
        }

        nextSource.volume = targetVolume;
        fadeRoutine = null;
    }

    private IEnumerator StopRoutine(float duration)
    {
        AudioSource source = activeSource;
        if (source == null)
        {
            yield break;
        }

        float startVolume = source.volume;
        if (duration <= 0.0001f)
        {
            source.Stop();
            source.volume = 0f;
            fadeRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            source.volume = Mathf.Lerp(startVolume, 0f, progress);
            yield return null;
        }

        source.Stop();
        source.volume = 0f;
        fadeRoutine = null;
    }

    private void SetupAudioSources()
    {
        if (primarySource == null)
        {
            primarySource = gameObject.AddComponent<AudioSource>();
        }

        if (secondarySource == null || secondarySource == primarySource)
        {
            secondarySource = gameObject.AddComponent<AudioSource>();
        }

        ConfigureMusicSource(primarySource);
        ConfigureMusicSource(secondarySource);

        if (activeSource == null)
        {
            activeSource = primarySource;
        }

        if (inactiveSource == null || inactiveSource == activeSource)
        {
            inactiveSource = activeSource == primarySource ? secondarySource : primarySource;
        }
    }

    private void ConfigureMusicSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = outputMixerGroup;
    }

    private string GetPlayedKey(SoundtrackEvent soundtrackEvent)
    {
        string id = soundtrackEvent.PersistentId;
        if (string.IsNullOrWhiteSpace(id))
        {
            id = soundtrackEvent.trigger + "_" + soundtrackEvent.objectiveOrPersonId + "_" + soundtrackEvent.customEventId;
        }

        return OneShotPrefix + id;
    }

    private bool CanPlayInCurrentScene()
    {
        return CanPlayInScene(SceneManager.GetActiveScene());
    }

    private bool CanPlayInScene(Scene scene)
    {
        return !onlyPlayInScene ||
            string.IsNullOrWhiteSpace(playableSceneName) ||
            string.Equals(scene.name, playableSceneName, StringComparison.Ordinal);
    }
}

public enum SoundtrackTrigger
{
    SceneEntered,
    FirstMessageSent,
    FirstRadioConversationStarted,
    ObjectiveCompleted,
    EndingStarted,
    Custom
}

[Serializable]
public class SoundtrackEvent
{
    public string eventId;
    public SoundtrackTrigger trigger;
    public string objectiveOrPersonId;
    public string customEventId;
    public AudioClip clip;
    public bool loop;
    [Min(0f)] public float delay;
    public bool rememberAcrossSave = true;
    public bool overrideVolume;
    [Range(0f, 1f)] public float volume = 0.8f;
    public bool overrideFadeDuration;
    [Min(0f)] public float fadeDuration = 2f;
    public UnityEvent onPlayed;

    public string PersistentId => !string.IsNullOrWhiteSpace(eventId) ? eventId : customEventId;

    public bool Matches(SoundtrackTrigger incomingTrigger, string incomingObjectiveOrPersonId, string incomingCustomEventId)
    {
        if (trigger != incomingTrigger)
        {
            return false;
        }

        if (trigger == SoundtrackTrigger.Custom)
        {
            return string.Equals(customEventId, incomingCustomEventId, StringComparison.Ordinal);
        }

        if (trigger == SoundtrackTrigger.ObjectiveCompleted || trigger == SoundtrackTrigger.EndingStarted)
        {
            return string.IsNullOrWhiteSpace(objectiveOrPersonId) ||
                string.Equals(objectiveOrPersonId, incomingObjectiveOrPersonId, StringComparison.Ordinal);
        }

        if (trigger == SoundtrackTrigger.FirstRadioConversationStarted)
        {
            return string.IsNullOrWhiteSpace(objectiveOrPersonId) ||
                string.Equals(objectiveOrPersonId, incomingObjectiveOrPersonId, StringComparison.Ordinal);
        }

        return true;
    }
}
