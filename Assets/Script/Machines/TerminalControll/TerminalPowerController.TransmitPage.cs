using UnityEngine;

public partial class TerminalPowerController
{
    private enum TransmitPage
    {
        Message,
        Check,
        Code,
        Sending,
        Done
    }

    private string BuildTransmitText()
    {
        switch (transmitPage)
        {
            case TransmitPage.Check:
                return BuildTransmitCheckPage();
            case TransmitPage.Code:
                return BuildTransmitCodePage();
            case TransmitPage.Sending:
                return BuildTransmitSendingPage();
            case TransmitPage.Done:
                return BuildTransmitDonePage();
            default:
                return BuildTransmitMessagePage();
        }
    }

    private string BuildTransmitMessagePage()
    {
        string wanted = "READ PAPER ORDER";
        string text =
            "SEND MESSAGE  1/4\n\n" +
            "ORDER: " + wanted + "\n\n" +
            "MESSAGE BUFFER:\n" +
            "> " + (typedMessage.Length > 0 ? typedMessage : "_") + "\n\n" +
            "VIRTUAL KEYBOARD\n";

        for (int row = 0; row < TransmitKeyboardRows.Length; row++)
        {
            text += BuildTransmitKeyboardRow(row) + "\n";
        }

        return text.TrimEnd();
    }

    private string BuildTransmitCheckPage()
    {
        string power = messageTransmitter != null && messageTransmitter.HasPower ? "OK" : "NO POWER";
        string signal = messageTransmitter != null ? Mathf.RoundToInt(messageTransmitter.SignalValue * 100f).ToString("000") + "%" : "---";
        string signalState = messageTransmitter != null && messageTransmitter.HasGoodSignal ? "HIGH" : "LOW";
        string freq = messageTransmitter != null ? FormatFrequency(messageTransmitter.CurrentFrequency) : "---.- MHz";
        string freqState = messageTransmitter != null && messageTransmitter.HasStableFrequency ? "LOCK" : "BAD";
        string wantedFreq = "FROM PAPER";

        return
            "SEND CHECK  2/4\n\n" +
            "POWER RACK: " + power + "\n" +
            "SIGNAL QUALITY: " + signalState + " " + signal + "\n" +
            "FREQUENCY: " + freqState + " " + freq + "\n\n" +
            "TARGET FREQUENCY: " + wantedFreq + "\n\n" +
            "WHEN ALL IS OK PRESS ENTER.";
    }

    private string BuildTransmitCodePage()
    {
        string wantedCode = showTransmitOrderOnTerminal && messageTransmitter != null ? messageTransmitter.ConfirmationCode : "FROM PAPER";
        string text =
            "CONFIRM CODE  3/4\n\n" +
            "ORDER CODE: " + wantedCode + "\n\n" +
            "CODE BUFFER:\n" +
            "> " + (typedConfirmCode.Length > 0 ? typedConfirmCode : "_") + "\n\n" +
            "VIRTUAL KEYBOARD\n";

        for (int row = 0; row < TransmitKeyboardRows.Length; row++)
        {
            text += BuildTransmitKeyboardRow(row) + "\n";
        }

        return text.TrimEnd();
    }

    private string BuildTransmitSendingPage()
    {
        string status = messageTransmitter != null ? messageTransmitter.LastStatus : "NO TRANSMITTER";
        float progress = messageTransmitter != null ? messageTransmitter.Progress : 0f;

        return
            "TRANSMITTING  4/4\n\n" +
            "MESSAGE:\n" +
            (typedMessage.Length > 0 ? typedMessage : "_") + "\n\n" +
            "PROGRESS " + BuildProgressBar(progress) + " " + Mathf.RoundToInt(progress * 100f).ToString("000") + "%\n" +
            "STATUS: " + status + "\n\n" +
            "KEEP POWER, SIGNAL AND FREQUENCY STABLE.";
    }

