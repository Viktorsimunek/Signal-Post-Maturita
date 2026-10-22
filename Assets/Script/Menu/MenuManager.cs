using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public partial class MenuManager : MonoBehaviour
{
    private enum TerminalScreen
    {
        Splash,
        Main,
        Settings,
        InputBindings,
        NewGameConfirm,
        NewGameDifficulty,
        NewGameProfile,
        Loading
    }

    [Header("UI")]
    [SerializeField] private TMP_Text terminalText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text eventText;
    [SerializeField] private CanvasGroup terminalCanvasGroup;
    [SerializeField] private CanvasGroup loadingScanBars;
    [SerializeField] private SliderBar screenTransitionBar;

    [Header("FPS Display")]
    [SerializeField] private TMP_FontAsset fpsDisplayFont;
    [SerializeField, Min(8)] private int fpsDisplayFontSize = 22;
    [SerializeField] private Color fpsDisplayColor = new Color(0.66f, 1f, 0.78f, 0.95f);

    [Header("Audio")]
    [SerializeField] private AudioMixer targetMixer;
    [SerializeField] private AudioSource menuMusicSource;
    [SerializeField] private AudioClip menuMusic;
    [SerializeField, Range(0f, 1f)] private float menuMusicVolume = 1f;
    [SerializeField, Min(0.01f)] private float menuMusicFadeDuration = 1f;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip cursorBeep;
    [SerializeField] private AudioClip confirmBeep;
    [SerializeField] private AudioClip errorBeep;
    [SerializeField] private AudioClip staticBurst;
    [SerializeField, Range(0.85f, 1.15f)] private float cursorPitchMin = 0.96f;
    [SerializeField, Range(0.85f, 1.15f)] private float cursorPitchMax = 1.04f;
    [SerializeField] private string masterVolumeParam = "MasterVolume";
    [SerializeField] private string musicVolumeParam = "MusicVolume";
    [SerializeField] private string sfxVolumeParam = "SFXVolume";

    [Header("Timing")]
    [SerializeField] private float idleDelaySeconds = 20f;
    [SerializeField] private float idleMessageDuration = 2.2f;
    [SerializeField] private Vector2 loadingScanDurationRange = new Vector2(0.4f, 0.7f);
    [SerializeField] private float scanSweepDistance = 1200f;
    [SerializeField, Range(0.05f, 1.5f)] private float menuTransitionDuration = 0.35f;

    [Header("Scenes")]
    [SerializeField] private string newGameScene = "Game";
    [SerializeField] private string continueScene = "Game";

    [Header("Input")]
    [SerializeField] private float stickMoveThreshold = 0.55f;
    [SerializeField, Range(0.01f, 0.2f)] private float volumeStep = 0.05f;
    [SerializeField, Range(0.02f, 0.25f)] private float brightnessStep = 0.05f;
    [SerializeField, Range(0.05f, 0.25f)] private float renderScaleStep = 0.05f;
    [SerializeField, Range(0.02f, 0.3f)] private float sensitivityStep = 0.05f;
    [SerializeField, Range(0.1f, 0.6f)] private float holdRepeatDelay = 0.32f;
    [SerializeField, Range(0.03f, 0.2f)] private float holdRepeatInterval = 0.08f;

    private readonly string[] mainMenuItems = { "NEW GAME", "CONTINUE", "SETTINGS", "EXIT" };
    private readonly string[] settingsItems =
    {
        "MASTER VOLUME",
        "MUSIC VOLUME",
        "SFX VOLUME",
        "DISPLAY MODE",
        "RESOLUTION",
        "RENDER SCALE",
        "BRIGHTNESS",
        "INPUT MODE",
        "INPUT BINDINGS",
        "INVERT Y",
        "V SYNC",
        "FPS LIMIT",
        "FPS DISPLAY",
        "RESET DEFAULTS",
        "BACK"
    };

    private readonly int[] fpsLimitOptions = { 0, 30, 60, 90, 120, 144, 165, 240 };

    private readonly GameSettings.RebindableAction[] inputBindingActions =
    {
        GameSettings.RebindableAction.Interact,
        GameSettings.RebindableAction.Flashlight,
        GameSettings.RebindableAction.Crouch,
        GameSettings.RebindableAction.Sprint
    };

    private Resolution[] availableResolutions;
    private Volume runtimeVolume;
    private VolumeProfile runtimeVolumeProfile;
    private ColorAdjustments colorAdjustments;

    private TerminalScreen currentScreen = TerminalScreen.Splash;
    private int selectedIndex;
    private float lastInputTime;
    private bool idleMessageShown;
    private Coroutine blinkRoutine;
    private Coroutine eventRoutine;
    private int heldVerticalDirection;
    private float nextVerticalRepeatTime;
    private int heldHorizontalDirection;
    private float nextHorizontalRepeatTime;
    private Coroutine screenTransitionRoutine;
    private Coroutine menuMusicFadeRoutine;
    private bool screenTransitionPlaying;
    private bool waitingForBinding;
    private GameSettings.RebindableAction pendingBindingAction;
    private float bindingCaptureTime;
    private bool gameplayPreloadRequested;

    private void Start()
    {
        selectedIndex = 0;
        GameSettings.Load();
        GameSettings.ApplyRuntime();
        CacheAvailableResolutions();
        ApplyMixerVolumes();
        StartMenuMusic();
        SetupBrightnessControl();
        ApplyBrightness();
        FpsDisplay.EnsureExists(fpsDisplayFont, fpsDisplayFontSize, fpsDisplayColor);
        lastInputTime = Time.unscaledTime;
        PrepareIntroVisualState();
        UpdateStatusText();
        introRoutine = StartCoroutine(SplashRoutine());
        StartCoroutine(PreloadGameplaySceneAfterStartup());
    }

    private void Update()
    {
        if (introPlaying && WasIntroSkipPressed())
        {
            skipIntroRequested = true;
        }

        UpdateStatusText();
        if (currentScreen == TerminalScreen.Splash || currentScreen == TerminalScreen.Loading || screenTransitionPlaying)
        {
            return;
        }

        HandleNavigationInput();
        if (currentScreen != TerminalScreen.NewGameProfile &&
            currentScreen != TerminalScreen.NewGameConfirm &&
            currentScreen != TerminalScreen.NewGameDifficulty)
        {
            HandleIdleStatusMessage();
        }
    }

    private IEnumerator SplashRoutine()
    {
        currentScreen = TerminalScreen.Splash;
        introPlaying = true;
        skipIntroRequested = false;
        eventText.text = string.Empty;

        yield return StartCoroutine(PlayIntroSequence());

        introPlaying = false;
        introRoutine = null;
    }
}
