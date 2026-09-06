using UnityEngine;
using System.Collections;
using System.Collections.Generic;

//the lobbed orb Q throws. thrown on an arc at a point rather than fired flat, so it drops
//behind cover and lands short instead of sailing off into the distance.
//it detonates twice: a hit on impact, then a larger delayed blast - so standing in the crater
//after the first one is its own mistake. damage routes back through the Demon for lifesteal.
public class DemonBomb : MonoBehaviour
{
    [Header("Impact")]
    public float damage = 200f;
    public float radius = 12f;

    [Header("Delayed blast")]
    [Tooltip("Seconds after the impact before the big one goes off.")]
    public float secondaryDelay = 1f;

    public float secondaryDamage = 320f;
    public float secondaryRadius = 18f;

    [Header("Flight")]
    public float maxLifetime = 6f;
    public float hitRadius = 0.5f;

    [Tooltip("Downward acceleration on the throw. Higher drops it more sharply.")]
    public float lobGravity = 30f;

    [Header("Effects")]
    public GameObject explosionVfx;
    public float explosionVfxScale = 1f;
    public float explosionVfxLifetime = 2f;

    public GameObject secondaryVfx;
    public float secondaryVfxScale = 1.6f;
    public float secondaryVfxLifetime = 3f;

    public CameraShake cameraShake;
    public float impactShake = 0.35f;
    public float secondaryShake = 0.7f;

    [Tooltip("Draws the flight path and both blast radii while tuning.")]
    public bool drawDebug = true;

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
                StartCoroutine(Detonate());
                return;
            }
        }

        if (drawDebug)
            Debug.DrawLine(transform.position, transform.position + step, new Color(1f, 0.5f, 0f), 1.5f);

        transform.position += step;

        if (Time.time >= dieAt) StartCoroutine(Detonate());
    }

    IEnumerator Detonate()
    {
        if (spent) yield break;
        spent = true;

        // ---- first hit, on impact
        Blast(damage, radius, explosionVfx, explosionVfxScale, explosionVfxLifetime,
              impactShake, new Color(1f, 0.5f, 0f));

        //the orb itself is gone after the impact, but the object has to live on to run the
        //second blast, so only the visuals are switched off
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;

        yield return new WaitForSeconds(secondaryDelay);

        // ---- the big one
        Blast(secondaryDamage, secondaryRadius, secondaryVfx, secondaryVfxScale, secondaryVfxLifetime,
              secondaryShake, new Color(1f, 0.15f, 0.05f));

        Destroy(gameObject);
    }

    //each blast keeps its own hit list, so anything still standing takes both
    void Blast(float dmg, float rad, GameObject vfx, float vfxScale, float vfxLife,
               float shake, Color ringColour)
    {
        if (vfx != null)
        {
            GameObject fx = Instantiate(vfx, transform.position, Quaternion.identity);
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Clear(true);
                ps.Play(true);
            }

            if (!Mathf.Approximately(vfxScale, 1f))
            {
                //asset-pack effects usually scale from the hierarchy, not their own transform
                foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule main = ps.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
                fx.transform.localScale *= vfxScale;
            }

            Destroy(fx, vfxLife);
        }

        if (cameraShake != null && shake > 0f) cameraShake.AddTrauma(shake);

        if (drawDebug)
        {
            const int segs = 32;
            for (int i = 0; i < segs; i++)
            {
                float a0 = i / (float)segs * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)segs * Mathf.PI * 2f;
                Debug.DrawLine(transform.position + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * rad,
                               transform.position + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * rad,
                               ringColour, 1f);
            }
        }

        List<VirusHealthBase> alreadyHit = new List<VirusHealthBase>();
        foreach (Collider c in Physics.OverlapSphere(transform.position, rad))
        {
            VirusHealthBase vh = c.GetComponentInParent<VirusHealthBase>();
            if (vh == null || alreadyHit.Contains(vh)) continue;
            if (VirusAllegiance.IsConverted(vh)) continue; // don't blow up our own converts
            alreadyHit.Add(vh);

            if (owner != null) owner.DealDamage(vh, dmg);
            else vh.TakeDamage(dmg);
        }
    }
}
