using UnityEngine;

//feeds a virus model's Animator from what the virus is actually doing. the AI scripts move the
//transform directly rather than through a controller, so speed is measured from how far it
//actually travelled rather than read off anything - that way it stays honest no matter which
//movement script is driving it.
[RequireComponent(typeof(Animator))]
public class VirusAnimatorDriver : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("The health component. Drives the death animation.")]
    public VirusHealthBase health;

    [Tooltip("How fast its animations play. A quick virus reads better sped up.")]
    public float animationSpeed = 1f;

    [Tooltip("What actually moves. Leave empty to measure this object.")]
    public Transform movementRoot;

    [Header("Blend")]
    [Tooltip("Speed the walk sits at, in metres per second.")]
    public float walkSpeed = 1f;

    [Tooltip("Speed the run sits at. Anything faster just stays at the run.")]
    public float runSpeed = 3f;

    [Tooltip("Seconds to ease the speed reading, so a stutter in position doesn't flicker the blend.")]
    public float speedDamp = 0.15f;

    [Tooltip("Length of the jump clip. The hop is usually shorter, so the clip is sped up to " +
             "fit it - otherwise the creature lands before the animation has left the ground.")]
    public float jumpClipLength = 1.1f;

    //found rather than assigned, so this one driver serves every virus type
    IVirusAttack attack;
    VirusLedgeJump traversal;

    Animator animator;
    Vector3 lastPosition;
    float smoothedSpeed;

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (movementRoot == null) movementRoot = transform.parent != null ? transform.parent : transform;
        if (health == null) health = GetComponentInParent<VirusHealthBase>();
        attack = GetComponentInParent<IVirusAttack>();
        traversal = GetComponentInParent<VirusLedgeJump>();

        animator.speed = animationSpeed;

        lastPosition = movementRoot.position;
    }

    void OnEnable()
    {
        if (attack != null) attack.OnAttack += PlayAttack;
        if (health != null) health.OnDied += PlayDeath;
        if (traversal != null) traversal.OnJump += PlayJump;
    }

    void OnDisable()
    {
        if (attack != null) attack.OnAttack -= PlayAttack;
        if (health != null) health.OnDied -= PlayDeath;
        if (traversal != null) traversal.OnJump -= PlayJump;
    }

    void PlayAttack(int swing)
    {
        if (health != null && health.IsDead) return;

        //whole numbers, so the blend tree lands on one clip outright rather than mixing two
        animator.SetFloat("AttackIndex", swing);
        animator.SetTrigger("Attack");
    }

    void PlayJump()
    {
        if (health != null && health.IsDead) return;

        //played at whatever rate covers the hop, so take-off, peak and landing line up with
        //where the creature actually is
        if (traversal != null && traversal.JumpDuration > 0.01f)
            animator.SetFloat("JumpSpeed", jumpClipLength / traversal.JumpDuration);

        animator.SetTrigger("Jump");
    }

    void PlayDeath()
    {
        //a bool rather than a trigger: a trigger can be consumed by the wrong transition, and
        //death is the one state that must never be left half-entered
        animator.SetBool("Dead", true);
    }

    void Update()
    {
        //measured, not asked for - the movement script writes straight to the transform
        Vector3 now = movementRoot.position;
        Vector3 step = now - lastPosition;
        step.y = 0f;
        lastPosition = now;

        float raw = Time.deltaTime > 0f ? step.magnitude / Time.deltaTime : 0f;
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, raw, speedDamp > 0f ? Time.deltaTime / speedDamp : 1f);

        //0 = idle, 1 = walk, 2 = run, so the blend tree can sit on whole numbers
        float blend;
        if (smoothedSpeed <= 0.05f) blend = 0f;
        else if (smoothedSpeed <= walkSpeed) blend = Mathf.InverseLerp(0f, walkSpeed, smoothedSpeed);
        else blend = 1f + Mathf.InverseLerp(walkSpeed, runSpeed, smoothedSpeed);

        animator.SetFloat("Speed", blend);
    }
}
