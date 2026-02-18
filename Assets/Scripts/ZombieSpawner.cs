using UnityEngine;

public class ZombieSpawner : MonoBehaviour
{
    public GameObject zombiePrefab;
    public Transform wallTarget;

    public float spawnInterval = 1.5f;
    public int maxAlive = 20;

    float timer;
    int alive;

    void Update()
    {
        if (zombiePrefab == null || wallTarget == null) return;
        if (alive >= maxAlive) return;

        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;
            Spawn();
        }
    }

    void Spawn()
    {
        GameObject z = Instantiate(zombiePrefab, transform.position, Quaternion.identity);
        alive++;

        ZombieMover mover = z.GetComponent<ZombieMover>();
        if (mover != null) mover.target = wallTarget;

        // when zombie is destroyed later, decrement alive
        ZombieLifeTracker t = z.AddComponent<ZombieLifeTracker>();
        t.spawner = this;
    }

    public void NotifyZombieDied()
    {
        alive = Mathf.Max(0, alive - 1);
    }

    class ZombieLifeTracker : MonoBehaviour
    {
        public ZombieSpawner spawner;
        void OnDestroy()
        {
            if (spawner != null) spawner.NotifyZombieDied();
        }
    }
}
