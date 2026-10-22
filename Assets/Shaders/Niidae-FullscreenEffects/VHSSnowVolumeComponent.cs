using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[VolumeComponentMenu("Niidae/N_VHS Snow")]
[VolumeRequiresRendererFeatures(typeof(NiidaeCustomEffectsRendererFeature))]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
[DisplayInfo(name = "N_VHS Snow")]
public sealed class VHSSnowVolumeComponent : VolumeComponent, IPostProcessComponent
{
    [Tooltip("Snow strength.")]
    public ClampedFloatParameter strength = new ClampedFloatParameter(0.75f, 0f, 1f);

    [Tooltip("Snow grain scale.")]
    public MinFloatParameter scale = new MinFloatParameter(32f, 0f);

    [FormerlySerializedAs("width")]
    [Tooltip("Snow grain aspect ratio.")]
    public MinFloatParameter aspectRatio = new MinFloatParameter(128f, 0.01f);

    [Tooltip("Snow threshold.")]
    public ClampedFloatParameter threshold = new ClampedFloatParameter(0.75f, 0f, 1f);

    [Tooltip("Footer snow multiplier.")]
    public FloatParameter footerMultiplier = new MinFloatParameter(0.25f, 0f);

    [Tooltip("Footer band height.")]
    public ClampedFloatParameter footerHeight = new ClampedFloatParameter(0.5f, 0f, 1f);

    public bool IsActive() => strength.value > 0f;
}
