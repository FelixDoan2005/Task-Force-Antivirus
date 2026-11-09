using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public enum SquadOrder { SeekOut, HoldPosition, FollowPlayer, Surround }
public enum SquadFormation { Wedge, Line, Column, Box }

//a group of soldiers that keep a formation round an anchor and carry out one order together
public class Squad : MonoBehaviour
{
    public static readonly List<Squad> All = new List<Squad>();

    public SquadOrder order = SquadOrder.HoldPosition;
    public SquadFormation formation = SquadFormation.Wedge;

    [Tooltip("Gap between neighbours in the formation.")]
    public float spacing = 2f;
    [Tooltip("Gap between neighbours in the tight grid used when following or holding.")]
    public float gridSpacing = 1.6f;
    [Tooltip("Soldiers on their way to a place go round the player at least this far off, never through them.")]
    public float playerClearance = 2.5f;
    [Tooltip("Soldiers this close to the player, or to the path ahead of them, step aside to let them through.")]
    public float makeWayRadius = 1.6f;
    [Tooltip("How far ahead of a walking player soldiers start stepping aside.")]
    public float makeWayAhead = 2f;
    [Tooltip("In the surround, soldiers only make way while no virus is within this distance of their place.")]
    public float wallMakeWayClear = 15f;
    public int maxMembers = 1000;

    [Tooltip("Soldiers with no squad join this one while it has room.")]
    public bool autoRecruit = true;

    [Tooltip("How far either side of the middle a knight must be before it swaps between sitting and standing.")]
    public float seatingBand = 0.3f;

    [Tooltip("How far past its engage radius a soldier chases before giving up and coming back.")]
    public float leashExtra = 3f;

    [Header("Seek out")]
    public float advanceSpeed = 3f;
    [Tooltip("Fights viruses this close to the squad.")]
    public float seekEngageRadius = 8f;
    [Tooltip("Stops advancing while most of the squad is this far off their places.")]
    public float regroupDistance = 3f;

    [Header("Hold position")]
    public float holdRadius = 7f;

    [Header("Follow")]
    public Transform leader;
    [Tooltip("Gap between the player and the near edge of the pack.")]
    public float followDistance = 5f;
    public float followEngageRadius = 7f;
    [Tooltip("How far the leader moves before the formation shifts after them.")]
    public float followStep = 1f;

    [Header("Surround")]
    public SurroundPlan.Settings surround = new SurroundPlan.Settings();
    [Tooltip("The soldiers inside the walls never stand more than this many rows deep: the square grows instead.")]
    public int maxRingRows = 2;
    [Tooltip("Gap between neighbours on the ring inside the walls.")]
    public float ringSpacing = 1.4f;
    [Tooltip("The walls may spread out to this many times their normal gap to close a bigger square.")]
    public float maxWallStretch = 2f;
    [Tooltip("With no spearmen, the swordsmen stand where the walls would be, this far apart.")]
    public float swordWallSpacing = 1.5f;
    [Tooltip("How fast soldiers run to their places in the surround.")]
    public float surroundRunSpeed = 6f;
    [Tooltip("In the shield wall, the share of swings that are blocks rather than attacks.")]
    [Range(0f, 1f)] public float blockChance = 0.8f;

    readonly List<SoldierAI> members = new List<SoldierAI>();
    //formation place per knight: 0 and up is in the shape, below 0 a captain. in the surround, a place in the plan
    readonly Dictionary<SoldierAI, int> places = new Dictionary<SoldierAI, int>();
    readonly HashSet<SoldierAI> sitters = new HashSet<SoldierAI>();
    readonly List<float> depths = new List<float>();
    float nextSeating;
    float nextUntangle;
    float nextRebalance;
    int wallCount;
    int captainCount;
    int boxCols = 1;
    readonly List<Vector3> placeCache = new List<Vector3>();
    readonly List<SoldierAI> unplaced = new List<SoldierAI>();
    readonly List<SoldierAI> lefts = new List<SoldierAI>();
    readonly List<SoldierAI> rights = new List<SoldierAI>();
    readonly List<SoldierAI> captains = new List<SoldierAI>();
    readonly List<SoldierAI> scratch = new List<SoldierAI>();

    Vector3 anchor;
    Vector3 facing = Vector3.forward;
    bool hasAnchor;

    //seek out
    NavMeshPath path;
    Vector3[] corners = new Vector3[0];
    int corner;
    float nextPath;
    Transform quarry;

    //follow
    Vector3 lastLeader;
    Vector3 reassignFacing;
    Vector3 lastHeading = Vector3.forward;
    Vector3 packCentre;
    bool overtaking;
    float leaderSpeed;
    Vector3 leaderPrev;
    Vector3 leaderVelocity;
    Vector3 velocityPrev;

    //surround
    SurroundPlan plan;
    SurroundPlan lastPlan;
    SoldierAI[] occupants;
    SquadOrder beforeSurround = SquadOrder.FollowPlayer;

    public Vector3 Anchor => anchor;
    public Vector3 Facing => facing;
    public int Count => members.Count;
    public IReadOnlyList<SoldierAI> Members => members;
    public SurroundPlan Plan => plan;
    public bool Surrounding => order == SquadOrder.Surround && plan != null;

    //how fast its places are moving, so members keep pace instead of stop-starting
    public float MoveSpeed { get; private set; }

