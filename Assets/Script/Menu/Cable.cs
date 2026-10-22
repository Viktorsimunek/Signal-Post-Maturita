using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class Cable : MonoBehaviour
{
    private enum SagDirectionMode
    {
        WorldDown,
        LocalDown,
        Custom
    }

    [Header("References")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Transform[] controlPoints = new Transform[0];
    [SerializeField] private Transform[] straightPoints = new Transform[0];
    [SerializeField] private bool requireBothPointsStraight = false;

    [Header("Shape")]
    [SerializeField, Min(2)] private int samplesPerSpan = 12;
    [SerializeField, Min(0f)] private float sagAmount = 0.1f;
    [SerializeField, Min(0f)] private float distanceInfluence = 0.05f;
    [SerializeField] private SagDirectionMode sagDirectionMode = SagDirectionMode.WorldDown;
    [SerializeField] private Vector3 customSagDirection = Vector3.down;
    [SerializeField] private AnimationCurve sagProfile = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.5f, 1f),
        new Keyframe(1f, 0f));

    [Header("Updates")]
    [SerializeField] private bool updateInEditMode = true;
    [SerializeField] private bool updateEveryFrame = true;

    private readonly List<Vector3> generatedPositions = new List<Vector3>();

    private void Reset()
    {
        CacheReferences();
        EnsureSagProfile();
        RebuildCable();
    }

    private void Awake()
    {
        CacheReferences();
        EnsureSagProfile();
        RebuildCable();
    }

    private void OnEnable()
    {
        CacheReferences();
        EnsureSagProfile();
        RebuildCable();
    }

    private void OnValidate()
    {
        samplesPerSpan = Mathf.Max(2, samplesPerSpan);
        sagAmount = Mathf.Max(0f, sagAmount);
        distanceInfluence = Mathf.Max(0f, distanceInfluence);

        CacheReferences();
        EnsureSagProfile();
        RebuildCable();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            if (updateInEditMode)
            {
                RebuildCable();
            }

            return;
        }

        if (updateEveryFrame)
        {
            RebuildCable();
        }
    }

    [ContextMenu("Rebuild Cable")]
    public void RebuildCable()
    {
        CacheReferences();
        if (lineRenderer == null)
        {
            return;
        }

        if (controlPoints == null || controlPoints.Length < 2)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        generatedPositions.Clear();

        Transform previousPoint = null;
        bool hasSpan = false;

        for (int i = 0; i < controlPoints.Length; i++)
        {
            Transform currentPoint = controlPoints[i];
            if (currentPoint == null)
            {
                continue;
            }

            if (previousPoint == null)
            {
                previousPoint = currentPoint;
                continue;
            }

            AddSpan(previousPoint, currentPoint, !hasSpan);
            previousPoint = currentPoint;
            hasSpan = true;
        }

        if (!hasSpan)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        lineRenderer.positionCount = generatedPositions.Count;
        lineRenderer.SetPositions(generatedPositions.ToArray());
    }

    public void SetControlPoints(Transform[] points)
    {
        controlPoints = points ?? new Transform[0];
        RebuildCable();
    }

    public void SetStraightPoints(Transform[] points)
    {
        straightPoints = points ?? new Transform[0];
        RebuildCable();
    }

    private void AddSpan(Transform startPoint, Transform endPoint, bool includeStartPoint)
    {
        Vector3 start = startPoint.position;
        Vector3 end = endPoint.position;
        int firstIndex = includeStartPoint ? 0 : 1;
        float spanDistance = Vector3.Distance(start, end);
        bool straightSpan = IsStraightSpan(startPoint, endPoint);
        float spanSag = straightSpan ? 0f : sagAmount + (spanDistance * distanceInfluence);
        Vector3 sagDirection = GetSagDirection();

        for (int i = firstIndex; i < samplesPerSpan; i++)
        {
            float t = i / (samplesPerSpan - 1f);
            Vector3 position = Vector3.Lerp(start, end, t);
            float sagOffset = sagProfile.Evaluate(t) * spanSag;
            position += sagDirection * sagOffset;
            generatedPositions.Add(ToLineSpace(position));
        }
    }

    private Vector3 GetSagDirection()
    {
        switch (sagDirectionMode)
        {
            case SagDirectionMode.LocalDown:
                return -transform.up;

            case SagDirectionMode.Custom:
                if (customSagDirection.sqrMagnitude > 0.0001f)
                {
                    return customSagDirection.normalized;
                }

                break;
        }

        return Vector3.down;
    }

    private Vector3 ToLineSpace(Vector3 worldPosition)
    {
        if (lineRenderer.useWorldSpace)
        {
            return worldPosition;
        }

        return transform.InverseTransformPoint(worldPosition);
    }

    private bool IsStraightSpan(Transform startPoint, Transform endPoint)
    {
        bool startIsStraight = ContainsTransform(straightPoints, startPoint);
        bool endIsStraight = ContainsTransform(straightPoints, endPoint);

        if (requireBothPointsStraight)
        {
            return startIsStraight && endIsStraight;
        }

        return startIsStraight || endIsStraight;
    }

    private static bool ContainsTransform(Transform[] points, Transform target)
    {
        if (points == null || target == null)
        {
            return false;
        }

        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == target)
            {
                return true;
            }
        }

        return false;
    }

    private void CacheReferences()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }
    }

    private void EnsureSagProfile()
    {
        if (sagProfile == null || sagProfile.length == 0)
        {
            sagProfile = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, 1f),
                new Keyframe(1f, 0f));
        }
    }
}
