using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[VolumeComponentMenu("Niidae/N_CcdOverflow")]
[VolumeRequiresRendererFeatures(typeof(NiidaeCustomEffectsRendererFeature))]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
[DisplayInfo(name = "N_CcdOverflow")]
public sealed class CcdOverflowVolumeComponent : VolumeComponent, IPostProcessComponent
{
    [Tooltip("Overall strength.")]
    public ClampedFloatParameter strength = new ClampedFloatParameter(0.8f, 0f, 1f);

    [Tooltip("Threshold for the bright area detection.")]
    public MinFloatParameter threshold = new MinFloatParameter(1.0f, 0f);

    [Tooltip("Gamma correction for overflowed brightness. (lower values will boost the dim artefacts and reduce the brighter ones, higher values will do the opposite)")]
    public ClampedFloatParameter gammaRemap = new ClampedFloatParameter(1.0f, 0.1f, 2f);

    [Tooltip("Purple tint strength.")]
    public ClampedFloatParameter purpleTint = new ClampedFloatParameter(0.8f, 0f, 1f);

    public bool IsActive() => strength.value > 0f;
}