    void OnEnable()
    {
        All.Add(this);
        SquadCommander.Ensure();
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    //---------------------------------------------------------------- membership

    public static Squad Enlist(SoldierAI soldier)
    {
        foreach (Squad s in All)
            if (s.autoRecruit && s.Count < s.maxMembers)
            {
                s.Add(soldier);
                return s;
            }

        //a new squad picks up the orders of the last one, so everyone faces the same way
        Squad created = new GameObject("Squad").AddComponent<Squad>();
        if (All.Count > 1)
        {
            Squad last = All[All.Count - 2];
            created.order = last.Surrounding ? last.beforeSurround : last.order;
            created.formation = last.formation;
            created.leader = last.leader;
            created.facing = last.facing;
            created.hasAnchor = false;
        }
        created.Add(soldier);
        return created;
    }

    public void Add(SoldierAI soldier)
    {
        if (members.Contains(soldier)) return;

        //a fresh squad (or a seeking one that was wiped out) starts where its first soldier is
        if (members.Count == 0 && (!hasAnchor || order == SquadOrder.SeekOut))
        {
            anchor = soldier.transform.position;
            if (!hasAnchor && All.Count <= 1) facing = Flat(soldier.transform.forward, Vector3.forward);
            hasAnchor = true;
        }

        members.Add(soldier);
        soldier.squad = this;
        if (Surrounding) JoinSurround(soldier);
        else Rebalance();
    }

    public void Remove(SoldierAI soldier)
    {
        sitters.Remove(soldier);
        if (!members.Remove(soldier)) return;
        if (soldier.squad == this) soldier.squad = null;
        if (Surrounding) LeaveSurround(soldier);
        else Rebalance();
    }

    //---------------------------------------------------------------- orders

    public void Command(SquadOrder next, Transform commander)
    {
        foreach (SoldierAI m in members)
            if (m != null) m.FinishFight();

        if (next == SquadOrder.Surround)
        {
            Surround(commander);
            return;
        }

        //already standing in a grid (not the surround): hold just stops it where it is
        bool inGrid = plan == null && Gridded;
        if (next == SquadOrder.HoldPosition && inGrid)
        {
            order = next;
            if (commander != null) leader = commander;
            return;
        }

        if (plan != null) lastPlan = plan;
        plan = null;
        occupants = null;
        order = next;
        if (commander != null) leader = commander;

        //hold and seek start from where the squad stands now
        if (members.Count > 0) anchor = Centroid();
        if (commander != null) facing = Flat(commander.forward, facing);

        //holding, the grid forms where they are but never round the player: crowded round them, it moves off to one side
        if (order == SquadOrder.HoldPosition && leader != null && members.Count > 0)
        {
            float half = Mathf.Sqrt(members.Count) * gridSpacing * 0.5f;
            Vector3 centre = anchor;
            Vector3 away = Flat(centre - leader.position);
            float clear = half * 1.42f + followDistance;
            if (away.magnitude < clear) centre = leader.position + Flat(away, -facing) * clear;
            centre.y = anchor.y;
            anchor = centre + facing * half;
        }

        if (order == SquadOrder.FollowPlayer && leader != null)
        {
            lastLeader = leaderPrev = leader.position;
            //the pack forms on the side of the player it's already on, facing them
            Vector3 back = members.Count > 0 ? Flat(Centroid() - leader.position, -Flat(leader.forward, facing)) : -Flat(leader.forward, facing);
            facing = reassignFacing = -back;
            lastHeading = Flat(leader.forward, facing);
            overtaking = false;
            anchor = leader.position - facing * followDistance;
            packCentre = PackCentre();
        }

        nextPath = 0f;
        Reassign();
    }

    //back to whatever it was doing before the surround
    public void BreakSurround(Transform commander) => Command(beforeSurround, commander);

    public void SetFormation(SquadFormation next)
    {
        foreach (SoldierAI m in members)
            if (m != null) m.FinishFight();
        formation = next;
        if (!Surrounding) Reassign();
    }

    //---------------------------------------------------------------- what to fight

    Vector3 EngageCentre => order == SquadOrder.FollowPlayer && leader != null ? leader.position : anchor;

    float EngageRadius => order switch
    {
        SquadOrder.HoldPosition => holdRadius,
        SquadOrder.FollowPlayer => followEngageRadius,
        _ => seekEngageRadius
    };

    public bool CanEngage(Vector3 point) => Flat(point - EngageCentre).magnitude <= EngageRadius;

    public bool WithinLeash(Vector3 point) => Flat(point - EngageCentre).magnitude <= EngageRadius + leashExtra;

    public float BlockChanceFor(SoldierAI soldier) => InWall(soldier, out _) ? blockChance : 0f;

    //---------------------------------------------------------------- moving the anchor

    void Update()
    {
        for (int i = members.Count - 1; i >= 0; i--)
        {
            SoldierAI m = members[i];
            if (m == null) members.RemoveAt(i);
            else if (!m.isActiveAndEnabled) Remove(m);
        }
        if (members.Count == 0)
        {
            MoveSpeed = 0f;
            return;
        }

        TrackLeader();

        if (Surrounding)
        {
            MoveSpeed = 0f;
            if (Time.time >= nextUntangle) Untangle();
            return;
        }

        switch (order)
        {
            case SquadOrder.SeekOut: Advance(); break;
            case SquadOrder.FollowPlayer: Follow(); break;
            default: MoveSpeed = 0f; break;
        }

        if (Time.time >= nextSeating) UpdateSeating();
        if (Time.time >= nextRebalance) Rebalance();
        if (Time.time >= nextUntangle) Untangle();
    }

    //---------------------------------------------------------------- seating

    public bool ShouldSit(SoldierAI soldier) => sitters.Contains(soldier);

    //the front half of the group sits: split by a line across the squad's facing, through the middle knight
    void UpdateSeating()
    {
        nextSeating = Time.time + 0.25f;

        depths.Clear();
        foreach (SoldierAI m in members)
            if (Idle(m)) depths.Add(Vector3.Dot(Flat(m.transform.position), facing));
            else sitters.Remove(m);
        if (depths.Count == 0) return;

        depths.Sort();
        float middle = depths[depths.Count / 2];
        bool oneRow = depths[depths.Count - 1] - depths[0] < 1f;

        foreach (SoldierAI m in members)
        {
            if (!Idle(m)) continue;
            float depth = Vector3.Dot(Flat(m.transform.position), facing);
            if (oneRow || depth > middle + seatingBand) sitters.Add(m);
            else if (depth < middle - seatingBand) sitters.Remove(m);
        }
    }

    bool Idle(SoldierAI m) => m.Target == null && m.DistanceToPlace < regroupDistance;

    void Advance()
    {
        MoveSpeed = 0f;

        if (Time.time >= nextPath) PlanRoute();
        if (quarry == null) return;

        //close enough that the members will take it on from here
        Vector3 toQuarry = Flat(quarry.position - anchor);
        if (toQuarry.magnitude <= seekEngageRadius * 0.5f)
        {
            facing = Vector3.Slerp(facing, Flat(toQuarry, facing), 5f * Time.deltaTime).normalized;
            return;
        }

        if (Straggling()) return;

        while (corner < corners.Length && Flat(corners[corner] - anchor).magnitude < 0.1f) corner++;
        Vector3 goal = corner < corners.Length ? corners[corner] : quarry.position;

        Vector3 step = Vector3.MoveTowards(anchor, goal, advanceSpeed * Time.deltaTime) - anchor;
        anchor += step;
        if (step.sqrMagnitude > 0f) facing = Vector3.Slerp(facing, Flat(step, facing), 5f * Time.deltaTime).normalized;
        MoveSpeed = advanceSpeed;
    }

    void PlanRoute()
    {
        nextPath = Time.time + 0.5f;

        quarry = null;
        float bestSqr = float.MaxValue;
        foreach (VirusHealthBase v in FindObjectsByType<VirusHealthBase>(FindObjectsSortMode.None))
        {
            if (v.IsDead || VirusAllegiance.IsConverted(v)) continue;
            float sqr = Flat(v.transform.position - anchor).sqrMagnitude;
            if (sqr >= bestSqr) continue;
            bestSqr = sqr;
            quarry = v.transform;
        }
        if (quarry == null) return;

        //walks the anchor along the navmesh so the formation goes round walls, not through them
        var filter = new NavMeshQueryFilter { agentTypeID = members[0].AgentTypeID, areaMask = NavMesh.AllAreas };
        path ??= new NavMeshPath();
        corner = 0;
        corners = NavMesh.SamplePosition(anchor, out NavMeshHit from, 3f, filter) &&
                  NavMesh.SamplePosition(quarry.position, out NavMeshHit to, 4f, filter) &&
                  NavMesh.CalculatePath(from.position, to.position, filter, path)
            ? path.corners
            : new Vector3[0];
    }

    //most of the formation is behind: wait for them
    bool Straggling()
    {
        int inFormation = 0, behind = 0;
        foreach (SoldierAI m in members)
        {
            if (m.Target != null) continue;
            inFormation++;
            if (m.DistanceToPlace > regroupDistance) behind++;
        }
        return behind * 2 > inFormation;
    }

    void Follow()
    {
        if (leader == null)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player == null) return;
            leader = player.transform;
            lastLeader = leaderPrev = leader.position;
        }

