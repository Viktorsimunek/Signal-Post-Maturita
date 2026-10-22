using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class TerminalPowerController
{
    private void ResolveReferences()
    {
        if (terminalControl == null)
        {
            terminalControl = GetComponent<TerminalControl>();
        }

        if (timeOfDayController == null)
        {
            timeOfDayController = FindFirstObjectByType<TimeOfDayController>();
        }

        if (messageTransmitter == null)
        {
            messageTransmitter = FindFirstObjectByType<MessageTransmitter>();
        }

        if (monitorOutput == null)
        {
            monitorOutput = GetComponent<CameraToTextureWithShader>();
        }

        if (sourceCamera == null && monitorOutput != null)
        {
            sourceCamera = monitorOutput.SourceCamera;
        }

        if (screenCanvas == null)
        {
            screenCanvas = GetComponentInChildren<Canvas>(true);
        }

        if (mainText == null && screenCanvas != null)
        {
            TMP_Text[] texts = screenCanvas.GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length > 0)
            {
                mainText = texts[0];
            }

            if (texts.Length > 1)
            {
                statusText = texts[1];
            }

            if (texts.Length > 2)
            {
                eventText = texts[2];
            }
        }

        if (monitorRenderer == null && monitorOutput != null)
        {
            monitorRenderer = monitorOutput.MonitorRenderer;
            materialIndex = monitorOutput.MaterialIndex;
        }

        if (terminalCanvasGroup == null && screenCanvas != null)
        {
            terminalCanvasGroup = screenCanvas.GetComponent<CanvasGroup>();
        }

        if (screenTransitionBar == null && screenCanvas != null)
        {
            screenTransitionBar = screenCanvas.GetComponentInChildren<SliderBar>(true);
        }
    }

    private void SubscribePowerFuse()
    {
        if (powerFuse != null)
        {
            powerFuse.PowerChanged -= HandlePowerFuseChanged;
            powerFuse.PowerChanged += HandlePowerFuseChanged;
        }
    }

    private void UnsubscribePowerFuse()
    {
        if (powerFuse != null)
        {
            powerFuse.PowerChanged -= HandlePowerFuseChanged;
        }
    }

    private void HandlePowerFuseChanged(FuseSwitch fuse, bool powered)
    {
        if (powered)
        {
            AddLog("TERMINAL FUSE RESTORED");
            if (autoPowerOnWhenFocused && terminalControl != null && terminalControl.IsFocused)
            {
                TurnOn();
            }
            return;
        }

        AddLog("TERMINAL FUSE TRIPPED");
        if (powerRoutine != null)
        {
            StopCoroutine(powerRoutine);
            powerRoutine = null;
        }

        PlayShutdownSound();
        PlayStaticBurst();
        ApplyImmediateOffState();
    }

    private void EnsureRuntimeComponents()
    {
        if (screenCanvas != null && terminalCanvasGroup == null)
        {
            terminalCanvasGroup = screenCanvas.gameObject.AddComponent<CanvasGroup>();
        }

        EnsureAudioSources();
    }

    private void EnsureAudioSources()
    {
        if (uiAudioSource == null)
        {
            uiAudioSource = GetComponent<AudioSource>();
        }

        if (uiAudioSource == null)
        {
            uiAudioSource = gameObject.AddComponent<AudioSource>();
        }

        if (ambientAudioSource == null)
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null && sources[i] != uiAudioSource)
                {
                    ambientAudioSource = sources[i];
                    break;
                }
            }
        }

        if (ambientAudioSource == null)
        {
            ambientAudioSource = gameObject.AddComponent<AudioSource>();
        }

        ConfigureUiAudioSource(uiAudioSource);
        ConfigureAmbientAudioSource(ambientAudioSource);
    }

#if UNITY_EDITOR
    private void TryAssignDefaultAssetsInEditor()
    {
        if (cursorBeep == null)
        {
            cursorBeep = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Sfx/Terminal/Terminal_click.mp3");
        }

        if (confirmBeep == null)
        {
            confirmBeep = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Sfx/Terminal/Terminal_confirm.mp3");
        }

        if (errorBeep == null)
        {
            errorBeep = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Sfx/Terminal/Terminal_Error.mp3");
        }

        if (startupClip == null)
        {
            startupClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Sfx/Terminal/Terminal_confirm2.mp3");
        }

        if (shutdownClip == null)
        {
            shutdownClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Sfx/Terminal/Terminal_confirm.mp3");
        }

        if (staticBurst == null)
        {
            staticBurst = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Sfx/Terminal/Terminal_noise.mp3");
        }

        if (ambientLoopClip == null)
        {
            ambientLoopClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Sfx/Terminal/Terminal_ambient.mp3");
        }
    }
