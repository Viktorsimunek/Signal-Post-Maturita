using System;
using System.Collections;
using UnityEngine;

public partial class MenuManager
{
    private void HandleIdleStatusMessage()
    {
        if (idleMessageShown)
        {
            return;
        }

        if (Time.unscaledTime - lastInputTime < idleDelaySeconds)
        {
            return;
        }

        idleMessageShown = true;
        ShowEventMessage("WAITING FOR OPERATOR INPUT", idleMessageDuration);
    }

    private void RegisterUserInput()
    {
        lastInputTime = Time.unscaledTime;
        idleMessageShown = false;
    }

    private void UpdateStatusText()
    {
        if (statusText == null || introPlaying)
        {
            return;
        }

        statusText.text =
            "SIGNAL STATUS: STABLE\n" +
            "NODE 04\n" +
            "TIME " + DateTime.Now.ToString("HH:mm");
    }

    private void StartBlinkEffect()
    {
        if (terminalCanvasGroup == null || introPlaying)
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
        float duration = 0.08f;
        terminalCanvasGroup.alpha = 0.6f;
        yield return new WaitForSecondsRealtime(duration);
        terminalCanvasGroup.alpha = 1f;
        blinkRoutine = null;
    }


    private void ShowEventMessage(string message, float clearAfter = 0f)
    {
        if (eventText == null)
        {
            return;
        }

        eventText.text = message;
        if (eventRoutine != null)
        {
            StopCoroutine(eventRoutine);
            eventRoutine = null;
        }

        if (clearAfter > 0f)
        {
            eventRoutine = StartCoroutine(ClearEventMessageAfterDelay(clearAfter));
        }
    }

    private IEnumerator ClearEventMessageAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (eventText != null)
        {
            eventText.text = string.Empty;
        }

        eventRoutine = null;
    }

    private void PlaySfx(AudioClip clip)
    {
        if (sfxSource == null || clip == null)
        {
            return;
        }

        sfxSource.pitch = clip == cursorBeep
            ? UnityEngine.Random.Range(Mathf.Min(cursorPitchMin, cursorPitchMax), Mathf.Max(cursorPitchMin, cursorPitchMax))
            : 1f;
        sfxSource.PlayOneShot(clip);
    }

    private void PlayErrorSfx()
    {
        PlaySfx(errorBeep != null ? errorBeep : confirmBeep);
    }

    private void StartMenuMusic()
    {
        if (menuMusicSource == null && menuMusic != null)
        {
            menuMusicSource = gameObject.AddComponent<AudioSource>();
        }

        if (menuMusicSource == null)
        {
            return;
        }

        if (menuMusicSource.clip == null)
        {
            menuMusicSource.clip = menuMusic;
        }

        if (menuMusicSource.clip == null)
        {
            return;
        }

        menuMusicSource.loop = true;
        menuMusicSource.playOnAwake = false;
        ApplyMenuMusicVolume();

        if (!menuMusicSource.isPlaying)
        {
            menuMusicSource.Play();
        }
    }

    private void ApplyMenuMusicVolume()
    {
        if (menuMusicSource == null)
        {
            return;
        }

        menuMusicSource.volume = GetMenuMusicTargetVolume();
    }

    private void FadeMenuMusic(bool audible)
    {
        if (menuMusicSource == null || menuMusicSource.clip == null)
        {
            return;
        }

        if (menuMusicFadeRoutine != null)
        {
            StopCoroutine(menuMusicFadeRoutine);
        }

        menuMusicFadeRoutine = StartCoroutine(FadeMenuMusicRoutine(audible));
    }

    private IEnumerator FadeMenuMusicRoutine(bool audible)
    {
        if (audible && !menuMusicSource.isPlaying)
        {
            menuMusicSource.Play();
        }

        float startVolume = menuMusicSource.volume;
        float targetVolume = audible ? GetMenuMusicTargetVolume() : 0f;
        float duration = Mathf.Max(0.01f, menuMusicFadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            menuMusicSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        menuMusicSource.volume = targetVolume;

        if (!audible)
        {
            menuMusicSource.Stop();
        }

        menuMusicFadeRoutine = null;
    }

    private float GetMenuMusicTargetVolume()
    {
        return Mathf.Clamp01(menuMusicVolume) * Mathf.Clamp01(GameSettings.MusicVolume);
    }
}
