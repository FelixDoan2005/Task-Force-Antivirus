using UnityEngine;

public class SpeedVirusAttack : MonoBehaviour
{
    public float damagePerHit = 3f;
    public float hitsPerSecond = 2f;
    public float attackRange = 1.6f;

    float timer;

    void Update()
    {
        if (BladeMaster.UltimateFreeze) return; // frozen for the length of the BladeMaster's ultimate

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
            if (collider.transform.root == transform.root) continue;

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

    void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
