using UnityEngine;

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

    VirusHealthBase target;
    float nextSearch;
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

    void Update()
    {
        if (target == null || target.IsDead || VirusAllegiance.IsConverted(target) || Time.time >= nextSearch)
            FindTarget();
        if (target == null) return;

        if (MoundTarget.SurfaceGap(transform, ownCollider, target.transform) > attackRange)
        {
            navigator.MoveTo(target.transform, speed, turnSpeed);
            return;
        }

        Vector3 dir = target.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir),
                                                          turnSpeed * Time.deltaTime);

        if (Time.time < nextHit) return;
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

        target = best;
    }
}
