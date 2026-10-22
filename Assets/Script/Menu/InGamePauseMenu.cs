using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class InGamePauseMenu : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string mainMenuScene = "Menu";

    [Header("UI References")]
    [SerializeField] private Canvas pauseCanvas;
    [SerializeField] private CanvasGroup pauseCanvasGroup;
    [SerializeField] private TMP_Text menuText;
    [SerializeField] private Camera canvasCamera;
    [SerializeField] private bool createFallbackUi = true;
    [SerializeField] private bool autoAssignCameraSpaceCamera = true;

    [Header("Display")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField, Min(12)] private int fontSize = 24;
    [SerializeField] private Color textColor = new Color(0.74f, 1f, 0.82f, 1f);
    [SerializeField] private Color panelColor = new Color(0.01f, 0.025f, 0.02f, 0.9f);
    [SerializeField, Min(0f)] private float fadeDuration = 0.18f;
    [SerializeField, Min(0.01f)] private float menuShuffleDuration = 0.16f;
    [SerializeField] private string shuffleCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789#$%/\\=-_";

    [Header("Input")]
    [SerializeField, Range(0.01f, 0.2f)] private float volumeStep = 0.05f;
    [SerializeField, Range(0.05f, 0.25f)] private float renderScaleStep = 0.05f;
    [SerializeField, Range(0.1f, 0.6f)] private float holdRepeatDelay = 0.32f;
    [SerializeField, Range(0.03f, 0.2f)] private float holdRepeatInterval = 0.08f;

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip cursorBeep;
    [SerializeField] private AudioClip confirmBeep;
    [SerializeField] private AudioMixer targetMixer;
    [SerializeField] private string masterVolumeParam = "MasterVolume";
    [SerializeField] private string musicVolumeParam = "MusicVolume";
    [SerializeField] private string sfxVolumeParam = "SFXVolume";
    [SerializeField, Range(0.85f, 1.15f)] private float cursorPitchMin = 0.96f;
    [SerializeField, Range(0.85f, 1.15f)] private float cursorPitchMax = 1.04f;

    [Header("Pause Blur")]
    [SerializeField] private bool enablePauseBlur = true;
    [SerializeField] private Volume postProcessVolume;
    [SerializeField] private bool keepMenuAbovePauseBlur = true;
    [SerializeField, Min(0.01f)] private float blurFadeDuration = 0.55f;
    [SerializeField, Min(0.01f)] private float blurFadeOutDuration = 0.75f;
    [SerializeField, Range(0.05f, 2f)] private float pauseBlurMaxRadius = 1.15f;
    [SerializeField, Min(0.01f)] private float pauseBlurStartDistance = 0.05f;
    [SerializeField, Min(0.02f)] private float pauseBlurEndDistance = 1.1f;

    private readonly string[] menuItems =
    {
        "RESUME",
        "MASTER VOLUME",
        "V SYNC",
        "FPS LIMIT",
        "RENDER SCALE",
        "FPS DISPLAY",
        "MAIN MENU",
        "QUIT"
    };

    private readonly int[] fpsLimitOptions = { 0, 30, 60, 90, 120, 144, 165, 240 };

    private FirstPersonController controller;
    private GameObject fallbackCanvasObject;
    private int selectedIndex;
    private bool isOpen;
    private float previousTimeScale = 1f;
    private int heldVerticalDirection;
    private float nextVerticalRepeatTime;
    private int heldHorizontalDirection;
    private float nextHorizontalRepeatTime;
    private string menuTargetText = string.Empty;
    private float menuShuffleElapsed;
    private bool isShufflingMenu;
    private float fadeTargetAlpha;
    private VolumeProfile originalPauseVolumeProfile;
    private VolumeProfile runtimePauseVolumeProfile;
    private DepthOfField pauseDepthOfField;
    private float blurTarget;
    private float blurAmount;
    private bool hasStoredCanvasRenderState;
    private RenderMode originalCanvasRenderMode;
    private Camera originalCanvasWorldCamera;
    private float originalCanvasPlaneDistance;
    private int originalCanvasSortingOrder;
    private bool restoreCanvasAfterCloseVisuals;
    private readonly StringBuilder menuTextBuilder = new StringBuilder(256);

    public bool IsOpen => isOpen;

    public void Configure(FirstPersonController targetController)
    {
        controller = targetController;
    }

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<FirstPersonController>();
        }

        GameSettings.EnsureLoaded();
        EnsureUi();
        SetupPauseBlur();
        SetOpen(false);
    }

    private void OnDestroy()
    {
        if (isOpen)
        {
            Time.timeScale = previousTimeScale;
        }

        if (postProcessVolume != null && originalPauseVolumeProfile != null)
        {
            postProcessVolume.profile = originalPauseVolumeProfile;
        }
    }

    private void Update()
    {
        if (WasPausePressed())
        {
            if (isOpen)
            {
                Close();
            }
            else if (CanOpenPauseMenu())
            {
                Open();
            }

            return;
        }

        if (!isOpen)
        {
            UpdateFade();
            UpdatePauseBlur();
            TryFinishCloseVisuals();
            return;
        }

        HandleNavigation();
        UpdateMenuShuffle();
        UpdateFade();
        UpdatePauseBlur();
    }

    private void Open()
    {
        selectedIndex = 0;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (controller != null)
        {
            controller.SetMovementEnabled(false);
            controller.SetLookEnabled(false);
            controller.SetCursorLockState(false, false);
        }

        SetOpen(true);
        RenderMenu();
    }

    private void Close()
    {
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;

        if (controller != null)
        {
            controller.SetMovementEnabled(true);
            controller.SetLookEnabled(true);
            controller.SetCursorLockState(true, true);
        }

        SetOpen(false);
    }

    private void SetOpen(bool open)
    {
        isOpen = open;
        if (pauseCanvasGroup != null)
        {
            fadeTargetAlpha = open ? 1f : 0f;
            if (fadeDuration <= 0f)
            {
                pauseCanvasGroup.alpha = fadeTargetAlpha;
            }

            pauseCanvasGroup.interactable = open;
            pauseCanvasGroup.blocksRaycasts = open;
        }

        blurTarget = open ? 1f : 0f;
        if (open)
        {
            restoreCanvasAfterCloseVisuals = false;
            SetPauseCanvasAboveBlur(true);
        }
        else
        {
            restoreCanvasAfterCloseVisuals = true;
        }

        if (pauseCanvas != null && pauseCanvas.gameObject == fallbackCanvasObject)
        {
            pauseCanvas.gameObject.SetActive(true);
        }

        if (!open && menuText != null)
        {
            menuText.text = string.Empty;
            menuTargetText = string.Empty;
            isShufflingMenu = false;
            menuShuffleElapsed = 0f;
        }
    }

    private void UpdateFade()
    {
        if (pauseCanvasGroup == null || Mathf.Approximately(pauseCanvasGroup.alpha, fadeTargetAlpha))
        {
            return;
        }

        if (fadeDuration <= 0f)
        {
            pauseCanvasGroup.alpha = fadeTargetAlpha;
            return;
        }

        pauseCanvasGroup.alpha = Mathf.MoveTowards(
            pauseCanvasGroup.alpha,
            fadeTargetAlpha,
            Time.unscaledDeltaTime / fadeDuration);
    }

    private void SetupPauseBlur()
    {
        if (!enablePauseBlur)
        {
            return;
        }

        if (postProcessVolume == null)
        {
            postProcessVolume = FindBestPostProcessVolume();
        }

        if (postProcessVolume == null)
        {
            return;
        }

        originalPauseVolumeProfile = postProcessVolume.sharedProfile != null ? postProcessVolume.sharedProfile : postProcessVolume.profile;
        if (originalPauseVolumeProfile == null)
        {
            return;
        }

        runtimePauseVolumeProfile = Instantiate(originalPauseVolumeProfile);
        postProcessVolume.profile = runtimePauseVolumeProfile;

        if (!runtimePauseVolumeProfile.TryGet(out pauseDepthOfField))
        {
            pauseDepthOfField = runtimePauseVolumeProfile.Add<DepthOfField>(true);
        }

        pauseDepthOfField.active = true;
        pauseDepthOfField.mode.overrideState = true;
        pauseDepthOfField.mode.value = DepthOfFieldMode.Gaussian;
        pauseDepthOfField.gaussianStart.overrideState = true;
        pauseDepthOfField.gaussianEnd.overrideState = true;
        pauseDepthOfField.gaussianMaxRadius.overrideState = true;

        ApplyPauseBlurAmount(0f);
    }

    private void SetPauseCanvasAboveBlur(bool aboveBlur)
    {
        if (!keepMenuAbovePauseBlur || pauseCanvas == null || pauseCanvas.renderMode == RenderMode.WorldSpace)
        {
            return;
        }

        if (!aboveBlur && !hasStoredCanvasRenderState)
        {
            return;
        }

        if (aboveBlur && !hasStoredCanvasRenderState)
        {
            originalCanvasRenderMode = pauseCanvas.renderMode;
            originalCanvasWorldCamera = pauseCanvas.worldCamera;
            originalCanvasPlaneDistance = pauseCanvas.planeDistance;
            originalCanvasSortingOrder = pauseCanvas.sortingOrder;
            hasStoredCanvasRenderState = true;
        }

        if (aboveBlur)
        {
            pauseCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            pauseCanvas.sortingOrder = short.MaxValue - 1;
            return;
        }

        pauseCanvas.renderMode = originalCanvasRenderMode;
        pauseCanvas.worldCamera = originalCanvasWorldCamera;
        pauseCanvas.planeDistance = originalCanvasPlaneDistance;
        pauseCanvas.sortingOrder = originalCanvasSortingOrder;
        AssignCanvasCameraIfNeeded();
    }

    private void TryFinishCloseVisuals()
    {
        if (!restoreCanvasAfterCloseVisuals || isOpen)
        {
            return;
        }

        bool blurFinished = !enablePauseBlur || pauseDepthOfField == null || blurAmount <= 0.001f;
        bool fadeFinished = pauseCanvasGroup == null || Mathf.Approximately(pauseCanvasGroup.alpha, fadeTargetAlpha);
        if (!blurFinished || !fadeFinished)
        {
            return;
        }

        SetPauseCanvasAboveBlur(false);
        restoreCanvasAfterCloseVisuals = false;
    }

    private static Volume FindBestPostProcessVolume()
    {
        Volume[] volumes = FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Volume bestVolume = null;
        float bestPriority = float.NegativeInfinity;

        for (int i = 0; i < volumes.Length; i++)
        {
            Volume volume = volumes[i];
            if (volume == null || (volume.sharedProfile == null && volume.profile == null))
            {
                continue;
            }

            float priority = volume.priority + (volume.isGlobal ? 10000f : 0f);
            if (bestVolume == null || priority > bestPriority)
            {
                bestVolume = volume;
                bestPriority = priority;
            }
        }

        return bestVolume;
    }

    private void UpdatePauseBlur()
    {
        if (!enablePauseBlur || pauseDepthOfField == null || Mathf.Approximately(blurAmount, blurTarget))
        {
            return;
        }

        float duration = blurTarget > blurAmount ? blurFadeDuration : blurFadeOutDuration;
        blurAmount = Mathf.MoveTowards(blurAmount, blurTarget, Time.unscaledDeltaTime / duration);
        ApplyPauseBlurAmount(blurAmount);
    }

    private void ApplyPauseBlurAmount(float amount)
    {
        if (pauseDepthOfField == null)
        {
            return;
        }

        amount = Mathf.Clamp01(amount);
        pauseDepthOfField.active = amount > 0.001f;

        float safeEndDistance = Mathf.Max(pauseBlurEndDistance, pauseBlurStartDistance + 0.01f);
        pauseDepthOfField.gaussianStart.value = pauseBlurStartDistance;
        pauseDepthOfField.gaussianEnd.value = safeEndDistance;
        pauseDepthOfField.gaussianMaxRadius.value = pauseBlurMaxRadius * amount;
    }

    private void HandleNavigation()
    {
        int verticalStep = GetHeldStep(GetVerticalRawDirection(), ref heldVerticalDirection, ref nextVerticalRepeatTime);
        if (verticalStep != 0)
        {
            selectedIndex = (selectedIndex + verticalStep + menuItems.Length) % menuItems.Length;
            PlaySfx(cursorBeep);
            RenderMenu();
        }

        int horizontalStep = GetHeldStep(GetHorizontalRawDirection(), ref heldHorizontalDirection, ref nextHorizontalRepeatTime);
        if (horizontalStep != 0)
        {
            AdjustSelectedSetting(horizontalStep);
        }

        if (WasSubmitPressed())
        {
            ActivateSelection();
        }
    }

    private void ActivateSelection()
    {
        string selectedItem = menuItems[selectedIndex];
        PlaySfx(confirmBeep);

        if (selectedItem == "RESUME")
        {
            Close();
            return;
        }

        if (selectedItem == "MAIN MENU")
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuScene);
            return;
        }

        if (selectedItem == "QUIT")
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            return;
        }

        AdjustSelectedSetting(1, false);
    }

    private void AdjustSelectedSetting(int direction, bool playCursorSfx = true)
    {
        if (direction == 0)
        {
            return;
        }

        string selectedItem = menuItems[selectedIndex];
        if (selectedItem == "MASTER VOLUME")
        {
            GameSettings.SetMasterVolume(GameSettings.MasterVolume + direction * volumeStep);
            if (playCursorSfx) PlaySfx(cursorBeep);
            CommitSettings();
            return;
        }

        if (selectedItem == "V SYNC")
        {
            GameSettings.SetVSync(!GameSettings.VSyncEnabled);
            if (playCursorSfx) PlaySfx(cursorBeep);
            CommitSettings();
            return;
        }

        if (selectedItem == "FPS LIMIT")
        {
            CycleFpsLimit(direction);
            if (playCursorSfx) PlaySfx(cursorBeep);
            CommitSettings();
            return;
        }

        if (selectedItem == "RENDER SCALE")
        {
            GameSettings.SetRenderScale(GameSettings.RenderScale + direction * renderScaleStep);
            if (playCursorSfx) PlaySfx(cursorBeep);
            CommitSettings();
            return;
        }

        if (selectedItem == "FPS DISPLAY")
        {
            GameSettings.SetShowFpsDisplay(!GameSettings.ShowFpsDisplay);
            if (playCursorSfx) PlaySfx(cursorBeep);
            CommitSettings();
        }
    }

    private void PlaySfx(AudioClip clip)
    {
        if (sfxSource == null || clip == null)
        {
            return;
        }

        sfxSource.pitch = clip == cursorBeep
            ? Random.Range(Mathf.Min(cursorPitchMin, cursorPitchMax), Mathf.Max(cursorPitchMin, cursorPitchMax))
            : 1f;
        sfxSource.PlayOneShot(clip);
    }

    private void CommitSettings()
    {
        GameSettings.Save();
        GameSettings.ApplyRuntime();
        ApplyMixerVolumes();
        RenderMenu();
    }

    private void ApplyMixerVolumes()
    {
        if (targetMixer == null)
        {
            return;
        }

        targetMixer.SetFloat(masterVolumeParam, LinearToDecibels(GameSettings.MasterVolume));
        targetMixer.SetFloat(musicVolumeParam, LinearToDecibels(GameSettings.MusicVolume));
        targetMixer.SetFloat(sfxVolumeParam, LinearToDecibels(GameSettings.SfxVolume));
    }

    private void RenderMenu()
    {
        if (menuText == null)
        {
            return;
        }

        string text = "PAUSED\n\n";
        for (int i = 0; i < menuItems.Length; i++)
        {
            string label = GetItemLabel(menuItems[i]);
            text += (i == selectedIndex ? "> " : "  ") + label + "\n";
        }

        SetMenuTargetText(text.TrimEnd());
    }

    private void SetMenuTargetText(string nextText)
    {
        nextText = nextText ?? string.Empty;
        if (menuTargetText == nextText)
        {
            return;
        }

        menuTargetText = nextText;
        menuShuffleElapsed = 0f;
        isShufflingMenu = !string.IsNullOrEmpty(menuTargetText);

        if (menuText == null)
        {
            return;
        }

        if (isShufflingMenu)
        {
            menuText.text = BuildScrambledText(menuTargetText, 0f);
        }
        else
        {
            menuText.text = menuTargetText;
        }
    }

    private void UpdateMenuShuffle()
    {
        if (menuText == null || string.IsNullOrEmpty(menuTargetText))
        {
            return;
        }

        if (!isShufflingMenu)
        {
            if (menuText.text != menuTargetText)
            {
                menuText.text = menuTargetText;
            }

            return;
        }

        menuShuffleElapsed += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(menuShuffleElapsed / menuShuffleDuration);
        menuText.text = BuildScrambledText(menuTargetText, progress);

        if (progress >= 1f)
        {
            isShufflingMenu = false;
            menuText.text = menuTargetText;
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
        menuTextBuilder.Clear();

        for (int i = 0; i < targetText.Length; i++)
        {
            char targetCharacter = targetText[i];
            if (i < revealCount || ShouldKeepCharacterVisible(targetCharacter))
            {
                menuTextBuilder.Append(targetCharacter);
                continue;
            }

            int shuffleIndex = Mathf.Abs((Time.frameCount * 17) + (i * 31)) % shuffleCharacters.Length;
            menuTextBuilder.Append(shuffleCharacters[shuffleIndex]);
        }

        return menuTextBuilder.ToString();
    }

    private static bool ShouldKeepCharacterVisible(char value)
    {
        return char.IsWhiteSpace(value) || char.IsPunctuation(value) || char.IsSymbol(value);
    }

    private string GetItemLabel(string item)
    {
        switch (item)
        {
            case "MASTER VOLUME":
                return "MASTER VOLUME: " + FormatPercent(GameSettings.MasterVolume);
            case "V SYNC":
                return "V SYNC: " + (GameSettings.VSyncEnabled ? "ON" : "OFF");
            case "FPS LIMIT":
                return "FPS LIMIT: " + FormatFpsLimit();
            case "RENDER SCALE":
                return "RENDER SCALE: " + FormatPercent(GameSettings.RenderScale);
            case "FPS DISPLAY":
                return "FPS DISPLAY: " + (GameSettings.ShowFpsDisplay ? "ON" : "OFF");
            default:
                return item;
        }
    }

    private void CycleFpsLimit(int direction)
    {
        int currentIndex = 0;
        int bestDistance = int.MaxValue;
        for (int i = 0; i < fpsLimitOptions.Length; i++)
        {
            int distance = Mathf.Abs(fpsLimitOptions[i] - GameSettings.FpsLimit);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                currentIndex = i;
            }
        }

        int nextIndex = (currentIndex + direction + fpsLimitOptions.Length) % fpsLimitOptions.Length;
        GameSettings.SetFpsLimit(fpsLimitOptions[nextIndex]);
    }

    private bool CanOpenPauseMenu()
    {
        TerminalControl[] terminals = FindObjectsByType<TerminalControl>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < terminals.Length; i++)
        {
            if (terminals[i] != null && terminals[i].IsFocused)
            {
                return false;
            }
        }

        return true;
    }

    private static bool WasPausePressed()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;

        bool keyboardPressed = GameSettings.AllowsKeyboardMouseInput() && keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        bool gamepadPressed = GameSettings.AllowsGamepadInput() && gamepad != null && gamepad.startButton.wasPressedThisFrame;
        if (keyboardPressed) InputDeviceTracker.MarkKeyboardMouse();
        if (gamepadPressed) InputDeviceTracker.MarkGamepad();
        return keyboardPressed || gamepadPressed;
    }

    private static bool WasSubmitPressed()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;

        bool keyboardPressed = GameSettings.AllowsKeyboardMouseInput() && keyboard != null &&
                               (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame);
        bool gamepadPressed = GameSettings.AllowsGamepadInput() && gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
        if (keyboardPressed) InputDeviceTracker.MarkKeyboardMouse();
        if (gamepadPressed) InputDeviceTracker.MarkGamepad();
        return keyboardPressed || gamepadPressed;
    }

    private static int GetVerticalRawDirection()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;

        bool up = GameSettings.AllowsKeyboardMouseInput() && keyboard != null && (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed);
        bool down = GameSettings.AllowsKeyboardMouseInput() && keyboard != null && (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed);

        if (GameSettings.AllowsGamepadInput() && gamepad != null)
        {
            up |= gamepad.dpad.up.isPressed || gamepad.leftStick.ReadValue().y > 0.55f;
            down |= gamepad.dpad.down.isPressed || gamepad.leftStick.ReadValue().y < -0.55f;
        }

        if (up == down)
        {
            return 0;
        }

        return up ? -1 : 1;
    }

    private static int GetHorizontalRawDirection()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;

        bool left = GameSettings.AllowsKeyboardMouseInput() && keyboard != null && (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed);
        bool right = GameSettings.AllowsKeyboardMouseInput() && keyboard != null && (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed);

        if (GameSettings.AllowsGamepadInput() && gamepad != null)
        {
            left |= gamepad.dpad.left.isPressed || gamepad.leftStick.ReadValue().x < -0.55f;
            right |= gamepad.dpad.right.isPressed || gamepad.leftStick.ReadValue().x > 0.55f;
        }

        if (left == right)
        {
            return 0;
        }

        return right ? 1 : -1;
    }

    private int GetHeldStep(int rawDirection, ref int heldDirection, ref float nextRepeatTime)
    {
        if (rawDirection == 0)
        {
            heldDirection = 0;
            nextRepeatTime = 0f;
            return 0;
        }

        if (rawDirection != heldDirection)
        {
            heldDirection = rawDirection;
            nextRepeatTime = Time.unscaledTime + holdRepeatDelay;
            return rawDirection;
        }

        if (Time.unscaledTime >= nextRepeatTime)
        {
            nextRepeatTime = Time.unscaledTime + holdRepeatInterval;
            return rawDirection;
        }

        return 0;
    }

    private void EnsureUi()
    {
        if (pauseCanvas == null && menuText != null && menuText.canvas != null)
        {
            pauseCanvas = menuText.canvas.rootCanvas;
        }

        if (pauseCanvas != null)
        {
            if (pauseCanvas.gameObject != fallbackCanvasObject)
            {
                pauseCanvas.gameObject.SetActive(true);
            }

            if (menuText == null)
            {
                menuText = pauseCanvas.GetComponentInChildren<TMP_Text>(true);
            }

            if (pauseCanvasGroup == null)
            {
                pauseCanvasGroup = pauseCanvas.GetComponent<CanvasGroup>();
                if (pauseCanvasGroup == null)
                {
                    pauseCanvasGroup = pauseCanvas.gameObject.AddComponent<CanvasGroup>();
                }
            }

            AssignCanvasCameraIfNeeded();
            return;
        }

        if (createFallbackUi)
        {
            CreateFallbackUi();
        }
    }

    private void AssignCanvasCameraIfNeeded()
    {
        if (!autoAssignCameraSpaceCamera || pauseCanvas == null || pauseCanvas.renderMode != RenderMode.ScreenSpaceCamera)
        {
            return;
        }

        if (pauseCanvas.worldCamera != null)
        {
            return;
        }

        Camera targetCamera = canvasCamera != null ? canvasCamera : Camera.main;
        if (targetCamera != null)
        {
            pauseCanvas.worldCamera = targetCamera;
        }
    }

    private void CreateFallbackUi()
    {
        GameObject canvasObject = new GameObject("Pause Menu Canvas");
        canvasObject.transform.SetParent(transform, false);
        fallbackCanvasObject = canvasObject;

        pauseCanvas = canvasObject.AddComponent<Canvas>();
        pauseCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        pauseCanvas.worldCamera = canvasCamera != null ? canvasCamera : Camera.main;
        pauseCanvas.planeDistance = 0.5f;
        pauseCanvas.sortingOrder = short.MaxValue - 1;

        pauseCanvasGroup = canvasObject.AddComponent<CanvasGroup>();

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject shadeObject = new GameObject("Shade");
        shadeObject.transform.SetParent(canvasObject.transform, false);
        Image shade = shadeObject.AddComponent<Image>();
        shade.color = new Color(0f, 0f, 0f, 0.45f);
        RectTransform shadeRect = shade.rectTransform;
        shadeRect.anchorMin = Vector2.zero;
        shadeRect.anchorMax = Vector2.one;
        shadeRect.offsetMin = Vector2.zero;
        shadeRect.offsetMax = Vector2.zero;

        GameObject panelObject = new GameObject("Panel");
        panelObject.transform.SetParent(canvasObject.transform, false);
        Image panel = panelObject.AddComponent<Image>();
        panel.color = panelColor;
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(520f, 520f);
        panelRect.anchoredPosition = Vector2.zero;

        GameObject textObject = new GameObject("Menu Text");
        textObject.transform.SetParent(panelObject.transform, false);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = textColor;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        if (font != null)
        {
            text.font = font;
        }

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(40f, 34f);
        textRect.offsetMax = new Vector2(-40f, -34f);

        menuText = text;
    }

    private static string FormatPercent(float value)
    {
        return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
    }

    private string FormatFpsLimit()
    {
        return GameSettings.FpsLimit == 0 ? "UNLIMITED" : GameSettings.FpsLimit.ToString();
    }

    private static float LinearToDecibels(float value)
    {
        float clamped = Mathf.Clamp(value, 0.0001f, 1f);
        return Mathf.Log10(clamped) * 20f;
    }

}
