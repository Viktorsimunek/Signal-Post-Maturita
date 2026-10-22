using System.Collections;
using TMPro;
using UnityEngine;

public partial class MenuManager
{
    private static readonly string[][] NameKeyboardRows =
    {
        new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" },
        new[] { "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T" },
        new[] { "U", "V", "W", "X", "Y", "Z", "0", "1", "2", "3" },
        new[] { "4", "5", "6", "7", "8", "9", "-", ".", "_" },
        new[] { "SPACE", "BACK", "CLEAR", "OK" }
    };

    [Header("New Game")]
    [SerializeField, Min(3)] private int playerNameMaxLength = 12;
    [SerializeField, Range(0.2f, 3f)] private float newGameWelcomeDuration = 1.4f;
    [SerializeField, Range(0.2f, 4f)] private float newGameSceneTransferDuration = 1.45f;
    [SerializeField, Range(0.05f, 6f)] private float newGameSceneTransferFadeDuration = 3f;
    [SerializeField] private Camera transitionCaptureCamera;

    private int newGameConfirmIndex;
    private int newGameDifficultyIndex;
    private GameProgress.DifficultyMode pendingDifficulty = GameProgress.DifficultyMode.Hard;
    private string pendingPlayerName = string.Empty;
    private int nameKeyboardRow;
    private int nameKeyboardColumn;
    private Coroutine newGameRoutine;

    private void StartNewGameFlow()
    {
        if (newGameRoutine != null)
        {
            StopCoroutine(newGameRoutine);
        }

        newGameRoutine = StartCoroutine(NewGameFlowRoutine());
    }

    private IEnumerator NewGameFlowRoutine()
    {
        yield return StartCoroutine(StartScreenTransitionRoutine(TerminalScreen.NewGameConfirm, PrepareNewGameConfirm));
        newGameRoutine = null;
    }

    private void PrepareNewGameConfirm()
    {
        newGameConfirmIndex = 1;
        selectedIndex = 0;
        ShowEventMessage("NEW GAME WILL ERASE CURRENT PROFILE", 0f);
    }

    private void PrepareNewGameProfile()
    {
        GameProgress.BeginNewGame(pendingDifficulty);

        pendingPlayerName = string.Empty;
        nameKeyboardRow = 0;
        nameKeyboardColumn = 0;
        selectedIndex = 0;

        ShowEventMessage(
            pendingDifficulty == GameProgress.DifficultyMode.Easy
                ? "TYPE CALLSIGN  ENTER CONFIRM  BACKSPACE DELETE"
                : "ARROWS SELECT  ENTER CONFIRM  ESC DELETE",
            0f);
    }

    private void HandleNewGameConfirmInput()
    {
        int horizontalStep = GetHorizontalStep();
        int verticalStep = GetVerticalStep();
        int combinedStep = horizontalStep != 0 ? horizontalStep : verticalStep;

        if (combinedStep != 0)
        {
            newGameConfirmIndex = WrapIndex(newGameConfirmIndex + combinedStep, 2);
            RegisterUserInput();
            PlaySfx(cursorBeep);
            RenderCurrentMenuImmediate();
            return;
        }

        if (WasBackPressed())
        {
            RegisterUserInput();
            PlaySfx(cursorBeep);
            RequestOpenMainMenu();
            return;
        }

        if (!WasSubmitPressed())
        {
            return;
        }

        RegisterUserInput();
        PlaySfx(confirmBeep);

        if (newGameConfirmIndex == 0)
        {
            if (newGameRoutine != null)
            {
                StopCoroutine(newGameRoutine);
            }

            newGameRoutine = StartCoroutine(OpenNewGameDifficultyRoutine());
            return;
        }

        RequestOpenMainMenu();
    }

    private IEnumerator OpenNewGameDifficultyRoutine()
    {
        yield return StartCoroutine(StartScreenTransitionRoutine(TerminalScreen.NewGameDifficulty, PrepareNewGameDifficulty));
        newGameRoutine = null;
    }

    private void PrepareNewGameDifficulty()
    {
        newGameDifficultyIndex = 0;
        pendingDifficulty = GameProgress.DifficultyMode.Easy;
        selectedIndex = 0;
        ShowEventMessage("SELECT DIFFICULTY", 0f);
    }

