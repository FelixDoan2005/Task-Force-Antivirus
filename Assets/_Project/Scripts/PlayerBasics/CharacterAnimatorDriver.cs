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

    [Header("Ultimate")]
    [Tooltip("How many swing clips are in the UltSwing blend tree. Each blink picks one at random. " +
             "Leave at 1 if you only have a single swing.")]
    public int ultimateSwingVariants = 1;

    [Tooltip("Prints which swing variant each blink picked, to check they're actually varying.")]
    public bool logUltSwings;

    Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void OnEnable()
    {
        if (bladeMaster == null) return;
        bladeMaster.OnDash += HandleDash;
        bladeMaster.OnUltimateStarted += HandleUltimateStarted;
        bladeMaster.OnUltimateBlink += HandleUltimateBlink;
        bladeMaster.OnUltimateEnded += HandleUltimateEnded;
    }

    void OnDisable()
    {
        if (bladeMaster == null) return;
        bladeMaster.OnDash -= HandleDash;
        bladeMaster.OnUltimateStarted -= HandleUltimateStarted;
        bladeMaster.OnUltimateBlink -= HandleUltimateBlink;
        bladeMaster.OnUltimateEnded -= HandleUltimateEnded;
    }

    void HandleDash()
    {
        animator.SetTrigger("Dash");
    }

    void HandleUltimateStarted()
    {
        animator.SetBool("IsUltimate", true);   // holds the channel/windup pose
    }

    void HandleUltimateBlink()
    {
        //pick a random swing variant. the blend tree's thresholds are whole numbers and this
        //value lands exactly on one, so it plays that clip outright rather than blending two
        int variant = 0;
        if (ultimateSwingVariants > 1)
        {
            variant = Random.Range(0, ultimateSwingVariants);
            animator.SetFloat("UltSwingIndex", variant);
        }

        if (logUltSwings)
            Debug.Log($"ult swing variant {variant}");

        animator.SetTrigger("UltSwing");
    }

    void HandleUltimateEnded()
    {
        animator.SetBool("IsUltimate", false);

        //triggers stay queued until a transition consumes them. if the last blink's trigger
        //hasn't been used by the time the sequence ends, it would fire one stray swing
        //after we're already back in locomotion.
        animator.ResetTrigger("UltSwing");
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
