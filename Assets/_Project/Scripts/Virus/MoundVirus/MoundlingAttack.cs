using UnityEngine;

//bites the player, and cannot reach anything else: the target is a PlayerHealth rather than an
//IDamageable, so the wall is not something it can pick up by accident
public class MoundlingAttack : MonoBehaviour, IVirusAttack
{
    public float damagePerHit = 4f;
    public float hitsPerSecond = 1.5f;
    public float attackRange = 0.8f;

    float timer;

    public event System.Action<int> OnAttack;

    Collider ownCollider;
    MoundlingMovement movement;

    PlayerHealth fallback;
    float nextSearch;

    void Awake()
    {
        ownCollider = GetComponent<Collider>();
        movement = GetComponent<MoundlingMovement>();
    }

    void Update()
    {
        if (BladeMaster.UltimateFreeze) return;

        PlayerHealth target = Target();
        if (target == null) return;

        if (MoundTarget.SurfaceGap(transform, ownCollider, target.transform) > attackRange) return;

        timer += Time.deltaTime;

        float interval = 1f / Mathf.Max(0.0001f, hitsPerSecond);
        if (timer < interval) return;

        timer = 0f;
        target.TakeDamage(damagePerHit);
        OnAttack?.Invoke(0);
    }

    //the movement component already knows who it is chasing; this only looks on its own if the
    //moundling was built without one
    PlayerHealth Target()
    {
        if (movement != null) return movement.Player;

        if (fallback == null || !fallback.IsTargetable)
        {
            if (Time.time < nextSearch) return null;

            nextSearch = Time.time + 0.5f;
            fallback = MoundTarget.NearestPlayer(transform.position);
        }

        return fallback;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
