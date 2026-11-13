using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>Owns runtime pipeline copies; changing graphics never edits the source assets.</summary>
public sealed class GraphicsQualityController : MonoBehaviour
{
    public const int MonitorRendererIndex = 2;
    private static GraphicsQualityController instance;
    private RenderPipelineAsset originalPipeline;
    private UniversalRenderPipelineAsset runtimePipeline;
    private GameSettings.GraphicsQuality appliedQuality = (GameSettings.GraphicsQuality)(-1);
    private Terrain[] terrains = System.Array.Empty<Terrain>();
    private Lights[] fixtures = System.Array.Empty<Lights>();
    private float originalLodBias;
    private bool shuttingDown;
    private readonly HashSet<Camera> monitorCameras = new HashSet<Camera>();
    private UniversalRenderPipelineAsset monitorScalePipeline;
    private Camera monitorScaleCamera;
    private float monitorPreviousScale;
    private bool originalVeryLowKeyword, originalLowKeyword;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        // Shader globals can outlive the managed domain when entering/exiting Play Mode.
        Shader.DisableKeyword("SIGNALPOST_CRT_VERY_LOW");
        Shader.DisableKeyword("SIGNALPOST_CRT_LOW");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var root = new GameObject("Graphics Quality");
        root.AddComponent<GraphicsQualityController>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        originalPipeline = QualitySettings.renderPipeline;
        originalLodBias = QualitySettings.lodBias;
        originalVeryLowKeyword = Shader.IsKeywordEnabled("SIGNALPOST_CRT_VERY_LOW");
        originalLowKeyword = Shader.IsKeywordEnabled("SIGNALPOST_CRT_LOW");
        SceneManager.sceneLoaded += OnSceneLoaded;
        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        RenderPipelineManager.endCameraRendering += EndCameraRendering;
        GameSettings.EnsureLoaded();
        ApplyCurrent();
    }

    public static void ApplyCurrent()
    {
        if (!Application.isPlaying || instance == null || instance.shuttingDown) return;
        instance.Apply();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Includes additive scenes, without scanning objects on every frame or audio adjustment.
        terrains = FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        fixtures = FindObjectsByType<Lights>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        monitorCameras.Clear();
        ConfigureSceneCameras();
        ApplyDisplayRefresh();
        ApplyTerrain();
        ApplyLights();
        if (runtimePipeline != null) runtimePipeline.renderScale = GameSettings.RenderScale;
    }

    private void Apply()
    {
        bool changed = runtimePipeline == null || appliedQuality != GameSettings.CurrentGraphicsQuality;
        if (changed)
        {
            string assetName = GameSettings.CurrentGraphicsQuality.ToString();
            var source = Resources.Load<UniversalRenderPipelineAsset>("GraphicsQuality/" + assetName);
            if (source == null)
            {
                Debug.LogError("Missing graphics preset: " + assetName);
                return;
            }

            var previous = runtimePipeline;
            runtimePipeline = Instantiate(source);
            runtimePipeline.name = assetName + " (Runtime)";
            runtimePipeline.hideFlags = HideFlags.DontSave;
            appliedQuality = GameSettings.CurrentGraphicsQuality;
            runtimePipeline.renderScale = GameSettings.RenderScale;
            QualitySettings.renderPipeline = runtimePipeline;
            if (previous != null) Destroy(previous);
            ApplyTerrain();
            ApplyLights();
            ApplyDisplayRefresh();
        }
        else if (!Mathf.Approximately(runtimePipeline.renderScale, GameSettings.RenderScale))
        {
            runtimePipeline.renderScale = GameSettings.RenderScale;
        }
        // Reassert the selected variant even when applying the same preset again.
        ApplyCrtQuality();
    }

    private void ApplyTerrain()
    {
        var p = GraphicsQualityPresets.Current;
        QualitySettings.lodBias = p.LodBias;
        foreach (var t in terrains)
        {
            if (t == null) continue;
            t.treeDistance = p.TreeDistance;
            t.treeBillboardDistance = p.BillboardDistance;
            t.treeCrossFadeLength = p.TreeFade;
            t.treeMaximumFullLODCount = p.MaxFullLodTrees;
            t.detailObjectDistance = p.DetailDistance;
            t.detailObjectDensity = p.DetailDensity;
            // The custom PSX terrain shader has not been verified for terrain instancing.
            if (SystemInfo.supportsInstancing && t.materialTemplate != null &&
                t.materialTemplate.shader.name == "Universal Render Pipeline/Terrain/Lit")
                t.drawInstanced = true;
        }
    }

    private void ApplyLights()
    {
        foreach (var fixture in fixtures)
            if (fixture != null && fixture.isActiveAndEnabled) fixture.RefreshOptimization();
    }

    private void ConfigureSceneCameras()
    {
        foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            ConfigureMonitorCamera(camera);
    }

    private void ConfigureMonitorCamera(Camera camera)
    {
        if (camera == null || camera.targetTexture == null || camera.cullingMask != (1 << 5) ||
            !monitorCameras.Add(camera)) return;
        var data = camera.GetUniversalAdditionalCameraData();
        // The terminal deliberately uses renderer 1: its CRT pass supplies the green tint
        // and full-quality screen effect. Only plain UI displays should use renderer 2.
        bool authoredCrt = object.ReferenceEquals(data.scriptableRenderer, runtimePipeline.GetRenderer(1));
        data.SetRenderer(authoredCrt ? 1 : MonitorRendererIndex);
        data.requiresDepthOption = CameraOverrideOption.Off;
        data.requiresColorOption = CameraOverrideOption.Off;
        data.renderShadows = false;
        data.renderPostProcessing = false;
        data.antialiasing = AntialiasingMode.None;
        data.volumeLayerMask = 0;
        // Render textures must not inherit visibility from the player's view of the physical screen.
        camera.useOcclusionCulling = false;
    }

    private void ApplyDisplayRefresh()
    {
        bool low = appliedQuality <= GameSettings.GraphicsQuality.Low;
        var outputs = low ? FindObjectsByType<CameraToTextureWithShader>(FindObjectsInactive.Include, FindObjectsSortMode.None) : null;
        foreach (var camera in monitorCameras)
        {
            if (camera == null) continue;
            var refresh = camera.GetComponent<LowQualityDisplayRefresh>();
            if (!low)
            {
                if (refresh != null) refresh.RestoreFullRate();
                continue;
            }
            if (refresh == null) refresh = camera.gameObject.AddComponent<LowQualityDisplayRefresh>();
            CameraToTextureWithShader owner = null;
            foreach (var output in outputs)
                if (output.SourceCamera == camera) { owner = output; break; }
            refresh.Configure(owner);
        }
    }

    private void ApplyCrtQuality()
    {
        SetKeyword("SIGNALPOST_CRT_VERY_LOW", appliedQuality == GameSettings.GraphicsQuality.VeryLow);
        SetKeyword("SIGNALPOST_CRT_LOW", appliedQuality == GameSettings.GraphicsQuality.Low);
    }

    private static void SetKeyword(string name, bool enabled)
    {
        if (enabled) Shader.EnableKeyword(name);
        else Shader.DisableKeyword(name);
    }

    private void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        // Never switch a renderer here: URP already selected its frame-data context before this callback.
        if (runtimePipeline == null || camera.targetTexture == null || camera.cullingMask != (1 << 5)) return;
        monitorScalePipeline = runtimePipeline;
        monitorScaleCamera = camera;
        monitorPreviousScale = runtimePipeline.renderScale;
        runtimePipeline.renderScale = 1f;
    }

    private void EndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != monitorScaleCamera) return;
        RestoreMonitorScale();
    }

    private void RestoreMonitorScale()
    {
        if (monitorScalePipeline != null) monitorScalePipeline.renderScale = monitorPreviousScale;
        monitorScalePipeline = null;
        monitorScaleCamera = null;
    }

    private void OnApplicationQuit()
    {
        shuttingDown = true;
        RestoreMonitorScale();
        SetKeyword("SIGNALPOST_CRT_VERY_LOW", originalVeryLowKeyword);
        SetKeyword("SIGNALPOST_CRT_LOW", originalLowKeyword);
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        shuttingDown = true;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= EndCameraRendering;
        RestoreMonitorScale();
        SetKeyword("SIGNALPOST_CRT_VERY_LOW", originalVeryLowKeyword);
        SetKeyword("SIGNALPOST_CRT_LOW", originalLowKeyword);
        if (QualitySettings.renderPipeline == runtimePipeline)
            QualitySettings.renderPipeline = originalPipeline;
        QualitySettings.lodBias = originalLodBias;
        if (runtimePipeline != null) Destroy(runtimePipeline);
        instance = null;
    }
}
