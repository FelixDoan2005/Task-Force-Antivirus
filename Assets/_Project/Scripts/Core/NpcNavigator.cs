using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NpcNavigator : MonoBehaviour
{
    [Tooltip("How quickly it gets up to speed and stops. High, so it moves the way it always has " +
             "rather than drifting into place.")]
    public float acceleration = 40f;

    [Tooltip("Seconds between route updates while what it is heading for keeps moving.")]
    public float repathInterval = 0.2f;

    [Tooltip("How far what it is heading for has to move before the route is worked out again.")]
    public float repathDistance = 0.4f;

    [Tooltip("When the navmesh cannot reach the destination - someone up on a ledge, say - this is " +
             "how near the end of the route it gets before walking straight on, to hop up or step off.")]
    public float straightOnWithin = 1f;

    [Tooltip("How far ahead it looks for room when backing away from something.")]
    public float retreatLookAhead = 3f;

    NavMeshAgent agent;
    VirusLedgeJump traversal;
    float radius;

    //its own agent type's navmesh - big viruses have one baked further back from walls
    NavMeshQueryFilter filter;
    float meshRadius;

    bool attached;
    float nextAttach;

    //this frame's order
    int orderFrame = -1;
    Vector3 orderPoint;
    Transform orderTarget;
    bool orderAway;
    float orderSpeed;
    float orderTurn;

    //the route it is on
    bool routed;
    bool routedAway;
    bool noRoute;
    bool straightOn;
    bool hopAtEnd;
    bool shortOfTarget;
    NavMeshPath scratchPath;
    Vector3 routedFor;
    float nextRepath;
    float routeExpires;

    Vector3 lastBody;
    bool wasHopping;

    static readonly float[] retreatAngles = { 0f, 45f, -45f, 90f, -90f, 135f, -135f };

    //false while it is falling, mid-hop, or in a scene with no navmesh - it walks straight then
    public bool IsOnNavMesh => attached;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        traversal = GetComponent<VirusLedgeJump>();

        Collider body = GetComponent<Collider>();
        Bounds bounds = body != null ? body.bounds : new Bounds(transform.position, Vector3.one);
        radius = Mathf.Max(bounds.extents.x, bounds.extents.z);

        //it steers; the transform is written here and by the ledge jump, never by the agent
        agent.updatePosition = false;
        agent.updateRotation = false;

        //sized to the body, so they keep to their own space rather than overlapping
        agent.radius = radius;
        agent.height = Mathf.Max(bounds.size.y, 0.5f);
        agent.baseOffset = 0f;

        //it stops when its movement script says so, not when the agent thinks it has arrived
        agent.autoBraking = false;
        agent.stoppingDistance = 0f;
        agent.acceleration = acceleration;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

        filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = NavMesh.AllAreas };
        meshRadius = NavMesh.GetSettingsByID(agent.agentTypeID).agentRadius;
        scratchPath = new NavMeshPath();

        //switched on once it is standing on the navmesh, which a virus dropped in from above is not
        agent.enabled = false;
    }

    void OnEnable()
    {
        nextAttach = 0f;
    }

    void OnDisable()
    {
        Detach();
    }

    //---------------------------------------------------------------- orders

    public void MoveTo(Vector3 point, float speed, float turnSpeed)
    {
        Order(point, null, false, speed, turnSpeed);
    }

    public void MoveTo(Transform target, float speed, float turnSpeed)
    {
        Order(target.position, target, false, speed, turnSpeed);
    }

    public void MoveAwayFrom(Vector3 threat, float speed, float turnSpeed)
    {
        Order(threat, null, true, speed, turnSpeed);
    }

    void Order(Vector3 point, Transform target, bool away, float speed, float turnSpeed)
    {
        orderFrame = Time.frameCount;
        orderPoint = point;
        orderTarget = target;
        orderAway = away;
        orderSpeed = speed;
        orderTurn = turnSpeed;

        if (!attached) return;

        agent.speed = speed;

        bool moved = (point - routedFor).sqrMagnitude > repathDistance * repathDistance;

        if (!routed || away != routedAway || Time.time >= routeExpires ||
            (Time.time >= nextRepath && (away || moved)))
            Route();

        straightOn = ShouldWalkStraight();

    
        agent.isStopped = straightOn;
    }

    void Route()
    {
        routed = true;
        routedAway = orderAway;
        routedFor = orderPoint;
        nextRepath = Time.time + repathInterval;

        routeExpires = Time.time + 1f;

        Vector3 goal;

        if (orderAway)
            goal = RetreatPoint(orderPoint);
        else if (!OnNavMesh(AimPoint(), out goal))
        {

            noRoute = true;
            agent.ResetPath();
            return;
        }

        //walkable ground ends well short of what it's after (a big body at a wall): it closes the last bit straight
        shortOfTarget = !orderAway && Flat(AimPoint() - goal).magnitude > radius + 0.5f;

        //up on something it can hop onto: straight to the edge on the way, not round to the nearest side
        hopAtEnd = !orderAway && Unreachable(goal) && NothingTooTallBetween();
        if (hopAtEnd) goal = EdgeTowardTarget();

        noRoute = !agent.SetDestination(goal);
    }

    bool ShouldWalkStraight()
    {
        if (noRoute) return true;


        if (agent.pathPending) return straightOn;
        if (agent.pathStatus == NavMeshPathStatus.PathInvalid) return true;

        //near the end of a route that can't reach, stops short, or ends at the edge it means to hop up from
        return (hopAtEnd || shortOfTarget || agent.pathStatus == NavMeshPathStatus.PathPartial) &&
               agent.remainingDistance <= straightOnWithin;
    }

    bool Unreachable(Vector3 goal)
    {
        return NavMesh.CalculatePath(agent.nextPosition, goal, filter, scratchPath) &&
               scratchPath.status == NavMeshPathStatus.PathPartial;
    }

    //where the straight line to the target leaves the walkable ground - the edge to hop up (or drop down) from
    Vector3 EdgeTowardTarget()
    {
        Vector3 from = agent.nextPosition;
        Vector3 target = orderTarget != null ? orderTarget.position : orderPoint;
        target.y = from.y;

        return NavMesh.Raycast(from, target, out NavMeshHit edge, filter) ? edge.position : target;
    }

    bool NothingTooTallBetween()
    {
        Vector3 to = (orderTarget != null ? orderTarget.position : orderPoint) - transform.position;
        to.y = 0f;
        float distance = to.magnitude;
        if (distance < 0.01f) return true;

        float shin = traversal != null ? traversal.stepHeight + 0.05f : 0.35f;
        if (!Physics.SphereCast(Feet() + Vector3.up * shin, radius * 0.6f, to / distance, out RaycastHit hit,
                                distance, ~0, QueryTriggerInteraction.Ignore))
            return true;

        if (orderTarget != null && hit.collider.transform.IsChildOf(orderTarget)) return true;
        if (hit.collider.GetComponentInParent<VirusHealthBase>() != null) return true;

        return traversal != null && hit.collider.bounds.max.y - Feet().y <= traversal.maxJumpHeight;
    }

    //---------------------------------------------------------------- moving

    void LateUpdate()
    {
        if (!attached && Time.time >= nextAttach) Attach();

        bool hopping = traversal != null && traversal.IsBusy;


        if (attached && !hopping &&
            (wasHopping || Flat(transform.position - lastBody).sqrMagnitude > 0.0001f))
            Resync();

        wasHopping = hopping;

        bool ordered = orderFrame == Time.frameCount;
        bool busy = BladeMaster.UltimateFreeze || hopping;

        if (!ordered || busy) Hold();
        else if (!attached || straightOn) WalkStraight();
        else FollowAgent();

        lastBody = transform.position;
    }

 
    void FollowAgent()
    {
        Vector3 next = agent.nextPosition;
        Vector3 p = transform.position;

    
        Vector3 flat = Vector3.MoveTowards(new Vector3(p.x, 0f, p.z), new Vector3(next.x, 0f, next.z),
                                           (orderSpeed * 2f + 1f) * Time.deltaTime);

        transform.position = new Vector3(flat.x, p.y, flat.z);

        //it looks where it means to go, not wherever the crowd happens to be nudging it
        Face(agent.desiredVelocity, orderTurn);
    }

    void WalkStraight()
    {
        Vector3 to = orderAway ? transform.position - orderPoint : AimPoint() - transform.position;
        to.y = 0f;

        if (to.sqrMagnitude < 0.0001f) return;

        Vector3 dir = to.normalized;
        Face(dir, orderTurn);

        if (traversal != null && traversal.TryClimb(dir)) return;

        //something solid in the way and too tall to hop: it stops against it, rather than walking on
        //through it the way they all used to
        if (Blocked(dir)) return;

        transform.position += orderSpeed * Time.deltaTime * dir;

        if (attached) Resync();
    }

    void Hold()
    {
        if (!attached) return;

        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        //mid-hop it is caught up with once it lands
        if (traversal == null || !traversal.IsBusy) Resync();
    }

    void Face(Vector3 direction, float turnSpeed)
    {
        direction.y = 0f;
        if (turnSpeed <= 0f || direction.sqrMagnitude < 0.01f) return;

        transform.rotation = Quaternion.RotateTowards(transform.rotation,
                                                      Quaternion.LookRotation(direction),
                                                      turnSpeed * Time.deltaTime);
    }

    bool Blocked(Vector3 dir)
    {
        float shin = traversal != null ? traversal.stepHeight + 0.05f : 0.35f;
        Vector3 origin = Feet() + Vector3.up * shin;

        if (!Physics.SphereCast(origin, radius * 0.6f, dir, out RaycastHit hit, radius + 0.15f,
                                ~0, QueryTriggerInteraction.Ignore))
            return false;

        //other viruses are not walls, and nor are its own parts
        return hit.collider.GetComponentInParent<VirusHealthBase>() == null &&
               !hit.collider.transform.IsChildOf(transform);
    }

    //---------------------------------------------------------------- the agent

    void Attach()
    {
        nextAttach = Time.time + 0.25f;

        if (!NavMesh.SamplePosition(Feet(), out NavMeshHit hit, meshRadius + 0.5f, filter)) return;

       
        Vector3 body = transform.position;
        transform.position = hit.position;
        agent.enabled = true;
        transform.position = body;

        attached = agent.isOnNavMesh;
        routed = false;
        lastBody = body;

        //spread out, so a wave that spawned together does not all re-route on the same frame
        nextRepath = Time.time + Random.Range(0f, repathInterval);
    }

    void Detach()
    {
        attached = false;
        routed = false;
        nextAttach = Time.time + 0.25f;

        if (agent != null && agent.enabled) agent.enabled = false;
    }

    //puts the agent back under the body
    void Resync()
    {
        Vector3 feet = Feet();
        agent.nextPosition = feet;

        Vector3 gap = agent.nextPosition - feet;

        
        if (Flat(gap).sqrMagnitude <= (meshRadius + 0.25f) * (meshRadius + 0.25f) && Mathf.Abs(gap.y) <= 0.45f) return;

    
        if (!NavMesh.SamplePosition(feet, out NavMeshHit hit, meshRadius + 0.5f, filter))
        {
            Detach();
            return;
        }

    
        if ((hit.position - agent.nextPosition).sqrMagnitude < 0.01f) return;

        Vector3 body = transform.position;
        bool warped = agent.Warp(hit.position);
        transform.position = body;

        if (warped) routed = false;
        else Detach();
    }

    Vector3 Feet()
    {
        return transform.position - Vector3.up * (traversal != null ? traversal.FeetOffset : 0f);
    }

    Vector3 AimPoint()
    {
        if (orderTarget == null) return orderPoint;

        Collider c = orderTarget.GetComponentInChildren<Collider>();
        return c != null && c.enabled ? c.bounds.ClosestPoint(transform.position) : orderTarget.position;
    }

    
    bool OnNavMesh(Vector3 point, out Vector3 onMesh)
    {
        //where it actually is first, so someone standing up on a ledge is looked for on that ledge
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, meshRadius + 1f, filter))
        {
            onMesh = hit.position;
            return true;
        }

        point.y = Feet().y;

        if (NavMesh.SamplePosition(point, out hit, meshRadius + 2.5f, filter))
        {
            onMesh = hit.position;
            return true;
        }

        onMesh = point;
        return false;
    }


    Vector3 RetreatPoint(Vector3 threat)
    {
        Vector3 from = agent.nextPosition;

        Vector3 away = from - threat;
        away.y = 0f;
        away = away.sqrMagnitude > 0.0001f ? away.normalized : -transform.forward;

        Vector3 best = from;
        float bestRoom = 0f;

        foreach (float angle in retreatAngles)
        {
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * away;

            float room = NavMesh.Raycast(from, from + dir * retreatLookAhead, out NavMeshHit hit, filter)
                ? hit.distance
                : retreatLookAhead;

            if (room >= retreatLookAhead * 0.75f) return from + dir * room;

            if (room > bestRoom)
            {
                bestRoom = room;
                best = from + dir * room;
            }
        }

        return best;
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}
