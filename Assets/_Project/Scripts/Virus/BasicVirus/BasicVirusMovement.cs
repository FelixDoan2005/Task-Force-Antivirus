using UnityEngine;

public class BasicVirusMovement : MonoBehaviour
{
    public float speed = 3f;
    public float stopDistance = 1.2f;
    public float detectionRange = 20f;

    VirusLedgeJump traversal;

    void Awake()
    {
        traversal = GetComponent<VirusLedgeJump>();
    }

    void Update()
    {
        if (BladeMaster.UltimateFreeze) return;

        //mid-hop the jump owns where it is, or the two fight over the same transform
        if (traversal != null && traversal.IsBusy) return; // frozen for the length of the BladeMaster's ultimate

        IDamageable target = FindNearestTarget();
        if (target == null) return;

        Transform targetTransform = ((MonoBehaviour)target).transform;

        Vector3 toTarget = targetTransform.position - transform.position;
        toTarget.y = 0f;

        float dist = toTarget.magnitude;
        if (dist <= stopDistance) return;

        Vector3 dir = toTarget.normalized;

        //something climbable in the way is hopped onto instead of walked through
        if (traversal != null && traversal.TryClimb(dir)) return;

        transform.position += speed * Time.deltaTime * dir;

        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    IDamageable FindNearestTarget()
    {
        //one shared scan for the whole virus set: no array allocated per call, and each
        //collider's owner is worked out once instead of every frame
        return TargetScan.NearestDamageable(transform, detectionRange);
    }
}