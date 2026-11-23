using UnityEngine;

[DisallowMultipleComponent]
public class HranicniKamery : MonoBehaviour
{
    [SerializeField] private Camera[] cameras;
    [SerializeField] private Transform operatorPosition;
    [SerializeField, Range(2f, 20f)] private float framesPerSecond = 8f;
    [SerializeField, Min(1f)] private float viewingDistance = 12f;
    [SerializeField] private Renderer[] cctvScreens;
    private MaterialPropertyBlock screenBlock;
    private bool? screenPower;
    private float nextFrame;
    private bool wasPowered = true;

    private void OnEnable()
    {
        screenPower = null;
        UpdateScreenPower(LightsPowerGrid.IsPowered);
        if (cameras == null) return;
        foreach (Camera source in cameras) if (source != null) source.enabled = false;
        nextFrame = 0f;
    }

    private void UpdateScreenPower(bool powered)
    {
        if (screenPower == powered) return;
        screenPower = powered;
        if (cctvScreens == null) return;
        if (screenBlock == null) screenBlock = new MaterialPropertyBlock();
        foreach (Renderer screen in cctvScreens)
        {
            if (screen == null) continue;
            screen.GetPropertyBlock(screenBlock);
            screenBlock.SetFloat("_ScreenPower", powered ? 1f : 0f);
            screen.SetPropertyBlock(screenBlock);
        }
    }

    private void LateUpdate()
    {
        bool powered = LightsPowerGrid.IsPowered;
        UpdateScreenPower(powered);
        if (!powered)
        {
            if (wasPowered && cameras != null)
            {
                RenderTexture previous = RenderTexture.active;
                try
                {
                    foreach (Camera source in cameras)
                    {
                        if (source == null || source.targetTexture == null) continue;
                        if (!source.targetTexture.IsCreated()) source.targetTexture.Create();
                        RenderTexture.active = source.targetTexture;
                        GL.Clear(true, true, Color.black);
                    }
                }
                finally { RenderTexture.active = previous; }
            }
            wasPowered = false;
            return;
        }
        wasPowered = true;
        if (operatorPosition == null || (operatorPosition.position - transform.position).sqrMagnitude > viewingDistance * viewingDistance) return;
        if (Time.unscaledTime < nextFrame || cameras == null) return;
        nextFrame = Time.unscaledTime + 1f / framesPerSecond;
        foreach (Camera source in cameras)
        {
            if (source == null || source.targetTexture == null) continue;
            source.Render();
        }
    }
}

