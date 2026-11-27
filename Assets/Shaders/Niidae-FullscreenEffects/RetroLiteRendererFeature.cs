using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>Explicitly installed only in Low/Very Low renderer assets.</summary>
public sealed class RetroLiteRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader shader;
    [SerializeField, Range(1f, 1.2f)] private float contrast = 1.08f;
    [SerializeField, Range(1f, 1.2f)] private float saturation = 1.06f;
    [SerializeField, Range(0f, .15f)] private float vignette = .035f;
    [SerializeField, Range(0f, .02f)] private float grain = .004f;
    private Material material;
    private LitePass pass;

    public override void Create()
    {
        if (shader == null) return;
        CoreUtils.Destroy(material);
        material = CoreUtils.CreateEngineMaterial(shader);
        pass = new LitePass(material) { renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // No global keywords or modifications to the High/Ultra rendering path.
        if (!Application.isPlaying || GameSettings.CurrentGraphicsQuality > GameSettings.GraphicsQuality.Low ||
            renderingData.cameraData.cameraType != CameraType.Game ||
            !renderingData.cameraData.postProcessEnabled || pass == null) return;
        pass.grade = new Vector4(contrast, saturation, vignette, grain);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
        pass = null;
    }

    private sealed class LitePass : ScriptableRenderPass
    {
        private readonly Material material;
        private readonly ProfilingSampler sampler = new ProfilingSampler("Signal Post Retro Lite");
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        public Vector4 grade;
        private static readonly int PixelId = Shader.PropertyToID("_RetroPixel");
        private static readonly int PosterId = Shader.PropertyToID("_RetroPoster");
        private static readonly int ScanId = Shader.PropertyToID("_RetroScan");
        private static readonly int GradeId = Shader.PropertyToID("_RetroGrade");
        private static readonly int GhostId = Shader.PropertyToID("_RetroGhost");
        private static readonly int TimeId = Shader.PropertyToID("_RetroTime");
        private static readonly int SourceId = Shader.PropertyToID("_BlitTexture");
        private static readonly int ScaleId = Shader.PropertyToID("_BlitScaleBias");

        private sealed class Data
        {
            public Material material;
            public TextureHandle source;
            public Vector4 pixel, poster, scan, grade, ghost;
            public float time;
            public MaterialPropertyBlock properties;
        }

        public LitePass(Material material) { this.material = material; }

        private static bool HasOverride(VolumeComponent component)
        {
            if (component == null || !component.active) return false;
            var parameters = component.parameters;
            for (int i = 0; i < parameters.Count; i++)
                if (parameters[i].overrideState) return true;
            return false;
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer || !resources.cameraColor.IsValid()) return;
            var stack = VolumeManager.instance.stack;
            var pixel = stack.GetComponent<PixelizationVolumeComponent>();
            var poster = stack.GetComponent<PosterizeVolumeComponent>();
            var scan = stack.GetComponent<ScanlinesVolumeComponent>();
            var ghost = stack.GetComponent<VHSGhostingVolumeComponent>();
            // Inactive profile components leave active default instances in the Volume stack.
            // Defaults (notably pixel intensity 1 / size 4) must never enable an unused effect.
            bool pixelActive = HasOverride(pixel) && pixel.IsActive();
            bool posterActive = HasOverride(poster) && poster.IsActive();
            bool scanActive = HasOverride(scan) && scan.IsActive();
            bool ghostActive = HasOverride(ghost) && ghost.IsActive();
            if (!pixelActive && !posterActive && !scanActive && !ghostActive) return;
            var desc = graph.GetTextureDesc(resources.cameraColor);
            Vector4 p = new Vector4(pixelActive ? pixel.intensity.value : 0,
                pixelActive ? pixel.pixelSize.value : 1, desc.width, desc.height);
            Vector4 q = new Vector4(posterActive ? poster.intensity.value : 0,
                poster != null ? poster.numColors.value : 73, poster != null ? poster.colorOffset.value : 0,
                posterActive ? poster.ditherStrength.value : 0);
            Vector4 s = new Vector4(poster != null ? poster.ditherScale.value : 1,
                scanActive ? scan.strength.value : 0,
                scan != null ? scan.frequency.value : 0, scan != null ? scan.dispersion.value : 0);
            desc.name = "_SignalPostRetroLite";
            desc.clearBuffer = false;
            desc.depthBufferBits = DepthBits.None;
            desc.msaaSamples = MSAASamples.None;
            var destination = graph.CreateTexture(desc);
            using (var builder = graph.AddRasterRenderPass<Data>("Signal Post Retro Lite", out var data, sampler))
            {
                data.material = material;
                data.source = resources.cameraColor;
                data.pixel = p; data.poster = q; data.scan = s; data.grade = grade;
                data.ghost = ghostActive ? new Vector4(ghost.strength.value, ghost.xOffset.value, 0, 0) : Vector4.zero;
                data.time = Time.unscaledTime;
                data.properties = properties;
                builder.UseTexture(data.source, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (Data data, RasterGraphContext context) =>
                {
                    var properties = data.properties;
                    properties.SetTexture(SourceId, data.source);
                    properties.SetVector(ScaleId, new Vector4(1, 1, 0, 0));
                    properties.SetVector(PixelId, data.pixel);
                    properties.SetVector(PosterId, data.poster);
                    properties.SetVector(ScanId, data.scan);
                    properties.SetVector(GradeId, data.grade);
                    properties.SetVector(GhostId, data.ghost);
                    properties.SetFloat(TimeId, data.time);
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1, properties);
                });
            }
            resources.cameraColor = destination;
        }
    }
}
