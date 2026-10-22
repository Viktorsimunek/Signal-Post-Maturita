using UnityEngine;

public class LightPuise : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private FuseSwitch powerFuse;
    [SerializeField] private Color emissionColor = Color.white;
    [SerializeField] private float emissionIntensity = 2f;
    [SerializeField] private float pulseSpeed = 2f;

    [SerializeField] private Light[] lights;
    [SerializeField] private float lightIntensity = 1f;

    private Material targetMaterial;

    private void Reset()
    {
        targetRenderer = GetComponent<Renderer>();
        lights = GetComponentsInChildren<Light>(true);
    }

    private void Awake()
    {
        if (targetRenderer != null)
        {
            targetMaterial = targetRenderer.sharedMaterial;
            targetMaterial.EnableKeyword("_EMISSION");
        }
    }

    private void Update()
    {
        if ((powerFuse != null && !powerFuse.IsOn) || !LightsPowerGrid.IsPowered)
        {
            SetOff();
            return;
        }

        float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;

        if (targetMaterial != null)
        {
            targetMaterial.SetColor(EmissionColorId, emissionColor * (pulse * emissionIntensity));
        }

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null)
            {
                lights[i].intensity = pulse * lightIntensity;
            }
        }
    }

    private void SetOff()
    {
        if (targetMaterial != null)
        {
            targetMaterial.SetColor(EmissionColorId, Color.black);
        }

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null)
            {
                lights[i].intensity = 0f;
            }
        }
    }
}
