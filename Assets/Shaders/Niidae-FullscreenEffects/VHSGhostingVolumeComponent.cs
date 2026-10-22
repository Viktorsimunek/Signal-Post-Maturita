using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[VolumeComponentMenu("Niidae/N_VHS Ghosting")]
[VolumeRequiresRendererFeatures(typeof(NiidaeCustomEffectsRendererFeature))]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
[DisplayInfo(name = "N_VHS Ghosting")]
public sealed class VHSGhostingVolumeComponent : VolumeComponent, IPostProcessComponent
{
    [Tooltip("Ghosting strength.")]
    public ClampedFloatParameter strength = new ClampedFloatParameter(0.2f, -1f, 1f);

    [Tooltip("Horizontal ghost offset.")]
    public ClampedFloatParameter xOffset = new ClampedFloatParameter(0.02f, -0.2f, 0.2f);

    [Tooltip("Ghost radius.")]
    public ClampedIntParameter radius = new ClampedIntParameter(1, 1, 3);

    public bool IsActive() => Mathf.Abs(strength.value) > 0f;
}
