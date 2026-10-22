using UnityEngine;

[ExecuteAlways]
public class Clock : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TimeOfDayController timeOfDayController;
    [SerializeField] private Transform hourHand;
    [SerializeField] private Transform minuteHand;

    [Header("Rotation")]
    [SerializeField] private Vector3 rotationAxis = Vector3.forward;
    [SerializeField] private bool clockwise = true;
    [SerializeField] private float hourHandOffset;
    [SerializeField] private float minuteHandOffset;

    private void Reset()
    {
        if (timeOfDayController == null)
        {
            timeOfDayController = FindFirstObjectByType<TimeOfDayController>();
        }
    }

    private void OnEnable()
    {
        ApplyClock();
    }

    private void OnValidate()
    {
        ApplyClock();
    }

    private void Update()
    {
        ApplyClock();
    }

    [ContextMenu("Apply Clock")]
    private void ApplyClock()
    {
        if (timeOfDayController == null)
        {
            timeOfDayController = FindFirstObjectByType<TimeOfDayController>();
        }

        if (timeOfDayController == null)
        {
            return;
        }

        float time01 = timeOfDayController.Time01;
        float totalHours = time01 * 24f;
        float hourValue = totalHours % 12f;
        float minuteValue = (totalHours - Mathf.Floor(totalHours)) * 60f;

        float direction = clockwise ? -1f : 1f;
        float hourAngle = hourHandOffset + (hourValue / 12f) * 360f * direction;
        float minuteAngle = minuteHandOffset + (minuteValue / 60f) * 360f * direction;
        Vector3 axis = rotationAxis.sqrMagnitude > 0.0001f ? rotationAxis.normalized : Vector3.forward;

        if (hourHand != null)
        {
            hourHand.localRotation = Quaternion.AngleAxis(hourAngle, axis);
        }

        if (minuteHand != null)
        {
            minuteHand.localRotation = Quaternion.AngleAxis(minuteAngle, axis);
        }
    }
}
