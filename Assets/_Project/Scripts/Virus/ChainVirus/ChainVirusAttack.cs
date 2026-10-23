using System.Collections.Generic;
using UnityEngine;

//it never strikes anything itself. it lobs one of the little ones off its own chain and lets
//that do the fighting.
//
//almost none of this is new. the arc is MoundlingLaunch, the same throw the mound uses when it
//bursts; what lands is carrying the moundling's own brain; and the backing-away is the ranged
//virus's movement. the only thing this script decides is when to let one go.
public class ChainVirusAttack : MonoBehaviour, IVirusAttack
{
    [Header("Throwing")]
    [Tooltip("What it throws - one of the little ones off its own chain.")]
    public GameObject chainlingPrefab;

    [Tooltip("How far it can throw. Matched to the ranged virus's firing range.")]
    public float throwRange = 14f;

    public float throwInterval = 4f;

    [Tooltip("Waits this long after spawning before the first throw.")]
    public float firstThrowDelay = 1.5f;

    [Tooltip("How many of its own it will keep in the field at once. Past this it holds on to " +
             "them rather than burying the player under an endless stream.")]
    public int maxAlive = 3;

    [Header("Arc")]
    [Tooltip("Seconds in the air. Longer reads as a lob, shorter as a hurl.")]
    public float flightTime = 0.8f;

    [Tooltip("How high above the straight line the throw peaks.")]
    public float arcHeight = 3.5f;

    [Tooltip("Lifted this far before it is thrown, so it does not start inside the floor.")]
    public float throwHeight = 1.5f;

    [Tooltip("How far short of the target it lands, so the little one has somewhere to run from " +
             "rather than arriving on top of them.")]
    public float landShortBy = 2f;

    public event System.Action<int> OnAttack;

    float nextThrow;

    //so it can count its own without searching the scene for every chainling in the level
    readonly List<GameObject> mine = new List<GameObject>();

    void Start()
    {
        nextThrow = Time.time + firstThrowDelay;
    }

    void Update()
    {
        if (BladeMaster.UltimateFreeze) return;
        if (Time.time < nextThrow) return;

        //a thrown one that has since died leaves a hole in the list rather than blocking a slot
        mine.RemoveAll(m => m == null);
        if (mine.Count >= maxAlive) return;

        if (chainlingPrefab == null) return;

        //thrown at people, because what lands only hunts people - lobbing one at the wall would
        //just be a long walk back
        PlayerHealth target = MoundTarget.NearestPlayer(transform.position);
        if (target == null) return;

        Vector3 flat = target.transform.position - transform.position;
        flat.y = 0f;

        float distance = flat.magnitude;
        if (distance > throwRange || distance < 0.01f) return;

        Throw(flat / distance, distance);

        nextThrow = Time.time + throwInterval;
        OnAttack?.Invoke(0);
    }

    void Throw(Vector3 direction, float distance)
    {
        Vector3 origin = transform.position + Vector3.up * throwHeight;

        GameObject child = Instantiate(chainlingPrefab, origin, Quaternion.LookRotation(direction));
        mine.Add(child);

        MoundlingLaunch launch = child.GetComponent<MoundlingLaunch>();
        if (launch == null) launch = child.AddComponent<MoundlingLaunch>();

        launch.flightTime = flightTime;
        launch.arcHeight = arcHeight;

        //short of the target rather than on top of it, and never behind the thrower
        launch.Throw(origin, direction, Mathf.Max(1f, distance - landShortBy));
    }

    void OnDrawGizmosSelected()
    {
        ObeliskVirusPulse.DrawRing(transform.position, throwRange, new Color(0.6f, 1f, 0.45f));
    }
}
