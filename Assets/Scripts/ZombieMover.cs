using UnityEngine;

public class ZombieMover : MonoBehaviour
{
    public Transform target;
    public float speed = 3f;
    public float stopDistance = 1.2f;

    void Update()
    {
        if (target == null) return;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f; // keep it on the ground plane

        float dist = toTarget.magnitude;
        if (dist <= stopDistance) return;

        Vector3 dir = toTarget.normalized;

        transform.position += dir * speed * Time.deltaTime;

        // face the direction it moves
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }
}
