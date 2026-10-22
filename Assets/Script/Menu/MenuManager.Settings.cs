using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public partial class MenuManager
{
    private string BuildSettingsMenuText()
    {
        string[] labels =
        {
            "MASTER VOLUME: " + FormatSlider(GameSettings.MasterVolume),
            "MUSIC VOLUME: " + FormatSlider(GameSettings.MusicVolume),
            "SFX VOLUME: " + FormatSlider(GameSettings.SfxVolume),
            "DISPLAY MODE: " + GameSettings.GetDisplayModeLabel(),
            "RESOLUTION: " + FormatResolution(),
            "RENDER SCALE: " + FormatPercent(GameSettings.RenderScale),
            "BRIGHTNESS: " + FormatPercent(GameSettings.Brightness),
            "INPUT MODE: " + GameSettings.GetInputModeLabel(),
            "INPUT BINDINGS",
            "INVERT Y: " + (GameSettings.InvertY ? "ON" : "OFF"),
            "V SYNC: " + (GameSettings.VSyncEnabled ? "ON" : "OFF"),
            "FPS LIMIT: " + FormatFpsLimit(),
            "FPS DISPLAY: " + (GameSettings.ShowFpsDisplay ? "ON" : "OFF"),
            "RESET DEFAULTS",
            "BACK"
        };

        return BuildMenuText("SETTINGS", labels, selectedIndex);
    }

    private string BuildInputBindingsMenuText()
    {
        if (waitingForBinding)
        {
            return "INPUT BINDINGS\n\n" +
                   GetInputActionLabel(pendingBindingAction) + ": ...\n\n" +
                   "PRESS A KEY OR CONTROLLER BUTTON\nESC / B CANCEL";
        }

        string[] labels =
        {
            "INTERACTION: " + GameSettings.GetBindingDisplay(GameSettings.RebindableAction.Interact),
            "FLASHLIGHT: " + GameSettings.GetBindingDisplay(GameSettings.RebindableAction.Flashlight),
            "CROUCH: " + GameSettings.GetBindingDisplay(GameSettings.RebindableAction.Crouch),
            "SPRINT: " + GameSettings.GetBindingDisplay(GameSettings.RebindableAction.Sprint),
            "RESET DEFAULTS",
            "BACK"
        };

        return BuildMenuText("INPUT BINDINGS", labels, selectedIndex);
    }

    private string[] BuildMainMenuLabels()
    {
        string[] labels = new string[mainMenuItems.Length];
        for (int i = 0; i < mainMenuItems.Length; i++)
        {
            labels[i] = mainMenuItems[i] == "CONTINUE" && !GameProgress.HasSave
                ? "CONTINUE: NO PROFILE"
                : mainMenuItems[i];
        }

        return labels;
    }

    private void HandleInputBindingsInput()
    {
        if (waitingForBinding)
        {
            UpdateBindingCapture();
            return;
        }

        int verticalStep = GetVerticalStep();
        if (verticalStep != 0)
        {
            selectedIndex = (selectedIndex + verticalStep + inputBindingActions.Length + 2) % (inputBindingActions.Length + 2);
            RegisterUserInput();
            PlaySfx(cursorBeep);
            RenderCurrentMenuImmediate();
            StartBlinkEffect();
            return;
        }

        if (WasBackPressed())
        {
            RegisterUserInput();
            RequestOpenSettingsMenu();
            return;
        }

        if (!WasSubmitPressed())
        {
            return;
        }

        RegisterUserInput();
        PlaySfx(confirmBeep);

        if (selectedIndex < inputBindingActions.Length)
        {
            BeginBindingCapture(inputBindingActions[selectedIndex]);
            return;
        }

        if (selectedIndex == inputBindingActions.Length)
        {
            GameSettings.ResetInputBindings();
            GameSettings.Save();
            ShowEventMessage("INPUT DEFAULTS RESTORED", 1.2f);
            RenderCurrentMenuImmediate();
            StartBlinkEffect();
            return;
        }

        RequestOpenSettingsMenu();
    }

    private void BeginBindingCapture(GameSettings.RebindableAction action)
    {
        waitingForBinding = true;
        pendingBindingAction = action;
        bindingCaptureTime = Time.unscaledTime + 0.12f;
        ShowEventMessage("WAITING FOR INPUT", 0f);
        RenderCurrentMenuImmediate();
    }

    private void UpdateBindingCapture()
    {
        if (Time.unscaledTime < bindingCaptureTime)
        {
            return;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            EndBindingCapture("REBIND CANCELLED");
            return;
        }

        if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
        {
            EndBindingCapture("REBIND CANCELLED");
            return;
        }

        if (TryGetPressedKeyboardKey(out Key key))
        {
            GameSettings.SetKeyboardBinding(pendingBindingAction, key);
            GameSettings.Save();
            InputDeviceTracker.MarkKeyboardMouse();
            EndBindingCapture(GetInputActionLabel(pendingBindingAction) + " SET");
            return;
        }

        if (TryGetPressedGamepadButton(out UnityEngine.InputSystem.LowLevel.GamepadButton button))
        {
            GameSettings.SetGamepadBinding(pendingBindingAction, button);
            GameSettings.Save();
            InputDeviceTracker.MarkGamepad();
            EndBindingCapture(GetInputActionLabel(pendingBindingAction) + " SET");
        }
    }

    private void EndBindingCapture(string message)
    {
        waitingForBinding = false;
        ShowEventMessage(message, 1.2f);
        RenderCurrentMenuImmediate();
        StartBlinkEffect();
    }

    private static bool TryGetPressedKeyboardKey(out Key key)
    {
        key = Key.None;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }

        foreach (Key candidate in Enum.GetValues(typeof(Key)))
        {
            if (candidate == Key.None || candidate == Key.Escape)
            {
                continue;
            }

            if (keyboard[candidate] != null && keyboard[candidate].wasPressedThisFrame)
            {
                key = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetPressedGamepadButton(out UnityEngine.InputSystem.LowLevel.GamepadButton button)
    {
        button = UnityEngine.InputSystem.LowLevel.GamepadButton.South;
        Gamepad gamepad = Gamepad.current;
        if (gamepad == null)
        {
            return false;
        }

        foreach (UnityEngine.InputSystem.LowLevel.GamepadButton candidate in Enum.GetValues(typeof(UnityEngine.InputSystem.LowLevel.GamepadButton)))
        {
            if (candidate == UnityEngine.InputSystem.LowLevel.GamepadButton.East)
            {
                continue;
            }

            if (gamepad[candidate] != null && gamepad[candidate].wasPressedThisFrame)
            {
                button = candidate;
                return true;
            }
        }

        return false;
    }

    private static string GetInputActionLabel(GameSettings.RebindableAction action)
    {
        return action switch
        {
            GameSettings.RebindableAction.Flashlight => "FLASHLIGHT",
            GameSettings.RebindableAction.Crouch => "CROUCH",
            GameSettings.RebindableAction.Sprint => "SPRINT",
            _ => "INTERACTION"
        };
    }

    private void AdjustCurrentSetting(int direction)
    {
        if (currentScreen != TerminalScreen.Settings)
        {
            return;
        }

        switch (selectedIndex)
        {
            case 0:
                GameSettings.SetMasterVolume(GameSettings.MasterVolume + direction * volumeStep);
                CommitSettings("MASTER " + FormatPercent(GameSettings.MasterVolume));
                break;
            case 1:
                GameSettings.SetMusicVolume(GameSettings.MusicVolume + direction * volumeStep);
                CommitSettings("MUSIC " + FormatPercent(GameSettings.MusicVolume));
                break;
            case 2:
                GameSettings.SetSfxVolume(GameSettings.SfxVolume + direction * volumeStep);
                CommitSettings("SFX " + FormatPercent(GameSettings.SfxVolume));
                break;
            case 3:
                if (direction != 0)
                {
                    GameSettings.CycleDisplayMode(direction);
                    CommitSettings("DISPLAY " + GameSettings.GetDisplayModeLabel());
                }
                break;
            case 4:
                CycleResolution(direction);
                CommitSettings("RESOLUTION " + FormatResolution());
                break;
            case 5:
                GameSettings.SetRenderScale(GameSettings.RenderScale + direction * renderScaleStep);
                CommitSettings("RENDER SCALE " + FormatPercent(GameSettings.RenderScale));
                break;
            case 6:
                GameSettings.SetBrightness(GameSettings.Brightness + direction * brightnessStep);
                CommitSettings("BRIGHTNESS " + FormatPercent(GameSettings.Brightness));
                break;
            case 7:
                if (direction != 0)
                {
                    GameSettings.CycleInputMode(direction);
                    CommitSettings("INPUT " + GameSettings.GetInputModeLabel());
                }
                break;
            case 8:
                RequestOpenInputBindingsMenu();
                break;
            case 9:
                if (direction != 0)
                {
                    GameSettings.SetInvertY(!GameSettings.InvertY);
                    CommitSettings(GameSettings.InvertY ? "INVERT Y ON" : "INVERT Y OFF");
                }
                break;
            case 10:
                if (direction != 0)
                {
                    GameSettings.SetVSync(!GameSettings.VSyncEnabled);
                    CommitSettings(GameSettings.VSyncEnabled ? "V SYNC ON" : "V SYNC OFF");
                }
                break;
            case 11:
                CycleFpsLimit(direction);
                CommitSettings("FPS LIMIT " + FormatFpsLimit());
                break;
            case 12:
                if (direction != 0)
                {
                    GameSettings.SetShowFpsDisplay(!GameSettings.ShowFpsDisplay);
                    CommitSettings(GameSettings.ShowFpsDisplay ? "FPS DISPLAY ON" : "FPS DISPLAY OFF");
                }
                break;
            case 13:
                GameSettings.ResetToDefaults();
                CommitSettings("SETTINGS RESET");
                break;
            case 14:
                RequestOpenMainMenu();
                break;
        }
    }

    private void CommitSettings(string feedback)
    {
        GameSettings.Save();
        GameSettings.ApplyRuntime();
        ApplyMixerVolumes();
        ApplyMenuMusicVolume();
        ApplyBrightness();
        RenderCurrentMenu();
        ShowEventMessage(feedback, 1.3f);
        StartBlinkEffect();
    }

    private void ApplyMixerVolumes()
    {
        if (targetMixer == null)
        {
            return;
        }

        targetMixer.SetFloat(masterVolumeParam, LinearToDecibels(GameSettings.MasterVolume));
        targetMixer.SetFloat(musicVolumeParam, LinearToDecibels(GameSettings.MusicVolume));
        targetMixer.SetFloat(sfxVolumeParam, LinearToDecibels(GameSettings.SfxVolume));
    }

    private static string FormatPercent(float value)
    {
        return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
    }

    private static string FormatSlider(float value)
    {
        const int steps = 8;
        int index = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(value) * (steps - 1)), 0, steps - 1);
        char[] chars = new char[steps];

        for (int i = 0; i < steps; i++)
        {
            chars[i] = i == index ? 'O' : '-';
        }

        return "[" + new string(chars) + "]";
    }

    private string FormatResolution()
    {
        if (GameSettings.CurrentDisplayMode == GameSettings.DisplayMode.BorderlessFullscreen)
        {
            Resolution desktop = Screen.currentResolution;
            return desktop.width + "x" + desktop.height + " DESKTOP";
        }

        return GameSettings.ResolutionWidth + "x" + GameSettings.ResolutionHeight;
    }

    private string FormatFpsLimit()
    {
        return GameSettings.FpsLimit == 0 ? "UNLIMITED" : GameSettings.FpsLimit.ToString();
    }

    private static string BuildMenuText(string title, string[] items, int activeIndex)
    {
        string menu = title + "\n\n";
        for (int i = 0; i < items.Length; i++)
        {
            string selector = i == activeIndex ? "> " : "  ";
            menu += selector + items[i] + "\n";
        }

        return menu.TrimEnd();
    }

    private void CacheAvailableResolutions()
    {
        Resolution[] detected = Screen.resolutions;
        if (detected == null || detected.Length == 0)
        {
            availableResolutions = new[] { Screen.currentResolution };
            return;
        }

        System.Collections.Generic.List<Resolution> unique = new System.Collections.Generic.List<Resolution>();
        for (int i = 0; i < detected.Length; i++)
        {
            Resolution candidate = detected[i];
            bool alreadyAdded = false;
            for (int j = 0; j < unique.Count; j++)
            {
                if (unique[j].width == candidate.width && unique[j].height == candidate.height)
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
            {
                unique.Add(candidate);
            }
        }

        availableResolutions = unique.ToArray();
    }

    private void CycleResolution(int direction)
    {
        if (direction == 0 || availableResolutions == null || availableResolutions.Length == 0)
        {
            return;
        }

        int currentIndex = 0;
        int bestDistance = int.MaxValue;
        for (int i = 0; i < availableResolutions.Length; i++)
        {
            int distance = Mathf.Abs(availableResolutions[i].width - GameSettings.ResolutionWidth) +
                           Mathf.Abs(availableResolutions[i].height - GameSettings.ResolutionHeight);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                currentIndex = i;
            }
        }

        int nextIndex = (currentIndex + direction + availableResolutions.Length) % availableResolutions.Length;
        Resolution selectedResolution = availableResolutions[nextIndex];
        GameSettings.SetResolution(selectedResolution.width, selectedResolution.height);
    }

    private void CycleFpsLimit(int direction)
    {
        if (direction == 0 || fpsLimitOptions == null || fpsLimitOptions.Length == 0)
        {
            return;
        }

        int currentIndex = 0;
        int bestDistance = int.MaxValue;
        for (int i = 0; i < fpsLimitOptions.Length; i++)
        {
            int distance = Mathf.Abs(fpsLimitOptions[i] - GameSettings.FpsLimit);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                currentIndex = i;
            }
        }

        int nextIndex = (currentIndex + direction + fpsLimitOptions.Length) % fpsLimitOptions.Length;
        GameSettings.SetFpsLimit(fpsLimitOptions[nextIndex]);
    }

    private void SetupBrightnessControl()
    {
        runtimeVolume = FindObjectOfType<Volume>();
        if (runtimeVolume == null)
        {
            return;
        }

        VolumeProfile sourceProfile = runtimeVolume.sharedProfile != null ? runtimeVolume.sharedProfile : runtimeVolume.profile;
        if (sourceProfile == null)
        {
            return;
        }

        runtimeVolumeProfile = Instantiate(sourceProfile);
        runtimeVolume.profile = runtimeVolumeProfile;

        if (!runtimeVolumeProfile.TryGet(out colorAdjustments))
        {
            colorAdjustments = runtimeVolumeProfile.Add<ColorAdjustments>(true);
        }

        colorAdjustments.active = true;
        colorAdjustments.postExposure.overrideState = true;
    }

    private void ApplyBrightness()
    {
        if (colorAdjustments == null)
        {
            return;
        }

        colorAdjustments.postExposure.value = Mathf.Lerp(-1f, 1f, GameSettings.Brightness);
    }

    private static float LinearToDecibels(float value)
    {
        float clamped = Mathf.Clamp(value, 0.0001f, 1f);
        return Mathf.Log10(clamped) * 20f;
    }
}
