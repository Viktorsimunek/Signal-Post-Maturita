using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public class DisplayBrightness : MonoBehaviour
{
    private static DisplayBrightness instance;
    private Canvas outputCanvas;
    private Material material;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (!instance) new GameObject("Display Brightness", typeof(Canvas), typeof(DisplayBrightness));
    }

    private void Awake()
    {
        if (instance && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        gameObject.layer = LayerMask.NameToLayer("UI");
        outputCanvas = GetComponent<Canvas>();
        outputCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        outputCanvas.sortingOrder = short.MaxValue;

        var source = Resources.Load<Material>("DisplayBrightness");
        if (!source)
        {
            Debug.LogError("DisplayBrightness: missing Resources/DisplayBrightness material.", this);
            outputCanvas.enabled = false;
            return;
        }
        material = new Material(source) { hideFlags = HideFlags.DontSave };
        var surface = new GameObject("Brightness", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        surface.transform.SetParent(transform, false);
        var rect = surface.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = surface.GetComponent<Image>();
        image.material = material;
        image.raycastTarget = false;

        GameSettings.EnsureLoaded();
        GameSettings.SettingsChanged += Apply;
        Apply();
    }

    private void Apply()
    {
        if (!material) return;
        // 50% is neutral; endpoints are half/double luminance, never a black screen.
        material.SetFloat("_Gain", Mathf.Pow(2f, (GameSettings.Brightness - 0.5f) * 2f));
        outputCanvas.enabled = !Mathf.Approximately(GameSettings.Brightness, 0.5f);
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        GameSettings.SettingsChanged -= Apply;
        if (material) Destroy(material);
        instance = null;
    }
}
