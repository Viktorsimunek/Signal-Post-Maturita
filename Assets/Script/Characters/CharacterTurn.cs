using System.Collections;
using UnityEngine;

// Lives on the Animator; only turn animations may move the character root.
public class CharacterTurn : MonoBehaviour
{
    public bool IsTurning { get; private set; }
    private Animator animator;
    private Transform character;
    private Quaternion target;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        animator.applyRootMotion = true;
    }

    public IEnumerator TurnTo(Transform root, Quaternion rotation)
    {
        if (Quaternion.Angle(root.rotation, rotation) < 5f) yield break;
        character = root;
        target = rotation;
        IsTurning = true;
        float previousSpeed = animator.speed;
        animator.speed = 1f;
        animator.SetFloat("Speed", 0f);

        bool hasCrouch = false;
        foreach (var parameter in animator.parameters)
            if (parameter.name == "Crouch") hasCrouch = true;
        bool crouched = hasCrouch && animator.GetBool("Crouch");
        if (crouched) animator.SetBool("Crouch", false);
        while (!IsStanding() || animator.IsInTransition(0)) yield return null;

      
        for (int step = 0; step < 2 && Quaternion.Angle(character.rotation, target) > 30f; step++)
        {
            float angle = Mathf.DeltaAngle(character.eulerAngles.y, target.eulerAngles.y);
            animator.SetTrigger(angle > 0f ? "TurnRight" : "TurnLeft");
            do { yield return null; } while (!IsTurn());
            while (IsTurn() || animator.IsInTransition(0)) yield return null;
        }

    
        while (Quaternion.Angle(character.rotation, target) > 0.2f)
        {
            character.rotation = Quaternion.RotateTowards(character.rotation, target, 90f * Time.deltaTime);
            yield return null;
        }
        if (crouched)
        {
            animator.SetBool("Crouch", true);
            do { yield return null; }
            while (!animator.GetCurrentAnimatorStateInfo(0).IsName("Crouch Locomotion") || animator.IsInTransition(0));
        }
        animator.speed = previousSpeed;
        IsTurning = false;
    }

    private bool IsStanding()
    {
        var state = animator.GetCurrentAnimatorStateInfo(0);
        return state.IsName("Locomotion") || state.IsName("Injured Locomotion");
    }

    private bool IsTurn()
    {
        var state = animator.GetCurrentAnimatorStateInfo(0);
        return state.IsName("Turn Left") || state.IsName("Turn Right");
    }

    private void OnAnimatorMove()
    {
        if (!IsTurning || character == null) return;
        var next = animator.GetNextAnimatorStateInfo(0);
        bool enteringTurn = animator.IsInTransition(0) && (next.IsName("Turn Left") || next.IsName("Turn Right"));
        if (!IsTurn() && !enteringTurn) return;
        float remaining = Mathf.DeltaAngle(character.eulerAngles.y, target.eulerAngles.y);
        float yaw = Mathf.DeltaAngle(0f, animator.deltaRotation.eulerAngles.y);
        if (Mathf.Abs(yaw) > Mathf.Abs(remaining)) yaw = remaining;
        character.Rotate(0f, yaw, 0f, Space.World);
        Vector3 motion = animator.deltaPosition;
        motion.y = 0f;
        character.position += motion;
    }
}
