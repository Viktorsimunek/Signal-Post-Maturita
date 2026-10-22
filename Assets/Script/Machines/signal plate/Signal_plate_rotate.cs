using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class Signal_plate_rotate : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "Otocit talir";
    [SerializeField] private Transform focusPoint;
    [SerializeField] private float moveTime = 0.45f;

    [SerializeField] private bool showFocusedPrompt = true;
    [TextArea(1, 3)]
    [SerializeField] private string focusedPromptFormat = "[{0}] otacet  [{1}] odejit";

    [SerializeField] private SignalQuality signalQuality;
    [SerializeField] private Transform plate;
    [SerializeField] private float rotateSpeed = 18f;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip rotateSound;
    [SerializeField] private float rotateVolume = 0.7f;
    [SerializeField] private float rotateFadeTime = 0.25f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.9f, 1.05f);

    private PlayerInteractor playerInteractor;
    private PlayerInteractionHud interactionHud;
    private FirstPersonController player;
    private Vector3 oldPos;
    private Quaternion oldRot;
    private float oldPitch;
    private bool oldMove, oldLook, oldCameraEffects, oldCharacterController, oldInteraction;
    private bool isUsing, moving, soundFading;

    public bool CanInteract(PlayerInteractor interactor) => !moving && (!isUsing || interactor == playerInteractor);
    public string GetInteractionPrompt(PlayerInteractor interactor) => isUsing ? "Odejit" : prompt;
    public void Interact(PlayerInteractor interactor) => StartCoroutine(isUsing ? Leave() : Use(interactor));

    private void Awake()
    {
        plate ??= transform;
        audioSource ??= GetComponent<AudioSource>();
        signalQuality ??= FindFirstObjectByType<SignalQuality>();
    }

    private void OnValidate()
    {
        rotateSpeed = Mathf.Max(0f, rotateSpeed);
        rotateVolume = Mathf.Max(0f, rotateVolume);
        rotateFadeTime = Mathf.Max(0.01f, rotateFadeTime);
        if (pitchRange.x > pitchRange.y) pitchRange = new Vector2(pitchRange.y, pitchRange.x);
    }

    private void OnDisable()
    {
        StopRotateSound();
        ClearPrompt();
    }

    private void Update()
    {
        SendAngle();
        if (!isUsing || moving) return;

        UpdatePrompt();
        if (PressedExit())
        {
            StartCoroutine(Leave());
            return;
        }

        float input = GetInput();
        if (Mathf.Abs(input) < 0.1f)
        {
            StopRotateSound();
            return;
        }

        plate.Rotate(Vector3.up, input * rotateSpeed * Time.deltaTime, Space.World);
        SendAngle();
        PlayRotateSound(Mathf.Abs(input));
    }

    private IEnumerator Use(PlayerInteractor interactor)
    {
        if (interactor == null) yield break;

        player = interactor.GetComponent<FirstPersonController>();
        if (player == null) yield break;

        playerInteractor = interactor;
        interactionHud = interactor.GetComponent<PlayerInteractionHud>();
        oldPos = player.transform.position;
        oldRot = player.transform.rotation;
        oldPitch = player.CurrentPitch;
        oldMove = player.IsMovementEnabled;
        oldLook = player.IsLookEnabled;
        oldCameraEffects = player.AreCameraEffectsEnabled;
        oldCharacterController = player.IsCharacterControllerEnabled;
        oldInteraction = interactor.IsInteractionEnabled;

        player.SetMovementEnabled(false);
        player.SetLookEnabled(false);
        player.SetCameraEffectsEnabled(false);
        player.SetCharacterControllerEnabled(false);
        interactor.SetInteractionEnabled(false);

        moving = true;
        Transform point = focusPoint != null ? focusPoint : transform;
        Quaternion rot = Quaternion.Euler(0f, point.eulerAngles.y, 0f);
        yield return MoveCamera(point.position - rot * player.CameraNeutralLocalPosition, rot, FixAngle(point.eulerAngles.x));

        isUsing = true;
        moving = false;
        UpdatePrompt();
    }

    private IEnumerator Leave()
    {
        if (player == null) yield break;

        ClearPrompt();
        StopRotateSound();
        moving = true;
        yield return MoveCamera(oldPos, oldRot, oldPitch);
        isUsing = false;

        player.SetMovementEnabled(oldMove);
        player.SetLookEnabled(oldLook);
        player.SetCameraEffectsEnabled(oldCameraEffects);
        player.SetCharacterControllerEnabled(oldCharacterController);
        playerInteractor?.SetInteractionEnabled(oldInteraction);

        player = null;
        playerInteractor = null;
        interactionHud = null;
        moving = false;
    }

    private IEnumerator MoveCamera(Vector3 pos, Quaternion rot, float pitch)
    {
        Vector3 startPos = player.transform.position;
        Quaternion startRot = player.transform.rotation;
        float startPitch = player.CurrentPitch;

        for (float t = 0f; t < 1f; t += Time.deltaTime / moveTime)
        {
            player.transform.position = Vector3.Lerp(startPos, pos, t);
            player.transform.rotation = Quaternion.Slerp(startRot, rot, t);
            player.SetLookRotation(player.transform.eulerAngles.y, Mathf.LerpAngle(startPitch, pitch, t), true);
            yield return null;
        }

        player.transform.SetPositionAndRotation(pos, rot);
        player.SetLookRotation(rot.eulerAngles.y, pitch, true);
    }

    private void PlayRotateSound(float input)
    {
        if (audioSource == null || rotateSound == null) return;

        if (!audioSource.isPlaying || audioSource.clip != rotateSound)
        {
            audioSource.clip = rotateSound;
            audioSource.loop = true;
            audioSource.volume = 0f;
            if (rotateSound.length > 0.05f) audioSource.time = Random.Range(0f, rotateSound.length);
            audioSource.Play();
            soundFading = true;
        }

        audioSource.pitch = Mathf.Lerp(pitchRange.x, pitchRange.y, input);
        audioSource.volume = soundFading
            ? Mathf.MoveTowards(audioSource.volume, rotateVolume, Time.deltaTime * rotateVolume / rotateFadeTime)
            : rotateVolume;

        if (audioSource.volume >= rotateVolume - 0.01f) soundFading = false;
    }

    private void StopRotateSound()
    {
        if (audioSource == null || audioSource.clip != rotateSound) return;
        audioSource.Stop();
        audioSource.clip = null;
        soundFading = false;
    }

    private float GetInput()
    {
        float input = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input--;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input++;
        }

        if (Gamepad.current != null)
        {
            input += Gamepad.current.leftStick.ReadValue().x;
            if (Gamepad.current.dpad.left.isPressed) input--;
            if (Gamepad.current.dpad.right.isPressed) input++;
        }

        return Mathf.Clamp(input, -1f, 1f);
    }

    private bool PressedExit()
    {
        bool keyboard = Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame);
        bool gamepad = Gamepad.current != null && (Gamepad.current.buttonEast.wasPressedThisFrame || Gamepad.current.buttonNorth.wasPressedThisFrame);
        return keyboard || gamepad;
    }

    private void SendAngle()
    {
        if (signalQuality != null && plate != null) signalQuality.SetPlateAngle(plate.eulerAngles.y);
    }

    private void UpdatePrompt()
    {
        if (!showFocusedPrompt || interactionHud == null) return;

        string tune = InputPromptDisplay.GetHorizontalTuneDisplay(playerInteractor);
        string exit = InputPromptDisplay.GetExitDisplay(playerInteractor, playerInteractor);
        interactionHud.SetActionHint(this, string.Format(focusedPromptFormat, tune, exit));
    }

    private void ClearPrompt()
    {
        interactionHud?.ClearActionHint(this);
    }

    private float FixAngle(float angle) => angle > 180f ? angle - 360f : angle;
}
