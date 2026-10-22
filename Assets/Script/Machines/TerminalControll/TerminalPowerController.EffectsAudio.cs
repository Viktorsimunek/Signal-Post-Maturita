using System.Collections;
using UnityEngine;

public partial class TerminalPowerController
{
    private void StartBlinkEffect()
    {
        if (terminalCanvasGroup == null || powerState == PowerState.Off)
        {
            return;
        }

        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
        }

        blinkRoutine = StartCoroutine(BlinkRoutine());
    }

    private IEnumerator BlinkRoutine()
    {
        float duration = Mathf.Max(0.02f, blinkDuration);
        float fadeOutDuration = duration * 0.35f;
        float fadeInDuration = duration * 0.65f;
        float startAlpha = terminalCanvasGroup.alpha;

        yield return StartCoroutine(FadeCanvasGroupAlpha(startAlpha, blinkMinAlpha, fadeOutDuration));
        yield return StartCoroutine(FadeCanvasGroupAlpha(terminalCanvasGroup.alpha, 1f, fadeInDuration));

        terminalCanvasGroup.alpha = 1f;
        blinkRoutine = null;
    }

    private IEnumerator FadeCanvasGroupAlpha(float from, float to, float duration)
    {
        if (terminalCanvasGroup == null)
        {
            yield break;
        }

        if (duration <= 0.0001f)
        {
            terminalCanvasGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            terminalCanvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        terminalCanvasGroup.alpha = to;
    }

    private IEnumerator PlayScreenTransition()
    {
        if (screenTransitionBar == null)
        {
            yield return new WaitForSecondsRealtime(screenTransitionDuration * 0.5f);
            yield break;
        }

        screenTransitionBar.gameObject.SetActive(true);
        screenTransitionBar.SnapTo01(0f);
        screenTransitionBar.Set01(1f);

        float elapsed = 0f;
        while (elapsed < screenTransitionDuration)
        {
            if (screenTransitionBar.DisplayedNormalizedValue >= 0.999f)
            {
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        screenTransitionBar.SnapTo01(0f);
        screenTransitionBar.gameObject.SetActive(false);
    }

    private void PlayCursorSfx()
    {
        PlayUiOneShot(cursorBeep, cursorVolume, cursorPitchRange);
    }

    private void PlayConfirmSfx()
    {
        PlayUiOneShot(confirmBeep, confirmVolume, new Vector2(1f, 1f));
    }

    private void PlayErrorSfx()
    {
        AudioClip clip = errorBeep != null ? errorBeep : confirmBeep;
        PlayUiOneShot(clip, errorVolume, new Vector2(1f, 1f));
    }

    private void PlayStartupSound()
    {
        PlayUiOneShot(startupClip, startupVolume, startupPitchRange);
        PlayUiOneShot(startupNoiseClip, startupNoiseVolume, new Vector2(1f, 1f));
    }

    private void PlayShutdownSound()
    {
        PlayUiOneShot(shutdownClip, shutdownVolume, shutdownPitchRange);
    }

    private void PlayStaticBurst()
    {
        PlayUiOneShot(staticBurst, staticVolume, new Vector2(1f, 1f));
    }

    private void PlayUiOneShot(AudioClip clip, float volume, Vector2 pitchRange)
    {
        if (uiAudioSource == null || clip == null)
        {
            return;
        }

        uiAudioSource.pitch = UnityEngine.Random.Range(pitchRange.x, pitchRange.y);
        uiAudioSource.PlayOneShot(clip, volume);
    }

    private void StopAmbientImmediate()
    {
        if (ambientAudioSource == null)
        {
            return;
        }

        ambientAudioSource.Stop();
        ambientAudioSource.clip = ambientLoopClip;
        ambientAudioSource.volume = 0f;
    }

    private void FadeAmbientLoop(bool shouldPlay, float duration)
    {
        if (ambientAudioSource == null)
        {
            return;
        }

        if (ambientFadeRoutine != null)
        {
            StopCoroutine(ambientFadeRoutine);
        }

        ambientFadeRoutine = StartCoroutine(FadeAmbientLoopRoutine(shouldPlay, duration));
    }

    private IEnumerator FadeAmbientLoopRoutine(bool shouldPlay, float duration)
    {
        ambientAudioSource.clip = ambientLoopClip;

        if (shouldPlay)
        {
            if (ambientLoopClip == null)
            {
                ambientAudioSource.volume = 0f;
                ambientFadeRoutine = null;
                yield break;
            }

            if (!ambientAudioSource.isPlaying)
            {
                ambientAudioSource.volume = 0f;
                ambientAudioSource.Play();
            }
        }

        float startVolume = ambientAudioSource.volume;
        float targetVolume = shouldPlay ? ambientVolume : 0f;

        if (duration <= 0.0001f)
        {
            ambientAudioSource.volume = targetVolume;
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                ambientAudioSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
                yield return null;
            }

            ambientAudioSource.volume = targetVolume;
        }

        if (!shouldPlay && ambientAudioSource.isPlaying)
        {
            ambientAudioSource.Stop();
        }

        ambientFadeRoutine = null;
    }
}
