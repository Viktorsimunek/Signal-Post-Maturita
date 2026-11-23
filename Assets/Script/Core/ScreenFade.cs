using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ScreenFade : MonoBehaviour
{
    private static ScreenFade instance;
    private CanvasGroup canvas;
    private static float revealDuration;

    public static IEnumerator To(float alpha, float duration)
    {
        EnsureCanvas();
        yield return CanvasFade.Run(instance.canvas, instance.canvas.alpha, alpha, duration);
    }

    public static void Clear()
    {
        if (instance != null) instance.canvas.alpha = 0f;
    }

    public static void RevealOnNextScene(float duration)
    {
        revealDuration = duration;
        SceneManager.sceneLoaded -= Reveal;
        SceneManager.sceneLoaded += Reveal;
    }

    private static void Reveal(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= Reveal;
        if (instance != null) instance.StartCoroutine(To(0f, revealDuration));
    }

    private static void EnsureCanvas()
    {
        if (instance != null) return;
        var root = new GameObject("ScreenFade", typeof(Canvas), typeof(CanvasGroup), typeof(ScreenFade));
        DontDestroyOnLoad(root);
        instance = root.GetComponent<ScreenFade>();
        instance.canvas = root.GetComponent<CanvasGroup>();
        instance.canvas.alpha = 0f;
        instance.canvas.blocksRaycasts = false;
        var overlay = root.GetComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = short.MaxValue;

        var image = new GameObject("Black", typeof(RectTransform), typeof(Image));
        image.transform.SetParent(root.transform, false);
        var rect = image.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        image.GetComponent<Image>().color = Color.black;
        image.GetComponent<Image>().raycastTarget = false;
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= Reveal;
        instance = null;
    }
}
