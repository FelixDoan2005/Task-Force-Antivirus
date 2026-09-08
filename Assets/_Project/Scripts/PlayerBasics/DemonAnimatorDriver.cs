using UnityEngine;

//feeds the Demon's Animator. he uses a single 1D blend tree rather than the BladeMaster's
//2D directional one, because his three clips are all forward locomotion - idle, run, sprint.
//sits on the model, not on the class object.
[RequireComponent(typeof(Animator))]
public class DemonAnimatorDriver : MonoBehaviour
{
    [Header("Setup")]
    public PlayerController playerMovement;
    public DemonClass demon;
    public PlayerHealth playerHealth;

    [Header("Blend")]
    [Tooltip("Seconds to ease between idle, run and sprint. Stops the tree snapping on key press.")]
    public float blendDampTime = 0.12f;

    [Tooltip("How far out sprinting pushes the blend. Must match the sprint clip's position " +
             "in the blend tree (currently MoveZ = 2).")]
    public float sprintBlendDistance = 2f;

    Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    //subscribed rather than polled, so a one-shot ability fires exactly once
    void OnEnable()
    {
        if (demon == null) return;
        demon.OnFireStarted += HandleFire;
        demon.OnBombThrown += HandleBomb;
        demon.OnTakeover += HandleTakeover;
        demon.OnCAbility += HandleCAbility;
    }

    void OnDisable()
    {
        if (demon == null) return;
        demon.OnFireStarted -= HandleFire;
        demon.OnBombThrown -= HandleBomb;
        demon.OnTakeover -= HandleTakeover;
        demon.OnCAbility -= HandleCAbility;
    }

    void HandleFire()     { animator.SetTrigger("Attack"); }
    void HandleBomb()     { animator.SetTrigger("QAbility"); }
    void HandleTakeover() { animator.SetTrigger("EAbility"); }
    void HandleCAbility() { animator.SetTrigger("CAbility"); }

    void Update()
    {
        //driven from state every frame rather than from the start/end events, so they survive a
        //pause and can't get stuck on if the ultimate is interrupted.
        //IsUltimate covers the whole ability, BeamsFiring only the half where the beams are out -
        //together they pick between the windup pose and the firing pose.
        if (demon != null)
        {
            animator.SetBool("IsUltimate", demon.IsUltimateActive);
            animator.SetBool("BeamsFiring", demon.BeamsFiring);
        }

        if (playerMovement == null) return;

        //sprinting pushes the same direction further from the origin, so the tree reaches the
        //sprint clip sitting further out along that axis.
        //MoveInput is already zeroed by PlayerController whenever movement is suspended, so
        //an ability that roots him drops back to idle without any extra checks here.
        Vector2 input = playerMovement.MoveInput;
        if (playerMovement.IsSprinting) input *= sprintBlendDistance;

        animator.SetFloat("MoveX", input.x, blendDampTime, Time.deltaTime);
        animator.SetFloat("MoveZ", input.y, blendDampTime, Time.deltaTime);
    }
}