        Vector3 p = leader.position;
        float measured = Time.deltaTime > 0f ? Flat(p - leaderPrev).magnitude / Time.deltaTime : 0f;
        leaderPrev = p;
        leaderSpeed = Mathf.Lerp(leaderSpeed, measured, 5f * Time.deltaTime);
        MoveSpeed = leaderSpeed;

        //the way they look once stopped, measured in steps so small shuffles don't turn them
        Vector3 moved = Flat(p - lastLeader);
        if (moved.magnitude >= followStep)
        {
            lastHeading = moved.normalized;
            lastLeader = p;
        }

        float depth = HalfDepth();
        float reach = followDistance + depth;   //player to the pack's middle
        Vector3 toPack = Flat(packCentre - p);
        bool moving = leaderVelocity.magnitude > 1f;
        Vector3 v = Flat(leaderVelocity, lastHeading);

        //walking into the pack: it stands idle and makes way until the player is out the far side
        if (!overtaking && moving && toPack.magnitude < reach + 1f && Vector3.Angle(toPack, v) < 60f) overtaking = true;
        if (overtaking && (-Vector3.Dot(toPack, v) >= depth + 1f || toPack.magnitude > reach + 2f)) overtaking = false;

        packCentre.y = p.y;
        if (!overtaking)
        {
            //the pack stays on whichever side of the player it's on, front row facing them, so it never closes round them.
            //once the player's through, it waits until they're the usual gap away, then follows
            Vector3 back = Flat(toPack, -facing);
            bool closingIn = moving && toPack.magnitude < reach && Vector3.Dot(toPack, v) > 0f;
            if (toPack.magnitude > reach || closingIn) packCentre = Flat(p) + Vector3.up * packCentre.y + back * reach;
            Turn(-back);
        }

