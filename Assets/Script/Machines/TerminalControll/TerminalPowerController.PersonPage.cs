using System;
using System.Collections;
using UnityEngine;

public partial class TerminalPowerController
{
    [Serializable]
    private class PeopleDatabase
    {
        public PersonRecord[] people;
    }

    [Serializable]
    private class PersonRecord
    {
        public string code;
        public string fullName;
        public string dateOfBirth;
        public string nationality;
        public string documentId;
        public bool exitAllowed;
        public string exitStatus;
        public string note;
    }

    private string BuildPersonDatabaseText()
    {
        if (personLookupCompleted && personLookupResultPageOpen)
        {
            return BuildPersonLookupResultPage();
        }

        string text =
            "PERSON DATABASE\n\n" +
            "PERSON CODE:\n" +
            "> " + (typedPersonCode.Length > 0 ? typedPersonCode : "_") + "\n\n";

        if (personLookupInProgress)
        {
            float progress = Mathf.Clamp01((Time.unscaledTime - personLookupStartedAt) / Mathf.Max(0.1f, peopleDatabaseLookupDuration));
            text +=
                "DATABASE LINK ACTIVE\n" +
                "PULLING RECORD " + BuildProgressBar(progress) + " " + Mathf.RoundToInt(progress * 100f).ToString("000") + "%\n\n";
        }
        else if (personLookupCompleted)
        {
            text += personLookupMessage + "\n";
            text += "OPEN RESULT: ENTER\n\n";
        }
        else
        {
            text += personLookupMessage + "\n\n";
        }

        text += "VIRTUAL KEYS\n";
        for (int row = 0; row < PersonLookupKeyboardRows.Length; row++)
        {
            text += BuildPersonLookupKeyboardRow(row) + "\n";
        }

        return text.TrimEnd();
    }

    private string BuildPersonLookupResultPage()
    {
        if (currentPersonRecord == null)
        {
            return
                "PERSON DATABASE RESULT\n\n" +
                "SEARCHED CODE\n" +
                "> " + SafeField(typedPersonCode).ToUpperInvariant() + "\n\n" +
                "NO MATCHING RECORD\n\n" +
                personLookupMessage;
        }

        string exitStatus = !string.IsNullOrWhiteSpace(currentPersonRecord.exitStatus)
            ? currentPersonRecord.exitStatus
            : currentPersonRecord.exitAllowed ? "EXIT PERMIT VALID" : "EXIT DENIED";

        return
            "PERSON RECORD\n" +
            "================================\n" +
            "CODE\n" +
            SafeField(currentPersonRecord.code).ToUpperInvariant() + "\n\n" +
            "IDENTITY\n" +
            "NAME        " + SafeField(currentPersonRecord.fullName) + "\n" +
            "BIRTH       " + SafeField(currentPersonRecord.dateOfBirth) + "\n" +
            "NATION      " + SafeField(currentPersonRecord.nationality) + "\n" +
            "DOCUMENT    " + SafeField(currentPersonRecord.documentId) + "\n\n" +
            "EXIT CONTROL\n" +
            "CLEARANCE   " + (currentPersonRecord.exitAllowed ? "ALLOWED" : "NOT ALLOWED") + "\n" +
            "STATUS      " + exitStatus + "\n\n" +
            "NOTE\n" +
            SafeField(currentPersonRecord.note);
    }

    private string BuildPersonLookupKeyboardRow(int row)
    {
        string result = string.Empty;
        string[] tokens = PersonLookupKeyboardRows[row];

        for (int column = 0; column < tokens.Length; column++)
        {
            if (column > 0)
            {
                result += " ";
            }

            bool selected = row == personKeyboardRow && column == personKeyboardColumn;
            result += selected ? "[" + tokens[column] + "]" : " " + tokens[column] + " ";
        }

        return result;
    }

    private void HandlePersonDatabaseInput()
    {
        if (personLookupResultPageOpen)
        {
            if (WasBackPressed() || WasSubmitPressed())
            {
                personLookupResultPageOpen = false;
                personLookupCompleted = false;
                currentPersonRecord = null;
                personLookupMessage = typedPersonCode.Length > 0 ? "READY TO SEARCH" : "ENTER PERSON CODE";
                PlayConfirmSfx();
                RenderCurrentView();
                StartBlinkEffect();
            }

            return;
        }

        if (personLookupInProgress)
        {
            if (WasBackPressed())
            {
                StopPersonLookup();
                PlayConfirmSfx();
                ShowEventMessage("LOOKUP CANCELED", 0.8f);
                RenderCurrentView();
                StartBlinkEffect();
            }

            return;
        }

        if (GameProgress.IsEasyDifficulty && HandleTypedPersonLookupInput())
        {
            RenderCurrentView();
            return;
        }

        int verticalStep = GetVerticalStep();
        int horizontalStep = GetHorizontalStep();

        if (verticalStep != 0 || horizontalStep != 0)
        {
            MovePersonLookupKeyboardSelection(verticalStep, horizontalStep);
            PlayCursorSfx();
            RenderCurrentView();
            StartBlinkEffect();
            return;
        }

        if (WasBackPressed())
        {
            if (typedPersonCode.Length > 0)
            {
                typedPersonCode = typedPersonCode.Substring(0, typedPersonCode.Length - 1);
                personLookupCompleted = false;
                personLookupResultPageOpen = false;
                currentPersonRecord = null;
                personLookupMessage = "ENTER PERSON CODE";
                PlayCursorSfx();
                RenderCurrentView();
                return;
            }

            PlayConfirmSfx();
            currentView = ReadyView.Menu;
            RenderCurrentView();
            StartBlinkEffect();
            return;
        }

        if (WasSubmitPressed())
        {
            if (personLookupCompleted)
            {
                personLookupResultPageOpen = true;
                PlayConfirmSfx();
                RenderCurrentView();
                StartBlinkEffect();
                return;
            }

            ActivatePersonLookupKeyboardToken();
            RenderCurrentView();
        }
    }

