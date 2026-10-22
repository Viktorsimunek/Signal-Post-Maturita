using UnityEngine;
using UnityEngine.SceneManagement;

public static class MainScenePlayerBootstrap
{
    private const string MainSceneName = "Main";
    private const float PlayerHeight = 1.78f;
    private const float PlayerRadius = 0.28f;
    private const float EyeHeight = 1.62f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateControllerIfNeeded()
    {
        EnsureMainScenePlayer();
    }

    public static void EnsureMainScenePlayer()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != MainSceneName)
        {
            return;
        }

        FirstPersonController existingController = Object.FindFirstObjectByType<FirstPersonController>();
        if (existingController != null)
        {
            EnsureFootstepAudio(existingController.gameObject);
            PlayerInteractor existingInteractor = EnsureInteractor(existingController);
            EnsureInteractionHud(existingController, existingInteractor);
            EnsurePauseMenu(existingController);
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("MainScenePlayerBootstrap could not find a Main Camera in the Main scene.");
            return;
        }

        Transform cameraTransform = mainCamera.transform;
        Vector3 cameraWorldPosition = cameraTransform.position;
        Quaternion cameraWorldRotation = cameraTransform.rotation;

        GameObject playerRoot = new GameObject("Player");
        playerRoot.transform.position = cameraWorldPosition - (Vector3.up * EyeHeight);
        playerRoot.transform.rotation = Quaternion.Euler(0f, cameraWorldRotation.eulerAngles.y, 0f);

        CharacterController controller = playerRoot.AddComponent<CharacterController>();
        controller.height = PlayerHeight;
        controller.radius = PlayerRadius;
        controller.center = new Vector3(0f, PlayerHeight * 0.5f, 0f);
        controller.stepOffset = 0.25f;
        controller.slopeLimit = 45f;
        controller.minMoveDistance = 0f;
        controller.skinWidth = 0.03f;

        cameraTransform.SetParent(playerRoot.transform, true);
        cameraTransform.localPosition = new Vector3(0f, EyeHeight, 0f);
        cameraTransform.localRotation = Quaternion.Euler(cameraWorldRotation.eulerAngles.x, 0f, 0f);

        FirstPersonController firstPersonController = playerRoot.AddComponent<FirstPersonController>();
        firstPersonController.Configure(cameraTransform);
        EnsureFootstepAudio(playerRoot);
        PlayerInteractor interactor = EnsureInteractor(firstPersonController);
        EnsureInteractionHud(firstPersonController, interactor);
        EnsurePauseMenu(firstPersonController);

        EnsureFlashlight(cameraTransform);
    }

    private static void EnsureFootstepAudio(GameObject playerRoot)
    {
        if (playerRoot.GetComponent<PlayerFootstepAudio>() == null)
        {
            playerRoot.AddComponent<PlayerFootstepAudio>();
        }
    }

    private static PlayerInteractor EnsureInteractor(FirstPersonController controller)
    {
        if (controller == null)
        {
            return null;
        }

        PlayerInteractor interactor = controller.GetComponent<PlayerInteractor>();
        if (interactor == null)
        {
            interactor = controller.gameObject.AddComponent<PlayerInteractor>();
        }

        Transform interactionCamera = controller.CameraRoot;
        if (interactionCamera == null && Camera.main != null)
        {
            interactionCamera = Camera.main.transform;
        }

        if (interactionCamera != null)
        {
            interactor.Configure(interactionCamera);
        }

        return interactor;
    }

    private static void EnsureInteractionHud(FirstPersonController controller, PlayerInteractor interactor)
    {
        if (controller == null || interactor == null)
        {
            return;
        }

        PlayerInteractionHud hud = controller.GetComponent<PlayerInteractionHud>();
        if (hud == null)
        {
            hud = controller.gameObject.AddComponent<PlayerInteractionHud>();
        }

        hud.Configure(interactor);
    }

    private static void EnsurePauseMenu(FirstPersonController controller)
    {
        if (controller == null)
        {
            return;
        }

        InGamePauseMenu pauseMenu = Object.FindFirstObjectByType<InGamePauseMenu>();
        if (pauseMenu == null)
        {
            pauseMenu = controller.gameObject.AddComponent<InGamePauseMenu>();
        }

        pauseMenu.Configure(controller);
    }

    private static void EnsureFlashlight(Transform cameraTransform)
    {
        Light existingFlashlight = cameraTransform.GetComponentInChildren<Light>(true);
        if (existingFlashlight == null)
        {
            Debug.LogWarning("MainScenePlayerBootstrap could not find an existing flashlight Light under the camera.");
            return;
        }

        FlashlightController flashlightController = existingFlashlight.GetComponent<FlashlightController>();
        if (flashlightController == null)
        {
            flashlightController = existingFlashlight.gameObject.AddComponent<FlashlightController>();
        }

        flashlightController.Configure(cameraTransform, existingFlashlight);
    }
}
