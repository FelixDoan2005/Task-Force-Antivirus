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

        Collider[] hits = Physics.OverlapSphere(attackPoint.position, range);

        foreach (Collider c in hits)
        {
            ZombieHealth zh = c.GetComponentInParent<ZombieHealth>();
            if (zh != null)
            {
                zh.TakeDamage(damage);
                Debug.Log("Sword hit zombie");
            }
        }
    }
}