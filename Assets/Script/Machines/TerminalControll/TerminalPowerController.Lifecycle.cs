using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class TerminalPowerController
{
    public void PlaySceneWelcomeHandoff(string welcomeText, float holdDuration, float fadeDuration)
    {
        powerOffOnNextFocusExit = true;
        StopAllRoutines();
        powerRoutine = StartCoroutine(SceneWelcomeHandoffRoutine(welcomeText, holdDuration, fadeDuration));
    }

    public void PlaySceneScreenshotHandoff(Texture handoffTexture, string welcomeText, float holdDuration, float fadeDuration)
    {
        powerOffOnNextFocusExit = true;
        StopAllRoutines();
        powerRoutine = StartCoroutine(SceneScreenshotHandoffRoutine(handoffTexture, welcomeText, holdDuration, fadeDuration));
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureMenuItems();
        CacheInitialStates();
        EnsureRuntimeComponents();
        InitializeRuntimeState();
        ApplyImmediateOffState();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (terminalControl != null)
        {
            terminalControl.FocusEntered += HandleFocusEntered;
            terminalControl.FocusExited += HandleFocusExited;
        }

        SubscribePowerFuse();
        SignalHistory.Changed -= HandleSignalHistoryChanged;
        SignalHistory.Changed += HandleSignalHistoryChanged;
    }

    private void OnDisable()
    {
        if (terminalControl != null)
        {
            terminalControl.FocusEntered -= HandleFocusEntered;
            terminalControl.FocusExited -= HandleFocusExited;
        }

        UnsubscribePowerFuse();
        SignalHistory.Changed -= HandleSignalHistoryChanged;

        StopAllRoutines();
        ApplyImmediateOffState();
    }

    private void OnValidate()
    {
        materialIndex = Mathf.Max(0, materialIndex);
        typeDelay = Mathf.Max(0.005f, typeDelay);
        splashHoldDuration = Mathf.Max(0f, splashHoldDuration);
        bootCompleteHoldDuration = Mathf.Max(0f, bootCompleteHoldDuration);
        shutdownHoldDuration = Mathf.Max(0f, shutdownHoldDuration);
        blinkDuration = Mathf.Max(0.02f, blinkDuration);
        blinkMinAlpha = Mathf.Clamp01(blinkMinAlpha);
        screenTransitionDuration = Mathf.Max(0.05f, screenTransitionDuration);
        loadingStepDurationRange = SortVector(loadingStepDurationRange);
        cursorPitchRange = SortVector(cursorPitchRange);
        startupPitchRange = SortVector(startupPitchRange);
        shutdownPitchRange = SortVector(shutdownPitchRange);
        ambientFadeInDuration = Mathf.Max(0f, ambientFadeInDuration);
        ambientFadeOutDuration = Mathf.Max(0f, ambientFadeOutDuration);
        ambientVolume = Mathf.Max(0f, ambientVolume);
        dynamicUiRefreshInterval = Mathf.Clamp(dynamicUiRefreshInterval, 0f, 0.25f);
        cursorVolume = Mathf.Max(0f, cursorVolume);
        confirmVolume = Mathf.Max(0f, confirmVolume);
        errorVolume = Mathf.Max(0f, errorVolume);
        startupVolume = Mathf.Max(0f, startupVolume);
        startupNoiseVolume = Mathf.Max(0f, startupNoiseVolume);
        shutdownVolume = Mathf.Max(0f, shutdownVolume);
        staticVolume = Mathf.Max(0f, staticVolume);
        maxRuntimeLogEntries = Mathf.Max(1, maxRuntimeLogEntries);
        maxTypedMessageLength = Mathf.Max(1, maxTypedMessageLength);
        maxTypedCodeLength = Mathf.Max(1, maxTypedCodeLength);
        maxPersonCodeLength = Mathf.Max(1, maxPersonCodeLength);
        peopleDatabaseLookupDuration = Mathf.Max(0.1f, peopleDatabaseLookupDuration);
        selectedMenuIndex = Mathf.Clamp(selectedMenuIndex, 0, Mathf.Max(GetMenuItemCount() - 1, 0));

        if (!Application.isPlaying)
        {
            ResolveReferences();
            EnsureMenuItems();
#if UNITY_EDITOR
            TryAssignDefaultAssetsInEditor();
#endif
        }
    }

    private void Update()
    {
        if (!HasFusePower)
        {
            if (powerState != PowerState.Off)
            {
                if (powerRoutine != null)
                {
                    StopCoroutine(powerRoutine);
                    powerRoutine = null;
                }

                PlayShutdownSound();
                PlayStaticBurst();
                ApplyImmediateOffState();
            }

            return;
        }

        UpdateTransmitFinishedState(false);

        if (powerState != PowerState.Off && (dynamicUiRefreshInterval <= 0f || Time.unscaledTime >= nextStatusRefreshTime))
        {
            nextStatusRefreshTime = dynamicUiRefreshInterval <= 0f
                ? Time.unscaledTime
                : Time.unscaledTime + dynamicUiRefreshInterval;
            RefreshDynamicUi();
        }

        if (powerState != PowerState.Ready || terminalControl == null || !terminalControl.IsFocused)
        {
            return;
        }

        HandleReadyInput();
    }
}
