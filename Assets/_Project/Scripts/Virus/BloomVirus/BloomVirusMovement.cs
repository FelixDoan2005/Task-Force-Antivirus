using UnityEngine;

//creeps at the wall and nothing else. it will not chase, will not turn aside for a player
//standing in front of it, and has no detection range to fall outside of - it knows where the
//wall is from the moment it spawns and walks until it is touching it.
[RequireComponent(typeof(NpcNavigator))]
public class BloomVirusMovement : MonoBehaviour
{
    [Tooltip("Metres per second. Deliberately a crawl - this thing is meant to be something you " +
             "notice late, not something that races you to the wall.")]
    public float speed = 0.5f;

    [Tooltip("Clear space left between its body and the wall, measured between the two colliders " +
             "rather than their centres.")]
    public float contactBuffer = 0.5f;

    [Tooltip("How fast it swings round to face the wall.")]
    public float turnSpeed = 90f;

    [Tooltip("Seconds between re-checks for the wall. It is a scene-wide search, so it is not run " +
             "every frame - the wall does not move.")]
    public float retargetInterval = 1f;

    Collider ownCollider;
    VirusLedgeJump traversal;
    NpcNavigator navigator;

    WallHealth wall;
    float nextSearch;

    //the attack reads this, so the two can never end up working on different targets
    public WallHealth Wall => wall;

    void Awake()
    {
        ownCollider = GetComponent<Collider>();
        traversal = GetComponent<VirusLedgeJump>();
        navigator = GetComponent<NpcNavigator>();
    }

    void Update()
    {
        if (BladeMaster.UltimateFreeze) return;

        //mid-hop the jump owns where it is, or the two fight over the same transform
        if (traversal != null && traversal.IsBusy) return;

        if (wall == null || !wall.IsTargetable) FindWall();
        if (wall == null) return;

        Vector3 toWall = WallPoint() - transform.position;
        toWall.y = 0f;

        Vector3 dir = toWall.normalized;

        if (BloomVirusTarget.SurfaceGap(transform, ownCollider, wall) <= contactBuffer)
        {
            //it keeps facing the wall once it has stopped, so it is hitting what it looks at.
            //while it walks it faces the way it is going
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                                                              Quaternion.LookRotation(dir),
                                                              turnSpeed * Time.deltaTime);
            return;
        }

        //the way there, round whatever is in the way, is the navigator's - hops included. it is
        //aimed at the nearest part of the wall, the same as WallPoint
        navigator.MoveTo(wall.transform, speed, turnSpeed);
    }

    void FindWall()
    {
        if (Time.time < nextSearch) return;

        nextSearch = Time.time + retargetInterval;
        wall = BloomVirusTarget.Nearest(transform.position);
    }

    //it walks at the nearest point on the wall's face, not at the wall's centre, so it goes
    //straight at whatever part of it is in front rather than sliding along to the middle
    Vector3 WallPoint()
    {
        Collider wallCollider = wall.GetComponentInChildren<Collider>();

        return wallCollider != null
            ? wallCollider.bounds.ClosestPoint(transform.position)
            : wall.transform.position;
    }
}
