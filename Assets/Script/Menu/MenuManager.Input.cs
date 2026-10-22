using UnityEngine;
using UnityEngine.InputSystem;

public partial class MenuManager
{
    private void HandleNavigationInput()
    {
        if (currentScreen == TerminalScreen.NewGameConfirm)
        {
            HandleNewGameConfirmInput();
            return;
        }

        if (currentScreen == TerminalScreen.NewGameDifficulty)
        {
            HandleNewGameDifficultyInput();
            return;
        }

        if (currentScreen == TerminalScreen.NewGameProfile)
        {
            HandleNewGameProfileInput();
            return;
        }

        if (currentScreen == TerminalScreen.InputBindings)
        {
            HandleInputBindingsInput();
            return;
        }

        bool moved = false;
        int maxItems = currentScreen == TerminalScreen.Main ? mainMenuItems.Length : settingsItems.Length;
        int verticalStep = GetVerticalStep();

        if (verticalStep < 0)
        {
            selectedIndex = (selectedIndex - 1 + maxItems) % maxItems;
            moved = true;
        }
        else if (verticalStep > 0)
        {
            selectedIndex = (selectedIndex + 1) % maxItems;
            moved = true;
        }

        if (moved)
        {
            RegisterUserInput();
            PlaySfx(cursorBeep);
            RenderCurrentMenu();
            StartBlinkEffect();
        }

        if (currentScreen == TerminalScreen.Settings)
        {
            int horizontalStep = GetHorizontalStep();
            if (horizontalStep != 0)
            {
                RegisterUserInput();
                PlaySfx(cursorBeep);
                AdjustCurrentSetting(horizontalStep);
                return;
            }
        }

        if (WasSubmitPressed())
        {
            RegisterUserInput();
            StartCoroutine(ConfirmSelectionRoutine());
        }

        if (currentScreen == TerminalScreen.Settings && WasBackPressed())
        {
            RegisterUserInput();
            RequestOpenMainMenu();
        }
    }

    private bool WasAnySubmitPressed()
    {
        return WasSubmitPressed() || WasBackPressed();
    }

    private bool WasSubmitPressed()
    {
        Keyboard kb = Keyboard.current;
        Gamepad gp = Gamepad.current;

        bool keyboardSubmit = ShouldReadKeyboardMouse() && kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
        bool gamepadSubmit = ShouldReadGamepad() && gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame);
        if (keyboardSubmit) InputDeviceTracker.MarkKeyboardMouse();
        if (gamepadSubmit) InputDeviceTracker.MarkGamepad();
        return keyboardSubmit || gamepadSubmit;
    }

    private bool WasBackPressed()
    {
        Keyboard kb = Keyboard.current;
        Gamepad gp = Gamepad.current;

        bool keyboardBack = ShouldReadKeyboardMouse() && kb != null && kb.escapeKey.wasPressedThisFrame;
        bool gamepadBack = ShouldReadGamepad() && gp != null && gp.buttonEast.wasPressedThisFrame;
        if (keyboardBack) InputDeviceTracker.MarkKeyboardMouse();
        if (gamepadBack) InputDeviceTracker.MarkGamepad();
        return keyboardBack || gamepadBack;
    }

    private int GetVerticalStep()
    {
        return GetHeldStep(GetVerticalRawDirection(), ref heldVerticalDirection, ref nextVerticalRepeatTime);
    }

    private int GetHorizontalStep()
    {
        return GetHeldStep(GetHorizontalRawDirection(), ref heldHorizontalDirection, ref nextHorizontalRepeatTime);
    }

    private int GetHeldStep(int rawDirection, ref int heldDirection, ref float nextRepeatTime)
    {
        if (rawDirection == 0)
        {
            heldDirection = 0;
            nextRepeatTime = 0f;
            return 0;
        }

        if (rawDirection != heldDirection)
        {
            heldDirection = rawDirection;
            nextRepeatTime = Time.unscaledTime + holdRepeatDelay;
            return rawDirection;
        }

        if (Time.unscaledTime >= nextRepeatTime)
        {
            nextRepeatTime = Time.unscaledTime + holdRepeatInterval;
            return rawDirection;
        }

        return 0;
    }

    private int GetVerticalRawDirection()
    {
        Keyboard kb = Keyboard.current;
        Gamepad gp = Gamepad.current;

        bool up = ShouldReadKeyboardMouse() && kb != null && (kb.wKey.isPressed || kb.upArrowKey.isPressed);
        bool down = ShouldReadKeyboardMouse() && kb != null && (kb.sKey.isPressed || kb.downArrowKey.isPressed);

        if (ShouldReadGamepad() && gp != null)
        {
            up |= gp.dpad.up.isPressed || gp.leftStick.ReadValue().y > stickMoveThreshold;
            down |= gp.dpad.down.isPressed || gp.leftStick.ReadValue().y < -stickMoveThreshold;
        }

        if (up == down)
        {
            return 0;
        }

        if ((ShouldReadKeyboardMouse() && kb != null && (kb.wKey.isPressed || kb.upArrowKey.isPressed || kb.sKey.isPressed || kb.downArrowKey.isPressed)))
        {
            InputDeviceTracker.MarkKeyboardMouse();
        }
        else
        {
            InputDeviceTracker.MarkGamepad();
        }

        return up ? -1 : 1;
    }

    private int GetHorizontalRawDirection()
    {
        Keyboard kb = Keyboard.current;
        Gamepad gp = Gamepad.current;

        bool left = ShouldReadKeyboardMouse() && kb != null && (kb.aKey.isPressed || kb.leftArrowKey.isPressed);
        bool right = ShouldReadKeyboardMouse() && kb != null && (kb.dKey.isPressed || kb.rightArrowKey.isPressed);

        if (ShouldReadGamepad() && gp != null)
        {
            left |= gp.dpad.left.isPressed || gp.leftStick.ReadValue().x < -stickMoveThreshold;
            right |= gp.dpad.right.isPressed || gp.leftStick.ReadValue().x > stickMoveThreshold;
        }

        if (left == right)
        {
            return 0;
        }

        if ((ShouldReadKeyboardMouse() && kb != null && (kb.aKey.isPressed || kb.leftArrowKey.isPressed || kb.dKey.isPressed || kb.rightArrowKey.isPressed)))
        {
            InputDeviceTracker.MarkKeyboardMouse();
        }
        else
        {
            InputDeviceTracker.MarkGamepad();
        }

        return right ? 1 : -1;
    }

    private static bool ShouldReadKeyboardMouse()
    {
        return GameSettings.AllowsKeyboardMouseInput() || Gamepad.current == null;
    }

    private static bool ShouldReadGamepad()
    {
        return GameSettings.AllowsGamepadInput();
    }
}
