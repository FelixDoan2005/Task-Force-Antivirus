using UnityEngine;

public class BasicVirusAttack : MonoBehaviour
{
    public float damagePerHit = 5f;
    public float hitsPerSecond = 1f;
    public float attackRange = 1.6f;

    float timer;

    void Update()
    {
        IDamageable target = FindNearestTarget();
        if (target == null) return;

        timer += Time.deltaTime;
        float interval = 1f / hitsPerSecond;

        if (timer >= interval)
        {
            timer = 0f;
            target.TakeDamage(damagePerHit);
        }
    }

    IDamageable FindNearestTarget()
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, attackRange);

        IDamageable closest = null;
        float closestDist = Mathf.Infinity;

        foreach (var collider in nearbyColliders)
        {
            // ignore self
            if (collider.transform.root == transform.root) continue;

            // IMPORTANT: parent search
            IDamageable damageable = collider.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsTargetable) continue;

            Vector3 closestPoint = collider.ClosestPoint(transform.position);
            float dist = (closestPoint - transform.position).sqrMagnitude;

            if (dist < closestDist)
            {
                closestDist = dist;
                closest = damageable;
            }
        }

        return closest;
    }

    //for debugging and seeing range
    void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}