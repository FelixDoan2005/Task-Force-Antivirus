using UnityEngine;
using UnityEngine.Serialization;

public class BasicVirusSpawner : MonoBehaviour
{
    [FormerlySerializedAs("zombiePrefab")]
    public GameObject virusPrefab;
    public Transform wallTarget;

    public float spawnInterval = 1.5f;
    public int maxAlive = 20;

    float timer;
    int alive;

    void Update()
    {
        if (virusPrefab == null || wallTarget == null) return;
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
        GameObject z = Instantiate(virusPrefab, transform.position, Quaternion.identity);
        alive++;

        // when virus is destroyed later, decrement alive
        VirusLifeTracker t = z.AddComponent<VirusLifeTracker>();
        t.spawner = this;
    }

    public void NotifyVirusDied()
    {
        alive = Mathf.Max(0, alive - 1);
    }

    class VirusLifeTracker : MonoBehaviour
    {
        public BasicVirusSpawner spawner;
        void OnDestroy()
        {
            if (spawner != null) spawner.NotifyVirusDied();
        }
    }
}
