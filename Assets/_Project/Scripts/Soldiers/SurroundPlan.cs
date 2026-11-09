using System.Collections.Generic;
using UnityEngine;

//works out a closed square of shield walls round a point: how many full walls fit, how big each one is,
//a ring of swordsmen a soldier's length inside them, and who's left over to stand in the middle as captains
public class SurroundPlan
{
    public enum Row { Front, Behind, Captain, Inside }

    public struct Slot
    {
        public Vector3 position;
        public Vector3 facing;
        public int layer;   //0 is the outside wall, -1 a captain, -2 a swordsman inside the walls
        public Row row;
        public bool corner;
    }

    [System.Serializable]
    public class Settings
    {
        [Tooltip("Gap between neighbours in a wall. The knight's capsule is 0.8 wide, so at 1.1 the shields touch and nobody gets through.")]
        public float spacing = 1.1f;
        [Tooltip("Half-spacings between the front wall and the one tucked in right behind it.")]
        public int tightStep = 1;
        [Tooltip("Half-spacings between any further walls.")]
        public int backStep = 3;
        [Tooltip("Metres from the player to the inner wall at its closest. Every metre further needs about 8 more knights per wall.")]
        public float innerDistance = 8.8f;
        [Tooltip("Each extra wall needs the inner square this many gaps wider per side. Bigger squads stand a little further out, " +
                 "but far slower than their numbers grow, so the rest go into more walls.")]
        public int growthPerWall = 1;
        [Tooltip("Above 0, the second wall is paired with the front one: one man straight behind each front man, " +
                 "this far back and pairSide to his left (set from the captured spear wall pose).")]
        public float pairBehind;
        public float pairSide;
        [Tooltip("Most walls it builds; extra knights widen the square instead. 0 for no limit.")]
        public int maxLayers;
        [Tooltip("How far inside the inner wall the swordsmen stand: about one soldier's length.")]
        public float swordGap = 1.6f;
        [Tooltip("How far in from the inner wall (or the swordsmen) the captains stand.")]
        public float captainInset = 1.2f;
        [Tooltip("Gap between neighbours on the ring inside the walls. 0 uses the walls' spacing.")]
        public float ringSpacing = 0f;

        //each plan keeps its own copy, so changing the squad's settings later doesn't move a plan's places
        public Settings Copy() => (Settings)MemberwiseClone();
        [Tooltip("Captains never stand closer together than this.")]
        public float captainSpacing = 1.2f;
        [Tooltip("Captains never stand closer to the middle than this.")]
        public float captainMinRing = 0.9f;
        [Tooltip("A single wall kneels too. Otherwise only a front wall with another behind it kneels.")]
        public bool singleWallKneels = false;
        [Tooltip("Forming again after losses, the inner wall stays (spread a little thinner) while it still has this share " +
                 "of its knights. Below that, the rest of it stands inside as captains.")]
        [Range(0f, 1f)] public float innerWallMinFill = 0.5f;
    }

    public readonly List<Slot> Slots = new List<Slot>();   //walls outside first, then captains, then swordsmen

    public int Soldiers { get; private set; }
    public int Layers { get; private set; }
    public int[] Sides { get; private set; }        //gaps per side of each wall, outside first
    public int[] Counts { get; private set; }       //knights in each wall; fewer than 4 x its sides when it's short
    public int Captains { get; private set; }
    //front-wall corner men left out of a paired wall, standing in as captains instead
    public int CornerCaptains { get; private set; }
    public int Swords { get; private set; }
    public float SwordRing { get; private set; }
    public bool Loose { get; private set; }         //too few for a tight wall: one ring spread round the smallest square
    public float CaptainRing { get; private set; }
    public Vector3 Centre => centre;
    public Quaternion Frame => frame;

    Settings s;
    Vector3 centre;
    Quaternion frame;
    int wallSlots;

    float Half => s.spacing * 0.5f;
    float RingSpacing => s.ringSpacing > 0f ? s.ringSpacing : s.spacing;
    public Settings Used => s;
    public float HalfSize(int layer) => Sides[layer] * Half;
    public bool Short(int layer) => Counts[layer] < 4 * Sides[layer];

