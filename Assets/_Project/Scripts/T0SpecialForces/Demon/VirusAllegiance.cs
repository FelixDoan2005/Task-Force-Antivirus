using UnityEngine;

//temporarily flips a virus to the player's side. rather than teaching every virus script about
//sides, this switches their own AI off, drives them itself for the duration, then hands control
//back exactly as it was - so nothing else in the project needs to know takeover exists.
//
//three phases: fighting for you, then frozen in place for a beat, then back to normal.
public class VirusAllegiance : MonoBehaviour
{
    public float speed = 4f;
    public float stopDistance = 1.4f;
    public float searchRadius = 40f;
    public float damagePerHit = 4f;
    public float hitsPerSecond = 2f;

    VirusHealthBase self;
    MonoBehaviour[] suspended;
    float timer;
    float fightUntil;
    float frozenUntil;
    bool frozen;

    //converted viruses shouldn't be hit by the player's own attacks either, so anything that
    //wants to filter them can check this
    public static bool IsConverted(VirusHealthBase v) => v != null && v.GetComponent<VirusAllegiance>() != null;

    //true once they've stopped fighting for you and are standing still, about to turn back
    public bool IsFrozen => frozen;

    public void Begin(float duration, float freezeDuration, MonoBehaviour[] toSuspend)
    {
        self = GetComponent<VirusHealthBase>();
        suspended = toSuspend;

        fightUntil = Time.time + duration;
        frozenUntil = fightUntil + freezeDuration;

        foreach (var mb in suspended)
            if (mb != null) mb.enabled = false;

        //while it is fighting for you it can't be killed - it was sent into a fight it did not
        //choose, and having it evaporate mid-takeover makes the ability feel like it did nothing
        if (self != null) self.invulnerable = true;
    }

    void Update()
    {
        if (Time.time >= frozenUntil) { Restore(); return; }

        //stunned for a beat before it turns - reads as the virus shaking off the possession,
        //and gives the player a moment to deal with it before it is hostile again
        if (Time.time >= fightUntil)
        {
            if (!frozen)
            {
                frozen = true;

                //vulnerable again the moment it stops fighting for you, so the freeze is a
                //window to kill it rather than free protection
                if (self != null) self.invulnerable = false;
            }
            return;
        }

        VirusHealthBase target = FindNearestVirus();
        if (target == null) return;

        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;

        if (dist > stopDistance)
        {
            Vector3 dir = toTarget.normalized;
            transform.position += speed * Time.deltaTime * dir;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir);
        }
        else
        {
            timer += Time.deltaTime;
            float interval = 1f / Mathf.Max(0.01f, hitsPerSecond);
            if (timer >= interval)
            {
                timer = 0f;
                target.TakeDamage(damagePerHit);
                Debug.DrawLine(transform.position, target.transform.position, Color.green, 0.2f);
            }
        }
    }

    //nearest virus that isn't us and hasn't also been converted
    VirusHealthBase FindNearestVirus()
    {
        VirusHealthBase best = null;
        float bestDist = Mathf.Infinity;

        foreach (Collider c in Physics.OverlapSphere(transform.position, searchRadius))
        {
            VirusHealthBase v = c.GetComponentInParent<VirusHealthBase>();
            if (v == null || v == self) continue;
            if (v.GetComponent<VirusAllegiance>() != null) continue; // on our side too

            float d = (v.transform.position - transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = v; }
        }
        return best;
    }

    void Restore()
    {
        if (self != null) self.invulnerable = false;

        if (suspended != null)
            foreach (var mb in suspended)
                if (mb != null) mb.enabled = true;

        Destroy(this);
    }

    //if this is disabled by a pause or a class switch, the virus would otherwise be left
    //invulnerable with its AI switched off - permanently harmless and unkillable
    void OnDisable()
    {
        if (self != null) self.invulnerable = false;

        if (suspended == null) return;
        foreach (var mb in suspended)
            if (mb != null) mb.enabled = true;
    }
}
