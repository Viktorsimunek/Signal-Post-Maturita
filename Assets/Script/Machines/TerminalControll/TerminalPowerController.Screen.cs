using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public partial class TerminalPowerController
{
    private void PrepareBootView()
    {
        if (screenCanvas != null)
        {
            screenCanvas.gameObject.SetActive(true);
        }

        if (screenTransitionBar != null)
        {
            screenTransitionBar.SnapTo01(0f);
            screenTransitionBar.gameObject.SetActive(false);
        }

        if (terminalCanvasGroup != null)
        {
            terminalCanvasGroup.alpha = 1f;
        }

        SetMainText(string.Empty);
        SetStatusText(string.Empty);
        SetEventText(string.Empty);
    }

    private void RefreshDynamicUi()
    {
        if (powerState == PowerState.Ready)
        {
            RenderCurrentView();
            return;
        }

        if (powerState == PowerState.Booting)
        {
            SetStatusText("BOOT SEQUENCE");
            RefreshEventText();
        }
    }

    private void RenderReadyView()
    {
        currentView = ReadyView.Menu;
        RenderCurrentView();
    }

    private IEnumerator SceneWelcomeHandoffRoutine(string welcomeText, float holdDuration, float fadeDuration)
    {
        powerState = PowerState.Ready;
        currentView = ReadyView.Menu;
        selectedMenuIndex = 0;
        powerConfirmIndex = 1;
        operatorLinkState = terminalControl != null && terminalControl.IsFocused ? "OPERATOR LINKED" : idleOperatorLabel;
        footerText = menuFooterBlock;
        transientEventText = string.Empty;

        SetPoweredObjects(true);
        ApplyMonitorMaterialState(true);
        PrepareBootView();
        ForceMonitorRender(Color.black);

        if (terminalControl != null)
        {
            terminalControl.BlockExitInput = true;
        }

        if (terminalCanvasGroup != null)
        {
            terminalCanvasGroup.alpha = 1f;
        }

        SetMainText(string.IsNullOrWhiteSpace(welcomeText) ? "WELCOME OPERATOR!" : welcomeText.Trim());
        SetStatusText("OPERATOR PROFILE ACCEPTED");
        SetFooterText("LINKING SESSION...");
        SetEventText(string.Empty);
        AddLog("OPERATOR PROFILE ACCEPTED");
        ForceMonitorRender(Color.black);

        if (holdDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(holdDuration);
        }

        if (terminalCanvasGroup != null)
        {
            yield return StartCoroutine(FadeCanvasGroupAlpha(1f, 0f, fadeDuration));
        }

        lastBootTimestamp = GetWorldTime01();
        hasBootTimestamp = true;
        RenderReadyView();
        ForceMonitorRender(Color.black);

        if (terminalCanvasGroup != null)
        {
            yield return StartCoroutine(FadeCanvasGroupAlpha(0f, 1f, fadeDuration));
        }

        if (terminalControl != null)
        {
            terminalControl.BlockExitInput = false;
            terminalControl.BlockInteractExitInput = false;
        }

        powerRoutine = null;
    }

    private IEnumerator SceneScreenshotHandoffRoutine(Texture handoffTexture, string welcomeText, float holdDuration, float fadeDuration)
    {
        powerState = PowerState.Ready;
        currentView = ReadyView.Menu;
        selectedMenuIndex = 0;
        powerConfirmIndex = 1;
        operatorLinkState = terminalControl != null && terminalControl.IsFocused ? "OPERATOR LINKED" : idleOperatorLabel;
        footerText = menuFooterBlock;
        transientEventText = string.Empty;

        SetPoweredObjects(true);
        ApplyMonitorMaterialState(true);
        SetMonitorTexture(handoffTexture != null ? handoffTexture : poweredBaseTexture);
        SetMonitorTint(poweredBaseColor, poweredEmissionColor);

        if (terminalControl != null)
        {
            terminalControl.BlockExitInput = true;
        }

        if (holdDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(holdDuration);
        }

        yield return StartCoroutine(FadeMonitorTint(poweredBaseColor, poweredEmissionColor, Color.black, Color.black, fadeDuration));

        PrepareBootView();
        SetMainText(string.IsNullOrWhiteSpace(welcomeText) ? "WELCOME OPERATOR!" : welcomeText.Trim());
        SetStatusText("OPERATOR PROFILE ACCEPTED");
        SetFooterText("LINKING SESSION...");
        SetEventText(string.Empty);
        AddLog("OPERATOR PROFILE ACCEPTED");

        lastBootTimestamp = GetWorldTime01();
        hasBootTimestamp = true;
        RenderReadyView();
        ForceMonitorRender(Color.black);
        SetMonitorTexture(poweredBaseTexture);

        yield return StartCoroutine(FadeMonitorTint(Color.black, Color.black, poweredBaseColor, poweredEmissionColor, fadeDuration));

        if (terminalControl != null)
        {
            terminalControl.BlockExitInput = false;
            terminalControl.BlockInteractExitInput = false;
        }

        powerRoutine = null;
    }

    private string BuildReadyMenuText()
    {
        string result = menuTitle + "\n\n";
        int itemCount = GetMenuItemCount();

        for (int i = 0; i < itemCount; i++)
        {
            string prefix = i == selectedMenuIndex ? "> " : "  ";
            result += prefix + readyMenuItems[i] + "\n";
        }

        return result.TrimEnd();
    }

    private string BuildSidebarStatus()
    {
        string stateLabel;
        switch (powerState)
        {
            case PowerState.Booting:
                stateLabel = "BOOT";
                break;
            case PowerState.ShuttingDown:
                stateLabel = "DOWN";
                break;
            case PowerState.Ready:
                stateLabel = "READY";
                break;
            default:
                stateLabel = "OFF";
                break;
        }

        return "SIGNAL STATUS: " + stateLabel + "\n" +
               relayNodeId + "\n" +
               "TIME " + GetWorldTimeString();
    }

    private string BuildStatusText()
    {
        string text =
            "SYSTEM STATUS\n\n" +
            "POWER: " + powerState.ToString().ToUpperInvariant() + "\n" +
            "RELAY: " + relayNodeId + "\n" +
            channelLabel + "\n" +
            "OPERATOR: " + operatorLinkState + "\n" +
            "LAST BOOT: " + FormatTimestamp(hasBootTimestamp, lastBootTimestamp) + "\n" +
            "LAST SHUTDOWN: " + FormatTimestamp(hasShutdownTimestamp, lastShutdownTimestamp);

        if (!string.IsNullOrWhiteSpace(statusBlock))
        {
            text += "\n\n" + statusBlock.Trim();
        }

        return text;
    }

    private string BuildLogsText()
    {
        string text = "RECENT LOGS\n\n";

        if (runtimeLogs.Count == 0)
        {
            text += "NO RUNTIME ENTRIES";
        }
        else
        {
            for (int i = runtimeLogs.Count - 1; i >= 0; i--)
            {
                text += runtimeLogs[i] + "\n";
            }

            text = text.TrimEnd();
        }

        if (!string.IsNullOrWhiteSpace(logsBlock))
        {
            text += "\n\nARCHIVE SNAPSHOT\n\n" + logsBlock.Trim();
        }

        return text;
    }

    private string BuildSignalHistoryText()
    {
        IReadOnlyList<SignalHistory.Entry> entries = SignalHistory.Entries;
        string text = "SIGNAL HISTORY\n\n";

        if (entries.Count == 0)
        {
            return text + "NO SIGNALS RECORDED";
        }

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            SignalHistory.Entry entry = entries[i];
            text += "[" + entry.Timestamp + "] " + entry.Direction + "\n";
            text += entry.Text + "\n\n";
        }

        return text.TrimEnd();
    }

    private string BuildPowerConfirmText()
    {
        return
            "POWER DOWN CONFIRM\n\n" +
            "WARNING:\n" +
            "TERMINAL WILL LOSE CURRENT TRANSMIT DATA.\n" +
            "UNSENT MESSAGE BUFFER WILL BE CLEARED.\n\n" +
            "POWER DOWN TERMINAL?\n\n" +
            BuildConfirmOption("YES", powerConfirmIndex == 0) + "\n" +
            BuildConfirmOption("NO", powerConfirmIndex == 1);
    }

    private string BuildConfirmOption(string label, bool selected)
    {
        return selected ? "> " + label : "  " + label;
    }

    private void RenderCurrentView()
    {
        RefreshTerminalExitInput();

        switch (currentView)
        {
            case ReadyView.Status:
                SetMainText(BuildStatusText());
                SetStatusText("STATUS VIEW");
                SetFooterText(pageFooterBlock);
                break;
            case ReadyView.Logs:
                SetMainText(BuildLogsText());
                SetStatusText("ARCHIVE VIEW");
                SetFooterText(pageFooterBlock);
                break;
            case ReadyView.SignalHistory:
                SetMainText(BuildSignalHistoryText());
                SetStatusText("SIGNAL HISTORY");
                SetFooterText(pageFooterBlock);
                break;
            case ReadyView.PersonDatabase:
                SetMainText(BuildPersonDatabaseText());
                SetStatusText(personLookupResultPageOpen ? "PERSON RECORD" : personLookupInProgress ? "DATABASE LINK" : "PERSON DATABASE");
                SetFooterText(personLookupResultPageOpen
                    ? "ENTER/BACKSPACE SEARCH  E/ESC EXIT"
                    : GameProgress.IsEasyDifficulty
                        ? "TYPE CODE  ENTER SEARCH  BACKSPACE MENU"
                        : "ARROWS SELECT  ENTER PRESS  BACKSPACE MENU");
                break;
            case ReadyView.Transmit:
                SetMainText(BuildTransmitText());
                SetStatusText(BuildTransmitStatus());
                SetFooterText(GetTransmitFooter());
                break;
            case ReadyView.PowerConfirm:
                SetMainText(BuildPowerConfirmText());
                SetStatusText("POWER CONFIRM");
                SetFooterText("A/D SELECT  ENTER CONFIRM  BACKSPACE BACK");
                break;
            default:
                SetMainText(BuildReadyMenuText());
                SetStatusText(BuildSidebarStatus());
                SetFooterText(menuFooterBlock);
                break;
        }
    }

    private void RefreshTerminalExitInput()
    {
        if (terminalControl == null)
        {
            return;
        }

        terminalControl.BlockInteractExitInput = ShouldBlockInteractExitInput();
    }

    private bool ShouldBlockInteractExitInput()
    {
        if (!GameProgress.IsEasyDifficulty)
        {
            return false;
        }

        if (currentView == ReadyView.Transmit)
        {
            return transmitPage == TransmitPage.Message || transmitPage == TransmitPage.Code;
        }

        if (currentView == ReadyView.PersonDatabase)
        {
            return !personLookupInProgress && !personLookupResultPageOpen;
        }

        return false;
    }

    private IEnumerator TypeText(TMP_Text target, string content)
    {
        if (target == null)
        {
            yield break;
        }

        target.text = string.Empty;

        for (int i = 0; i < content.Length; i++)
        {
            target.text = content.Substring(0, i + 1);
            yield return new WaitForSecondsRealtime(typeDelay);
        }

        target.text = content;
    }

    private void SetPoweredObjects(bool enabled)
    {
        if (terminalPowerLight != null)
        {
            terminalPowerLight.enabled = enabled;
        }

        if (screenCanvas != null)
        {
            screenCanvas.gameObject.SetActive(enabled);
        }

        if (monitorOutput != null)
        {
            monitorOutput.enabled = enabled;
        }
        else if (sourceCamera != null)
        {
            sourceCamera.enabled = enabled;
        }

        if (activateWhilePowered != null)
        {
            for (int i = 0; i < activateWhilePowered.Length; i++)
            {
                GameObject target = activateWhilePowered[i];
                if (target != null)
                {
                    bool desiredState = enabled && i < cachedActiveStates.Length && cachedActiveStates[i];
                    target.SetActive(desiredState);
                }
            }
        }

        if (enableWhilePowered != null)
        {
            for (int i = 0; i < enableWhilePowered.Length; i++)
            {
                Behaviour target = enableWhilePowered[i];
                if (target != null)
                {
                    bool desiredState = enabled && i < cachedBehaviourStates.Length && cachedBehaviourStates[i];
                    target.enabled = desiredState;
                }
            }
        }
    }

    private void ApplyMonitorMaterialState(bool powered)
    {
        if (monitorRenderer == null)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        int safeIndex = GetSafeMaterialIndex();
        monitorRenderer.GetPropertyBlock(propertyBlock, safeIndex);

        if (HasMaterialProperty(BaseMapId))
        {
            propertyBlock.SetTexture(BaseMapId, powered ? poweredBaseTexture : offBaseTexture);
        }

        if (alsoWriteMainTex && HasMaterialProperty(MainTexId))
        {
            propertyBlock.SetTexture(MainTexId, powered ? poweredBaseTexture : offBaseTexture);
        }

        if (HasMaterialProperty(EmissionMapId))
        {
            propertyBlock.SetTexture(EmissionMapId, powered ? poweredEmissionTexture : offEmissionTexture);
        }

        if (HasMaterialProperty(BaseColorId))
        {
            propertyBlock.SetColor(BaseColorId, powered ? poweredBaseColor : offBaseColor);
        }

        if (HasMaterialProperty(LegacyColorId))
        {
            propertyBlock.SetColor(LegacyColorId, powered ? poweredBaseColor : offBaseColor);
        }

        if (HasMaterialProperty(EmissionColorId))
        {
            propertyBlock.SetColor(EmissionColorId, powered ? poweredEmissionColor : offEmissionColor);
        }

        monitorRenderer.SetPropertyBlock(propertyBlock, safeIndex);
    }

    private void SetMonitorTexture(Texture texture)
    {
        if (monitorRenderer == null || texture == null)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        int safeIndex = GetSafeMaterialIndex();
        monitorRenderer.GetPropertyBlock(propertyBlock, safeIndex);
        propertyBlock.SetTexture(BaseMapId, texture);
        if (alsoWriteMainTex)
        {
            propertyBlock.SetTexture(MainTexId, texture);
        }
        propertyBlock.SetTexture(EmissionMapId, texture);
        monitorRenderer.SetPropertyBlock(propertyBlock, safeIndex);
    }

    private void SetMonitorTint(Color baseColor, Color emissionColor)
    {
        if (monitorRenderer == null)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        int safeIndex = GetSafeMaterialIndex();
        monitorRenderer.GetPropertyBlock(propertyBlock, safeIndex);
        propertyBlock.SetColor(BaseColorId, baseColor);
        propertyBlock.SetColor(LegacyColorId, baseColor);
        propertyBlock.SetColor(EmissionColorId, emissionColor);
        monitorRenderer.SetPropertyBlock(propertyBlock, safeIndex);
    }

    private IEnumerator FadeMonitorTint(
        Color fromBaseColor,
        Color fromEmissionColor,
        Color toBaseColor,
        Color toEmissionColor,
        float duration)
    {
        if (duration <= 0.0001f)
        {
            SetMonitorTint(toBaseColor, toEmissionColor);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            SetMonitorTint(
                Color.Lerp(fromBaseColor, toBaseColor, t),
                Color.Lerp(fromEmissionColor, toEmissionColor, t));
            yield return null;
        }

        SetMonitorTint(toBaseColor, toEmissionColor);
    }

    private void ForceMonitorRender(Color clearColor)
    {
        if (monitorOutput == null)
        {
            return;
        }

        monitorOutput.ClearTarget(clearColor);
        monitorOutput.RenderNow();
    }

    private bool HasMaterialProperty(int propertyId)
    {
        Material sharedMaterial = GetSharedMaterial();
        return sharedMaterial != null && sharedMaterial.HasProperty(propertyId);
    }

    private Material GetSharedMaterial()
    {
        if (monitorRenderer == null || monitorRenderer.sharedMaterials == null || monitorRenderer.sharedMaterials.Length == 0)
        {
            return null;
        }

        return monitorRenderer.sharedMaterials[GetSafeMaterialIndex()];
    }

    private int GetSafeMaterialIndex()
    {
        if (monitorRenderer == null || monitorRenderer.sharedMaterials == null || monitorRenderer.sharedMaterials.Length == 0)
        {
            return 0;
        }

        return Mathf.Clamp(materialIndex, 0, monitorRenderer.sharedMaterials.Length - 1);
    }

    private void ClearTexts()
    {
        SetMainText(string.Empty);
        SetStatusText(string.Empty);
        SetEventText(string.Empty);
    }

    private void SetMainText(string content)
    {
        if (mainText != null)
        {
            mainText.text = content;
        }
    }

    private void SetStatusText(string content)
    {
        if (statusText != null)
        {
            statusText.text = content;
        }
    }

    private void SetEventText(string content)
    {
        if (eventText != null)
        {
            eventText.text = content;
        }
    }

    private void SetFooterText(string content)
    {
        footerText = content ?? string.Empty;
        RefreshEventText();
    }

    private void ShowEventMessage(string message, float clearAfter)
    {
        transientEventText = message ?? string.Empty;
        RefreshEventText();

        if (eventRoutine != null)
        {
            StopCoroutine(eventRoutine);
            eventRoutine = null;
        }

        if (clearAfter > 0f)
        {
            eventRoutine = StartCoroutine(ClearEventMessageAfterDelay(clearAfter));
        }
    }

    private IEnumerator ClearEventMessageAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        transientEventText = string.Empty;
        RefreshEventText();
        eventRoutine = null;
    }

    private void RefreshEventText()
    {
        SetEventText(string.IsNullOrWhiteSpace(transientEventText) ? footerText : transientEventText);
    }
}
