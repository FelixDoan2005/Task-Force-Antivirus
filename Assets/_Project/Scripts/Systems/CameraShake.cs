using UnityEngine;

//goes on the camera itself. runs in LateUpdate so it layers on top of whatever
//PlayerController.Look() positioned the camera at during Update, rather than fighting it.
//
//uses a "trauma" model rather than a fixed shake duration: callers add trauma, it decays on
//its own, and the actual shake is trauma squared. that means small hits barely register while
//several landing together stack into something violent, which reads much better than a
//constant-amplitude rumble.
public class CameraShake : MonoBehaviour
{
    [Header("Shake")]
    [Tooltip("Maximum positional offset at full trauma.")]
    public float maxOffset = 0.35f;

    [Tooltip("Maximum camera roll in degrees at full trauma.")]
    public float maxRoll = 2.5f;

    [Tooltip("How fast trauma drains. Higher = snappier recovery.")]
    public float decay = 1.6f;

    [Tooltip("How jittery the shake is. Higher = more frantic.")]
    public float frequency = 22f;

    float trauma;
    float seed;

    void Awake()
    {
        //so two cameras (or a restart) don't shake in identical patterns
        seed = Random.value * 100f;
    }

    /// Add trauma. Values are additive and clamped to 1, so repeated hits stack.
    /// Use this for one-off impacts.
    public void AddTrauma(float amount)
    {
        trauma = Mathf.Clamp01(trauma + amount);
    }

    /// Hold trauma at least at this level. Use this for sustained rumbles - calling AddTrauma
    /// every frame doesn't work, because decay drains faster than small per-frame additions
    /// accumulate, so the shake never gets off the ground.
    public void SetTraumaFloor(float amount)
    {
        trauma = Mathf.Clamp01(Mathf.Max(trauma, amount));
    }

    void LateUpdate()
    {
        if (trauma <= 0f) return;

        //squared so light taps stay subtle and heavy stacking gets dramatic
        float strength = trauma * trauma;
        float t = Time.time * frequency;

        //perlin rather than Random so the motion is continuous instead of buzzing
        float x = (Mathf.PerlinNoise(seed, t) * 2f - 1f) * maxOffset * strength;
        float y = (Mathf.PerlinNoise(seed + 10f, t) * 2f - 1f) * maxOffset * strength;
        float roll = (Mathf.PerlinNoise(seed + 20f, t) * 2f - 1f) * maxRoll * strength;

        transform.localPosition += new Vector3(x, y, 0f);
        transform.localRotation *= Quaternion.Euler(0f, 0f, roll);

        trauma = Mathf.Max(0f, trauma - decay * Time.deltaTime);
    }
}
