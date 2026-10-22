using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[VolumeComponentMenu("Niidae/Pixelization")]
[VolumeRequiresRendererFeatures(typeof(NiidaeCustomEffectsRendererFeature))]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
[DisplayInfo(name = "N_Pixelization")]
public sealed class PixelizationVolumeComponent : VolumeComponent, IPostProcessComponent
{
    [Tooltip("Blends between original image (0) and full pixelization (1).")]
    public ClampedFloatParameter intensity = new ClampedFloatParameter(1f, 0f, 1f);

    [Tooltip("Pixel block size in screen pixels.")]
    public ClampedFloatParameter pixelSize = new ClampedFloatParameter(4f, 1f, 256f);

    public bool IsActive()
    {
        return intensity.value > 0.0f;
    }
}
