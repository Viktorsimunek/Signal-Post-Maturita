using System;
using System.Collections;
using TMPro;
using UnityEngine;

public static class Typewriter
{
    public static IEnumerator Write(TMP_Text label, string text, float delay,
        Action<char, int> onLetter = null, Func<bool> skip = null, string hint = null)
    {
        if (label == null) yield break;
        text = text ?? "";
        bool showHint = !string.IsNullOrEmpty(hint) && text.EndsWith(hint, StringComparison.Ordinal);
        int hintIndex = showHint ? text.LastIndexOf(hint, StringComparison.Ordinal) : -1;
        label.text = text;
        label.maxVisibleCharacters = 0;
        label.ForceMeshUpdate(true);
        int count = showHint ? text.Length : label.textInfo.characterCount;

        try
        {
            for (int i = 0; i < count; i++)
            {
                if (label == null) yield break;
                if (skip?.Invoke() == true) break;
                char letter;
                if (showHint)
                {
                    label.maxVisibleCharacters = int.MaxValue;
                    label.text = text.Substring(0, i + 1);
                    if (i + 1 < hintIndex) label.text += "\n\n" + hint;
                    letter = text[i];
                }
                else
                {
                    label.maxVisibleCharacters = i + 1;
                    letter = label.textInfo.characterInfo[i].character;
                }
                onLetter?.Invoke(letter, i);
                yield return new WaitForSecondsRealtime(Mathf.Max(0.001f, delay));
            }
        }
        finally
        {
            if (label != null) label.maxVisibleCharacters = int.MaxValue;
        }
        if (label == null) yield break;
        label.text = text;
        label.maxVisibleCharacters = int.MaxValue;
    }
}
