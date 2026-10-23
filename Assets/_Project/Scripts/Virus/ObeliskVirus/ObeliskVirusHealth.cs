using UnityEngine;

//killing it is not a clean win: it spends itself on one last pulse, wider and heavier than the
//ones it was beating out alive.
public class ObeliskVirusHealth : VirusHealthBase
{
    [Header("Dying pulse")]
    [Tooltip("Much wider than the living pulse - this is the one that catches people who thought " +
             "they were clear of it.")]
    public float deathPulseRadius = 28f;

    [Tooltip("What a caught target's speed is multiplied by. Lower than the living pulse.")]
    [Range(0.05f, 1f)]
    public float deathSlowMultiplier = 0.35f;

    public float deathSlowDuration = 6f;

    [Header("Effect")]
    [Tooltip("Spawned where it dies. Edit the prefab to change how it looks - or drop a different " +
             "one in, it does not have to be the ring.")]
    public GameObject deathPulseVfx;

    [Tooltip("How long the effect object is left alive before it is cleaned up.")]
    public float deathPulseVfxLifetime = 3f;

    protected override void Die()
    {
        if (IsDead) return;   //a volley can land several hits in the same frame; only one burst

        ObeliskVirusPulse.Emit(transform.position, deathPulseRadius,
                               deathSlowMultiplier, deathSlowDuration);

        //spawned unparented, so it still plays out after the body has been cleaned up
        ObeliskVirusPulse.Show(deathPulseVfx, transform.position, deathPulseRadius,
                               deathPulseVfxLifetime);

        base.Die();
    }

    [Header("Debug")]
    [Tooltip("Draws the dying pulse's radius in the Scene view without having to select it.")]
    public bool alwaysShowRadius = true;

    void OnDrawGizmos()
    {
        if (alwaysShowRadius) DrawRadius();
    }

    void OnDrawGizmosSelected()
    {
        DrawRadius();
    }

    void DrawRadius()
    {
        //warmer and wider than the living pulse, so the two rings cannot be confused
        ObeliskVirusPulse.DrawRing(transform.position, deathPulseRadius, new Color(1f, 0.45f, 0.2f));

#if UNITY_EDITOR
        UnityEditor.Handles.color = new Color(1f, 0.45f, 0.2f);
        UnityEditor.Handles.Label(transform.position + new Vector3(deathPulseRadius, 0.4f, 0f),
                                  "death " + deathPulseRadius.ToString("0.#") + "m  x" +
                                  deathSlowMultiplier.ToString("0.##"));
#endif
    }
}