    private string BuildTransmitDonePage()
    {
        return
            "SIGNAL SENT  4/4\n\n" +
            "TRANSMISSION COMPLETE\n" +
            "REMOTE ACK RECEIVED\n\n" +
            "MESSAGE:\n" +
            (typedMessage.Length > 0 ? typedMessage : "_") + "\n\n" +
            "CONFIRMATION CODE ACCEPTED\n" +
            "RELAY LINK CLOSED";
    }

    private string BuildTransmitKeyboardRow(int row)
    {
        string result = string.Empty;
        string[] tokens = TransmitKeyboardRows[row];

        for (int column = 0; column < tokens.Length; column++)
        {
            if (column > 0)
            {
                result += " ";
            }

            bool selected = row == transmitKeyboardRow && column == transmitKeyboardColumn;
            string label = tokens[column] == "NEXT" && transmitPage == TransmitPage.Code ? "SEND" : tokens[column];
            result += selected ? "[" + label + "]" : " " + label + " ";
        }

        return result;
    }

    private string BuildTransmitStatus()
    {
        if (messageTransmitter == null)
        {
            return "TX OFFLINE";
        }

        string power = messageTransmitter.HasPower ? "POWER OK" : "NO POWER";
        string signal = Mathf.RoundToInt(messageTransmitter.SignalValue * 100f).ToString("000") + "%";
        string signalState = messageTransmitter.HasGoodSignal ? "HIGH" : "LOW";
        string freq = FormatFrequency(messageTransmitter.CurrentFrequency);
        string freqState = messageTransmitter.HasStableFrequency ? "LOCK" : "BAD";

        return power + "\n" +
               "SIGNAL " + signalState + " " + signal + "\n" +
               "FREQ " + freqState + " " + freq + "\n" +
               relayNodeId;
    }

    private string BuildProgressBar(float value)
    {
        value = Mathf.Clamp01(value);
        const int size = 18;
        int filled = Mathf.RoundToInt(value * size);
        string bar = "[";

        for (int i = 0; i < size; i++)
        {
            bar += i < filled ? "#" : "-";
        }

        return bar + "]";
    }

    private string GetTransmitFooter()
    {
        switch (transmitPage)
        {
            case TransmitPage.Check:
                return transmitCheckFooterBlock;
            case TransmitPage.Code:
                return GameProgress.IsEasyDifficulty ? "TYPE CODE  ENTER SEND  BACKSPACE PREV" : transmitCodeFooterBlock;
            case TransmitPage.Sending:
                return transmitSendingFooterBlock;
            case TransmitPage.Done:
                return "ENTER MENU  BACKSPACE MENU";
            default:
                if (GameProgress.IsEasyDifficulty)
                {
                    return "TYPE MESSAGE  ENTER NEXT  BACKSPACE MENU";
                }

                return string.IsNullOrWhiteSpace(transmitMessageFooterBlock)
                    ? "ARROWS SELECT  ENTER PRESS  BACKSPACE MENU"
                    : transmitMessageFooterBlock.Replace("BACKSPACE DEL", "BACKSPACE MENU");
        }
    }

    private void HandleTransmitInput()
    {
        if (messageTransmitter != null && messageTransmitter.IsSending)
        {
            transmitPage = TransmitPage.Sending;
            return;
        }

        if (UpdateTransmitFinishedState(true))
        {
            return;
        }

        if (transmitPage == TransmitPage.Check)
        {
            HandleTransmitCheckInput();
            return;
        }

        if (transmitPage == TransmitPage.Sending || transmitPage == TransmitPage.Done)
        {
            if (WasSubmitPressed() || WasBackPressed())
            {
                LeaveTransmitView();
            }

            return;
        }

        if (GameProgress.IsEasyDifficulty && HandleTypedTransmitInput())
        {
            RenderCurrentView();
            return;
        }

        int verticalStep = GetVerticalStep();
        int horizontalStep = GetHorizontalStep();
        if (verticalStep != 0 || horizontalStep != 0)
        {
            MoveTransmitKeyboardSelection(verticalStep, horizontalStep);
            PlayCursorSfx();
            RenderCurrentView();
            return;
        }

        if (WasBackPressed())
        {
            if (transmitPage == TransmitPage.Code)
            {
                transmitPage = TransmitPage.Check;
                PlayConfirmSfx();
                RenderCurrentView();
                StartBlinkEffect();
                return;
            }

            if (transmitPage == TransmitPage.Message)
            {
                PlayConfirmSfx();
                LeaveTransmitView();
                return;
            }

            RemoveLetterFromCurrentField();
            RenderCurrentView();
            return;
        }

        if (WasSubmitPressed())
        {
            ActivateTransmitKeyboardToken();
            RenderCurrentView();
        }
    }

