using UnityEngine;

/// <summary>Caps only UI render-texture cameras on Low and Very Low. Never uses player visibility.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public sealed class LowQualityDisplayRefresh : MonoBehaviour
{
    private Camera target;
    private CameraToTextureWithShader owner;
    private bool originalEnabled, controlled, wasRequested, shuttingDown;
    private float nextRefresh;

    private void Awake() { target = GetComponent<Camera>(); }

    public void Configure(CameraToTextureWithShader output)
    {
        if (target == null) target = GetComponent<Camera>();
        owner = output;
        if (!controlled) originalEnabled = target.enabled;
        controlled = true;
        nextRefresh = 0;
        enabled = true;
    }

    private void LateUpdate()
    {
        if (shuttingDown || target == null) return;
        if (GameSettings.CurrentGraphicsQuality > GameSettings.GraphicsQuality.Low)
        {
            RestoreFullRate();
            return;
        }
        if (!controlled)
        {
            originalEnabled = target.enabled;
            controlled = true;
        }
        bool requested = owner != null ? owner.isActiveAndEnabled : originalEnabled;
        if (requested && !wasRequested) nextRefresh = 0;
        wasRequested = requested;
        if (!requested) { target.enabled = false; return; }
        bool refresh = Time.unscaledTime >= nextRefresh;
        target.enabled = refresh;
        if (refresh)
        {
            float rate = GameSettings.CurrentGraphicsQuality == GameSettings.GraphicsQuality.VeryLow ? 20f : 30f;
            nextRefresh = Time.unscaledTime + 1f / rate;
        }
    }

    public void RestoreFullRate()
    {
        RestoreState();
        enabled = false;
    }

    private void RestoreState()
    {
        if (!controlled || target == null) return;
        controlled = false;
        target.enabled = owner != null ? owner.isActiveAndEnabled : originalEnabled;
    }

    private void OnApplicationQuit() { shuttingDown = true; }
    private void OnDisable() { if (!shuttingDown) RestoreState(); }
}
