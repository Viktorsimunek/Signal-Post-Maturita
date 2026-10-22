using System;
using System.Collections.Generic;
using UnityEngine;

public static class SignalHistory
{
    public struct Entry
    {
        public Entry(string direction, string text, string timestamp)
        {
            Direction = direction;
            Text = text;
            Timestamp = timestamp;
        }

        public string Direction { get; }
        public string Text { get; }
        public string Timestamp { get; }
    }

    private const int MaxEntries = 32;
    private static readonly List<Entry> entries = new List<Entry>();

    public static IReadOnlyList<Entry> Entries => entries;
    public static event Action Changed;

    public static void RecordIncoming(string text)
    {
        Add("RECEIVED", text);
    }

    public static void RecordOutgoing(string text)
    {
        Add("SENT", text);
    }

    public static void RecordOutgoing(string text, string timestamp)
    {
        Add("SENT", text, timestamp);
    }

    private static void Add(string direction, string text)
    {
        Add(direction, text, GetWorldTimeString());
    }

    private static void Add(string direction, string text, string timestamp)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        entries.Add(new Entry(direction, text.Trim(), string.IsNullOrWhiteSpace(timestamp) ? "--:--:--" : timestamp));

        while (entries.Count > MaxEntries)
        {
            entries.RemoveAt(0);
        }

        Changed?.Invoke();
    }

    private static string GetWorldTimeString()
    {
        TimeOfDayController timeOfDayController = UnityEngine.Object.FindFirstObjectByType<TimeOfDayController>();
        if (timeOfDayController == null)
        {
            return "--:--:--";
        }

        float totalSeconds = Mathf.Repeat(timeOfDayController.Time01, 1f) * 86400f;
        int hours = Mathf.FloorToInt(totalSeconds / 3600f) % 24;
        int minutes = Mathf.FloorToInt(totalSeconds / 60f) % 60;
        int seconds = Mathf.FloorToInt(totalSeconds) % 60;
        return hours.ToString("00") + ":" + minutes.ToString("00") + ":" + seconds.ToString("00");
    }
}
