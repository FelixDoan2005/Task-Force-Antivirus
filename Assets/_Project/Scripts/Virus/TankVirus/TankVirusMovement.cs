using UnityEngine;

[RequireComponent(typeof(NpcNavigator))]
public class TankVirusMovement : MonoBehaviour
{
    public float speed = 1.5f;

    [Tooltip("How much clear space to leave between its body and its target's. Measured between " +
             "their colliders, not their centres, so it holds up when the virus is scaled.")]
    public float contactBuffer = 0.3f;

    [Tooltip("Fallback gap when neither side has a collider to measure.")]
    public float stopDistance = 1.5f;

    public float detectionRange = 20f;

    [Tooltip("It keeps turning to face its target after it stops, so it attacks what it is " +
             "looking at rather than shuffling past.")]
    public float turnSpeed = 360f;

    Collider ownCollider;
    VirusLedgeJump traversal;
    NpcNavigator navigator;

    void Awake()
    {
        ownCollider = GetComponent<Collider>();
        traversal = GetComponent<VirusLedgeJump>();
        navigator = GetComponent<NpcNavigator>();
    }

    void Update()
    {
        if (BladeMaster.UltimateFreeze) return;

        //mid-hop the jump owns where it is, or the two fight over the same transform
        if (traversal != null && traversal.IsBusy) return;
        
        IDamageable target = FindNearestTarget();
        if (target == null) return;

        Transform targetTransform = ((MonoBehaviour)target).transform;

        Vector3 toTarget = targetTransform.position - transform.position;
        toTarget.y = 0f;

        float dist = toTarget.magnitude;
        Vector3 dir = toTarget.normalized;

        //the gap that matters is between the two bodies, not their origins: comparing centres
        //meant a big virus kept walking long after it was already touching the player, shoving
        //them backwards for as long as it lived.
        if (SurfaceGap(targetTransform) <= contactBuffer)
        {
            //facing is kept up once it has stopped, so a virus standing in contact still turns to
            //follow the player rather than attacking thin air. while it walks it faces the way it
            //is going, which with nothing in the way is the same thing
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                                                              Quaternion.LookRotation(dir),
                                                              turnSpeed * Time.deltaTime);
            return;
        }

        //the way there - round whatever is in the way, and up onto it if that is where the target
        //is - is the navigator's
        navigator.MoveTo(targetTransform, speed, turnSpeed);
    }

    //the flat distance between the nearest points of the two bodies. worked out from the actual
    //shapes rather than a radius, because a radius is meaningless for a wall - half the width of
    //a 40m wall would have this thing stopping twenty metres out and never swinging at it.
    float SurfaceGap(Transform target)
    {
        Collider targetCol = target.GetComponentInChildren<Collider>();

        Vector3 me = transform.position;

        Vector3 theirPoint = targetCol != null ? targetCol.bounds.ClosestPoint(me) : target.position;
        Vector3 myPoint = ownCollider != null ? ownCollider.bounds.ClosestPoint(theirPoint) : me;

        Vector3 between = theirPoint - myPoint;
        between.y = 0f;

        return between.magnitude;
    }

    IDamageable FindNearestTarget()
    {
        //one shared scan for the whole virus set: no array allocated per call, and each
        //collider's owner is worked out once instead of every frame
        return TargetScan.NearestDamageable(transform, detectionRange);
    }
}
