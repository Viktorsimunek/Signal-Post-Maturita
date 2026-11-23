using System.Collections;
using UnityEngine;

public class GateVisitorTest : MonoBehaviour
{
    [SerializeField] private GameObject visitorPrefab;
    [SerializeField] private RuntimeAnimatorController dialogueController;
    [SerializeField] private RadioDialogueUI radio;
    [SerializeField] private Buttons buttons;
    [SerializeField] private Transform leftGate;
    [SerializeField] private Transform rightGate;

    [Header("Route (local positions)")]
    [SerializeField] private Vector3 startPoint = new Vector3(0f, 0f, 28f);
    [SerializeField] private Vector3 waitingPoint = new Vector3(0f, 0f, 3.2f);
    [SerializeField] private Vector3 exitPoint = new Vector3(0f, 0f, -10f);
    [SerializeField] private float walkSpeed = 1.3f;
    [SerializeField] private float startDelay = 3f;

    private const string PersonId = "gateTest";
    private GameObject visitor;
    private Animator animator;
    private RuntimeAnimatorController movementController;
    private Quaternion leftClosed;
    private Quaternion rightClosed;
    private bool waiting;
    private bool allowed;
    private bool gateCached;
    private float movementBlend;
    private bool wasSpeaking;
    private CharacterTurn turner;

    private void OnEnable()
    {
        if (buttons != null) buttons.ButtonPressed += OnDecision;
        if (radio != null) radio.PersonCompleted += OnCallCompleted;
    }

    private IEnumerator Start()
    {
        if (visitorPrefab == null || radio == null || buttons == null || leftGate == null || rightGate == null)
        {
            Debug.LogWarning("Gate visitor test needs its scene references.", this);
            yield break;
        }

        leftClosed = leftGate.localRotation;
        rightClosed = rightGate.localRotation;
        gateCached = true;
        yield return new WaitForSeconds(startDelay);

        visitor = Instantiate(visitorPrefab, transform);
        visitor.transform.position = GroundPoint(transform.TransformPoint(startPoint));
        animator = visitor.GetComponentInChildren<Animator>();
        if (animator == null) yield break;
        animator.applyRootMotion = false;
        movementController = animator.runtimeAnimatorController;
        turner = animator.gameObject.AddComponent<CharacterTurn>();

        yield return MoveTo(transform.TransformPoint(waitingPoint));
        yield return turner.TurnTo(visitor.transform, Quaternion.LookRotation(-transform.forward));
        yield return new WaitForSeconds(0.5f);
        while (radio.HasActivePerson || !LightsPowerGrid.IsPowered) yield return null;

        if (dialogueController != null)
        {
            animator.speed = 1f;
            animator.runtimeAnimatorController = dialogueController;
            animator.SetBool("Talking", true);
        }
        waiting = true;
        radio.PersonCame(PersonId);
    }

    private void Update()
    {
        if (animator == null) return;
        if (animator.runtimeAnimatorController == movementController)
        {
            if (turner != null && turner.IsTurning) return;
            animator.speed = walkSpeed / 1.55f;
            animator.SetFloat("Speed", movementBlend, 0.3f, Time.deltaTime);
        }
        else if (waiting && dialogueController != null)
        {
            bool speaking = radio.IsSpeaking;
            if (speaking && !wasSpeaking) animator.SetInteger("TalkVariant", Random.Range(0, 2));
            animator.SetBool("Talking", speaking);
            wasSpeaking = speaking;
        }
    }
    private void OnDecision(Buttons.ButtonType decision)
    {
        if (!waiting || !radio.HasActivePerson) return;
        allowed = decision == Buttons.ButtonType.Allow;
        if (dialogueController != null) animator.SetBool("Talking", false);
    }

    private void OnCallCompleted(string personId)
    {
        if (!waiting || personId != PersonId) return;
        waiting = false;
        StartCoroutine(Leave());
    }

    private IEnumerator Leave()
    {
        if (!allowed && dialogueController != null)
        {
            animator.SetBool("Talking", false);
            animator.SetTrigger("Disappointed");
            yield return new WaitForSeconds(5.1f);
        }

        animator.runtimeAnimatorController = movementController;
        if (allowed) yield return MoveGate(true);
        Vector3 destination = transform.TransformPoint(allowed ? exitPoint : startPoint);
        yield return MoveTo(destination);
        if (allowed) yield return MoveGate(false);
        Destroy(visitor);
    }

    private IEnumerator MoveTo(Vector3 destination)
    {
        movementBlend = 0f;
        Vector3 facing = destination - visitor.transform.position;
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.001f)
            yield return turner.TurnTo(visitor.transform, Quaternion.LookRotation(facing));
        movementBlend = 1f;
        while (true)
        {
            Vector3 direction = destination - visitor.transform.position;
            direction.y = 0f;
            if (direction.magnitude < 0.03f) break;
            movementBlend = Mathf.Clamp01(direction.magnitude / 0.65f);
            float speed = animator.GetFloat("Speed") * walkSpeed;
            Vector3 next = visitor.transform.position + direction.normalized * Mathf.Min(speed * Time.deltaTime, direction.magnitude);
            visitor.transform.position = GroundPoint(next);
            yield return null;
        }
        movementBlend = 0f;
    }

    private IEnumerator MoveGate(bool open)
    {
        Quaternion fromLeft = leftGate.localRotation;
        Quaternion fromRight = rightGate.localRotation;
        Quaternion toLeft = leftClosed * Quaternion.Euler(0f, open ? 95f : 0f, 0f);
        Quaternion toRight = rightClosed * Quaternion.Euler(0f, open ? -95f : 0f, 0f);
        for (float time = 0f; time < 1f; time += Time.deltaTime)
        {
            leftGate.localRotation = Quaternion.Slerp(fromLeft, toLeft, time);
            rightGate.localRotation = Quaternion.Slerp(fromRight, toRight, time);
            yield return null;
        }
        leftGate.localRotation = toLeft;
        rightGate.localRotation = toRight;
    }

    private Vector3 GroundPoint(Vector3 point)
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (point.x < origin.x || point.x > origin.x + size.x || point.z < origin.z || point.z > origin.z + size.z) continue;
            point.y = terrain.SampleHeight(point) + origin.y;
            break;
        }
        if (Physics.Raycast(point + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 3f)) point.y = hit.point.y;
        return point;
    }

    private void OnDisable()
    {
        if (buttons != null) buttons.ButtonPressed -= OnDecision;
        if (radio != null) radio.PersonCompleted -= OnCallCompleted;
        StopAllCoroutines();
        if (visitor != null) Destroy(visitor);
        if (!gateCached) return;
        if (leftGate != null) leftGate.localRotation = leftClosed;
        if (rightGate != null) rightGate.localRotation = rightClosed;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 start = transform.TransformPoint(startPoint);
        Vector3 wait = transform.TransformPoint(waitingPoint);
        Vector3 exit = transform.TransformPoint(exitPoint);
        Gizmos.DrawLine(start, wait);
        Gizmos.DrawLine(wait, exit);
        foreach (Vector3 point in new[] { start, wait, exit }) Gizmos.DrawWireSphere(point, 0.3f);
    }
}
