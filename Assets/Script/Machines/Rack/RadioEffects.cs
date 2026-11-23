using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Text, fades and sound live here. RadioDialogueUI only decides what happens next.
public class RadioEffects : MonoBehaviour
{
    [Header("Text and panel")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text radioText;
    [SerializeField] private float fadeTime = 0.2f;
    [SerializeField] private float textShuffleDuration = 0.18f;
    [SerializeField] private float talkingLetterDelay = 0.025f;
    [SerializeField] private float tuneTime = 1.2f;
    [SerializeField] private string shuffleCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    [SerializeField] private int tuningCharacterCount = 32;

    [Header("Radio sound")]
    [SerializeField] private AudioSource openStaticSource;
    [SerializeField] private AudioSource incomingCallSource;
    [SerializeField] private AudioSource tuningSource;
    [SerializeField] private AudioClip openStaticLoop;
    [SerializeField] private AudioClip incomingCallLoop;
    [SerializeField] private AudioClip tuningLoop;
    [SerializeField] private float openStaticVolume = 0.25f;
    [SerializeField] private float incomingCallVolume = 0.55f;
    [SerializeField] private float tuningVolume = 0.45f;
    [SerializeField] private float audioFadeTime = 0.35f;

    [Header("Voices")]
    [SerializeField] private AudioSource voiceSource;
    [SerializeField] private AudioClip womanVoice;
    [SerializeField] private AudioClip farManVoice;
    [SerializeField] private AudioClip manVoice;
    [SerializeField] private AudioClip childVoice;
    [SerializeField] private float voiceVolume = 0.55f;
    [SerializeField] private Vector2 voicePitchRange = new Vector2(0.95f, 1.05f);
    [SerializeField] private int voiceSoundEveryLetters = 2;
    [SerializeField] private float voiceBitLength = 0.05f;
    [SerializeField] private float voiceBitFadeTime = 0.006f;

    private readonly TextShuffle shuffle = new TextShuffle();
    private Coroutine textRoutine;
    private Coroutine voiceRoutine;
    private bool visible;
    private float staticTarget;
    private float callTarget;
    private float tuningTarget;

    public bool IsSpeaking { get; private set; }
    public string Text => radioText != null ? radioText.text : "";

    private void Awake()
    {
        var usedSources = new HashSet<AudioSource>();
        openStaticSource = SetupAudio(openStaticSource, openStaticLoop, usedSources);
        incomingCallSource = SetupAudio(incomingCallSource, incomingCallLoop, usedSources);
        tuningSource = SetupAudio(tuningSource, tuningLoop, usedSources);
        voiceSource = SetupAudio(voiceSource, null, usedSources);
        voiceSource.loop = false;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        Show(false);
    }

    private void Update()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, visible ? 1f : 0f,
                Time.unscaledDeltaTime / Mathf.Max(0.001f, fadeTime));
        }
        FadeSound(openStaticSource, staticTarget);
        FadeSound(incomingCallSource, callTarget);
        FadeSound(tuningSource, tuningTarget);
    }

    public void Show(bool show)
    {
        visible = show;
        if (canvasGroup == null) return;
        canvasGroup.interactable = show;
        canvasGroup.blocksRaycasts = show;
    }

    public void SetSound(bool powered, bool radioOpen, bool incomingCall)
    {
        staticTarget = powered && radioOpen ? openStaticVolume : 0f;
        callTarget = powered && !radioOpen && incomingCall ? incomingCallVolume : 0f;
        if (!powered) Silence();
    }

    public AudioClip GetVoice(string voice)
    {
        switch (voice)
        {
            case "Woman": return womanVoice;
            case "FarMan": return farManVoice;
            case "Man": return manVoice;
            case "Child": return childVoice;
            default: return null;
        }
    }

    public void SetText(string text, bool shuffle = false)
    {
        CancelText();
        if (radioText == null) return;
        radioText.maxVisibleCharacters = int.MaxValue;
        if (shuffle && isActiveAndEnabled) textRoutine = StartCoroutine(Shuffle(text ?? ""));
        else radioText.text = text ?? "";
    }

    public void Speak(string text, AudioClip voice, Action onFinished)
    {
        CancelText();
        IsSpeaking = true;
        textRoutine = StartCoroutine(TypeText(text ?? "", voice, onFinished));
    }

    private IEnumerator TypeText(string text, AudioClip voice, Action onFinished)
    {
        yield return null;
        yield return Typewriter.Write(radioText, text, talkingLetterDelay, (letter, index) =>
        {
            if (voice == null || char.IsWhiteSpace(letter) || index % Mathf.Max(1, voiceSoundEveryLetters) != 0) return;
            if (voiceRoutine != null) StopCoroutine(voiceRoutine);
            voiceRoutine = StartCoroutine(VoiceBit(voice));
        });
        StopVoice();
        IsSpeaking = false;
        textRoutine = null;
        onFinished?.Invoke();
    }

    private IEnumerator Shuffle(string text)
    {
        yield return null;
        yield return shuffle.Play(radioText, text, textShuffleDuration, shuffleCharacters);
        textRoutine = null;
    }

    public IEnumerator TuneNoise()
    {
        tuningTarget = tuningVolume;
        char[] noise = new char[Mathf.Max(1, tuningCharacterCount)];
        for (float time = 0f; time < tuneTime; time += Time.unscaledDeltaTime)
        {
            for (int i = 0; i < noise.Length; i++) noise[i] = RandomCharacter();
            if (radioText != null) radioText.text = new string(noise) + new string('.', 1 + (int)(time * 7f) % 4);
            yield return null;
        }
        tuningTarget = 0f;
    }

    private char RandomCharacter()
    {
        if (string.IsNullOrEmpty(shuffleCharacters)) return '.';
        return shuffleCharacters[UnityEngine.Random.Range(0, shuffleCharacters.Length)];
    }

    private IEnumerator VoiceBit(AudioClip clip)
    {
        float duration = Mathf.Max(0.001f, Mathf.Min(voiceBitLength, talkingLetterDelay * Mathf.Max(1, voiceSoundEveryLetters) * 0.85f));
        float fade = Mathf.Clamp(voiceBitFadeTime, 0.001f, duration * 0.45f);
        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.pitch = UnityEngine.Random.Range(voicePitchRange.x, voicePitchRange.y);
        voiceSource.time = UnityEngine.Random.Range(0f, Mathf.Max(0f, clip.length - duration));
        voiceSource.volume = 0f;
        voiceSource.Play();
        for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
        {
            float envelope = Mathf.Min(time / fade, (duration - time) / fade);
            voiceSource.volume = voiceVolume * Mathf.Clamp01(envelope);
            yield return null;
        }
        voiceSource.Stop();
        voiceRoutine = null;
    }

    public void CancelText()
    {
        if (textRoutine != null) StopCoroutine(textRoutine);
        textRoutine = null;
        IsSpeaking = false;
        tuningTarget = 0f;
        StopVoice();
    }

    private void StopVoice()
    {
        if (voiceRoutine != null) StopCoroutine(voiceRoutine);
        voiceRoutine = null;
        if (voiceSource != null) voiceSource.Stop();
    }

    private void FadeSound(AudioSource source, float target)
    {
        if (source == null) return;
        if (target > 0f && source.clip != null && !source.isPlaying) source.Play();
        source.volume = Mathf.MoveTowards(source.volume, target, Time.unscaledDeltaTime / Mathf.Max(0.001f, audioFadeTime));
        if (target == 0f && source.volume == 0f) source.Stop();
    }

    private AudioSource SetupAudio(AudioSource source, AudioClip clip, HashSet<AudioSource> used)
    {
        if (source == null || used.Contains(source))
        {
            var template = source;
            source = gameObject.AddComponent<AudioSource>();
            if (template != null)
            {
                source.outputAudioMixerGroup = template.outputAudioMixerGroup;
                source.spatialBlend = template.spatialBlend;
                source.minDistance = template.minDistance;
                source.maxDistance = template.maxDistance;
                source.rolloffMode = template.rolloffMode;
            }
        }
        used.Add(source);
        source.playOnAwake = false;
        source.loop = true;
        source.Stop();
        source.volume = 0f;
        source.clip = clip;
        return source;
    }

    private void Silence()
    {
        staticTarget = callTarget = tuningTarget = 0f;
        foreach (var source in new[] { openStaticSource, incomingCallSource, tuningSource, voiceSource })
        {
            if (source == null) continue;
            source.Stop();
            source.volume = 0f;
        }
    }

    private void OnDisable()
    {
        CancelText();
        Silence();
    }
}
