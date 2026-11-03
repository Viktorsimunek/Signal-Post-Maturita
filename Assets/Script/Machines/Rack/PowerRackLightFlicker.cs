using UnityEngine;

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public class PowerRackLightFlicker : MonoBehaviour
{
    [SerializeField] private PowerSupplyRack[] powerSupplies;
    [SerializeField] private Light[] lights;
    [SerializeField, Range(0f, 1f)] private float startHeat = 0.75f;
    [SerializeField] private Vector2 blinksPerSecond = new Vector2(0.4f, 7f);
    [SerializeField, Min(0.01f)] private float blinkDuration = 0.08f;

    private Light[] cachedLights;
    private float[] originalIntensity;
    private float[] blinkTimers;
    private bool[] dimmed;

    private void OnEnable()
    {
        cachedLights = lights != null ? (Light[])lights.Clone() : new Light[0];
        originalIntensity = new float[cachedLights.Length];
        blinkTimers = new float[cachedLights.Length];
        dimmed = new bool[cachedLights.Length];
    }

    private void Update()
    {
        // Vratit jas pred tim, nez svetla aktualizuji vypinace.
        RestoreLights();
    }

    private void LateUpdate()
    {
        float heat = 0f;
        if (powerSupplies != null)
        {
            for (int i = 0; i < powerSupplies.Length; i++)
            {
                if (powerSupplies[i] != null && powerSupplies[i].isActiveAndEnabled)
                    heat = Mathf.Max(heat, powerSupplies[i].Heat);
            }
        }

        float hotPart = Mathf.InverseLerp(startHeat, 1f, heat);
        float rate = Mathf.Lerp(blinksPerSecond.x, blinksPerSecond.y, hotPart);

        for (int i = 0; i < cachedLights.Length; i++)
        {
            Light light = cachedLights[i];
            if (heat <= startHeat || light == null || !light.isActiveAndEnabled)
            {
                blinkTimers[i] = 0f;
                continue;
            }

            blinkTimers[i] -= Time.deltaTime;
            if (blinkTimers[i] <= 0f && Random.value < rate * Time.deltaTime)
                blinkTimers[i] = Random.Range(blinkDuration * 0.5f, blinkDuration);

            if (blinkTimers[i] > 0f)
            {
                originalIntensity[i] = light.intensity;
                light.intensity = originalIntensity[i] * 0.05f;
                dimmed[i] = true;
            }
        }
    }

    private void OnDisable()
    {
        RestoreLights();
    }

    private void RestoreLights()
    {
        if (cachedLights == null) return;

        for (int i = 0; i < cachedLights.Length; i++)
        {
            if (dimmed[i] && cachedLights[i] != null)
                cachedLights[i].intensity = originalIntensity[i];

            dimmed[i] = false;
        }
    }

    private void OnValidate()
    {
        blinksPerSecond.x = Mathf.Max(0f, blinksPerSecond.x);
        blinksPerSecond.y = Mathf.Max(blinksPerSecond.x, blinksPerSecond.y);
        blinkDuration = Mathf.Max(0.01f, blinkDuration);
    }
}
