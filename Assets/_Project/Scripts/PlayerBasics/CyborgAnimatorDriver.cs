using UnityEngine;

//feeds the Cyborg's Animator. standing still plays the rifle idle; while he's shooting the
//Firing tree swaps that for the firing clip, so the rifle comes up without the legs changing.
//lives on the 'root' bone rather than the model root - the Mixamo clips address bones from
//there down, so Generic binds by path with no retargeting and no stance corrections needed.
[RequireComponent(typeof(Animator))]
public class CyborgAnimatorDriver : MonoBehaviour
{
    [Header("Setup")]
    public PlayerController playerMovement;
    public CyborgClass cyborg;
    public PlayerHealth playerHealth;

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

    void OnDisable()
    {
        //animator.speed is shared, so it goes back to normal for whoever runs next
        if (animator != null) animator.speed = 1f;
    }

    void Update()
    {
        //driven from state every frame rather than from events, so it survives a pause and
        //can't get stuck on if an ability is interrupted
        if (cyborg != null)
            animator.SetBool("IsFiring", cyborg.IsFiring);

        if (playerMovement == null) return;

        animator.SetBool("IsCrouching", playerMovement.IsCrouching);

        Vector2 input = playerMovement.MoveInput;
        if (playerMovement.IsSprinting) input *= sprintBlendDistance;

        animator.SetFloat("MoveX", input.x, blendDampTime, Time.deltaTime);
        animator.SetFloat("MoveZ", input.y, blendDampTime, Time.deltaTime);
    }
}
