using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

//crowded in behind another of its kind, it climbs up onto its back instead of sitting inside it: the visual lifts
//and tips up the more it's squashed against the one ahead. bodies are never let closer than minGap
public class SwarmClimb : MonoBehaviour
{
    static readonly List<SwarmClimb> All = new List<SwarmClimb>();

    [Tooltip("The model to lift; the root stays on the ground for the navigation.")]
    public Transform visual;
    [Tooltip("Centres are never closer than this, so they're never fully inside each other.")]
    public float minGap = 2.6f;
    [Tooltip("Closer than this to one ahead of it, it starts to climb; at minGap it's fully up.")]
    public float climbFrom = 3.4f;
    [Tooltip("Metres it climbs when fully up.")]
    public float maxLift = 2.2f;
    [Tooltip("Degrees it tips up while climbing.")]
    public float maxPitch = 18f;
    public float settleSpeed = 3f;

    NavMeshAgent agent;
    Vector3 restPosition;
    Quaternion restRotation;
    float climb;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
        if (visual != null)
        {
            restPosition = visual.localPosition;
            restRotation = visual.localRotation;
        }
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void LateUpdate()
    {
        if (visual == null) return;

        float want = 0f;
        Vector3 me = transform.position, ahead = transform.forward;
        foreach (SwarmClimb other in All)
        {
            if (other == this) continue;
            Vector3 to = other.transform.position - me;
            to.y = 0f;
            float d = to.magnitude;
            if (d >= climbFrom) continue;

            //too close: each pushes itself out half the overlap
            if (d < minGap)
            {
                Vector3 away = d > 0.001f ? -to / d : Random.insideUnitSphere;
                away.y = 0f;
                Shift(away.normalized * ((minGap - d) * 0.5f));
            }

            //squashed against one straight ahead, it climbs onto it
            if (d > 0.001f && Vector3.Dot(to / d, ahead) > 0.7f)
                want = Mathf.Max(want, Mathf.InverseLerp(climbFrom, minGap, d));
        }

        climb = Mathf.MoveTowards(climb, want, settleSpeed * Time.deltaTime);
        visual.localPosition = restPosition + transform.InverseTransformVector(Vector3.up * (maxLift * climb));
        visual.localRotation = Quaternion.Euler(-maxPitch * climb, 0f, 0f) * restRotation;
    }

    //moves the body and keeps the agent with it, so the navigation doesn't pull it back
    void Shift(Vector3 by)
    {
        Vector3 p = transform.position + by;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.nextPosition = p;
            p.x = agent.nextPosition.x;
            p.z = agent.nextPosition.z;
        }
        transform.position = new Vector3(p.x, transform.position.y, p.z);
    }
}
