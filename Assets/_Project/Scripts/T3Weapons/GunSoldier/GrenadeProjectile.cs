using UnityEngine;

public class GrenadeProjectile : MonoBehaviour
{
    public float damage = 40f;
    public float blastRadius = 5f;
    public float fuseTime = 2f;
    public float throwSpeed = 18f;

    Rigidbody rb;
    bool hasExploded;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        rb.linearVelocity = transform.forward * throwSpeed;
        Invoke(nameof(Explode), fuseTime);
    }

    void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        Collider[] hits = Physics.OverlapSphere(transform.position, blastRadius);
        foreach (Collider c in hits)
        {
            VirusHealthBase vh = c.GetComponentInParent<VirusHealthBase>();
            if (vh != null)
                vh.TakeDamage(damage);
        }

        Destroy(gameObject);
    }
}