    private void HandleNewGameDifficultyInput()
    {
        int horizontalStep = GetHorizontalStep();
        int verticalStep = GetVerticalStep();
        int combinedStep = horizontalStep != 0 ? horizontalStep : verticalStep;

        if (combinedStep != 0)
        {
            newGameDifficultyIndex = WrapIndex(newGameDifficultyIndex + combinedStep, 2);
            pendingDifficulty = newGameDifficultyIndex == 0
                ? GameProgress.DifficultyMode.Easy
                : GameProgress.DifficultyMode.Hard;
            RegisterUserInput();
            PlaySfx(cursorBeep);
            RenderCurrentMenuImmediate();
            return;
        }

        if (WasBackPressed())
        {
            RegisterUserInput();
            PlaySfx(cursorBeep);
            newGameRoutine = StartCoroutine(NewGameFlowRoutine());
            return;
        }

        if (!WasSubmitPressed())
        {
            return;
        }

        pendingDifficulty = newGameDifficultyIndex == 0
            ? GameProgress.DifficultyMode.Easy
            : GameProgress.DifficultyMode.Hard;
        RegisterUserInput();
        PlaySfx(confirmBeep);

        if (newGameRoutine != null)
        {
            StopCoroutine(newGameRoutine);
        }

        newGameRoutine = StartCoroutine(ConfirmNewGameRoutine());
    }

    private IEnumerator ConfirmNewGameRoutine()
    {
        yield return StartCoroutine(StartScreenTransitionRoutine(TerminalScreen.NewGameProfile, PrepareNewGameProfile));
        newGameRoutine = null;
    }

    private void HandleNewGameProfileInput()
    {
        if (pendingDifficulty == GameProgress.DifficultyMode.Easy && HandleTypedPlayerNameInput())
        {
            return;
        }

        int verticalStep = GetVerticalStep();
        int horizontalStep = GetHorizontalStep();

        if (verticalStep != 0 || horizontalStep != 0)
        {
            MoveNameKeyboardSelection(verticalStep, horizontalStep);
            RegisterUserInput();
            PlaySfx(cursorBeep);
            RenderCurrentMenuImmediate();
            return;
        }

        if (WasBackPressed())
        {
            RegisterUserInput();
            DeletePlayerNameCharacter();
            return;
        }

        if (!WasSubmitPressed())
        {
            return;
        }

        RegisterUserInput();
        PlaySfx(confirmBeep);
        ActivateNameKeyboardToken();
    }

    private bool HandleTypedPlayerNameInput()
    {
        if (KeyboardTextInput.WasDeletePressed())
        {
            DeletePlayerNameCharacter();
            RegisterUserInput();
            return true;
        }

        if (KeyboardTextInput.WasEnterPressed())
        {
            ConfirmPlayerName();
            RegisterUserInput();
            return true;
        }

        if (KeyboardTextInput.TryReadAllowedCharacter(true, true, out string token))
        {
            AppendPlayerNameCharacter(token);
            RegisterUserInput();
            return true;
        }

        return false;
    }

    private void MoveNameKeyboardSelection(int verticalStep, int horizontalStep)
    {
        if (verticalStep != 0)
        {
            nameKeyboardRow = WrapIndex(nameKeyboardRow + verticalStep, NameKeyboardRows.Length);
            nameKeyboardColumn = Mathf.Clamp(nameKeyboardColumn, 0, NameKeyboardRows[nameKeyboardRow].Length - 1);
        }

        if (horizontalStep != 0)
        {
            nameKeyboardColumn = WrapIndex(nameKeyboardColumn + horizontalStep, NameKeyboardRows[nameKeyboardRow].Length);
        }
    }

    private void ActivateNameKeyboardToken()
    {
        string token = NameKeyboardRows[nameKeyboardRow][nameKeyboardColumn];

        switch (token)
        {
            case "SPACE":
                AppendPlayerNameCharacter(" ");
                return;
            case "BACK":
                if (pendingPlayerName.Length > 0)
                {
                    pendingPlayerName = pendingPlayerName.Substring(0, pendingPlayerName.Length - 1);
                    ShowEventMessage("CHARACTER DELETED", 0.8f);
                    RenderCurrentMenuImmediate();
                }
                else
                {
                    PlayErrorSfx();
                    ShowEventMessage("BUFFER EMPTY", 0.8f);
                }
                return;
            case "CLEAR":
                pendingPlayerName = string.Empty;
                ShowEventMessage("BUFFER CLEARED", 0.8f);
                RenderCurrentMenuImmediate();
                return;
            case "OK":
                ConfirmPlayerName();
                return;
            default:
                AppendPlayerNameCharacter(token);
                return;
        }
    }

    private void AppendPlayerNameCharacter(string token)
    {
        if (pendingPlayerName.Length >= playerNameMaxLength)
        {
            PlayErrorSfx();
            ShowEventMessage("CALLSIGN LIMIT " + playerNameMaxLength, 0.8f);
            return;
        }

        string candidate = pendingPlayerName + token;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            PlayErrorSfx();
            ShowEventMessage("CALLSIGN REQUIRED", 0.8f);
            return;
        }