    private bool HandleTypedTransmitInput()
    {
        if (KeyboardTextInput.WasDeletePressed())
        {
            if (HasTextInCurrentField())
            {
                RemoveLetterFromCurrentField();
                return true;
            }

            if (transmitPage == TransmitPage.Code)
            {
                transmitPage = TransmitPage.Check;
                PlayConfirmSfx();
                StartBlinkEffect();
                return true;
            }

            LeaveTransmitView();
            return true;
        }

        if (KeyboardTextInput.WasEnterPressed())
        {
            GoNextTransmitPage();
            return true;
        }

        bool allowSpace = transmitPage == TransmitPage.Message;
        if (KeyboardTextInput.TryReadAllowedCharacter(allowSpace, false, out string token))
        {
            AddTokenToCurrentField(token);
            return true;
        }

        return false;
    }

    private void HandleTransmitCheckInput()
    {
        if (WasBackPressed())
        {
            transmitPage = TransmitPage.Message;
            PlayConfirmSfx();
            RenderCurrentView();
            StartBlinkEffect();
            return;
        }

        if (!WasSubmitPressed())
        {
            return;
        }

        if (messageTransmitter == null)
        {
            PlayErrorSfx();
            ShowEventMessage("NO TRANSMITTER CONNECTED", 1.2f);
            return;
        }

        if (!messageTransmitter.CanStartSend(out string reason))
        {
            PlayErrorSfx();
            ShowEventMessage(reason, 1.2f);
            return;
        }

        transmitPage = TransmitPage.Code;
        PlayConfirmSfx();
        RenderCurrentView();
        StartBlinkEffect();
    }

    private void MoveTransmitKeyboardSelection(int verticalStep, int horizontalStep)
    {
        if (verticalStep != 0)
        {
            transmitKeyboardRow = WrapIndex(transmitKeyboardRow + verticalStep, TransmitKeyboardRows.Length);
            transmitKeyboardColumn = Mathf.Clamp(transmitKeyboardColumn, 0, TransmitKeyboardRows[transmitKeyboardRow].Length - 1);
        }

        if (horizontalStep != 0)
        {
            transmitKeyboardColumn = WrapIndex(transmitKeyboardColumn + horizontalStep, TransmitKeyboardRows[transmitKeyboardRow].Length);
        }
    }

    private void ActivateTransmitKeyboardToken()
    {
        string token = TransmitKeyboardRows[transmitKeyboardRow][transmitKeyboardColumn];

        switch (token)
        {
            case "SPACE":
                AddTokenToCurrentField(" ");
                return;
            case "BACK":
                RemoveLetterFromCurrentField();
                return;
            case "CLEAR":
                ClearCurrentField();
                PlayConfirmSfx();
                return;
            case "NEXT":
                GoNextTransmitPage();
                return;
            default:
                AddTokenToCurrentField(token);
                return;
        }
    }

    private bool HasTextInCurrentField()
    {
        return transmitPage == TransmitPage.Code ? typedConfirmCode.Length > 0 : typedMessage.Length > 0;
    }

    private void AddTokenToCurrentField(string token)
    {
        if (transmitPage == TransmitPage.Code)
        {
            if (typedConfirmCode.Length >= maxTypedCodeLength)
            {
                PlayErrorSfx();
                ShowEventMessage("CODE LIMIT", 0.8f);
                return;
            }

            typedConfirmCode += token;
            PlayCursorSfx();
            return;
        }

        if (typedMessage.Length >= maxTypedMessageLength)
        {
            PlayErrorSfx();
            ShowEventMessage("MESSAGE LIMIT", 0.8f);
            return;
        }

        typedMessage += token;
        PlayCursorSfx();
    }

