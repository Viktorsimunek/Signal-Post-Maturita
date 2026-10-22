using UnityEngine;

public partial class TerminalPowerController
{
    private void HandleReadyInput()
    {
        if (currentView == ReadyView.Menu)
        {
            int move = GetVerticalStep();
            if (move != 0 && GetMenuItemCount() > 0)
            {
                selectedMenuIndex = (selectedMenuIndex + move + GetMenuItemCount()) % GetMenuItemCount();
                PlayCursorSfx();
                RenderCurrentView();
                StartBlinkEffect();
            }

            if (WasSubmitPressed())
            {
                ActivateCurrentMenuItem();
            }

            return;
        }

        if (currentView == ReadyView.Transmit)
        {
            HandleTransmitInput();
            return;
        }

        if (currentView == ReadyView.PersonDatabase)
        {
            HandlePersonDatabaseInput();
            return;
        }

        if (currentView == ReadyView.PowerConfirm)
        {
            HandlePowerConfirmInput();
            return;
        }

        if (WasSubmitPressed() || WasBackPressed())
        {
            PlayConfirmSfx();
            currentView = ReadyView.Menu;
            if (terminalControl != null)
            {
                terminalControl.BlockExitInput = false;
                terminalControl.BlockInteractExitInput = false;
            }
            RenderCurrentView();
            StartBlinkEffect();
        }
    }

    private void ActivateCurrentMenuItem()
    {
        if (GetMenuItemCount() == 0)
        {
            PlayErrorSfx();
            ShowEventMessage("NO MENU ITEMS CONFIGURED", 1f);
            return;
        }

        string selectedItem = readyMenuItems[Mathf.Clamp(selectedMenuIndex, 0, GetMenuItemCount() - 1)];
        PlayConfirmSfx();

        if (ContainsToken(selectedItem, "STATUS"))
        {
            currentView = ReadyView.Status;
            RenderCurrentView();
            StartBlinkEffect();
            return;
        }

        if (ContainsToken(selectedItem, "LOG"))
        {
            AddLog("ARCHIVE VIEW OPENED");
            currentView = ReadyView.Logs;
            RenderCurrentView();
            StartBlinkEffect();
            return;
        }

        if (ContainsToken(selectedItem, "HISTORY"))
        {
            AddLog("SIGNAL HISTORY OPENED");
            currentView = ReadyView.SignalHistory;
            RenderCurrentView();
            StartBlinkEffect();
            return;
        }

        if (ContainsToken(selectedItem, "PERSON") || ContainsToken(selectedItem, "PEOPLE") || ContainsToken(selectedItem, "DATABASE"))
        {
            currentView = ReadyView.PersonDatabase;
            personKeyboardRow = 0;
            personKeyboardColumn = 0;
            personLookupMessage = "ENTER PERSON CODE";
            AddLog("PERSON DATABASE OPENED");
            RenderCurrentView();
            StartBlinkEffect();
            return;
        }

        if (ContainsToken(selectedItem, "SEND") || ContainsToken(selectedItem, "TRANSMIT"))
        {
            currentView = ReadyView.Transmit;
            transmitPage = TransmitPage.Message;
            transmitKeyboardRow = 0;
            transmitKeyboardColumn = 0;
            AddLog("TRANSMIT SCREEN OPENED");
            RenderCurrentView();
            StartBlinkEffect();
            return;
        }

        if (ContainsToken(selectedItem, "POWER"))
        {
            currentView = ReadyView.PowerConfirm;
            powerConfirmIndex = 1;
            AddLog("POWER DOWN CONFIRM OPENED");
            RenderCurrentView();
            StartBlinkEffect();
            return;
        }

        PlayErrorSfx();
        ShowEventMessage("NO ACTION ASSIGNED", 1f);
        AddLog("UNMAPPED MENU ITEM: " + selectedItem.ToUpperInvariant());
        StartBlinkEffect();
    }

    private void HandlePowerConfirmInput()
    {
        int horizontalStep = GetHorizontalStep();
        int verticalStep = GetVerticalStep();
        int step = horizontalStep != 0 ? horizontalStep : verticalStep;

        if (step != 0)
        {
            powerConfirmIndex = WrapIndex(powerConfirmIndex + step, 2);
            PlayCursorSfx();
            RenderCurrentView();
            StartBlinkEffect();
            return;
        }

        if (WasBackPressed())
        {
            PlayConfirmSfx();
            currentView = ReadyView.Menu;
            RenderCurrentView();
            StartBlinkEffect();
            return;
        }

        if (!WasSubmitPressed())
        {
            return;
        }

        PlayConfirmSfx();
        if (powerConfirmIndex == 0)
        {
            typedMessage = string.Empty;
            typedConfirmCode = string.Empty;
            AddLog("POWER DOWN CONFIRMED");
            TurnOff();
            return;
        }

        currentView = ReadyView.Menu;
        RenderCurrentView();
        StartBlinkEffect();
    }
}
