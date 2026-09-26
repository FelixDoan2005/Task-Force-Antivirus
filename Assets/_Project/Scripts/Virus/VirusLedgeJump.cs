using UnityEngine;

//gives a virus enough awareness of the world to climb onto things and to fall off them.
//
//the virus movement scripts write straight to transform.position - no rigidbody, no character
//controller - so nothing in the level stops them and nothing pulls them down. that is why they
//walk through a platform instead of onto it. this sits alongside them and does two jobs:
//
//  * looks ahead for something solid, and if its top is low enough to climb, arcs onto it
//  * keeps the virus on whatever surface is beneath it, so it falls off edges rather than
//    floating along in mid air
//
//while a jump is running it owns the movement, and the movement script stands aside.
[RequireComponent(typeof(Collider))]
public class VirusLedgeJump : MonoBehaviour
{
    [Header("Climbing")]
    [Tooltip("The tallest ledge it can get onto. Anything higher is treated as a wall.")]
    public float maxJumpHeight = 2.5f;

    [Tooltip("Anything this low is stepped over without a jump.")]
    public float stepHeight = 0.3f;

    [Tooltip("How far ahead it looks for something to climb.")]
    public float lookAhead = 1.2f;

    [Tooltip("How long the hop takes.")]
    public float jumpDuration = 0.6f;

    [Tooltip("How high above the landing point the arc peaks.")]
    public float arcHeight = 0.8f;

    [Tooltip("How far past the edge it lands, so it does not come down on the lip.")]
    public float landingInset = 0.6f;

    [Tooltip("Seconds before it will try another climb, so a failed hop does not loop.")]
    public float jumpCooldown = 0.8f;

    [Header("Falling")]
    [Tooltip("Off, it keeps whatever height its movement gives it - which is how they used to " +
             "drift along above the floor.")]
    public bool applyGravity = true;

    public float gravity = 20f;

    [Tooltip("What counts as ground. Leave as everything and other viruses are filtered out anyway.")]
    public LayerMask groundMask = ~0;

    //---------------------------------------------------------------- state
    Collider body;
    float feetOffset;        //how far the origin sits above the soles
    float radius;

    public float JumpDuration => jumpDuration;

    bool jumping;
    float jumpStarted;
    Vector3 jumpFrom, jumpTo;

    float nextJumpAllowed;
    float fallSpeed;

    public bool IsBusy => jumping;

    //so the model can play its hop
    public event System.Action OnJump;

    void Awake()
    {
        body = GetComponent<Collider>();
        radius = Mathf.Max(body.bounds.size.x, body.bounds.size.z) * 0.5f;

        MeasureFeet();

        //it has to clear the edge by more than its own half-width, or it lands still hanging over
        //the drop, sees the same ledge in front of it, and hops again
        landingInset = Mathf.Max(landingInset, radius + 0.35f);
    }

    //measured from what you can SEE, not from the collider. a hitbox is a rough box around a
    //creature and its bottom is often nowhere near its feet - standing the collider on the floor
    //is what left the big one hanging in the air.
    void MeasureFeet()
    {
        float lowest = float.MaxValue;
        foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>())
            if (smr.gameObject.activeInHierarchy) lowest = Mathf.Min(lowest, smr.bounds.min.y);

        feetOffset = lowest < float.MaxValue
            ? transform.position.y - lowest
            : transform.position.y - body.bounds.min.y;
    }

    int framesAlive;

    void Update()
    {
        //BoneProportions re-shapes the skeleton in LateUpdate, so a creature whose legs have
        //been shortened - or removed - is not the shape Awake measured. one re-measure after a
        //frame has gone by catches that, rather than leaving it standing on legs it no longer has.
        if (framesAlive < 3)
        {
            framesAlive++;
            if (framesAlive == 2) MeasureFeet();
        }

        if (jumping) { Arc(); return; }

        if (applyGravity) Fall();
    }

    //---------------------------------------------------------------- climbing
    //called by the movement script before it moves, so the decision happens while we know which
    //way it actually wants to go
    public bool TryClimb(Vector3 direction)
    {
        if (jumping || Time.time < nextJumpAllowed) return false;
        if (direction.sqrMagnitude < 0.0001f) return false;

        direction.y = 0f;
        direction.Normalize();

        //cast from shin height, so the ground it is standing on is not what gets hit
        Vector3 origin = transform.position + Vector3.up * (stepHeight - feetOffset + 0.05f);

        if (!Physics.SphereCast(origin, radius * 0.6f, direction, out RaycastHit hit,
                                lookAhead, groundMask, QueryTriggerInteraction.Ignore))
            return false;

        //other viruses are not scenery to be climbed
        if (hit.collider.GetComponentInParent<VirusHealthBase>() != null) return false;
        if (hit.collider.transform.IsChildOf(transform)) return false;

        float feetY = transform.position.y - feetOffset;
        float topY = hit.collider.bounds.max.y;
        float rise = topY - feetY;

        //too low to be worth a hop, or too high to be anything but a wall
        if (rise <= stepHeight || rise > maxJumpHeight) return false;

        Vector3 landing = new Vector3(hit.point.x, topY + feetOffset, hit.point.z)
                        + direction * landingInset;

        //is there actually room to stand there? a ledge with a ceiling is not a ledge
        if (Physics.CheckSphere(landing + Vector3.up * (radius - feetOffset + 0.05f),
                                radius * 0.5f, groundMask, QueryTriggerInteraction.Ignore))
            return false;

        jumping = true;
        jumpStarted = Time.time;
        jumpFrom = transform.position;
        jumpTo = landing;
        fallSpeed = 0f;

        OnJump?.Invoke();
        return true;
    }

    void Arc()
    {
        float t = jumpDuration > 0f ? (Time.time - jumpStarted) / jumpDuration : 1f;

        if (t >= 1f)
        {
            transform.position = jumpTo;
            jumping = false;
            nextJumpAllowed = Time.time + jumpCooldown;
            return;
        }

        //a straight line from take-off to landing, plus a hump, so it reads as a hop rather
        //than a slide up an invisible ramp
        Vector3 flat = Vector3.Lerp(jumpFrom, jumpTo, t);
        flat.y += Mathf.Sin(t * Mathf.PI) * arcHeight;

        transform.position = flat;
    }

    //---------------------------------------------------------------- falling
    void Fall()
    {
        Vector3 from = transform.position + Vector3.up * 0.2f;
        float reach = feetOffset + 0.4f + fallSpeed * Time.deltaTime;

        if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, reach + 0.2f,
                            groundMask, QueryTriggerInteraction.Ignore) &&
            hit.collider.GetComponentInParent<VirusHealthBase>() == null)
        {
            //planted: sit exactly on whatever is underneath
            fallSpeed = 0f;
            Vector3 p = transform.position;
            p.y = hit.point.y + feetOffset;
            transform.position = p;
            return;
        }

        fallSpeed += gravity * Time.deltaTime;
        transform.position += Vector3.down * (fallSpeed * Time.deltaTime);
    }
}
