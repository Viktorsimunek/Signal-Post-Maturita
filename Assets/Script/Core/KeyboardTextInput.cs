using UnityEngine.InputSystem;

public static class KeyboardTextInput
{
    private static readonly Key[] LetterKeys =
    {
        Key.A, Key.B, Key.C, Key.D, Key.E, Key.F, Key.G, Key.H, Key.I, Key.J,
        Key.K, Key.L, Key.M, Key.N, Key.O, Key.P, Key.Q, Key.R, Key.S, Key.T,
        Key.U, Key.V, Key.W, Key.X, Key.Y, Key.Z
    };

    private static readonly Key[] DigitKeys =
    {
        Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
        Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
    };

    private static readonly Key[] NumpadKeys =
    {
        Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4,
        Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9
    };

    public static bool WasEnterPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !GameSettings.AllowsKeyboardMouseInput())
        {
            return false;
        }

        bool pressed = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
        if (pressed)
        {
            InputDeviceTracker.MarkKeyboardMouse();
        }

        return pressed;
    }

    public static bool WasDeletePressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !GameSettings.AllowsKeyboardMouseInput())
        {
            return false;
        }

        bool pressed = keyboard.backspaceKey.wasPressedThisFrame || keyboard.deleteKey.wasPressedThisFrame;
        if (pressed)
        {
            InputDeviceTracker.MarkKeyboardMouse();
        }

        return pressed;
    }

    public static bool TryReadAllowedCharacter(bool allowSpace, bool allowUnderscore, out string token)
    {
        token = string.Empty;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !GameSettings.AllowsKeyboardMouseInput())
        {
            return false;
        }

        for (int i = 0; i < 26; i++)
        {
            Key key = LetterKeys[i];
            if (keyboard[key].wasPressedThisFrame)
            {
                token = ((char)('A' + i)).ToString();
                InputDeviceTracker.MarkKeyboardMouse();
                return true;
            }
        }

        for (int i = 0; i < 10; i++)
        {
            if (keyboard[DigitKeys[i]].wasPressedThisFrame || keyboard[NumpadKeys[i]].wasPressedThisFrame)
            {
                token = i.ToString();
                InputDeviceTracker.MarkKeyboardMouse();
                return true;
            }
        }

        if (allowUnderscore && keyboard.minusKey.wasPressedThisFrame && IsShiftPressed(keyboard))
        {
            token = "_";
        }
        else if (keyboard.minusKey.wasPressedThisFrame)
        {
            token = "-";
        }
        else if (keyboard.periodKey.wasPressedThisFrame || keyboard.numpadPeriodKey.wasPressedThisFrame)
        {
            token = ".";
        }
        else if (keyboard.slashKey.wasPressedThisFrame)
        {
            token = "/";
        }
        else if (allowSpace && keyboard.spaceKey.wasPressedThisFrame)
        {
            token = " ";
        }

        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        InputDeviceTracker.MarkKeyboardMouse();
        return true;
    }

    private static bool IsShiftPressed(Keyboard keyboard)
    {
        return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
    }
}
