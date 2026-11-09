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
    [Tooltip("Speed the run clip plays at: below 1 the legs turn over slower.")]
    public float runClipSpeed = 0.8f;

    [Tooltip("In the surround, it flinches (the impact clip) when hit, at most this often.")]
    public float impactCooldown = 1.2f;

    [Header("Idles")]
    [Tooltip("Seconds stood still before a front-row knight sits down.")]
    public float sitDelay = 1f;
    [Tooltip("Seconds a back-row knight stands relaxed before switching to the ready idle.")]
    public Vector2 standingSeconds = new Vector2(15f, 30f);
    [Tooltip("Seconds it holds the ready idle before relaxing again.")]
    public Vector2 readySeconds = new Vector2(2f, 3.5f);
    public float idleDampTime = 0.3f;

    //IdleVariant values in the animator's Idles blend
    const float Ready = 0f, Standing = 1f, Sitting = 2f;

    float stillFor;
    bool ready;
    float nextSwap;
    float wallRow = 1f;
    float lastHealth = -1f;
    float nextImpact;

    Animator animator;
    Transform body;
    Vector3 lastPosition;

    void Awake()
    {
        animator = GetComponent<Animator>();
        body = soldier != null ? soldier.transform : transform.root;
        lastPosition = body.position;
        nextSwap = Time.time + Random.Range(standingSeconds.x, standingSeconds.y);
    }

    void OnEnable()
    {
        if (soldier != null) soldier.OnAttack += HandleAttack;
        if (health != null) health.OnHealthChanged += HandleHealth;
    }

    void OnDisable()
    {
        if (soldier != null) soldier.OnAttack -= HandleAttack;
        if (health != null) health.OnHealthChanged -= HandleHealth;
    }

    void HandleAttack(int swing)
    {
        animator.SetFloat("ComboStep", swing);
        animator.SetTrigger("Attack");
    }

    void HandleHealth(float current, float max)
    {
        bool hurt = current < (lastHealth >= 0f ? lastHealth : max);
        lastHealth = current;
        if (!hurt || current <= 0f || Time.time < nextImpact) return;
        if (soldier == null || soldier.squad == null || !soldier.squad.Surrounding) return;
        nextImpact = Time.time + impactCooldown;
        animator.SetTrigger("Hit");
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

        //faster than its run clip (keeping up with the player), the clip plays faster so the feet don't slide
        float target = speed > walkSpeed ? runClipSpeed : 1f;
        animator.speed = Mathf.MoveTowards(animator.speed, target, 2f * Time.deltaTime);

        animator.SetFloat("MoveX", 0f, blendDampTime, Time.deltaTime);
        animator.SetFloat("MoveZ", forward, blendDampTime, Time.deltaTime);
        animator.SetFloat("IdleVariant", PickIdle(speed), idleDampTime, Time.deltaTime);

        //in the surround's shield wall: kneeling in the front row, shield up behind it. moving up to fill a
        //gap it stands to walk, then takes the pose of its new row
        bool inWall = soldier != null && soldier.squad != null && soldier.AtPlace && soldier.squad.InWall(soldier, out wallRow);
        animator.SetBool("ShieldWall", inWall);
        animator.SetFloat("WallRow", wallRow, idleDampTime, Time.deltaTime);
    }

    //the front half sits, the back half mostly stands relaxed; fighting or squadless, it stays ready
    float PickIdle(float speed)
    {
        bool fighting = soldier == null || soldier.Target != null;
        stillFor = !fighting && speed < 0.1f ? stillFor + Time.deltaTime : 0f;

        if (fighting || soldier.squad == null) return Ready;
        //in any formation, everyone off the wall stands relaxed: the idle stance is set for the standing idle
        if (soldier.squad.Gridded || soldier.squad.Surrounding) return Standing;

        if (soldier.squad.ShouldSit(soldier)) return stillFor >= sitDelay ? Sitting : Standing;

        if (Time.time >= nextSwap)
        {
            ready = !ready;
            Vector2 range = ready ? readySeconds : standingSeconds;
            nextSwap = Time.time + Random.Range(range.x, range.y);
        }
        return ready ? Ready : Standing;
    }
}
