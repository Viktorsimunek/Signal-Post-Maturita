using System;
using UnityEngine;
using UnityEngine.InputSystem;

public static class InputPromptDisplay
{
    public const string KeyboardMouseScheme = "Keyboard&Mouse";
    public const string GamepadScheme = "Gamepad";

    public static string GetCurrentControlScheme(Component context)
    {
        InputDeviceTracker.MarkFromCurrentFrame();

        if (GameSettings.CurrentInputMode == GameSettings.InputMode.KeyboardMouse)
        {
            return KeyboardMouseScheme;
        }

        if (GameSettings.CurrentInputMode == GameSettings.InputMode.Gamepad)
        {
            return GamepadScheme;
        }

        if (InputDeviceTracker.LastInputWasGamepad)
        {
            return GamepadScheme;
        }

        return KeyboardMouseScheme;
    }

    public static bool IsGamepadScheme(Component context)
    {
        return string.Equals(GetCurrentControlScheme(context), GamepadScheme, StringComparison.OrdinalIgnoreCase);
    }

    public static string GetExitDisplay(Component context, PlayerInteractor interactor)
    {
        if (IsGamepadScheme(context))
        {
            string interact = interactor != null ? interactor.GetInteractionButtonText() : "Y";
            return JoinUnique(interact, "B");
        }

        string keyboardInteract = interactor != null ? interactor.GetInteractionButtonText() : "E";
        return JoinUnique(keyboardInteract, "Esc");
    }

    public static string GetHorizontalTuneDisplay(Component context)
    {
        return IsGamepadScheme(context) ? "Left Stick / D-Pad" : "A / D";
    }

    public static string GetBindingDisplayString(InputAction action, string controlScheme)
    {
        if (action == null)
        {
            return string.Empty;
        }

        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (binding.isComposite || binding.isPartOfComposite)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(controlScheme) && !BindingMatchesControlScheme(binding.groups, controlScheme))
            {
                continue;
            }

            string displayString = action.GetBindingDisplayString(i, InputBinding.DisplayStringOptions.DontIncludeInteractions);
            if (!string.IsNullOrWhiteSpace(displayString))
            {
                return displayString;
            }
        }

        return string.Empty;
    }

    public static bool BindingMatchesControlScheme(string bindingGroups, string controlScheme)
    {
        if (string.IsNullOrWhiteSpace(controlScheme))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(bindingGroups))
        {
            return false;
        }

        string[] groups = bindingGroups.Split(';', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < groups.Length; i++)
        {
            if (string.Equals(groups[i].Trim(), controlScheme, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string JoinUnique(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first))
        {
            return second;
        }

        if (string.IsNullOrWhiteSpace(second) || string.Equals(first, second, StringComparison.OrdinalIgnoreCase))
        {
            return first;
        }

        return first + " / " + second;
    }
}
