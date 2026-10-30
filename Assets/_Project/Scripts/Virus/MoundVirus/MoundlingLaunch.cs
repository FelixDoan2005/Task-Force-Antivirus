using UnityEngine;

//throws a moundling clear of the body it came out of, then hands it back to its own brain.
//
//the arc is driven here rather than by physics because everything else in the virus set moves
//by writing the transform, and a rigidbody thrown into the middle of that would be fighting the
//movement script over the same position the moment it landed.
public class MoundlingLaunch : MonoBehaviour
{
    public float flightTime = 0.55f;

    [Tooltip("How high the hop peaks above the straight line out.")]
    public float arcHeight = 1.4f;

    Vector3 from, to;
    float started;
    bool flying;

    //everything that would otherwise be driving the transform while it is in the air
    MonoBehaviour[] grounded;

    public bool Flying => flying;

    public void Throw(Vector3 origin, Vector3 direction, float distance)
    {
        from = origin;
        to = origin + direction.normalized * distance;

        //it lands on whatever is actually under that spot rather than at the height it was
        //thrown from, so a moundling flung at a step does not finish inside it
        if (Physics.Raycast(to + Vector3.up * 6f, Vector3.down, out RaycastHit hit, 30f,
                            ~0, QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<VirusHealthBase>() == null)
            to.y = hit.point.y;

        //held still for the flight: its gravity, its chase and its navigator would all be writing
        //the same position this component is animating
        grounded = new MonoBehaviour[]
        {
            GetComponent<MoundlingMovement>(),
            GetComponent<MoundlingAttack>(),
            GetComponent<VirusLedgeJump>(),
            GetComponent<NpcNavigator>()
        };

        foreach (MonoBehaviour m in grounded)
            if (m != null) m.enabled = false;

        transform.position = from;
        started = Time.time;
        flying = true;
    }

    void Update()
    {
        if (!flying) return;

        float t = flightTime > 0f ? (Time.time - started) / flightTime : 1f;

        if (t >= 1f)
        {
            transform.position = to;
            Land();
            return;
        }

        Vector3 flat = Vector3.Lerp(from, to, t);
        flat.y += Mathf.Sin(t * Mathf.PI) * arcHeight;

        transform.position = flat;
    }

    void Land()
    {
        flying = false;

        foreach (MonoBehaviour m in grounded)
            if (m != null) m.enabled = true;

        //its job is done, and leaving it on is an Update every frame doing nothing
        Destroy(this);
    }
}
