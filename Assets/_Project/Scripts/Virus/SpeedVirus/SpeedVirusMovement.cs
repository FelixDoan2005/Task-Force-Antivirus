using UnityEngine;

[RequireComponent(typeof(NpcNavigator))]
public class SpeedVirusMovement : MonoBehaviour
{
    public float speed = 7f;
    public float stopDistance = 1.2f;
    public float detectionRange = 40f;

    [Tooltip("How fast it turns to face the way it is walking.")]
    public float turnSpeed = 720f;

    VirusLedgeJump traversal;
    NpcNavigator navigator;

    void Awake()
    {
        traversal = GetComponent<VirusLedgeJump>();
        navigator = GetComponent<NpcNavigator>();
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

        //the way there - round whatever is in the way, and up onto it if that is where the target
        //is - is the navigator's
        navigator.MoveTo(targetTransform, speed, turnSpeed);
    }

    IDamageable FindNearestTarget()
    {
        //one shared scan for the whole virus set: no array allocated per call, and each
        //collider's owner is worked out once instead of every frame
        return TargetScan.NearestDamageable(transform, detectionRange);
    }
}
