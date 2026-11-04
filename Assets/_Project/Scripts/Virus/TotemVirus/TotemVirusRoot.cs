using UnityEngine;

//walks in behind the front line, plants itself, and holds that ground for as long as there is
//anything near it worth shielding.
//
//the walking in is the ranged virus's movement, reused outright: close to a distance and hold
//there, which is exactly "stand behind the fight". this notices when it has stopped and plants.
//
//battles move, though. planted with nothing to shield for long enough, the fight has gone
//somewhere else - so it pulls itself up, walks to the nearest virus it could be protecting, and
//plants again there.
[RequireComponent(typeof(RangedVirusMovement))]
public class TotemVirusRoot : MonoBehaviour
{
    [Header("Planting")]
    [Tooltip("Holding still this long means it has found its spot.")]
    public float settleTime = 1.5f;

    [Tooltip("Drifting less than this still counts as holding still.")]
    public float settleTolerance = 0.15f;

    [Tooltip("Plants wherever it is after walking this long, so it can never wander about " +
             "forever - stuck on a wall, or chasing something that keeps moving.")]
    public float maxWalkTime = 12f;

    [Header("Moving on")]
    [Tooltip("Planted with nothing to shield for this long and it decides the fight has moved " +
             "on, pulls up, and goes looking.")]
    public float uprootAfter = 15f;

    [Tooltip("How far it will look for viruses to go and protect. Nothing inside this and it " +
             "stays put - walking off towards nothing helps no one.")]
    public float seekRange = 60f;

    [Tooltip("Plants again once it is this close to the virus it walked to, as a fraction of its " +
             "shield reach, so that one and whatever is around it end up inside.")]
    [Range(0.2f, 0.9f)]
    public float replantFraction = 0.5f;

    [Tooltip("Seconds between looking for who to walk to. A scene-wide search, so not every frame.")]
    public float seekInterval = 0.5f;

    [Tooltip("Plants wherever it is after moving on for this long. Separate from the walk-in cap " +
             "and much longer: sharing that one cut a long walk into short legs, with a planted " +
             "stall in the middle of nowhere between each.")]
    public float maxRegroupTime = 45f;

    [Header("Effect")]
    [Tooltip("Played where it plants. Edit the prefab to change the look.")]
    public GameObject rootVfx;

    public float rootVfxRadius = 4f;
    public float rootVfxLifetime = 2f;

    public bool IsRooted { get; private set; }
    public bool IsRegrouping => regrouping;

    public event System.Action OnRooted;

    RangedVirusMovement movement;
    TotemVirusShield shield;
    VirusLedgeJump traversal;
    NpcNavigator navigator;

    Vector3 anchor;
    float stillSince;
    float walkingSince;

    //planted with nothing held since this moment; below zero while it is holding something
    float emptySince = -1f;

    bool regrouping;
    VirusHealthBase seekTarget;
    float nextSeek;

    void Awake()
    {
        movement = GetComponent<RangedVirusMovement>();
        shield = GetComponent<TotemVirusShield>();
        traversal = GetComponent<VirusLedgeJump>();
        navigator = GetComponent<NpcNavigator>();
    }

    void Start()
    {
        ResetSettle();
        walkingSince = Time.time;
    }

    void Update()
    {
        //frozen by the ultimate is not the same as choosing to stop - without this it would plant
        //the instant the freeze lifted, wherever it happened to be caught
        if (BladeMaster.UltimateFreeze)
        {
            stillSince = Time.time;
            return;
        }

        if (IsRooted)
        {
            WatchForEmpty();
            return;
        }

        if (regrouping)
        {
            Regroup();
            if (IsRooted) return;
        }

        bool stuck = Settled();
        bool tooLong = Time.time - walkingSince >= (regrouping ? maxRegroupTime : maxWalkTime);

        if (stuck || tooLong) Root();
    }

    //---------------------------------------------------------------- planted

    void WatchForEmpty()
    {
        if (shield == null) return;

        if (shield.HeldCount > 0)
        {
            emptySince = -1f;
            return;
        }

        if (emptySince < 0f)
        {
            emptySince = Time.time;
            return;
        }

        if (Time.time - emptySince < uprootAfter) return;

        //only pull up if there is somewhere to go. with nothing anywhere near it stays planted,
        //and checks again shortly rather than searching the whole scene every frame
        if (Time.time < nextSeek) return;
        nextSeek = Time.time + seekInterval;

        VirusHealthBase target = NearestEligible();
        if (target == null) return;

        Uproot(target);
    }

    void Uproot(VirusHealthBase target)
    {
        IsRooted = false;
        regrouping = true;
        seekTarget = target;
        emptySince = -1f;
        walkingSince = Time.time;
        ResetSettle();

        //the borrowed movement stays off: it heads for people or the wall, and this is walking to
        //its own side instead
    }

    //---------------------------------------------------------------- moving on

    void Regroup()
    {
        if (traversal != null && traversal.IsBusy) return;

        if (Time.time >= nextSeek)
        {
            nextSeek = Time.time + seekInterval;
            seekTarget = NearestEligible();
        }

        if (seekTarget == null)
        {
            //everything it might have protected is gone - back to walking in the ordinary way,
            //towards people or the wall, and planting when it settles
            regrouping = false;
            movement.enabled = true;
            walkingSince = Time.time;
            ResetSettle();
            return;
        }

        Vector3 to = seekTarget.transform.position - transform.position;
        to.y = 0f;

        float distance = to.magnitude;
        float replantAt = (shield != null ? shield.ShieldRadius : 20f) * replantFraction;

        if (distance <= replantAt)
        {
            Root();
            return;
        }

        //the way there, round whatever is in the way, is the navigator's - hops included
        navigator.MoveTo(seekTarget.transform, movement.speed, movement.turnSpeed);
    }

    //the nearest virus it would actually be willing to protect - asked through the shield, so the
    //two can never disagree about who counts
    VirusHealthBase NearestEligible()
    {
        VirusHealthBase best = null;
        float bestSqr = seekRange * seekRange;
        Vector3 from = transform.position;

        foreach (VirusHealthBase v in FindObjectsByType<VirusHealthBase>(FindObjectsSortMode.None))
        {
            if (shield != null ? !shield.IsEligible(v) : (v == null || v.IsDead)) continue;

            Vector3 d = v.transform.position - from;
            d.y = 0f;

            float sqr = d.sqrMagnitude;
            if (sqr >= bestSqr) continue;

            bestSqr = sqr;
            best = v;
        }

        return best;
    }

    //---------------------------------------------------------------- shared

    bool Settled()
    {
        //height is ignored: settling onto the floor after spawning is not walking
        Vector3 moved = transform.position - anchor;
        moved.y = 0f;

        if (moved.magnitude > settleTolerance)
        {
            anchor = transform.position;
            stillSince = Time.time;
        }

        return Time.time - stillSince >= settleTime;
    }

    void ResetSettle()
    {
        anchor = transform.position;
        stillSince = Time.time;
    }

    void Root()
    {
        IsRooted = true;
        regrouping = false;
        seekTarget = null;
        emptySince = -1f;
        movement.enabled = false;

        ObeliskVirusPulse.Show(rootVfx, transform.position, rootVfxRadius, rootVfxLifetime);

        OnRooted?.Invoke();
    }
}
