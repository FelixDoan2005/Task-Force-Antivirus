using System;
using UnityEngine;

public class ArrowProjectile : MonoBehaviour
{
    public float speed = 40f;
    public float lifeTime = 5f;
    public float damage = 3f;

    [Tooltip("How long a landed arrow stays in the world. The ultimate can put hundreds of these " +
             "on screen at once, and every one of them costs a trail until it goes away.")]
    public float stickDuration = 1f;

    [Header("Explosive (optional)")]
    public bool isExplosive = false;
    public float explosionRadius = 3f;
    public float explosionDamage = 15f;

    [Tooltip("Spawned where the arrow goes off. Set by whoever fires the arrow, so it lives on " +
             "the class rather than being configured per arrow.")]
    public GameObject explosionVfx;

    [Tooltip("How long that effect is given to finish before it is cleaned up.")]
    public float explosionVfxLifetime = 3f;

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

        //it has already done its damage, so it stops being something physics has to test against
        //while it sits there - it is scenery now
        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;

        if (zh != null)
            transform.SetParent(collision.transform);

        Destroy(gameObject, stickDuration);
    }

    void Explode()
    {
        //unparented, or it would be destroyed along with the arrow a frame later
        VfxSpawner.Spawn(explosionVfx, transform.position, Quaternion.identity,
                         null, explosionVfxLifetime);

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