using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

//a friendly soldier: fights viruses its squad's order allows, otherwise keeps its place in the formation.
//with no squad it hunts the nearest virus anywhere
[RequireComponent(typeof(NpcNavigator))]
[RequireComponent(typeof(SoldierHealth))]
public class SoldierAI : MonoBehaviour
{
    public float speed = 3.5f;
    [Tooltip("Fastest it ever runs keeping up with a moving squad.")]
    public float maxSpeed = 6.5f;
    public float turnSpeed = 540f;

    [Tooltip("Gap between its body and the virus's at which it stops and swings.")]
    public float attackRange = 1.2f;

    [Tooltip("Extra reach against a Tank virus, whose legs keep soldiers further off.")]
    public float tankRangeBonus = 1f;

    public float damagePerHit = 15f;
    [Tooltip("Keep one swing no shorter than the longest attack clip (1.73s), or the next swing cuts it off.")]
    public float hitsPerSecond = 0.55f;

    [Tooltip("Seconds between looking for the nearest virus. A scene-wide search, so not every frame.")]
    public float retargetInterval = 0.5f;

    [Tooltip("Only switches to a new virus if it's this much closer than the one it's fighting.")]
    public float switchMargin = 1.5f;

    [Tooltip("How many different swings the attack blend tree has.")]
    public int swingVariants = 6;

    [Tooltip("Swing number of the first block in the attack blend tree; the blocks follow on from it.")]
    public int firstBlockSwing = 6;
    public int blockSwings = 2;

    [Tooltip("Joins a squad (or starts one) when it spawns.")]
    public bool joinSquad = true;

    [Tooltip("Carries a spear: in the surround it stands in the wall behind the kneeling front row.")]
    public bool spearman;

    [Tooltip("A captain: always takes the captain spots in the middle of a formation, never a place in the walls.")]
    public bool captain;

    [Tooltip("Leave empty to join one automatically.")]
    public Squad squad;

    NpcNavigator navigator;
    Collider ownCollider;

    [Tooltip("How close to its place in the ring counts as there.")]
    public float slotTolerance = 0.6f;

    VirusHealthBase target;
    float nextSearch;
    Collider[] targetColliders;
    Vector3 slot;
    bool hasSlot;
    float nextSlot;
    float nextHit;
    int swing;

    Vector3 placeRaw;
    Vector3 place;
    bool hasPlace;
    bool returning;
    bool passThrough;
    bool makingWay;
    bool finishing;
    CharacterController letThrough;
    bool placeMoved;
    float closest;
    float lastProgress;

    public VirusHealthBase Target => target;
    public int AgentTypeID => navigator.AgentTypeID;

    //standing in its place in the formation, not on its way there
    public bool AtPlace => squad != null && !returning;

    //how far it is from its place in the formation
    public float DistanceToPlace { get; private set; }

    //the model swings when this fires
    public event System.Action<int> OnAttack;

    void Awake()
    {
        navigator = GetComponent<NpcNavigator>();
        ownCollider = GetComponent<Collider>();
    }

    void OnEnable()
    {
        if (squad != null) squad.Add(this);
        else if (joinSquad) Squad.Enlist(this);
    }

    void OnDisable()
    {
        LetThrough(null);
        Engage(null);
        if (squad != null) squad.Remove(this);
    }

    //a new order mid-fight: it sees its current enemy off (or dies trying) before it goes
    public void FinishFight()
    {
        if (target != null) finishing = true;
    }

