using UnityEngine;

//keeps the obelisk standing where its own pulse can actually reach.
//
//it borrows the ranged virus's kiting movement, whose hold distance is a plain typed-in number.
//that is fine for something that shoots, because the projectile's reach and the standoff get set
//together by hand. it is wrong for a support whose radius you retune: the two fall out of step
//silently. the obelisk ended up holding station at 12m while pulsing 8m, so it never touched
//anybody - and it backed away at 8m, which was the pulse's own edge.
//
//so the standoff is worked out from the pulse rather than typed in a second time. change the
//radius and this follows it; there is nothing to keep in sync by hand and nothing to forget.
//
//this does not change the radius. it only changes where the creature chooses to stand.
[RequireComponent(typeof(RangedVirusMovement))]
[RequireComponent(typeof(ObeliskVirusAttack))]
public class ObeliskStandoff : MonoBehaviour
{
    [Tooltip("Where it settles, as a fraction of the pulse radius. Under 1 so its target sits " +
             "comfortably inside the field instead of teetering on the edge of it.")]
    [Range(0.2f, 0.95f)]
    public float holdFraction = 0.7f;

    [Tooltip("How close something gets before it backs off, as a fraction of the pulse radius. " +
             "Kept well under the hold so it retreats to another spot inside its own field " +
             "rather than retreating out of it.")]
    [Range(0.1f, 0.9f)]
    public float retreatFraction = 0.45f;

    [Tooltip("On, it keeps tracking the pulse radius while the game runs, so retuning the radius " +
             "in play takes effect straight away.")]
    public bool keepInSync = true;

    RangedVirusMovement movement;
    ObeliskVirusAttack attack;

    void Awake()
    {
        movement = GetComponent<RangedVirusMovement>();
        attack = GetComponent<ObeliskVirusAttack>();

        Apply();
    }

    void Update()
    {
        if (keepInSync) Apply();
    }

    void Apply()
    {
        if (movement == null || attack == null) return;

        float radius = attack.pulseRadius;

        movement.preferredRange = radius * holdFraction;

        //never at or past the hold distance: the two readings either side of it are what the
        //movement uses to decide to advance or retreat, and crossing them makes it jitter on
        //the spot instead of settling
        movement.tooClose = Mathf.Min(radius * retreatFraction, movement.preferredRange - 0.5f);
    }

    void OnDrawGizmosSelected()
    {
        if (attack == null) attack = GetComponent<ObeliskVirusAttack>();
        if (attack == null) return;

        //where it will try to stand, against the pulse it is standing there to land
        ObeliskVirusPulse.DrawRing(transform.position, attack.pulseRadius * holdFraction,
                                   new Color(0.6f, 1f, 0.5f));
    }
}
