using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class TerminalControl : MonoBehaviour, IInteractable
{
    [Header("Power")]
    [SerializeField] private FuseSwitch powerFuse;
    [SerializeField] private string noPowerPrompt = "Terminal bez proudu";

    [Header("Interaction")]
    [SerializeField] private bool canInteract = true;
    [SerializeField] private string interactionPrompt = "Use terminal";
    [SerializeField] private string exitPrompt = "Exit terminal";
    [SerializeField] private Transform focusPoint;
    [SerializeField] private bool allowInteractToExit = true;
    [SerializeField] private bool allowEscapeToExit = true;
    [SerializeField] private bool forceFlashlightOffWhileFocused = true;

    [Header("Focused Prompt")]
    [SerializeField] private bool showFocusedPrompt = true;
    [SerializeField] private TMP_Text focusedPromptLabel;
    [TextArea(1, 3)]
    [SerializeField] private string focusedPromptFormat = "[{0}] to exit";
    [SerializeField, Min(0.01f)] private float focusedPromptFadeSpeed = 14f;
    [SerializeField, Min(0.01f)] private float focusedPromptShuffleDuration = 0.16f;
    [SerializeField] private string focusedPromptShuffleCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    [Header("Transition")]
    [SerializeField, Min(0.05f)] private float enterDuration = 0.45f;
    [SerializeField, Min(0.05f)] private float exitDuration = 0.35f;
    [SerializeField] private Transform sceneTransferStartFocusPoint;
    [SerializeField, Min(0.05f)] private float sceneTransferFocusDuration = 1.35f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Focused State")]
    [SerializeField] private GameObject[] activateWhileFocused;
    [SerializeField] private Behaviour[] enableWhileFocused;
    [SerializeField] private Behaviour[] disableWhileFocused;

    [Header("Events")]
    [SerializeField] private UnityEvent onFocusEntered;
    [SerializeField] private UnityEvent onFocusExited;

    private PlayerInteractor activeInteractor;
    private FirstPersonController activeController;
    private FlashlightController activeFlashlight;
    private Coroutine transitionRoutine;
    private bool isFocused;
    private bool isTransitioning;
    private float focusedPromptVisibility;
    private bool focusedPromptVisible;
    private string focusedPromptTargetText = string.Empty;
    private float focusedPromptShuffleElapsed;
    private bool isShufflingFocusedPrompt;
    private readonly StringBuilder focusedPromptBuilder = new StringBuilder(64);

    private Vector3 cachedPlayerPosition;
    private Quaternion cachedPlayerRotation;
    private float cachedPlayerPitch;
    private bool cachedMovementEnabled;
    private bool cachedLookEnabled;
    private bool cachedCameraEffectsEnabled;
    private bool cachedCursorLocked;
    private bool cachedCursorStateManaged;
    private bool cachedCharacterControllerEnabled;
    private bool cachedInteractionEnabled;
    private bool cachedFlashlightInputEnabled;
    private bool forcedFocusPowerOverride;
    private bool[] cachedActiveStates;
    private bool[] cachedEnableStates;
    private bool[] cachedDisableStates;

    public event Action<TerminalControl> FocusEntered;
    public event Action<TerminalControl> FocusExited;

    public bool IsFocused => isFocused;
    public bool IsTransitioning => isTransitioning;
    public bool BlockExitInput { get; set; }
    public bool BlockInteractExitInput { get; set; }
    public bool HasPower => forcedFocusPowerOverride || ((powerFuse == null || powerFuse.IsOn) && LightsPowerGrid.IsPowered);
    public Transform FocusPoint => focusPoint != null ? focusPoint : transform;

    private void Awake()
    {
        HideFocusedPromptImmediately();
    }

    private void OnEnable()
    {
        if (powerFuse != null)
        {
            powerFuse.PowerChanged -= OnFuseChanged;
            powerFuse.PowerChanged += OnFuseChanged;
        }
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        if (!canInteract || !isActiveAndEnabled || isTransitioning)
        {
            return false;
        }

        return !isFocused || interactor == activeInteractor;
    }

    public string GetInteractionPrompt(PlayerInteractor interactor)
    {
        if (!HasPower && !isFocused)
        {
            return noPowerPrompt;
        }

        if (isFocused && interactor == activeInteractor)
        {
            return exitPrompt;
        }

        return interactionPrompt;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        if (isFocused)
        {
            RequestExitFocus();
            return;
        }

        if (!HasPower)
        {
            return;
        }

        BeginFocus(interactor);
    }

    public void ExitFocus()
    {
        RequestExitFocus();
    }

    public void PlaceControllerAtFocusPoint(FirstPersonController controller)
    {
        PlaceControllerAtFocusPoint(controller, FocusPoint);
    }

    private void PlaceControllerAtFocusPoint(FirstPersonController controller, Transform target)
    {
        if (controller == null)
        {
            return;
        }

        if (target == null)
        {
            target = transform;
        }

        Quaternion targetRotation = GetFocusRootRotation(target);
        Vector3 targetPosition = GetFocusRootPosition(target, controller);
        float targetPitch = GetFocusPitch(target);

        controller.SetCharacterControllerEnabled(false);
        controller.transform.SetPositionAndRotation(targetPosition, targetRotation);
        controller.SetLookRotation(targetRotation.eulerAngles.y, targetPitch, true);
        controller.ResetMotionState();
        controller.SetCharacterControllerEnabled(true);
    }

    public void ForceFocusAtTerminal(FirstPersonController controller)
    {
        if (controller == null)
        {
            return;
        }

        forcedFocusPowerOverride = true;
        PlayerInteractor interactor = controller.GetComponent<PlayerInteractor>();
        activeController = controller;
        activeInteractor = interactor;
        activeFlashlight = controller.GetComponentInChildren<FlashlightController>(true);

        CachePlayerState();
        ApplyFocusedObjectState(true);
        PlaceControllerAtFocusPoint(controller, sceneTransferStartFocusPoint != null ? sceneTransferStartFocusPoint : FocusPoint);

        controller.SetMovementEnabled(false);
        controller.SetLookEnabled(false);
        controller.SetCameraEffectsEnabled(false);
        controller.SetCursorLockState(true, false);

        if (interactor != null)
        {
            interactor.SetInteractionEnabled(false);
        }

        if (forceFlashlightOffWhileFocused && activeFlashlight != null)
        {
            activeFlashlight.SetInputEnabled(false);
            activeFlashlight.SetFlashlightEnabled(false, true);
        }

        isFocused = true;
        UpdateFocusedPrompt();

        if (sceneTransferStartFocusPoint != null)
        {
            isTransitioning = true;
            transitionRoutine = StartCoroutine(ForcedFocusMoveRoutine());
        }
        else
        {
            isTransitioning = false;
        }
    }

    private void Update()
    {
        if (isFocused && !HasPower)
        {
            RequestExitFocus();
        }

        if (isFocused && !isTransitioning)
        {
            UpdateFocusedPrompt();

            if (ShouldExitFocusThisFrame())
            {
                RequestExitFocus();
            }
        }

        UpdateFocusedPromptVisuals();
    }

    private void OnDisable()
    {
        if (powerFuse != null)
        {
            powerFuse.PowerChanged -= OnFuseChanged;
        }

        ForceRestorePlayerState();
        HideFocusedPromptImmediately();
    }

    private void OnDestroy()
    {
        ForceRestorePlayerState();
        HideFocusedPromptImmediately();
    }

    private void BeginFocus(PlayerInteractor interactor)
    {
        if (!HasPower)
        {
            return;
        }

        if (interactor == null)
        {
            return;
        }

        FirstPersonController controller = interactor.GetComponent<FirstPersonController>();
        if (controller == null)
        {
            return;
        }

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
        }

        activeInteractor = interactor;
        activeController = controller;
        activeFlashlight = controller.GetComponentInChildren<FlashlightController>(true);

        CachePlayerState();
        ApplyFocusedObjectState(true);
        transitionRoutine = StartCoroutine(TransitionRoutine(true));
    }

    private void OnFuseChanged(FuseSwitch fuse, bool powerOn)
    {
        if (!powerOn && isFocused)
        {
            RequestExitFocus();
        }
    }

    private void RequestExitFocus()
    {
        if (!isFocused || isTransitioning || activeController == null)
        {
            return;
        }

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
        }

        transitionRoutine = StartCoroutine(TransitionRoutine(false));
    }

    private IEnumerator TransitionRoutine(bool entering)
    {
        isTransitioning = true;

        Transform playerTransform = activeController.transform;
        Vector3 startPosition = playerTransform.position;
        Quaternion startRotation = playerTransform.rotation;
        float startPitch = activeController.CurrentPitch;

        Vector3 targetPosition = entering ? GetFocusRootPosition(FocusPoint, activeController) : cachedPlayerPosition;
        Quaternion targetRotation = entering ? GetFocusRootRotation(FocusPoint) : cachedPlayerRotation;
        float targetPitch = entering ? GetFocusPitch(FocusPoint) : cachedPlayerPitch;
        float duration = Mathf.Max(0.01f, entering ? enterDuration : exitDuration);

        activeController.ResetMotionState();
        activeController.SetMovementEnabled(false);
        activeController.SetLookEnabled(false);
        activeController.SetCameraEffectsEnabled(false);
        activeController.SetCursorLockState(true, false);
        activeController.SetCharacterControllerEnabled(false);

        if (forceFlashlightOffWhileFocused && activeFlashlight != null)
        {
            activeFlashlight.SetInputEnabled(false);
            activeFlashlight.SetFlashlightEnabled(false, true);
        }

        if (activeInteractor != null)
        {
            activeInteractor.SetInteractionEnabled(false);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float easedTime = transitionCurve != null ? transitionCurve.Evaluate(normalizedTime) : normalizedTime;

            Quaternion blendedRotation = Quaternion.Slerp(startRotation, targetRotation, easedTime);
            float blendedPitch = Mathf.LerpAngle(startPitch, targetPitch, easedTime);

            playerTransform.position = Vector3.Lerp(startPosition, targetPosition, easedTime);
            playerTransform.rotation = blendedRotation;
            activeController.SetLookRotation(blendedRotation.eulerAngles.y, blendedPitch, true);
            yield return null;
        }

        playerTransform.SetPositionAndRotation(targetPosition, targetRotation);
        activeController.SetLookRotation(targetRotation.eulerAngles.y, targetPitch, true);

        if (entering)
        {
            isFocused = true;
            activeController.SetCursorLockState(true, false);
            UpdateFocusedPrompt();
            onFocusEntered?.Invoke();
            FocusEntered?.Invoke(this);
        }
        else
        {
            ClearFocusedPrompt();
            isFocused = false;
            forcedFocusPowerOverride = false;
            RestorePlayerState();
            onFocusExited?.Invoke();
            FocusExited?.Invoke(this);
        }

        isTransitioning = false;
        transitionRoutine = null;
    }

    private IEnumerator ForcedFocusMoveRoutine()
    {
        if (activeController == null)
        {
            isTransitioning = false;
            transitionRoutine = null;
            yield break;
        }

        Transform playerTransform = activeController.transform;
        Vector3 startPosition = playerTransform.position;
        Quaternion startRotation = playerTransform.rotation;
        float startPitch = activeController.CurrentPitch;

        Vector3 targetPosition = GetFocusRootPosition(FocusPoint, activeController);
        Quaternion targetRotation = GetFocusRootRotation(FocusPoint);
        float targetPitch = GetFocusPitch(FocusPoint);
        float duration = Mathf.Max(0.01f, sceneTransferFocusDuration);

        activeController.SetCharacterControllerEnabled(false);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float easedTime = transitionCurve != null ? transitionCurve.Evaluate(normalizedTime) : normalizedTime;

            Quaternion blendedRotation = Quaternion.Slerp(startRotation, targetRotation, easedTime);
            float blendedPitch = Mathf.LerpAngle(startPitch, targetPitch, easedTime);

            playerTransform.position = Vector3.Lerp(startPosition, targetPosition, easedTime);
            playerTransform.rotation = blendedRotation;
            activeController.SetLookRotation(blendedRotation.eulerAngles.y, blendedPitch, true);
            yield return null;
        }

        playerTransform.SetPositionAndRotation(targetPosition, targetRotation);
        activeController.SetLookRotation(targetRotation.eulerAngles.y, targetPitch, true);
        activeController.SetCharacterControllerEnabled(true);

        isTransitioning = false;
        transitionRoutine = null;
        UpdateFocusedPrompt();
    }

    private void CachePlayerState()
    {
        if (activeController == null)
        {
            return;
        }

        cachedPlayerPosition = activeController.transform.position;
        cachedPlayerRotation = activeController.transform.rotation;
        cachedPlayerPitch = activeController.CurrentPitch;
        cachedMovementEnabled = activeController.IsMovementEnabled;
        cachedLookEnabled = activeController.IsLookEnabled;
        cachedCameraEffectsEnabled = activeController.AreCameraEffectsEnabled;
        cachedCursorLocked = activeController.IsCursorLocked;
        cachedCursorStateManaged = activeController.IsCursorStateManaged;
        cachedCharacterControllerEnabled = activeController.IsCharacterControllerEnabled;
        cachedInteractionEnabled = activeInteractor != null && activeInteractor.IsInteractionEnabled;
        cachedFlashlightInputEnabled = activeFlashlight != null && activeFlashlight.IsInputEnabled;

        cachedActiveStates = CacheActiveStates(activateWhileFocused);
        cachedEnableStates = CacheEnabledStates(enableWhileFocused);
        cachedDisableStates = CacheEnabledStates(disableWhileFocused);
    }

    private void RestorePlayerState()
    {
        RestoreFocusedObjectState();

        if (activeController != null)
        {
            activeController.ResetMotionState();
            activeController.SetMovementEnabled(cachedMovementEnabled);
            activeController.SetLookEnabled(cachedLookEnabled);
            activeController.SetCameraEffectsEnabled(cachedCameraEffectsEnabled);
            activeController.SetCharacterControllerEnabled(cachedCharacterControllerEnabled);
            activeController.SetCursorLockState(cachedCursorLocked, cachedCursorStateManaged);
        }

        if (activeInteractor != null)
        {
            activeInteractor.SetInteractionEnabled(cachedInteractionEnabled);
        }

        if (activeFlashlight != null)
        {
            activeFlashlight.SetInputEnabled(cachedFlashlightInputEnabled);
        }

        ClearFocusedPrompt();
        activeFlashlight = null;
        activeController = null;
        activeInteractor = null;
    }

    private void ForceRestorePlayerState()
    {
        bool shouldNotifyExit = isFocused;

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        if (activeController != null)
        {
            activeController.transform.SetPositionAndRotation(cachedPlayerPosition, cachedPlayerRotation);
            activeController.SetLookRotation(cachedPlayerRotation.eulerAngles.y, cachedPlayerPitch, true);
        }

        isFocused = false;
        isTransitioning = false;
        forcedFocusPowerOverride = false;
        RestorePlayerState();

        if (shouldNotifyExit)
        {
            onFocusExited?.Invoke();
            FocusExited?.Invoke(this);
        }
    }

    private void ApplyFocusedObjectState(bool focused)
    {
        SetGameObjectsActive(activateWhileFocused, focused);
        SetBehavioursEnabled(enableWhileFocused, focused);
        SetBehavioursEnabled(disableWhileFocused, !focused);
    }

    private void RestoreFocusedObjectState()
    {
        RestoreGameObjectStates(activateWhileFocused, cachedActiveStates);
        RestoreBehaviourStates(enableWhileFocused, cachedEnableStates);
        RestoreBehaviourStates(disableWhileFocused, cachedDisableStates);
    }

    private bool ShouldExitFocusThisFrame()
    {
        if (BlockExitInput)
        {
            return false;
        }

        if (allowInteractToExit && !BlockInteractExitInput && WasInteractPressedThisFrame())
        {
            return true;
        }

        if (allowEscapeToExit && WasCancelPressedThisFrame())
        {
            return true;
        }

        return false;
    }

    private void UpdateFocusedPrompt()
    {
        if (!showFocusedPrompt || focusedPromptLabel == null)
        {
            return;
        }

        string exitDisplay = BlockInteractExitInput ? "Esc" : InputPromptDisplay.GetExitDisplay(activeInteractor, activeInteractor);
        SetFocusedPromptTarget(string.Format(focusedPromptFormat, exitDisplay));
        focusedPromptVisible = true;
    }

    private void ClearFocusedPrompt()
    {
        focusedPromptVisible = false;
    }

    private void SetFocusedPromptTarget(string nextPromptText)
    {
        if (nextPromptText == null)
        {
            nextPromptText = string.Empty;
        }

        if (focusedPromptTargetText == nextPromptText)
        {
            return;
        }

        focusedPromptTargetText = nextPromptText;
        focusedPromptShuffleElapsed = 0f;
        isShufflingFocusedPrompt = !string.IsNullOrEmpty(focusedPromptTargetText);
    }

    private void UpdateFocusedPromptVisuals()
    {
        if (focusedPromptLabel == null)
        {
            return;
        }

        float targetVisibility = focusedPromptVisible ? 1f : 0f;
        focusedPromptVisibility = Mathf.MoveTowards(focusedPromptVisibility, targetVisibility, Time.unscaledDeltaTime * focusedPromptFadeSpeed);
        focusedPromptLabel.alpha = focusedPromptVisibility;
        focusedPromptLabel.enabled = focusedPromptVisibility > 0.001f || focusedPromptVisible;

        if (!focusedPromptVisible)
        {
            if (focusedPromptVisibility <= 0.001f && focusedPromptLabel.text.Length > 0)
            {
                focusedPromptLabel.text = string.Empty;
                focusedPromptTargetText = string.Empty;
                isShufflingFocusedPrompt = false;
            }

            return;
        }

        if (isShufflingFocusedPrompt)
        {
            focusedPromptShuffleElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(focusedPromptShuffleElapsed / focusedPromptShuffleDuration);
            focusedPromptLabel.text = BuildFocusedPromptScramble(focusedPromptTargetText, progress);

            if (progress >= 1f)
            {
                isShufflingFocusedPrompt = false;
                focusedPromptLabel.text = focusedPromptTargetText;
            }

            return;
        }

        if (focusedPromptLabel.text != focusedPromptTargetText)
        {
            focusedPromptLabel.text = focusedPromptTargetText;
        }
    }

    private string BuildFocusedPromptScramble(string targetText, float progress)
    {
        if (string.IsNullOrEmpty(targetText))
        {
            return string.Empty;
        }

        if (progress >= 1f || string.IsNullOrEmpty(focusedPromptShuffleCharacters))
        {
            return targetText;
        }

        int revealCount = Mathf.FloorToInt(targetText.Length * progress);
        focusedPromptBuilder.Clear();

        for (int i = 0; i < targetText.Length; i++)
        {
            char targetCharacter = targetText[i];
            if (i < revealCount || ShouldKeepFocusedPromptCharacterVisible(targetCharacter))
            {
                focusedPromptBuilder.Append(targetCharacter);
                continue;
            }

            int shuffleIndex = Mathf.Abs((Time.frameCount * 17) + (i * 31)) % focusedPromptShuffleCharacters.Length;
            focusedPromptBuilder.Append(focusedPromptShuffleCharacters[shuffleIndex]);
        }

        return focusedPromptBuilder.ToString();
    }

    private static bool ShouldKeepFocusedPromptCharacterVisible(char value)
    {
        return char.IsWhiteSpace(value) || char.IsPunctuation(value) || char.IsSymbol(value);
    }

    private void HideFocusedPromptImmediately()
    {
        focusedPromptVisible = false;
        focusedPromptVisibility = 0f;
        focusedPromptTargetText = string.Empty;
        focusedPromptShuffleElapsed = 0f;
        isShufflingFocusedPrompt = false;

        if (focusedPromptLabel == null)
        {
            return;
        }

        focusedPromptLabel.alpha = 0f;
        focusedPromptLabel.text = string.Empty;
        focusedPromptLabel.enabled = false;
    }

    private Vector3 GetFocusRootPosition(Transform target, FirstPersonController controller)
    {
        if (target == null)
        {
            target = transform;
        }

        Quaternion yawRotation = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
        return target.position - (yawRotation * controller.CameraNeutralLocalPosition);
    }

    private Quaternion GetFocusRootRotation(Transform target)
    {
        if (target == null)
        {
            target = transform;
        }

        return Quaternion.Euler(0f, target.eulerAngles.y, 0f);
    }

    private float GetFocusPitch(Transform target)
    {
        if (target == null)
        {
            target = transform;
        }

        return NormalizeAngle(target.eulerAngles.x);
    }

    private static bool[] CacheActiveStates(GameObject[] targets)
    {
        if (targets == null)
        {
            return null;
        }

        bool[] states = new bool[targets.Length];
        for (int i = 0; i < targets.Length; i++)
        {
            states[i] = targets[i] != null && targets[i].activeSelf;
        }

        return states;
    }

    private static bool[] CacheEnabledStates(Behaviour[] targets)
    {
        if (targets == null)
        {
            return null;
        }

        bool[] states = new bool[targets.Length];
        for (int i = 0; i < targets.Length; i++)
        {
            states[i] = targets[i] != null && targets[i].enabled;
        }

        return states;
    }

    private static void SetGameObjectsActive(GameObject[] targets, bool active)
    {
        if (targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
            {
                targets[i].SetActive(active);
            }
        }
    }

    private static void SetBehavioursEnabled(Behaviour[] targets, bool enabled)
    {
        if (targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
            {
                targets[i].enabled = enabled;
            }
        }
    }

    private static void RestoreGameObjectStates(GameObject[] targets, bool[] states)
    {
        if (targets == null || states == null)
        {
            return;
        }

        int count = Mathf.Min(targets.Length, states.Length);
        for (int i = 0; i < count; i++)
        {
            if (targets[i] != null)
            {
                targets[i].SetActive(states[i]);
            }
        }
    }

    private static void RestoreBehaviourStates(Behaviour[] targets, bool[] states)
    {
        if (targets == null || states == null)
        {
            return;
        }

        int count = Mathf.Min(targets.Length, states.Length);
        for (int i = 0; i < count; i++)
        {
            if (targets[i] != null)
            {
                targets[i].enabled = states[i];
            }
        }
    }

    private static bool WasInteractPressedThisFrame()
    {
        bool keyboardPressed = GameSettings.AllowsKeyboardMouseInput() && GameSettings.WasKeyboardBindingPressed(GameSettings.RebindableAction.Interact);
        bool gamepadPressed = GameSettings.AllowsGamepadInput() && GameSettings.WasGamepadBindingPressed(GameSettings.RebindableAction.Interact);
        if (keyboardPressed) InputDeviceTracker.MarkKeyboardMouse();
        if (gamepadPressed) InputDeviceTracker.MarkGamepad();
        return keyboardPressed || gamepadPressed;
    }

    private static bool WasCancelPressedThisFrame()
    {
        bool keyboardPressed = GameSettings.AllowsKeyboardMouseInput() && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool gamepadPressed = GameSettings.AllowsGamepadInput() && Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
        if (keyboardPressed) InputDeviceTracker.MarkKeyboardMouse();
        if (gamepadPressed) InputDeviceTracker.MarkGamepad();
        return keyboardPressed || gamepadPressed;
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f)
        {
            angle -= 360f;
        }

        while (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
    }

    private void OnDrawGizmosSelected()
    {
        Transform target = focusPoint != null ? focusPoint : transform;
        Gizmos.color = new Color(0.1f, 1f, 0.7f, 0.9f);
        Gizmos.DrawWireSphere(target.position, 0.04f);
        Gizmos.DrawRay(target.position, target.forward * 0.3f);

        if (sceneTransferStartFocusPoint != null)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(sceneTransferStartFocusPoint.position, 0.035f);
            Gizmos.DrawRay(sceneTransferStartFocusPoint.position, sceneTransferStartFocusPoint.forward * 0.25f);
        }
    }
}
