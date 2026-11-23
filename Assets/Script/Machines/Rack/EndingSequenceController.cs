using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class EndingSequenceController : MonoBehaviour
{
    [SerializeField] private FuseSwitch[] fuses;
    [SerializeField] private GameSoundtrackController soundtrackController;
    [SerializeField] private string creditsScene = "Credits";
    [SerializeField] private float fadeDuration = 3f;
    [SerializeField] private float musicFadeDuration = 3f;
    [SerializeField] private float creditsRevealFadeDuration = 1.5f;

    private Coroutine endingRoutine;

    public bool IsPlaying
    {
        get { return endingRoutine != null; }
    }

    [ContextMenu("Play Ending")]
    public void Play()
    {
        if (!isActiveAndEnabled || endingRoutine != null)
        {
            return;
        }

        endingRoutine = StartCoroutine(EndingRoutine());
    }

    public IEnumerator PlayRoutine()
    {
        if (endingRoutine == null)
        {
            endingRoutine = StartCoroutine(EndingRoutine());
        }

        while (endingRoutine != null)
        {
            yield return null;
        }
    }

    public void Stop()
    {
        if (endingRoutine != null)
        {
            StopCoroutine(endingRoutine);
            endingRoutine = null;
        }
    }

    private void OnValidate()
    {
        fadeDuration = Mathf.Max(0f, fadeDuration);
        musicFadeDuration = Mathf.Max(0f, musicFadeDuration);
        creditsRevealFadeDuration = Mathf.Max(0f, creditsRevealFadeDuration);
    }

    private IEnumerator EndingRoutine()
    {
        while (!AreFusesOff())
        {
            yield return null;
        }

        StopEndingMusic();
        yield return ScreenFade.To(1f, fadeDuration);
        ScreenFade.RevealOnNextScene(creditsRevealFadeDuration);

        endingRoutine = null;
        SceneManager.LoadScene(creditsScene);
    }

    private void StopEndingMusic()
    {
        GameSoundtrackController soundtrack = soundtrackController != null
            ? soundtrackController
            : FindFirstObjectByType<GameSoundtrackController>();

        if (soundtrack != null)
        {
            soundtrack.StopMusic(musicFadeDuration);
        }
    }

    private bool AreFusesOff()
    {
        FuseSwitch[] fusesToCheck = GetFuses();

        for (int i = 0; i < fusesToCheck.Length; i++)
        {
            if (fusesToCheck[i] != null && fusesToCheck[i].IsOn)
            {
                return false;
            }
        }

        return true;
    }

    private FuseSwitch[] GetFuses()
    {
        if (fuses != null && fuses.Length > 0)
        {
            return fuses;
        }

        fuses = FindObjectsByType<FuseSwitch>(FindObjectsSortMode.None);
        return fuses;
    }

}
