using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class CameraToTextureWithShader : MonoBehaviour
{
    private const int UiLayer = 5;

    [Header("Source")]
    [SerializeField] private Camera sourceCamera;
    [SerializeField] private bool disableSourceCameraShadows = true;
    [SerializeField] private bool disableSourceCameraPostProcessing = true;
    [SerializeField] private Color clearColor = Color.black;

    [Header("Monitor")]
    [SerializeField] private Renderer monitorRenderer;
    [SerializeField] private int materialIndex;
    [SerializeField] private Canvas[] monitorCanvases;

    public Camera SourceCamera => sourceCamera;
    public Renderer MonitorRenderer => monitorRenderer;
    public int MaterialIndex => materialIndex;

    private Camera[] originalCanvasCameras;

    private void OnEnable()
    {
        EnsureMonitorCanvases();
        ConfigureSourceCameraData();
        RedirectCanvasCameras(sourceCamera);

        if (sourceCamera != null)
        {
            sourceCamera.enabled = true;
            ClearTarget(clearColor);
            Canvas.ForceUpdateCanvases();
            sourceCamera.Render();
        }
    }

    private void OnDisable()
    {
        if (sourceCamera != null)
        {
            sourceCamera.enabled = false;
        }

        RestoreCanvasCameras();
    }

    private void OnValidate()
    {
        materialIndex = Mathf.Max(0, materialIndex);
    }

    public void ClearTarget(Color color)
    {
        if (sourceCamera == null || sourceCamera.targetTexture == null)
        {
            return;
        }

        RenderTexture targetTexture = sourceCamera.targetTexture;
        if (!targetTexture.IsCreated())
        {
            targetTexture.Create();
        }

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture.active = targetTexture;
        GL.Clear(true, true, color);
        RenderTexture.active = previousActive;
    }

    public void RenderNow()
    {
        if (sourceCamera == null)
        {
            return;
        }

        EnsureMonitorCanvases();
        ConfigureSourceCameraData();
        RedirectCanvasCameras(sourceCamera);
        sourceCamera.enabled = true;
        Canvas.ForceUpdateCanvases();
        sourceCamera.Render();
    }

    private void EnsureMonitorCanvases()
    {
        if (monitorCanvases != null && monitorCanvases.Length > 0)
        {
            return;
        }

        monitorCanvases = GetComponentsInChildren<Canvas>(true);
    }

    private void RedirectCanvasCameras(Camera targetCamera)
    {
        if (targetCamera == null || monitorCanvases == null || monitorCanvases.Length == 0)
        {
            return;
        }

        if (originalCanvasCameras == null || originalCanvasCameras.Length != monitorCanvases.Length)
        {
            originalCanvasCameras = new Camera[monitorCanvases.Length];
            for (int i = 0; i < monitorCanvases.Length; i++)
            {
                originalCanvasCameras[i] = monitorCanvases[i] != null ? monitorCanvases[i].worldCamera : null;
            }
        }

        for (int i = 0; i < monitorCanvases.Length; i++)
        {
            Canvas canvas = monitorCanvases[i];
            if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceCamera)
            {
                continue;
            }

            canvas.worldCamera = targetCamera;
        }
    }

    private void RestoreCanvasCameras()
    {
        if (monitorCanvases == null || originalCanvasCameras == null)
        {
            return;
        }

        int count = Mathf.Min(monitorCanvases.Length, originalCanvasCameras.Length);
        for (int i = 0; i < count; i++)
        {
            Canvas canvas = monitorCanvases[i];
            if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceCamera)
            {
                continue;
            }

            canvas.worldCamera = originalCanvasCameras[i];
        }
    }

    private void ConfigureSourceCameraData()
    {
        if (sourceCamera == null)
        {
            return;
        }

        sourceCamera.clearFlags = CameraClearFlags.SolidColor;
        sourceCamera.backgroundColor = clearColor;
        sourceCamera.cullingMask = 1 << UiLayer;
        sourceCamera.allowMSAA = false;
        sourceCamera.depthTextureMode = DepthTextureMode.None;

        UniversalAdditionalCameraData cameraData = sourceCamera.GetUniversalAdditionalCameraData();
        cameraData.volumeLayerMask = 0;
        cameraData.volumeTrigger = null;
        cameraData.requiresDepthOption = CameraOverrideOption.Off;
        cameraData.requiresColorOption = CameraOverrideOption.Off;
        cameraData.antialiasing = AntialiasingMode.None;

        if (disableSourceCameraShadows)
        {
            cameraData.renderShadows = false;
        }

        if (disableSourceCameraPostProcessing)
        {
            cameraData.renderPostProcessing = false;
        }
    }
}
