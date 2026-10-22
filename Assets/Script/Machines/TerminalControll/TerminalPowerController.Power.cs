using System.Collections;
using UnityEngine;

public partial class TerminalPowerController
{
    [ContextMenu("Turn On")]
    public void TurnOn()
    {
        if (powerState == PowerState.Booting || powerState == PowerState.Ready || !HasFusePower)
        {
            if (!HasFusePower)
            {
                PlayErrorSfx();
                AddLog("POWER REQUEST BLOCKED: FUSE OFF");
            }

            return;
        }

        if (powerRoutine != null)
        {
            StopCoroutine(powerRoutine);
        }

        powerRoutine = StartCoroutine(PowerOnRoutine());
    }

    [ContextMenu("Turn Off")]
    public void TurnOff()
    {
        if (powerState == PowerState.Off || powerState == PowerState.ShuttingDown)
        {
            return;
        }

        if (powerRoutine != null)
        {
            StopCoroutine(powerRoutine);
        }

        powerRoutine = StartCoroutine(PowerOffRoutine());
    }

    [ContextMenu("Toggle Power")]
    public void TogglePower()
    {
        if (IsPowered)
        {
            TurnOff();
            return;
        }

        TurnOn();
    }

    private IEnumerator PowerOnRoutine()
    {
        powerState = PowerState.Booting;
        currentView = ReadyView.Menu;
        selectedMenuIndex = 0;
        powerConfirmIndex = 1;
        operatorLinkState = terminalControl != null && terminalControl.IsFocused ? "OPERATOR LINKED" : idleOperatorLabel;

        SetPoweredObjects(true);
        ApplyMonitorMaterialState(true);
        PrepareBootView();
        AddLog("POWER ROUTED TO TERMINAL");

        PlayStaticBurst();
        PlayStartupSound();
        StartBlinkEffect();

        if (playTransitionOnBoot)
        {
            yield return StartCoroutine(PlayScreenTransition());
        }

        SetStatusText("BOOT SEQUENCE");
        SetFooterText("INITIALIZING...");

        yield return StartCoroutine(TypeText(mainText, splashText));

        if (splashHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(splashHoldDuration);
        }

        for (int i = 0; i < loadingMessages.Length; i++)
        {
            string message = loadingMessages[i];
            SetMainText(message);
            SetFooterText(message);
            yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(loadingStepDurationRange.x, loadingStepDurationRange.y));
        }

        SetMainText(bootCompleteText);
        SetStatusText("LINK ESTABLISHED");
        SetFooterText("FINALIZING...");

        if (bootCompleteHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(bootCompleteHoldDuration);
        }

        powerState = PowerState.Ready;
        lastBootTimestamp = GetWorldTime01();
        hasBootTimestamp = true;
        operatorLinkState = terminalControl != null && terminalControl.IsFocused ? "OPERATOR LINKED" : idleOperatorLabel;
        AddLog("BOOT COMPLETE");
        FadeAmbientLoop(true, ambientFadeInDuration);
        RenderReadyView();
        ShowEventMessage(bootCompleteText, 1.1f);
        StartBlinkEffect();
        powerRoutine = null;
    }

    private IEnumerator PowerOffRoutine()
    {
        powerState = PowerState.ShuttingDown;
        operatorLinkState = "OFFLINE";

        SetPoweredObjects(true);
        ApplyMonitorMaterialState(true);

        if (screenCanvas != null)
        {
            screenCanvas.gameObject.SetActive(true);
        }

        if (terminalCanvasGroup != null)
        {
            terminalCanvasGroup.alpha = 1f;
        }

        SetMainText("POWERING DOWN...\n\nSAVING SESSION\nDISCHARGING CRT");
        SetStatusText("SHUTDOWN SEQUENCE");
        SetFooterText("PLEASE WAIT");
        AddLog("POWER DOWN REQUEST ACCEPTED");

        PlayShutdownSound();
        PlayStaticBurst();
        FadeAmbientLoop(false, ambientFadeOutDuration);
        StartBlinkEffect();

        if (shutdownHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(shutdownHoldDuration);
        }

        if (playTransitionOnShutdown)
        {
            yield return StartCoroutine(PlayScreenTransition());
        }

        lastShutdownTimestamp = GetWorldTime01();
        hasShutdownTimestamp = true;
        AddLog("DISPLAY OFFLINE");
        ApplyImmediateOffState();
        powerRoutine = null;
    }

    private void HandleFocusEntered(TerminalControl _)
    {
        operatorLinkState = "OPERATOR LINKED";
        AddLog("OPERATOR SESSION START");

        if (autoPowerOnWhenFocused)
        {
            if (HasFusePower)
            {
                TurnOn();
            }
            return;
        }

        if (powerState == PowerState.Ready)
        {
            RenderCurrentView();
            ShowEventMessage("OPERATOR LINKED", 0.9f);
        }
    }

    private void HandleFocusExited(TerminalControl _)
    {
        if (terminalControl != null)
        {
            terminalControl.BlockInteractExitInput = false;
        }

        operatorLinkState = powerState == PowerState.Off ? "OFFLINE" : idleOperatorLabel;
        AddLog("OPERATOR SESSION CLOSED");

        if (powerOffOnNextFocusExit)
        {
            powerOffOnNextFocusExit = false;
            TurnOff();
            return;
        }

        if (autoPowerOffWhenFocusEnds)
        {
            TurnOff();
            return;
        }

        if (powerState == PowerState.Ready)
        {
            RenderCurrentView();
        }
    }

    private void HandleSignalHistoryChanged()
    {
        if (powerState == PowerState.Ready && currentView == ReadyView.SignalHistory)
        {
            RenderCurrentView();
        }
    }
}
