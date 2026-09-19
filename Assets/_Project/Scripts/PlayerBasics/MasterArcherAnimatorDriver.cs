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

    [Tooltip("Drives the death animation. Assign the Player's PlayerHealth.")]
    public PlayerHealth playerHealth;

    [Header("Blend")]
    [Tooltip("Seconds to ease between stand, run and sprint.")]
    public float blendDampTime = 0.12f;

    [Tooltip("How far out sprinting pushes the blend. Must match the sprint clips' position.")]
    public float sprintBlendDistance = 2f;

    [Header("Bow")]
    [Tooltip("The masked layer holding the draw animation.")]
    public string bowLayerName = "Bow";

    [Tooltip("The state on that layer holding the draw clip.")]
    public string drawStateName = "Draw";

    [Tooltip("Where in the draw clip the arrow is nocked and the pull actually starts. The first " +
             "half of the clip is her reaching for an arrow, which shouldn't play while charging - " +
             "charge maps onto the stretch from here to the end instead.")]
    [Range(0f, 1f)] public float drawPullStart = 0.55f;

    [Tooltip("The state on the bow layer holding the melee swing.")]
    public string meleeStateName = "Melee";

    Animator animator;
    int bowLayer = -1;
    int drawStateHash;
    int meleeStateHash;

    void Awake()
    {
        animator = GetComponent<Animator>();
        bowLayer = animator.GetLayerIndex(bowLayerName);
        drawStateHash = Animator.StringToHash(drawStateName);
        meleeStateHash = Animator.StringToHash(meleeStateName);
    }

    //the swing is a one-off, so it is played on the event rather than polled from state
    void OnEnable()
    {
        if (archer != null) archer.OnMelee += PlayMelee;
    }

    void OnDisable()
    {
        if (archer != null) archer.OnMelee -= PlayMelee;
    }

    void PlayMelee()
    {
        if (bowLayer >= 0) animator.Play(meleeStateHash, bowLayer, 0f);
    }

    void Update()
    {
        //driven from state every frame rather than from events, so it survives a pause and
        //can't get stuck on if something is interrupted
        if (playerHealth != null)
            animator.SetBool("IsDead", playerHealth.isDead);

        if (archer != null)
        {
            animator.SetBool("IsAiming", archer.IsAiming);

            bool drawing = archer.IsDrawing();
            animator.SetBool("IsDrawing", drawing);

            //scrub the draw by how charged the shot is rather than letting the clip play at its
            //own pace. the pose then always matches the arrow: a flick of the mouse barely bends
            //the bow and fires a weak arrow, holding to full power reaches full draw.
            if (drawing && bowLayer >= 0)
                animator.Play(drawStateHash, bowLayer,
                              Mathf.Lerp(drawPullStart, 1f, archer.DrawProgress01));
        }

        if (playerMovement == null) return;

        animator.SetBool("IsCrouching", playerMovement.IsCrouching);

        Vector2 input = playerMovement.MoveInput;
        if (playerMovement.IsSprinting) input *= sprintBlendDistance;

        animator.SetFloat("MoveX", input.x, blendDampTime, Time.deltaTime);
        animator.SetFloat("MoveZ", input.y, blendDampTime, Time.deltaTime);
    }
}
