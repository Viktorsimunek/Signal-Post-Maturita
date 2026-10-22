using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class SliderBar : MonoBehaviour
{
    public enum MoveMode
    {
        Smooth,
        Stepped
    }

    [Header("References")]
    [SerializeField] private RectTransform targetRect;
    [SerializeField] private RectTransform fromPoint;
    [SerializeField] private RectTransform toPoint;

    [Header("Value")]
    [SerializeField] private float minValue = 0f;
    [SerializeField] private float maxValue = 1f;
    [SerializeField] private float currentValue = 0f;
    [SerializeField] private float displayedValue = 0f;

    [Header("Movement")]
    [SerializeField] private MoveMode moveMode = MoveMode.Smooth;
    [SerializeField, Min(0f)] private float moveSpeed = 3f;
    [SerializeField, Min(1)] private int steps = 10;

    public float MinValue => minValue;
    public float MaxValue => maxValue;
    public float CurrentValue => currentValue;
    public float NormalizedValue => GetNormalizedValue(displayedValue);
    public float DisplayedNormalizedValue => GetRawNormalizedValue(displayedValue);
    public float TargetNormalizedValue => GetRawNormalizedValue(currentValue);
    public bool IsMoving => !Mathf.Approximately(displayedValue, currentValue);

    private void Reset()
    {
        CacheReferences();
        ApplyPosition();
    }

    private void Awake()
    {
        CacheReferences();
        displayedValue = Mathf.Clamp(currentValue, minValue, maxValue);
        ApplyPosition();
    }

    private void OnEnable()
    {
        CacheReferences();
        displayedValue = Mathf.Clamp(currentValue, minValue, maxValue);
        ApplyPosition();
    }

    private void OnValidate()
    {
        if (maxValue < minValue)
        {
            maxValue = minValue;
        }

        steps = Mathf.Max(1, steps);
        currentValue = Mathf.Clamp(currentValue, minValue, maxValue);
        displayedValue = Mathf.Clamp(displayedValue, minValue, maxValue);
        CacheReferences();
        ApplyPosition();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            displayedValue = currentValue;
            ApplyPosition();
            return;
        }

        float previousValue = displayedValue;
        displayedValue = GetNextDisplayedValue();
        if (!Mathf.Approximately(previousValue, displayedValue))
        {
            ApplyPosition();
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        ApplyPosition();
    }

    public void SetValue(float value)
    {
        currentValue = Mathf.Clamp(value, minValue, maxValue);

        if (!Application.isPlaying || moveSpeed <= 0f)
        {
            displayedValue = currentValue;
            ApplyPosition();
        }
    }

    public void SetNormalizedValue(float normalizedValue)
    {
        SetValue(Mathf.Lerp(minValue, maxValue, Mathf.Clamp01(normalizedValue)));
    }

    public void Set01(float value)
    {
        SetNormalizedValue(value);
    }

    public void SnapToValue(float value)
    {
        currentValue = Mathf.Clamp(value, minValue, maxValue);
        displayedValue = currentValue;
        ApplyPosition();
    }

    public void SnapTo01(float value)
    {
        SnapToValue(Mathf.Lerp(minValue, maxValue, Mathf.Clamp01(value)));
    }

    public void SetRange(float newMinValue, float newMaxValue)
    {
        minValue = newMinValue;
        maxValue = Mathf.Max(newMinValue, newMaxValue);
        currentValue = Mathf.Clamp(currentValue, minValue, maxValue);
        displayedValue = Mathf.Clamp(displayedValue, minValue, maxValue);

        if (!Application.isPlaying || moveSpeed <= 0f)
        {
            displayedValue = currentValue;
        }

        ApplyPosition();
    }

    public void AddValue(float amount)
    {
        SetValue(currentValue + amount);
    }

    private void ApplyPosition()
    {
        CacheReferences();

        if (targetRect == null || fromPoint == null || toPoint == null)
        {
            return;
        }

        Vector2 fromPosition = GetRelativeAnchoredPosition(fromPoint);
        Vector2 toPosition = GetRelativeAnchoredPosition(toPoint);
        float t = GetNormalizedValue(displayedValue);

        targetRect.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, t);
    }

    private float GetNormalizedValue(float value)
    {
        float normalizedValue = GetRawNormalizedValue(value);

        if (moveMode == MoveMode.Stepped)
        {
            normalizedValue = Mathf.Round(normalizedValue * steps) / steps;
        }

        return normalizedValue;
    }

    private float GetRawNormalizedValue(float value)
    {
        if (Mathf.Approximately(maxValue, minValue))
        {
            return 0f;
        }

        float normalizedValue = (value - minValue) / (maxValue - minValue);
        return Mathf.Clamp01(normalizedValue);
    }

    private float GetNextDisplayedValue()
    {
        if (moveSpeed <= 0f)
        {
            return currentValue;
        }

        return Mathf.MoveTowards(displayedValue, currentValue, moveSpeed * Time.unscaledDeltaTime);
    }

    private Vector2 GetRelativeAnchoredPosition(RectTransform reference)
    {
        RectTransform parentRect = targetRect.parent as RectTransform;
        if (parentRect == null)
        {
            return reference.anchoredPosition;
        }

        Vector3 worldPosition = reference.TransformPoint(reference.rect.center);
        Vector2 localPoint = parentRect.InverseTransformPoint(worldPosition);
        return localPoint;
    }

    private void CacheReferences()
    {
        if (targetRect == null)
        {
            targetRect = GetComponent<RectTransform>();
        }
    }
}
