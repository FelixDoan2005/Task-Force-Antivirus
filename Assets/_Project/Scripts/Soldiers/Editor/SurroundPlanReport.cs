using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

//Tools > Squad > Surround Layout Report: measures the knight, then lays out the surround for every squad
//size and checks each layout, so the numbers to spawn can be read straight off
public static class SurroundPlanReport
{
    const string Prefab = SoldierModelBuilder.FootSoldier;
    const string Clips = "Assets/_Project/Art/Characters/Animations/Knight/";

    [MenuItem("Tools/Squad/Surround Layout Report")]
    public static void Run()
    {
        var settings = new SurroundPlan.Settings();
        var report = new StringBuilder();

        report.AppendLine(MeasureKnight());
        report.AppendLine(string.Format("Wall spacing {0:0.00}m, front wall {1:0.00}m outside the next, further walls {2:0.00}m apart, inner wall at least {3:0.0}m from the player",
            settings.spacing, settings.tightStep * settings.spacing / 2f, settings.backStep * settings.spacing / 2f,
            SurroundPlan.MinSide(settings) * settings.spacing / 2f));

        //a loose ring still stops the player while the gaps between capsules are narrower than he is
        float perimeter = 8f * SurroundPlan.MinSide(settings) * settings.spacing / 2f;
        report.AppendLine(string.Format("Too few for a tight wall, they spread round it; that still stops the player (1.0m wide, knights 0.8m) from {0} knights",
            Mathf.CeilToInt(perimeter / 1.8f)));
        report.AppendLine();
        report.AppendLine("knights | walls (outside in)        | inner sq | outer sq | captains");

        int failures = 0;
        int lastLayers = -1;
        var firstWith = new int[8];
        var exact = new System.Collections.Generic.List<int>[8];
        for (int i = 0; i < exact.Length; i++) exact[i] = new System.Collections.Generic.List<int>();

        for (int n = 1; n <= 300; n++)
        {
            SurroundPlan plan = SurroundPlan.Build(n, settings, Vector3.zero, Vector3.forward);
            failures += Check(plan, settings, report);

            int layers = plan.Loose ? 0 : plan.Layers;
            if (layers < lastLayers)
            {
                failures++;
                report.AppendLine(string.Format("  ! {0} knights: {1} walls, but {2} with one fewer", n, layers, lastLayers));
            }
            if (layers < firstWith.Length && firstWith[layers] == 0) firstWith[layers] = n;
            if (!plan.Loose && plan.Captains == 0 && layers < exact.Length) exact[layers].Add(n);

            //a row whenever the shape changes, and every 10
            bool changed = layers != lastLayers;
            lastLayers = layers;
            if (!changed && n % 20 != 0) continue;

            string walls = plan.Loose ? "loose ring of " + n
                : string.Join(", ", Enumerable.Range(0, plan.Layers).Select(l => plan.CountIn(l).ToString()));
            report.AppendLine(string.Format("{0,7} | {1,-25} | {2,7:0.0}m | {3,7:0.0}m | {4}",
                n, walls, 2f * plan.HalfSize(plan.Layers - 1), 2f * plan.HalfSize(0), plan.Captains));
        }

        report.AppendLine();
        for (int layers = 1; layers < firstWith.Length; layers++)
        {
            if (firstWith[layers] == 0) continue;
            report.AppendLine(string.Format("{0} wall{1}: from {2} knights. No captains at: {3}", layers, layers == 1 ? "" : "s",
                firstWith[layers], string.Join(", ", exact[layers].Take(8))));
        }
        //forming again after losses keeps the outer walls and gives up the inner ones first
        report.AppendLine();
        foreach (int start in new[] { 100, 160, 260 })
        {
            SurroundPlan first = SurroundPlan.Build(start, settings, Vector3.zero, Vector3.forward);
            var line = new StringBuilder(string.Format("Re-forming {0} ({1}) after losing", start, Walls(first)));
            foreach (int lost in new[] { 1, 5, 20, 40, 60 })
            {
                SurroundPlan again = SurroundPlan.Rebuild(start - lost, first, settings, Vector3.zero, Vector3.forward);
                failures += Check(again, settings, report);
                line.AppendFormat("  {0}: {1} +{2}c", lost, Walls(again), again.Captains);
            }
            report.AppendLine(line.ToString());
        }

        //a front knight falls: the one stepping up from the wall behind has to be right behind him
        float worstStep = 0f;
        foreach (int n in new[] { 140, 200, 244, 300 })
        {
            SurroundPlan plan = SurroundPlan.Build(n, settings, Vector3.zero, Vector3.forward);
            for (int layer = 0; layer + 1 < plan.Layers; layer++)
                foreach (var gap in plan.Slots.Where(x => x.layer == layer))
                    worstStep = Mathf.Max(worstStep, plan.Slots.Where(x => x.layer == layer + 1).Min(x => Vector3.Distance(x.position, gap.position)));
        }
        report.AppendLine(string.Format("Longest step up into a fallen knight's place: {0:0.00}m", worstStep));

        report.AppendLine(failures == 0 ? "All layouts checked out." : failures + " layout problems (see above).");

        Debug.Log(report.ToString());
    }

