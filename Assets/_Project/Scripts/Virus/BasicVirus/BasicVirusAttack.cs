using UnityEngine;

public class BasicVirusAttack : MonoBehaviour, IVirusAttack
{
    public float damagePerHit = 5f;
    public float hitsPerSecond = 1f;
    public float attackRange = 1.6f;

    float timer;

    //so a model can swing when it lands a blow
    public event System.Action<int> OnAttack;

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
            OnAttack?.Invoke(0);
        }
    }

    IDamageable FindNearestTarget()
    {
        //one shared scan for the whole virus set: no array allocated per call, and each
        //collider's owner is worked out once instead of every frame
        return TargetScan.NearestDamageable(transform, attackRange);
    }

    //for debugging and seeing range
    void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}