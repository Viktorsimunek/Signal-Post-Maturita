using UnityEngine;

public class SignalQualityIndicator : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [SerializeField] private SignalQuality signalQuality;
    [SerializeField] private FuseSwitch powerFuse;
    [SerializeField] private Renderer[] lights;
    [SerializeField] private Color badColor = Color.red;
    [SerializeField] private Color goodColor = Color.green;
    [SerializeField] private float emissionPower = 2f;

    private Material[] materials;
    private float manualQuality = 1f;

    private void Reset()
    {
        lights = GetComponentsInChildren<Renderer>(true);
    }

    private void Awake()
    {
        if (signalQuality == null)
        {
            signalQuality = FindFirstObjectByType<SignalQuality>();
        }

        MakeMaterials();
        UpdateLights();
    }

    private void OnValidate()
    {
        manualQuality = Mathf.Clamp01(manualQuality);
        emissionPower = Mathf.Max(0f, emissionPower);
    }

    private void Update()
    {
        UpdateLights();
    }

    public void SetQuality(float value)
    {
        manualQuality = Mathf.Clamp01(value);
        UpdateLights();
    }

    private void MakeMaterials()
    {
        if (lights == null)
        {
            lights = new Renderer[0];
        }

        materials = new Material[lights.Length];

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null)
            {
                continue;
            }

            materials[i] = lights[i].material;
            materials[i].EnableKeyword("_EMISSION");
        }
    }

    private void UpdateLights()
    {
        if (materials == null)
        {
            return;
        }

        bool hasPower = (powerFuse == null || powerFuse.IsOn) && LightsPowerGrid.IsPowered;
        if (signalQuality != null && !signalQuality.HasPower)
        {
            hasPower = false;
        }

        float quality = hasPower ? (signalQuality != null ? signalQuality.Quality : manualQuality) : 0f;
        quality = Mathf.Clamp01(quality);

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == null)
            {
                continue;
            }

            float lightValue = Mathf.Clamp01((quality * materials.Length) - i);
            Color color = hasPower ? Color.Lerp(badColor, goodColor, lightValue) : Color.black;
            SetMaterialColor(materials[i], color);
        }
    }

    private void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty(BaseColorId))
        {
            material.SetColor(BaseColorId, color);
        }
        else if (material.HasProperty(ColorId))
        {
            material.SetColor(ColorId, color);
        }

        if (material.HasProperty(EmissionColorId))
        {
            material.SetColor(EmissionColorId, color * emissionPower);
        }
    }
}
