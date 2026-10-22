using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraRoot;
    [SerializeField] private InputActionReference crouchActionReference;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 2.15f;
    [SerializeField] private float briskSpeed = 3.05f;
    [SerializeField] private float acceleration = 7.5f;
    [SerializeField] private float deceleration = 9.5f;
    [SerializeField] private float gravity = -22f;
    [SerializeField] private float groundedStickForce = -2f;

    [Header("Crouch")]
    [SerializeField] private float crouchSpeed = 1.35f;
    [SerializeField] private float crouchHeight = 1.1f;
    [SerializeField] private float crouchCameraOffset = -0.45f;
    [SerializeField] private float crouchTransitionSpeed = 8f;
    [SerializeField] private LayerMask crouchObstructionMask = ~0;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float gamepadSensitivity = 150f;
    [SerializeField] private float lookSmoothTime = 0.03f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Camera Feel")]
    [SerializeField] private float moveTilt = 1.25f;
    [SerializeField] private float swayAmount = 1.1f;
    [SerializeField] private float swaySmooth = 10f;
    [SerializeField] private float bobAmplitude = 0.012f;
    [SerializeField] private float bobFrequency = 1.4f;
    [SerializeField] private float idleBreathAmplitude = 0.006f;
    [SerializeField] private float idleBreathFrequency = 0.9f;

    private CharacterController characterController;
    private InputAction crouchAction;
    private InputAction fallbackCrouchAction;
    private Vector3 planarVelocity;
    private float verticalVelocity;
    private float targetYaw;
    private float smoothedYaw;
    private float targetPitch;
    private float smoothedPitch;
    private float yawVelocity;
    private float pitchVelocity;
    private Vector2 smoothedLookDelta;
    private Vector2 lookVelocity;
    private Vector3 cameraInitialLocalPosition;
    private Quaternion cameraInitialLocalRotation;
    private Vector3 standingControllerCenter;
    private float standingControllerHeight;
    private float standingStepOffset;
    private float currentCameraCrouchOffset;
    private float bobTime;
    private bool cursorLocked = true;
    private bool isCrouching;
    private bool movementEnabled = true;
    private bool lookEnabled = true;
    private bool cameraEffectsEnabled = true;
    private bool cursorStateManaged = true;
    private readonly Collider[] crouchOverlapResults = new Collider[8];

    public CharacterController Controller => characterController;
    public Transform CameraRoot => cameraRoot;
    public float PlanarSpeed => new Vector3(planarVelocity.x, 0f, planarVelocity.z).magnitude;
    public float WalkSpeed => walkSpeed;
    public float BriskSpeed => briskSpeed;
    public bool IsGrounded => characterController != null && characterController.isGrounded;
    public bool IsCursorLocked => cursorLocked;
    public bool IsMovementEnabled => movementEnabled;
    public bool IsLookEnabled => lookEnabled;
    public bool AreCameraEffectsEnabled => cameraEffectsEnabled;
    public bool IsCursorStateManaged => cursorStateManaged;
    public bool IsCharacterControllerEnabled => characterController != null && characterController.enabled;
    public bool IsCrouching => isCrouching;
    public float CurrentYaw => smoothedYaw;
    public float CurrentPitch => smoothedPitch;
    public Vector3 CameraNeutralLocalPosition => cameraInitialLocalPosition;

    public void Configure(Transform targetCamera)
    {
        cameraRoot = targetCamera;
    }

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        if (!enabled)
        {
            ResetMotionState();
        }
    }

    public void SetLookEnabled(bool enabled)
    {
        lookEnabled = enabled;
        if (!enabled)
        {
            smoothedLookDelta = Vector2.zero;
            lookVelocity = Vector2.zero;
        }
    }

    public void SetCameraEffectsEnabled(bool enabled)
    {
        cameraEffectsEnabled = enabled;
        if (!enabled)
        {
            bobTime = 0f;
            smoothedLookDelta = Vector2.zero;
        }
    }

    public void SetCursorLockState(bool shouldLock, bool allowAutomaticToggle)
    {
        cursorStateManaged = allowAutomaticToggle;
        SetCursorLock(shouldLock);
    }

    public void SetCharacterControllerEnabled(bool enabled)
    {
        if (characterController == null || characterController.enabled == enabled)
        {
            return;
        }

        characterController.enabled = enabled;
        if (!enabled)
        {
            ResetMotionState();
        }
    }

    public void SetLookRotation(float yaw, float pitch, bool snapInstantly)
    {
        targetYaw = yaw;
        targetPitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        if (snapInstantly)
        {
            smoothedYaw = targetYaw;
            smoothedPitch = targetPitch;
            yawVelocity = 0f;
            pitchVelocity = 0f;
        }

        transform.rotation = Quaternion.Euler(0f, smoothedYaw, 0f);
    }

    public void ResetMotionState()
    {
        planarVelocity = Vector3.zero;
        verticalVelocity = 0f;
        bobTime = 0f;
        smoothedLookDelta = Vector2.zero;
        lookVelocity = Vector2.zero;
    }

    private void Awake()
    {
        GameSettings.EnsureLoaded();

        characterController = GetComponent<CharacterController>();
        crouchAction = ResolveCrouchAction();
        standingControllerHeight = characterController.height;
        standingControllerCenter = characterController.center;
        standingStepOffset = characterController.stepOffset;

        if (cameraRoot == null && Camera.main != null)
        {
            cameraRoot = Camera.main.transform;
        }

        if (cameraRoot != null)
        {
            cameraInitialLocalPosition = cameraRoot.localPosition;
            Vector3 initialEuler = cameraRoot.localEulerAngles;
            targetPitch = NormalizeAngle(initialEuler.x);
            smoothedPitch = targetPitch;
            cameraInitialLocalRotation = Quaternion.Euler(0f, initialEuler.y, initialEuler.z);
        }

        targetYaw = transform.eulerAngles.y;
        smoothedYaw = targetYaw;
    }

    private void OnDisable()
    {
        crouchAction?.Disable();
        fallbackCrouchAction?.Disable();
        SetCursorLock(false);
    }

    private void OnEnable()
    {
        crouchAction?.Enable();
        fallbackCrouchAction?.Enable();
        SetCursorLock(true);
    }

    private void OnDestroy()
    {
        fallbackCrouchAction?.Dispose();
    }

    private void OnValidate()
    {
        walkSpeed = Mathf.Max(0f, walkSpeed);
        briskSpeed = Mathf.Max(0f, briskSpeed);
        acceleration = Mathf.Max(0f, acceleration);
        deceleration = Mathf.Max(0f, deceleration);
        crouchSpeed = Mathf.Max(0.1f, crouchSpeed);
        crouchHeight = Mathf.Max(0.5f, crouchHeight);
        crouchTransitionSpeed = Mathf.Max(0.01f, crouchTransitionSpeed);
    }

    private void Update()
    {
        if (cursorStateManaged)
        {
            HandleCursorState();
        }

        HandleLook();
        HandleCrouch();
        HandleMovement();
        UpdateCameraEffects();
    }

    private void HandleCursorState()
    {
        if (!GameSettings.AllowsKeyboardMouseInput() || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetCursorLock(false);
        }
        else if (!cursorLocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            SetCursorLock(true);
        }
    }

    private void SetCursorLock(bool shouldLock)
    {
        cursorLocked = shouldLock;
        Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !shouldLock;
    }

    private void HandleLook()
    {
        if (cameraRoot == null || !cursorLocked || !lookEnabled)
        {
            return;
        }

        Vector2 rawLook = ReadLookInput();
        smoothedLookDelta = Vector2.SmoothDamp(smoothedLookDelta, rawLook, ref lookVelocity, lookSmoothTime);

        targetYaw += rawLook.x;
        targetPitch = Mathf.Clamp(targetPitch - rawLook.y, minPitch, maxPitch);

        smoothedYaw = Mathf.SmoothDampAngle(smoothedYaw, targetYaw, ref yawVelocity, lookSmoothTime);
        smoothedPitch = Mathf.SmoothDampAngle(smoothedPitch, targetPitch, ref pitchVelocity, lookSmoothTime);

        transform.rotation = Quaternion.Euler(0f, smoothedYaw, 0f);
    }

    private Vector2 ReadLookInput()
    {
        Vector2 look = Vector2.zero;
        float yMultiplier = GameSettings.InvertY ? -1f : 1f;

        if (GameSettings.AllowsKeyboardMouseInput() && Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            if (mouseDelta.sqrMagnitude > 0.0001f)
            {
                InputDeviceTracker.MarkKeyboardMouse();
            }
            mouseDelta.y *= yMultiplier;
            look += mouseDelta * (mouseSensitivity * GameSettings.MouseSensitivity);
        }

        if (GameSettings.AllowsGamepadInput() && Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.rightStick.ReadValue();
            if (stick.sqrMagnitude > 0.0001f)
            {
                InputDeviceTracker.MarkGamepad();
            }
            stick.y *= yMultiplier;
            look += stick * (gamepadSensitivity * GameSettings.GamepadSensitivity * Time.deltaTime);
        }

        return look;
    }

    private void HandleCrouch()
    {
        if (characterController == null)
        {
            return;
        }

        float targetCrouchHeight = GetResolvedCrouchHeight();
        bool wantsCrouch = movementEnabled && IsCrouchPressed();
        bool shouldCrouch = wantsCrouch || (isCrouching && !CanStandUp());
        float targetHeight = shouldCrouch ? targetCrouchHeight : standingControllerHeight;
        float heightStep = crouchTransitionSpeed * standingControllerHeight * Time.deltaTime;
        float newHeight = Mathf.MoveTowards(characterController.height, targetHeight, heightStep);

        characterController.height = newHeight;
        characterController.center = GetControllerCenterForHeight(newHeight);
        characterController.stepOffset = shouldCrouch
            ? Mathf.Min(standingStepOffset, newHeight * 0.25f)
            : standingStepOffset;

        float targetCameraOffset = shouldCrouch ? crouchCameraOffset : 0f;
        float cameraStep = Mathf.Max(0.01f, Mathf.Abs(crouchCameraOffset) * crouchTransitionSpeed * Time.deltaTime);
        currentCameraCrouchOffset = Mathf.MoveTowards(currentCameraCrouchOffset, targetCameraOffset, cameraStep);
        isCrouching = shouldCrouch;
    }

    private void HandleMovement()
    {
        if (!movementEnabled || characterController == null || !characterController.enabled)
        {
            ResetMotionState();
            return;
        }

        Vector2 moveInput = ReadMoveInput();
        Vector3 desiredMove = (transform.forward * moveInput.y) + (transform.right * moveInput.x);
        desiredMove = Vector3.ClampMagnitude(desiredMove, 1f);

        float targetSpeed = isCrouching ? crouchSpeed : (IsBriskMoving() ? briskSpeed : walkSpeed);
        Vector3 targetVelocity = desiredMove * targetSpeed;

        float rate = desiredMove.sqrMagnitude > 0.001f ? acceleration : deceleration;
        planarVelocity = Vector3.MoveTowards(planarVelocity, targetVelocity, rate * Time.deltaTime);

        if (characterController.isGrounded)
        {
            verticalVelocity = groundedStickForce;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 motion = planarVelocity + (Vector3.up * verticalVelocity);
        characterController.Move(motion * Time.deltaTime);
    }

    private Vector2 ReadMoveInput()
    {
        Vector2 move = Vector2.zero;

        bool keyboardMoved = false;
        if (GameSettings.AllowsKeyboardMouseInput() && Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            {
                move.y += 1f;
                keyboardMoved = true;
            }

            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            {
                move.y -= 1f;
                keyboardMoved = true;
            }

            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            {
                move.x -= 1f;
                keyboardMoved = true;
            }

            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                move.x += 1f;
                keyboardMoved = true;
            }
        }

        if (keyboardMoved)
        {
            InputDeviceTracker.MarkKeyboardMouse();
        }

        if (GameSettings.AllowsGamepadInput() && Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.leftStick.ReadValue();
            if (stick.sqrMagnitude > 0.0001f)
            {
                InputDeviceTracker.MarkGamepad();
            }
            move += stick;
        }

        return Vector2.ClampMagnitude(move, 1f);
    }

    private bool IsCrouchPressed()
    {
        bool keyboardPressed = GameSettings.AllowsKeyboardMouseInput() && GameSettings.IsKeyboardBindingPressed(GameSettings.RebindableAction.Crouch);
        bool gamepadPressed = GameSettings.AllowsGamepadInput() && GameSettings.IsGamepadBindingPressed(GameSettings.RebindableAction.Crouch);
        if (keyboardPressed) InputDeviceTracker.MarkKeyboardMouse();
        if (gamepadPressed) InputDeviceTracker.MarkGamepad();
        return keyboardPressed || gamepadPressed;
    }

    private bool IsBriskMoving()
    {
        bool keyboardSprint = GameSettings.AllowsKeyboardMouseInput() && GameSettings.IsKeyboardBindingPressed(GameSettings.RebindableAction.Sprint);
        bool gamepadSprint = GameSettings.AllowsGamepadInput() && GameSettings.IsGamepadBindingPressed(GameSettings.RebindableAction.Sprint);
        if (keyboardSprint) InputDeviceTracker.MarkKeyboardMouse();
        if (gamepadSprint) InputDeviceTracker.MarkGamepad();
        return keyboardSprint || gamepadSprint;
    }

    private void UpdateCameraEffects()
    {
        if (cameraRoot == null)
        {
            return;
        }

        Vector3 localPosition = cameraInitialLocalPosition;
        localPosition.y += currentCameraCrouchOffset;
        Quaternion localRotation = Quaternion.Euler(smoothedPitch, 0f, 0f) * cameraInitialLocalRotation;

        if (!cameraEffectsEnabled)
        {
            bobTime = 0f;
            cameraRoot.localPosition = Vector3.Lerp(cameraRoot.localPosition, localPosition, Time.deltaTime * swaySmooth);
            cameraRoot.localRotation = localRotation;
            return;
        }

        float movementFactor = Mathf.InverseLerp(0f, briskSpeed, PlanarSpeed);

        if (movementFactor > 0.01f)
        {
            bobTime += Time.deltaTime * (bobFrequency + (movementFactor * 0.55f));
            localPosition.y += Mathf.Sin(bobTime * Mathf.PI * 2f) * bobAmplitude * movementFactor;
        }
        else
        {
            bobTime = 0f;
            localPosition.y += Mathf.Sin(Time.time * idleBreathFrequency * Mathf.PI * 2f) * idleBreathAmplitude;
        }

        float lateralTilt = 0f;
        if (GameSettings.AllowsKeyboardMouseInput() && Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed)
            {
                lateralTilt += moveTilt;
            }

            if (Keyboard.current.dKey.isPressed)
            {
                lateralTilt -= moveTilt;
            }
        }

        if (GameSettings.AllowsGamepadInput() && Gamepad.current != null)
        {
            lateralTilt -= Gamepad.current.leftStick.ReadValue().x * moveTilt;
        }

        float swayPitch = smoothedLookDelta.y * swayAmount * 0.35f;
        float swayRoll = (-smoothedLookDelta.x * swayAmount) + lateralTilt;
        Quaternion targetRotation = localRotation * Quaternion.Euler(swayPitch, 0f, swayRoll);

        cameraRoot.localPosition = Vector3.Lerp(cameraRoot.localPosition, localPosition, Time.deltaTime * swaySmooth);
        cameraRoot.localRotation = targetRotation;
    }

    private float GetResolvedCrouchHeight()
    {
        if (characterController == null)
        {
            return crouchHeight;
        }

        float minimumHeight = (characterController.radius * 2f) + 0.05f;
        return Mathf.Clamp(crouchHeight, minimumHeight, standingControllerHeight);
    }

    private Vector3 GetControllerCenterForHeight(float height)
    {
        float standingBottom = standingControllerCenter.y - (standingControllerHeight * 0.5f);
        Vector3 adjustedCenter = standingControllerCenter;
        adjustedCenter.y = standingBottom + (height * 0.5f);
        return adjustedCenter;
    }

    private bool CanStandUp()
    {
        if (characterController == null)
        {
            return true;
        }

        if (characterController.height >= standingControllerHeight - 0.001f)
        {
            return true;
        }

        float radius = Mathf.Max(0.01f, characterController.radius - characterController.skinWidth);
        float currentHalfHeight = Mathf.Max(characterController.height * 0.5f, radius);
        float standingHalfHeight = Mathf.Max(standingControllerHeight * 0.5f, radius);
        Vector3 currentWorldCenter = transform.TransformPoint(characterController.center);
        Vector3 standingWorldCenter = transform.TransformPoint(standingControllerCenter);
        Vector3 bottom = currentWorldCenter + (Vector3.up * (currentHalfHeight - radius));
        Vector3 top = standingWorldCenter + (Vector3.up * (standingHalfHeight - radius));

        int hitCount = Physics.OverlapCapsuleNonAlloc(
            bottom,
            top,
            radius,
            crouchOverlapResults,
            crouchObstructionMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = crouchOverlapResults[i];
            if (hitCollider == null)
            {
                continue;
            }

            if (hitCollider.transform.root == transform.root)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private InputAction ResolveCrouchAction()
    {
        if (crouchActionReference != null && crouchActionReference.action != null)
        {
            return crouchActionReference.action;
        }

        PlayerInput playerInput = GetComponent<PlayerInput>() ?? GetComponentInParent<PlayerInput>();
        if (playerInput != null)
        {
            InputAction playerAction = playerInput.actions?.FindAction("Crouch", false);
            if (playerAction != null)
            {
                return playerAction;
            }
        }

        fallbackCrouchAction = new InputAction("CrouchFallback", InputActionType.Button);
        fallbackCrouchAction.AddBinding("<Keyboard>/leftCtrl");
        fallbackCrouchAction.AddBinding("<Gamepad>/buttonEast");
        return fallbackCrouchAction;
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
}
