using System;
using UnityEngine;

public class ArrowProjectile : MonoBehaviour
{
    public float speed = 40f;
    public float lifeTime = 5f;
    public float damage = 3f;

    [Header("Explosive (optional)")]
    public bool isExplosive = false;
    public float explosionRadius = 3f;
    public float explosionDamage = 15f;

    public event Action<Vector3> OnImpact;

    Rigidbody rb;
    bool hasHit = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        Physics.IgnoreLayerCollision(gameObject.layer, gameObject.layer, true);
    }

    void Start()
    {
        rb.linearVelocity = transform.forward * speed;
        Destroy(gameObject, lifeTime);
    }
    void FixedUpdate()
    {
        Vector3 v = rb.linearVelocity;
        if (v.sqrMagnitude > 0.01f)
            transform.forward = v.normalized;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;
        hasHit = true;

        OnImpact?.Invoke(transform.position);

        if (isExplosive)
        {
            Explode();
            Destroy(gameObject);
            return;
        }

        VirusHealthBase zh = collision.collider.GetComponentInParent<VirusHealthBase>();
        if (zh != null)
            zh.TakeDamage(damage);

        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;
        if (zh != null)
            transform.SetParent(collision.transform);

        Destroy(gameObject, 5f);
    }

    void Explode()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (Collider c in hits)
        {
            VirusHealthBase vh = c.GetComponentInParent<VirusHealthBase>();
            if (vh != null)
                vh.TakeDamage(explosionDamage);
        }
    }

    void OnDrawGizmos()
    {
        if (!isExplosive) return;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}