    void Update()
    {
        bool finishingFight = finishing && target != null && !target.IsDead && !VirusAllegiance.IsConverted(target);
        finishing = finishingFight;

        if (squad != null && squad.Surrounding && !finishingFight)
        {
            HoldTheLine();
            return;
        }

        if (!finishingFight && (target == null || target.IsDead || VirusAllegiance.IsConverted(target) || Time.time >= nextSearch ||
            (squad != null && !squad.WithinLeash(target.transform.position))))
            FindTarget();

        if (target == null)
        {
            if (squad != null) KeepPlace();
            return;
        }
        DistanceToPlace = 0f;
        StartReturning();
        navigator.PassThrough = false;
        navigator.FaceOverride = Vector3.zero;
        LetThrough(null);

        float gap = GapToTarget();
        float reach = target is TankVirusHealth ? attackRange + tankRangeBonus : attackRange;

        //each attacker takes its own place in a ring round the target, so they spread out instead
        //of piling up on whichever side they arrived from
        if (Time.time >= nextSlot) PickSlot();
        Vector3 toSlot = slot - transform.position;
        toSlot.y = 0f;
        bool atSlot = !hasSlot || toSlot.magnitude <= slotTolerance;

        if (!atSlot)
        {
            navigator.MoveTo(slot, speed, turnSpeed);
            return;
        }

        //no place to stand (a wall's in the way): straight in instead
        if (gap > reach && !hasSlot)
        {
            navigator.MoveTo(target.transform, speed, turnSpeed);
            return;
        }

        Vector3 dir = target.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir),
                                                          turnSpeed * Time.deltaTime);

        //in its place but the ring had to widen to fit everyone: it waits its turn there
        if (gap > reach || Time.time < nextHit) return;
        nextHit = Time.time + 1f / Mathf.Max(0.01f, hitsPerSecond);

        target.TakeDamage(damagePerHit);
        OnAttack?.Invoke(NextSwing());
    }

    //a random swing each time, never the same one twice in a row
    int NextSwing()
    {
        int variants = Mathf.Max(1, swingVariants);
        int next = Random.Range(0, variants);
        if (variants > 1 && next == swing) next = (next + 1 + Random.Range(0, variants - 1)) % variants;
        return swing = next;
    }

    //in the surround it never leaves its place: it takes on whatever comes within reach, and in the wall
    //that's mostly with its shield
    void HoldTheLine()
    {
        if (target == null || target.IsDead || VirusAllegiance.IsConverted(target) || Time.time >= nextSearch)
            FindTargetInReach();

        KeepPlace();
        if (target == null || returning) return;

        float reach = target is TankVirusHealth ? attackRange + tankRangeBonus : attackRange;
        if (GapToTarget() > reach || Time.time < nextHit) return;
        nextHit = Time.time + 1f / Mathf.Max(0.01f, hitsPerSecond);

        if (Random.value < squad.BlockChanceFor(this))
        {
            OnAttack?.Invoke(firstBlockSwing + Random.Range(0, Mathf.Max(1, blockSwings)));
            return;
        }

        target.TakeDamage(damagePerHit);
        OnAttack?.Invoke(NextSwing());
    }

    //the nearest virus it can reach from where it stands, or none
    void FindTargetInReach()
    {
        nextSearch = Time.time + retargetInterval;

        float reach = attackRange + tankRangeBonus;
        VirusHealthBase best = null;
        float bestSqr = (reach + 6f) * (reach + 6f);
        Vector3 from = transform.position;

        foreach (VirusHealthBase v in Viruses())
        {
            if (v == null || v.IsDead || VirusAllegiance.IsConverted(v)) continue;
            Vector3 to = v.transform.position - from;
            to.y = 0f;
            if (to.sqrMagnitude >= bestSqr) continue;
            bestSqr = to.sqrMagnitude;
            best = v;
        }

        Engage(best);
        if (target != null && GapToTarget() > reach + 0.5f) Engage(null);
    }

    //every virus in the scene, looked up once a frame however many soldiers ask
    static VirusHealthBase[] virusCache = new VirusHealthBase[0];
    static int virusFrame = -1;

    static VirusHealthBase[] Viruses()
    {
        if (virusFrame != Time.frameCount)
        {
            virusFrame = Time.frameCount;
            virusCache = FindObjectsByType<VirusHealthBase>(FindObjectsSortMode.None);
        }
        return virusCache;
    }

    void FindTarget()
    {
        nextSearch = Time.time + retargetInterval;

        VirusHealthBase best = null;
        float bestSqr = float.MaxValue;
        Vector3 from = transform.position;

        foreach (VirusHealthBase v in Viruses())
        {
            if (v == null || v.IsDead || VirusAllegiance.IsConverted(v)) continue;
            if (squad != null && !squad.CanEngage(v.transform.position)) continue;

            float sqr = (v.transform.position - from).sqrMagnitude;
            if (sqr >= bestSqr) continue;

            bestSqr = sqr;
            best = v;
        }

        //stick with the current one unless the new one is clearly closer, so it doesn't flip between two
        if (target != null && !target.IsDead && !VirusAllegiance.IsConverted(target) && best != target &&
            (squad == null || squad.WithinLeash(target.transform.position)) &&
            (target.transform.position - from).magnitude - Mathf.Sqrt(bestSqr) < switchMargin)
            return;

        Engage(best);
    }

    //nothing to fight: back to its place in the formation, then faces the way its place does
    void KeepPlace()
    {
        Vector3 raw = squad.PlaceFor(this);
        makingWay = squad.MakingWay(this, out Vector3 look);
        navigator.FaceOverride = makingWay ? look : Vector3.zero;
        LetThrough(makingWay && squad.leader != null ? squad.leader.GetComponent<CharacterController>() : null);
        if (!hasPlace || (raw - placeRaw).sqrMagnitude > 0.01f)
        {
            placeRaw = raw;
            var filter = new NavMeshQueryFilter { agentTypeID = navigator.AgentTypeID, areaMask = NavMesh.AllAreas };
            place = NavMesh.SamplePosition(raw, out NavMeshHit hit, 3f, filter) ? hit.position : raw;
            hasPlace = true;
            placeMoved = true;
        }

        Vector3 to = place - transform.position;
        to.y = 0f;
        DistanceToPlace = to.magnitude;

        //follows its place exactly when the place moves; only a bigger shove off it while standing sends it back
        bool squadMoving = squad.MoveSpeed > 0.1f;
        if (!returning && DistanceToPlace > (squadMoving || placeMoved ? 0.2f : slotTolerance)) StartReturning();

        if (returning)
        {
            if (DistanceToPlace < closest - 0.1f)
            {
                closest = DistanceToPlace;
                lastProgress = Time.time;
            }

            //boxed in by the others: it walks through them. still nowhere after that, its place can't be reached
            float stuckFor = Time.time - lastProgress;
            if (stuckFor > 1f) passThrough = true;

            if (DistanceToPlace < 0.2f || (!squadMoving && stuckFor > 3f))
            {
                returning = false;
                placeMoved = false;
            }
        }

        //holding or surrounding, it phases straight through the others on its way rather than waiting to get stuck
        navigator.PassThrough = returning && (passThrough || makingWay || squad.order == SquadOrder.HoldPosition || squad.Surrounding);

        if (returning)
        {
            //keeps up with a fast-moving squad (a running player) rather than falling behind at its own pace
            float top = squad.Surrounding ? squad.surroundRunSpeed : Mathf.Clamp(squad.MoveSpeed + 0.5f, speed, maxSpeed);
            navigator.MoveTo(squad.RouteAround(transform.position, place), Mathf.Min(top, squad.MoveSpeed + DistanceToPlace * 2f + 0.5f), turnSpeed);
            return;
        }

        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(squad.FacingFor(this)),
                                                      turnSpeed * Time.deltaTime);
    }

    //stepping aside for the player, it doesn't block them if they reach it first
    void LetThrough(CharacterController player)
    {
        if (player == letThrough) return;
        if (letThrough != null && ownCollider != null) Physics.IgnoreCollision(letThrough, ownCollider, false);
        if (player != null && ownCollider != null) Physics.IgnoreCollision(player, ownCollider, true);
        letThrough = player;
    }

    void StartReturning()
    {
        returning = true;
        passThrough = false;
        closest = float.MaxValue;
        lastProgress = Time.time;
    }

    //gap to the nearest of all the target's colliders, so a big virus's body hitboxes count, not just its small root one
    float GapToTarget()
    {
        float best = float.MaxValue;
        Vector3 me = transform.position;

        if (targetColliders != null)
            foreach (Collider c in targetColliders)
            {
                if (c == null || !c.enabled || c.isTrigger) continue;

                Vector3 theirs = c.bounds.ClosestPoint(me);
                Vector3 between = theirs - ownCollider.bounds.ClosestPoint(theirs);
                between.y = 0f;
                best = Mathf.Min(best, between.magnitude);
            }

        return best < float.MaxValue ? best : MoundTarget.SurfaceGap(transform, ownCollider, target.transform);
    }

    //---------------------------------------------------------------- surrounding

    //who is attacking each virus, so they can share out the places round it
    static readonly Dictionary<VirusHealthBase, List<SoldierAI>> attackers = new Dictionary<VirusHealthBase, List<SoldierAI>>();

    void Engage(VirusHealthBase next)
    {
        if (next == target) return;

        if (target != null && attackers.TryGetValue(target, out List<SoldierAI> old))
        {
            old.Remove(this);
            if (old.Count == 0) attackers.Remove(target);
        }

        target = next;
        hasSlot = false;
        nextSlot = 0f;
        targetColliders = target != null ? target.GetComponentsInChildren<Collider>() : null;

        if (target == null) return;
        if (!attackers.TryGetValue(target, out List<SoldierAI> list)) attackers[target] = list = new List<SoldierAI>();
        list.Add(this);
    }

    void PickSlot()
    {
        nextSlot = Time.time + retargetInterval;
        hasSlot = false;

        if (!attackers.TryGetValue(target, out List<SoldierAI> list)) return;
        list.RemoveAll(s => s == null || s.target != target || !s.isActiveAndEnabled);
        if (list.Count == 0) return;

        Vector3 centre = target.transform.position;
        float Angle(SoldierAI s) => Mathf.Atan2(s.transform.position.x - centre.x, s.transform.position.z - centre.z);

        //slots are spaced evenly from where the first attacker stands; everyone keeps their order
        //round the ring, so nobody walks through anyone else to get to theirs
        float start = Angle(list[0]);
        float mine = Mathf.Repeat(Angle(this) - start, Mathf.PI * 2f);
        int index = 0;
        foreach (SoldierAI s in list)
            if (s != this && Mathf.Repeat(Angle(s) - start, Mathf.PI * 2f) < mine) index++;

        //sized to the widest of its colliders, so it stands round a big virus's body rather than inside it
        float targetRadius = 0.5f;
        if (targetColliders != null)
            foreach (Collider c in targetColliders)
            {
                if (c == null || !c.enabled || c.isTrigger) continue;
                Vector3 off = c.bounds.center - centre;
                off.y = 0f;
                targetRadius = Mathf.Max(targetRadius, off.magnitude + Mathf.Max(c.bounds.extents.x, c.bounds.extents.z));
            }
        float ring = targetRadius + navigator.Radius + attackRange * 0.5f;
        ring = Mathf.Max(ring, list.Count * navigator.Radius * 2f / (Mathf.PI * 2f));   //wide enough to fit everyone

        float angle = start + index * Mathf.PI * 2f / list.Count;
        Vector3 point = centre + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * ring;

        //a slot inside a wall or off the edge isn't a place to stand; it just goes straight in instead
        var filter = new NavMeshQueryFilter { agentTypeID = navigator.AgentTypeID, areaMask = NavMesh.AllAreas };
        point.y = transform.position.y;
        if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 1f, filter)) return;

        slot = hit.position;
        hasSlot = true;
    }
}
