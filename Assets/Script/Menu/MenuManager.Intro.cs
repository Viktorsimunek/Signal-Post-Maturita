using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class MenuManager
{
    private enum IntroDestination
    {
        Main,
        Settings
    }

    [Header("Splash")]
    [SerializeField] private TMP_Text splashTextLabel;
    [SerializeField] private IntroDestination introDestination = IntroDestination.Settings;
    [SerializeField] private string splashText = "BORDER RELAY TERMINAL";
    [SerializeField, Range(1f, 4f)] private float splashDuration = 2.2f;
    [SerializeField] private string splashSkipHint = "PRESS ANY KEY TO SKIP";
    [SerializeField, Range(0.005f, 0.08f)] private float splashTypeDelay = 0.018f;
    [SerializeField, Range(0.005f, 0.08f)] private float menuTypeDelay = 0.012f;
    [SerializeField, Range(0.05f, 1f)] private float terminalIntroFadeDuration = 0.3f;
    [SerializeField, Range(0.05f, 1f)] private float textOutDuration = 0.22f;
    [SerializeField, Range(0f, 1f)] private float textHoldBeforeReveal = 0.1f;
    [SerializeField, Range(0.05f, 2f)] private float audioFadeDuration = 0.6f;

    private Coroutine introRoutine;
    private bool introPlaying;
    private bool skipIntroRequested;
    private float introStartMasterVolume = 1f;

    private void PrepareIntroVisualState()
    {
        introStartMasterVolume = GameSettings.MasterVolume;
        GameSettings.SetMasterVolume(0f);
        ApplyMixerVolumes();

        if (terminalText != null)
        {
            terminalText.text = string.Empty;
            terminalText.enabled = false;
        }

        if (loadingScanBars != null)
        {
            loadingScanBars.alpha = 0f;
            loadingScanBars.gameObject.SetActive(false);
        }

        if (screenTransitionBar != null)
        {
            screenTransitionBar.SnapTo01(0f);
            screenTransitionBar.gameObject.SetActive(false);
        }

        if (splashTextLabel != null)
        {
            splashTextLabel.text = string.Empty;
            splashTextLabel.enabled = true;
            SetTextAlpha(splashTextLabel, 0f);
        }

        SetIntroSecondaryUiVisible(false);
    }

    private IEnumerator PlayIntroSequence()
    {
        string splashMessage = BuildSplashText();

        yield return StartCoroutine(FadeTextAlpha(splashTextLabel, 0f, 1f, terminalIntroFadeDuration));
        StartCoroutine(FadeIntroAudio(0f, introStartMasterVolume, audioFadeDuration));

        if (!skipIntroRequested)
        {
            yield return StartCoroutine(TypeText(splashTextLabel, splashMessage, splashTypeDelay, true));
        }
        else if (splashTextLabel != null)
        {
            splashTextLabel.text = splashMessage;
        }

        float elapsed = 0f;
        while (elapsed < splashDuration && !skipIntroRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!skipIntroRequested)
        {
            yield return StartCoroutine(PlayIntroDismiss());
            if (skipIntroRequested)
            {
                FinishIntroImmediately();
                yield break;
            }

            yield return new WaitForSecondsRealtime(textHoldBeforeReveal);
            yield return StartCoroutine(RevealDestinationMenu());
            yield break;
        }

        FinishIntroImmediately();
    }

    private IEnumerator PlayIntroDismiss()
    {
        if (splashTextLabel == null)
        {
            yield break;
        }

        string source = splashTextLabel.text;
        float elapsed = 0f;

        while (elapsed < textOutDuration)
        {
            if (skipIntroRequested)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / textOutDuration);
            float scrambleStrength = 1f - t;
            splashTextLabel.text = ScrambleText(source, scrambleStrength);
            SetTextAlpha(splashTextLabel, Mathf.Lerp(1f, 0.1f, t));

            yield return null;
        }

        splashTextLabel.text = string.Empty;
        SetTextAlpha(splashTextLabel, 0f);
    }

    private IEnumerator RevealDestinationMenu()
    {
        currentScreen = introDestination == IntroDestination.Settings ? TerminalScreen.Settings : TerminalScreen.Main;
        selectedIndex = 0;
        GameSettings.SetMasterVolume(introStartMasterVolume);
        ApplyMixerVolumes();

        if (splashTextLabel != null)
        {
            splashTextLabel.enabled = false;
        }

        if (terminalText != null)
        {
            terminalText.enabled = true;
        }

        SetIntroSecondaryUiVisible(true);

        string targetText = GetCurrentScreenText();
        yield return StartCoroutine(TypeText(terminalText, targetText, menuTypeDelay, false));
        RegisterUserInput();
        StartBlinkEffect();
        PreloadGameplayScene();
    }

    private void FinishIntroImmediately()
    {
        if (loadingScanBars != null)
        {
            loadingScanBars.alpha = 0f;
            loadingScanBars.gameObject.SetActive(false);
        }

        if (screenTransitionBar != null)
        {
            screenTransitionBar.SnapTo01(0f);
            screenTransitionBar.gameObject.SetActive(false);
        }

        if (splashTextLabel != null)
        {
            splashTextLabel.text = string.Empty;
            splashTextLabel.enabled = false;
        }

        if (terminalText != null)
        {
            terminalText.enabled = true;
        }

        GameSettings.SetMasterVolume(introStartMasterVolume);
        ApplyMixerVolumes();
        SetIntroSecondaryUiVisible(true);

        currentScreen = introDestination == IntroDestination.Settings ? TerminalScreen.Settings : TerminalScreen.Main;
        selectedIndex = 0;
        RenderCurrentMenuImmediate();
        RegisterUserInput();
        PreloadGameplayScene();
    }

    private IEnumerator TypeText(TMP_Text label, string target, float delay, bool keepHintVisible)
    {
        if (label == null)
        {
            yield break;
        }

        if (skipIntroRequested)
        {
            label.text = target;
            yield break;
        }

        label.text = string.Empty;
        for (int i = 0; i < target.Length; i++)
        {
            if (skipIntroRequested)
            {
                label.text = target;
                yield break;
            }

            label.text = target.Substring(0, i + 1);

            if (keepHintVisible && i < target.Length - 1 && target.EndsWith(splashSkipHint, StringComparison.Ordinal))
            {
                int hintIndex = target.LastIndexOf(splashSkipHint, StringComparison.Ordinal);
                if (i + 1 < hintIndex)
                {
                    label.text = target.Substring(0, i + 1) + "\n\n" + splashSkipHint;
                }
            }

            yield return new WaitForSecondsRealtime(delay);
        }

        label.text = target;
    }

    private bool WasIntroSkipPressed()
    {
        if (WasAnySubmitPressed())
        {
            return true;
        }

        Keyboard kb = Keyboard.current;
        if (kb != null && kb.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        Gamepad gp = Gamepad.current;
        if (gp == null)
        {
            return false;
        }

        return gp.dpad.up.wasPressedThisFrame ||
               gp.dpad.down.wasPressedThisFrame ||
               gp.dpad.left.wasPressedThisFrame ||
               gp.dpad.right.wasPressedThisFrame ||
               gp.leftShoulder.wasPressedThisFrame ||
               gp.rightShoulder.wasPressedThisFrame ||
               gp.buttonWest.wasPressedThisFrame ||
               gp.buttonNorth.wasPressedThisFrame;
    }

    private string GetCurrentScreenText()
    {
        if (currentScreen == TerminalScreen.Main)
        {
            return BuildMenuText("BORDER RELAY TERMINAL", mainMenuItems, selectedIndex);
        }

        return BuildSettingsMenuText();
    }

    private void SetIntroSecondaryUiVisible(bool visible)
    {
        if (statusText != null)
        {
            statusText.enabled = visible;
        }

        if (eventText != null)
        {
            eventText.enabled = visible;
            if (!visible)
            {
                eventText.text = string.Empty;
            }
        }
    }

    private static string ScrambleText(string source, float amount)
    {
        if (string.IsNullOrEmpty(source))
        {
            return string.Empty;
        }

        const string noiseChars = "#$%01/\\=-_";
        char[] chars = source.ToCharArray();

        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] == '\n' || chars[i] == ' ')
            {
                continue;
            }

            if (UnityEngine.Random.value < amount * 0.75f)
            {
                chars[i] = noiseChars[UnityEngine.Random.Range(0, noiseChars.Length)];
            }
            else if (UnityEngine.Random.value < amount * 0.35f)
            {
                chars[i] = ' ';
            }
        }

        return new string(chars);
    }

    private string BuildSplashText()
    {
        return splashText + "\n\n" + splashSkipHint;
    }

    private IEnumerator FadeTextAlpha(TMP_Text label, float from, float to, float duration)
    {
        if (label == null)
        {
            yield break;
        }

        if (duration <= 0.0001f)
        {
            SetTextAlpha(label, to);
            yield break;
        }

        SetTextAlpha(label, from);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (skipIntroRequested)
            {
                SetTextAlpha(label, to);
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetTextAlpha(label, Mathf.Lerp(from, to, t));
            yield return null;
        }

        SetTextAlpha(label, to);
    }

    private static void SetTextAlpha(TMP_Text label, float alpha)
    {
        if (label == null)
        {
            return;
        }

        Color color = label.color;
        color.a = alpha;
        label.color = color;
    }

    private IEnumerator FadeIntroAudio(float from, float to, float duration)
    {
        if (duration <= 0.0001f)
        {
            GameSettings.SetMasterVolume(to);
            ApplyMixerVolumes();
            yield break;
        }

        float elapsed = 0f;
        GameSettings.SetMasterVolume(from);
        ApplyMixerVolumes();

        while (elapsed < duration)
        {
            if (skipIntroRequested)
            {
                GameSettings.SetMasterVolume(to);
                ApplyMixerVolumes();
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            GameSettings.SetMasterVolume(Mathf.Lerp(from, to, t));
            ApplyMixerVolumes();
            yield return null;
        }

        GameSettings.SetMasterVolume(to);
        ApplyMixerVolumes();
    }
}
