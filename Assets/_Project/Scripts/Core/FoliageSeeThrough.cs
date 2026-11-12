using UnityEngine;

//puts a see-through tunnel in the foliage in front of the camera so crouching or aiming in tall grass doesn't blind you.
//the grass shader reads the two globals; set the second one's w to 0 and the effect is off
[DefaultExecutionOrder(1000)]
public class FoliageSeeThrough : MonoBehaviour
{
    [Header("Always on: a bubble round the lens")]
    public float bubbleInner = 0.5f;
    public float bubbleOuter = 1.3f;

    [Header("Crouching or aiming: a tunnel along where you look")]
    public float tunnelLength = 16f;
    public float tunnelInner = 0.8f;
    public float tunnelOuter = 2.0f;
    public float blendSpeed = 5f;

    static readonly int A = Shader.PropertyToID("_TTFE_SeeThroughA");
    static readonly int B = Shader.PropertyToID("_TTFE_SeeThroughB");

    PlayerController player;
    MasterArcher archer;
    float amount;

    void Start()
    {
        player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        archer = FindFirstObjectByType<MasterArcher>(FindObjectsInactive.Include);
    }

    void LateUpdate()
    {
        bool aiming = archer != null && archer.isActiveAndEnabled && archer.IsAiming;
        bool low = player != null && player.IsCrouching;
        amount = Mathf.MoveTowards(amount, aiming || low ? 1f : 0f, blendSpeed * Time.unscaledDeltaTime);

        Vector3 from = transform.position;
        Vector3 to = from + transform.forward * Mathf.Lerp(0f, tunnelLength, amount);
        float inner = Mathf.Lerp(bubbleInner, tunnelInner, amount);
        float outer = Mathf.Lerp(bubbleOuter, tunnelOuter, amount);

        Shader.SetGlobalVector(A, new Vector4(from.x, from.y, from.z, inner));
        Shader.SetGlobalVector(B, new Vector4(to.x, to.y, to.z, outer));
    }

    void OnDisable()
    {
        Shader.SetGlobalVector(A, Vector4.zero);
        Shader.SetGlobalVector(B, Vector4.zero);
    }
}
