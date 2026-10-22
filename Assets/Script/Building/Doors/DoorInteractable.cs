using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorInteractable : MonoBehaviour, IInteractable
{
    private enum Axis { X, Y, Z }

    private struct Handle
    {
        public Transform tr;
        public Quaternion startRot;
        public bool inverted;
    }

    [Header("Interact")]
    [SerializeField] private bool canInteract = true;
    [SerializeField] private bool locked;
    [SerializeField] private float lockedCooldown = 0.45f;
    [SerializeField] private string openPrompt = "Open door";
    [SerializeField] private string closePrompt = "Close door";
    [SerializeField] private string lockedPrompt = "Locked";

    [Header("Door")]
    [SerializeField] private Transform primaryPivot;
    [SerializeField] private Transform secondaryPivot;
    [SerializeField] private bool startOpen;
    [SerializeField] private float primaryOpenAngle = 95f;
    [SerializeField] private float openDuration = 0.35f;
    [SerializeField] private Axis openAxis = Axis.Y;

    [Header("Handle")]
    [SerializeField] private bool animateHandles = true;
    [SerializeField] private Axis handleAxis = Axis.X;
    [SerializeField] private float handleAngle = 28f;
    [SerializeField] private float handleDuration = 0.16f;
    [SerializeField] private bool invertHandleDirection = true;

    [Header("Sound")]
    [SerializeField] private AudioSource doorAudioSource;
    [SerializeField] private AudioClip[] openClips;
    [SerializeField] private AudioClip[] closeClips;
    [SerializeField] private AudioClip lockedSound;
    [SerializeField] private float audioVolume = 1f;

    private Vector3 primaryClosed;
    private Vector3 secondaryClosed;
    private readonly List<Handle> handles = new List<Handle>();
    private Coroutine routine;
    private bool isOpen;
    private bool isMoving;
    private bool lockedBusy;

    public bool CanInteract(PlayerInteractor interactor)
    {
        return canInteract && isActiveAndEnabled && !isMoving && !lockedBusy;
    }

    public string GetInteractionPrompt(PlayerInteractor interactor)
    {
        if (locked) return lockedPrompt;
        return isOpen ? closePrompt : openPrompt;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor)) return;

        if (locked)
        {
            StartCoroutine(LockedDoor());
            return;
        }

        SetOpen(!isOpen);
    }

    private void Awake()
    {
        if (primaryPivot == null) primaryPivot = transform;
        if (doorAudioSource == null) doorAudioSource = GetComponent<AudioSource>();

        primaryClosed = primaryPivot.localEulerAngles;
        if (secondaryPivot != null) secondaryClosed = secondaryPivot.localEulerAngles;

        FindHandles(primaryPivot);
        FindHandles(secondaryPivot);

        isOpen = startOpen;
        SetDoorRotation(startOpen, 1f);
        SetHandleRotation(0f);
    }

    private void OnValidate()
    {
        openDuration = Mathf.Max(0f, openDuration);
        handleDuration = Mathf.Max(0f, handleDuration);
        lockedCooldown = Mathf.Max(0f, lockedCooldown);
        primaryOpenAngle = Mathf.Clamp(primaryOpenAngle, 0f, 180f);
        handleAngle = Mathf.Clamp(handleAngle, 0f, 65f);
        audioVolume = Mathf.Max(0f, audioVolume);
    }

    [ContextMenu("Toggle")]
    private void Toggle()
    {
        SetOpen(!isOpen);
    }

    private void SetOpen(bool open)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(MoveDoor(open));
    }

    private IEnumerator MoveDoor(bool open)
    {
        isMoving = true;
        if (open) PlaySound(PickClip(openClips));

        Vector3 startA = primaryPivot.localEulerAngles;
        Vector3 endA = GetDoorEuler(primaryClosed, open, false);

        Vector3 startB = Vector3.zero;
        Vector3 endB = Vector3.zero;
        if (secondaryPivot != null)
        {
            startB = secondaryPivot.localEulerAngles;
            endB = GetDoorEuler(secondaryClosed, open, true);
        }

        if (openDuration <= 0.01f)
        {
            SetDoorRotation(open, 1f);
            SetHandleRotation(0f);
            Finish(open);
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / openDuration;
            float smooth = Mathf.SmoothStep(0f, 1f, t);

            primaryPivot.localEulerAngles = LerpEuler(startA, endA, smooth);
            if (secondaryPivot != null) secondaryPivot.localEulerAngles = LerpEuler(startB, endB, smooth);

            SetHandleRotation(HandleAnim(t * openDuration));
            yield return null;
        }

        SetDoorRotation(open, 1f);
        SetHandleRotation(0f);
        Finish(open);
    }

    private IEnumerator LockedDoor()
    {
        lockedBusy = true;
        PlaySound(lockedSound);

        float time = 0f;
        float wait = Mathf.Max(lockedCooldown, handleDuration);
        while (time < wait)
        {
            time += Time.deltaTime;
            SetHandleRotation(HandleAnim(time));
            yield return null;
        }

        SetHandleRotation(0f);
        lockedBusy = false;
    }

    private void Finish(bool open)
    {
        isOpen = open;
        isMoving = false;
        routine = null;

        if (!open) PlaySound(PickClip(closeClips));
    }

    private void SetDoorRotation(bool open, float t)
    {
        primaryPivot.localEulerAngles = LerpEuler(primaryClosed, GetDoorEuler(primaryClosed, open, false), t);
        if (secondaryPivot != null) secondaryPivot.localEulerAngles = LerpEuler(secondaryClosed, GetDoorEuler(secondaryClosed, open, true), t);
    }

    private Vector3 GetDoorEuler(Vector3 closed, bool open, bool secondDoor)
    {
        if (!open) return closed;

        float angle = secondDoor ? -primaryOpenAngle : primaryOpenAngle;
        if (openAxis == Axis.X) closed.x += angle;
        if (openAxis == Axis.Y) closed.y += angle;
        if (openAxis == Axis.Z) closed.z += angle;

        return closed;
    }

    private float HandleAnim(float time)
    {
        if (!animateHandles || handleDuration <= 0.01f) return 0f;

        float half = handleDuration * 0.5f;
        if (time < half) return Mathf.SmoothStep(0f, 1f, time / half);
        if (time < handleDuration) return Mathf.SmoothStep(1f, 0f, (time - half) / half);
        return 0f;
    }

    private void SetHandleRotation(float t)
    {
        if (!animateHandles) return;

        for (int i = 0; i < handles.Count; i++)
        {
            Handle h = handles[i];
            if (h.tr == null) continue;

            float dir = invertHandleDirection ? -1f : 1f;
            if (h.inverted) dir *= -1f;

            h.tr.localRotation = h.startRot * Quaternion.AngleAxis(handleAngle * dir * t, GetAxis(handleAxis));
        }
    }

    private void FindHandles(Transform root)
    {
        if (root == null || !animateHandles) return;

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == root || !all[i].name.ToLower().Contains("handle")) continue;

            handles.Add(new Handle
            {
                tr = all[i],
                startRot = all[i].localRotation,
                inverted = handles.Count % 2 == 1
            });
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (doorAudioSource != null && clip != null) doorAudioSource.PlayOneShot(clip, audioVolume);
    }

    private AudioClip PickClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;
        return clips[Random.Range(0, clips.Length)];
    }

    private Vector3 LerpEuler(Vector3 a, Vector3 b, float t)
    {
        a.x = Mathf.LerpAngle(a.x, b.x, t);
        a.y = Mathf.LerpAngle(a.y, b.y, t);
        a.z = Mathf.LerpAngle(a.z, b.z, t);
        return a;
    }

    private Vector3 GetAxis(Axis axis)
    {
        if (axis == Axis.X) return Vector3.right;
        if (axis == Axis.Z) return Vector3.forward;
        return Vector3.up;
    }
}
