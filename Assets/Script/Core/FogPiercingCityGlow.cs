using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FogPiercingCityGlow : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");

    [Header("Look")]
    [ColorUsage(false, true)]
    [SerializeField] private Color glowColor = new Color(1f, 0.72f, 0.32f, 0.65f);
    [Min(0f)]
    [SerializeField] private float intensity = 3.5f;
    [Range(0.2f, 8f)]
    [SerializeField] private float softness = 2.4f;
    [SerializeField] private Vector2 size = new Vector2(180f, 75f);

    [Header("Time Blend")]
    [SerializeField] private TimeOfDayController timeOfDay;
    [Min(0f)]
    [SerializeField] private float dayIntensity = 0.05f;
    [Min(0f)]
    [SerializeField] private float nightIntensity = 1f;
    [Min(0f)]
    [SerializeField] private float stormIntensityBoost = 0.35f;

    [Header("Billboard")]
    [SerializeField] private bool faceMainCamera = true;
    [SerializeField] private Vector3 rotationOffset;

    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;

    private void Reset()
    {
        timeOfDay = FindFirstObjectByType<TimeOfDayController>();
        EnsureQuadMesh();
    }

    private void OnEnable()
    {
        EnsureComponents();
        EnsureQuadMesh();
        ApplyVisuals();
    }

    private void OnValidate()
    {
        size.x = Mathf.Max(0.01f, size.x);
        size.y = Mathf.Max(0.01f, size.y);
        intensity = Mathf.Max(0f, intensity);
        dayIntensity = Mathf.Max(0f, dayIntensity);
        nightIntensity = Mathf.Max(0f, nightIntensity);
        stormIntensityBoost = Mathf.Max(0f, stormIntensityBoost);

        EnsureComponents();
        EnsureQuadMesh();
        ApplyVisuals();
    }

    private void LateUpdate()
    {
        if (faceMainCamera && Camera.main != null)
        {
            Vector3 toCamera = transform.position - Camera.main.transform.position;
            if (toCamera.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up) * Quaternion.Euler(rotationOffset);
            }
        }

        ApplyVisuals();
    }

    private void EnsureComponents()
    {
        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        propertyBlock ??= new MaterialPropertyBlock();
    }

    private void EnsureQuadMesh()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter.sharedMesh != null)
        {
            return;
        }

        Mesh mesh = new Mesh
        {
            name = "Fog Piercing Glow Quad",
            vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            },
            uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            },
            triangles = new[] { 0, 2, 1, 2, 3, 1 }
        };

        mesh.RecalculateBounds();
        meshFilter.sharedMesh = mesh;
    }

    private void ApplyVisuals()
    {
        if (meshRenderer == null)
        {
            return;
        }

        float weatherMultiplier = 1f;
        if (timeOfDay != null)
        {
            weatherMultiplier = Mathf.Lerp(dayIntensity, nightIntensity, timeOfDay.NightWeight01);
            weatherMultiplier += timeOfDay.StormAmount01 * stormIntensityBoost;
        }

        transform.localScale = new Vector3(size.x, size.y, 1f);

        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(ColorId, glowColor);
        propertyBlock.SetFloat(IntensityId, intensity * weatherMultiplier);
        propertyBlock.SetFloat(SoftnessId, softness);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }
}
