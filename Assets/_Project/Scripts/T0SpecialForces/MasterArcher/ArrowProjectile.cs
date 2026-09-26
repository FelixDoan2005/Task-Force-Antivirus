using System;
using System.Collections.Generic;
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

    [Header("Pass-through damage (optional)")]
    [Tooltip("Dealt to anything the arrow flies past on its way, once each. 0 turns it off.")]
    public float passThroughDamage = 0f;

    [Tooltip("How close it has to pass to catch them.")]
    public float passThroughRadius = 1.5f;

    [Header("Sticking")]
    [Tooltip("Snap onto the target's actual animated mesh rather than wherever its collider " +
             "happened to stop the arrow. Big creatures have crude colliders, so without this " +
             "arrows hang in the air beside them.")]
    public bool snapToMeshSurface = true;

    [Tooltip("How far past the surface the arrowhead is pushed, so it reads as buried in the " +
             "body rather than balanced on the skin.")]
    public float embedDepth = 0.12f;

    //one graze per virus - without this a slow arrow would tick the same target every step
    readonly HashSet<VirusHealthBase> grazed = new HashSet<VirusHealthBase>();
    Vector3 lastSweepPoint;

    //the virus is passed along too - whoever fired it often needs to know what was struck,
    //not just the point in space where the arrow stopped
    public event Action<Vector3, VirusHealthBase> OnImpact;

    //which way the surface it struck was facing. a wall faces sideways and a floor faces up,
    //and that is the difference between a decal lying flat and one stuck to a wall.
    public Vector3 LastImpactNormal { get; private set; } = Vector3.up;

    //raised when this arrow lodges in something alive, so whoever fired it can keep track of
    //which targets are currently carrying one
    public event Action<VirusHealthBase> OnStuckIn;

    Rigidbody rb;
    bool hasHit = false;

    //when this arrow is due back in the pool. replaces the Destroy(gameObject, t) calls, which
    //cannot be used on something that is going to be reused.
    float releaseAt = -1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        Physics.IgnoreLayerCollision(gameObject.layer, gameObject.layer, true);
    }

    //everything a reused arrow has to forget. called before whoever fired it configures the
    //shot, so their damage values and their event subscriptions survive it.
    //
    //the tunables are copied back from the prefab rather than left alone, and that is not
    //tidiness: the Q arms a shot by setting isExplosive on the arrow it just made. leave that
    //set and the next arrow out of the pool explodes too, whoever fired it and whatever for.
    public void ResetForReuse(ArrowProjectile defaults)
    {
        hasHit = false;
        releaseAt = -1f;

        grazed.Clear();
        lastSweepPoint = transform.position;
        LastImpactNormal = Vector3.up;

        //subscribers from the last flight would otherwise still be listening
        OnImpact = null;
        OnStuckIn = null;

        if (defaults != null)
        {
            speed = defaults.speed;
            lifeTime = defaults.lifeTime;
            damage = defaults.damage;
            stickDuration = defaults.stickDuration;

            isExplosive = defaults.isExplosive;
            explosionRadius = defaults.explosionRadius;
            explosionDamage = defaults.explosionDamage;
            explosionVfx = defaults.explosionVfx;
            explosionVfxLifetime = defaults.explosionVfxLifetime;

            passThroughDamage = defaults.passThroughDamage;
            passThroughRadius = defaults.passThroughRadius;

            snapToMeshSurface = defaults.snapToMeshSurface;
            embedDepth = defaults.embedDepth;
        }

        //a landed arrow turns its colliders off and rides whatever it stuck into
        foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = true;

        if (rb == null) rb = GetComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        //a trail remembers every point it has been through. without this the arrow arrives
        //dragging a streak across the map from wherever it finished last time.
        foreach (TrailRenderer t in GetComponentsInChildren<TrailRenderer>(true)) t.Clear();
    }

    //sent on its way, once the shot has been set up
    public void Launch()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        rb.linearVelocity = transform.forward * speed;
        lastSweepPoint = transform.position;

        releaseAt = Time.time + lifeTime;
    }

    void Update()
    {
        if (releaseAt < 0f || Time.time < releaseAt) return;

        releaseAt = -1f;
        ProjectilePool.Release(gameObject);
    }
    void FixedUpdate()
    {
        Vector3 v = rb.linearVelocity;
        if (v.sqrMagnitude > 0.01f)
            transform.forward = v.normalized;

        SweepPassThrough();
    }

    //an arrow moves metres per physics step, so checking only where it happens to be each step
    //would let it skip straight over anything between two positions. this sweeps the whole
    //segment it actually covered instead, which is what makes the graze reliable at speed.
    void SweepPassThrough()
    {
        if (hasHit || passThroughDamage <= 0f || passThroughRadius <= 0f) return;

        Vector3 now = transform.position;
        if (Vector3.Distance(lastSweepPoint, now) > 0.0001f)
        {
            foreach (Collider c in Physics.OverlapCapsule(lastSweepPoint, now, passThroughRadius))
            {
                VirusHealthBase vh = c.GetComponentInParent<VirusHealthBase>();
                if (vh == null || grazed.Contains(vh)) continue;

                grazed.Add(vh);
                vh.TakeDamage(passThroughDamage);
            }
        }

        lastSweepPoint = now;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;
        hasHit = true;

        //worked out before the event so subscribers are told what was hit in the same call
        VirusHealthBase struck = collision.collider.GetComponentInParent<VirusHealthBase>();

        if (collision.contactCount > 0) LastImpactNormal = collision.GetContact(0).normal;

        OnImpact?.Invoke(transform.position, struck);

        if (isExplosive)
        {
            Explode();
            ProjectilePool.Release(gameObject);
            return;
        }

        VirusHealthBase zh = struck;
        if (zh != null)
        {
            zh.TakeDamage(damage);
            OnStuckIn?.Invoke(zh);
        }

        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;

        //it has already done its damage, so it stops being something physics has to test against
        //while it sits there - it is scenery now
        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;

        if (zh != null)
        {
            Transform ride = collision.transform;

            //the collider only decides THAT it hit; where it looks like it hit comes from the
            //mesh, which is the only thing the player can actually see
            if (snapToMeshSurface &&
                MeshSurfaceSnap.TryFindSurface(zh.gameObject, transform.position,
                                               out Vector3 surface, out Transform bone))
            {
                transform.position = surface + transform.forward * embedDepth;
                if (bone != null) ride = bone;
            }

            //worldPositionStays, or a bone scaled several times over would blow the arrow up
            transform.SetParent(ride, true);
        }

        releaseAt = Time.time + stickDuration;
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