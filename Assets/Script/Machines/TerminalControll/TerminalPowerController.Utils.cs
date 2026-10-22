using UnityEngine;
using System;
using UnityEngine.InputSystem;

public partial class TerminalPowerController
{
    private static int GetVerticalStep()
    {
        bool up = GameSettings.AllowsKeyboardMouseInput() && Keyboard.current != null &&
                  (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame);
        bool down = GameSettings.AllowsKeyboardMouseInput() && Keyboard.current != null &&
                    (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame);

        if (GameSettings.AllowsGamepadInput() && Gamepad.current != null)
        {
            up |= Gamepad.current.dpad.up.wasPressedThisFrame || Gamepad.current.leftStick.up.wasPressedThisFrame;
            down |= Gamepad.current.dpad.down.wasPressedThisFrame || Gamepad.current.leftStick.down.wasPressedThisFrame;
        }

        if (up == down)
        {
            return 0;
        }

        if (GameSettings.AllowsKeyboardMouseInput() && Keyboard.current != null &&
            (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame ||
             Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame))
        {
            InputDeviceTracker.MarkKeyboardMouse();
        }
        else
        {
            InputDeviceTracker.MarkGamepad();
        }

        return up ? -1 : 1;
    }

    private static int GetHorizontalStep()
    {
        bool left = GameSettings.AllowsKeyboardMouseInput() && Keyboard.current != null &&
                    (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame);
        bool right = GameSettings.AllowsKeyboardMouseInput() && Keyboard.current != null &&
                     (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame);

        if (GameSettings.AllowsGamepadInput() && Gamepad.current != null)
        {
            left |= Gamepad.current.dpad.left.wasPressedThisFrame || Gamepad.current.leftStick.ReadValue().x < -0.7f;
            right |= Gamepad.current.dpad.right.wasPressedThisFrame || Gamepad.current.leftStick.ReadValue().x > 0.7f;
        }

        if (left == right)
        {
            return 0;
        }

        if (GameSettings.AllowsKeyboardMouseInput() && Keyboard.current != null &&
            (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame ||
             Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame))
        {
            InputDeviceTracker.MarkKeyboardMouse();
        }
        else
        {
            InputDeviceTracker.MarkGamepad();
        }

        return right ? 1 : -1;
    }

    private static int WrapIndex(int value, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        value %= count;
        if (value < 0)
        {
            value += count;
        }

        return value;
    }

    private static bool WasSubmitPressed()
    {
        bool keyboardSubmit = GameSettings.AllowsKeyboardMouseInput() && Keyboard.current != null &&
                              (Keyboard.current.enterKey.wasPressedThisFrame ||
                               Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                               Keyboard.current.spaceKey.wasPressedThisFrame);
        bool gamepadSubmit = GameSettings.AllowsGamepadInput() && Gamepad.current != null &&
                             (Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.startButton.wasPressedThisFrame);
        if (keyboardSubmit) InputDeviceTracker.MarkKeyboardMouse();
        if (gamepadSubmit) InputDeviceTracker.MarkGamepad();
        return keyboardSubmit || gamepadSubmit;
    }

    private static bool WasBackPressed()
    {
        bool keyboardBack = GameSettings.AllowsKeyboardMouseInput() && Keyboard.current != null &&
                            (Keyboard.current.backspaceKey.wasPressedThisFrame || Keyboard.current.deleteKey.wasPressedThisFrame);
        bool gamepadBack = GameSettings.AllowsGamepadInput() && Gamepad.current != null &&
                           (Gamepad.current.buttonWest.wasPressedThisFrame || Gamepad.current.selectButton.wasPressedThisFrame);
        if (keyboardBack) InputDeviceTracker.MarkKeyboardMouse();
        if (gamepadBack) InputDeviceTracker.MarkGamepad();
        return keyboardBack || gamepadBack;
    }

    private static bool[] CacheObjectStates(GameObject[] targets)
    {
        if (targets == null)
        {
            return Array.Empty<bool>();
        }

        bool[] states = new bool[targets.Length];
        for (int i = 0; i < targets.Length; i++)
        {
            states[i] = targets[i] != null && targets[i].activeSelf;
        }

        return states;
    }

    private static bool[] CacheBehaviourStates(Behaviour[] targets)
    {
        if (targets == null)
        {
            return Array.Empty<bool>();
        }

        bool[] states = new bool[targets.Length];
        for (int i = 0; i < targets.Length; i++)
        {
            states[i] = targets[i] != null && targets[i].enabled;
        }

        return states;
    }

    private static Vector2 SortVector(Vector2 value)
    {
        return value.x <= value.y ? value : new Vector2(value.y, value.x);
    }

    private static bool ContainsToken(string source, string token)
    {
        return !string.IsNullOrEmpty(source) &&
               source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string SafeField(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "UNKNOWN" : value.Trim();
    }

    private int GetMenuItemCount()
    {
        return readyMenuItems != null ? readyMenuItems.Length : 0;
    }

    private void AddLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        runtimeLogs.Add("[" + GetWorldTimeString() + "] " + message);
        while (runtimeLogs.Count > maxRuntimeLogEntries)
        {
            runtimeLogs.RemoveAt(0);
        }
    }

    private float GetWorldTime01()
    {
        if (timeOfDayController == null)
        {
            timeOfDayController = FindFirstObjectByType<TimeOfDayController>();
        }

        return timeOfDayController != null ? Mathf.Repeat(timeOfDayController.Time01, 1f) : 0f;
    }

    private string GetWorldTimeString()
    {
        return FormatTime01(GetWorldTime01());
    }

    private static string FormatTimestamp(bool hasValue, float value)
    {
        return hasValue ? FormatTime01(value) : "--:--:--";
    }

    private static string FormatFrequency(float frequency)
    {
        return frequency.ToString("000.0") + " MHz";
    }

    private static string FormatTime01(float time01)
    {
        float totalSeconds = Mathf.Repeat(time01, 1f) * 86400f;
        int hours = Mathf.FloorToInt(totalSeconds / 3600f) % 24;
        int minutes = Mathf.FloorToInt(totalSeconds / 60f) % 60;
        int seconds = Mathf.FloorToInt(totalSeconds) % 60;
        return hours.ToString("00") + ":" + minutes.ToString("00") + ":" + seconds.ToString("00");
    }
}
