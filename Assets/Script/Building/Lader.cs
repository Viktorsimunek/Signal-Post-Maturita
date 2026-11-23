using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class Lader : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform topExitPoint;
    [SerializeField] private string interactionPrompt = "Climb ladder";
    [SerializeField, Min(0f)] private float fadeDuration = 0.25f;
    [SerializeField, Min(0f)] private float climbDuration = 1f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip climbClip;
    [SerializeField, Min(0f)] private float climbVolume = 1f;

    private Coroutine climbRoutine;
    private FirstPersonController player;
    private PlayerInteractor playerInteractor;
    private bool movementEnabled, lookEnabled, cameraEffectsEnabled;
    private bool cursorLocked, cursorManaged, controllerEnabled, interactionEnabled;

    private void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        return isActiveAndEnabled && topExitPoint != null && interactor != null && climbRoutine == null;
    }

    public string GetInteractionPrompt(PlayerInteractor interactor) => interactionPrompt;

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor)) return;
        player = interactor.GetComponent<FirstPersonController>();
        if (player == null) return;
        playerInteractor = interactor;
        climbRoutine = StartCoroutine(Climb());
    }

    private IEnumerator Climb()
    {
        LockPlayer();
        yield return ScreenFade.To(1f, fadeDuration);
        if (climbClip != null) audioSource.PlayOneShot(climbClip, climbVolume);
        yield return new WaitForSecondsRealtime(climbClip != null ? climbClip.length : climbDuration);

        if (player != null && topExitPoint != null)
        {
            player.SetCharacterControllerEnabled(false);
            player.transform.position = topExitPoint.position;
            player.SetLookRotation(topExitPoint.eulerAngles.y, player.CurrentPitch, true);
            player.SetCharacterControllerEnabled(controllerEnabled);
        }
        yield return ScreenFade.To(0f, fadeDuration);
        RestorePlayer();
        climbRoutine = null;
    }

    private void LockPlayer()
    {
        movementEnabled = player.IsMovementEnabled;
        lookEnabled = player.IsLookEnabled;
        cameraEffectsEnabled = player.AreCameraEffectsEnabled;
        cursorLocked = player.IsCursorLocked;
        cursorManaged = player.IsCursorStateManaged;
        controllerEnabled = player.IsCharacterControllerEnabled;
        interactionEnabled = playerInteractor.IsInteractionEnabled;

        player.SetMovementEnabled(false);
        player.SetLookEnabled(false);
        player.SetCameraEffectsEnabled(false);
        player.SetCursorLockState(true, false);
        playerInteractor.SetInteractionEnabled(false);
    }

    private void RestorePlayer()
    {
        if (player != null)
        {
            player.ResetMotionState();
            player.SetMovementEnabled(movementEnabled);
            player.SetLookEnabled(lookEnabled);
            player.SetCameraEffectsEnabled(cameraEffectsEnabled);
            player.SetCharacterControllerEnabled(controllerEnabled);
            player.SetCursorLockState(cursorLocked, cursorManaged);
        }
        if (playerInteractor != null) playerInteractor.SetInteractionEnabled(interactionEnabled);
        player = null;
        playerInteractor = null;
    }

    private void OnDisable()
    {
        if (climbRoutine == null) return;
        StopCoroutine(climbRoutine);
        climbRoutine = null;
        audioSource.Stop();
        ScreenFade.Clear();
        RestorePlayer();
    }
}
