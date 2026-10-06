using UnityEngine;

//hits whatever is in reach, player or wall, exactly like the rest of the set
public class MoundVirusAttack : MonoBehaviour, IVirusAttack
{
    public float damagePerHit = 15f;
    public float hitsPerSecond = 0.5f;

    [Tooltip("Measured between the two colliders, not their centres - this thing is wide.")]
    public float attackRange = 1.2f;

    float timer;

    public event System.Action<int> OnAttack;

    Collider ownCollider;

    void Awake()
    {
        ownCollider = GetComponent<Collider>();
    }

    void Update()
    {
        if (BladeMaster.UltimateFreeze) return;

        IDamageable target = MoundTarget.NearestDamageable(transform, attackRange + 4f);
        if (target == null) return;

        Transform targetTransform = ((MonoBehaviour)target).transform;
        if (MoundTarget.SurfaceGap(transform, ownCollider, targetTransform) > attackRange) return;

        timer += Time.deltaTime;

        float interval = 1f / Mathf.Max(0.0001f, hitsPerSecond);
        if (timer < interval) return;

        timer = 0f;
        target.TakeDamage(damagePerHit);
        OnAttack?.Invoke(0);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