    //wall gaps per side of the smallest square: the inner distance, rounded up to whole gaps
    public static int MinSide(Settings s) => Mathf.Max(1, Mathf.CeilToInt(s.innerDistance / (s.spacing * 0.5f) - 0.001f));

    //soldiers make the walls; swords stand in their own ring inside them
    public static SurroundPlan Build(int soldiers, Settings settings, Vector3 centre, Vector3 facing, int swords = 0)
    {
        var plan = new SurroundPlan
        {
            s = settings,
            centre = centre,
            frame = Quaternion.LookRotation(facing.sqrMagnitude > 0.0001f ? facing : Vector3.forward),
            Soldiers = Mathf.Max(0, soldiers),
            Swords = Mathf.Max(0, swords)
        };
        plan.Choose();
        plan.Lay();
        return plan;
    }

    //forming again after losses: the same walls as last time, filled from the outside in, so the losses come out
    //of the inner wall. it stays, spread a little thinner, while it's mostly there; past that its knights stand
    //inside as captains. with more knights than last time, or too few for the outside wall, it starts afresh
    public static SurroundPlan Rebuild(int soldiers, SurroundPlan last, Settings settings, Vector3 centre, Vector3 facing, int swords = 0)
    {
        if (last == null || last.Loose || soldiers > last.Soldiers) return Build(soldiers, settings, centre, facing, swords);

        int captains = Mathf.Min(last.Captains - last.CornerCaptains, soldiers);
        int left = soldiers - captains;
        var counts = new List<int>();
        for (int i = 0; i < last.Layers && left > 0; i++)
        {
            int full = 4 * last.Sides[i];
            if (left >= full)
            {
                counts.Add(full);
                left -= full;
                continue;
            }
            if (left >= full * settings.innerWallMinFill)
            {
                counts.Add(left);
                left = 0;
            }
            break;
        }
        captains += left;

        int layers = counts.Count;
        if (layers == 0 || captains > CaptainRoom(last.Sides[layers - 1], settings)) return Build(soldiers, settings, centre, facing, swords);

        var plan = new SurroundPlan
        {
            s = settings,
            centre = centre,
            frame = Quaternion.LookRotation(facing.sqrMagnitude > 0.0001f ? facing : Vector3.forward),
            Soldiers = soldiers,
            Layers = layers,
            Sides = new int[layers],
            Counts = counts.ToArray(),
            Captains = captains,
            Swords = Mathf.Max(0, swords)
        };
        System.Array.Copy(last.Sides, plan.Sides, layers);
        plan.Lay();
        return plan;
    }

    //---------------------------------------------------------------- the sums

    //walls from the inside out: back walls backStep apart, then the front wall tightStep outside the last
    public static int[] SidesFor(int inner, int layers, Settings s)
    {
        var sides = new int[layers];
        int k = inner;
        for (int i = layers - 1; i >= 0; i--)
        {
            sides[i] = k;
            k += i == 1 ? s.tightStep : s.backStep;
        }
        return sides;
    }

    //smallest inner square that can have this many walls round it
    public static int NeededSide(int layers, Settings s) => MinSide(s) + (layers - 1) * s.growthPerWall;

    public static int KnightsFor(int layers, int innerSide, Settings s)
    {
        int total = 0;
        foreach (int k in SidesFor(innerSide, layers, s)) total += 4 * k;
        return total;
    }

    //as many walls as the squad can fill; spare knights widen them all a step at a time, and whoever's
    //left over, too few to widen every wall again, stands inside as a captain
    void Choose()
    {
        int most = s.maxLayers > 0 ? s.maxLayers : 49;
        for (int layers = 1; layers <= most && KnightsFor(layers, NeededSide(layers, s), s) <= Soldiers; layers++)
            Layers = layers;

        //not enough for even the smallest tight square: everyone in one ring round it, a bit spread out
        if (Layers > 0)
        {
            int inner = NeededSide(Layers, s);
            while (KnightsFor(Layers, inner + 1, s) <= Soldiers) inner++;

            Sides = SidesFor(inner, Layers, s);
            Counts = new int[Layers];
            for (int i = 0; i < Layers; i++) Counts[i] = 4 * Sides[i];
            Captains = Soldiers - KnightsFor(Layers, inner, s);
        }
        else
        {
            Loose = true;
            Layers = Soldiers > 0 ? 1 : 0;
            Sides = new[] { MinSide(s) };
            Counts = new[] { Soldiers };
            Captains = 0;
        }
    }

