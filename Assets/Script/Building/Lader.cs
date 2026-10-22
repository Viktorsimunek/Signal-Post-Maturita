using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class Lader : MonoBehaviour, IInteractable
{
    [Header("Setup")]
    [SerializeField] private Transform topExitPoint;
    [SerializeField] private string interactionPrompt = "Climb ladder";

    [Header("Transition")]
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private float climbDuration = 1f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip climbClip;
    [SerializeField] private float climbVolume = 1f;

    private Coroutine climbRoutine;
    private FirstPersonController activeController;
    private PlayerInteractor activeInteractor;
    private bool cachedMovementEnabled;
    private bool cachedLookEnabled;
    private bool cachedCameraEffectsEnabled;
    private bool cachedCursorLocked;
    private bool cachedCursorStateManaged;
    private bool cachedCharacterControllerEnabled;
    private bool cachedInteractionEnabled;

    public bool CanInteract(PlayerInteractor interactor)
    {
        return topExitPoint != null &&
               climbRoutine == null &&
               interactor != null &&
               isActiveAndEnabled;
    }

    public string GetInteractionPrompt(PlayerInteractor interactor)
    {
        return interactionPrompt;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        FirstPersonController controller = interactor.GetComponent<FirstPersonController>();
        if (controller == null)
        {
            return;
        }

        activeController = controller;
        activeInteractor = interactor;
        climbRoutine = StartCoroutine(ClimbRoutine());
    }

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    private void OnDisable()
    {
        if (climbRoutine != null)
        {
            StopCoroutine(climbRoutine);
            climbRoutine = null;
        }

        FadeCanvas.SetAlpha(0f);
        RestorePlayerState();
    }

    private void OnValidate()
    {
        fadeDuration = Mathf.Max(0f, fadeDuration);
        climbDuration = Mathf.Max(0f, climbDuration);
        climbVolume = Mathf.Max(0f, climbVolume);
    }

    private IEnumerator ClimbRoutine()
    {
        CachePlayerState();
        DisablePlayerState();

        yield return FadeCanvas.FadeTo(1f, fadeDuration);

        if (audioSource != null && climbClip != null)
        {
            audioSource.PlayOneShot(climbClip, climbVolume);
        }

        float waitTime = climbClip != null ? climbClip.length : climbDuration;
        if (waitTime > 0f)
        {
            yield return new WaitForSecondsRealtime(waitTime);
        }

        TeleportPlayerToTop();
        yield return FadeCanvas.FadeTo(0f, fadeDuration);

        RestorePlayerState();
        climbRoutine = null;
    }

    private void CachePlayerState()
    {
        if (activeController == null)
        {
            return;
        }

        cachedMovementEnabled = activeController.IsMovementEnabled;
        cachedLookEnabled = activeController.IsLookEnabled;
        cachedCameraEffectsEnabled = activeController.AreCameraEffectsEnabled;
        cachedCursorLocked = activeController.IsCursorLocked;
        cachedCursorStateManaged = activeController.IsCursorStateManaged;
        cachedCharacterControllerEnabled = activeController.IsCharacterControllerEnabled;
        cachedInteractionEnabled = activeInteractor != null && activeInteractor.IsInteractionEnabled;
    }

    private void DisablePlayerState()
    {
        if (activeController == null)
        {
            return;
        }

        activeController.ResetMotionState();
        activeController.SetMovementEnabled(false);
        activeController.SetLookEnabled(false);
        activeController.SetCameraEffectsEnabled(false);
        activeController.SetCursorLockState(true, false);

        if (activeInteractor != null)
        {
            activeInteractor.SetInteractionEnabled(false);
        }
    }

    private void RestorePlayerState()
    {
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

        activeController = null;
        activeInteractor = null;
    }

    private void TeleportPlayerToTop()
    {
        if (activeController == null || topExitPoint == null)
        {
            return;
        }

        activeController.SetCharacterControllerEnabled(false);
        activeController.transform.SetPositionAndRotation(
            topExitPoint.position,
            Quaternion.Euler(0f, topExitPoint.eulerAngles.y, 0f));
        activeController.SetLookRotation(topExitPoint.eulerAngles.y, activeController.CurrentPitch, true);
        activeController.SetCharacterControllerEnabled(cachedCharacterControllerEnabled);
    }

    private static class FadeCanvas
    {
        private static CanvasGroup canvasGroup;

        public static IEnumerator FadeTo(float targetAlpha, float duration)
        {
            EnsureCanvas();

            if (duration <= 0.0001f)
            {
                canvasGroup.alpha = targetAlpha;
                yield break;
            }

            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
        }

        public static void SetAlpha(float value)
        {
            EnsureCanvas();
            canvasGroup.alpha = value;
        }

        private static void EnsureCanvas()
        {
            if (canvasGroup != null)
            {
                return;
            }

            GameObject root = new GameObject("LadderFadeCanvas");
            Object.DontDestroyOnLoad(root);

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            GameObject fadeImageObject = new GameObject("Fade");
            fadeImageObject.transform.SetParent(root.transform, false);

            RectTransform rectTransform = fadeImageObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            Image fadeImage = fadeImageObject.AddComponent<Image>();
            fadeImage.color = Color.black;
        }
    }
}
