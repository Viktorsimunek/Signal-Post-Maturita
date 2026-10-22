using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[VolumeComponentMenu("Niidae/N_VHS Grain")]
[VolumeRequiresRendererFeatures(typeof(NiidaeCustomEffectsRendererFeature))]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
[DisplayInfo(name = "N_VHS Grain")]
public sealed class VHSGrainVolumeComponent : VolumeComponent, IPostProcessComponent
{
    [Tooltip("Grain strength.")]
    public ClampedFloatParameter grainStrength = new ClampedFloatParameter(0.01f, 0f, 1f);

    [Tooltip("Grain scale.")]
    public MinFloatParameter grainScale = new MinFloatParameter(1000f, 0f);

    [Tooltip("Grain aspect.")]
    public ClampedFloatParameter grainAspect = new ClampedFloatParameter(0.2f, -1f, 1f);

    [Tooltip("Chroma grain strength.")]
    public ClampedFloatParameter chromaStrength = new ClampedFloatParameter(0.01f, 0f, 1f);

    [Tooltip("Chroma grain scale.")]
    public MinFloatParameter chromaScale = new MinFloatParameter(16f, 0f);

    [Tooltip("Chroma grain aspect.")]
    public ClampedFloatParameter chromaAspect = new ClampedFloatParameter(-0.7f, -1f, 1f);

    public bool IsActive() => grainStrength.value > 0f || chromaStrength.value > 0f;
}
