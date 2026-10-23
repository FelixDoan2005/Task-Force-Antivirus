using UnityEngine;

//hits the wall, hard, and only the wall. there is no branch here that can reach a player: the
//target is a WallHealth, not an IDamageable, so it cannot pick one up by accident.
public class BloomVirusAttack : MonoBehaviour, IVirusAttack
{
    [Tooltip("Per hit. Far above the rest of the set - the tank, the next worst, manages 10 a " +
             "second against the wall and this does double that.")]
    public float damagePerHit = 40f;

    public float hitsPerSecond = 0.5f;

    [Tooltip("How close its body has to be to the wall's, measured between the two colliders.")]
    public float attackRange = 1f;

    float timer;

    //so a model can react when it lands a blow
    public event System.Action<int> OnAttack;

    Collider ownCollider;
    BloomVirusMovement movement;
    WallHealth wall;
    float nextSearch;

    void Awake()
    {
        ownCollider = GetComponent<Collider>();
        movement = GetComponent<BloomVirusMovement>();
    }

    void Update()
    {
        if (BladeMaster.UltimateFreeze) return;

        WallHealth target = Target();
        if (target == null) return;

        if (BloomVirusTarget.SurfaceGap(transform, ownCollider, target) > attackRange) return;

        timer += Time.deltaTime;

        float interval = 1f / Mathf.Max(0.0001f, hitsPerSecond);
        if (timer < interval) return;

        timer = 0f;
        target.TakeDamage(damagePerHit);
        OnAttack?.Invoke(0);
    }

    //the movement component already knows which wall it is walking at; this only goes looking
    //on its own if the virus was built without one
    WallHealth Target()
    {
        if (movement != null) return movement.Wall;

        if (wall == null || !wall.IsTargetable)
        {
            if (Time.time < nextSearch) return null;

            nextSearch = Time.time + 1f;
            wall = BloomVirusTarget.Nearest(transform.position);
        }

        return wall;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
