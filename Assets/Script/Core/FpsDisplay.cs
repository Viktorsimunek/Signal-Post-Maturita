using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FpsDisplay : MonoBehaviour
{
    private const string RootName = "FPS Display";
    private const float RefreshInterval = 0.25f;

    private static FpsDisplay instance;

    private TMP_Text fpsText;
    private float elapsed;
    private int frames;
    private float nextRefreshTime;

    public static void EnsureExists(TMP_FontAsset font, int fontSize, Color color)
    {
        if (instance == null)
        {
            GameObject root = new GameObject(RootName);
            instance = root.AddComponent<FpsDisplay>();
        }

        instance.Configure(font, fontSize, color);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        CreateUi();
        GameSettings.SettingsChanged += ApplyVisibility;
        GameSettings.EnsureLoaded();
        ApplyVisibility();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            GameSettings.SettingsChanged -= ApplyVisibility;
            instance = null;
        }
    }

    private void Update()
    {
        if (fpsText == null || !fpsText.gameObject.activeSelf)
        {
            return;
        }

        float deltaTime = Time.unscaledDeltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        elapsed += deltaTime;
        frames++;

        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        float fps = frames / Mathf.Max(0.0001f, elapsed);
        fpsText.text = "FPS " + Mathf.RoundToInt(fps);
        elapsed = 0f;
        frames = 0;
        nextRefreshTime = Time.unscaledTime + RefreshInterval;
    }

    private void Configure(TMP_FontAsset font, int fontSize, Color color)
    {
        CreateUi();

        if (fpsText == null)
        {
            return;
        }

        if (font != null)
        {
            fpsText.font = font;
        }

        fpsText.fontSize = Mathf.Max(8, fontSize);
        fpsText.color = color;
    }

    private void CreateUi()
    {
        if (fpsText != null)
        {
            return;
        }

        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        GameObject textObject = new GameObject("FPS Label");
        textObject.transform.SetParent(transform, false);

        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = "FPS --";
        label.alignment = TextAlignmentOptions.TopRight;
        label.enableWordWrapping = false;
        label.raycastTarget = false;

        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-16f, -12f);
        rect.sizeDelta = new Vector2(180f, 40f);

        fpsText = label;
    }

    private void ApplyVisibility()
    {
        if (fpsText != null)
        {
            fpsText.gameObject.SetActive(GameSettings.ShowFpsDisplay);
        }
    }
}
