using UnityEngine;
using UnityEngine.InputSystem;

public class SwordWeapon : MonoBehaviour
{
    public float damage = 5f;
    public float range = 2f;
    public float cooldown = 0.6f;
    public Camera playerCamera;
    public Transform attackPoint;

    float nextAttackTime;


    // void Update()
    // {
    //     if (Mouse.current == null) return;

    //     if (Mouse.current.leftButton.wasPressedThisFrame && Time.time >= nextAttackTime)
    //     {
    //         Attack();
    //         nextAttackTime = Time.time + cooldown;
    //     }
    // }


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

        foreach (Collider c in hits)
        {
            ZombieHealthBase zh = c.GetComponentInParent<ZombieHealthBase>();
            if (zh != null)
            {
                zh.TakeDamage(damage);
                Debug.DrawLine(p, c.transform.position, Color.green, 0.2f);
                Debug.Log("Sword hit zombie");
            }
        }
    }
}