    //---------------------------------------------------------------- the places

    void Lay()
    {
        Slots.Clear();
        for (int layer = 0; layer < Layers; layer++)
        {
            if (Paired && layer == 1)
            {
                PairUp();
                continue;
            }
            bool kneels = layer == 0 && (Layers >= 2 || s.singleWallKneels);
            Ring(Counts[layer], HalfSize(layer), layer, kneels ? Row.Front : Row.Behind);
        }

        //paired, a kneeling man on a corner doesn't fit: he stands in the middle as a captain instead
        if (Paired)
        {
            CornerCaptains = Slots.RemoveAll(x => x.layer == 0 && x.corner);
            Counts[0] -= CornerCaptains;
            Captains += CornerCaptains;
        }
        wallSlots = Slots.Count;
        LayInside();
    }

    //the second wall copies the front one man for man
    bool Paired => s.pairBehind > 0f && Layers >= 2;

    //one man behind each front-wall man, at the pair's offset, facing the same way. short of men (after losses),
    //the ones there are spread evenly behind the front
    void PairUp()
    {
        int front = Counts[0], back = Counts[1];
        for (int i = 0; i < back; i++)
        {
            Slot f = Slots[i * front / back];
            Vector3 left = Vector3.Cross(f.facing, Vector3.up).normalized;
            //on a corner he stands square in it, straight back along the diagonal
            float side = f.corner ? 0f : s.pairSide;
            Slots.Add(new Slot { position = f.position - f.facing * s.pairBehind + left * side, facing = f.facing, layer = 1, row = Row.Behind, corner = f.corner });
        }
    }

    //how far out the innermost wall stands; with no wall at all, where it would be
    float InnerWall => Layers > 0 && Counts[Layers - 1] > 0
        ? HalfSize(Layers - 1) - (Paired && Layers == 2 ? s.pairBehind : 0f)
        : MinSide(s) * Half + s.swordGap;

    //evenly round the square from its front-left corner; a multiple of 4 puts one on every corner
    void Ring(int count, float half, int layer, Row row)
    {
        float perimeter = 8f * half;
        for (int i = 0; i < count; i++)
        {
            bool corner = Around(half, perimeter * i / count, out Vector3 local, out Vector3 normal);
            Slots.Add(new Slot { position = centre + frame * local, facing = frame * normal, layer = layer, row = row, corner = corner });
        }
    }

    //inside the walls: the swordsmen a soldier's length in (more rings further in if one won't hold them all),
    //and the captains inside them
    void LayInside()
    {
        Slots.RemoveRange(wallSlots, Slots.Count - wallSlots);

        var swordRings = new List<(int count, float half)>();
        float half = SwordRing = InnerWall - s.swordGap;
        for (int left = Swords; left > 0; half -= s.swordGap)
        {
            bool last = half - s.swordGap < s.captainMinRing + s.captainInset;
            int count = last ? left : Mathf.Min(left, Mathf.Max(1, Mathf.FloorToInt(8f * half / RingSpacing)));
            swordRings.Add((count, half));
            left -= count;
        }
        float innermost = swordRings.Count > 0 ? swordRings[swordRings.Count - 1].half : InnerWall;

        LayCaptains(innermost);
        foreach (var ring in swordRings) Ring(ring.count, ring.half, -2, Row.Inside);
    }

    //captains on circles inside the inner wall, outer circle first, each spread evenly from the front-left
    //diagonal; a circle keeps them evenly apart where a square would bunch them at its corners
    void LayCaptains(float inside)
    {
        if (Captains == 0) return;

        float radius = CaptainRing = Mathf.Max(s.captainMinRing, inside - s.captainInset);
        int left = Captains;
        for (int circle = 0; left > 0; circle++, radius -= s.captainSpacing)
        {
            bool last = radius - s.captainSpacing < s.captainMinRing;
            int count = last ? left : Mathf.Min(left, CircleRoom(radius, s));
            float start = -45f + (circle % 2 == 1 ? 180f / count : 0f);

            for (int i = 0; i < count; i++)
            {
                float angle = (start + 360f * i / count) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Slots.Add(new Slot { position = centre + frame * (dir * radius), facing = frame * dir, layer = -1, row = Row.Captain });
            }
            left -= count;
        }
    }

