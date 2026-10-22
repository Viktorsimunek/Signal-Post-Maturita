using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class NiidaeCustomEffectsRendererFeature : ScriptableRendererFeature
{
    private enum EffectType
    {
        Pixelization,
        Posterize,
        Bloom,
        VHSGhosting,
        VHSGrain,
        VHSJitter,
        VHSSnow,
        Scanlines,
        CcdOverflow
    }

    [Serializable]
    private sealed class EffectConfig
    {
        public EffectType effect;
        public Shader shader;

        [Tooltip("Injection point for this effect.")]
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

        [HideInInspector]
        public Material material;
    }

    private sealed class EffectRuntime
    {
        public EffectConfig config;
        public CombinedPass pass;
    }

    private const string kPosterizeShaderName = "Hidden/Custom/URP/PosterizeVolume";
    private const string kPixelizationShaderName = "Hidden/Custom/URP/PixelizationVolume";
    private const string kBloomShaderName = "Hidden/Custom/URP/BloomVolume";
    private const string kVHSGhostingShaderName = "Hidden/Custom/URP/VHSGhosting";
    private const string kVHSGrainShaderName = "Hidden/Custom/URP/VHSGrain";
    private const string kVHSJitterShaderName = "Hidden/Custom/URP/VHSJitter";
    private const string kVHSSnowShaderName = "Hidden/Custom/URP/VHSSnow";
    private const string kScanlinesShaderName = "Hidden/Custom/URP/Scanlines";
    private const string kCcdOverflowShaderName = "Hidden/Custom/URP/CcdOverflow";
    private static readonly int kGlobalFpsPropertyId = Shader.PropertyToID("_FPS");

    private static readonly EffectType[] kAllEffects =
    {
        EffectType.Pixelization,
        EffectType.Posterize,
        EffectType.Bloom,
        EffectType.VHSGhosting,
        EffectType.VHSGrain,
        EffectType.VHSJitter,
        EffectType.VHSSnow,
        EffectType.Scanlines,
        EffectType.CcdOverflow
    };

    [SerializeField]
    [Tooltip("Enable custom effects in Scene View cameras.")]
    private bool m_EnableInSceneView = true;

    public bool EnableInSceneView
    {
        get => m_EnableInSceneView;
        set => m_EnableInSceneView = value;
    }

    [SerializeField]
    [Tooltip("Execution order for custom effects when they share the same injection point.")]
    private EffectType[] m_EffectOrder =
    {
        EffectType.Pixelization,
        EffectType.Posterize,
        EffectType.Bloom,
        EffectType.VHSGhosting,
        EffectType.VHSGrain,
        EffectType.VHSJitter,
        EffectType.VHSSnow,
        EffectType.Scanlines,
        EffectType.CcdOverflow
    };

    [SerializeField]
    [Tooltip("Per-effect shader and injection point settings.")]
    private EffectConfig[] m_EffectConfigs = CreateDefaultEffectConfigs();

    [SerializeField]
    [Range(1f, 30f)]
    private float m_Framerate = 24f;

    // Legacy serialized fields kept for a one-time migration path.
    [SerializeField]
    [HideInInspector]
    private bool m_ConfigsMigrated;

    [SerializeField]
    [HideInInspector]
    private Shader m_PosterizeShader;

    [SerializeField]
    [HideInInspector]
    private RenderPassEvent m_PosterizeInjectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

    [SerializeField]
    [HideInInspector]
    private Shader m_PixelizationShader;

    [SerializeField]
    [HideInInspector]
    private RenderPassEvent m_PixelizationInjectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

    [SerializeField]
    [HideInInspector]
    private Shader m_BloomShader;

    [SerializeField]
    [HideInInspector]
    private RenderPassEvent m_BloomInjectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

    [SerializeField]
    [HideInInspector]
    private Shader m_VHSGhostingShader;

    [SerializeField]
    [HideInInspector]
    private RenderPassEvent m_VHSGhostingInjectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

    [SerializeField]
    [HideInInspector]
    private Shader m_VHSGrainShader;

    [SerializeField]
    [HideInInspector]
    private RenderPassEvent m_VHSGrainInjectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

    [SerializeField]
    [HideInInspector]
    private Shader m_VHSJitterShader;

    [SerializeField]
    [HideInInspector]
    private RenderPassEvent m_VHSJitterInjectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

    [SerializeField]
    [HideInInspector]
    private Shader m_VHSSnowShader;

    [SerializeField]
    [HideInInspector]
    private RenderPassEvent m_VHSSnowInjectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

    [SerializeField]
    [HideInInspector]
    private Shader m_ScanlinesShader;

    [SerializeField]
    [HideInInspector]
    private RenderPassEvent m_ScanlinesInjectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

    [SerializeField]
    [HideInInspector]
    private Shader m_CcdOverflowShader;

    [SerializeField]
    [HideInInspector]
    private RenderPassEvent m_CcdOverflowInjectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

    private readonly Dictionary<EffectType, EffectRuntime> m_RuntimeEffects = new Dictionary<EffectType, EffectRuntime>(8);
    private readonly Dictionary<EffectType, bool> m_ActiveEffects = new Dictionary<EffectType, bool>(8);
    private readonly HashSet<EffectType> m_QueuedEffects = new HashSet<EffectType>();

    public override void Create()
    {
        EnsureEffectConfigs();

        m_RuntimeEffects.Clear();
        for (int i = 0; i < m_EffectConfigs.Length; i++)
        {
            EffectConfig config = m_EffectConfigs[i];
            if (config == null)
                continue;

            ResolveShader(config);
            config.material = EnsureMaterial(config.material, config.shader);

            EffectRuntime runtime = new EffectRuntime();
            runtime.config = config;
            if (config.material != null)
            {
                string passName = string.Concat(name, " ", GetEffectDisplayName(config.effect));
                runtime.pass = new CombinedPass(passName, config.effect, config.material);
                runtime.pass.SetFrameRate(m_Framerate);
            }

            m_RuntimeEffects[config.effect] = runtime;
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        CameraType cameraType = renderingData.cameraData.cameraType;
        if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
            return;

        if (cameraType == CameraType.SceneView && !m_EnableInSceneView)
            return;

        Shader.SetGlobalFloat(kGlobalFpsPropertyId, Mathf.Max(1f, m_Framerate));
        ApplyFramerateToPasses();
        CacheActiveEffects(VolumeManager.instance.stack);

        m_QueuedEffects.Clear();

        if (m_EffectOrder != null)
        {
            for (int i = 0; i < m_EffectOrder.Length; i++)
                TryEnqueueEffect(m_EffectOrder[i], renderer);
        }

        // Fallback for missing or duplicate order entries.
        for (int i = 0; i < kAllEffects.Length; i++)
            TryEnqueueEffect(kAllEffects[i], renderer);
    }

    protected override void Dispose(bool disposing)
    {
        foreach (KeyValuePair<EffectType, EffectRuntime> runtimeEntry in m_RuntimeEffects)
            runtimeEntry.Value.pass?.Dispose();
        m_RuntimeEffects.Clear();

        if (m_EffectConfigs != null)
        {
            for (int i = 0; i < m_EffectConfigs.Length; i++)
            {
                EffectConfig config = m_EffectConfigs[i];
                if (config == null)
                    continue;

                CoreUtils.Destroy(config.material);
                config.material = null;
            }
        }

        m_ActiveEffects.Clear();
        m_QueuedEffects.Clear();
    }

    private static EffectConfig[] CreateDefaultEffectConfigs()
    {
        EffectConfig[] configs = new EffectConfig[kAllEffects.Length];
        for (int i = 0; i < kAllEffects.Length; i++)
        {
            EffectConfig config = new EffectConfig();
            config.effect = kAllEffects[i];
            config.injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
            configs[i] = config;
        }

        return configs;
    }

    private void EnsureEffectConfigs()
    {
        if (!m_ConfigsMigrated)
        {
            if (HasLegacyConfigurationData())
                m_EffectConfigs = CreateConfigsFromLegacy();

            m_ConfigsMigrated = true;
        }

        Dictionary<EffectType, EffectConfig> byType = new Dictionary<EffectType, EffectConfig>(kAllEffects.Length);
        if (m_EffectConfigs != null)
        {
            for (int i = 0; i < m_EffectConfigs.Length; i++)
            {
                EffectConfig config = m_EffectConfigs[i];
                if (config == null)
                    continue;

                if (!byType.ContainsKey(config.effect))
                    byType.Add(config.effect, config);
            }
        }

        bool changed = m_EffectConfigs == null || byType.Count != m_EffectConfigs.Length || byType.Count != kAllEffects.Length;
        EffectConfig[] normalized = new EffectConfig[kAllEffects.Length];

        for (int i = 0; i < kAllEffects.Length; i++)
        {
            EffectType effect = kAllEffects[i];
            if (!byType.TryGetValue(effect, out EffectConfig config))
            {
                config = new EffectConfig();
                config.effect = effect;
                config.injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
                changed = true;
            }

            normalized[i] = config;
        }

        if (changed)
            m_EffectConfigs = normalized;

        EnsureEffectOrderIncludesAllEffects();
    }

    private void EnsureEffectOrderIncludesAllEffects()
    {
        HashSet<EffectType> seen = new HashSet<EffectType>();
        List<EffectType> normalized = new List<EffectType>(kAllEffects.Length);

        if (m_EffectOrder != null)
        {
            for (int i = 0; i < m_EffectOrder.Length; i++)
            {
                EffectType effect = m_EffectOrder[i];
                if (seen.Add(effect))
                    normalized.Add(effect);
            }
        }

        for (int i = 0; i < kAllEffects.Length; i++)
        {
            EffectType effect = kAllEffects[i];
            if (seen.Add(effect))
                normalized.Add(effect);
        }

        if (m_EffectOrder == null || normalized.Count != m_EffectOrder.Length)
            m_EffectOrder = normalized.ToArray();
    }

    private bool HasLegacyConfigurationData()
    {
        return m_PosterizeShader != null || m_PixelizationShader != null || m_BloomShader != null || m_VHSGhostingShader != null ||
               m_VHSGrainShader != null || m_VHSJitterShader != null || m_VHSSnowShader != null || m_ScanlinesShader != null || m_CcdOverflowShader != null || 
               m_PosterizeInjectionPoint != RenderPassEvent.AfterRenderingPostProcessing ||
               m_PixelizationInjectionPoint != RenderPassEvent.AfterRenderingPostProcessing ||
               m_BloomInjectionPoint != RenderPassEvent.AfterRenderingPostProcessing ||
               m_VHSGhostingInjectionPoint != RenderPassEvent.AfterRenderingPostProcessing ||
               m_VHSGrainInjectionPoint != RenderPassEvent.AfterRenderingPostProcessing ||
               m_VHSJitterInjectionPoint != RenderPassEvent.AfterRenderingPostProcessing ||
               m_VHSSnowInjectionPoint != RenderPassEvent.AfterRenderingPostProcessing ||
               m_ScanlinesInjectionPoint != RenderPassEvent.AfterRenderingPostProcessing ||
               m_CcdOverflowInjectionPoint != RenderPassEvent.AfterRenderingPostProcessing;
    }

    private EffectConfig[] CreateConfigsFromLegacy()
    {
        return new[]
        {
            new EffectConfig { effect = EffectType.Pixelization, shader = m_PixelizationShader, injectionPoint = m_PixelizationInjectionPoint },
            new EffectConfig { effect = EffectType.Posterize, shader = m_PosterizeShader, injectionPoint = m_PosterizeInjectionPoint },
            new EffectConfig { effect = EffectType.Bloom, shader = m_BloomShader, injectionPoint = m_BloomInjectionPoint },
            new EffectConfig { effect = EffectType.VHSGhosting, shader = m_VHSGhostingShader, injectionPoint = m_VHSGhostingInjectionPoint },
            new EffectConfig { effect = EffectType.VHSGrain, shader = m_VHSGrainShader, injectionPoint = m_VHSGrainInjectionPoint },
            new EffectConfig { effect = EffectType.VHSJitter, shader = m_VHSJitterShader, injectionPoint = m_VHSJitterInjectionPoint },
            new EffectConfig { effect = EffectType.VHSSnow, shader = m_VHSSnowShader, injectionPoint = m_VHSSnowInjectionPoint },
            new EffectConfig { effect = EffectType.Scanlines, shader = m_ScanlinesShader, injectionPoint = m_ScanlinesInjectionPoint },
            new EffectConfig { effect = EffectType.CcdOverflow, shader = m_CcdOverflowShader, injectionPoint = m_CcdOverflowInjectionPoint }
        };
    }

    private void ApplyFramerateToPasses()
    {
        foreach (KeyValuePair<EffectType, EffectRuntime> runtimeEntry in m_RuntimeEffects)
            runtimeEntry.Value.pass?.SetFrameRate(m_Framerate);
    }

    private void CacheActiveEffects(VolumeStack stack)
    {
        m_ActiveEffects.Clear();
        for (int i = 0; i < kAllEffects.Length; i++)
        {
            EffectType effect = kAllEffects[i];
            m_ActiveEffects[effect] = IsEffectActive(effect, stack);
        }
    }

    private static bool HasAnyParameterOverride(VolumeComponent component)
    {
        if (component == null || !component.active)
            return false;

        IReadOnlyList<VolumeParameter> parameters = component.parameters;
        for (int i = 0; i < parameters.Count; i++)
        {
            VolumeParameter parameter = parameters[i];
            if (parameter != null && parameter.overrideState)
                return true;
        }

        return false;
    }

    private static bool IsComponentActive<T>(VolumeStack stack, Func<T, bool> isActive) where T : VolumeComponent
    {
        T component = stack?.GetComponent<T>();
        return component != null && HasAnyParameterOverride(component) && isActive(component);
    }

    private static bool IsEffectActive(EffectType effect, VolumeStack stack)
    {
        switch (effect)
        {
            case EffectType.Posterize:
                return IsComponentActive<PosterizeVolumeComponent>(stack, static c => c.IsActive());
            case EffectType.Pixelization:
                return IsComponentActive<PixelizationVolumeComponent>(stack, static c => c.IsActive());
            case EffectType.Bloom:
                return IsComponentActive<BloomVolumeComponent>(stack, static c => c.IsActive());
            case EffectType.VHSGhosting:
                return IsComponentActive<VHSGhostingVolumeComponent>(stack, static c => c.IsActive());
            case EffectType.VHSGrain:
                return IsComponentActive<VHSGrainVolumeComponent>(stack, static c => c.IsActive());
            case EffectType.VHSJitter:
                return IsComponentActive<VHSJitterVolumeComponent>(stack, static c => c.IsActive());
            case EffectType.VHSSnow:
                return IsComponentActive<VHSSnowVolumeComponent>(stack, static c => c.IsActive());
            case EffectType.Scanlines:
                return IsComponentActive<ScanlinesVolumeComponent>(stack, static c => c.IsActive());
            case EffectType.CcdOverflow:
                return IsComponentActive<CcdOverflowVolumeComponent>(stack, static c => c.IsActive());
            default:
                return false;
        }
    }

    private void TryEnqueueEffect(EffectType effect, ScriptableRenderer renderer)
    {
        if (m_QueuedEffects.Contains(effect))
            return;

        if (!m_ActiveEffects.TryGetValue(effect, out bool active) || !active)
            return;

        if (!m_RuntimeEffects.TryGetValue(effect, out EffectRuntime runtime) || runtime == null || runtime.pass == null || runtime.config == null)
            return;

        runtime.pass.renderPassEvent = runtime.config.injectionPoint;
        runtime.pass.ConfigureInput(ScriptableRenderPassInput.None);
        renderer.EnqueuePass(runtime.pass);
        m_QueuedEffects.Add(effect);
    }

    private static void ResolveShader(EffectConfig config)
    {
#if UNITY_EDITOR
        if (config.shader == null)
        {
            string editorPath = GetEditorShaderPath(config.effect);
            if (!string.IsNullOrEmpty(editorPath))
                config.shader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(editorPath);
        }
#endif

        if (config.shader == null)
        {
            string shaderName = GetHiddenShaderName(config.effect);
            if (!string.IsNullOrEmpty(shaderName))
                config.shader = Shader.Find(shaderName);
        }
    }

    private static Material EnsureMaterial(Material material, Shader shader)
    {
        if (shader == null)
        {
            if (material != null)
                CoreUtils.Destroy(material);
            return null;
        }

        if (material != null && material.shader == shader)
            return material;

        if (material != null)
            CoreUtils.Destroy(material);

        return CoreUtils.CreateEngineMaterial(shader);
    }

    private static string GetHiddenShaderName(EffectType effect)
    {
        switch (effect)
        {
            case EffectType.Posterize:
                return kPosterizeShaderName;
            case EffectType.Pixelization:
                return kPixelizationShaderName;
            case EffectType.Bloom:
                return kBloomShaderName;
            case EffectType.VHSGhosting:
                return kVHSGhostingShaderName;
            case EffectType.VHSGrain:
                return kVHSGrainShaderName;
            case EffectType.VHSJitter:
                return kVHSJitterShaderName;
            case EffectType.VHSSnow:
                return kVHSSnowShaderName;
            case EffectType.Scanlines:
                return kScanlinesShaderName;
            case EffectType.CcdOverflow:
                return kCcdOverflowShaderName;
            default:
                return string.Empty;
        }
    }

#if UNITY_EDITOR
    private static string GetEditorShaderPath(EffectType effect)
    {
        switch (effect)
        {
            case EffectType.Posterize:
                return "Assets/Shaders/Fullscreen/Posterize.shader";
            case EffectType.Pixelization:
                return "Assets/Shaders/Fullscreen/Pixelization.shader";
            case EffectType.Bloom:
                return "Assets/Shaders/Fullscreen/Bloom.shader";
            case EffectType.VHSGhosting:
                return "Assets/Shaders/Fullscreen/VHSGhosting.shader";
            case EffectType.VHSGrain:
                return "Assets/Shaders/Fullscreen/VHSGrain.shader";
            case EffectType.VHSJitter:
                return "Assets/Shaders/Fullscreen/VHSJitter.shader";
            case EffectType.VHSSnow:
                return "Assets/Shaders/Fullscreen/VHSSnow.shader";
            case EffectType.Scanlines:
                return "Assets/Shaders/Fullscreen/Scanlines.shader";
            case EffectType.CcdOverflow:
                return "Assets/Shaders/Fullscreen/CcdOverflow.shader";
            default:
                return string.Empty;
        }
    }
#endif

    private static string GetEffectDisplayName(EffectType effect)
    {
        switch (effect)
        {
            case EffectType.Posterize:
                return "Posterize";
            case EffectType.Pixelization:
                return "Pixelization";
            case EffectType.Bloom:
                return "Bloom";
            case EffectType.VHSGhosting:
                return "VHS Ghosting";
            case EffectType.VHSGrain:
                return "VHS Grain";
            case EffectType.VHSJitter:
                return "VHS Jitter";
            case EffectType.VHSSnow:
                return "VHS Snow";
            case EffectType.Scanlines:
                return "Scanlines";
            case EffectType.CcdOverflow:
                return "CcdOverflow";
            default:
                return effect.ToString();
        }
    }

    private sealed class CombinedPass : ScriptableRenderPass
    {
        private enum PassMode
        {
            Posterize = 1,
            Pixelization = 2,
            BloomPrefilter = 3,
            BloomBlur = 4,
            BloomComposite = 5,
            VHSGhosting = 6,
            VHSGrain = 7,
            VHSJitter = 8,
            VHSSnow = 9,
            Scanlines = 10,
            CcdOverflow = 11
        }

        private static readonly int kBlitTexturePropertyId = Shader.PropertyToID("_BlitTexture");
        private static readonly int kBlitScaleBiasPropertyId = Shader.PropertyToID("_BlitScaleBias");
        private static readonly int kFpsId = Shader.PropertyToID("_FPS");
        private static readonly int kUnscaledTimeId = Shader.PropertyToID("_UnscaledTime");

        private static readonly int kPosterizeIntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int kPosterizeNumColorsId = Shader.PropertyToID("_NumColors");
        private static readonly int kPosterizeColorOffsetId = Shader.PropertyToID("_ColorOffset");
        private static readonly int kPosterizeDitherStrengthId = Shader.PropertyToID("_DitherStrength");
        private static readonly int kPosterizeDitherScaleId = Shader.PropertyToID("_DitherScale");

        private static readonly int kPixelizationIntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int kPixelizationPixelSizeId = Shader.PropertyToID("_PixelSize");

        private static readonly int kBloomParamsId = Shader.PropertyToID("_Params");
        private static readonly int kBloomBlurParamsId = Shader.PropertyToID("_BlurParams");
        private static readonly int kBloomTexId = Shader.PropertyToID("_BloomTex");
        private static readonly int kBloomTex2Id = Shader.PropertyToID("_BloomTex2");
        private static readonly int kBloomCompositeParamsId = Shader.PropertyToID("_CompositeParams");
        private static readonly int kBloomMultiScaleParamsId = Shader.PropertyToID("_MultiScaleParams");
        private static readonly int kBloomReservedParamsId = Shader.PropertyToID("_ReservedParams");

        private static readonly int kVHSGhostingStrengthId = Shader.PropertyToID("_Strength");
        private static readonly int kVHSGhostingXOffsetId = Shader.PropertyToID("_XOffset");
        private static readonly int kVHSGhostingRadiusId = Shader.PropertyToID("_Radius");

        private static readonly int kVHSGrainStrengthId = Shader.PropertyToID("_GrainStrength");
        private static readonly int kVHSGrainScaleId = Shader.PropertyToID("_GrainScale");
        private static readonly int kVHSGrainAspectId = Shader.PropertyToID("_GrainAspect");
        private static readonly int kVHSChromaStrengthId = Shader.PropertyToID("_ChromaStrength");
        private static readonly int kVHSChromaScaleId = Shader.PropertyToID("_ChromaScale");
        private static readonly int kVHSChromaAspectId = Shader.PropertyToID("_ChromaAspect");

        private static readonly int kVHSJitterStrengthId = Shader.PropertyToID("_Strength");
        private static readonly int kVHSJitterSpeedId = Shader.PropertyToID("_Speed");
        private static readonly int kVHSJitterFrequencyId = Shader.PropertyToID("_Frequency");
        private static readonly int kVHSJitterGammaId = Shader.PropertyToID("_Gamma");
        private static readonly int kVHSJitterFooterJitterId = Shader.PropertyToID("_FooterJitter");
        private static readonly int kVHSJitterFooterHeightId = Shader.PropertyToID("_FooterHeight");

        private static readonly int kVHSSnowStrengthId = Shader.PropertyToID("_Strength");
        private static readonly int kVHSSnowScaleId = Shader.PropertyToID("_Scale");
        private static readonly int kVHSSnowThresholdId = Shader.PropertyToID("_Threshold");
        private static readonly int kVHSSnowFooterMultiplierId = Shader.PropertyToID("_FooterMultiplier");
        private static readonly int kVHSSnowFooterHeightId = Shader.PropertyToID("_FooterHeight");

        private static readonly int kScanlinesStrengthId = Shader.PropertyToID("_Strength");
        private static readonly int kScanlinesFrequencyId = Shader.PropertyToID("_Frequency");
        private static readonly int kScanlinesDispersionId = Shader.PropertyToID("_Dispersion");
        
        private static readonly int kCcdOverflowStrengthId = Shader.PropertyToID("_Strength");
        private static readonly int kCcdOverflowThresholdId = Shader.PropertyToID("_Threshold");
        private static readonly int kCcdOverflowGammaId = Shader.PropertyToID("_Gamma");
        private static readonly int kCcdOverflowPurpleTintId = Shader.PropertyToID("_PurpleTint");

        private readonly EffectType m_Effect;
        private readonly Material m_Material;

        private static readonly MaterialPropertyBlock s_PropertyBlock = new MaterialPropertyBlock();
        private float m_Framerate = 24f;

        private sealed class PassData
        {
            public Material material;
            public TextureHandle source;
            public TextureHandle extraTex0;
            public TextureHandle extraTex1;
            public int shaderPass;
            public PassMode mode;
            public float fps;
            public float unscaledTime;
            public Vector4 p0;
            public Vector4 p1;
            public Vector4 p2;
        }

        public CombinedPass(string passName, EffectType effect, Material material)
        {
            profilingSampler = new ProfilingSampler(passName);
            m_Effect = effect;
            m_Material = material;
            requiresIntermediateTexture = true;
        }

        public void Dispose()
        {
        }

        public void SetFrameRate(float framerate)
        {
            m_Framerate = Mathf.Max(1f, framerate);
        }

        private static void Execute(PassData data, RasterGraphContext context)
        {
            s_PropertyBlock.Clear();
            s_PropertyBlock.SetVector(kBlitScaleBiasPropertyId, new Vector4(1f, 1f, 0f, 0f));
            if (data.source.IsValid())
                s_PropertyBlock.SetTexture(kBlitTexturePropertyId, data.source);
            s_PropertyBlock.SetFloat(kFpsId, data.fps);
            s_PropertyBlock.SetFloat(kUnscaledTimeId, data.unscaledTime);

            switch (data.mode)
            {
                case PassMode.Posterize:
                    s_PropertyBlock.SetFloat(kPosterizeIntensityId, data.p0.x);
                    s_PropertyBlock.SetFloat(kPosterizeNumColorsId, data.p0.y);
                    s_PropertyBlock.SetFloat(kPosterizeColorOffsetId, data.p0.z);
                    s_PropertyBlock.SetFloat(kPosterizeDitherStrengthId, data.p0.w);
                    s_PropertyBlock.SetFloat(kPosterizeDitherScaleId, data.p1.x);
                    break;

                case PassMode.Pixelization:
                    s_PropertyBlock.SetFloat(kPixelizationIntensityId, data.p0.x);
                    s_PropertyBlock.SetFloat(kPixelizationPixelSizeId, data.p0.y);
                    break;

                case PassMode.BloomPrefilter:
                    s_PropertyBlock.SetVector(kBloomParamsId, data.p0);
                    break;

                case PassMode.BloomBlur:
                    s_PropertyBlock.SetVector(kBloomBlurParamsId, data.p0);
                    break;

                case PassMode.BloomComposite:
                    s_PropertyBlock.SetTexture(kBloomTexId, data.extraTex0);
                    s_PropertyBlock.SetTexture(kBloomTex2Id, data.extraTex1);
                    s_PropertyBlock.SetVector(kBloomCompositeParamsId, data.p0);
                    s_PropertyBlock.SetVector(kBloomMultiScaleParamsId, data.p1);
                    s_PropertyBlock.SetVector(kBloomReservedParamsId, data.p2);
                    break;

                case PassMode.VHSGhosting:
                    s_PropertyBlock.SetFloat(kVHSGhostingStrengthId, data.p0.x);
                    s_PropertyBlock.SetFloat(kVHSGhostingXOffsetId, data.p0.y);
                    s_PropertyBlock.SetFloat(kVHSGhostingRadiusId, data.p0.z);
                    break;

                case PassMode.VHSGrain:
                    s_PropertyBlock.SetFloat(kVHSGrainStrengthId, data.p0.x);
                    s_PropertyBlock.SetFloat(kVHSGrainScaleId, data.p0.y);
                    s_PropertyBlock.SetFloat(kVHSGrainAspectId, data.p0.z);
                    s_PropertyBlock.SetFloat(kVHSChromaStrengthId, data.p0.w);
                    s_PropertyBlock.SetFloat(kVHSChromaScaleId, data.p1.x);
                    s_PropertyBlock.SetFloat(kVHSChromaAspectId, data.p1.y);
                    break;

                case PassMode.VHSJitter:
                    s_PropertyBlock.SetFloat(kVHSJitterStrengthId, data.p0.x);
                    s_PropertyBlock.SetFloat(kVHSJitterSpeedId, data.p0.y);
                    s_PropertyBlock.SetFloat(kVHSJitterFrequencyId, data.p0.z);
                    s_PropertyBlock.SetFloat(kVHSJitterGammaId, data.p0.w);
                    s_PropertyBlock.SetFloat(kVHSJitterFooterJitterId, data.p1.x);
                    s_PropertyBlock.SetFloat(kVHSJitterFooterHeightId, data.p1.y);
                    break;

                case PassMode.VHSSnow:
                    s_PropertyBlock.SetFloat(kVHSSnowStrengthId, data.p0.x);
                    s_PropertyBlock.SetVector(kVHSSnowScaleId, new Vector4(data.p0.y, data.p0.z, 0f, 0f));
                    s_PropertyBlock.SetFloat(kVHSSnowThresholdId, data.p1.z);
                    s_PropertyBlock.SetFloat(kVHSSnowFooterMultiplierId, data.p1.x);
                    s_PropertyBlock.SetFloat(kVHSSnowFooterHeightId, data.p1.y);
                    break;

                case PassMode.Scanlines:
                    s_PropertyBlock.SetFloat(kScanlinesStrengthId, data.p0.x);
                    s_PropertyBlock.SetFloat(kScanlinesFrequencyId, data.p0.y);
                    s_PropertyBlock.SetFloat(kScanlinesDispersionId, data.p0.z);
                    break;

                case PassMode.CcdOverflow:
                    s_PropertyBlock.SetFloat(kCcdOverflowStrengthId, data.p0.x);
                    s_PropertyBlock.SetFloat(kCcdOverflowThresholdId, data.p0.y);
                    s_PropertyBlock.SetFloat(kCcdOverflowGammaId, data.p0.z);
                    s_PropertyBlock.SetFloat(kCcdOverflowPurpleTintId, data.p0.w);
                    break;
            }

            context.cmd.DrawProcedural(Matrix4x4.identity, data.material, data.shaderPass, MeshTopology.Triangles, 3, 1, s_PropertyBlock);
        }

        private TextureHandle RunFullscreen(
            RenderGraph renderGraph,
            TextureHandle source,
            Material material,
            int shaderPass,
            PassMode mode,
            string passName,
            Vector4 p0,
            Vector4 p1,
            Vector4 p2,
            TextureHandle extraTex0 = default,
            TextureHandle extraTex1 = default)
        {
            TextureDesc dstDesc = renderGraph.GetTextureDesc(source);
            dstDesc.clearBuffer = false;
            dstDesc.msaaSamples = MSAASamples.None;
            dstDesc.depthBufferBits = DepthBits.None;
            dstDesc.name = passName + "_Output";
            TextureHandle destination = renderGraph.CreateTexture(dstDesc);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out PassData passData, profilingSampler))
            {
                passData.material = material;
                passData.source = source;
                passData.extraTex0 = extraTex0;
                passData.extraTex1 = extraTex1;
                passData.shaderPass = shaderPass;
                passData.mode = mode;
                passData.fps = m_Framerate;
                passData.unscaledTime = Time.unscaledTime;
                passData.p0 = p0;
                passData.p1 = p1;
                passData.p2 = p2;

                builder.UseTexture(passData.source, AccessFlags.Read);
                if (passData.extraTex0.IsValid())
                    builder.UseTexture(passData.extraTex0, AccessFlags.Read);
                if (passData.extraTex1.IsValid())
                    builder.UseTexture(passData.extraTex1, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) => Execute(data, ctx));
            }

            return destination;
        }

        private TextureHandle RunFullscreenTo(
            RenderGraph renderGraph,
            TextureHandle source,
            TextureHandle destination,
            Material material,
            int shaderPass,
            PassMode mode,
            string passName,
            Vector4 p0,
            Vector4 p1,
            Vector4 p2,
            TextureHandle extraTex0 = default,
            TextureHandle extraTex1 = default)
        {
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out PassData passData, profilingSampler))
            {
                passData.material = material;
                passData.source = source;
                passData.extraTex0 = extraTex0;
                passData.extraTex1 = extraTex1;
                passData.shaderPass = shaderPass;
                passData.mode = mode;
                passData.fps = m_Framerate;
                passData.unscaledTime = Time.unscaledTime;
                passData.p0 = p0;
                passData.p1 = p1;
                passData.p2 = p2;

                builder.UseTexture(passData.source, AccessFlags.Read);
                if (passData.extraTex0.IsValid())
                    builder.UseTexture(passData.extraTex0, AccessFlags.Read);
                if (passData.extraTex1.IsValid())
                    builder.UseTexture(passData.extraTex1, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) => Execute(data, ctx));
            }

            return destination;
        }

        private static int ComputeIterations(int radius)
        {
            return Mathf.Clamp(radius, 1, 8);
        }

        private TextureHandle ExecuteBloom(RenderGraph renderGraph, TextureHandle color, BloomVolumeComponent bloom)
        {
            var srcDesc = color.GetDescriptor(renderGraph);
            int iterations = ComputeIterations(bloom.radius.value);

            TextureDesc nearDesc = renderGraph.GetTextureDesc(color);
            nearDesc.clearBuffer = false;
            nearDesc.msaaSamples = MSAASamples.None;
            nearDesc.depthBufferBits = DepthBits.None;
            nearDesc.width = Mathf.Max(1, srcDesc.width / Mathf.Clamp(bloom.downscaleNear.value, 1, 4));
            nearDesc.height = Mathf.Max(1, srcDesc.height / Mathf.Clamp(bloom.downscaleNear.value, 1, 4));
            nearDesc.name = "_NiidaeCombinedBloomNearA";
            TextureHandle nearA = renderGraph.CreateTexture(nearDesc);

            nearDesc.name = "_NiidaeCombinedBloomNearBlurA";
            TextureHandle nearBlurA = renderGraph.CreateTexture(nearDesc);
            nearDesc.name = "_NiidaeCombinedBloomNearBlurB";
            TextureHandle nearBlurB = renderGraph.CreateTexture(nearDesc);

            int farDown = Mathf.Max(Mathf.Clamp(bloom.downscaleFar.value, 2, 8), Mathf.Clamp(bloom.downscaleNear.value, 1, 4));
            TextureDesc farDesc = renderGraph.GetTextureDesc(color);
            farDesc.clearBuffer = false;
            farDesc.msaaSamples = MSAASamples.None;
            farDesc.depthBufferBits = DepthBits.None;
            farDesc.width = Mathf.Max(1, srcDesc.width / farDown);
            farDesc.height = Mathf.Max(1, srcDesc.height / farDown);
            farDesc.name = "_NiidaeCombinedBloomFarA";
            TextureHandle farA = renderGraph.CreateTexture(farDesc);

            farDesc.name = "_NiidaeCombinedBloomFarBlurA";
            TextureHandle farBlurA = renderGraph.CreateTexture(farDesc);
            farDesc.name = "_NiidaeCombinedBloomFarBlurB";
            TextureHandle farBlurB = renderGraph.CreateTexture(farDesc);

            Vector4 bloomPrefilterParams = new Vector4(Mathf.Max(0f, bloom.threshold.value), Mathf.Clamp01(bloom.knee.value), 0f, 0f);
            nearA = RunFullscreenTo(renderGraph, color, nearA, m_Material, 0, PassMode.BloomPrefilter, "Niidae Combined Bloom Prefilter Near", bloomPrefilterParams, Vector4.zero, Vector4.zero);
            farA = RunFullscreenTo(renderGraph, color, farA, m_Material, 0, PassMode.BloomPrefilter, "Niidae Combined Bloom Prefilter Far", bloomPrefilterParams, Vector4.zero, Vector4.zero);

            TextureHandle currentNear = nearA;
            TextureHandle currentFar = farA;
            float radiusT = Mathf.Clamp01((bloom.radius.value - 1f) / 15f);
            float nearScale = Mathf.Lerp(0.8f, 2.2f, radiusT) * Mathf.Max(0.25f, bloom.blurRadiusNear.value);
            float farScale = Mathf.Lerp(0.8f, 2.2f, radiusT) * Mathf.Max(0.25f, bloom.blurRadiusFar.value);

            for (int i = 0; i < iterations; i++)
            {
                currentNear = RunFullscreenTo(renderGraph, currentNear, nearBlurA, m_Material, 1, PassMode.BloomBlur, $"Niidae Combined Bloom Near Blur A {i}", new Vector4(0f, 0f, nearScale, 0.3926991f * (i + 1)), Vector4.zero, Vector4.zero);
                currentNear = RunFullscreenTo(renderGraph, currentNear, nearBlurB, m_Material, 2, PassMode.BloomBlur, $"Niidae Combined Bloom Near Blur B {i}", new Vector4(0f, 0f, nearScale, 1.1780972f * (i + 1)), Vector4.zero, Vector4.zero);

                currentFar = RunFullscreenTo(renderGraph, currentFar, farBlurA, m_Material, 1, PassMode.BloomBlur, $"Niidae Combined Bloom Far Blur A {i}", new Vector4(0f, 0f, farScale, 0.7853982f * (i + 1)), Vector4.zero, Vector4.zero);
                currentFar = RunFullscreenTo(renderGraph, currentFar, farBlurB, m_Material, 2, PassMode.BloomBlur, $"Niidae Combined Bloom Far Blur B {i}", new Vector4(0f, 0f, farScale, 1.9634954f * (i + 1)), Vector4.zero, Vector4.zero);
            }

            Vector4 compositeParams = new Vector4(bloom.intensity.value, bloom.steps.value, bloom.saturation.value, bloom.gammaBefore.value);
            Vector4 multiScaleParams = new Vector4(0.75f, 0.5f, 0f, 0f);
            Vector4 reservedParams = new Vector4(bloom.gammaAfter.value, 0f, 0f, 0f);

            return RunFullscreen(
                renderGraph,
                color,
                m_Material,
                shaderPass: 3,
                mode: PassMode.BloomComposite,
                passName: "Niidae Combined Bloom Composite",
                p0: compositeParams,
                p1: multiScaleParams,
                p2: reservedParams,
                extraTex0: currentNear,
                extraTex1: currentFar);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer)
                return;

            TextureHandle color = resources.cameraColor;
            if (!color.IsValid() || m_Material == null)
                return;

            VolumeStack stack = VolumeManager.instance.stack;
            if (stack == null)
                return;

            switch (m_Effect)
            {
                case EffectType.Posterize:
                {
                    PosterizeVolumeComponent posterize = stack.GetComponent<PosterizeVolumeComponent>();
                    if (posterize == null || !posterize.IsActive())
                        return;

                    color = RunFullscreen(
                        renderGraph,
                        color,
                        m_Material,
                        shaderPass: 0,
                        mode: PassMode.Posterize,
                        passName: "Niidae Combined Posterize",
                        p0: new Vector4(posterize.intensity.value, posterize.numColors.value, posterize.colorOffset.value, posterize.ditherStrength.value),
                        p1: new Vector4(posterize.ditherScale.value, 0f, 0f, 0f),
                        p2: Vector4.zero);
                    break;
                }

                case EffectType.Pixelization:
                {
                    PixelizationVolumeComponent pixelization = stack.GetComponent<PixelizationVolumeComponent>();
                    if (pixelization == null || !pixelization.IsActive())
                        return;

                    color = RunFullscreen(
                        renderGraph,
                        color,
                        m_Material,
                        shaderPass: 0,
                        mode: PassMode.Pixelization,
                        passName: "Niidae Combined Pixelization",
                        p0: new Vector4(pixelization.intensity.value, pixelization.pixelSize.value, 0f, 0f),
                        p1: Vector4.zero,
                        p2: Vector4.zero);
                    break;
                }

                case EffectType.Bloom:
                {
                    BloomVolumeComponent bloom = stack.GetComponent<BloomVolumeComponent>();
                    if (bloom == null || !bloom.IsActive())
                        return;

                    color = ExecuteBloom(renderGraph, color, bloom);
                    break;
                }

                case EffectType.VHSGhosting:
                {
                    VHSGhostingVolumeComponent vhsGhosting = stack.GetComponent<VHSGhostingVolumeComponent>();
                    if (vhsGhosting == null || !vhsGhosting.IsActive())
                        return;

                    color = RunFullscreen(
                        renderGraph,
                        color,
                        m_Material,
                        shaderPass: 0,
                        mode: PassMode.VHSGhosting,
                        passName: "Niidae Combined VHS Ghosting",
                        p0: new Vector4(vhsGhosting.strength.value, vhsGhosting.xOffset.value, vhsGhosting.radius.value, 0f),
                        p1: Vector4.zero,
                        p2: Vector4.zero);
                    break;
                }

                case EffectType.VHSGrain:
                {
                    VHSGrainVolumeComponent vhsGrain = stack.GetComponent<VHSGrainVolumeComponent>();
                    if (vhsGrain == null || !vhsGrain.IsActive())
                        return;

                    color = RunFullscreen(
                        renderGraph,
                        color,
                        m_Material,
                        shaderPass: 0,
                        mode: PassMode.VHSGrain,
                        passName: "Niidae Combined VHS Grain",
                        p0: new Vector4(vhsGrain.grainStrength.value, vhsGrain.grainScale.value, vhsGrain.grainAspect.value, vhsGrain.chromaStrength.value),
                        p1: new Vector4(vhsGrain.chromaScale.value, vhsGrain.chromaAspect.value, 0f, 0f),
                        p2: Vector4.zero);
                    break;
                }

                case EffectType.VHSJitter:
                {
                    VHSJitterVolumeComponent vhsJitter = stack.GetComponent<VHSJitterVolumeComponent>();
                    if (vhsJitter == null || !vhsJitter.IsActive())
                        return;

                    color = RunFullscreen(
                        renderGraph,
                        color,
                        m_Material,
                        shaderPass: 0,
                        mode: PassMode.VHSJitter,
                        passName: "Niidae Combined VHS Jitter",
                        p0: new Vector4(vhsJitter.strength.value, vhsJitter.speed.value, vhsJitter.frequency.value, vhsJitter.gamma.value),
                        p1: new Vector4(vhsJitter.footerJitter.value, vhsJitter.footerHeight.value, 0f, 0f),
                        p2: Vector4.zero);
                    break;
                }

                case EffectType.VHSSnow:
                {
                    VHSSnowVolumeComponent vhsSnow = stack.GetComponent<VHSSnowVolumeComponent>();
                    if (vhsSnow == null || !vhsSnow.IsActive())
                        return;

                    color = RunFullscreen(
                        renderGraph,
                        color,
                        m_Material,
                        shaderPass: 0,
                        mode: PassMode.VHSSnow,
                        passName: "Niidae Combined VHS Snow",
                        p0: new Vector4(vhsSnow.strength.value, vhsSnow.scale.value, vhsSnow.aspectRatio.value, 0f),
                        p1: new Vector4(vhsSnow.footerMultiplier.value, vhsSnow.footerHeight.value, vhsSnow.threshold.value, 0f),
                        p2: Vector4.zero);
                    break;
                }

                case EffectType.Scanlines:
                {
                    ScanlinesVolumeComponent scanlines = stack.GetComponent<ScanlinesVolumeComponent>();
                    if (scanlines == null || !scanlines.IsActive())
                        return;

                    color = RunFullscreen(
                        renderGraph,
                        color,
                        m_Material,
                        shaderPass: 0,
                        mode: PassMode.Scanlines,
                        passName: "Niidae Combined Scanlines",
                        p0: new Vector4(scanlines.strength.value, scanlines.frequency.value, scanlines.dispersion.value, 0f),
                        p1: Vector4.zero,
                        p2: Vector4.zero);
                    break;
                }

                case EffectType.CcdOverflow:
                {
                    CcdOverflowVolumeComponent ccdOverflow = stack.GetComponent<CcdOverflowVolumeComponent>();
                    if (ccdOverflow == null || !ccdOverflow.IsActive())
                        return;

                    color = RunFullscreen(
                        renderGraph,
                        color,
                        m_Material,
                        shaderPass: 0,
                        mode: PassMode.CcdOverflow,
                        passName: "Niidae Combined Ccd Overflow",
                        p0: new Vector4(ccdOverflow.strength.value, ccdOverflow.threshold.value, ccdOverflow.gammaRemap.value, ccdOverflow.purpleTint.value),
                        p1: Vector4.zero,
                        p2: Vector4.zero);
                    break;
                }

                default:
                    return;
            }

            resources.cameraColor = color;
        }
    }
}
