using System.Collections.Generic;
using UnityEngine;

//once planted, it reaches out to the viruses nearest it and makes them unkillable for as long as
//it stands. it does no damage at all. it is the thing the players have to break through to.
//
//a visible tether runs to every virus it is holding, because an enemy that silently shrugs off
//every shot reads as a bug, not a mechanic.
[RequireComponent(typeof(TotemVirusRoot))]
public class TotemVirusShield : MonoBehaviour, IVirusAttack
{
    [Header("Shield")]
    [Tooltip("How far it can reach. Wider than the distance it holds back from the fight, so " +
             "the front line is actually inside it.")]
    public float shieldRadius = 20f;

    [Tooltip("How many it holds at once.")]
    public int maxShielded = 3;

    [Tooltip("Seconds between checks for who to hold. It keeps the ones it has while they stay " +
             "in reach, so the tethers do not flicker between viruses as they move.")]
    public float retargetInterval = 0.5f;

    [Tooltip("Off, it shields while still walking in. On, there is a window to catch it before " +
             "it plants.")]
    public bool onlyWhenRooted = true;

    [Header("Tethers")]
    [Tooltip("One of these is drawn to every virus it holds - a LineRenderer. Edit the prefab to " +
             "change the look.")]
    public GameObject tetherPrefab;

    [Tooltip("How far up the totem the tethers leave from.")]
    public float tetherHeight = 3f;

    [Tooltip("How far up the held virus the tether lands.")]
    public float tetherTargetHeight = 1f;

    public event System.Action<int> OnAttack;

    //generous: it only ever needs a handful, but a crowded fight returns a lot of colliders
    const int Capacity = 128;
    static readonly Collider[] buffer = new Collider[Capacity];

    readonly List<VirusHealthBase> held = new List<VirusHealthBase>();
    readonly List<LineRenderer> tethers = new List<LineRenderer>();
    readonly List<VirusHealthBase> nearby = new List<VirusHealthBase>();

    TotemVirusRoot root;
    VirusHealthBase self;
    float nextRetarget;

    void Awake()
    {
        root = GetComponent<TotemVirusRoot>();
        self = GetComponent<VirusHealthBase>();
    }

    void OnEnable()
    {
        root.OnRooted += Planted;
    }

    //death disables every script on the body, and destroying it does too - either way, everything
    //it was holding is let go here, so nothing stays unkillable after the totem is gone
    void OnDisable()
    {
        root.OnRooted -= Planted;
        ReleaseAll();
    }

    void Planted()
    {
        //played once as it plants, so the driver gives it a gesture as it digs in
        OnAttack?.Invoke(0);
        nextRetarget = 0f;
    }

    void Update()
    {
        if (onlyWhenRooted && !root.IsRooted) return;

        if (Time.time >= nextRetarget)
        {
            nextRetarget = Time.time + retargetInterval;
            Retarget();
        }

        DrawTethers();
    }

    void Retarget()
    {
        Vector3 from = transform.position;
        float reachSqr = shieldRadius * shieldRadius;

        //first let go of anything that has died, been removed, or wandered out of reach
        for (int i = held.Count - 1; i >= 0; i--)
        {
            VirusHealthBase v = held[i];

            bool gone = v == null || v.IsDead;
            bool outOfReach = !gone && Flat(v.transform.position - from).sqrMagnitude > reachSqr;

            if (!gone && !outOfReach) continue;

            if (v != null) v.RemoveShield();
            held.RemoveAt(i);
        }

        if (held.Count >= maxShielded) return;

        //then fill the empty slots with whoever is nearest
        nearby.Clear();

        int count = Physics.OverlapSphereNonAlloc(from, shieldRadius, buffer,
                                                  ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            if (buffer[i] == null) continue;

            VirusHealthBase v = buffer[i].GetComponentInParent<VirusHealthBase>();
            if (!CanHold(v)) continue;

            nearby.Add(v);
        }

        nearby.Sort((a, b) =>
            Flat(a.transform.position - from).sqrMagnitude
                .CompareTo(Flat(b.transform.position - from).sqrMagnitude));

        for (int i = 0; i < nearby.Count && held.Count < maxShielded; i++)
        {
            nearby[i].AddShield();
            held.Add(nearby[i]);
        }
    }

    public int HeldCount => held.Count;
    public float ShieldRadius => shieldRadius;

    //who it is willing to protect at all. public because the root asks the same question when it
    //goes looking for somewhere new to plant - the two must never disagree.
    public bool IsEligible(VirusHealthBase v)
    {
        if (v == null || v == self || v.IsDead) return false;

        //never another totem: two of them holding each other would both be unkillable
        if (v.GetComponent<TotemVirusShield>() != null) return false;

        //never the bloom. it is the stealth virus, and a glowing tether running to it would point
        //straight at the one thing that is supposed to go unnoticed
        if (v is BloomVirusHealth) return false;

        //never one the Demon has turned. it fights for the players now
        if (VirusAllegiance.IsConverted(v)) return false;

        return true;
    }

    bool CanHold(VirusHealthBase v)
    {
        if (!IsEligible(v)) return false;

        //a body with several colliders comes back more than once per query
        return !held.Contains(v) && !nearby.Contains(v);
    }

    void DrawTethers()
    {
        //one line per virus held, made the first time it is needed and kept after that
        while (tethers.Count < held.Count)
        {
            LineRenderer line = MakeTether();
            if (line == null) break;
            tethers.Add(line);
        }

        Vector3 start = transform.position + Vector3.up * tetherHeight;

        for (int i = 0; i < tethers.Count; i++)
        {
            LineRenderer line = tethers[i];
            if (line == null) continue;

            bool used = i < held.Count && held[i] != null;
            line.enabled = used;
            if (!used) continue;

            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, held[i].transform.position + Vector3.up * tetherTargetHeight);
        }
    }

    LineRenderer MakeTether()
    {
        if (tetherPrefab == null) return null;

        GameObject go = Instantiate(tetherPrefab, transform);
        LineRenderer line = go.GetComponentInChildren<LineRenderer>();

        if (line == null) { Destroy(go); return null; }

        line.useWorldSpace = true;
        return line;
    }

    void ReleaseAll()
    {
        foreach (VirusHealthBase v in held)
            if (v != null) v.RemoveShield();

        held.Clear();

        foreach (LineRenderer line in tethers)
            if (line != null) line.enabled = false;
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    void OnDrawGizmosSelected()
    {
        ObeliskVirusPulse.DrawRing(transform.position, shieldRadius, new Color(1f, 0.8f, 0.25f));
    }
}