#endif

    private void ConfigureUiAudioSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = audioSpatialBlend;
    }

    private void ConfigureAmbientAudioSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.loop = true;
        source.clip = ambientLoopClip;
        source.volume = 0f;
        source.spatialBlend = audioSpatialBlend;
    }

    private void CacheInitialStates()
    {
        cachedActiveStates = CacheObjectStates(activateWhilePowered);
        cachedBehaviourStates = CacheBehaviourStates(enableWhilePowered);
    }

    private void EnsureMenuItems()
    {
        if (readyMenuItems == null || readyMenuItems.Length == 0)
        {
            readyMenuItems = new[] { "CHECK STATUS", "SEND MESSAGE", "SIGNAL HISTORY", "PERSON DATABASE", "OPEN LOGS", "POWER DOWN" };
            return;
        }

        bool hasSend = false;
        bool hasHistory = false;
        bool hasPeople = false;
        int writeIndex = 0;
        string[] cleanedItems = new string[readyMenuItems.Length + 3];

        for (int i = 0; i < readyMenuItems.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(readyMenuItems[i]))
            {
                continue;
            }

            if (ContainsToken(readyMenuItems[i], "SEND") || ContainsToken(readyMenuItems[i], "TRANSMIT"))
            {
                hasSend = true;
            }

            if (ContainsToken(readyMenuItems[i], "HISTORY"))
            {
                hasHistory = true;
            }

            if (ContainsToken(readyMenuItems[i], "PERSON") || ContainsToken(readyMenuItems[i], "PEOPLE") || ContainsToken(readyMenuItems[i], "DATABASE"))
            {
                hasPeople = true;
            }

            if (ContainsToken(readyMenuItems[i], "POWER"))
            {
                continue;
            }

            cleanedItems[writeIndex] = readyMenuItems[i];
            writeIndex++;
        }

        if (!hasSend)
        {
            cleanedItems[writeIndex] = "SEND MESSAGE";
            writeIndex++;
        }

        if (!hasHistory)
        {
            cleanedItems[writeIndex] = "SIGNAL HISTORY";
            writeIndex++;
        }

        if (!hasPeople)
        {
            cleanedItems[writeIndex] = "PERSON DATABASE";
            writeIndex++;
        }

        cleanedItems[writeIndex] = "POWER DOWN";
        writeIndex++;

        string[] newItems = new string[writeIndex];
        for (int i = 0; i < writeIndex; i++)
        {
            newItems[i] = cleanedItems[i];
        }

        readyMenuItems = newItems;
        selectedMenuIndex = Mathf.Clamp(selectedMenuIndex, 0, Mathf.Max(GetMenuItemCount() - 1, 0));
    }

    private void InitializeRuntimeState()
    {
        runtimeLogs.Clear();
        typedMessage = string.Empty;
        typedConfirmCode = string.Empty;
        typedPersonCode = string.Empty;
        personKeyboardRow = 0;
        personKeyboardColumn = 0;
        personLookupInProgress = false;
        personLookupCompleted = false;
        personLookupResultPageOpen = false;
        currentPersonRecord = null;
        personLookupMessage = "ENTER PERSON CODE";
        powerOffOnNextFocusExit = false;
        transmitPage = TransmitPage.Message;
        transmitKeyboardRow = 0;
        transmitKeyboardColumn = 0;
        AddLog("TERMINAL STANDBY");
    }

    private void StopAllRoutines()
    {
        if (powerRoutine != null)
        {
            StopCoroutine(powerRoutine);
            powerRoutine = null;
        }

        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        if (eventRoutine != null)
        {
            StopCoroutine(eventRoutine);
            eventRoutine = null;
        }

        if (ambientFadeRoutine != null)
        {
            StopCoroutine(ambientFadeRoutine);
            ambientFadeRoutine = null;
        }

        if (personLookupRoutine != null)
        {
            StopCoroutine(personLookupRoutine);
            personLookupRoutine = null;
        }

        personLookupInProgress = false;
        personLookupResultPageOpen = false;

        if (messageTransmitter != null && messageTransmitter.IsSending)
        {
            messageTransmitter.CancelSend();
        }
    }

    private void ApplyImmediateOffState()
    {
        powerState = PowerState.Off;
        currentView = ReadyView.Menu;
        selectedMenuIndex = 0;
        powerConfirmIndex = 1;
        typedMessage = string.Empty;
        typedConfirmCode = string.Empty;
        typedPersonCode = string.Empty;
        personKeyboardRow = 0;
        personKeyboardColumn = 0;
        personLookupInProgress = false;
        personLookupCompleted = false;
        personLookupResultPageOpen = false;
        currentPersonRecord = null;
        personLookupMessage = "ENTER PERSON CODE";
        transmitPage = TransmitPage.Message;
        transmitKeyboardRow = 0;
        transmitKeyboardColumn = 0;
        if (terminalControl != null)
        {
            terminalControl.BlockExitInput = false;
            terminalControl.BlockInteractExitInput = false;
        }
        operatorLinkState = "OFFLINE";
        footerText = menuFooterBlock;
        transientEventText = string.Empty;
        nextStatusRefreshTime = 0f;

        if (screenTransitionBar != null)
        {
            screenTransitionBar.SnapTo01(0f);
            screenTransitionBar.gameObject.SetActive(false);
        }

        if (terminalCanvasGroup != null)
        {
            terminalCanvasGroup.alpha = 1f;
        }

        ApplyMonitorMaterialState(false);
        StopAmbientImmediate();
        SetPoweredObjects(false);
        ClearTexts();
    }
}