        pendingPlayerName = candidate.ToUpperInvariant();
        RenderCurrentMenuImmediate();
    }

    private void DeletePlayerNameCharacter()
    {
        if (pendingPlayerName.Length > 0)
        {
            pendingPlayerName = pendingPlayerName.Substring(0, pendingPlayerName.Length - 1);
            PlaySfx(cursorBeep);
            ShowEventMessage("CHARACTER DELETED", 0.8f);
            RenderCurrentMenuImmediate();
        }
        else
        {
            PlayErrorSfx();
            ShowEventMessage("CALLSIGN REQUIRED", 0.8f);
        }
    }

    private void ConfirmPlayerName()
    {
        string finalName = pendingPlayerName.Trim();
        if (string.IsNullOrWhiteSpace(finalName))
        {
            PlayErrorSfx();
            ShowEventMessage("ENTER CALLSIGN BEFORE PROCEEDING", 1f);
            return;
        }

        PlayerProfile.SetPlayerName(finalName);
        ShowEventMessage("WELCOME " + finalName + "!", newGameWelcomeDuration);
        RenderCurrentMenuImmediate();

        if (newGameRoutine != null)
        {
            StopCoroutine(newGameRoutine);
        }

        newGameRoutine = StartCoroutine(FinishNewGameRoutine(finalName));
    }

    private IEnumerator FinishNewGameRoutine(string playerName)
    {
        currentScreen = TerminalScreen.Loading;
        ShowEventMessage(string.Empty);
        TMP_Text welcomeText = ShowCenteredTransferText("WELCOME " + playerName + "!");

        SmoothSceneTransition.BeginTerminalTransfer(
            this,
            newGameScene,
            welcomeText,
            transitionCaptureCamera,
            newGameSceneTransferDuration,
            newGameSceneTransferFadeDuration,
            targetMixer,
            masterVolumeParam,
            audioFadeDuration);

        yield return null;
        newGameRoutine = null;
    }

    private string BuildNewGameProfileText()
    {
        string text =
            "NEW OPERATOR PROFILE\n\n" +
            "DIFFICULTY: " + pendingDifficulty.ToString().ToUpperInvariant() + "\n" +
            "ARCHIVE PURGED\n" +
            "ENTER CALLSIGN:\n\n" +
            "> " + GetDisplayedPlayerNameBuffer() + "\n\n" +
            "VIRTUAL KEYBOARD\n";

        for (int row = 0; row < NameKeyboardRows.Length; row++)
        {
            text += BuildKeyboardRow(row) + "\n";
        }

        text += "\nCONFIRM TO DEPLOY";
        return text;
    }

    private string BuildNewGameConfirmText()
    {
        return
            "NEW GAME INITIALIZATION\n\n" +
            "WARNING:\n" +
            "CURRENT OPERATOR DATA WILL BE ERASED.\n\n" +
            "PROCEED WITH NEW GAME?\n\n" +
            BuildConfirmOption("YES", newGameConfirmIndex == 0) + "\n" +
            BuildConfirmOption("NO", newGameConfirmIndex == 1) + "\n\n" +
            "SELECT OPTION";
    }

    private string BuildNewGameDifficultyText()
    {
        return
            "SELECT DIFFICULTY\n\n" +
            BuildConfirmOption("EASY  - PHYSICAL KEYBOARD ENABLED", newGameDifficultyIndex == 0) + "\n" +
            BuildConfirmOption("HARD  - TERMINAL KEYBOARD ONLY", newGameDifficultyIndex == 1) + "\n\n" +
            "CHOOSE OPERATOR MODE";
    }

    private static string BuildConfirmOption(string label, bool selected)
    {
        return selected ? "> " + label : "  " + label;
    }

    private string BuildKeyboardRow(int row)
    {
        string rowText = "";
        string[] tokens = NameKeyboardRows[row];

        for (int column = 0; column < tokens.Length; column++)
        {
            if (column > 0)
            {
                rowText += " ";
            }

            bool isSelected = row == nameKeyboardRow && column == nameKeyboardColumn;
            string token = tokens[column];

            if (isSelected)
            {
                rowText += "[" + token + "]";
            }
            else
            {
                rowText += " " + token + " ";
            }
        }

        return rowText;
    }

    private string GetDisplayedPlayerNameBuffer()
    {
        string buffer = pendingPlayerName;
        if (buffer.Length == 0)
        {
            buffer = "_";
        }
        else if (buffer.Length < playerNameMaxLength)
        {
            buffer += "_";
        }

        return buffer;
    }

    private static int WrapIndex(int value, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        return (value % count + count) % count;
    }
}
