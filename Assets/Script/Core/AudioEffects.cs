using System.Collections;
using UnityEngine;

public static class AudioEffects
{
    public static IEnumerator FadeVolume(AudioSource source, float target, float duration,
        bool smooth = false, bool unscaled = true)
    {
        if (source == null) yield break;
        yield return Fade.Value(source.volume, target, duration, volume => source.volume = volume,
            smooth, cancelled: () => source == null, unscaled: unscaled);
    }

    public static IEnumerator Crossfade(AudioSource previous, AudioSource next, float target, float duration)
    {
        if (next == null) yield break;
        float previousVolume = previous != null ? previous.volume : 0f;
        yield return Fade.Value(0f, 1f, duration, progress =>
        {
            if (previous != null) previous.volume = Mathf.Lerp(previousVolume, 0f, progress);
            next.volume = Mathf.Lerp(0f, target, progress);
        }, smooth: true, cancelled: () => next == null);
        if (previous != null) previous.Stop();
    }

    public static float ToDecibels(float volume)
    {
        return Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20f;
    }
}
