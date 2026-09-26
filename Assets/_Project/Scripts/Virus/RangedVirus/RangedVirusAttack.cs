using UnityEngine;

//spits a projectile at whatever it can see, on a timer. it never needs to touch its target,
//which is the whole point of it.
public class RangedVirusAttack : MonoBehaviour, IVirusAttack
{
    public float damagePerShot = 8f;
    public float shotsPerSecond = 0.4f;
    public float range = 20f;

    [Tooltip("What it spits. Without one it does nothing, rather than hitting from nowhere.")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 18f;

    [Tooltip("Where shots come from. Empty fires from the middle of its body.")]
    public Transform muzzle;

    [Tooltip("How long it winds up before the shot leaves, so the animation is seen first.")]
    public float windup = 0.35f;

    public event System.Action<int> OnAttack;

    float timer;
    float fireAt = -1f;
    IDamageable pending;

    void Update()
    {
        if (BladeMaster.UltimateFreeze) return;

        //a shot already announced is waiting on its wind-up
        if (fireAt > 0f && Time.time >= fireAt)
        {
            Fire();
            fireAt = -1f;
        }

        IDamageable target = FindTarget();
        if (target == null) return;

        timer += Time.deltaTime;
        float interval = 1f / Mathf.Max(0.01f, shotsPerSecond);

        if (timer < interval || fireAt > 0f) return;

        timer = 0f;
        pending = target;
        fireAt = Time.time + windup;

        OnAttack?.Invoke(0);
    }

    void Fire()
    {
        if (projectilePrefab == null || pending == null) return;

        Transform targetTransform = ((MonoBehaviour)pending).transform;
        if (targetTransform == null) return;

        Vector3 from = muzzle != null ? muzzle.position : transform.position;
        Vector3 aim = targetTransform.position + Vector3.up;

        GameObject shot = Instantiate(projectilePrefab, from,
                                      Quaternion.LookRotation((aim - from).normalized));

        var vp = shot.GetComponent<VirusProjectile>();
        if (vp != null)
        {
            vp.damage = damagePerShot;
            vp.speed = projectileSpeed;
        }
    }

    IDamageable FindTarget()
    {
        //one shared scan for the whole virus set: no array allocated per call, and each
        //collider's owner is worked out once instead of every frame
        return TargetScan.NearestDamageable(transform, range);
    }
}
