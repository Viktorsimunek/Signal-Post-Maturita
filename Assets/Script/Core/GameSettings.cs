using UnityEngine;
using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class GameSettings
{
    private const string MasterVolumeKey = "settings_master_volume";
    private const string MusicVolumeKey = "settings_music_volume";
    private const string SfxVolumeKey = "settings_sfx_volume";
    private const string VSyncKey = "settings_vsync";
    private const string FpsLimitKey = "settings_fps_limit";
    private const string ShowFpsDisplayKey = "settings_show_fps_display";
    private const string DisplayModeKey = "settings_display_mode";
    private const string ResolutionWidthKey = "settings_resolution_width";
    private const string ResolutionHeightKey = "settings_resolution_height";
    private const string RenderScaleKey = "settings_render_scale";
    private const string BrightnessKey = "settings_brightness";
    private const string MouseSensitivityKey = "settings_mouse_sensitivity";
    private const string GamepadSensitivityKey = "settings_gamepad_sensitivity";
    private const string InvertYKey = "settings_invert_y";
    private const string InputModeKey = "settings_input_mode";
    private const string KeyboardBindingPrefix = "settings_keyboard_binding_";
    private const string GamepadBindingPrefix = "settings_gamepad_binding_";

    private const float DefaultMasterVolume = 1f;
    private const float DefaultMusicVolume = 0.8f;
    private const float DefaultSfxVolume = 0.8f;
    private const int DefaultVSync = 1;
    private const int DefaultFpsLimit = 60;
    private const int DefaultShowFpsDisplay = 0;
    private const int DefaultDisplayMode = (int)DisplayMode.BorderlessFullscreen;
    private const float DefaultRenderScale = 1f;
    private const float DefaultBrightness = 0.5f;
    private const float DefaultMouseSensitivity = 0.6f;
    private const float DefaultGamepadSensitivity = 0.6f;
    private const int DefaultInvertY = 0;
    private const int DefaultInputMode = (int)InputMode.Auto;
    private const float MinMouseSensitivity = 0.15f;
    private const float MaxMouseSensitivity = 2f;
    private const float MinGamepadSensitivity = 0.15f;
    private const float MaxGamepadSensitivity = 2f;
    private const float MinRenderScale = 0.5f;
    private const float MaxRenderScale = 1f;

    public enum DisplayMode
    {
        Windowed = 0,
        BorderlessFullscreen = 1,
        ExclusiveFullscreen = 2
    }

    public enum InputMode
    {
        Auto = 0,
        KeyboardMouse = 1,
        Gamepad = 2
    }

    public enum RebindableAction
    {
        Interact = 0,
        Flashlight = 1,
        Crouch = 2,
        Sprint = 3
    }

    public static float MasterVolume { get; private set; }
    public static float MusicVolume { get; private set; }
    public static float SfxVolume { get; private set; }
    public static bool VSyncEnabled { get; private set; }
    public static int FpsLimit { get; private set; }
    public static bool ShowFpsDisplay { get; private set; }
    public static DisplayMode CurrentDisplayMode { get; private set; }
    public static int ResolutionWidth { get; private set; }
    public static int ResolutionHeight { get; private set; }
    public static float RenderScale { get; private set; }
    public static float Brightness { get; private set; }
    public static float MouseSensitivity { get; private set; }
    public static float GamepadSensitivity { get; private set; }
    public static bool InvertY { get; private set; }
    public static InputMode CurrentInputMode { get; private set; }
    public static event Action SettingsChanged;

    private static Key interactKey = Key.E;
    private static Key flashlightKey = Key.F;
    private static Key crouchKey = Key.LeftCtrl;
    private static Key sprintKey = Key.LeftShift;
    private static GamepadButton interactButton = GamepadButton.North;
    private static GamepadButton flashlightButton = GamepadButton.RightShoulder;
    private static GamepadButton crouchButton = GamepadButton.East;
    private static GamepadButton sprintButton = GamepadButton.LeftStick;
    private static bool isLoaded;

    public static void EnsureLoaded()
    {
        if (!isLoaded)
        {
            Load();
        }
    }

    public static void Load()
    {
        MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume));
        MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume));
        SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume));
        VSyncEnabled = PlayerPrefs.GetInt(VSyncKey, DefaultVSync) == 1;
        FpsLimit = Mathf.Max(0, PlayerPrefs.GetInt(FpsLimitKey, DefaultFpsLimit));
        ShowFpsDisplay = PlayerPrefs.GetInt(ShowFpsDisplayKey, DefaultShowFpsDisplay) == 1;
        CurrentDisplayMode = SanitizeDisplayMode(PlayerPrefs.GetInt(DisplayModeKey, DefaultDisplayMode));
        Resolution currentResolution = Screen.currentResolution;
        ResolutionWidth = Mathf.Max(640, PlayerPrefs.GetInt(ResolutionWidthKey, currentResolution.width));
        ResolutionHeight = Mathf.Max(360, PlayerPrefs.GetInt(ResolutionHeightKey, currentResolution.height));
        RenderScale = ClampRenderScale(PlayerPrefs.GetFloat(RenderScaleKey, DefaultRenderScale));
        Brightness = Mathf.Clamp01(PlayerPrefs.GetFloat(BrightnessKey, DefaultBrightness));
        MouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(MouseSensitivityKey, DefaultMouseSensitivity), MinMouseSensitivity, MaxMouseSensitivity);
        GamepadSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(GamepadSensitivityKey, DefaultGamepadSensitivity), MinGamepadSensitivity, MaxGamepadSensitivity);
        InvertY = PlayerPrefs.GetInt(InvertYKey, DefaultInvertY) == 1;
        CurrentInputMode = SanitizeInputMode(PlayerPrefs.GetInt(InputModeKey, DefaultInputMode));
        LoadInputBindings();
        isLoaded = true;
        NotifyChanged();
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat(MasterVolumeKey, MasterVolume);
        PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
        PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
        PlayerPrefs.SetInt(VSyncKey, VSyncEnabled ? 1 : 0);
        PlayerPrefs.SetInt(FpsLimitKey, FpsLimit);
        PlayerPrefs.SetInt(ShowFpsDisplayKey, ShowFpsDisplay ? 1 : 0);
        PlayerPrefs.SetInt(DisplayModeKey, (int)CurrentDisplayMode);
        PlayerPrefs.SetInt(ResolutionWidthKey, ResolutionWidth);
        PlayerPrefs.SetInt(ResolutionHeightKey, ResolutionHeight);
        PlayerPrefs.SetFloat(RenderScaleKey, RenderScale);
        PlayerPrefs.SetFloat(BrightnessKey, Brightness);
        PlayerPrefs.SetFloat(MouseSensitivityKey, MouseSensitivity);
        PlayerPrefs.SetFloat(GamepadSensitivityKey, GamepadSensitivity);
        PlayerPrefs.SetInt(InvertYKey, InvertY ? 1 : 0);
        PlayerPrefs.SetInt(InputModeKey, (int)CurrentInputMode);
        SaveInputBindings();
        PlayerPrefs.Save();
    }

    public static void ApplyRuntime()
    {
        ApplyDisplayMode();
        ApplyRenderScale();
        QualitySettings.vSyncCount = VSyncEnabled ? 1 : 0;

        if (VSyncEnabled)
        {
            Application.targetFrameRate = -1;
            return;
        }

        Application.targetFrameRate = FpsLimit == 0 ? -1 : FpsLimit;
    }

    public static void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        NotifyChanged();
    }

    public static void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        NotifyChanged();
    }

    public static void SetSfxVolume(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
        NotifyChanged();
    }

    public static void SetVSync(bool enabled)
    {
        VSyncEnabled = enabled;
        NotifyChanged();
    }

    public static void SetFpsLimit(int value)
    {
        FpsLimit = Mathf.Max(0, value);
        NotifyChanged();
    }

    public static void SetShowFpsDisplay(bool enabled)
    {
        ShowFpsDisplay = enabled;
        NotifyChanged();
    }

    public static void SetResolution(int width, int height)
    {
        ResolutionWidth = Mathf.Max(640, width);
        ResolutionHeight = Mathf.Max(360, height);
        NotifyChanged();
    }

    public static void SetRenderScale(float value)
    {
        RenderScale = ClampRenderScale(value);
        NotifyChanged();
    }

    public static void SetBrightness(float value)
    {
        Brightness = Mathf.Clamp01(value);
        NotifyChanged();
    }

    public static void SetMouseSensitivity(float value)
    {
        MouseSensitivity = Mathf.Clamp(value, MinMouseSensitivity, MaxMouseSensitivity);
        NotifyChanged();
    }

    public static void SetGamepadSensitivity(float value)
    {
        GamepadSensitivity = Mathf.Clamp(value, MinGamepadSensitivity, MaxGamepadSensitivity);
        NotifyChanged();
    }

    public static void SetInvertY(bool enabled)
    {
        InvertY = enabled;
        NotifyChanged();
    }

    public static void CycleInputMode(int direction)
    {
        if (direction == 0)
        {
            return;
        }

        const int modeCount = 3;
        int current = (int)CurrentInputMode;
        int next = (current + direction + modeCount) % modeCount;
        CurrentInputMode = (InputMode)next;
        NotifyChanged();
    }

    public static void CycleDisplayMode(int direction)
    {
        if (direction == 0)
        {
            return;
        }

        const int modeCount = 3;
        int current = (int)CurrentDisplayMode;
        int next = (current + direction + modeCount) % modeCount;
        CurrentDisplayMode = (DisplayMode)next;
        NotifyChanged();
    }

    public static void ResetToDefaults()
    {
        MasterVolume = DefaultMasterVolume;
        MusicVolume = DefaultMusicVolume;
        SfxVolume = DefaultSfxVolume;
        VSyncEnabled = DefaultVSync == 1;
        FpsLimit = DefaultFpsLimit;
        ShowFpsDisplay = DefaultShowFpsDisplay == 1;
        CurrentDisplayMode = (DisplayMode)DefaultDisplayMode;
        Resolution currentResolution = Screen.currentResolution;
        ResolutionWidth = currentResolution.width;
        ResolutionHeight = currentResolution.height;
        RenderScale = DefaultRenderScale;
        Brightness = DefaultBrightness;
        MouseSensitivity = DefaultMouseSensitivity;
        GamepadSensitivity = DefaultGamepadSensitivity;
        InvertY = DefaultInvertY == 1;
        CurrentInputMode = (InputMode)DefaultInputMode;
        ResetInputBindings(false);
        isLoaded = true;
        NotifyChanged();
    }

    public static string GetDisplayModeLabel()
    {
        return CurrentDisplayMode switch
        {
            DisplayMode.Windowed => "WINDOWED",
            DisplayMode.BorderlessFullscreen => "WINDOWED FULLSCREEN",
            DisplayMode.ExclusiveFullscreen => "EXCLUSIVE",
            _ => "WINDOWED FULLSCREEN"
        };
    }

    public static string GetInputModeLabel()
    {
        return CurrentInputMode switch
        {
            InputMode.KeyboardMouse => "KEYBOARD",
            InputMode.Gamepad => "CONTROLLER",
            _ => "AUTO"
        };
    }

    public static bool AllowsKeyboardMouseInput()
    {
        return CurrentInputMode != InputMode.Gamepad;
    }

    public static bool AllowsGamepadInput()
    {
        return CurrentInputMode != InputMode.KeyboardMouse;
    }

    public static Key GetKeyboardBinding(RebindableAction action)
    {
        return action switch
        {
            RebindableAction.Flashlight => flashlightKey,
            RebindableAction.Crouch => crouchKey,
            RebindableAction.Sprint => sprintKey,
            _ => interactKey
        };
    }

    public static GamepadButton GetGamepadBinding(RebindableAction action)
    {
        return action switch
        {
            RebindableAction.Flashlight => flashlightButton,
            RebindableAction.Crouch => crouchButton,
            RebindableAction.Sprint => sprintButton,
            _ => interactButton
        };
    }

    public static void SetKeyboardBinding(RebindableAction action, Key key)
    {
        if (key == Key.None)
        {
            return;
        }

        switch (action)
        {
            case RebindableAction.Flashlight:
                flashlightKey = key;
                break;
            case RebindableAction.Crouch:
                crouchKey = key;
                break;
            case RebindableAction.Sprint:
                sprintKey = key;
                break;
            default:
                interactKey = key;
                break;
        }

        NotifyChanged();
    }

    public static void SetGamepadBinding(RebindableAction action, GamepadButton button)
    {
        switch (action)
        {
            case RebindableAction.Flashlight:
                flashlightButton = button;
                break;
            case RebindableAction.Crouch:
                crouchButton = button;
                break;
            case RebindableAction.Sprint:
                sprintButton = button;
                break;
            default:
                interactButton = button;
                break;
        }

        NotifyChanged();
    }

    public static bool WasKeyboardBindingPressed(RebindableAction action)
    {
        Keyboard keyboard = Keyboard.current;
        Key key = GetKeyboardBinding(action);
        return keyboard != null && key != Key.None && keyboard[key] != null && keyboard[key].wasPressedThisFrame;
    }

    public static bool IsKeyboardBindingPressed(RebindableAction action)
    {
        Keyboard keyboard = Keyboard.current;
        Key key = GetKeyboardBinding(action);
        return keyboard != null && key != Key.None && keyboard[key] != null && keyboard[key].isPressed;
    }

    public static bool WasGamepadBindingPressed(RebindableAction action)
    {
        Gamepad gamepad = Gamepad.current;
        return gamepad != null && gamepad[GetGamepadBinding(action)] != null && gamepad[GetGamepadBinding(action)].wasPressedThisFrame;
    }

    public static bool IsGamepadBindingPressed(RebindableAction action)
    {
        Gamepad gamepad = Gamepad.current;
        return gamepad != null && gamepad[GetGamepadBinding(action)] != null && gamepad[GetGamepadBinding(action)].isPressed;
    }

    public static void ResetInputBindings(bool notify = true)
    {
        interactKey = Key.E;
        flashlightKey = Key.F;
        crouchKey = Key.LeftCtrl;
        sprintKey = Key.LeftShift;
        interactButton = GamepadButton.North;
        flashlightButton = GamepadButton.RightShoulder;
        crouchButton = GamepadButton.East;
        sprintButton = GamepadButton.LeftStick;

        if (notify)
        {
            NotifyChanged();
        }
    }

    public static string GetBindingDisplay(RebindableAction action)
    {
        return FormatKey(GetKeyboardBinding(action)) + " / " + FormatButton(GetGamepadBinding(action));
    }

    public static string GetBindingDisplay(RebindableAction action, bool gamepad)
    {
        return gamepad ? FormatButton(GetGamepadBinding(action)) : FormatKey(GetKeyboardBinding(action));
    }

    private static DisplayMode SanitizeDisplayMode(int value)
    {
        if (Enum.IsDefined(typeof(DisplayMode), value))
        {
            return (DisplayMode)value;
        }

        return (DisplayMode)DefaultDisplayMode;
    }

    private static InputMode SanitizeInputMode(int value)
    {
        if (Enum.IsDefined(typeof(InputMode), value))
        {
            return (InputMode)value;
        }

        return (InputMode)DefaultInputMode;
    }

    private static float ClampRenderScale(float value)
    {
        return Mathf.Clamp(value, MinRenderScale, MaxRenderScale);
    }

    private static void LoadInputBindings()
    {
        interactKey = LoadKey(RebindableAction.Interact, Key.E);
        flashlightKey = LoadKey(RebindableAction.Flashlight, Key.F);
        crouchKey = LoadKey(RebindableAction.Crouch, Key.LeftCtrl);
        sprintKey = LoadKey(RebindableAction.Sprint, Key.LeftShift);
        interactButton = LoadButton(RebindableAction.Interact, GamepadButton.North);
        flashlightButton = LoadButton(RebindableAction.Flashlight, GamepadButton.RightShoulder);
        crouchButton = LoadButton(RebindableAction.Crouch, GamepadButton.East);
        sprintButton = LoadButton(RebindableAction.Sprint, GamepadButton.LeftStick);
    }

    private static void SaveInputBindings()
    {
        SaveBinding(RebindableAction.Interact);
        SaveBinding(RebindableAction.Flashlight);
        SaveBinding(RebindableAction.Crouch);
        SaveBinding(RebindableAction.Sprint);
    }

    private static Key LoadKey(RebindableAction action, Key fallback)
    {
        int value = PlayerPrefs.GetInt(KeyboardBindingPrefix + action, (int)fallback);
        return Enum.IsDefined(typeof(Key), value) ? (Key)value : fallback;
    }

    private static GamepadButton LoadButton(RebindableAction action, GamepadButton fallback)
    {
        int value = PlayerPrefs.GetInt(GamepadBindingPrefix + action, (int)fallback);
        return Enum.IsDefined(typeof(GamepadButton), value) ? (GamepadButton)value : fallback;
    }

    private static void SaveBinding(RebindableAction action)
    {
        PlayerPrefs.SetInt(KeyboardBindingPrefix + action, (int)GetKeyboardBinding(action));
        PlayerPrefs.SetInt(GamepadBindingPrefix + action, (int)GetGamepadBinding(action));
    }

    private static string FormatKey(Key key)
    {
        return key switch
        {
            Key.LeftCtrl => "LEFT CTRL",
            Key.RightCtrl => "RIGHT CTRL",
            Key.LeftShift => "LEFT SHIFT",
            Key.RightShift => "RIGHT SHIFT",
            Key.LeftAlt => "LEFT ALT",
            Key.RightAlt => "RIGHT ALT",
            Key.Space => "SPACE",
            Key.Enter => "ENTER",
            Key.Escape => "ESC",
            _ => key.ToString().ToUpperInvariant()
        };
    }

    private static string FormatButton(GamepadButton button)
    {
        return button switch
        {
            GamepadButton.South => "A",
            GamepadButton.East => "B",
            GamepadButton.West => "X",
            GamepadButton.North => "Y",
            GamepadButton.LeftShoulder => "LB",
            GamepadButton.RightShoulder => "RB",
            GamepadButton.LeftTrigger => "LT",
            GamepadButton.RightTrigger => "RT",
            GamepadButton.LeftStick => "L3",
            GamepadButton.RightStick => "R3",
            GamepadButton.Start => "START",
            GamepadButton.Select => "SELECT",
            _ => button.ToString().ToUpperInvariant()
        };
    }

    private static void ApplyDisplayMode()
    {
        Resolution currentResolution = Screen.currentResolution;
        int targetWidth = Mathf.Clamp(ResolutionWidth, 640, currentResolution.width);
        int targetHeight = Mathf.Clamp(ResolutionHeight, 360, currentResolution.height);

        switch (CurrentDisplayMode)
        {
            case DisplayMode.Windowed:
                Screen.SetResolution(targetWidth, targetHeight, FullScreenMode.Windowed, currentResolution.refreshRateRatio);
                break;
            case DisplayMode.ExclusiveFullscreen:
                Screen.SetResolution(targetWidth, targetHeight, FullScreenMode.ExclusiveFullScreen, currentResolution.refreshRateRatio);
                break;
            default:
                Screen.SetResolution(currentResolution.width, currentResolution.height, FullScreenMode.FullScreenWindow, currentResolution.refreshRateRatio);
                break;
        }
    }

    private static void ApplyRenderScale()
    {
        RenderPipelineAsset pipelineAsset = GraphicsSettings.currentRenderPipeline != null
            ? GraphicsSettings.currentRenderPipeline
            : QualitySettings.renderPipeline;

        UniversalRenderPipelineAsset urpAsset = pipelineAsset as UniversalRenderPipelineAsset;
        if (urpAsset != null)
        {
            urpAsset.renderScale = RenderScale;
        }
    }

    private static void NotifyChanged()
    {
        SettingsChanged?.Invoke();
    }
}