    private bool HandleTypedPersonLookupInput()
    {
        if (KeyboardTextInput.WasEnterPressed())
        {
            if (personLookupCompleted)
            {
                personLookupResultPageOpen = true;
                PlayConfirmSfx();
                StartBlinkEffect();
                return true;
            }

            StartPersonLookup();
            return true;
        }

        if (KeyboardTextInput.TryReadAllowedCharacter(false, false, out string token))
        {
            AddTokenToPersonCode(token);
            return true;
        }

        return false;
    }

    private void MovePersonLookupKeyboardSelection(int verticalStep, int horizontalStep)
    {
        personKeyboardRow = WrapIndex(personKeyboardRow + verticalStep, PersonLookupKeyboardRows.Length);
        personKeyboardColumn = WrapIndex(personKeyboardColumn + horizontalStep, PersonLookupKeyboardRows[personKeyboardRow].Length);
    }

    private void ActivatePersonLookupKeyboardToken()
    {
        string token = PersonLookupKeyboardRows[personKeyboardRow][personKeyboardColumn];

        switch (token)
        {
            case "BACK":
                if (typedPersonCode.Length <= 0)
                {
                    PlayErrorSfx();
                    ShowEventMessage("CODE EMPTY", 0.8f);
                    return;
                }

                typedPersonCode = typedPersonCode.Substring(0, typedPersonCode.Length - 1);
                personLookupCompleted = false;
                personLookupResultPageOpen = false;
                currentPersonRecord = null;
                personLookupMessage = "ENTER PERSON CODE";
                PlayCursorSfx();
                return;
            case "CLEAR":
                typedPersonCode = string.Empty;
                personLookupCompleted = false;
                personLookupResultPageOpen = false;
                currentPersonRecord = null;
                personLookupMessage = "ENTER PERSON CODE";
                PlayConfirmSfx();
                return;
            case "SEARCH":
                StartPersonLookup();
                return;
            default:
                AddTokenToPersonCode(token);
                return;
        }
    }

    private void AddTokenToPersonCode(string token)
    {
        if (typedPersonCode.Length >= maxPersonCodeLength)
        {
            PlayErrorSfx();
            ShowEventMessage("CODE LIMIT", 0.8f);
            return;
        }

        typedPersonCode += token;
        personLookupCompleted = false;
        personLookupResultPageOpen = false;
        currentPersonRecord = null;
        personLookupMessage = "READY TO SEARCH";
        PlayCursorSfx();
    }

    private void StartPersonLookup()
    {
        if (string.IsNullOrWhiteSpace(typedPersonCode))
        {
            PlayErrorSfx();
            ShowEventMessage("CODE EMPTY", 0.8f);
            return;
        }

        StopPersonLookup();
        personLookupRoutine = StartCoroutine(PersonLookupRoutine(typedPersonCode.Trim()));
        PlayConfirmSfx();
        AddLog("PERSON LOOKUP STARTED: " + typedPersonCode.Trim().ToUpperInvariant());
    }

    private IEnumerator PersonLookupRoutine(string code)
    {
        personLookupInProgress = true;
        personLookupCompleted = false;
        personLookupResultPageOpen = false;
        currentPersonRecord = null;
        personLookupMessage = "SEARCHING DATABASE";
        personLookupStartedAt = Time.unscaledTime;
        RenderCurrentView();

        yield return new WaitForSecondsRealtime(peopleDatabaseLookupDuration);

        currentPersonRecord = FindPersonRecord(code);
        personLookupInProgress = false;
        personLookupCompleted = true;
        personLookupResultPageOpen = true;
        personLookupRoutine = null;

        if (currentPersonRecord == null)
        {
            personLookupMessage = "NO RECORD FOUND FOR CODE " + code.ToUpperInvariant();
            PlayErrorSfx();
            AddLog("PERSON LOOKUP FAILED: " + code.ToUpperInvariant());
        }
        else
        {
            personLookupMessage = "RECORD FOUND";
            PlayConfirmSfx();
            AddLog("PERSON LOOKUP FOUND: " + SafeField(currentPersonRecord.fullName).ToUpperInvariant());
        }

        RenderCurrentView();
        StartBlinkEffect();
    }

    private void StopPersonLookup()
    {
        if (personLookupRoutine != null)
        {
            StopCoroutine(personLookupRoutine);
            personLookupRoutine = null;
        }

        personLookupInProgress = false;
        personLookupResultPageOpen = false;
    }

    private PersonRecord FindPersonRecord(string code)
    {
        TextAsset json = peopleDatabaseJson != null ? peopleDatabaseJson : Resources.Load<TextAsset>(peopleDatabaseResourcesName);
        if (json == null)
        {
            personLookupMessage = "DATABASE FILE MISSING: " + peopleDatabaseResourcesName;
            return null;
        }

        PeopleDatabase database = JsonUtility.FromJson<PeopleDatabase>(json.text);
        if (database == null || database.people == null)
        {
            personLookupMessage = "DATABASE READ ERROR";
            return null;
        }

        for (int i = 0; i < database.people.Length; i++)
        {
            PersonRecord person = database.people[i];
            if (person != null && string.Equals(person.code, code, StringComparison.OrdinalIgnoreCase))
            {
                return person;
            }
        }

        return null;
    }
}
