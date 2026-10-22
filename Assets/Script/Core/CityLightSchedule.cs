using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class CityLightSchedule : MonoBehaviour
{
    private static readonly int LightOnAmountId = Shader.PropertyToID("_LightOnAmount");
    private static readonly int LightRandomnessId = Shader.PropertyToID("_LightRandomness");
    private static readonly int LightRandomGridId = Shader.PropertyToID("_LightRandomGrid");
    private static readonly int LightEdgeSoftnessId = Shader.PropertyToID("_LightEdgeSoftness");
    private static readonly int LightOffEmissionId = Shader.PropertyToID("_LightOffEmission");
    private static readonly int LightRandomSeedId = Shader.PropertyToID("_LightRandomSeed");

    [SerializeField] private TimeOfDayController timeOfDay;
    [SerializeField] private Renderer[] targetRenderers;

    [Header("Evening")]
    [Range(0f, 1f)]
    [SerializeField] private float lightsStartTime = 0.66f;
    [Range(0f, 1f)]
    [SerializeField] private float lightsFullyOnTime = 0.78f;

    [Header("Morning")]
    [Range(0f, 1f)]
    [SerializeField] private float lightsStartTurningOffTime = 0.18f;
    [Range(0f, 1f)]
    [SerializeField] private float lightsFullyOffTime = 0.31f;

    [Header("Random Pattern")]
    [Range(0f, 1f)]
    [SerializeField] private float randomness = 0.85f;
    [Min(1f)]
    [SerializeField] private float randomGrid = 42f;
    [Range(0.001f, 0.4f)]
    [SerializeField] private float edgeSoftness = 0.08f;
    [Range(0f, 0.25f)]
    [SerializeField] private float lightOffEmission = 0.03f;
    [SerializeField] private float seed = 12.7f;
    [SerializeField] private bool varyPerRenderer = true;
    [SerializeField] private bool varyPerMaterial = true;

    [Header("Preview")]
    [Range(0f, 1f)]
    [SerializeField] private float manualLightAmount = 1f;
    [SerializeField] private bool useManualPreview;
    [SerializeField] private bool updateInEditMode = true;

    private MaterialPropertyBlock propertyBlock;

    private void Reset()
    {
        timeOfDay = FindFirstObjectByType<TimeOfDayController>();
        targetRenderers = GetComponentsInChildren<Renderer>();
    }

    private void OnEnable()
    {
        propertyBlock ??= new MaterialPropertyBlock();
        EnsureTargets();
        Apply();

#if UNITY_EDITOR
        EditorApplication.update -= EditorUpdate;
        EditorApplication.update += EditorUpdate;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorUpdate;
#endif
    }

    private void OnValidate()
    {
        propertyBlock ??= new MaterialPropertyBlock();
        randomGrid = Mathf.Max(1f, randomGrid);
        EnsureTargets();
        Apply();
    }

    private void Update()
    {
        Apply();
    }

#if UNITY_EDITOR
    private void EditorUpdate()
    {
        if (Application.isPlaying || !updateInEditMode)
        {
            return;
        }

        Apply();
        SceneView.RepaintAll();
    }
#endif

    private void Apply()
    {
        EnsureTargets();

        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            return;
        }

        float lightAmount = useManualPreview || timeOfDay == null
            ? manualLightAmount
            : EvaluateLightAmount(timeOfDay.Time01);

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer targetRenderer = targetRenderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            Material[] materials = targetRenderer.sharedMaterials;
            int materialCount = Mathf.Max(1, materials.Length);

            for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
            {
                targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);
                propertyBlock.SetFloat(LightOnAmountId, lightAmount);
                propertyBlock.SetFloat(LightRandomnessId, randomness);
                propertyBlock.SetFloat(LightRandomGridId, randomGrid);
                propertyBlock.SetFloat(LightEdgeSoftnessId, edgeSoftness);
                propertyBlock.SetFloat(LightOffEmissionId, lightOffEmission);
                propertyBlock.SetFloat(LightRandomSeedId, EvaluateSeed(targetRenderer, i, materialIndex));
                targetRenderer.SetPropertyBlock(propertyBlock, materialIndex);
            }
        }
    }

    private float EvaluateSeed(Renderer targetRenderer, int rendererIndex, int materialIndex)
    {
        float rendererSeed = 0f;
        if (varyPerRenderer)
        {
            Vector3 position = targetRenderer.transform.position;
            rendererSeed = Hash(position.x * 0.31f + position.y * 1.37f + position.z * 2.11f + rendererIndex * 13.13f);
        }

        float materialSeed = varyPerMaterial ? Hash(materialIndex * 17.17f + 5.31f) : 0f;
        return seed + rendererSeed * 101.3f + materialSeed * 37.7f;
    }

    private static float Hash(float value)
    {
        return Mathf.Repeat(Mathf.Sin(value * 12.9898f) * 43758.5453f, 1f);
    }

    private void EnsureTargets()
    {
        if (targetRenderers != null && targetRenderers.Length > 0)
        {
            return;
        }

        targetRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
    }

    private float EvaluateLightAmount(float time01)
    {
        if (IsInTimeRange(time01, lightsStartTime, lightsFullyOnTime))
        {
            return SmoothBetweenTimes(time01, lightsStartTime, lightsFullyOnTime);
        }

        if (IsInTimeRange(time01, lightsFullyOnTime, lightsStartTurningOffTime))
        {
            return 1f;
        }

        if (IsInTimeRange(time01, lightsStartTurningOffTime, lightsFullyOffTime))
        {
            return 1f - SmoothBetweenTimes(time01, lightsStartTurningOffTime, lightsFullyOffTime);
        }

        return 0f;
    }

    private static float SmoothBetweenTimes(float time01, float start, float end)
    {
        float duration = Mathf.Repeat(end - start, 1f);
        if (duration <= 0.0001f)
        {
            return 1f;
        }

        float elapsed = Mathf.Repeat(time01 - start, 1f);
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
    }

    private static bool IsInTimeRange(float time01, float start, float end)
    {
        if (Mathf.Approximately(start, end))
        {
            return false;
        }

        return start < end
            ? time01 >= start && time01 < end
            : time01 >= start || time01 < end;
    }
}
