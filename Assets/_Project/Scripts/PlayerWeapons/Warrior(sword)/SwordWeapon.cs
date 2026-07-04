using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public class SwordWeapon : MonoBehaviour
{
    public float damage = 5f;
    public float range = 2f;
    public float cooldown = 0.6f;
    public Camera playerCamera;
    public Transform attackPoint;
    public float arcAngle = 5f;

    private bool swingLeftToRight = true;
    public float delayBetweenHits = 5f;
    public void Attack()
    {
        if (attackPoint == null) return;

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
            ZombieHealthBase zh = c.GetComponentInParent<ZombieHealthBase>();
            if (Mathf.Abs(angle) <= arcAngle) targets.Add(new SwingSword {zombie = zh, angle = angle});
        }
        
        targets.Sort((z1, z2) => swingLeftToRight
            ? z1.angle.CompareTo(z2.angle)   // returns -1 if z1<z2 so stays meaning hits smaller angle i.e. left -> right
                                             // returns +1 if z1>z2 so swaps meaning hits smaller angle i.e. left -> right
            : z2.angle.CompareTo(z1.angle)); // this is if were swinging right to left.
                                             // returns -1 if z2<z1 so stays meaning hits larger angle i.e. right -> left
                                             // returns +1 if z2>z2 so swaps meaning hits larger angle i.e. right -> left

            // targets.Sort((z1, z2) =>
            // {
            //     if (swingLeftToRight)
            //     {
            //         if (z1.angle < z2.angle) return -1;
            //         if (z1.angle > z2.angle) return 1;
            //         return 0;
            //     }
            //     else
            //     {
            //         if (z2.angle < z1.angle) return -1;
            //         if (z2.angle > z1.angle) return 1;
            //         return 0;
            //     }
            // });
            //     }

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
        foreach (SwingSword t in targets)
        {
            t.zombie.TakeDamage(damage);
            Debug.DrawLine(p, t.zombie.transform.position, Color.green, 0.2f);
            yield return new WaitForSeconds(delayBetweenHits);
            
        }
    }
    struct SwingSword
    {
        public ZombieHealthBase zombie;
        public float angle;
    }
}
