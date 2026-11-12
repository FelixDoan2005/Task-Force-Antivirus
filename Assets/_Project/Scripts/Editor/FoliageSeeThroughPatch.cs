using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

//adds the see-through dither to the Toby Foliage Engine grass shaders (paid pack, so it lives in the ignored folder and
//a re-import wipes it): run Tools > Foliage > Add See-Through To Grass Shaders again after importing the pack
public static class FoliageSeeThroughPatch
{
    const string Marker = "TTFE_SeeThroughClip";
    const string Dir = "Assets/Toby Fredson/The Toby Foliage Engine/(TTFE)_Core/Shaders/Default/";
    static readonly string[] Files = { "(TTFE) Grass Foliage.shader", "(TTFE) Grass Foliage (Mobile).shader" };
    static readonly string[] Passes = { "Forward", "DepthOnly", "DepthNormals", "GBuffer" };

    const string Function = @"
		float4 _TTFE_SeeThroughA;   // xyz start, w inner radius (fully see-through)
		float4 _TTFE_SeeThroughB;   // xyz end, w outer radius (fully solid); w = 0 turns it off
		void TTFE_SeeThroughClip(float3 posWS, float4 posCS)
		{
			float outer = _TTFE_SeeThroughB.w;
			if (outer <= 0.0) return;
			float3 a = _TTFE_SeeThroughA.xyz;
			float3 ab = _TTFE_SeeThroughB.xyz - a;
			float t = saturate(dot(posWS - a, ab) / max(dot(ab, ab), 0.0001));
			float d = distance(posWS, a + ab * t);
			float inner = _TTFE_SeeThroughA.w;
			float vis = saturate((d - inner) / max(outer - inner, 0.001));
			float n = frac(52.9829189 * frac(dot(posCS.xy, float2(0.06711056, 0.00583715))));
			clip(vis - n + 0.001);
		}
";

    [MenuItem("Tools/Foliage/Add See-Through To Grass Shaders")]
    public static void Apply()
    {
        foreach (string file in Files)
        {
            string path = Dir + file;
            if (!File.Exists(path)) { Debug.LogWarning("Missing " + path); continue; }
            string text = File.ReadAllText(path, Encoding.UTF8);
            if (text.Contains(Marker)) { Debug.Log(file + ": already patched"); continue; }

            string nl = text.Contains("\r\n") ? "\r\n" : "\n";
            string fn = Function.Replace("\r\n", "\n").Replace("\n", nl);

            //the shared block that every pass includes ends at the first ENDHLSL
            int shared = text.IndexOf("ENDHLSL", System.StringComparison.Ordinal);
            if (shared < 0) { Debug.LogWarning(file + ": no shared HLSL block"); continue; }
            text = text.Insert(shared, fn);

            int hits = 0;
            foreach (string pass in Passes)
            {
                int p = text.IndexOf("Name \"" + pass + "\"", System.StringComparison.Ordinal);
                if (p < 0) continue;
                int c = text.IndexOf("clip(Alpha - AlphaClipThreshold);", p, System.StringComparison.Ordinal);
                int next = text.IndexOf("Name \"", p + 8, System.StringComparison.Ordinal);
                if (c < 0 || (next >= 0 && c > next)) continue;
                text = text.Insert(c, "TTFE_SeeThroughClip(WorldPosition, input.positionCS);" + nl + "\t\t\t\t\t");
                hits++;
            }
            File.WriteAllText(path, text, new UTF8Encoding(false));
            Debug.Log(file + ": patched " + hits + " passes");
        }
        AssetDatabase.Refresh();
    }
}
