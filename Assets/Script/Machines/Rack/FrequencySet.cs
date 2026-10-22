using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class FrequencySet : MonoBehaviour, IInteractable
{
    [Header("Power")]
    [SerializeField] private FuseSwitch powerFuse;
    [SerializeField] private string noPowerPrompt = "Bez proudu";
    [SerializeField] private string noPowerText = "---.- MHz";

    [Header("Interact")]
    [SerializeField] private string prompt = "Nastavit frekvenci";
    [SerializeField] private Transform focusPoint;
    [SerializeField] private float moveTime = 0.45f;

    [Header("Focused Prompt")]
    [SerializeField] private bool showFocusedPrompt = true;
    [TextArea(1, 3)]
    [SerializeField] private string focusedPromptFormat = "[{0}] to tune  [{1}] to exit";

    [Header("Frequency")]
    [SerializeField] private TMP_Text frequencyText;
    [SerializeField] private Transform knob;
    [SerializeField] private float minFrequency = 87.5f;
    [SerializeField] private float maxFrequency = 108f;
    [SerializeField] private float frequency = 98.6f;
    [SerializeField] private float changeSpeed = 0.7f;
    [SerializeField] private float knobSpeed = 240f;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip tuneSound;
    [SerializeField] private float tuneVolume = 0.6f;
    [SerializeField] private float soundDelay = 0.12f;

    private PlayerInteractor playerInteractor;
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
    private bool isUsing;
    private bool moving;
    private float lastTextFrequency = -1f;
    private float nextSoundTime;

    public float Frequency
    {
        get { return frequency; }
    }

    public bool HasPower
    {
        get { return (powerFuse == null || powerFuse.IsOn) && LightsPowerGrid.IsPowered; }
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        return !moving && (!isUsing || interactor == playerInteractor);
    }

    public string GetInteractionPrompt(PlayerInteractor interactor)
    {
        if (!HasPower)
        {
            return noPowerPrompt;
        }

        return isUsing ? "Odejit" : prompt;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (isUsing)
        {
            StartCoroutine(Leave());
        }
        else
        {
            if (!HasPower)
            {
                return;
            }

            StartCoroutine(Use(interactor));
        }
    }

    private void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        frequency = Mathf.Clamp(frequency, minFrequency, maxFrequency);
        UpdateText();
    }

    private void OnDisable()
    {
        ClearFocusedPrompt();
    }

    private void Update()
    {
        if (!HasPower)
        {
            UpdateText();

            if (isUsing && !moving)
            {
                StartCoroutine(Leave());
            }

            return;
        }

        if (!isUsing || moving)
        {
            UpdateText();
            return;
        }

        UpdateFocusedPrompt();

        if ((Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame)) ||
            (Gamepad.current != null && (Gamepad.current.buttonEast.wasPressedThisFrame || Gamepad.current.buttonNorth.wasPressedThisFrame)))
        {
            StartCoroutine(Leave());
            return;
        }

        float input = GetInput();
        if (Mathf.Abs(input) < 0.1f)
        {
            return;
        }

        frequency += input * changeSpeed * Time.deltaTime;
        frequency = Mathf.Clamp(frequency, minFrequency, maxFrequency);
        UpdateText();
        PlayTuneSound();

        if (knob != null)
        {
            knob.Rotate(Vector3.up, -input * knobSpeed * Time.deltaTime, Space.Self);
        }
    }

    private IEnumerator Use(PlayerInteractor interactor)
    {
        if (interactor == null)
        {
            yield break;
        }

        player = interactor.GetComponent<FirstPersonController>();
        if (player == null)
        {
            yield break;
        }

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
        Quaternion targetRot = Quaternion.Euler(0f, point.eulerAngles.y, 0f);
        Vector3 targetPos = point.position - (targetRot * player.CameraNeutralLocalPosition);
        yield return MoveCamera(targetPos, targetRot, NormalizeAngle(point.eulerAngles.x));
        isUsing = true;
        moving = false;
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
        yield return MoveCamera(oldPos, oldRot, oldPitch);
        isUsing = false;

        player.SetMovementEnabled(oldMove);
        player.SetLookEnabled(oldLook);
        player.SetCameraEffectsEnabled(oldCameraEffects);
        player.SetCharacterControllerEnabled(oldCharacterController);

        if (playerInteractor != null)
        {
            playerInteractor.SetInteractionEnabled(oldInteraction);
        }

        player = null;
        playerInteractor = null;
        interactionHud = null;
        moving = false;
    }

    private void UpdateFocusedPrompt()
    {
        if (!showFocusedPrompt || interactionHud == null)
        {
            return;
        }

        string tuneDisplay = InputPromptDisplay.GetHorizontalTuneDisplay(playerInteractor);
        string exitDisplay = InputPromptDisplay.GetExitDisplay(playerInteractor, playerInteractor);
        string text = string.Format(focusedPromptFormat, tuneDisplay, exitDisplay);
        interactionHud.SetActionHint(this, text);
    }

    private void ClearFocusedPrompt()
    {
        if (interactionHud != null)
        {
            interactionHud.ClearActionHint(this);
        }
    }

    private IEnumerator MoveCamera(Vector3 targetPos, Quaternion targetRot, float targetPitch)
    {
        Vector3 startPos = player.transform.position;
        Quaternion startRot = player.transform.rotation;
        float startPitch = player.CurrentPitch;
        float timer = 0f;

        while (timer < moveTime)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / moveTime);

            player.transform.position = Vector3.Lerp(startPos, targetPos, t);
            player.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            player.SetLookRotation(player.transform.eulerAngles.y, Mathf.LerpAngle(startPitch, targetPitch, t), true);
            yield return null;
        }

        player.transform.SetPositionAndRotation(targetPos, targetRot);
        player.SetLookRotation(targetRot.eulerAngles.y, targetPitch, true);
    }

    private float GetInput()
    {
        float input = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input += 1f;
        }

        if (Gamepad.current != null)
        {
            input += Gamepad.current.leftStick.ReadValue().x;
            if (Gamepad.current.dpad.left.isPressed) input -= 1f;
            if (Gamepad.current.dpad.right.isPressed) input += 1f;
        }

        return Mathf.Clamp(input, -1f, 1f);
    }

    private void UpdateText()
    {
        if (frequencyText != null)
        {
            if (!HasPower)
            {
                frequencyText.text = noPowerText;
                lastTextFrequency = -1f;
                return;
            }

            float showFrequency = Mathf.Round(frequency * 10f) / 10f;
            if (Mathf.Approximately(showFrequency, lastTextFrequency))
            {
                return;
            }

            lastTextFrequency = showFrequency;
            frequencyText.text = "<mspace=0.65em>" + showFrequency.ToString("000.0") + " MHz</mspace>";
        }
    }

    private void PlayTuneSound()
    {
        if (!HasPower || audioSource == null || tuneSound == null || Time.time < nextSoundTime)
        {
            return;
        }

        nextSoundTime = Time.time + soundDelay;
        audioSource.pitch = Random.Range(0.95f, 1.05f);
        audioSource.PlayOneShot(tuneSound, tuneVolume);
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
