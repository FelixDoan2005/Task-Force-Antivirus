using UnityEngine;

//feeds the soldier's Knight animator from what it's actually doing
[RequireComponent(typeof(Animator))]
public class SoldierAnimatorDriver : MonoBehaviour
{
    public SoldierAI soldier;
    public SoldierHealth health;

    [Tooltip("Speed (m/s) that plays the walk clip. Faster than this blends toward the run.")]
    public float walkSpeed = 1.5f;

    [Tooltip("Speed (m/s) that plays the run clip.")]
    public float runSpeed = 3.5f;

    public float blendDampTime = 0.12f;

    Animator animator;
    Transform body;
    Vector3 lastPosition;

    void Awake()
    {
        animator = GetComponent<Animator>();
        body = soldier != null ? soldier.transform : transform.root;
        lastPosition = body.position;
    }

    void OnEnable()
    {
        if (soldier != null) soldier.OnAttack += HandleAttack;
    }

    void OnDisable()
    {
        if (soldier != null) soldier.OnAttack -= HandleAttack;
    }

    void HandleAttack(int swing)
    {
        animator.SetFloat("ComboStep", swing);
        animator.SetTrigger("Attack");
    }

    void Update()
    {
        if (health != null) animator.SetBool("IsDead", health.IsDead);

        //measured from how far it actually moved, so it's right whatever is moving it
        Vector3 step = body.position - lastPosition;
        step.y = 0f;
        lastPosition = body.position;
        float speed = Time.deltaTime > 0f ? step.magnitude / Time.deltaTime : 0f;

        //the blend tree has walk at 1 and run at 2 straight ahead
        float forward = speed <= walkSpeed
            ? speed / walkSpeed
            : 1f + Mathf.InverseLerp(walkSpeed, runSpeed, speed);

        animator.SetFloat("MoveX", 0f, blendDampTime, Time.deltaTime);
        animator.SetFloat("MoveZ", forward, blendDampTime, Time.deltaTime);
    }
}
