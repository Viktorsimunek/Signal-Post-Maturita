using UnityEngine;

public static class PlayerProfile
{
    private const string PlayerNameKey = "player_name";
    private const string DefaultName = "OPERATOR";

    public static string PlayerName
    {
        get
        {
            string stored = PlayerPrefs.GetString(PlayerNameKey, DefaultName);
            return string.IsNullOrWhiteSpace(stored) ? DefaultName : stored;
        }
    }

    public static void SetPlayerName(string newName)
    {
        string finalName = string.IsNullOrWhiteSpace(newName) ? DefaultName : newName.Trim();
        PlayerPrefs.SetString(PlayerNameKey, finalName);
        PlayerPrefs.Save();
    }
}
