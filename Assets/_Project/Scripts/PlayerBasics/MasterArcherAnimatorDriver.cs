using UnityEngine;

//feeds the Master Archer's Animator from movement state, the same way the Cyborg's driver does.
//the Arissa clips come off the same Mixamo skeleton as the model, so Generic binds them by bone
//path with no retargeting and no stance corrections.
[RequireComponent(typeof(Animator))]
public class MasterArcherAnimatorDriver : MonoBehaviour
{
    [Header("Setup")]
    public PlayerController playerMovement;
    public MasterArcher archer;

    [Header("Blend")]
    [Tooltip("Seconds to ease between stand, run and sprint.")]
    public float blendDampTime = 0.12f;

    [Tooltip("How far out sprinting pushes the blend. Must match the sprint clips' position.")]
    public float sprintBlendDistance = 2f;

    Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        //driven from state every frame rather than from events, so it survives a pause and
        //can't get stuck on if something is interrupted
        if (archer != null)
        {
            //not used by any state yet - there are no bow clips - but wired so draw and aim
            //animations only need adding to the controller
            animator.SetBool("IsAiming", archer.IsAiming);
            animator.SetBool("IsDrawing", archer.IsDrawing());
        }

        if (playerMovement == null) return;

        animator.SetBool("IsCrouching", playerMovement.IsCrouching);

        Vector2 input = playerMovement.MoveInput;
        if (playerMovement.IsSprinting) input *= sprintBlendDistance;

        animator.SetFloat("MoveX", input.x, blendDampTime, Time.deltaTime);
        animator.SetFloat("MoveZ", input.y, blendDampTime, Time.deltaTime);
    }
}
