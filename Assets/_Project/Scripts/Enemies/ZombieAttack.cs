using UnityEngine;

public class ZombieAttack : MonoBehaviour
{
    public float damagePerHit = 5f;
    public float hitsPerSecond = 1f;
    public float attackRange = 1.6f;

    float timer;

    void Update()
    {
        IDamageable target = FindNearestTarget();
        Debug.Log("Chosen target = " + (target == null ? "NONE" : target.GetType().Name));

        if (target == null) return;

        timer += Time.deltaTime;
        float interval = 1f / hitsPerSecond;

        if (timer >= interval)
        {
            timer = 0f;
            Debug.Log("Zombie ATTACKING now");
            target.TakeDamage(damagePerHit);
        }
    }

    IDamageable FindNearestTarget()
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, attackRange);

        Debug.Log("Overlap hits: " + nearbyColliders.Length);
        foreach (var collider in nearbyColliders)
        {
            Debug.Log(" - hit: " + collider.name + " root: " + collider.transform.root.name);
        }

        IDamageable closest = null;
        float closestDist = Mathf.Infinity;

        foreach (var collider in nearbyColliders)
        {
            // ignore self
            if (collider.transform.root == transform.root) continue;

            // IMPORTANT: parent search
            IDamageable damageable = collider.GetComponentInParent<IDamageable>();
            if (damageable == null) continue;

            Debug.Log("Zombie sees damageable: " + collider.transform.root.name + " via collider: " + collider.name);
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