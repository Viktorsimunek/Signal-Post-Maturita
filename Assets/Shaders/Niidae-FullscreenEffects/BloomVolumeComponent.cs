using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable, VolumeComponentMenu("Niidae/Bloom")]
[VolumeRequiresRendererFeatures(typeof(NiidaeCustomEffectsRendererFeature))]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
[DisplayInfo(name = "N_Bloom")]
public sealed class BloomVolumeComponent : VolumeComponent, IPostProcessComponent
{
    [Tooltip("Overall effect intensity.")]
    public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 10f);

    [Tooltip("Brightness threshold.")]
    public MinFloatParameter threshold = new MinFloatParameter(1f, 0f);

    [Tooltip("Soft knee for the threshold.")]
    public ClampedFloatParameter knee = new ClampedFloatParameter(0.5f, 0f, 1f);

    [Tooltip("Bloom radius / spread.")]
    public ClampedIntParameter radius = new ClampedIntParameter(4, 1, 16);

    [Tooltip("Downscale factor for the first (sharper) bloom branch. 1 = full-res, 2 = half-res, etc.")]
    public ClampedIntParameter downscaleNear = new ClampedIntParameter(2, 1, 4);

    [Tooltip("Downscale factor for the second (wider) bloom branch. Higher values produce larger, softer glow.")]
    public ClampedIntParameter downscaleFar = new ClampedIntParameter(4, 2, 8);

    [Tooltip("Blur radius multiplier for the first bloom branch.")]
    public ClampedFloatParameter blurRadiusNear = new ClampedFloatParameter(1.0f, 0.25f, 4.0f);

    [Tooltip("Blur radius multiplier for the second bloom branch.")]
    public ClampedFloatParameter blurRadiusFar = new ClampedFloatParameter(1.35f, 0.25f, 6.0f);

    [Tooltip("Posterization of the bloom.")]
    public ClampedIntParameter steps = new ClampedIntParameter(255, 2, 255);

    [Tooltip("Colorization of the bloom.")]
    public ClampedFloatParameter saturation = new ClampedFloatParameter(0f, 0f, 1f);

    [Tooltip("Gamma correction for the bloom before posterization.")]
    public ClampedFloatParameter gammaBefore = new ClampedFloatParameter(1f, 0.5f, 2f);

    [Tooltip("Gamma correction for the bloom after posterization.")]
    public ClampedFloatParameter gammaAfter = new ClampedFloatParameter(1f, 0.5f, 2f);

    public bool IsActive() => intensity.value > 0f;

    public bool IsTileCompatible() => false;
}
