using UnityEngine;

//what the ranged virus spits. deliberately simple: it flies straight, hits the first thing it
//touches, and cleans itself up - no physics body, so it cannot shove the player around.
public class VirusProjectile : MonoBehaviour
{
    public float speed = 18f;
    public float damage = 8f;
    public float lifeTime = 5f;

    [Tooltip("Spawned where it lands. Optional.")]
    public GameObject impactVfx;
    public float impactVfxLifetime = 2f;

    float dieAt;

    void Start()
    {
        dieAt = Time.time + lifeTime;
    }

    void Update()
    {
        float step = speed * Time.deltaTime;

        //swept, so a fast shot cannot pass through a thin target between two frames
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, step,
                            ~0, QueryTriggerInteraction.Ignore))
        {
            Hit(hit.point, hit.collider);
            return;
        }

        transform.position += transform.forward * step;

        if (Time.time >= dieAt) Destroy(gameObject);
    }

    void Hit(Vector3 point, Collider what)
    {
        //viruses do not shoot each other
        if (what.GetComponentInParent<VirusHealthBase>() != null) return;

        IDamageable target = what.GetComponentInParent<IDamageable>();
        if (target != null && target.IsTargetable) target.TakeDamage(damage);

        VfxSpawner.Spawn(impactVfx, point, Quaternion.identity, null, impactVfxLifetime);
        Destroy(gameObject);
    }
}
