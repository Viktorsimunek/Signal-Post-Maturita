using UnityEngine;

public class IdleVariation : StateMachineBehaviour
{
    public bool changePose = true;
    public bool hasMovement = true;
    private float nextChange;
    private float pose;
    private float rate;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        pose = Random.Range(0, 2);
        rate = Random.Range(0.94f, 1.06f);
        nextChange = Random.Range(6f, 10f);
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        bool moving = hasMovement && animator.GetFloat("Speed") > 0.05f;
        if (!moving)
        {
            nextChange -= Time.deltaTime;
            if (nextChange <= 0f)
            {
                pose = 1f - pose;
                rate = Random.Range(0.94f, 1.06f);
                nextChange = Random.Range(6f, 10f);
            }
            if (changePose) animator.SetFloat("IdleVariant", pose, 0.8f, Time.deltaTime);
        }
        animator.SetFloat("IdleRate", moving ? 1f : rate, 0.5f, Time.deltaTime);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetFloat("IdleRate", 1f);
    }
}
