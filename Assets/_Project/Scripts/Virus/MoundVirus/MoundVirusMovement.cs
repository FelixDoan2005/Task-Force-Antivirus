using UnityEngine;

//lumbers at whatever is nearest, the same way the tank does, just far slower.
[RequireComponent(typeof(NpcNavigator))]
public class MoundVirusMovement : MonoBehaviour
{
    [Tooltip("Metres per second. Very slow on purpose - the threat is what it leaves behind, " +
             "not how fast it arrives.")]
    public float speed = 0.6f;

    [Tooltip("Clear space left between its body and its target's, measured between the colliders.")]
    public float contactBuffer = 0.4f;

    public float detectionRange = 25f;
    public float turnSpeed = 120f;

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

        IDamageable target = MoundTarget.NearestDamageable(transform, detectionRange);
        if (target == null) return;

        Transform targetTransform = ((MonoBehaviour)target).transform;

        Vector3 toTarget = targetTransform.position - transform.position;
        toTarget.y = 0f;

        Vector3 dir = toTarget.normalized;

        if (MoundTarget.SurfaceGap(transform, ownCollider, targetTransform) <= contactBuffer)
        {
            //it keeps turning after it stops, so it hits what it is looking at. while it walks it
            //faces the way it is going
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                                                              Quaternion.LookRotation(dir),
                                                              turnSpeed * Time.deltaTime);
            return;
        }

        //the way there, round whatever is in the way, is the navigator's - hops included
        navigator.MoveTo(targetTransform, speed, turnSpeed);
    }
}
