using System.Collections;
using UnityEngine;

public static class CanvasFade
{
    public static IEnumerator Run(CanvasGroup canvas, float from, float to, float duration, bool smooth = false)
    {
        if (canvas == null) yield break;
        yield return Fade.Value(from, to, duration, alpha => canvas.alpha = alpha,
            smooth, cancelled: () => canvas == null);
    }
}
