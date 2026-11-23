using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

public sealed class TextShuffle
{
    private readonly StringBuilder buffer = new StringBuilder(128);

    public string Build(string text, float progress, string characters)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (progress >= 1f || string.IsNullOrEmpty(characters)) return text;

        int revealed = Mathf.FloorToInt(text.Length * Mathf.Clamp01(progress));
        buffer.Clear();
        for (int i = 0; i < text.Length; i++)
        {
            char letter = text[i];
            bool keep = i < revealed || char.IsWhiteSpace(letter) || char.IsPunctuation(letter) || char.IsSymbol(letter);
            long seed = (long)Time.frameCount * 17 + i * 31L;
            buffer.Append(keep ? letter : characters[(int)(seed % characters.Length)]);
        }
        return buffer.ToString();
    }

    public IEnumerator Play(TMP_Text label, string text, float duration, string characters)
    {
        if (label == null) yield break;
        text = text ?? "";
        label.maxVisibleCharacters = int.MaxValue;
        for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
        {
            if (label == null) yield break;
            label.text = Build(text, time / duration, characters);
            yield return null;
        }
        if (label != null) label.text = text;
    }
}
