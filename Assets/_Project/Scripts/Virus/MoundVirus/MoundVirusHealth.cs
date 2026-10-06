using UnityEngine;

//when it dies it does not simply stop existing - it comes apart, and the lumps riding on its
//back go their own way
public class MoundVirusHealth : VirusHealthBase
{
    [Header("Splitting")]
    [Tooltip("What it bursts into. Empty and it just dies, rather than dying silently wrong.")]
    public GameObject moundlingPrefab;

    public int moundlings = 6;

    [Tooltip("How far out they land. Kept small so they come apart in a cluster rather than " +
             "scattering across the level.")]
    public float minScatter = 1.5f;
    public float maxScatter = 3.5f;

    [Tooltip("How far each heading may wander off its even share of the circle. They are spread " +
             "evenly first so none of them overlap, then jittered so it does not read as a " +
             "machine firing on a fixed pattern.")]
    public float headingJitter = 22f;

    [Tooltip("Lifted slightly before they are thrown, so they do not start inside the floor.")]
    public float burstHeight = 0.6f;

    protected override void Die()
    {
        if (IsDead) return;   //several hits can land in the same frame; only one burst

        Split();
        base.Die();
    }

    void Split()
    {
        if (moundlingPrefab == null || moundlings <= 0) return;

        Vector3 origin = transform.position + Vector3.up * burstHeight;
        float share = 360f / moundlings;

        for (int i = 0; i < moundlings; i++)
        {
            //an even share each, then nudged, so every one of them leaves on its own heading
            float heading = i * share + Random.Range(-headingJitter, headingJitter);
            Vector3 direction = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;

            GameObject child = Instantiate(moundlingPrefab, origin, Quaternion.LookRotation(direction));

            MoundlingLaunch launch = child.GetComponent<MoundlingLaunch>();
            if (launch == null) launch = child.AddComponent<MoundlingLaunch>();

            launch.Throw(origin, direction, Random.Range(minScatter, maxScatter));
        }
    }
}