    private void RemoveLetterFromCurrentField()
    {
        if (!HasTextInCurrentField())
        {
            PlayErrorSfx();
            ShowEventMessage("BUFFER EMPTY", 0.8f);
            return;
        }

        if (transmitPage == TransmitPage.Code)
        {
            typedConfirmCode = typedConfirmCode.Substring(0, typedConfirmCode.Length - 1);
            PlayCursorSfx();
            return;
        }

        typedMessage = typedMessage.Substring(0, typedMessage.Length - 1);
        PlayCursorSfx();
    }

    private void ClearCurrentField()
    {
        if (transmitPage == TransmitPage.Code)
        {
            typedConfirmCode = string.Empty;
            return;
        }

        typedMessage = string.Empty;
    }

    private void GoNextTransmitPage()
    {
        if (transmitPage == TransmitPage.Message)
        {
            if (string.IsNullOrWhiteSpace(typedMessage))
            {
                PlayErrorSfx();
                ShowEventMessage("MESSAGE EMPTY", 1f);
                return;
            }

            transmitPage = TransmitPage.Check;
            transmitKeyboardRow = 0;
            transmitKeyboardColumn = 0;
            PlayConfirmSfx();
            StartBlinkEffect();
            return;
        }

        if (transmitPage == TransmitPage.Code)
        {
            TrySendTypedMessage();
            return;
        }
    }

    private void LeaveTransmitView()
    {
        currentView = ReadyView.Menu;
        transmitPage = TransmitPage.Message;
        if (terminalControl != null)
        {
            terminalControl.BlockExitInput = false;
            terminalControl.BlockInteractExitInput = false;
        }
        RenderCurrentView();
        StartBlinkEffect();
    }

    private void TrySendTypedMessage()
    {
        if (messageTransmitter == null)
        {
            PlayErrorSfx();
            ShowEventMessage("NO TRANSMITTER CONNECTED", 1.2f);
            AddLog("TRANSMIT FAILED: NO TRANSMITTER");
            return;
        }

        if (string.IsNullOrWhiteSpace(typedMessage))
        {
            PlayErrorSfx();
            ShowEventMessage("MESSAGE EMPTY", 1f);
            return;
        }

        if (string.IsNullOrWhiteSpace(typedConfirmCode))
        {
            PlayErrorSfx();
            ShowEventMessage("CONFIRM CODE EMPTY", 1f);
            return;
        }

        if (!messageTransmitter.IsCodeCorrect(typedConfirmCode))
        {
            PlayErrorSfx();
            ShowEventMessage("CODE REJECTED", 1.2f);
            AddLog("TRANSMIT BLOCKED: BAD CODE");
            return;
        }

        if (!messageTransmitter.CanStartSend(out string reason))
        {
            PlayErrorSfx();
            ShowEventMessage(reason, 1.2f);
            AddLog("TRANSMIT BLOCKED: " + reason);
            return;
        }

        if (!messageTransmitter.TryStartSend(typedMessage, typedConfirmCode))
        {
            PlayErrorSfx();
            ShowEventMessage(messageTransmitter.LastStatus, 1.2f);
            AddLog("TRANSMIT BLOCKED: " + messageTransmitter.LastStatus);
            return;
        }

        transmitPage = TransmitPage.Sending;
        PlayConfirmSfx();
        ShowEventMessage("TRANSMITTING", 1.2f);
        SignalHistory.RecordOutgoing(typedMessage, GetWorldTimeString());
        AddLog("TRANSMIT STARTED");
    }

    private bool UpdateTransmitFinishedState(bool makeNoise)
    {
        if (transmitPage != TransmitPage.Sending || messageTransmitter == null || !messageTransmitter.LastSendFinished)
        {
            return false;
        }

        transmitPage = TransmitPage.Done;

        if (makeNoise)
        {
            PlayConfirmSfx();
            ShowEventMessage("ACK RECEIVED", 1.4f);
            RenderCurrentView();
            StartBlinkEffect();
        }

        return true;
    }
}
