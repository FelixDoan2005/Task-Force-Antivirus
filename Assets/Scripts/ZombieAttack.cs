using UnityEngine;

public class ZombieAttack : MonoBehaviour
{
    public float damagePerHit = 5f;
    public float hitsPerSecond = 1f;
    public float attackRange = 1.6f;

    WallHealth wall;
    float timer;

    void Start()
    {
        wall = FindFirstObjectByType<WallHealth>();

    }

    void Update()
    {
        if (wall == null) return;

        float dist = Vector3.Distance(transform.position, wall.transform.position);
        if (dist > attackRange) return;

        timer += Time.deltaTime;
        float interval = 1f / hitsPerSecond;

        if (timer >= interval)
        {
            timer = 0f;
            wall.TakeDamage(damagePerHit);
        }
    }
}
