using System.Collections;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

public class LookFocus : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "Look at object";
    [SerializeField] private bool showFocusedPrompt = true;
    [SerializeField] private bool unlockCursorOnFocus = true;
    [TextArea(1, 3)]
    [SerializeField] private string focusedPromptFormat = "[{0}] to exit";
    [SerializeField] private Transform focusPoint;
    [SerializeField] private float moveTime = 0.45f;
    [SerializeField] private UnityEvent onFocusStarted;
    [SerializeField] private UnityEvent onFocusEnded;

    private Func<bool> hasPowerProvider;
    private Action powerLost;
    private string noPowerPrompt = "Bez proudu";
    private PlayerInteractor interactor;
    private PlayerInteractionHud interactionHud;
    private FirstPersonController player;
    private Vector3 oldPos;
    private Quaternion oldRot;
    private float oldPitch;
    private bool oldMove;
    private bool oldLook;
    private bool oldCameraEffects;
    private bool oldCharacterController;
    private bool oldInteraction;
    private bool oldCursorLocked;
    private bool oldCursorStateManaged;
    private bool isUsing;
    private bool moving;

    public event Action FocusStarted;
    public event Action FocusEnded;

    public bool CanInteract(PlayerInteractor playerInteractor)
    {
        return !moving && (!isUsing || playerInteractor == interactor);
    }

    public string GetInteractionPrompt(PlayerInteractor playerInteractor)
    {
        if (!HasPower())
        {
            return noPowerPrompt;
        }

        return isUsing ? "Odejit" : prompt;
    }

    public void Interact(PlayerInteractor playerInteractor)
    {
        if (!HasPower())
        {
            return;
        }

        if (isUsing)
        {
            StartCoroutine(Leave());
        }
        else
        {
            StartCoroutine(Use(playerInteractor));
        }
    }

    public void SetFocusPoint(Transform point)
    {
        focusPoint = point;
    }

    public void SetPrompt(string text)
    {
        prompt = text;
    }

    public void SetPowerGate(Func<bool> hasPower, string noPowerText, Action onPowerLost)
    {
        hasPowerProvider = hasPower;
        noPowerPrompt = string.IsNullOrWhiteSpace(noPowerText) ? noPowerPrompt : noPowerText;
        powerLost = onPowerLost;
    }

    private void Update()
    {
        if (!isUsing || moving)
        {
            return;
        }

        if (!HasPower())
        {
            powerLost?.Invoke();
            StartCoroutine(Leave());
            return;
        }

        UpdateFocusedPrompt();

        bool keyboardExit = Keyboard.current != null &&
            (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame);
        bool gamepadExit = Gamepad.current != null &&
            (Gamepad.current.buttonNorth.wasPressedThisFrame || Gamepad.current.buttonEast.wasPressedThisFrame);

        if (keyboardExit || gamepadExit)
        {
            StartCoroutine(Leave());
        }
    }

    private IEnumerator Use(PlayerInteractor playerInteractor)
    {
        player = playerInteractor.GetComponent<FirstPersonController>();
        if (player == null)
        {
            yield break;
        }

        interactor = playerInteractor;
        interactionHud = playerInteractor.GetComponent<PlayerInteractionHud>();
        oldPos = player.transform.position;
        oldRot = player.transform.rotation;
        oldPitch = player.CurrentPitch;
        oldMove = player.IsMovementEnabled;
        oldLook = player.IsLookEnabled;
        oldCameraEffects = player.AreCameraEffectsEnabled;
        oldCharacterController = player.IsCharacterControllerEnabled;
        oldInteraction = playerInteractor.IsInteractionEnabled;
        oldCursorLocked = player.IsCursorLocked;
        oldCursorStateManaged = player.IsCursorStateManaged;

        player.SetMovementEnabled(false);
        player.SetLookEnabled(false);
        player.SetCameraEffectsEnabled(false);
        player.SetCharacterControllerEnabled(false);
        playerInteractor.SetInteractionEnabled(false);
        if (unlockCursorOnFocus)
        {
            player.SetCursorLockState(false, false);
        }

        Transform point = focusPoint != null ? focusPoint : transform;
        Quaternion targetRot = Quaternion.Euler(0f, point.eulerAngles.y, 0f);
        Vector3 targetPos = point.position - (targetRot * player.CameraNeutralLocalPosition);

        moving = true;
        yield return MoveTo(targetPos, targetRot, NormalizeAngle(point.eulerAngles.x));
        isUsing = true;
        moving = false;
        FocusStarted?.Invoke();
        onFocusStarted?.Invoke();
        UpdateFocusedPrompt();
    }

    private IEnumerator Leave()
    {
        if (player == null)
        {
            yield break;
        }

        ClearFocusedPrompt();
        moving = true;
        yield return MoveTo(oldPos, oldRot, oldPitch);
        isUsing = false;

        player.SetMovementEnabled(oldMove);
        player.SetLookEnabled(oldLook);
        player.SetCameraEffectsEnabled(oldCameraEffects);
        player.SetCharacterControllerEnabled(oldCharacterController);
        player.SetCursorLockState(oldCursorLocked, oldCursorStateManaged);

        if (interactor != null)
        {
            interactor.SetInteractionEnabled(oldInteraction);
        }

        player = null;
        interactor = null;
        interactionHud = null;
        moving = false;
        FocusEnded?.Invoke();
        onFocusEnded?.Invoke();
    }

    private void OnDisable()
    {
        ClearFocusedPrompt();
        onFocusEnded?.Invoke();
    }

    private void UpdateFocusedPrompt()
    {
        if (!showFocusedPrompt || interactionHud == null)
        {
            return;
        }

        string exitDisplay = InputPromptDisplay.GetExitDisplay(interactor, interactor);
        interactionHud.SetActionHint(this, string.Format(focusedPromptFormat, exitDisplay));
    }

    private void ClearFocusedPrompt()
    {
        if (interactionHud != null)
        {
            interactionHud.ClearActionHint(this);
        }
    }

    private bool HasPower()
    {
        return hasPowerProvider == null || hasPowerProvider();
    }

    private IEnumerator MoveTo(Vector3 pos, Quaternion rot, float pitch)
    {
        Vector3 startPos = player.transform.position;
        Quaternion startRot = player.transform.rotation;
        float startPitch = player.CurrentPitch;
        float timer = 0f;
        float duration = Mathf.Max(0.01f, moveTime);

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / duration));

            player.transform.position = Vector3.Lerp(startPos, pos, t);
            player.transform.rotation = Quaternion.Slerp(startRot, rot, t);
            player.SetLookRotation(player.transform.eulerAngles.y, Mathf.LerpAngle(startPitch, pitch, t), true);
            yield return null;
        }

        player.transform.SetPositionAndRotation(pos, rot);
        player.SetLookRotation(rot.eulerAngles.y, pitch, true);
    }

    private float NormalizeAngle(float angle)
    {
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }
}
