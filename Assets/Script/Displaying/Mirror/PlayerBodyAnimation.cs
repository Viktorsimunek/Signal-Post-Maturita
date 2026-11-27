using UnityEngine;

[DisallowMultipleComponent]
public class PlayerBodyAnimation : MonoBehaviour
{
    [SerializeField] private FirstPersonController player;
    [SerializeField] private Animator animator;
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int Crouch = Animator.StringToHash("Crouch");

    private void Awake()
    {
        if (!player) player = GetComponentInParent<FirstPersonController>();
        if (!animator) animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (!player || !animator) return;
        float speed = player.PlanarSpeed;
        float blend = speed <= player.WalkSpeed
            ? speed / Mathf.Max(0.01f, player.WalkSpeed)
            : 1f + Mathf.InverseLerp(player.WalkSpeed, player.BriskSpeed, speed);
        animator.SetFloat(Speed, blend, 0.12f, Time.deltaTime);
        animator.SetBool(Crouch, player.IsCrouching);
    }
}
