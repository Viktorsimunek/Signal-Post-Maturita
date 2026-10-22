using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[VolumeComponentMenu("Niidae/Posterize")]

[VolumeRequiresRendererFeatures(typeof(NiidaeCustomEffectsRendererFeature))]

[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]

[DisplayInfo(name = "N_Posterize")]

public sealed class PosterizeVolumeComponent : VolumeComponent, IPostProcessComponent
{
    [Tooltip("Enter the description for the property that is shown when hovered")]
    public ClampedFloatParameter intensity = new ClampedFloatParameter(1.0f, 0f, 1f);

    [Tooltip("How many color steps are used for posterization.")]
    public ClampedFloatParameter numColors = new ClampedFloatParameter(32f, 2f, 256f);

    [Tooltip("Offset applied before quantization. Useful for palette shifting.")]
    public ClampedFloatParameter colorOffset = new ClampedFloatParameter(0f, -1f, 1f);

    [Tooltip("Strength of dithering noise blended into quantization.")]
    public ClampedFloatParameter ditherStrength = new ClampedFloatParameter(0f, 0f, 2f);

    [Tooltip("Scale of dithering pattern in screen space.")]
    public ClampedFloatParameter ditherScale = new ClampedFloatParameter(4f, 0.1f, 64f);

    public bool IsActive()
    {
        return intensity.GetValue<float>() > 0.0f;
    }
}
