using UnityEngine;

//keeps its distance instead of closing in: it walks until its target is within firing range and
//backs off if something gets too close, so it behaves like artillery rather than a brawler.
//
//people come first - a player, or an allied soldier once they exist, inside detection range is
//what it fights. with nobody in range it does not stand idle waiting for someone to wander past:
//it heads for the wall, however far away that is, and holds off it the same way it would a
//person. it used to only see the wall once it was inside detection range, so from the spawn row
//it just stood there.
[RequireComponent(typeof(NpcNavigator))]
public class RangedVirusMovement : MonoBehaviour
{
    public float speed = 2f;

    [Tooltip("How far it looks for people. The wall is found at any distance; this only decides " +
             "when someone close enough pulls it off the wall.")]
    public float detectionRange = 30f;

    [Tooltip("It stops closing once the target is this near.")]
    public float preferredRange = 14f;

    [Tooltip("Anything closer than this and it backs away.")]
    public float tooClose = 8f;

    public float turnSpeed = 240f;

    [Tooltip("Seconds between searches for the wall. It is a scene-wide search and the wall does " +
             "not move, so not every frame.")]
    public float wallSearchInterval = 1f;

    VirusLedgeJump traversal;
    NpcNavigator navigator;

    WallHealth wall;
    Collider wallCollider;
    float nextWallSearch;

    void Awake()
    {
        traversal = GetComponent<VirusLedgeJump>();
        navigator = GetComponent<NpcNavigator>();
    }

    void Update()
    {
        if (BladeMaster.UltimateFreeze) return;
        if (traversal != null && traversal.IsBusy) return;

        if (!FindTarget(out Vector3 targetPoint)) return;

        Vector3 toTarget = targetPoint - transform.position;
        toTarget.y = 0f;

        float dist = toTarget.magnitude;
        if (dist < 0.01f) return;

        Vector3 dir = toTarget.normalized;

        //it faces its target while it holds its ground or backs off, because it shoots from where
        //it stands. walking in, it looks the way it is going - the navigator turns it
        if (dist <= preferredRange)
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                                                          Quaternion.LookRotation(dir),
                                                          turnSpeed * Time.deltaTime);

        //inside its comfortable band it holds position
        if (dist <= preferredRange && dist >= tooClose) return;

        //backing off it keeps its eyes on whatever it is backing away from, so the navigator leaves
        //its facing alone
        if (dist < tooClose) navigator.MoveAwayFrom(targetPoint, speed, 0f);
        else navigator.MoveTo(targetPoint, speed, turnSpeed);
    }

    bool FindTarget(out Vector3 point)
    {
        //one shared scan for the whole virus set. walls are left out of it so that a person in
        //range always wins, even one standing further off than the wall does.
        IDamageable person = TargetScan.NearestDamageable(transform, detectionRange, includeWalls: false);

        if (person != null)
        {
            point = ((MonoBehaviour)person).transform.position;
            return true;
        }

        if ((wall == null || !wall.IsTargetable) && Time.time >= nextWallSearch)
        {
            nextWallSearch = Time.time + wallSearchInterval;

            //the bloom's wall search, reused - it already finds the wall at any range
            wall = BloomVirusTarget.Nearest(transform.position);
            wallCollider = wall != null ? wall.GetComponentInChildren<Collider>() : null;
        }

        if (wall == null || !wall.IsTargetable)
        {
            point = default;
            return false;
        }

        //the nearest point on its face rather than its centre: the wall is wide, and its centre
        //is nowhere near where something coming in from the side would end up standing
        point = wallCollider != null
            ? wallCollider.bounds.ClosestPoint(transform.position)
            : wall.transform.position;

        return true;
    }
}