    public static int CircleRoom(float radius, Settings s)
    {
        if (radius < s.captainMinRing) return 0;
        float half = s.captainSpacing / (2f * radius);
        return half >= 1f ? 1 : Mathf.FloorToInt(Mathf.PI / Mathf.Asin(half) + 0.0001f);
    }

    public static int CaptainRoom(int innerSide, Settings s)
    {
        int room = 0;
        for (float r = innerSide * s.spacing * 0.5f - s.captainInset; r >= s.captainMinRing; r -= s.captainSpacing)
            room += CircleRoom(r, s);
        return room;
    }

    //one more captain (someone joined with every place taken). the captains spread out again to fit him; the
    //swordsmen's places after his move up one
    public int AddCaptain()
    {
        Captains++;
        LayInside();
        return wallSlots + Captains - 1;
    }

    //one more swordsman: the swordsmen spread out again to fit him; his place is the last
    public int AddSword()
    {
        Swords++;
        LayInside();
        return Slots.Count - 1;
    }

    //distance t round the square of half-size h, clockwise from the front-left corner. true on a corner
    static bool Around(float h, float t, out Vector3 local, out Vector3 normal)
    {
        float edge = 2f * h;
        int side = Mathf.Min(3, Mathf.FloorToInt(t / edge + 0.0001f));
        float along = Mathf.Max(0f, t - side * edge);

        Vector3 start, dir;
        switch (side)
        {
            case 0: start = new Vector3(-h, 0f, h); dir = Vector3.right; normal = Vector3.forward; break;
            case 1: start = new Vector3(h, 0f, h); dir = Vector3.back; normal = Vector3.right; break;
            case 2: start = new Vector3(h, 0f, -h); dir = Vector3.left; normal = Vector3.back; break;
            default: start = new Vector3(-h, 0f, -h); dir = Vector3.forward; normal = Vector3.left; break;
        }
        local = start + dir * along;

        //on a corner it faces out across the corner
        if (along < 0.01f)
        {
            Vector3 previous = side switch { 0 => Vector3.left, 1 => Vector3.forward, 2 => Vector3.right, _ => Vector3.back };
            normal = (normal + previous).normalized;
            return true;
        }
        return false;
    }

    //---------------------------------------------------------------- reading it out

    public int CountIn(int layer)
    {
        int n = 0;
        foreach (Slot slot in Slots) if (slot.layer == layer) n++;
        return n;
    }

    public string Describe()
    {
        if (Soldiers == 0) return "Surround: no soldiers";
        if (Loose)
            return string.Format("Surround: {0} knights - too few for a tight wall ({1} needed), so one ring {2:0.0}m square, {3:0.00}m apart",
                                 Soldiers, 4 * MinSide(s), 2f * HalfSize(0), 8f * HalfSize(0) / Soldiers);

        var walls = new System.Text.StringBuilder();
        for (int layer = 0; layer < Layers; layer++)
        {
            if (layer > 0) walls.Append(", ");
            if (Short(layer)) walls.AppendFormat("{0} of {1} ({2:0.0}m)", Counts[layer], 4 * Sides[layer], 2f * HalfSize(layer));
            else walls.AppendFormat("{0} ({1:0.0}m)", Counts[layer], 2f * HalfSize(layer));
        }
        return string.Format("Surround: {0} knights, {1:0.00}m apart -> {2} wall{3} outside in: {4}; {5} captain{6}{7}{8}",
                             Soldiers, s.spacing, Layers, Layers == 1 ? "" : "s", walls, Captains, Captains == 1 ? "" : "s",
                             Captains > 0 ? string.Format(" inside, {0:0.0}m from the middle", CaptainRing) : "",
                             Swords > 0 ? string.Format("; {0} swordsmen on a {1:0.0}m square inside the walls", Swords, 2f * SwordRing) : "");
    }
}
