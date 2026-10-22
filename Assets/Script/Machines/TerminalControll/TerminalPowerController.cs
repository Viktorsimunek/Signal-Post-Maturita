using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public partial class TerminalPowerController : MonoBehaviour
{
    private enum PowerState
    {
        Off,
        Booting,
        Ready,
        ShuttingDown
    }

    private enum ReadyView
    {
        Menu,
        Status,
        Logs,
        SignalHistory,
        PersonDatabase,
        Transmit,
        PowerConfirm
    }

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Core")]
    [SerializeField] private FuseSwitch powerFuse;
    [SerializeField] private TerminalControl terminalControl;
    [SerializeField] private TimeOfDayController timeOfDayController;
    [SerializeField] private Canvas screenCanvas;
    [SerializeField] private TMP_Text mainText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text eventText;

    [Header("Optional Output Pipeline")]
    [SerializeField] private CameraToTextureWithShader monitorOutput;
    [SerializeField] private Camera sourceCamera;
    [SerializeField] private Light terminalPowerLight;
    [SerializeField] private Behaviour[] enableWhilePowered;
    [SerializeField] private GameObject[] activateWhilePowered;

    [Header("Monitor Material")]
    [SerializeField] private Renderer monitorRenderer;
    [SerializeField, Min(0)] private int materialIndex;
    [SerializeField] private Texture poweredBaseTexture;
    [SerializeField] private Texture offBaseTexture;
    [SerializeField] private Texture poweredEmissionTexture;
    [SerializeField] private Texture offEmissionTexture;
    [SerializeField] private Color poweredBaseColor = Color.white;
    [SerializeField] private Color offBaseColor = Color.black;
    [SerializeField] private Color poweredEmissionColor = Color.white;
    [SerializeField] private Color offEmissionColor = Color.black;
    [SerializeField] private bool alsoWriteMainTex = true;

    [Header("Screen Effects")]
    [SerializeField] private CanvasGroup terminalCanvasGroup;
    [SerializeField] private SliderBar screenTransitionBar;
    [SerializeField, Range(0.02f, 0.25f)] private float blinkDuration = 0.08f;
    [SerializeField, Range(0f, 1f)] private float blinkMinAlpha = 0.6f;
    [SerializeField, Range(0.05f, 1.5f)] private float screenTransitionDuration = 0.35f;
    [SerializeField] private bool playTransitionOnBoot = true;
    [SerializeField] private bool playTransitionOnShutdown = true;

    [Header("Audio")]
    [SerializeField] private AudioSource uiAudioSource;
    [SerializeField] private AudioSource ambientAudioSource;
    [SerializeField] private AudioClip cursorBeep;
    [SerializeField] private AudioClip confirmBeep;
    [SerializeField] private AudioClip errorBeep;
    [SerializeField] private AudioClip startupClip;
    [SerializeField] private AudioClip startupNoiseClip;
    [SerializeField] private AudioClip shutdownClip;
    [SerializeField] private AudioClip staticBurst;
    [SerializeField] private AudioClip ambientLoopClip;
    [SerializeField] private float cursorVolume = 1f;
    [SerializeField] private float confirmVolume = 1f;
    [SerializeField] private float errorVolume = 1f;
    [SerializeField] private float startupVolume = 1f;
    [SerializeField] private float startupNoiseVolume = 0.85f;
    [SerializeField] private float shutdownVolume = 1f;
    [SerializeField] private float staticVolume = 0.85f;
    [SerializeField] private float ambientVolume = 0.2f;
    [SerializeField] private Vector2 cursorPitchRange = new Vector2(0.96f, 1.04f);
    [SerializeField] private Vector2 startupPitchRange = new Vector2(0.98f, 1.02f);
    [SerializeField] private Vector2 shutdownPitchRange = new Vector2(0.96f, 1.04f);
    [SerializeField] private float ambientFadeInDuration = 0.35f;
    [SerializeField] private float ambientFadeOutDuration = 0.18f;
    [SerializeField, Range(0f, 1f)] private float audioSpatialBlend = 1f;

    [Header("Boot")]
    [SerializeField] private bool autoPowerOnWhenFocused = true;
    [SerializeField] private bool autoPowerOffWhenFocusEnds;
    [SerializeField] private string splashText = "BORDER RELAY TERMINAL";
    [SerializeField] private string bootCompleteText = "LINK ESTABLISHED";
    [SerializeField] private string[] loadingMessages =
    {
        "INITIALIZING CRT BUS...",
        "CHECKING RELAY PATH...",
        "SYNCING TERMINAL INTERFACE..."
    };
    [SerializeField, Range(0.005f, 0.08f)] private float typeDelay = 0.015f;
    [SerializeField] private Vector2 loadingStepDurationRange = new Vector2(0.35f, 0.65f);
    [SerializeField, Range(0f, 2f)] private float splashHoldDuration = 0.25f;
    [SerializeField, Range(0f, 2f)] private float bootCompleteHoldDuration = 0.2f;
    [SerializeField, Range(0f, 2f)] private float shutdownHoldDuration = 0.3f;

    [Header("Ready UI")]
    [SerializeField] private string menuTitle = "BORDER RELAY TERMINAL";
    [SerializeField] private string relayNodeId = "NODE 04";
    [SerializeField] private string channelLabel = "CHANNEL: LOCKED";
    [SerializeField] private string idleOperatorLabel = "AWAITING OPERATOR";
    [SerializeField, Range(0f, 0.25f)] private float dynamicUiRefreshInterval = 0.033f;
    [SerializeField] private int maxRuntimeLogEntries = 8;
    [SerializeField] private string[] readyMenuItems =
    {
        "CHECK STATUS",
        "SEND MESSAGE",
        "SIGNAL HISTORY",
        "OPEN LOGS",
        "POWER DOWN"
    };
    [SerializeField] private string statusBlock =
        "SIGNAL STATUS: STABLE\n" +
        "NODE 04\n" +
        "CHANNEL: LOCKED";
    [SerializeField] private string logsBlock =
        "RECENT LOGS\n\n" +
        "[22:14] RELAY LINK STABLE\n" +
        "[22:09] NO FOREIGN TRAFFIC";
    [SerializeField] private string menuFooterBlock = "W/S NAV  ENTER SELECT  E/ESC EXIT";
    [SerializeField] private string pageFooterBlock = "ENTER/BACKSPACE BACK  E/ESC EXIT";

    [Header("Person Database")]
    [SerializeField] private TextAsset peopleDatabaseJson;
    [SerializeField] private string peopleDatabaseResourcesName = "PeopleDatabase";
    [SerializeField, Range(0.1f, 5f)] private float peopleDatabaseLookupDuration = 1.4f;
    [SerializeField] private int maxPersonCodeLength = 12;

    [Header("Message Sending")]
    [SerializeField] private MessageTransmitter messageTransmitter;
    [SerializeField] private int maxTypedMessageLength = 80;
    [SerializeField] private int maxTypedCodeLength = 16;
    [SerializeField] private bool showTransmitOrderOnTerminal;
    [SerializeField] private string transmitMessageFooterBlock = "ARROWS SELECT  ENTER PRESS  BACKSPACE MENU";
    [SerializeField] private string transmitCheckFooterBlock = "ENTER NEXT  BACKSPACE PREV";
    [SerializeField] private string transmitCodeFooterBlock = "ARROWS SELECT  ENTER PRESS  BACKSPACE PREV";
    [SerializeField] private string transmitSendingFooterBlock = "TRANSMITTING... KEEP SYSTEM STABLE";

    private Coroutine powerRoutine;
    private Coroutine blinkRoutine;
    private Coroutine eventRoutine;
    private Coroutine ambientFadeRoutine;
    private Coroutine personLookupRoutine;
    private MaterialPropertyBlock propertyBlock;
    private PowerState powerState = PowerState.Off;
    private ReadyView currentView = ReadyView.Menu;
    private int selectedMenuIndex;
    private int powerConfirmIndex = 1;
    private bool[] cachedActiveStates = Array.Empty<bool>();
    private bool[] cachedBehaviourStates = Array.Empty<bool>();
    private readonly List<string> runtimeLogs = new List<string>();
    private string footerText = string.Empty;
    private string transientEventText = string.Empty;
    private string operatorLinkState = "OFFLINE";
    private float lastBootTimestamp;
    private float lastShutdownTimestamp;
    private bool hasBootTimestamp;
    private bool hasShutdownTimestamp;
    private float nextStatusRefreshTime;
    private string typedMessage = string.Empty;
    private string typedConfirmCode = string.Empty;
    private bool powerOffOnNextFocusExit;
    private TransmitPage transmitPage = TransmitPage.Message;
    private int transmitKeyboardRow;
    private int transmitKeyboardColumn;
    private string typedPersonCode = string.Empty;
    private int personKeyboardRow;
    private int personKeyboardColumn;
    private bool personLookupInProgress;
    private bool personLookupCompleted;
    private bool personLookupResultPageOpen;
    private float personLookupStartedAt;
    private PersonRecord currentPersonRecord;
    private string personLookupMessage = "ENTER PERSON CODE";

    public bool IsPowered => powerState != PowerState.Off;
    public bool IsReady => powerState == PowerState.Ready;
    public bool HasFusePower => (powerFuse == null || powerFuse.IsOn) && LightsPowerGrid.IsPowered;
    public bool ShowTransmitSymbol => currentView == ReadyView.Transmit &&
                                      transmitPage == TransmitPage.Sending &&
                                      messageTransmitter != null &&
                                      messageTransmitter.IsSending;

}
