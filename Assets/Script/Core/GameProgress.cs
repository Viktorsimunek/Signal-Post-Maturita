using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameProgress
{
    public enum DifficultyMode
    {
        Easy,
        Hard
    }

    private const string HasSaveKey = "progress_has_save";
    private const string DifficultyKey = "progress_difficulty";
    private const string TransmissionIndexKey = "progress_transmission_index";
    private const string TransmissionCompletedKey = "progress_transmission_completed";
    private const string CompletedTransmissionObjectiveIdsKey = "progress_completed_transmission_objective_ids";
    private const string SentFaxIdsKey = "progress_sent_fax_ids";
    private const string BoardFaxIdsKey = "progress_board_fax_ids";
    private const char ListSeparator = '|';

    public static bool HasSave => PlayerPrefs.GetInt(HasSaveKey, 0) == 1;
    public static DifficultyMode Difficulty => (DifficultyMode)Mathf.Clamp(
        PlayerPrefs.GetInt(DifficultyKey, (int)DifficultyMode.Hard),
        (int)DifficultyMode.Easy,
        (int)DifficultyMode.Hard);
    public static bool IsEasyDifficulty => Difficulty == DifficultyMode.Easy;
    public static int TransmissionIndex => Mathf.Max(0, PlayerPrefs.GetInt(TransmissionIndexKey, 0));
    public static bool TransmissionCompleted => PlayerPrefs.GetInt(TransmissionCompletedKey, 0) == 1;

    public static void BeginNewGame()
    {
        BeginNewGame(DifficultyMode.Hard);
    }

    public static void BeginNewGame(DifficultyMode difficulty)
    {
        PlayerPrefs.SetInt(HasSaveKey, 1);
        PlayerPrefs.SetInt(DifficultyKey, (int)difficulty);
        PlayerPrefs.SetInt(TransmissionIndexKey, 0);
        PlayerPrefs.SetInt(TransmissionCompletedKey, 0);
        PlayerPrefs.DeleteKey(CompletedTransmissionObjectiveIdsKey);
        PlayerPrefs.DeleteKey(SentFaxIdsKey);
        PlayerPrefs.DeleteKey(BoardFaxIdsKey);
        PlayerPrefs.Save();
    }

    public static void SaveTransmissionProgress(int objectiveIndex, bool completed)
    {
        PlayerPrefs.SetInt(HasSaveKey, 1);
        PlayerPrefs.SetInt(TransmissionIndexKey, Mathf.Max(0, objectiveIndex));
        PlayerPrefs.SetInt(TransmissionCompletedKey, completed ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static bool IsTransmissionObjectiveCompleted(string objectiveId)
    {
        if (string.IsNullOrWhiteSpace(objectiveId))
        {
            return false;
        }

        return LoadIdSet(CompletedTransmissionObjectiveIdsKey).Contains(objectiveId);
    }

    public static HashSet<string> LoadCompletedTransmissionObjectiveIds()
    {
        return LoadIdSet(CompletedTransmissionObjectiveIdsKey);
    }

    public static void MarkTransmissionObjectiveCompleted(string objectiveId)
    {
        SaveId(CompletedTransmissionObjectiveIdsKey, objectiveId);
    }

    public static HashSet<string> LoadSentFaxIds()
    {
        return LoadIdSet(SentFaxIdsKey);
    }

    public static void MarkFaxSent(string faxId)
    {
        SaveId(SentFaxIdsKey, faxId);
    }

    public static List<string> LoadBoardFaxIds()
    {
        string stored = PlayerPrefs.GetString(BoardFaxIdsKey, string.Empty);
        List<string> ids = new List<string>();
        if (string.IsNullOrWhiteSpace(stored))
        {
            return ids;
        }

        string[] split = stored.Split(ListSeparator);
        for (int i = 0; i < split.Length; i++)
        {
            string id = split[i];
            if (!string.IsNullOrWhiteSpace(id) && !ids.Contains(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    public static void SaveBoardFaxIds(IEnumerable<string> faxIds)
    {
        PlayerPrefs.SetInt(HasSaveKey, 1);
        PlayerPrefs.SetString(BoardFaxIdsKey, JoinIds(faxIds));
        PlayerPrefs.Save();
    }

    private static void SaveId(string key, string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        HashSet<string> ids = LoadIdSet(key);
        if (!ids.Add(id))
        {
            return;
        }

        PlayerPrefs.SetInt(HasSaveKey, 1);
        PlayerPrefs.SetString(key, JoinIds(ids));
        PlayerPrefs.Save();
    }

    private static HashSet<string> LoadIdSet(string key)
    {
        HashSet<string> ids = new HashSet<string>();
        string stored = PlayerPrefs.GetString(key, string.Empty);
        if (string.IsNullOrWhiteSpace(stored))
        {
            return ids;
        }

        string[] split = stored.Split(ListSeparator);
        for (int i = 0; i < split.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(split[i]))
            {
                ids.Add(split[i]);
            }
        }

        return ids;
    }

    private static string JoinIds(IEnumerable<string> ids)
    {
        List<string> cleanIds = new List<string>();
        foreach (string id in ids)
        {
            if (!string.IsNullOrWhiteSpace(id) && !cleanIds.Contains(id))
            {
                cleanIds.Add(id);
            }
        }

        return string.Join(ListSeparator.ToString(), cleanIds);
    }
}
