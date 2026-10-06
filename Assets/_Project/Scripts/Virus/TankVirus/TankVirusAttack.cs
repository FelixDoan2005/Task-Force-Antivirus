using UnityEngine;

public class TankVirusAttack : MonoBehaviour, IVirusAttack
{
    public float damagePerHit = 20f;
    public float hitsPerSecond = 0.5f;
    public float attackRange = 1.8f;

    float timer;

    [Tooltip("How many swing animations the model has. Each hit picks one at random.")]
    public int attackVariants = 2;

    //so a model can swing when it actually lands a hit. the number is which swing to play.
    public event System.Action<int> OnAttack;

    //never the same swing twice running - it reads as a stutter rather than a second blow
    int lastSwing = -1;

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
            OnAttack?.Invoke(PickSwing());
        }
    }

    int PickSwing()
    {
        if (attackVariants <= 1) return 0;

        int pick = Random.Range(0, attackVariants);
        if (pick == lastSwing) pick = (pick + 1 + Random.Range(0, attackVariants - 1)) % attackVariants;

        lastSwing = pick;
        return pick;
    }

    IDamageable FindNearestTarget()
    {
        //one shared scan for the whole virus set: no array allocated per call, and each
        //collider's owner is worked out once instead of every frame
        return TargetScan.NearestDamageable(transform, attackRange);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
