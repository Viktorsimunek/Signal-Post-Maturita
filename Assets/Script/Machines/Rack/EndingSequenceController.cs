using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        yield return BlackScreen.FadeTo(1f, fadeDuration);
        BlackScreen.FadeOutAfterNextSceneLoad(creditsRevealFadeDuration);

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

    private static class BlackScreen
    {
        private static BlackScreenRunner runner;
        private static CanvasGroup canvasGroup;
        private static float revealDuration;

        public static IEnumerator FadeTo(float targetAlpha, float duration)
        {
            EnsureCanvas();

            if (duration <= 0.0001f)
            {
                canvasGroup.alpha = targetAlpha;
                yield break;
            }

            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
        }

        public static void FadeOutAfterNextSceneLoad(float duration)
        {
            EnsureCanvas();
            revealDuration = Mathf.Max(0f, duration);
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void EnsureCanvas()
        {
            if (canvasGroup != null)
            {
                return;
            }

            GameObject root = new GameObject("EndingFadeCanvas");
            Object.DontDestroyOnLoad(root);
            runner = root.AddComponent<BlackScreenRunner>();

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            GameObject fadeImageObject = new GameObject("Fade");
            fadeImageObject.transform.SetParent(root.transform, false);

            RectTransform rectTransform = fadeImageObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            Image fadeImage = fadeImageObject.AddComponent<Image>();
            fadeImage.color = Color.black;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            EnsureCanvas();

            if (runner != null)
            {
                runner.StartCoroutine(FadeTo(0f, revealDuration));
            }
        }
    }

    private class BlackScreenRunner : MonoBehaviour
    {
    }
}
