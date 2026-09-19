using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public class SwordWeapon : MonoBehaviour
{
    [Header("Setup")]
    public Camera playerCamera;
    public Transform attackPoint;

    [Header("Weapon")]
    public float damage = 5f;
    public float range = 2f;
    public float cooldown = 0.6f;
    public float arcAngle = 5f;
    public float swingDuration = 0.3f;
    public float delayBetweenHits = 5f;

    private bool swingLeftToRight = true;

    [Tooltip("How many swing animations the model has. Each attack picks one at random.")]
    public int attackVariants = 6;

    //the last one played, so a random pick never repeats itself back to back - two identical
    //swings in a row read as a stutter rather than as a second attack
    int lastSwing = -1;

    //the knight model listens to this - the weapon itself stays a plain hit-detector
    public event System.Action<int> OnAttack;

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    //a fresh swing each time, never the one just played
    int PickSwing()
    {
        if (attackVariants <= 1) return 0;

        int pick = Random.Range(0, attackVariants);
        if (pick == lastSwing) pick = (pick + 1 + Random.Range(0, attackVariants - 1)) % attackVariants;

        lastSwing = pick;
        return pick;
    }

    public void Attack()
    {
        if (attackPoint == null) return;

        OnAttack?.Invoke(PickSwing());

        // visualise the swing sphere
        Vector3 p = attackPoint.position;
        Debug.DrawRay(p, Vector3.forward  * range, Color.red,    0.2f);
        Debug.DrawRay(p, Vector3.back     * range, Color.red,    0.2f);
        Debug.DrawRay(p, Vector3.left     * range, Color.red,    0.2f);
        Debug.DrawRay(p, Vector3.right    * range, Color.red,    0.2f);
        Debug.DrawRay(p, Vector3.up       * range, Color.yellow, 0.2f);
        Debug.DrawRay(p, Vector3.down     * range, Color.yellow, 0.2f);

        Collider[] hits = Physics.OverlapSphere(p, range);
        Vector3 viewForward = playerCamera.transform.forward;
        viewForward.y = 0f;
        viewForward = viewForward.normalized;

        List<SwingSword> targets = new List<SwingSword>();
        
        foreach (Collider c in hits)
        {
            
            Vector3 dirToTarget = c.transform.position - p;
            dirToTarget.y = 0f;
            dirToTarget = dirToTarget.normalized;

            float angle = Vector3.SignedAngle(viewForward, dirToTarget, Vector3.up);
            VirusHealthBase vh = c.GetComponentInParent<VirusHealthBase>();
            if (Mathf.Abs(angle) <= arcAngle) targets.Add(new SwingSword {virus = vh, angle = angle});
        }
        
        targets.Sort((z1, z2) => swingLeftToRight
            ? z1.angle.CompareTo(z2.angle)   // returns -1 if z1<z2 so stays meaning hits smaller angle i.e. left -> right
                                             // returns +1 if z1>z2 so swaps meaning hits smaller angle i.e. left -> right
            : z2.angle.CompareTo(z1.angle)); // this is if were swinging right to left.
                                             // returns -1 if z2<z1 so stays meaning hits larger angle i.e. right -> left
                                             // returns +1 if z2>z2 so swaps meaning hits larger angle i.e. right -> left

            StartCoroutine(SwingDamage(targets, p));
            swingLeftToRight = !swingLeftToRight;
    }

    IEnumerator SwingDamage(List<SwingSword> targets, Vector3 p)
    {
        Vector3 viewForward = playerCamera.transform.forward;
        viewForward.y = 0f;
        viewForward = viewForward.normalized;

        Debug.DrawRay(p, Quaternion.Euler(0,  arcAngle, 0) * viewForward * range, Color.cyan, 0.5f);
        Debug.DrawRay(p, Quaternion.Euler(0, -arcAngle, 0) * viewForward * range, Color.cyan, 0.5f);
        
        float startTime = Time.time;

        foreach (SwingSword t in targets)
        {
            float normalisedAngle = (t.angle + arcAngle) / (2f * arcAngle);
            if (!swingLeftToRight) normalisedAngle = 1f - normalisedAngle;

            float hitTime = startTime + (swingDuration * normalisedAngle);
            yield return new WaitUntil(() => Time.time >= hitTime);

            if (t.virus == null) continue;

            t.virus.TakeDamage(damage);
            Debug.DrawLine(p, t.virus.transform.position, Color.green, 0.2f);
        }
    }
    struct SwingSword
    {
        public VirusHealthBase virus;
        public float angle;
    }
}
