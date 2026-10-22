using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(FirstPersonController))]
public class PlayerInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FirstPersonController controller;
    [SerializeField] private Transform aimOrigin;
    [SerializeField] private InputActionReference interactActionReference;

    [Header("Detection")]
    [SerializeField] private LayerMask interactionMask = ~0;
    [SerializeField, Min(0.25f)] private float interactionDistance = 3f;
    [SerializeField, Min(0f)] private float interactionRadius = 0.08f;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Debug")]
    [SerializeField] private bool drawDebugRay;

    private InputAction interactAction;
    private InputAction fallbackInteractAction;
    private IInteractable currentInteractable;
    private RaycastHit currentHit;
    private bool hasCurrentHit;
    private bool interactionEnabled = true;

    public event Action<PlayerInteractor, IInteractable> InteractableChanged;

    public Transform AimOrigin => aimOrigin;
    public IInteractable CurrentInteractable => currentInteractable;
    public bool HasInteractable => currentInteractable != null;
    public bool HasCurrentHit => hasCurrentHit;
    public RaycastHit CurrentHit => currentHit;
    public string CurrentPrompt => currentInteractable != null ? currentInteractable.GetInteractionPrompt(this) : string.Empty;
    public bool IsInteractionEnabled => interactionEnabled;
    public string CurrentInteractionButtonText => GetInteractionButtonText();

    public void Configure(Transform targetAimOrigin)
    {
        aimOrigin = targetAimOrigin;
    }

    public bool TryGetCurrentInteractable(out IInteractable interactable)
    {
        interactable = currentInteractable;
        return interactable != null;
    }

    public bool TryGetCurrentHit(out RaycastHit hit)
    {
        hit = currentHit;
        return hasCurrentHit;
    }

    public void SetInteractionEnabled(bool enabled)
    {
        if (interactionEnabled == enabled)
        {
            return;
        }

        interactionEnabled = enabled;
        if (!interactionEnabled)
        {
            ClearCurrentInteractable();
        }
    }

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<FirstPersonController>();
        }

        ResolveAimOrigin();
        interactAction = ResolveInteractAction();
    }

    private void OnEnable()
    {
        ResolveAimOrigin();
        interactAction?.Enable();
        fallbackInteractAction?.Enable();
    }

    private void OnDisable()
    {
        interactAction?.Disable();
        fallbackInteractAction?.Disable();
        ClearCurrentInteractable();
    }

    private void OnDestroy()
    {
        fallbackInteractAction?.Dispose();
    }

    private void OnValidate()
    {
        interactionDistance = Mathf.Max(0.25f, interactionDistance);
        interactionRadius = Mathf.Max(0f, interactionRadius);

        if (controller == null)
        {
            controller = GetComponent<FirstPersonController>();
        }
    }

    private void Update()
    {
        if (!interactionEnabled)
        {
            ClearCurrentInteractable();
            return;
        }

        ResolveAimOrigin();
        UpdateCurrentInteractable();

        if (currentInteractable == null || !CanProcessInput() || !ShouldInteractThisFrame())
        {
            return;
        }

        currentInteractable.Interact(this);
        UpdateCurrentInteractable();
    }

    private void UpdateCurrentInteractable()
    {
        if (!TryFindInteractable(out IInteractable nextInteractable, out RaycastHit nextHit))
        {
            hasCurrentHit = false;
            currentHit = default;
            SetCurrentInteractable(null);
            return;
        }

        hasCurrentHit = true;
        currentHit = nextHit;
        SetCurrentInteractable(nextInteractable);
    }

    private bool TryFindInteractable(out IInteractable interactable, out RaycastHit hit)
    {
        interactable = null;
        hit = default;

        if (aimOrigin == null)
        {
            return false;
        }

        Vector3 origin = aimOrigin.position;
        Vector3 direction = aimOrigin.forward;
        bool didHit = interactionRadius > 0.001f
            ? Physics.SphereCast(origin, interactionRadius, direction, out hit, interactionDistance, interactionMask, triggerInteraction)
            : Physics.Raycast(origin, direction, out hit, interactionDistance, interactionMask, triggerInteraction);

        if (drawDebugRay)
        {
            Debug.DrawRay(origin, direction * interactionDistance, didHit ? Color.green : Color.red);
        }

        if (!didHit || hit.collider == null)
        {
            return false;
        }

        if (!TryResolveInteractable(hit.collider, out interactable))
        {
            interactable = null;
            return false;
        }

        if (!interactable.CanInteract(this))
        {
            interactable = null;
            return false;
        }

        return true;
    }

    private bool TryResolveInteractable(Collider targetCollider, out IInteractable interactable)
    {
        interactable = null;
        if (targetCollider == null)
        {
            return false;
        }

        MonoBehaviour[] behaviours = targetCollider.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || !behaviour.isActiveAndEnabled)
            {
                continue;
            }

            if (behaviour.transform.root == transform.root)
            {
                continue;
            }

            if (behaviour is IInteractable candidate)
            {
                interactable = candidate;
                return true;
            }
        }

        return false;
    }

    private void SetCurrentInteractable(IInteractable nextInteractable)
    {
        if (ReferenceEquals(currentInteractable, nextInteractable))
        {
            return;
        }

        currentInteractable = nextInteractable;
        InteractableChanged?.Invoke(this, currentInteractable);
    }

    private void ClearCurrentInteractable()
    {
        hasCurrentHit = false;
        currentHit = default;
        SetCurrentInteractable(null);
    }

    private void ResolveAimOrigin()
    {
        if (aimOrigin != null)
        {
            return;
        }

        if (controller != null && controller.CameraRoot != null)
        {
            aimOrigin = controller.CameraRoot;
            return;
        }

        if (Camera.main != null)
        {
            aimOrigin = Camera.main.transform;
        }
    }

    private bool CanProcessInput()
    {
        return controller == null || controller.IsCursorLocked;
    }

    private bool ShouldInteractThisFrame()
    {
        bool keyboardPressed = GameSettings.AllowsKeyboardMouseInput() && GameSettings.WasKeyboardBindingPressed(GameSettings.RebindableAction.Interact);
        bool gamepadPressed = GameSettings.AllowsGamepadInput() && GameSettings.WasGamepadBindingPressed(GameSettings.RebindableAction.Interact);
        if (keyboardPressed) InputDeviceTracker.MarkKeyboardMouse();
        if (gamepadPressed) InputDeviceTracker.MarkGamepad();
        return keyboardPressed || gamepadPressed;
    }

    public string GetInteractionButtonText()
    {
        return GameSettings.GetBindingDisplay(
            GameSettings.RebindableAction.Interact,
            InputPromptDisplay.IsGamepadScheme(this));
    }

    private InputAction ResolveInteractAction()
    {
        if (interactActionReference != null && interactActionReference.action != null)
        {
            return interactActionReference.action;
        }

        PlayerInput playerInput = GetComponent<PlayerInput>() ?? GetComponentInParent<PlayerInput>();
        if (playerInput != null)
        {
            InputAction playerAction = playerInput.actions?.FindAction("Interact", false);
            if (playerAction != null)
            {
                return playerAction;
            }
        }

        fallbackInteractAction = new InputAction("InteractFallback", InputActionType.Button);
        fallbackInteractAction.AddBinding("<Keyboard>/e");
        fallbackInteractAction.AddBinding("<Gamepad>/buttonNorth");
        return fallbackInteractAction;
    }

}