        packCentre.y = p.y;
        anchor = packCentre + facing * depth;
    }

    //turns the grid, reshuffling who stands where only once it's turned a fair way
    void Turn(Vector3 next)
    {
        facing = next;
        anchor = packCentre + facing * HalfDepth();
        if (Vector3.Angle(facing, reassignFacing) > 30f)
        {
            reassignFacing = facing;
            Reassign();
        }
    }

    float HalfDepth() => Mathf.Max(0, wallCount / Mathf.Max(1, boxCols) - 1) * Gap * 0.5f;

    //---------------------------------------------------------------- places

    public Vector3 PlaceFor(SoldierAI soldier)
    {
        //just back from a fight: it gets a place straight away
        if (!Surrounding && !places.ContainsKey(soldier)) Rebalance();
        Vector3 place = PlaceAt(soldier);
        return WallHolds(place) ? place : MakeWay(place);
    }

    Vector3 PlaceAt(SoldierAI soldier)
    {
        if (!places.TryGetValue(soldier, out int index)) return anchor;
        if (Surrounding) return plan.Slots[index].position;
        return anchor + Quaternion.LookRotation(facing) * (index >= 0 ? Offset(index) : CaptainOffset(-1 - index));
    }

    //the way it faces once it's there: the squad's way, or straight out from the surround
    public Vector3 FacingFor(SoldierAI soldier)
    {
        if (MakingWay(soldier, out Vector3 look)) return look;
        //following, they look the way the player last went
        if (order == SquadOrder.FollowPlayer) return lastHeading;
        return Surrounding && places.TryGetValue(soldier, out int index) ? plan.Slots[index].facing : facing;
    }

    //stepping aside for the player, it keeps its eyes on them
    public bool MakingWay(SoldierAI soldier, out Vector3 look)
    {
        look = Vector3.zero;
        if (leader == null || !places.ContainsKey(soldier)) return false;
        Vector3 raw = PlaceAt(soldier);
        if (WallHolds(raw)) return false;
        if ((MakeWay(raw) - raw).sqrMagnitude < 0.0001f) return false;
        look = Flat(leader.position - soldier.transform.position, facing);
        return true;
    }

    public bool IsCaptain(SoldierAI soldier) =>
        places.TryGetValue(soldier, out int index) && (Surrounding ? plan.Slots[index].row == SurroundPlan.Row.Captain : index < 0);

    //(x right, z forward) from the anchor, with the front at the anchor. places only ever get added at the
    //back or sides, so someone joining doesn't shift everyone else and every gap stays one spacing
    Vector3 Offset(int i)
    {
        float s = Gap;

        switch (Shape)
        {
            case SquadFormation.Line:   //out from the middle, alternating right and left
                return new Vector3((i % 2 == 1 ? -1f : 1f) * ((i + 1) / 2) * s, 0f, 0f);
            case SquadFormation.Column:
                return new Vector3(0f, 0f, -i * s);
            case SquadFormation.Box:    //full rows only; whoever's left over is a captain
                return new Vector3((i % boxCols - (boxCols - 1) * 0.5f) * s, 0f, -(i / boxCols) * s);
            default:   //wedge: tip first, then alternating left and right down the arms
                int rank = (i + 1) / 2;
                float side = i % 2 == 1 ? -1f : 1f;
                return new Vector3(side * rank * s * 0.8f, 0f, -rank * s * 0.6f);
        }
    }

    //captains stand in the middle: inside the wedge behind its tip, behind the middle of a line, behind a box
    Vector3 CaptainOffset(int j)
    {
        float s = Gap;
        float across = (j - (captainCount - 1) * 0.5f) * s;

        switch (Shape)
        {
            case SquadFormation.Wedge: return new Vector3(across, 0f, -1.2f * s);
            case SquadFormation.Box: return new Vector3(across, 0f, -(wallCount / boxCols) * s);
            case SquadFormation.Column: return new Vector3(s, 0f, -j * s);
            default: return new Vector3(across, 0f, -s);
        }
    }

    //how many of n knights make up the shape itself; the rest are captains. wedge and line keep both sides
    //even, a box keeps only full rows
    int ShapeCount(int n)
    {
        switch (Shape)
        {
            case SquadFormation.Wedge:
            case SquadFormation.Line:
                return n % 2 == 1 ? n : Mathf.Max(0, n - 1);
            case SquadFormation.Box:
                boxCols = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(n)));
                return n / boxCols * boxCols;
            default:
                return n;
        }
    }

    //each place, front first, goes to the nearest knight still without one, so nobody crosses the formation
    void Reassign()
    {
        if (Surrounding) return;

        places.Clear();
        var free = new List<SoldierAI>();
        var units = new List<SoldierAI>();
        foreach (SoldierAI m in members)
            if (m != null && m.Target == null) (m.captain ? units : free).Add(m);

        //captain units are always captains; the shape is made from everyone else
        wallCount = ShapeCount(free.Count);
        captainCount = free.Count - wallCount + units.Count;

        Quaternion rot = Quaternion.LookRotation(facing);
        for (int i = 0; i < wallCount + captainCount; i++)
        {
            int index = i < wallCount ? i : -1 - (i - wallCount);
            Vector3 place = anchor + rot * (index >= 0 ? Offset(index) : CaptainOffset(-1 - index));
            List<SoldierAI> from = index < 0 && units.Any(u => !places.ContainsKey(u)) ? units : free;

            SoldierAI best = null;
            float bestSqr = float.MaxValue;
            foreach (SoldierAI m in from)
            {
                if (places.ContainsKey(m)) continue;
                float sqr = Flat(m.transform.position - place).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = m;
                }
            }
            if (best != null) places[best] = index;
        }
    }

    //---------------------------------------------------------------- symmetry

    //wedge and line places: 0 is the middle, odd numbers go down the left, even down the right
    bool Sided => Shape == SquadFormation.Wedge || Shape == SquadFormation.Line;

    //every order but the surround uses the tight grid (seeking out, it advances on the viruses in it)
    public bool Gridded => order != SquadOrder.Surround;
    SquadFormation Shape => Gridded ? SquadFormation.Box : formation;
    float Gap => Gridded ? gridSpacing : spacing;

    //middle of the formation's shape; the front row is at the anchor
    Vector3 PackCentre() => anchor - facing * HalfDepth();

    float nextLeaderSearch;

    void TrackLeader()
    {
        //no order given yet (the default hold): the player still leads, so make way and routing work as if they'd pressed it
        if (leader == null && Time.time >= nextLeaderSearch)
        {
            nextLeaderSearch = Time.time + 1f;
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null) leader = player.transform;
        }
        if (leader == null || Time.deltaTime <= 0f) return;
        Vector3 p = leader.position;
        if (velocityPrev == Vector3.zero) velocityPrev = p;
        leaderVelocity = Vector3.Lerp(leaderVelocity, Flat(p - velocityPrev) / Time.deltaTime, 8f * Time.deltaTime);
        velocityPrev = p;
    }

    //a place in the player's way moves aside: off their path while they walk, out of their circle while they stand
    Vector3 MakeWay(Vector3 p)
    {
        if (leader == null) return p;
        Vector3 c = leader.position;
        Vector3 d = Flat(p - c);
        float r = makeWayRadius;

        if (leaderVelocity.magnitude < 0.5f)
        {
            if (d.magnitude >= r) return p;
            Vector3 q = c + Flat(d, Vector3.right) * r;
            q.y = p.y;
            return q;
        }

        Vector3 v = leaderVelocity.normalized;
        float along = Vector3.Dot(d, v);
        Vector3 lateral = d - v * along;
        //the faster they come, the earlier it moves
        if (along < -r || along > r + makeWayAhead + leaderVelocity.magnitude * 0.5f || lateral.magnitude >= r) return p;
        Vector3 side = lateral.sqrMagnitude > 0.0001f ? lateral.normalized : Vector3.Cross(Vector3.up, v);
        return p + side * (r - lateral.magnitude);
    }

    //in the surround, a place with a virus nearby holds firm rather than opening for the player
    bool WallHolds(Vector3 place)
    {
        if (!Surrounding) return false;
        float clear = wallMakeWayClear * wallMakeWayClear;
        foreach (VirusHealthBase v in LiveViruses())
            if (v != null && !v.IsDead && !VirusAllegiance.IsConverted(v) && Flat(v.transform.position - place).sqrMagnitude < clear)
                return true;
        return false;
    }

    //every virus in the scene, looked up once a frame for all squads
    static VirusHealthBase[] virusCache = new VirusHealthBase[0];
    static int virusFrame = -1;
    static VirusHealthBase[] LiveViruses()
    {
        if (virusFrame != Time.frameCount)
        {
            virusFrame = Time.frameCount;
            virusCache = FindObjectsByType<VirusHealthBase>(FindObjectsSortMode.None);
        }
        return virusCache;
    }

    //on the way to a place: a point to head for first so it goes round the player, or the place itself
    public Vector3 RouteAround(Vector3 from, Vector3 to)
    {
        if (leader == null) return to;
        Vector3 c = leader.position;
        float r = playerClearance;
        Vector3 a = Flat(from - c), b = Flat(to - c);
        if (a.magnitude < r || b.magnitude < r) return to;

        Vector3 ab = b - a;
        float t = Mathf.Clamp01(-Vector3.Dot(a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
        Vector3 q = a + ab * t;
        if (q.magnitude >= r) return to;

        Vector3 dir = q.sqrMagnitude > 0.0001f ? q.normalized : Vector3.Cross(Vector3.up, ab).normalized;
        Vector3 w = c + dir * r * 1.3f;
        w.y = from.y;
        return w;
    }
    static int Side(int i) => i == 0 ? 0 : (i % 2 == 1 ? -1 : 1);

    //only knights standing in formation hold places, always 0..k-1 with no gaps, so both sides stay even.
    //an odd one out stands in the middle as a captain. moves as few knights as it can to get there
    void Rebalance()
    {
        if (Surrounding) return;
        nextRebalance = Time.time + 0.5f;

        //fighters give their places up
        scratch.Clear();
        foreach (SoldierAI m in places.Keys)
            if (m == null || m.Target != null || m.squad != this || !members.Contains(m)) scratch.Add(m);
        foreach (SoldierAI m in scratch) places.Remove(m);

        //captains go back in the pool first, so they're the ones who fill any gap
        unplaced.Clear();
        foreach (KeyValuePair<SoldierAI, int> kv in places)
            if (kv.Value < 0) unplaced.Add(kv.Key);
        unplaced.Sort((a, b) => places[b].CompareTo(places[a]));
        foreach (SoldierAI m in unplaced) places.Remove(m);
        foreach (SoldierAI m in members)
            if (m != null && m.Target == null && !places.ContainsKey(m) && !unplaced.Contains(m)) unplaced.Add(m);

        //captain units stay out of the shape and always stand in the middle
        var units = members.Where(m => m != null && m.captain && m.Target == null).ToList();
        foreach (SoldierAI u in units)
        {
            places.Remove(u);
            unplaced.Remove(u);
        }

        captains.Clear();

        if (!Sided)
        {
            //everyone keeps their order, closing up any gaps; newcomers on the end
            scratch.Clear();
            scratch.AddRange(places.Keys);
            scratch.Sort((a, b) => places[a].CompareTo(places[b]));
            scratch.AddRange(unplaced);

            wallCount = ShapeCount(scratch.Count);
            places.Clear();
            for (int i = 0; i < scratch.Count; i++)
                if (i < wallCount) places[scratch[i]] = i;
                else captains.Add(scratch[i]);
        }
        else
        {
            SoldierAI middle = null;
            lefts.Clear();
            rights.Clear();
            foreach (KeyValuePair<SoldierAI, int> kv in places)
            {
                int side = Side(kv.Value);
                if (side == 0) middle = kv.Key;
                else (side < 0 ? lefts : rights).Add(kv.Key);
            }
            //sides keep their order front to back, closing up any gaps
            lefts.Sort((a, b) => places[a].CompareTo(places[b]));
            rights.Sort((a, b) => places[a].CompareTo(places[b]));

            //the middle's empty: the front knight of the longer side steps in, or else a newcomer
            if (middle == null)
            {
                List<SoldierAI> longer = lefts.Count >= rights.Count ? lefts : rights;
                if (longer.Count > 0)
                {
                    middle = longer[0];
                    longer.RemoveAt(0);
                }
                else if (unplaced.Count > 0)
                {
                    middle = unplaced[0];
                    unplaced.RemoveAt(0);
                }
            }

            //newcomers onto the end of the shorter side, or the nearer end when they're level
            foreach (SoldierAI m in unplaced)
            {
                List<SoldierAI> side;
                if (lefts.Count != rights.Count) side = lefts.Count < rights.Count ? lefts : rights;
                else
                {
                    Vector3 leftEnd = anchor + Quaternion.LookRotation(facing) * Offset(2 * (lefts.Count + 1) - 1);
                    Vector3 rightEnd = anchor + Quaternion.LookRotation(facing) * Offset(2 * (rights.Count + 1));
                    Vector3 p = m.transform.position;
                    side = Flat(leftEnd - p).sqrMagnitude <= Flat(rightEnd - p).sqrMagnitude ? lefts : rights;
                }
                side.Add(m);
            }

            //still lopsided (someone left to fight): the back knight of the longer side crosses over
            while (Mathf.Abs(lefts.Count - rights.Count) > 1)
            {
                List<SoldierAI> from = lefts.Count > rights.Count ? lefts : rights;
                List<SoldierAI> to = from == lefts ? rights : lefts;
                to.Add(from[from.Count - 1]);
                from.RemoveAt(from.Count - 1);
            }

            //one side still a knight longer: he stands in the middle as a captain instead
            if (lefts.Count != rights.Count)
            {
                List<SoldierAI> longer = lefts.Count > rights.Count ? lefts : rights;
                captains.Add(longer[longer.Count - 1]);
                longer.RemoveAt(longer.Count - 1);
            }

            places.Clear();
            if (middle != null) places[middle] = 0;
            for (int j = 0; j < lefts.Count; j++) places[lefts[j]] = 2 * (j + 1) - 1;
            for (int j = 0; j < rights.Count; j++) places[rights[j]] = 2 * (j + 1);
            wallCount = places.Count;
        }

        captains.InsertRange(0, units);
        captainCount = captains.Count;
        for (int j = 0; j < captains.Count; j++) places[captains[j]] = -1 - j;
    }

    //swaps places between two knights when that shortens their walks, which uncrosses their routes
    //so they don't have to get past each other
    void Untangle()
    {
        nextUntangle = Time.time + 1f;

        int n = members.Count;
        if (!Surrounding)
        {
            Quaternion rot = Quaternion.LookRotation(facing);
            placeCache.Clear();
            for (int i = 0; i < wallCount; i++) placeCache.Add(anchor + rot * Offset(i));
        }

        for (int a = 0; a < n; a++)
        for (int b = a + 1; b < n; b++)
        {
            SoldierAI A = members[a], B = members[b];
            if (!places.TryGetValue(A, out int pa) || !places.TryGetValue(B, out int pb)) continue;

            Vector3 placeA, placeB;
            if (Surrounding)
            {
                //captains keep their own spots, and spears and swords keep to their own walls
                if (plan.Slots[pa].row == SurroundPlan.Row.Captain || plan.Slots[pb].row == SurroundPlan.Row.Captain) continue;
                if (plan.Slots[pa].row == SurroundPlan.Row.Inside || plan.Slots[pb].row == SurroundPlan.Row.Inside) continue;
                if (A.spearman != B.spearman) continue;
                placeA = plan.Slots[pa].position;
                placeB = plan.Slots[pb].position;
            }
            else
            {
                if (A.Target != null || B.Target != null) continue;
                if (pa < 0 || pb < 0 || pa >= wallCount || pb >= wallCount) continue;
                placeA = placeCache[pa];
                placeB = placeCache[pb];
            }

            Vector3 posA = A.transform.position, posB = B.transform.position;
            float now = Flat(placeA - posA).magnitude + Flat(placeB - posB).magnitude;
            float swapped = Flat(placeB - posA).magnitude + Flat(placeA - posB).magnitude;
            if (swapped >= now - 0.5f) continue;

            places[A] = pb;
            places[B] = pa;
            if (Surrounding)
            {
                occupants[pa] = B;
                occupants[pb] = A;
            }
        }
    }

    //---------------------------------------------------------------- surround

    //a closed square of shield walls round whoever gave the order, fixed where they stood: they can walk
    //about inside it until it's broken up
    void Surround(Transform commander)
    {
        if (order != SquadOrder.Surround) beforeSurround = order;
        order = SquadOrder.Surround;
        if (commander != null) leader = commander;

        Transform around = commander != null ? commander : leader;
        members.RemoveAll(m => m == null);
        anchor = around != null ? around.position : Centroid();
        if (around != null) facing = Flat(around.forward, facing);
        MoveSpeed = 0f;
        sitters.Clear();

        //the sums first, then everyone gets a place before anyone moves. after losses the inner walls go first
        //spearmen with a captured wall pose pair their two outer walls the way the posed pair stands
        WallPoseData pose = members.Where(m => m.spearman).Select(m => m.GetComponentInChildren<WallPoseApplier>()).FirstOrDefault(a => a != null && a.pose != null)?.pose;
        surround.pairBehind = pose != null ? pose.pairBehind : 0f;
        surround.pairSide = pose != null ? pose.pairSide : 0f;
        surround.tightStep = pose != null ? 0 : 1;
        surround.spacing = pose != null && pose.pairGap > 0f ? pose.pairGap : 1.1f;
        surround.maxLayers = pose != null ? 2 : 0;
        if (baseInnerDistance <= 0f) baseInnerDistance = surround.innerDistance;
        surround.innerDistance = baseInnerDistance;
        surround.ringSpacing = ringSpacing;

        //the walls are spearmen; swordsmen stand in their own ring a soldier's length inside them. too few
        //spearmen for even one wall and the roles swap: the swordsmen make the walls (standing, as they do behind
        //the spears) and the spearmen take the ring inside
        int captainUnits = members.Count(m => m.captain);
        int spearCount = members.Count(m => !m.captain && m.spearman);
        int swordCount = members.Count(m => !m.captain && !m.spearman);
        swapped = swordCount > spearCount && spearCount < 4 * SurroundPlan.MinSide(surround);
        int ring = swapped ? spearCount : swordCount;
        if (swapped)
        {
            surround.spacing = swordWallSpacing;
            surround.tightStep = 2;
            surround.maxLayers = 0;
            surround.pairBehind = 0f;
        }
        FitSquare(members.Count - captainUnits - ring, ring);

        //the last walls are only reused while the same kind of soldier holds them in the same size square
        SurroundPlan previous = plan ?? lastPlan;
        if (previous != null && (swapped != planSwapped || !Mathf.Approximately(previous.Used.spacing, surround.spacing) ||
                                 !Mathf.Approximately(previous.Used.innerDistance, surround.innerDistance))) previous = null;
        plan = SurroundPlan.Rebuild(members.Count - captainUnits - ring, previous, surround.Copy(), anchor, facing, ring);
        planSwapped = swapped;
        for (int i = 0; i < captainUnits; i++) plan.AddCaptain();

        //places off the floor can't be stood in: whoever would have taken one stands inside as a captain instead
        int offFloor = OffFloor().Count;
        for (int i = 0; i < offFloor; i++) plan.AddCaptain();
        blocked = OffFloor();
        if (offFloor > 0) Debug.Log("Surround: " + offFloor + " places are off the floor; those knights stand inside as captains", this);
        Debug.Log(plan.Describe() + (captainUnits > 0 ? " (" + captainUnits + " of the captains are captain units)" : ""), this);

        places.Clear();
        occupants = new SoldierAI[plan.Slots.Count];
        FillSurround();
    }

    HashSet<int> blocked = new HashSet<int>();

    //wall and swordsman places with no floor under them
    HashSet<int> OffFloor()
    {
        var off = new HashSet<int>();
        if (members.Count == 0) return off;
        var filter = new NavMeshQueryFilter { agentTypeID = members[0].AgentTypeID, areaMask = NavMesh.AllAreas };
        for (int i = 0; i < plan.Slots.Count; i++)
        {
            if (plan.Slots[i].row == SurroundPlan.Row.Captain) continue;
            Vector3 p = plan.Slots[i].position;
            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 0.6f, filter)) off.Add(i);
        }
        return off;
    }

    //outside wall first, from the knights furthest out, then inwards; the ones nearest the middle are captains
    void FillSurround()
    {
        var pool = new List<SoldierAI>(members);
        Vector3 c = anchor;
        pool.Sort((a, b) => Flat(b.transform.position - c).sqrMagnitude.CompareTo(Flat(a.transform.position - c).sqrMagnitude));

        //walls first, then the swordsmen's ring, then the captains (who also take anyone left over)
        var order = new List<int>();
        for (int layer = 0; layer < plan.Layers; layer++) order.Add(layer);
        order.Add(-2);
        order.Add(-1);
        foreach (int wanted in order)
        {
            var slots = new List<int>();
            for (int i = 0; i < plan.Slots.Count; i++)
                if (plan.Slots[i].layer == wanted && !blocked.Contains(i)) slots.Add(i);

            List<SoldierAI> chosen;
            if (wanted == -1)
            {
                chosen = pool.Where(k => k.captain).Take(slots.Count).ToList();
                chosen.AddRange(pool.Where(k => !k.captain).Take(slots.Count - chosen.Count));
            }
            else if (wanted == -2) chosen = pool.Where(k => !k.captain && !WallUnit(k)).Take(slots.Count).ToList();
            else chosen = pool.Where(k => !k.captain && WallUnit(k)).Take(slots.Count).ToList();
            foreach (SoldierAI k in chosen) pool.Remove(k);
            Match(chosen, slots);
        }
    }

    //round a wall the knights keep the order they're already in, so nobody crosses anyone else's path;
    //tries every turn of the ring and keeps the one with the least walking
    void Match(List<SoldierAI> knights, List<int> slots)
    {
        if (knights.Count == 0 || slots.Count == 0) return;

        Vector3 c = anchor;
        float Angle(Vector3 p) => Mathf.Atan2(p.x - c.x, p.z - c.z);
        knights.Sort((a, b) => Angle(a.transform.position).CompareTo(Angle(b.transform.position)));
        slots.Sort((a, b) => Angle(plan.Slots[a].position).CompareTo(Angle(plan.Slots[b].position)));

        int n = slots.Count, k = knights.Count;
        int bestShift = 0;
        float bestCost = float.MaxValue;
        for (int shift = 0; shift < n; shift++)
        {
            float cost = 0f;
            for (int i = 0; i < k; i++)
                cost += Flat(plan.Slots[slots[(i * n / k + shift) % n]].position - knights[i].transform.position).magnitude;
            if (cost < bestCost)
            {
                bestCost = cost;
                bestShift = shift;
            }
        }

        for (int i = 0; i < k; i++) Seat(knights[i], slots[(i * n / k + bestShift) % n]);
    }

    void Seat(SoldierAI knight, int slot)
    {
        occupants[slot] = knight;
        places[knight] = slot;
    }

    //someone joins while the square's up: the first gap, outside wall first, else one more captain
    void JoinSurround(SoldierAI soldier)
    {
        for (int i = 0; i < occupants.Length; i++)
            if (occupants[i] == null && !blocked.Contains(i) && Fits(soldier, plan.Slots[i].row))
            {
                Seat(soldier, i);
                return;
            }

        //a swordsman gets one more place on the swordsmen's ring, anyone else one more captain's spot
        if (!soldier.captain && !WallUnit(soldier))
        {
            int last = plan.AddSword();
            System.Array.Resize(ref occupants, plan.Slots.Count);
            blocked = OffFloor();
            Seat(soldier, last);
            return;
        }

        int slot = plan.AddCaptain();
        var grown = new List<SoldierAI>(occupants);
        grown.Insert(slot, null);
        occupants = grown.ToArray();
        foreach (SoldierAI k in places.Keys.ToList())
            if (places[k] >= slot) places[k]++;
        blocked = OffFloor();
        Seat(soldier, slot);
    }

    //swordsmen outnumber the spearmen: the swordsmen hold the walls and the spearmen the ring inside
    bool swapped, planSwapped;
    float baseInnerDistance;

    //the square grows until the ring inside the walls fits in maxRingRows rows; the walls then spread out (up to
    //maxWallStretch) so the soldiers they have still close it, two walls deep
    void FitSquare(int wallKnights, int ringKnights)
    {
        if (ringKnights <= 0 || wallKnights <= 0) return;

        //the outermost ring's half-size that holds them all in the rows allowed
        float rs = surround.ringSpacing > 0f ? surround.ringSpacing : surround.spacing;
        float half = surround.captainMinRing + surround.captainInset;
        for (int guard = 0; guard < 500; guard++, half += 0.25f)
        {
            int room = 0;
            for (int row = 0; row < maxRingRows; row++) room += Mathf.FloorToInt(8f * Mathf.Max(0f, half - row * surround.swordGap) / rs);
            if (room >= ringKnights) break;
        }

        float inner = half + surround.swordGap + surround.pairBehind;
        if (inner <= surround.innerDistance) return;
        surround.innerDistance = inner;

        //two walls round that square at their normal gap need this many knights; short of that they spread out
        int side = SurroundPlan.MinSide(surround);
        if (SurroundPlan.KnightsFor(2, SurroundPlan.NeededSide(2, surround), surround) <= wallKnights) return;
        int fits = side;
        while (fits > 1 && SurroundPlan.KnightsFor(2, fits + surround.growthPerWall, surround) > wallKnights) fits--;
        float stretched = 2f * inner / fits;
        float most = surround.spacing * maxWallStretch;
        surround.spacing = Mathf.Min(stretched, most);
        if (stretched > most) surround.innerDistance = Mathf.Max(baseInnerDistance, surround.spacing * fits * 0.5f);
    }
    bool WallUnit(SoldierAI k) => swapped ? !k.spearman : k.spearman;

    //captain units only to captain spots, the ring's soldiers only to the ring, the walls' soldiers to the walls
    //or a captain spot
    bool Fits(SoldierAI soldier, SurroundPlan.Row row)
    {
        if (soldier.captain) return row == SurroundPlan.Row.Captain;
        if (!WallUnit(soldier)) return row == SurroundPlan.Row.Inside;
        return row != SurroundPlan.Row.Inside;
    }

    //a knight falls: the nearest one in the wall behind steps up into his place (and takes his row), then
    //the one behind him does the same, so the gap ends up in the innermost wall
    void LeaveSurround(SoldierAI soldier)
    {
        if (!places.TryGetValue(soldier, out int slot)) return;
        places.Remove(soldier);
        occupants[slot] = null;

        for (int layer = plan.Slots[slot].layer; layer >= 0 && layer + 1 < plan.Layers; layer++)
        {
            int behind = NearestTaken(layer + 1, plan.Slots[slot].position);
            if (behind < 0) break;

            SoldierAI mover = occupants[behind];
            occupants[behind] = null;
            Seat(mover, slot);
            slot = behind;
        }
    }

    int NearestTaken(int layer, Vector3 near)
    {
        int best = -1;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < plan.Slots.Count; i++)
        {
            if (plan.Slots[i].layer != layer || occupants[i] == null) continue;
            float sqr = (plan.Slots[i].position - near).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = i;
            }
        }
        return best;
    }

    //its row in the shield wall: 0 kneeling at the front, 1 standing behind. false for anyone not in a wall
    public bool InWall(SoldierAI soldier, out float row)
    {
        row = 1f;
        if (!Surrounding || !places.TryGetValue(soldier, out int index)) return false;

        SurroundPlan.Row r = plan.Slots[index].row;
        if (r == SurroundPlan.Row.Captain || r == SurroundPlan.Row.Inside) return false;
        //swordsmen holding the walls stand as they do behind the spears, not in the wall pose
        if (!soldier.spearman) return false;
        row = r == SurroundPlan.Row.Front ? 0f : 1f;
        return true;
    }

    //----------------------------------------------------------------

    Vector3 Centroid()
    {
        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (SoldierAI m in members)
        {
            if (m == null) continue;
            sum += m.transform.position;
            n++;
        }
        return n > 0 ? sum / n : anchor;
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    static Vector3 Flat(Vector3 v, Vector3 fallback)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : fallback;
    }

    //the surround's walls and captains, drawn whether or not the squad's selected
    void OnDrawGizmos()
    {
        if (!Surrounding) return;

        for (int layer = 0; layer < plan.Layers; layer++)
        {
            Gizmos.color = layer == 0 ? Color.red : Color.yellow;
            float h = plan.HalfSize(layer);
            Vector3 c = plan.Centre;
            Quaternion f = plan.Frame;
            Vector3 a = c + f * new Vector3(-h, 0f, h), b = c + f * new Vector3(h, 0f, h);
            Vector3 d = c + f * new Vector3(-h, 0f, -h), e = c + f * new Vector3(h, 0f, -h);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, e);
            Gizmos.DrawLine(e, d);
            Gizmos.DrawLine(d, a);
        }

        foreach (SurroundPlan.Slot slot in plan.Slots)
        {
            if (slot.row == SurroundPlan.Row.Captain) Gizmos.color = Color.magenta;
            else if (slot.row == SurroundPlan.Row.Inside) Gizmos.color = Color.green;
            else continue;
            Gizmos.DrawWireSphere(slot.position, 0.35f);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (Surrounding) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(EngageCentre, EngageRadius);
        Gizmos.color = Color.yellow;
        foreach (SoldierAI m in members)
            if (m != null) Gizmos.DrawWireSphere(PlaceAt(m), 0.3f);
    }
}
