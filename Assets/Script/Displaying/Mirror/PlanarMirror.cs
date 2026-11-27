using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Renderer))]
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public class PlanarMirror : MonoBehaviour
{
// TEST

    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Material lowQualityMaterial;
    [SerializeField] private Vector3 localNormal = Vector3.back;

    [Header("Vykon")]
    [SerializeField, Range(128, 2048)] private int reflectionResolution = 384;
    [SerializeField] private float maxDistance = 12f;
    [SerializeField, Range(1, 120)] private int maxUpdatesPerSecond = 60;

    [Header("Kamera odrazu")]
    [SerializeField] private LayerMask cullingMask = ~0;
    [SerializeField] private float farClip = 60f;
    [SerializeField] private float clipPlaneOffset = 0.02f;
    [SerializeField] private int rendererIndex = 2;
    [SerializeField] private bool renderShadows;

    private Renderer surface;
    private Material originalMaterial;
    private MaterialPropertyBlock properties;
    private Camera mirrorCamera;
    private RenderTexture texture;
    private bool lowQuality;
    private float nextUpdate;
    private static bool rendering;
    private readonly Plane[] frustum = new Plane[6];
    private readonly UniversalRenderPipeline.SingleCameraRequest request = new UniversalRenderPipeline.SingleCameraRequest();

    // Zapnuti, vypnuti a kvalita
    private void OnEnable()
    {
        surface = GetComponent<Renderer>();
        originalMaterial = surface.sharedMaterial;
        properties = new MaterialPropertyBlock();
        nextUpdate = 0f;
        if (!Application.isPlaying) return;
        GameSettings.EnsureLoaded();
        GameSettings.SettingsChanged += ApplyQuality;
        ApplyQuality();
    }

    private void OnDisable()
    {
        GameSettings.SettingsChanged -= ApplyQuality;
        if (surface) surface.sharedMaterial = originalMaterial;
        lowQuality = false;
        ClearReflection();
    }

    private void ApplyQuality()
    {
        lowQuality = GameSettings.CurrentGraphicsQuality <= GameSettings.GraphicsQuality.Low;
        surface.sharedMaterial = originalMaterial;
        if (lowQuality && lowQualityMaterial) surface.sharedMaterial = lowQualityMaterial;
        if (lowQuality) ClearReflection();
        nextUpdate = 0f;
    }

    private void LateUpdate() => RenderReflection();

    // Hlavni postup: kontrola -> priprava -> odraz -> material
    public void RenderReflection()
    {
        Vector3 normal = transform.worldToLocalMatrix.transpose.MultiplyVector(localNormal).normalized;
        if (!CanRender(normal)) return;

        PrepareTexture();
        PrepareCamera();
        ReflectCamera(normal);
        ClipBehindMirror(normal);
        if (!RenderToTexture()) return;
        UpdateMaterial();

        nextUpdate = Time.unscaledTime + 1f / Mathf.Max(1, maxUpdatesPerSecond);
    }

    private bool CanRender(Vector3 normal)
    {
        if (lowQuality || rendering || Time.timeScale == 0f) return false;
        if (Time.unscaledTime < nextUpdate) return false;
        if (!surface || !surface.enabled) return false;
        if (!playerCamera) playerCamera = Camera.main;
        if (!playerCamera || !playerCamera.isActiveAndEnabled) return false;
        if ((playerCamera.cullingMask & (1 << gameObject.layer)) == 0) return false;

        Vector3 position = playerCamera.transform.position;
        bool inFront = Vector3.Dot(normal, position - transform.position) > clipPlaneOffset;
        bool closeEnough = Vector3.Distance(position, surface.bounds.ClosestPoint(position)) <= maxDistance;
        if (!inFront || !closeEnough) return false;

        GeometryUtility.CalculateFrustumPlanes(playerCamera, frustum);
        return GeometryUtility.TestPlanesAABB(frustum, surface.bounds);
    }

    private bool RenderToTexture()
    {
        request.destination = texture;
        if (!RenderPipeline.SupportsRenderRequest(mirrorCamera, request)) return false;
        float previous = Shader.GetGlobalFloat("_PlanarReflectionRendering");
        try
        {
            rendering = true;
            Shader.SetGlobalFloat("_PlanarReflectionRendering", 1f);
            RenderPipeline.SubmitRenderRequest(mirrorCamera, request);
        }
        finally
        {
            Shader.SetGlobalFloat("_PlanarReflectionRendering", previous);
            rendering = false;
        }
        return true;
    }

    private void UpdateMaterial()
    {
        Matrix4x4 projection = GL.GetGPUProjectionMatrix(mirrorCamera.projectionMatrix, true);
        surface.GetPropertyBlock(properties);
        properties.SetTexture("_ReflectionTex", texture);
        properties.SetMatrix("_ReflectionVP", projection * mirrorCamera.worldToCameraMatrix);
        surface.SetPropertyBlock(properties);
    }

    private void PrepareCamera()
    {
        if (!mirrorCamera)
        {
            GameObject cameraObject = new GameObject("Mirror Camera", typeof(Camera));
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            mirrorCamera = cameraObject.GetComponent<Camera>();
        }

        mirrorCamera.CopyFrom(playerCamera);
        mirrorCamera.enabled = false;
        mirrorCamera.cameraType = CameraType.Reflection;
        mirrorCamera.targetTexture = texture;
        mirrorCamera.rect = new Rect(0, 0, 1, 1);
        mirrorCamera.aspect = playerCamera.aspect;
        mirrorCamera.cullingMask = cullingMask;
        float minDistance = playerCamera.nearClipPlane + 0.01f;
        float maxDistance = Mathf.Min(farClip, playerCamera.farClipPlane);
        mirrorCamera.farClipPlane = Mathf.Max(minDistance, maxDistance);
        mirrorCamera.allowHDR = false;
        mirrorCamera.allowMSAA = false;
        mirrorCamera.useOcclusionCulling = false;

        var data = mirrorCamera.GetUniversalAdditionalCameraData();
        data.SetRenderer(rendererIndex);
        data.renderShadows = renderShadows;
        data.renderPostProcessing = false;
        data.requiresColorOption = CameraOverrideOption.Off;
        data.requiresDepthOption = CameraOverrideOption.On; // Kvuli mlze.
        data.volumeLayerMask = 0;
        data.allowXRRendering = false;
    }

    private void ReflectCamera(Vector3 normal)
    {
        // Kamera je stejne daleko za zrcadlem, jako je hrac pred nim.
        Vector3 position = playerCamera.transform.position;
        float distance = Vector3.Dot(position - transform.position, normal);
        Vector3 reflectedPosition = position - 2f * distance * normal;
        Vector3 forward = Vector3.Reflect(playerCamera.transform.forward, normal);
        Vector3 up = Vector3.Reflect(playerCamera.transform.up, normal);
        mirrorCamera.transform.SetPositionAndRotation(reflectedPosition, Quaternion.LookRotation(forward, up));
        mirrorCamera.ResetWorldToCameraMatrix();
        mirrorCamera.ResetProjectionMatrix();
    }

    private void ClipBehindMirror(Vector3 normal)
    {
        // Odrizneme objekty za zrcadlem, aby nezakryvaly odraz.
        Matrix4x4 view = mirrorCamera.worldToCameraMatrix;
        Vector3 clipPoint = view.MultiplyPoint(transform.position + normal * clipPlaneOffset);
        Vector3 clipNormal = view.MultiplyVector(normal).normalized;
        Vector4 plane = new Vector4(clipNormal.x, clipNormal.y, clipNormal.z, -Vector3.Dot(clipPoint, clipNormal));
        mirrorCamera.projectionMatrix = mirrorCamera.CalculateObliqueMatrix(plane);
    }
    private void PrepareTexture()
    {
        int size = Mathf.Clamp(reflectionResolution, 128, 2048);
        if (texture && texture.width == size && texture.IsCreated()) return;
        ReleaseTexture();
        texture = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.Create();
    }

    private void ClearReflection()
    {
        if (surface && properties != null)
        {
            surface.GetPropertyBlock(properties);
            properties.SetTexture("_ReflectionTex", Texture2D.blackTexture);
            surface.SetPropertyBlock(properties);
        }
        ReleaseTexture();
        if (mirrorCamera) DestroyResource(mirrorCamera.gameObject);
        mirrorCamera = null;
    }

    private void ReleaseTexture()
    {
        if (!texture) return;
        if (mirrorCamera) mirrorCamera.targetTexture = null;
        request.destination = null;
        texture.Release();
        DestroyResource(texture);
        texture = null;
    }

    private void DestroyResource(Object resource)
    {
        if (Application.isPlaying) Destroy(resource);
        else DestroyImmediate(resource);
    }
}
