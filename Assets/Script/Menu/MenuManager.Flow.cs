using System;
using System.Collections;
using TMPro;
using UnityEngine;

public partial class MenuManager
{
    private IEnumerator ConfirmSelectionRoutine()
    {
        if (currentScreen == TerminalScreen.Loading || currentScreen == TerminalScreen.Splash)
        {
            yield break;
        }

        PlaySfx(confirmBeep);

        if (currentScreen == TerminalScreen.Main)
        {
            string chosenMainItem = mainMenuItems[selectedIndex];
            if (chosenMainItem == "NEW GAME")
            {
                StartNewGameFlow();
                yield break;
            }

            if (chosenMainItem == "SETTINGS")
            {
                RequestOpenSettingsMenu();
                yield break;
            }

            if (chosenMainItem == "EXIT")
            {
                yield return StartCoroutine(LoadingTransitionRoutine(string.Empty, true));
                yield break;
            }

            if (!GameProgress.HasSave)
            {
                PlayErrorSfx();
                ShowEventMessage("NO SAVED OPERATOR PROFILE", 1.3f);
                RenderCurrentMenu();
                yield break;
            }

            yield return StartCoroutine(LoadingTransitionRoutine(continueScene, false));
            yield break;
        }

        string chosenSettingsItem = settingsItems[selectedIndex];
        if (chosenSettingsItem == "INPUT BINDINGS")
        {
            RequestOpenInputBindingsMenu();
            yield break;
        }

        if (chosenSettingsItem == "BACK")
        {
            RequestOpenMainMenu();
            yield break;
        }

        if (chosenSettingsItem == "RESET DEFAULTS")
        {
            GameSettings.ResetToDefaults();
            CommitSettings("SETTINGS RESET");
            yield break;
        }

        if (chosenSettingsItem == "DISPLAY MODE" || chosenSettingsItem == "INPUT MODE" || chosenSettingsItem == "INVERT Y" || chosenSettingsItem == "V SYNC" || chosenSettingsItem == "FPS DISPLAY")
        {
            AdjustCurrentSetting(1);
            yield break;
        }

        AdjustCurrentSetting(1);
    }

    private IEnumerator LoadingTransitionRoutine(string sceneName, bool quitApp)
    {
        currentScreen = TerminalScreen.Loading;
        terminalText.text = "LOADING MODULE...";
        ShowEventMessage(string.Empty);
        PlaySfx(staticBurst);

        float duration = UnityEngine.Random.Range(loadingScanDurationRange.x, loadingScanDurationRange.y);
        yield return StartCoroutine(PlayLoadingScanBars(duration));

        if (quitApp)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            yield break;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            OpenMainMenu();
            yield break;
        }

        string playerName = string.IsNullOrWhiteSpace(PlayerProfile.PlayerName) ? "OPERATOR" : PlayerProfile.PlayerName;
        TMP_Text welcomeText = ShowCenteredTransferText("WELCOME BACK " + playerName + "!");

        SmoothSceneTransition.BeginTerminalTransfer(
            this,
            sceneName,
            welcomeText,
            transitionCaptureCamera,
            newGameSceneTransferDuration,
            newGameSceneTransferFadeDuration,
            targetMixer,
            masterVolumeParam,
            audioFadeDuration);

