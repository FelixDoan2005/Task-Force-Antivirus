using UnityEngine;

//carries a slow for anything that can be slowed - the player now, allied soldiers when they
//exist.
//
//it is a separate channel rather than a write to PlayerController.speedMultiplier because the
//class scripts own that field and set it every single frame: BladeMaster, Cyborg and Demon all
//push it back to 1 whenever they are not blocking or casting. a debuff written there survives
//until the next Update and no longer. this is the field nothing else touches.
public class Slowable : MonoBehaviour
{
    float multiplier = 1f;
    float expiresAt;

    //1 is full speed. read by whatever moves this thing.
    public float Multiplier => multiplier;

    PlayerController player;

    void Awake()
    {
        player = GetComponent<PlayerController>();
    }

    //found or added on demand, so nothing has to be wired onto the player prefab by hand and a
    //future ally picks the same behaviour up for free
    public static Slowable For(Component target)
    {
        if (target == null) return null;

        //it belongs on whatever actually moves, which is not always the object the hitbox is on
        PlayerController controller = target.GetComponentInParent<PlayerController>();

        GameObject host = controller != null
            ? controller.gameObject
            : target.transform.root.gameObject;

        Slowable slowable = host.GetComponent<Slowable>();
        if (slowable == null) slowable = host.AddComponent<Slowable>();

        return slowable;
    }

    public void ApplySlow(float slowMultiplier, float duration)
    {
        slowMultiplier = Mathf.Clamp01(slowMultiplier);

        //while a stronger slow is still running a weaker one does nothing. two obelisks pulsing
        //the same target should not multiply into a standstill, and a routine pulse landing a
        //frame after a death pulse should not undercut it.
        if (Time.time < expiresAt && slowMultiplier > multiplier) return;

        multiplier = slowMultiplier;
        expiresAt = Time.time + duration;
    }

    public void ClearSlow()
    {
        multiplier = 1f;
        expiresAt = 0f;
    }

    void Update()
    {
        if (multiplier < 1f && Time.time >= expiresAt) multiplier = 1f;

        //written every frame rather than on change, so it cannot be left stuck slow if this
        //component is disabled or the player is rebuilt under it
        if (player != null) player.debuffSpeedMultiplier = multiplier;
    }

    void OnDisable()
    {
        if (player != null) player.debuffSpeedMultiplier = 1f;
    }
}
