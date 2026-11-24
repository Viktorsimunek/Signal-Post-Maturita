using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class ChairContactPush : MonoBehaviour
{
    [SerializeField] private Rigidbody chair;
    [SerializeField] private Rigidbody swivelBody;
    [SerializeField, Min(0f)] private float pushAcceleration = 12f;
    [SerializeField, Min(0f)] private float maximumPushSpeed = 2.2f;
    private CharacterController controller;
    private Collider[] chairColliders;
    private Vector3 requestedVelocity;
    private Rigidbody pushedBody;
    private Vector3 pushVelocity;
    private Vector3 pushPoint;
    private float savedStepOffset = -1f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (chair == null) return;
        chairColliders = chair.GetComponentsInChildren<Collider>();
        foreach (Rigidbody body in new[] { chair, swivelBody })
        {
            if (body == null) continue;
            body.maxLinearVelocity = body.maxAngularVelocity = 3f;
            body.maxDepenetrationVelocity = 0.6f;
            body.solverIterations = 16;
            body.solverVelocityIterations = 8;
            body.sleepThreshold = 0.005f;
        }
    }

    public void BeginMove(Vector3 velocity)
    {
        requestedVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
        if (chair == null || chairColliders == null) return;
        Vector3 center = transform.TransformPoint(controller.center);
        float clearance = controller.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z)
            + requestedVelocity.magnitude * Time.deltaTime + 0.15f;
        foreach (Collider col in chairColliders)
        {
            if (col == null || !col.enabled || col.isTrigger) continue;
            Vector3 closest = col.bounds.ClosestPoint(center);
            closest.y = center.y;
            if ((closest - center).sqrMagnitude > clearance * clearance) continue;
            if (savedStepOffset < 0f) savedStepOffset = controller.stepOffset;
            controller.stepOffset = 0f;
            return;
        }
    }

    public void EndMove()
    {
        if (savedStepOffset < 0f || controller == null) return;
        controller.stepOffset = savedStepOffset;
        savedStepOffset = -1f;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (chair == null || chair.isKinematic || (hit.rigidbody != chair && hit.rigidbody != swivelBody) ||
            requestedVelocity.sqrMagnitude < 0.0025f || Mathf.Abs(hit.normal.y) > 0.7f)
            return;
        Vector3 direction = requestedVelocity.normalized;
        if (Vector3.Dot(direction, -hit.normal) < 0.05f) return;
        float speed = Mathf.Min(maximumPushSpeed, requestedVelocity.magnitude);
        float previousSpeed = pushVelocity.magnitude;
        if (speed < previousSpeed || (Mathf.Approximately(speed, previousSpeed) && pushedBody == swivelBody)) return;

        pushedBody = hit.rigidbody;
        pushVelocity = direction * speed;
        Vector3 lever = Vector3.ProjectOnPlane(hit.point - pushedBody.worldCenterOfMass, Vector3.up);
        pushPoint = pushedBody.worldCenterOfMass + Vector3.ClampMagnitude(lever, 0.55f);
    }

    private void FixedUpdate()
    {
        // Silu aplikujeme ve fyzikalnim kroku, vodorovne a s omezenou rychlosti.
        if (pushedBody != null && !pushedBody.isKinematic && pushVelocity.sqrMagnitude > 0f)
        {
            Vector3 direction = pushVelocity.normalized;
            float currentSpeed = Vector3.Dot(pushedBody.GetPointVelocity(pushPoint), direction);
            float change = Mathf.Clamp(pushVelocity.magnitude - currentSpeed, 0f, pushAcceleration * Time.fixedDeltaTime);
            float mass = chair.mass + (swivelBody != null ? swivelBody.mass : 0f);
            if (change > 0f) pushedBody.AddForceAtPosition(direction * change * mass, pushPoint, ForceMode.Impulse);
        }
        pushVelocity = Vector3.zero;
    }

    private void OnDisable()
    {
        EndMove();
        requestedVelocity = Vector3.zero;
        pushVelocity = Vector3.zero;
    }
}
