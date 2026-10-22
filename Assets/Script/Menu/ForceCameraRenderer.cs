using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(UniversalAdditionalCameraData))]
public class ForceCameraRenderer : MonoBehaviour
{
    [Tooltip("Renderer index from the active URP Asset renderer list.")]
    [SerializeField] private int rendererIndex = 1;

    [Tooltip("Apply every time this object is enabled.")]
    [SerializeField] private bool applyOnEnable = true;

    private void OnEnable()
    {
        if (applyOnEnable)
        {
            ApplyRenderer();
        }
    }

    [ContextMenu("Apply Renderer Now")]
    public void ApplyRenderer()
    {
        UniversalAdditionalCameraData cameraData = GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null)
        {
            Debug.LogError("ForceCameraRenderer: Missing UniversalAdditionalCameraData.", this);
            return;
        }

        UniversalRenderPipelineAsset urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset == null)
        {
            Debug.LogError("ForceCameraRenderer: Active render pipeline is not URP.", this);
            return;
        }

        ScriptableRenderer testRenderer = urpAsset.GetRenderer(rendererIndex);
        if (testRenderer == null)
        {
            Debug.LogError("ForceCameraRenderer: Invalid renderer index " + rendererIndex + ".", this);
            return;
        }

        cameraData.SetRenderer(rendererIndex);
    }
}
