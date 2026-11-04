using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

//a friendly soldier: finds the nearest virus anywhere, walks to it and fights it.
//later this is where orders (hold position, seek out, return) will go
[RequireComponent(typeof(NpcNavigator))]
[RequireComponent(typeof(SoldierHealth))]
public class SoldierAI : MonoBehaviour
{
    public float speed = 3.5f;
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

    public VirusHealthBase Target => target;

    //the model swings when this fires
    public event System.Action<int> OnAttack;

    void Awake()
    {
        navigator = GetComponent<NpcNavigator>();
        ownCollider = GetComponent<Collider>();
    }

    void OnDisable()
    {
        Engage(null);
    }

    void Update()
    {
        if (target == null || target.IsDead || VirusAllegiance.IsConverted(target) || Time.time >= nextSearch)
            FindTarget();
        if (target == null) return;

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

        //a random swing each time, never the same one twice in a row
        int variants = Mathf.Max(1, swingVariants);
        int next = Random.Range(0, variants);
        if (variants > 1 && next == swing) next = (next + 1 + Random.Range(0, variants - 1)) % variants;
        swing = next;
        OnAttack?.Invoke(swing);
    }

    void FindTarget()
    {
        nextSearch = Time.time + retargetInterval;

        VirusHealthBase best = null;
        float bestSqr = float.MaxValue;
        Vector3 from = transform.position;

        foreach (VirusHealthBase v in FindObjectsByType<VirusHealthBase>(FindObjectsSortMode.None))
        {
            if (v.IsDead || VirusAllegiance.IsConverted(v)) continue;

            float sqr = (v.transform.position - from).sqrMagnitude;
            if (sqr >= bestSqr) continue;

            bestSqr = sqr;
            best = v;
        }

        //stick with the current one unless the new one is clearly closer, so it doesn't flip between two
        if (target != null && !target.IsDead && !VirusAllegiance.IsConverted(target) && best != target &&
            (target.transform.position - from).magnitude - Mathf.Sqrt(bestSqr) < switchMargin)
            return;

        Engage(best);
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
