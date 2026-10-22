using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LookFocus))]
public class PaperLook : MonoBehaviour
{
    [SerializeField] private string prompt = "Look at paper";
    [SerializeField] private Transform focusPoint;
    [SerializeField] private Vector3 focusPointOffset = new Vector3(0f, 0f, -0.6f);

    private LookFocus lookFocus;

    private void Awake()
    {
        EnsureFocusPoint();
        Apply();
    }

    private void OnValidate()
    {
        Apply();
    }

    public void SetPrompt(string text)
    {
        prompt = text;
        Apply();
    }

    public void SetFocusPoint(Transform point)
    {
        focusPoint = point;
        Apply();
    }

    public void SetFocusPointOffset(Vector3 offset)
    {
        focusPointOffset = offset;
        EnsureFocusPoint();
        Apply();
    }

    private void EnsureFocusPoint()
    {
        if (focusPoint != null)
        {
            return;
        }

        focusPoint = transform.Find("Look Point");
        if (focusPoint != null)
        {
            return;
        }

        GameObject point = new GameObject("Look Point");
        point.transform.SetParent(transform, false);
        focusPoint = point.transform;
    }

    private void Apply()
    {
        if (lookFocus == null)
        {
            lookFocus = GetComponent<LookFocus>();
        }

        if (lookFocus == null)
        {
            return;
        }

        if (focusPoint != null)
        {
            focusPoint.localPosition = focusPointOffset;
            focusPoint.localRotation = Quaternion.identity;
        }

        lookFocus.SetPrompt(prompt);
        lookFocus.SetFocusPoint(focusPoint != null ? focusPoint : transform);
    }
}
