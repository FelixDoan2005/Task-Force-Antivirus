using UnityEngine;

//support, not a brawler: it never deals damage. it beats a slow out over a medium radius and
//drags down everything on the good side standing in it.
//
//the beat goes out on the attack channel every other virus uses, so the shared animator driver
//plays its swing for it and nothing here needs to know about animation.
public class ObeliskVirusAttack : MonoBehaviour, IVirusAttack
{
    [Header("Pulse")]
    [Tooltip("How far the slow reaches.")]
    public float pulseRadius = 14f;

    [Tooltip("Seconds between pulses.")]
    public float pulseInterval = 2.5f;

    [Tooltip("What a caught target's speed is multiplied by. 0.5 is half speed.")]
    [Range(0.05f, 1f)]
    public float slowMultiplier = 0.55f;

    [Tooltip("How long one pulse holds. Deliberately longer than the interval, so standing in " +
             "the field is a steady drag rather than a stutter in and out of full speed.")]
    public float slowDuration = 3.5f;

    [Tooltip("Waits this long after spawning before its first pulse, so one does not land the " +
             "instant it appears.")]
    public float firstPulseDelay = 1f;

    [Header("Effect")]
    [Tooltip("Spawned where the pulse goes off. Edit the prefab to change how it looks - or drop " +
             "a different one in, it does not have to be the ring.")]
    public GameObject pulseVfx;

    [Tooltip("How long the effect object is left alive before it is cleaned up.")]
    public float pulseVfxLifetime = 2f;

    public event System.Action<int> OnAttack;

    float nextPulse;

    void Start()
    {
        nextPulse = Time.time + firstPulseDelay;
    }

    void Update()
    {
        if (BladeMaster.UltimateFreeze) return;
        if (Time.time < nextPulse) return;

        nextPulse = Time.time + pulseInterval;

        ObeliskVirusPulse.Emit(transform.position, pulseRadius, slowMultiplier, slowDuration);
        ObeliskVirusPulse.Show(pulseVfx, transform.position, pulseRadius, pulseVfxLifetime);

        //raised whether or not it caught anyone - the obelisk is visibly doing its thing either
        //way, and a support that only animates when it works reads as broken
        OnAttack?.Invoke(0);
    }

    [Header("Debug")]
    [Tooltip("Draws the pulse radius in the Scene view without having to select it first. " +
             "Scene view only - it is a gizmo, so nothing shows in the game.")]
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
        ObeliskVirusPulse.DrawRing(transform.position, pulseRadius, new Color(0.25f, 0.85f, 1f));

#if UNITY_EDITOR
        UnityEditor.Handles.color = new Color(0.25f, 0.85f, 1f);
        UnityEditor.Handles.Label(transform.position + new Vector3(pulseRadius, 0.4f, 0f),
                                  "slow " + pulseRadius.ToString("0.#") + "m  x" +
                                  slowMultiplier.ToString("0.##"));
#endif
    }
}
