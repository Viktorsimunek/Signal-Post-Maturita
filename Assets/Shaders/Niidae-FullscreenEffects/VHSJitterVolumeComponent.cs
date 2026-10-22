using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[VolumeComponentMenu("Niidae/N_VHS Jitter")]
[VolumeRequiresRendererFeatures(typeof(NiidaeCustomEffectsRendererFeature))]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
[DisplayInfo(name = "N_VHS Jitter")]
public sealed class VHSJitterVolumeComponent : VolumeComponent, IPostProcessComponent
{
    [Tooltip("Jitter strength.")]
    public ClampedFloatParameter strength = new ClampedFloatParameter(0.15f, 0f, 1f);

    [Tooltip("Jitter speed.")]
    public MinFloatParameter speed = new MinFloatParameter(32f, 0f);

    [Tooltip("Jitter frequency.")]
    public MinFloatParameter frequency = new MinFloatParameter(64f, 0f);

    [Tooltip("Jitter gamma.")]
    public ClampedFloatParameter gamma = new ClampedFloatParameter(1f, 0.5f, 2f);

    [Tooltip("Footer jitter amount.")]
    public ClampedFloatParameter footerJitter = new ClampedFloatParameter(0.4f, 0f, 1f);

    [Tooltip("Footer band height.")]
    public ClampedFloatParameter footerHeight = new ClampedFloatParameter(0.25f, 0f, 1f);

    public bool IsActive() => strength.value > 0f || footerJitter.value > 0f;
}
