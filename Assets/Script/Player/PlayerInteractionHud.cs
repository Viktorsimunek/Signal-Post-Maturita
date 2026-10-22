using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInteractor))]
public class PlayerInteractionHud : MonoBehaviour
{
    private const string PromptObjectName = "InteractionPrompt";

    [Header("References")]
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private FirstPersonController controller;
    [SerializeField] private Canvas hudCanvas;
    [SerializeField] private Image crosshairImage;
    [SerializeField] private TextMeshProUGUI promptLabel;
    [SerializeField] private TextMeshProUGUI actionHintLabel;

    [Header("Prompt")]
    [SerializeField] private Vector2 promptOffset = new Vector2(0f, -42f);
    [SerializeField] private string promptFormat = "[{0}] {1}";
    [SerializeField] private Color promptColor = new Color(0.94f, 0.94f, 0.94f, 0.95f);
    [SerializeField] private float promptFontSize = 22f;
    [SerializeField, Min(0.01f)] private float promptFadeSpeed = 14f;
    [SerializeField, Min(0.01f)] private float promptShuffleDuration = 0.16f;
    [SerializeField] private string shuffleCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    [Header("Crosshair")]
    [SerializeField] private Color crosshairIdleColor = new Color(0.6037736f, 0.6037736f, 0.6037736f, 0.5019608f);
    [SerializeField] private Color crosshairHighlightColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField, Min(0.01f)] private float crosshairColorLerpSpeed = 10f;
    [SerializeField] private Vector2 createdCrosshairSize = new Vector2(10f, 10f);

    private float promptVisibility;
    private bool promptVisible;
    private string promptTargetText = string.Empty;
    private float promptShuffleElapsed;
    private bool isShufflingPrompt;
    private Object actionHintOwner;
    private float actionHintVisibility;
    private bool actionHintVisible;
    private string actionHintTargetText = string.Empty;
    private float actionHintShuffleElapsed;
    private bool isShufflingActionHint;
    private readonly StringBuilder promptBuilder = new StringBuilder(64);

    public void Configure(PlayerInteractor targetInteractor)
    {
        interactor = targetInteractor;
    }

    public void SetActionHint(Object owner, string hint)
    {
        if (actionHintOwner != null && owner != null && actionHintOwner != owner)
        {
            return;
        }

        actionHintOwner = owner;

        if (actionHintLabel != null)
        {
            actionHintLabel.raycastTarget = false;
        }

        SetActionHintTarget(hint);
        actionHintVisible = !string.IsNullOrWhiteSpace(actionHintTargetText);
    }

    public void ClearActionHint(Object owner)
    {
        if (actionHintOwner != null && owner != null && actionHintOwner != owner)
        {
            return;
        }

        actionHintOwner = null;

        actionHintVisible = false;
    }

    private void Reset()
    {
        interactor = GetComponent<PlayerInteractor>();
        controller = GetComponent<FirstPersonController>();
    }

    private void Awake()
    {
        if (interactor == null)
        {
            interactor = GetComponent<PlayerInteractor>();
        }

        if (controller == null)
        {
            controller = GetComponent<FirstPersonController>();
        }

        EnsureHud();
        SetCrosshairColorImmediately(crosshairIdleColor);
        HidePromptImmediately();
    }

    private void LateUpdate()
    {
        EnsureHud();
        RefreshHudState();
        UpdatePromptVisuals();
        UpdateActionHintVisuals();
    }

    private void OnDisable()
    {
        HideActionHintImmediately();
        HidePromptImmediately();

        if (crosshairImage != null)
        {
            SetCrosshairColorImmediately(crosshairIdleColor);
        }
    }

    private void EnsureHud()
    {
        if (crosshairImage == null)
        {
            TryFindExistingCrosshair();
        }

        if (hudCanvas == null && promptLabel != null && promptLabel.canvas != null)
        {
            hudCanvas = promptLabel.canvas.rootCanvas;
        }

        if (hudCanvas == null)
        {
            CreateHudCanvas();
        }

        if (crosshairImage == null)
        {
            crosshairImage = CreateCrosshairImage(hudCanvas.transform);
        }

        if (promptLabel == null)
        {
            promptLabel = FindOwnedPromptLabel();
        }

        if (promptLabel == null)
        {
            promptLabel = CreatePromptLabel(hudCanvas.transform);
        }

        if (crosshairImage != null)
        {
            crosshairImage.raycastTarget = false;
        }

        if (promptLabel != null)
        {
            promptLabel.raycastTarget = false;
            promptLabel.color = promptColor;
            promptLabel.alpha = promptVisibility;
        }

        if (actionHintLabel != null)
        {
            actionHintLabel.raycastTarget = false;
            actionHintLabel.alpha = actionHintVisibility;
            actionHintLabel.enabled = actionHintVisibility > 0.001f || actionHintVisible;
        }
    }

    private void RefreshHudState()
    {
        bool canShowPrompt = interactor != null
            && interactor.IsInteractionEnabled
            && interactor.HasInteractable
            && (controller == null || controller.IsCursorLocked);

        if (crosshairImage != null)
        {
            Color targetColor = canShowPrompt ? crosshairHighlightColor : crosshairIdleColor;
            crosshairImage.color = Color.Lerp(crosshairImage.color, targetColor, Time.unscaledDeltaTime * crosshairColorLerpSpeed);
        }

        if (!canShowPrompt)
        {
            promptVisible = false;
            return;
        }

        string prompt = interactor.CurrentPrompt;
        if (string.IsNullOrWhiteSpace(prompt))
        {
            promptVisible = false;
            return;
        }

        string bindingDisplay = interactor.GetInteractionButtonText();
        string nextPromptText = string.IsNullOrWhiteSpace(bindingDisplay)
            ? prompt
            : string.Format(promptFormat, bindingDisplay, prompt);

        SetPromptTarget(nextPromptText);
        promptVisible = true;
    }

    private void SetPromptTarget(string nextPromptText)
    {
        if (promptTargetText == nextPromptText)
        {
            return;
        }

        promptTargetText = nextPromptText ?? string.Empty;
        promptShuffleElapsed = 0f;
        isShufflingPrompt = !string.IsNullOrEmpty(promptTargetText);
    }

    private void SetActionHintTarget(string nextHintText)
    {
        if (nextHintText == null)
        {
            nextHintText = string.Empty;
        }

        if (actionHintTargetText == nextHintText)
        {
            return;
        }

        actionHintTargetText = nextHintText;
        actionHintShuffleElapsed = 0f;
        isShufflingActionHint = !string.IsNullOrEmpty(actionHintTargetText);
    }

    private void UpdatePromptVisuals()
    {
        if (promptLabel == null)
        {
            return;
        }

        float targetVisibility = promptVisible ? 1f : 0f;
        promptVisibility = Mathf.MoveTowards(promptVisibility, targetVisibility, Time.unscaledDeltaTime * promptFadeSpeed);
        promptLabel.alpha = promptVisibility;

        if (!promptVisible)
        {
            if (promptVisibility <= 0.001f && promptLabel.text.Length > 0)
            {
                promptLabel.text = string.Empty;
            }

            return;
        }

        if (isShufflingPrompt)
        {
            promptShuffleElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(promptShuffleElapsed / promptShuffleDuration);
            promptLabel.text = BuildScrambledText(promptTargetText, progress);

            if (progress >= 1f)
            {
                isShufflingPrompt = false;
                promptLabel.text = promptTargetText;
            }

            return;
        }

        if (promptLabel.text != promptTargetText)
        {
            promptLabel.text = promptTargetText;
        }
    }

    private void UpdateActionHintVisuals()
    {
        if (actionHintLabel == null)
        {
            return;
        }

        float targetVisibility = actionHintVisible ? 1f : 0f;
        actionHintVisibility = Mathf.MoveTowards(actionHintVisibility, targetVisibility, Time.unscaledDeltaTime * promptFadeSpeed);
        actionHintLabel.alpha = actionHintVisibility;
        actionHintLabel.enabled = actionHintVisibility > 0.001f || actionHintVisible;

        if (!actionHintVisible)
        {
            if (actionHintVisibility <= 0.001f && actionHintLabel.text.Length > 0)
            {
                actionHintLabel.text = string.Empty;
                actionHintTargetText = string.Empty;
                isShufflingActionHint = false;
            }

            return;
        }

        if (isShufflingActionHint)
        {
            actionHintShuffleElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(actionHintShuffleElapsed / promptShuffleDuration);
            actionHintLabel.text = BuildScrambledText(actionHintTargetText, progress);

            if (progress >= 1f)
            {
                isShufflingActionHint = false;
                actionHintLabel.text = actionHintTargetText;
            }

            return;
        }

        if (actionHintLabel.text != actionHintTargetText)
        {
            actionHintLabel.text = actionHintTargetText;
        }
    }

    private string BuildScrambledText(string targetText, float progress)
    {
        if (string.IsNullOrEmpty(targetText))
        {
            return string.Empty;
        }

        if (progress >= 1f || string.IsNullOrEmpty(shuffleCharacters))
        {
            return targetText;
        }

        int revealCount = Mathf.FloorToInt(targetText.Length * progress);
        promptBuilder.Clear();

        for (int i = 0; i < targetText.Length; i++)
        {
            char targetCharacter = targetText[i];
            if (i < revealCount || ShouldKeepCharacterVisible(targetCharacter))
            {
                promptBuilder.Append(targetCharacter);
                continue;
            }

            int shuffleIndex = Mathf.Abs((Time.frameCount * 17) + (i * 31)) % shuffleCharacters.Length;
            promptBuilder.Append(shuffleCharacters[shuffleIndex]);
        }

        return promptBuilder.ToString();
    }

    private static bool ShouldKeepCharacterVisible(char value)
    {
        return char.IsWhiteSpace(value) || char.IsPunctuation(value) || char.IsSymbol(value);
    }

    private void HidePromptImmediately()
    {
        promptVisible = false;
        promptVisibility = 0f;
        promptTargetText = string.Empty;
        promptShuffleElapsed = 0f;
        isShufflingPrompt = false;

        if (promptLabel == null)
        {
            return;
        }

        promptLabel.alpha = 0f;
        promptLabel.text = string.Empty;
    }

    private void HideActionHintImmediately()
    {
        actionHintOwner = null;
        actionHintVisible = false;
        actionHintVisibility = 0f;
        actionHintTargetText = string.Empty;
        actionHintShuffleElapsed = 0f;
        isShufflingActionHint = false;

        if (actionHintLabel == null)
        {
            return;
        }

        actionHintLabel.alpha = 0f;
        actionHintLabel.text = string.Empty;
        actionHintLabel.enabled = false;
    }

    private void SetCrosshairColorImmediately(Color color)
    {
        if (crosshairImage != null)
        {
            crosshairImage.color = color;
        }
    }

    private void TryFindExistingCrosshair()
    {
        Image[] images = Object.FindObjectsOfType<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image candidate = images[i];
            if (candidate == null)
            {
                continue;
            }

            Canvas candidateCanvas = candidate.canvas != null ? candidate.canvas.rootCanvas : null;
            RectTransform rectTransform = candidate.rectTransform;
            if (candidateCanvas == null
                || candidateCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                || candidateCanvas.transform.parent != null
                || rectTransform == null
                || !IsCentered(rectTransform))
            {
                continue;
            }

            crosshairImage = candidate;
            break;
        }
    }

    private TextMeshProUGUI FindOwnedPromptLabel()
    {
        if (hudCanvas == null)
        {
            return null;
        }

        TextMeshProUGUI[] labels = hudCanvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            TextMeshProUGUI candidate = labels[i];
            if (IsValidPromptLabel(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private bool IsValidPromptLabel(TextMeshProUGUI candidate)
    {
        if (candidate == null || hudCanvas == null)
        {
            return false;
        }

        RectTransform rectTransform = candidate.rectTransform;
        if (rectTransform == null)
        {
            return false;
        }

        return candidate.gameObject.name == PromptObjectName
            && rectTransform.parent == hudCanvas.transform
            && Mathf.Approximately(rectTransform.anchorMin.x, 0.5f)
            && Mathf.Approximately(rectTransform.anchorMin.y, 0.5f)
            && Mathf.Approximately(rectTransform.anchorMax.x, 0.5f)
            && Mathf.Approximately(rectTransform.anchorMax.y, 0.5f);
    }

    private void CreateHudCanvas()
    {
        GameObject canvasObject = new GameObject("InteractionHudCanvas");
        hudCanvas = canvasObject.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
    }

    private Image CreateCrosshairImage(Transform parent)
    {
        GameObject crosshairObject = new GameObject("Crosshair");
        crosshairObject.transform.SetParent(parent, false);

        RectTransform rectTransform = crosshairObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = createdCrosshairSize;

        Image image = crosshairObject.AddComponent<Image>();
        image.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        image.color = crosshairIdleColor;
        image.raycastTarget = false;
        return image;
    }

    private TextMeshProUGUI CreatePromptLabel(Transform parent)
    {
        GameObject promptObject = new GameObject(PromptObjectName);
        promptObject.transform.SetParent(parent, false);

        RectTransform rectTransform = promptObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.anchoredPosition = promptOffset;
        rectTransform.sizeDelta = new Vector2(700f, 80f);

        TextMeshProUGUI text = promptObject.AddComponent<TextMeshProUGUI>();
        text.text = string.Empty;
        text.fontSize = promptFontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        text.color = promptColor;

        TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
        if (defaultFont != null)
        {
            text.font = defaultFont;
        }

        return text;
    }

    private static bool IsCentered(RectTransform rectTransform)
    {
        return Mathf.Approximately(rectTransform.anchorMin.x, 0.5f)
            && Mathf.Approximately(rectTransform.anchorMin.y, 0.5f)
            && Mathf.Approximately(rectTransform.anchorMax.x, 0.5f)
            && Mathf.Approximately(rectTransform.anchorMax.y, 0.5f)
            && rectTransform.anchoredPosition.sqrMagnitude < 0.01f;
    }
}
