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
        VirusHealthBase zh = collision.collider.GetComponentInParent<VirusHealthBase>();
        if (zh != null)
            zh.TakeDamage(damage);

        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;
        if (zh != null)
            transform.SetParent(collision.transform);

        Destroy(gameObject, 5f);
    }
}