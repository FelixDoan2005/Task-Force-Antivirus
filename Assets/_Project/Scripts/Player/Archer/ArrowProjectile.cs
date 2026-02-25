using UnityEngine;

public class ArrowProjectile : MonoBehaviour
{
    public float speed = 40f;
    public float lifeTime = 5f;
    public float damage = 3f;

    Rigidbody rb;
    bool hasHit = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
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
        ZombieHealth zh = collision.collider.GetComponentInParent<ZombieHealth>();
        if (zh != null)
            zh.TakeDamage(damage);

        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        if (zh != null)
            transform.SetParent(collision.transform);

        Destroy(gameObject, 5f);
    }
}