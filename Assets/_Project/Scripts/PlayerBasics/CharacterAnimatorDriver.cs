using UnityEngine;

//sits on a class's character model and feeds its Animator. each class has its own model and
//its own controller, so each gets its own driver rather than PlayerController knowing about them all
[RequireComponent(typeof(Animator))]
public class CharacterAnimatorDriver : MonoBehaviour
{
    [Header("Setup")]
    public PlayerController playerMovement;
    public BladeMaster bladeMaster; // optional - only assign on the BladeMaster model

    [Header("Blend")]
    [Tooltip("Seconds to ease between locomotion directions. Stops the blend tree snapping on key press/release.")]
    public float blendDampTime = 0.12f;

    [Tooltip("How far out the blend tree is pushed when sprinting. Must match the sprint clip's Pos Y in the blend tree.")]
    public float sprintBlendDistance = 2f;

    Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (playerMovement == null) return;

        //sprinting pushes the same direction further from the origin, so the blend tree
        //reaches the sprint clip sitting further out along that axis
        Vector2 input = playerMovement.MoveInput;
        if (playerMovement.IsSprinting)
            input *= sprintBlendDistance;

        //SetFloat's damping overload does the smoothing for us, no manual Lerp needed
        animator.SetFloat("MoveX", input.x, blendDampTime, Time.deltaTime);
        animator.SetFloat("MoveZ", input.y, blendDampTime, Time.deltaTime);

        if (bladeMaster != null)
            animator.SetBool("IsBlocking", bladeMaster.IsBlocking);
    }
}
