using UnityEngine;

//keeps its distance instead of closing in: it walks until its target is within firing range and
//backs off if something gets too close, so it behaves like artillery rather than a brawler.
public class RangedVirusMovement : MonoBehaviour
{
    public float speed = 2f;
    public float detectionRange = 30f;

    [Tooltip("It stops closing once the target is this near.")]
    public float preferredRange = 14f;

    [Tooltip("Anything closer than this and it backs away.")]
    public float tooClose = 8f;

    public float turnSpeed = 240f;

    VirusLedgeJump traversal;

    void Awake()
    {
        traversal = GetComponent<VirusLedgeJump>();
    }

    void Update()
    {
        if (BladeMaster.UltimateFreeze) return;
        if (traversal != null && traversal.IsBusy) return;

        IDamageable target = FindNearestTarget();
        if (target == null) return;

        Transform targetTransform = ((MonoBehaviour)target).transform;

        Vector3 toTarget = targetTransform.position - transform.position;
        toTarget.y = 0f;

        float dist = toTarget.magnitude;
        if (dist < 0.01f) return;

        Vector3 dir = toTarget.normalized;

        //it faces its target whether or not it is moving, because it shoots from where it stands
        transform.rotation = Quaternion.RotateTowards(transform.rotation,
                                                      Quaternion.LookRotation(dir),
                                                      turnSpeed * Time.deltaTime);

        //inside its comfortable band it holds position
        if (dist <= preferredRange && dist >= tooClose) return;

        Vector3 move = dist < tooClose ? -dir : dir;

        if (traversal != null && traversal.TryClimb(move)) return;

        transform.position += speed * Time.deltaTime * move;
    }

    IDamageable FindNearestTarget()
    {
        //one shared scan for the whole virus set: no array allocated per call, and each
        //collider's owner is worked out once instead of every frame
        return TargetScan.NearestDamageable(transform, detectionRange);
    }
}
