using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[VolumeComponentMenu("Niidae/N_Scanlines")]
[VolumeRequiresRendererFeatures(typeof(NiidaeCustomEffectsRendererFeature))]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
[DisplayInfo(name = "N_Scanlines")]
public sealed class ScanlinesVolumeComponent : VolumeComponent, IPostProcessComponent
{
    [Tooltip("Scanlines strength.")]
    public ClampedFloatParameter strength = new ClampedFloatParameter(0.1f, 0f, 1f);

    [Tooltip("Scanlines frequency.")]
    public MinFloatParameter frequency = new MinFloatParameter(64f, 0f);

    [Tooltip("Scanline RGB dispersion.")]
    public ClampedFloatParameter dispersion = new ClampedFloatParameter(0f, 0f, 1f);

    public bool IsActive() => strength.value > 0f;
}
