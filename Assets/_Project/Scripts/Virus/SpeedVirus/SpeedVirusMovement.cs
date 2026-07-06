using UnityEngine;

public class SpeedVirusMovement : MonoBehaviour
{
    public float speed = 7f;
    public float stopDistance = 1.2f;
    public float detectionRange = 40f;

    void Update()
    {
        IDamageable target = FindNearestTarget();
        if (target == null) return;

        Transform targetTransform = ((MonoBehaviour)target).transform;

        Vector3 toTarget = targetTransform.position - transform.position;
        toTarget.y = 0f;

        float dist = toTarget.magnitude;
        if (dist <= stopDistance) return;

        Vector3 dir = toTarget.normalized;

        transform.position += speed * Time.deltaTime * dir;

        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    IDamageable FindNearestTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRange);

        IDamageable closest = null;
        float closestDist = Mathf.Infinity;

        foreach (var hit in hits)
        {
            if (hit.transform.root == transform.root) continue;

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsTargetable) continue;

            Vector3 closestPoint = hit.ClosestPoint(transform.position);
            float dist = (closestPoint - transform.position).sqrMagnitude;

            if (dist < closestDist)
            {
                closestDist = dist;
                closest = damageable;
            }
        }

        return closest;
    }
}
