using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class SmoothSceneTransition : MonoBehaviour
{
    private const float DefaultWelcomeHoldDuration = 1.1f;
    private const float DefaultFadeDuration = 3f;

    private Texture2D capturedFrameTexture;
    private AsyncOperation preloadedScene;
    private string preloadedSceneName;
    private Coroutine preloadRoutine;
    private bool transitionRunning;

    public static void PreloadScene(string targetScene)
    {
        if (string.IsNullOrWhiteSpace(targetScene))
        {
            return;
        }

        SmoothSceneTransition runner = GetOrCreateRunner();
        runner.StartPreload(targetScene);
    }

    public static void BeginTerminalTransfer(
        MonoBehaviour owner,
        string targetScene,
        TMP_Text sourceText,
        Camera captureCamera,
        float welcomeHoldDuration = DefaultWelcomeHoldDuration,
        float fadeDuration = DefaultFadeDuration,
        AudioMixer audioMixer = null,
        string audioVolumeParam = null,
        float audioFadeDuration = 0.75f)
    {
        if (owner == null || string.IsNullOrWhiteSpace(targetScene))
        {
            return;
        }

        SmoothSceneTransition runner = GetOrCreateRunner();
        runner.StartCoroutine(runner.TerminalTransferRoutine(
            targetScene,
            sourceText,
            captureCamera,
            Mathf.Max(0.05f, welcomeHoldDuration),
            Mathf.Max(0.01f, fadeDuration),
            audioMixer,
            audioVolumeParam,
            Mathf.Max(0f, audioFadeDuration)));
    }

    private static SmoothSceneTransition GetOrCreateRunner()
    {
        SmoothSceneTransition[] existingTransitions = FindObjectsByType<SmoothSceneTransition>(FindObjectsSortMode.None);
        for (int i = 0; i < existingTransitions.Length; i++)
        {
            SmoothSceneTransition existing = existingTransitions[i];
            if (existing != null && existing.gameObject.name == "Smooth Scene Transition")
            {
                return existing;
            }
        }

        GameObject root = new GameObject("Smooth Scene Transition");
        DontDestroyOnLoad(root);
        return root.AddComponent<SmoothSceneTransition>();
    }

    private void StartPreload(string targetScene)
    {
        if (transitionRunning)
        {
            return;
        }

        if (preloadedScene != null && preloadedSceneName == targetScene)
        {
            return;
        }

        if (preloadRoutine != null)
        {
            return;
        }

        preloadRoutine = StartCoroutine(PreloadRoutine(targetScene));
    }

    private IEnumerator PreloadRoutine(string targetScene)
    {
        ThreadPriority previousPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = ThreadPriority.Low;

        preloadedSceneName = targetScene;
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);
        preloadedScene = loadOperation;
        if (loadOperation == null)
        {
            Application.backgroundLoadingPriority = previousPriority;
            preloadRoutine = null;
            yield break;
        }

        loadOperation.priority = 0;
        loadOperation.allowSceneActivation = false;

        while (loadOperation.progress < 0.9f)
        {
            yield return null;
        }

        Application.backgroundLoadingPriority = previousPriority;
        preloadRoutine = null;
    }

    private IEnumerator TerminalTransferRoutine(
        string targetScene,
        TMP_Text sourceText,
        Camera captureCamera,
        float welcomeHoldDuration,
        float fadeDuration,
        AudioMixer audioMixer,
        string audioVolumeParam,
        float audioFadeDuration)
    {
        if (transitionRunning)
        {
            yield break;
        }

        transitionRunning = true;

        yield return new WaitForEndOfFrame();

        string welcomeText = sourceText != null ? sourceText.text : "WELCOME " + PlayerProfile.PlayerName + "!";
        capturedFrameTexture = CaptureCamera(captureCamera);

        float restoreVolume = GameSettings.MasterVolume;
        float quietVolume = Mathf.Min(restoreVolume, 0.08f);
        if (audioMixer != null && !string.IsNullOrWhiteSpace(audioVolumeParam))
        {
            StartCoroutine(FadeMixerVolume(audioMixer, audioVolumeParam, restoreVolume, quietVolume, audioFadeDuration));
        }

        ThreadPriority previousLoadingPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = ThreadPriority.Low;

        AsyncOperation loadOperation = GetSceneLoadOperation(targetScene);
        if (loadOperation == null)
        {
            Application.backgroundLoadingPriority = previousLoadingPriority;
            SceneManager.LoadScene(targetScene);
            yield break;
        }

        while (loadOperation.progress < 0.9f)
        {
            yield return null;
        }

        loadOperation.allowSceneActivation = true;

        while (!loadOperation.isDone)
        {
            yield return null;
        }

        Application.backgroundLoadingPriority = previousLoadingPriority;

        if (audioMixer != null && !string.IsNullOrWhiteSpace(audioVolumeParam))
        {
            audioMixer.SetFloat(audioVolumeParam, LinearToDecibels(quietVolume));
        }

        Scene loadedScene = SceneManager.GetSceneByName(targetScene);
        yield return new WaitForEndOfFrame();

        MainScenePlayerBootstrap.EnsureMainScenePlayer();

        TerminalPowerController terminalPower = FindSceneComponent<TerminalPowerController>(loadedScene);
        TerminalControl terminalControl = terminalPower != null
            ? terminalPower.GetComponent<TerminalControl>()
            : FindSceneComponent<TerminalControl>(loadedScene);
        FirstPersonController playerController = FindSceneComponent<FirstPersonController>(loadedScene);

        if (terminalControl != null && playerController != null)
        {
            terminalControl.ForceFocusAtTerminal(playerController);
        }

        if (terminalPower != null)
        {
            terminalPower.PlaySceneScreenshotHandoff(capturedFrameTexture, welcomeText, welcomeHoldDuration, fadeDuration);
        }

        if (audioMixer != null && !string.IsNullOrWhiteSpace(audioVolumeParam))
        {
            StartCoroutine(FadeMixerVolume(audioMixer, audioVolumeParam, quietVolume, restoreVolume, fadeDuration));
        }

        yield return new WaitForSecondsRealtime(welcomeHoldDuration + fadeDuration + 0.1f);

        if (capturedFrameTexture != null)
        {
            Destroy(capturedFrameTexture);
        }
        Destroy(gameObject);
    }

    private AsyncOperation GetSceneLoadOperation(string targetScene)
    {
        if (preloadedScene != null && preloadedSceneName == targetScene)
        {
            AsyncOperation loadOperation = preloadedScene;
            preloadedScene = null;
            preloadedSceneName = null;
            preloadRoutine = null;
            return loadOperation;
        }

        AsyncOperation newLoad = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);
        if (newLoad != null)
        {
            newLoad.priority = 0;
            newLoad.allowSceneActivation = false;
        }

        return newLoad;
    }

    private static Texture2D CaptureScreenRect(Rect screenRect)
    {
        int x = Mathf.Clamp(Mathf.FloorToInt(screenRect.xMin), 0, Screen.width - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(screenRect.yMin), 0, Screen.height - 1);
        int width = Mathf.Clamp(Mathf.CeilToInt(screenRect.width), 1, Screen.width - x);
        int height = Mathf.Clamp(Mathf.CeilToInt(screenRect.height), 1, Screen.height - y);

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(x, y, width, height), 0, 0, false);
        texture.Apply(false, false);
        return texture;
    }

    private static Texture2D CaptureCamera(Camera captureCamera)
    {
        if (captureCamera == null)
        {
            return CaptureScreenRect(new Rect(0f, 0f, Screen.width, Screen.height));
        }

        int width = Mathf.Max(1, Screen.width);
        int height = Mathf.Max(1, Screen.height);
        RenderTexture previousTarget = captureCamera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;

        RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

        captureCamera.targetTexture = target;
        RenderTexture.active = target;
        captureCamera.Render();
        texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
        texture.Apply(false, false);

        captureCamera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        RenderTexture.ReleaseTemporary(target);

        return texture;
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid())
        {
            return FindFirstObjectByType<T>();
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T component = roots[i].GetComponentInChildren<T>(true);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    private static IEnumerator FadeMixerVolume(AudioMixer mixer, string parameter, float from, float to, float duration)
    {
        if (mixer == null || string.IsNullOrWhiteSpace(parameter))
        {
            yield break;
        }

        if (duration <= 0.0001f)
        {
            mixer.SetFloat(parameter, LinearToDecibels(to));
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            mixer.SetFloat(parameter, LinearToDecibels(Mathf.Lerp(from, to, t)));
            yield return null;
        }

        mixer.SetFloat(parameter, LinearToDecibels(to));
    }

    private static float LinearToDecibels(float value)
    {
        float clamped = Mathf.Clamp(value, 0.0001f, 1f);
        return Mathf.Log10(clamped) * 20f;
    }

}
