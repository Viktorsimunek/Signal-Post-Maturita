using System;
using System.Collections;
using UnityEngine;

public static class Fade
{
    public static IEnumerator Value(float from, float to, float duration, Action<float> apply,
        bool smooth = false, Func<bool> cancelled = null, bool unscaled = true)
    {
        if (cancelled?.Invoke() == true) yield break;
        if (duration <= 0.0001f)
        {
            apply(to);
            yield break;
        }

        apply(from);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (cancelled?.Invoke() == true) yield break;
            elapsed += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            if (smooth) progress = Mathf.SmoothStep(0f, 1f, progress);
            apply(Mathf.Lerp(from, to, progress));
            yield return null;
        }
        if (cancelled?.Invoke() != true) apply(to);
    }
}