        yield return null;
    }

    private TMP_Text ShowCenteredTransferText(string text)
    {
        if (terminalText != null)
        {
            terminalText.text = string.Empty;
            terminalText.enabled = false;
        }

        SetIntroSecondaryUiVisible(false);

        if (splashTextLabel != null)
        {
            splashTextLabel.text = text;
            splashTextLabel.enabled = true;
            SetTextAlpha(splashTextLabel, 1f);
            return splashTextLabel;
        }

        if (terminalText != null)
        {
            terminalText.enabled = true;
            terminalText.alignment = TMPro.TextAlignmentOptions.Center;
            terminalText.text = text;
            return terminalText;
        }

        return null;
    }

    private IEnumerator PlayLoadingScanBars(float duration)
    {
        if (loadingScanBars == null)
        {
            if (screenTransitionBar != null)
            {
                yield return StartCoroutine(PlaySinglePassTransition(duration, true));
            }
            else
            {
                yield return new WaitForSecondsRealtime(duration);
            }

            yield break;
        }

        loadingScanBars.gameObject.SetActive(true);
        RectTransform barsRect = loadingScanBars.GetComponent<RectTransform>();
        float elapsed = 0f;

        if (screenTransitionBar != null)
        {
            screenTransitionBar.gameObject.SetActive(true);
            screenTransitionBar.SnapTo01(0f);
            screenTransitionBar.Set01(1f);
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            loadingScanBars.alpha = Mathf.Sin(t * Mathf.PI) * 0.9f;

            if (barsRect != null)
            {
                Vector2 pos = barsRect.anchoredPosition;
                pos.y = Mathf.Lerp(-scanSweepDistance, scanSweepDistance, t);
                barsRect.anchoredPosition = pos;
            }

            yield return null;
        }

        loadingScanBars.alpha = 0f;
        if (screenTransitionBar != null)
        {
            screenTransitionBar.SnapTo01(0f);
            screenTransitionBar.gameObject.SetActive(false);
        }
        if (barsRect != null)
        {
            Vector2 resetPos = barsRect.anchoredPosition;
            resetPos.y = 0f;
            barsRect.anchoredPosition = resetPos;
        }
        loadingScanBars.gameObject.SetActive(false);
    }

    private void OpenMainMenu()
    {
        ApplyScreenState(TerminalScreen.Main);
    }

    private void OpenSettingsMenu()
    {
        ApplyScreenState(TerminalScreen.Settings);
    }

    private void RenderCurrentMenu()
    {
        if (introPlaying)
        {
            return;
        }

        RenderCurrentMenuImmediate();
    }

    private void RenderCurrentMenuImmediate()
    {
        if (terminalText != null)
        {
            terminalText.alignment = TMPro.TextAlignmentOptions.TopLeft;
        }

        if (currentScreen == TerminalScreen.Main)
        {
            terminalText.text = BuildMenuText("BORDER RELAY TERMINAL", BuildMainMenuLabels(), selectedIndex);
            return;
        }

        if (currentScreen == TerminalScreen.NewGameProfile)
        {
            terminalText.text = BuildNewGameProfileText();
            return;
        }

        if (currentScreen == TerminalScreen.NewGameDifficulty)
        {
            terminalText.text = BuildNewGameDifficultyText();
            return;
        }

        if (currentScreen == TerminalScreen.NewGameConfirm)
        {
            terminalText.text = BuildNewGameConfirmText();
            return;
        }

        if (currentScreen == TerminalScreen.InputBindings)
        {
            terminalText.text = BuildInputBindingsMenuText();
            return;
        }

        terminalText.text = BuildSettingsMenuText();
    }

    private void RequestOpenMainMenu()
    {
        StartScreenTransition(TerminalScreen.Main);
    }

    private void RequestOpenSettingsMenu()
    {
        StartScreenTransition(TerminalScreen.Settings);
    }

    private void RequestOpenInputBindingsMenu()
    {
        StartScreenTransition(TerminalScreen.InputBindings);
    }

    private void StartScreenTransition(TerminalScreen targetScreen)
    {
        if (currentScreen == targetScreen || introPlaying)
        {
            ApplyScreenState(targetScreen);
            return;
        }

        if (screenTransitionRoutine != null)
        {
            StopCoroutine(screenTransitionRoutine);
        }

        screenTransitionRoutine = StartCoroutine(StartScreenTransitionRoutine(targetScreen, null));
    }

    private IEnumerator StartScreenTransitionRoutine(TerminalScreen targetScreen, Action onCovered)
    {
        screenTransitionPlaying = true;

        if (screenTransitionBar == null)
        {
            onCovered?.Invoke();
            ApplyScreenState(targetScreen);
            screenTransitionPlaying = false;
            screenTransitionRoutine = null;
            yield break;
        }

        screenTransitionBar.gameObject.SetActive(true);
        screenTransitionBar.SnapTo01(0f);
        screenTransitionBar.Set01(1f);

        yield return StartCoroutine(WaitForTransitionProgress(0.5f, menuTransitionDuration));

        onCovered?.Invoke();
        ApplyScreenState(targetScreen);
        StartBlinkEffect();

        yield return StartCoroutine(WaitForTransitionProgress(1f, menuTransitionDuration));

        screenTransitionBar.SnapTo01(0f);
        screenTransitionBar.gameObject.SetActive(false);
        screenTransitionPlaying = false;
        screenTransitionRoutine = null;
    }

    private IEnumerator PlaySinglePassTransition(float maxDuration, bool hideWhenDone)
    {
        if (screenTransitionBar == null)
        {
            yield break;
        }

        screenTransitionBar.gameObject.SetActive(true);
        screenTransitionBar.SnapTo01(0f);
        screenTransitionBar.Set01(1f);
        yield return StartCoroutine(WaitForTransitionProgress(1f, maxDuration));
        screenTransitionBar.SnapTo01(0f);

        if (hideWhenDone)
        {
            screenTransitionBar.gameObject.SetActive(false);
        }
    }

    private IEnumerator WaitForTransitionProgress(float targetProgress, float maxDuration)
    {
        if (screenTransitionBar == null)
        {
            yield break;
        }

        float elapsed = 0f;
        targetProgress = Mathf.Clamp01(targetProgress);
        maxDuration = Mathf.Max(0.01f, maxDuration);

        while (elapsed < maxDuration)
        {
            if (screenTransitionBar.DisplayedNormalizedValue >= targetProgress)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void ApplyScreenState(TerminalScreen targetScreen)
    {
        if (targetScreen != TerminalScreen.InputBindings)
        {
            waitingForBinding = false;
        }

        currentScreen = targetScreen;
        selectedIndex = 0;
        FadeMenuMusic(targetScreen != TerminalScreen.NewGameProfile && targetScreen != TerminalScreen.Loading);
        RegisterUserInput();
        RenderCurrentMenuImmediate();
        PreloadGameplayScene();
    }

    private void PreloadGameplayScene()
    {
        if (gameplayPreloadRequested)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(newGameScene) &&
            !string.IsNullOrWhiteSpace(continueScene) &&
            !string.Equals(newGameScene, continueScene, StringComparison.Ordinal))
        {
            return;
        }

        string sceneName = !string.IsNullOrWhiteSpace(newGameScene) ? newGameScene : continueScene;
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        gameplayPreloadRequested = true;
        SmoothSceneTransition.PreloadScene(sceneName);
    }

    private IEnumerator PreloadGameplaySceneAfterStartup()
    {
        yield return null;
        yield return null;

        PreloadGameplayScene();
    }
}
