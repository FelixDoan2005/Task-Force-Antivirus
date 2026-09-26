using UnityEngine;

//goes for the player and only the player. it walks straight past the wall - by the time one of
//these exists, the mound it came out of has already died trying to reach whatever was nearest.
public class MoundlingMovement : MonoBehaviour
{
    [Tooltip("Matches the speed virus, the fastest thing in the set.")]
    public float speed = 7f;

    public float contactBuffer = 0.3f;
    public float turnSpeed = 540f;

    [Tooltip("Seconds between player lookups. It is a scene-wide search, so not every frame.")]
    public float retargetInterval = 0.5f;

    Collider ownCollider;
    VirusLedgeJump traversal;

    PlayerHealth player;
    float nextSearch;

    //the attack reads this, so the two can never work on different targets
    public PlayerHealth Player => player;

    void Awake()
    {
        ownCollider = GetComponent<Collider>();
        traversal = GetComponent<VirusLedgeJump>();
    }

    void Update()
    {
        if (BladeMaster.UltimateFreeze) return;

        //mid-hop the jump owns where it is, or the two fight over the same transform
        if (traversal != null && traversal.IsBusy) return;

        if (player == null || !player.IsTargetable) FindPlayer();
        if (player == null) return;

        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0f;

        Vector3 dir = toPlayer.normalized;

        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                                                          Quaternion.LookRotation(dir),
                                                          turnSpeed * Time.deltaTime);

        if (MoundTarget.SurfaceGap(transform, ownCollider, player.transform) <= contactBuffer) return;

        //something climbable in the way is hopped onto rather than walked through
        if (traversal != null && traversal.TryClimb(dir)) return;

        transform.position += speed * Time.deltaTime * dir;
    }

    void FindPlayer()
    {
        if (Time.time < nextSearch) return;

        nextSearch = Time.time + retargetInterval;
        player = MoundTarget.NearestPlayer(transform.position);
    }
}
