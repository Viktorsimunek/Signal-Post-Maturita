using UnityEngine.InputSystem;

public static class InputDeviceTracker
{
    private static bool lastInputWasGamepad;

    public static bool LastInputWasGamepad => lastInputWasGamepad;

    public static void MarkKeyboardMouse()
    {
        lastInputWasGamepad = false;
    }

    public static void MarkGamepad()
    {
        lastInputWasGamepad = true;
    }

    public static void MarkFromCurrentFrame()
    {
        if (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame)
        {
            MarkGamepad();
            return;
        }

        if ((Keyboard.current != null && Keyboard.current.wasUpdatedThisFrame) ||
            (Mouse.current != null && Mouse.current.wasUpdatedThisFrame))
        {
            MarkKeyboardMouse();
        }
    }
}
