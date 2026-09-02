using UnityEngine;
using System.Collections.Generic;

//the lobbed bomb Q turns the auto attack into. thrown on an arc at a point rather than fired
//flat, so it drops behind cover and lands short instead of sailing off into the distance.
//damage is routed back through the Demon so lifesteal still applies.
public class DemonBomb : MonoBehaviour
{
    public float damage = 60f;
    public float radius = 5f;
    public float maxLifetime = 6f;
    public float hitRadius = 0.5f;

    [Tooltip("Downward acceleration on the throw. Higher drops it more sharply.")]
    public float lobGravity = 30f;

    public GameObject explosionVfx;
    public float explosionVfxScale = 1f;
    public float explosionVfxLifetime = 2f;

    [Tooltip("Draws the flight path while tuning.")]
    public bool drawTrail = true;

    DemonClass owner;
    Vector3 velocity;
    float dieAt;
    bool spent;

    //solves the throw so it lands on the point aimed at. picking the flight time and then
    //deriving the velocity is much steadier than picking an angle - the arc stays the same
    //shape whether the target is near or far.
    public void Launch(DemonClass demon, Vector3 target, float gravity, float flightTime)
    {
        owner = demon;
        lobGravity = gravity;

        flightTime = Mathf.Max(0.1f, flightTime);
        Vector3 d = target - transform.position;

        // d = v*t + 0.5*a*t^2, solved for v with a = (0, -gravity, 0)
        velocity = d / flightTime + 0.5f * gravity * flightTime * Vector3.up;

        dieAt = Time.time + maxLifetime;
    }

    void Update()
    {
        if (spent) return;

        velocity += lobGravity * Time.deltaTime * Vector3.down;

        Vector3 step = velocity * Time.deltaTime;
        float dist = step.magnitude;

        if (dist > 0.0001f &&
            Physics.SphereCast(transform.position, hitRadius, step.normalized, out RaycastHit hit,
                               dist, ~0, QueryTriggerInteraction.Ignore))
        {
            //ignore the thrower's own capsule on the way out
            if (hit.collider.GetComponentInParent<CharacterController>() == null)
            {
                transform.position = hit.point;
                Explode();
                return;
            }
        }

        if (drawTrail)
            Debug.DrawLine(transform.position, transform.position + step, new Color(1f, 0.5f, 0f), 1.5f);

        transform.position += step;

        if (Time.time >= dieAt) Explode();
    }

    void Explode()
    {
        if (spent) return;
        spent = true;

        if (explosionVfx != null)
        {
            GameObject fx = Instantiate(explosionVfx, transform.position, Quaternion.identity);
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Clear(true);
                ps.Play(true);
            }
            if (!Mathf.Approximately(explosionVfxScale, 1f))
                fx.transform.localScale *= explosionVfxScale;
            Destroy(fx, explosionVfxLifetime);
        }

        //ring so the blast radius is readable while tuning
        const int segs = 24;
        for (int i = 0; i < segs; i++)
        {
            float a0 = i / (float)segs * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)segs * Mathf.PI * 2f;
            Debug.DrawLine(transform.position + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius,
                           transform.position + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius,
                           new Color(1f, 0.4f, 0f), 0.8f);
        }

        List<VirusHealthBase> alreadyHit = new List<VirusHealthBase>();
        foreach (Collider c in Physics.OverlapSphere(transform.position, radius))
        {
            VirusHealthBase vh = c.GetComponentInParent<VirusHealthBase>();
            if (vh == null || alreadyHit.Contains(vh)) continue;
            if (VirusAllegiance.IsConverted(vh)) continue; // don't blow up our own converts
            alreadyHit.Add(vh);

            if (owner != null) owner.DealDamage(vh, damage);
            else vh.TakeDamage(damage);
        }

        Destroy(gameObject);
    }
}
