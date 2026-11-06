using UnityEngine;
using UnityEngine.Events;

public class TowerModeSwitch : MonoBehaviour, IInteractable
{
    [SerializeField] private bool broadcasting;
    public Transform lever;
    public Renderer indicator;
    public UnityEvent onBroadcastEnabled = new UnityEvent();
    public bool IsBroadcasting => broadcasting;
    public bool IsJamming => !broadcasting;
    private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock properties;

    private void Awake() { ApplyVisuals(); }
    public bool CanInteract(PlayerInteractor actor) => !broadcasting && isActiveAndEnabled;
    public string GetInteractionPrompt(PlayerInteractor actor) => broadcasting ? "Vysílání aktivní" : "Přepnout z rušení na vysílání";
    public void Interact(PlayerInteractor actor)
    {
        if (CanInteract(actor)) SetBroadcasting(true);
    }
    public void SetBroadcasting(bool value)
    {
        if (broadcasting == value) return;
        broadcasting = value; ApplyVisuals();
        if (broadcasting) onBroadcastEnabled.Invoke();
    }
    public void ApplyVisuals()
    {
        if (lever) lever.localRotation = Quaternion.Euler(broadcasting ? -35 : 35, 0, 0);
        if (!indicator) return;
        if (properties == null) properties = new MaterialPropertyBlock();
        var color = broadcasting ? new Color(.15f,.75f,.28f) : new Color(.9f,.15f,.04f);
        indicator.GetPropertyBlock(properties); properties.SetColor(ColorId,color); properties.SetColor(EmissionId,color*1.5f); indicator.SetPropertyBlock(properties);
    }
}