    static string Walls(SurroundPlan plan) =>
        plan.Loose ? "loose " + plan.Soldiers
        : string.Join("/", Enumerable.Range(0, plan.Layers).Select(l => plan.Short(l) ? plan.Counts[l] + " of " + 4 * plan.Sides[l] : plan.Counts[l].ToString()));

    //every knight placed once; walls closed and evenly spaced; captains inside, apart, clear of the middle
    static int Check(SurroundPlan plan, SurroundPlan.Settings s, StringBuilder report)
    {
        int bad = 0;
        void Fail(string why) { bad++; report.AppendLine(string.Format("  ! {0} knights: {1}", plan.Soldiers, why)); }

        if (plan.Slots.Count != plan.Soldiers) Fail(plan.Slots.Count + " places");

        for (int layer = 0; layer < plan.Layers; layer++)
        {
            var ring = plan.Slots.Where(x => x.layer == layer).Select(x => x.position).ToList();
            if (ring.Count != plan.Counts[layer]) Fail("wall " + layer + " has " + ring.Count + " of " + plan.Counts[layer]);

            //a full wall is exactly one spacing apart; a short or loose one evenly round the same square
            bool full = !plan.Short(layer);
            float expected = full ? s.spacing : 8f * plan.HalfSize(layer) / ring.Count;
            for (int i = 0; i < ring.Count; i++)
            {
                float gap = Vector3.Distance(ring[i], ring[(i + 1) % ring.Count]);
                if (full ? Mathf.Abs(gap - expected) > 0.01f : gap > expected + 0.01f) { Fail("uneven gap " + gap.ToString("0.00") + " in wall " + layer); break; }
            }
        }

        var captains = plan.Slots.Where(x => x.layer < 0).Select(x => x.position).ToList();
        if (captains.Count != plan.Captains) Fail("captain count");
        for (int i = 0; i < captains.Count; i++)
        {
            if (new Vector2(captains[i].x, captains[i].z).magnitude < s.captainMinRing - 0.01f) Fail("captain too near the middle");
            if (Mathf.Max(Mathf.Abs(captains[i].x), Mathf.Abs(captains[i].z)) > plan.HalfSize(plan.Layers - 1) - 1f) Fail("captain against the wall");
            for (int j = i + 1; j < captains.Count; j++)
                if (Vector3.Distance(captains[i], captains[j]) < s.captainSpacing - 0.01f) { Fail("captains bunched"); j = captains.Count; i = captains.Count; }
        }
        return bad;
    }

    //the knight's real size in its poses, from the prefab posed off-scene
    static string MeasureKnight()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            Animator animator = root.GetComponentInChildren<Animator>();
            var capsule = root.GetComponent<CapsuleCollider>();
            var line = new StringBuilder("Knight: capsule " + (capsule != null ? (2f * capsule.radius).ToString("0.00") + "m wide" : "none"));

            foreach (string name in new[] { "Knight_Idle", "Knight_ShieldWall_Bottom", "Knight_ShieldWall_Top" })
            {
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(Clips + name + ".fbx").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
                if (clip == null) continue;
                clip.SampleAnimation(animator.gameObject, 0f);
                Bounds b = PosedBounds(root);
                line.AppendFormat("; {0} {1:0.00}w x {2:0.00}d x {3:0.00}h", name.Replace("Knight_", ""), b.size.x, b.size.z, b.size.y);
            }
            return line.ToString();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Bounds PosedBounds(GameObject root)
    {
        Matrix4x4 toRoot = root.transform.worldToLocalMatrix;
        var bounds = new Bounds();
        bool any = false;
        var baked = new Mesh();

        void Add(Vector3 p)
        {
            if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
            else bounds.Encapsulate(p);
        }

        foreach (var skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            skinned.BakeMesh(baked, true);
            Matrix4x4 m = toRoot * Matrix4x4.TRS(skinned.transform.position, skinned.transform.rotation, Vector3.one);
            foreach (Vector3 v in baked.vertices) Add(m.MultiplyPoint3x4(v));
        }
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
            Matrix4x4 m = toRoot * filter.transform.localToWorldMatrix;
            foreach (Vector3 v in filter.sharedMesh.vertices) Add(m.MultiplyPoint3x4(v));
        }
        Object.DestroyImmediate(baked);
        return bounds;
    }
}
