using UnityEngine;

//a ring that races outward along the ground. built from a LineRenderer because gizmos and
//Debug.DrawLine only ever draw in the scene view, and this one has to be visible in the game.
//
//everything about how it LOOKS lives on the prefab and is editable in the inspector: the colour
//over its life, how thick it is, how it eases outward, how long it lasts, how high it floats.
//the radius it grows to is handed in by whatever fired it, so editing an ability's radius keeps
//the effect matching instead of the two drifting apart.
[RequireComponent(typeof(LineRenderer))]
public class ExpandingRingVFX : MonoBehaviour
{
    [Header("Shape")]
    [Tooltip("How many points the ring is drawn from. Higher is rounder and costs a little more.")]
    [Range(8, 256)]
    public int segments = 72;

    [Tooltip("Fraction of the full radius it starts at. 0 starts at a point in the middle.")]
    [Range(0f, 1f)]
    public float startRadiusFraction = 0.08f;

    [Tooltip("Lifts it off the floor so it does not z-fight with the ground.")]
    public float heightAboveGround = 0.08f;

    [Header("Timing")]
    [Tooltip("Seconds from the middle to the full radius.")]
    public float duration = 0.7f;

    [Tooltip("How it eases outward over that time. A steep start reads as a shockwave; a gentle " +
             "one reads as a slow swell.")]
    public AnimationCurve growth = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Look")]
    [Tooltip("Colour and transparency across its life. The alpha at the end is what fades it out.")]
    public Gradient colourOverLife = new Gradient();

    [Tooltip("Thickness across its life, multiplied by Width.")]
    public AnimationCurve widthOverLife = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    public float width = 0.45f;

    [Header("Lifetime")]
    [Tooltip("On, it removes itself once it has finished. Off, it sits there - useful while you " +
             "are editing one in the scene.")]
    public bool destroyWhenDone = true;

    LineRenderer line;
    float radius = 1f;
    float age;
    bool playing;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = false;     //so the ring follows this object's transform
        line.loop = true;
    }

    //called by whatever set it off. without this it still plays, just at whatever radius it was
    //last given, so dropping the prefab into a scene to look at it works fine.
    public void Play(float targetRadius)
    {
        radius = Mathf.Max(0.01f, targetRadius);
        age = 0f;
        playing = true;

        if (line == null) line = GetComponent<LineRenderer>();
        Apply();
    }

    void OnEnable()
    {
        if (!playing) { age = 0f; playing = true; }
    }

    void Update()
    {
        if (!playing) return;

        age += Time.deltaTime;

        if (age >= duration)
        {
            if (destroyWhenDone) { Destroy(gameObject); return; }

            age = duration;
            playing = false;
        }

        Apply();
    }

    void Apply()
    {
        if (line == null) return;

        float t = duration > 0f ? Mathf.Clamp01(age / duration) : 1f;

        float eased = growth.Evaluate(t);
        float r = Mathf.Lerp(radius * startRadiusFraction, radius, eased);

        if (line.positionCount != segments) line.positionCount = segments;

        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(a) * r, heightAboveGround, Mathf.Sin(a) * r));
        }

        Color c = colourOverLife.Evaluate(t);
        line.startColor = c;
        line.endColor = c;

        line.widthMultiplier = width * widthOverLife.Evaluate(t);
    }

    //so a freshly added one looks like something straight away instead of an invisible black ring
    void Reset()
    {
        colourOverLife = DefaultGradient(new Color(0.3f, 0.85f, 1f));
    }

    public static Gradient DefaultGradient(Color tint)
    {
        Gradient g = new Gradient();

        g.SetKeys(
            new[] { new GradientColorKey(tint, 0f), new GradientColorKey(tint, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(0f, 1f) });

        return g;
    }
}
