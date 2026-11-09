using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

//Tools > Squad > Build Elf Soldiers: merges each elf's parts into one mesh, builds the green foot soldier, blue elite and
//red captain (models and soldier prefabs), and lines them up in the open scene to look at and tweak.
//safe to run again: weapon grips tweaked on the foot soldier model are kept
public static class SoldierModelBuilder
{
    const string ElfPrefabs = "Assets/Novozhilov3D/Elf_02/URP/Prefab/";
    const string ElfMaterials = "Assets/Novozhilov3D/Elf_02/URP/Materials/";
    const string Anims = "Assets/_Project/Art/Characters/Animations/Knight/";
    const string MeshDir = "Assets/_Project/Art/Characters/ElfSoldiers";
    const string SoldierDir = "Assets/_Project/Prefabs/Soldiers";
    const string ModelDir = SoldierDir + "/Models";
    const string OldSoldier = "Assets/_Project/Prefabs/PaladinSoldier.prefab";

    //the elf is a real-world 1.9m; this matches the player's 3m characters
    const float ModelScale = 1.62f;

    public const string FootSoldier = SoldierDir + "/FootSoldier.prefab";
    const string EliteSoldier = SoldierDir + "/EliteSoldier.prefab";
    const string CaptainSoldier = SoldierDir + "/CaptainSoldier.prefab";
    const string FootModel = ModelDir + "/ElfFootSoldier.prefab";
    const string EliteModel = ModelDir + "/ElfEliteSoldier.prefab";
    const string CaptainModel = ModelDir + "/ElfCaptain.prefab";
    const string SpearSoldier = SoldierDir + "/SpearSoldier.prefab";
    const string SpearModel = ModelDir + "/ElfSpearSoldier.prefab";

    //sword through the right fist, shield on the left (worked out from the knight animations' poses)
    static readonly Grip DefaultSword = new Grip("hand_r", new Vector3(0.1073f, -0.0746f, -0.1687f), new Quaternion(-0.04397f, 0.13770f, 0.73243f, 0.66532f));

    struct Grip
    {
        public string bone;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        public Grip(string bone, Vector3 position, Quaternion rotation)
        {
            this.bone = bone;
            this.position = position;
            this.rotation = rotation;
            scale = Vector3.one;
        }
    }

    [MenuItem("Tools/Squad/Build Elf Soldiers")]
    public static void Build()
    {
        Folder(MeshDir);
        Folder(SoldierDir);
        Folder(ModelDir);

        //keep any grips tweaked on the last build
        Grip sword = ReadGrip("SM_Sword") ?? DefaultSword;
        Grip shield = ReadGrip("SM_Shield") ?? ShieldFromKneel();
        Grip? spear = ReadGrip("SM_Glaive", SpearModel);

        //green foot soldier: the first premade as it comes. the others reuse its skeleton, so they only swap mesh and colours
        string[] bones;
        Material[] footMaterials;
        Mesh footMesh = Merge("Elf_02_UE4_Var_01", null, out bones, out footMaterials, "ElfKnight_Var01");
        GameObject footModel = BuildFootModel(footMesh, footMaterials, sword, shield);

        //blue elite: the third premade (closed helmet, blue cloth)
        Mesh eliteMesh = Merge("Elf_02_UE4_Var_03", bones, out _, out Material[] eliteMaterials, "ElfKnight_Var03");
        GameObject eliteModel = BuildVariant(footModel, EliteModel, eliteMesh, eliteMaterials);

        //red captain: the foot soldier's armour with the red cloth
        Material[] captainMaterials = footMaterials.Select(m => Swap(m, ("Cloth 1", "Cloth/Cloth 2"), ("Outfit 1", "Outfit/Outfit 2"))).ToArray();
        GameObject captainModel = BuildVariant(footModel, CaptainModel, null, captainMaterials);

        //spear elf: the foot soldier with the glaive in place of the sword
        GameObject spearModel = BuildSpearVariant(footModel, spear ?? SpearFromSword(sword));

        //the soldiers themselves: the paladin prefab becomes the foot soldier (the spawner keeps pointing at it)
        if (AssetDatabase.LoadAssetAtPath<GameObject>(FootSoldier) == null && AssetDatabase.LoadAssetAtPath<GameObject>(OldSoldier) != null)
        {
            string error = AssetDatabase.MoveAsset(OldSoldier, FootSoldier);
            if (!string.IsNullOrEmpty(error)) Debug.LogError(error);
        }
        BuildSoldier(FootSoldier, FootSoldier, footModel);
        BuildSoldier(FootSoldier, EliteSoldier, eliteModel);
        BuildSoldier(FootSoldier, CaptainSoldier, captainModel);
        BuildSoldier(FootSoldier, SpearSoldier, spearModel);

        AssetDatabase.SaveAssets();
        LineUp(footModel, eliteModel, captainModel, spearModel);

        Debug.Log(string.Format("Elf soldiers built: {0} parts merged into 1 mesh ({1} materials, {2} triangles) + sword and shield",
            AssetDatabase.LoadAssetAtPath<GameObject>(ElfPrefabs + "Elf_02_UE4_Var_01.prefab").GetComponentsInChildren<SkinnedMeshRenderer>().Length,
            footMaterials.Length, footMesh.triangles.Length / 3));
    }

    //---------------------------------------------------------------- merging

    //every part of a premade elf as one skinned mesh, one submesh per material. bones in the given order (or the
    //premade's own skeleton order), so all the soldiers share one skeleton and can swap meshes
    static Mesh Merge(string premade, string[] boneOrder, out string[] bones, out Material[] materials, string assetName)
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var elf = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ElfPrefabs + premade + ".prefab"), scene);
            Transform root = elf.transform;

            //the parts' meshes have to be readable to be merged
            foreach (string model in root.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r => AssetDatabase.GetAssetPath(r.sharedMesh)).Distinct())
                if (AssetImporter.GetAtPath(model) is ModelImporter importer && !importer.isReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }

            Transform[] skeleton = root.Find("Armature").GetComponentsInChildren<Transform>(true);
            bones = boneOrder ?? skeleton.Select(t => t.name).ToArray();

            var boneIndex = new Dictionary<string, int>();
            for (int i = 0; i < bones.Length; i++)
                if (!boneIndex.ContainsKey(bones[i])) boneIndex[bones[i]] = i;

            var bindposes = new Matrix4x4[bones.Length];
            var bound = new bool[bones.Length];
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uvs = new List<Vector2>();
            var perVertex = new List<byte>();
            var weights = new List<BoneWeight1>();
            var groups = new List<(Material material, List<int> triangles)>();
            var shapes = new List<(string name, float weight, int start, Vector3[] dv, Vector3[] dn, Vector3[] dt)>();
            var shapeNames = new HashSet<string>();
            float worstBind = 0f;

            foreach (SkinnedMeshRenderer part in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                Mesh m = part.sharedMesh;
                Matrix4x4 toBody = root.worldToLocalMatrix * part.transform.localToWorldMatrix;
                Matrix4x4 fromBody = toBody.inverse;
                int start = vertices.Count;

                vertices.AddRange(m.vertices.Select(v => toBody.MultiplyPoint3x4(v)));
                Vector3[] n0 = m.normals;
                normals.AddRange(n0.Length == m.vertexCount ? n0.Select(n => toBody.MultiplyVector(n).normalized) : Enumerable.Repeat(Vector3.up, m.vertexCount));
                Vector4[] t = m.tangents;
                tangents.AddRange(t.Length == m.vertexCount
                    ? t.Select(x => { Vector3 d = toBody.MultiplyVector(new Vector3(x.x, x.y, x.z)).normalized; return new Vector4(d.x, d.y, d.z, x.w); })
                    : Enumerable.Repeat(new Vector4(1, 0, 0, 1), m.vertexCount));
                Vector2[] uv = m.uv;
                uvs.AddRange(uv.Length == m.vertexCount ? uv : new Vector2[m.vertexCount]);

                //its bones onto the shared skeleton; the bind pose expressed from the merged mesh's space
                Transform[] partBones = part.bones;
                Matrix4x4[] partBind = m.bindposes;
                var remap = new int[partBones.Length];
                for (int b = 0; b < partBones.Length; b++)
                {
                    if (partBones[b] == null || !boneIndex.TryGetValue(partBones[b].name, out int c)) c = 0;
                    remap[b] = c;
                    Matrix4x4 bp = partBind[b] * fromBody;
                    if (!bound[c]) { bindposes[c] = bp; bound[c] = true; }
                    else for (int k = 0; k < 16; k++) worstBind = Mathf.Max(worstBind, Mathf.Abs(bindposes[c][k] - bp[k]));
                }
                NativeArray<byte> bpv = m.GetBonesPerVertex();
                NativeArray<BoneWeight1> bw = m.GetAllBoneWeights();
                perVertex.AddRange(bpv);
                foreach (BoneWeight1 w in bw) weights.Add(new BoneWeight1 { boneIndex = remap[w.boneIndex], weight = w.weight });

                //triangles grouped by material, so the whole knight is a handful of draws
                Material[] mats = part.sharedMaterials;
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    Material mat = mats[Mathf.Min(s, mats.Length - 1)];
                    int g = groups.FindIndex(x => x.material == mat);
                    if (g < 0) { groups.Add((mat, new List<int>())); g = groups.Count - 1; }
                    groups[g].triangles.AddRange(m.GetTriangles(s).Select(i => i + start));
                }

                //face shapes carried over, renamed only if another part already used the name
                for (int s = 0; s < m.blendShapeCount; s++)
                {
                    string shapeName = m.GetBlendShapeName(s);
                    for (int n = 2; !shapeNames.Add(shapeName); n++) shapeName = m.GetBlendShapeName(s) + "_" + n;

                    for (int f = 0; f < m.GetBlendShapeFrameCount(s); f++)
                    {
                        var dv = new Vector3[m.vertexCount];
                        var dn = new Vector3[m.vertexCount];
                        var dt = new Vector3[m.vertexCount];
                        m.GetBlendShapeFrameVertices(s, f, dv, dn, dt);
                        for (int i = 0; i < dv.Length; i++)
                        {
                            dv[i] = toBody.MultiplyVector(dv[i]);
                            dn[i] = toBody.MultiplyVector(dn[i]);
                            dt[i] = toBody.MultiplyVector(dt[i]);
                        }
                        shapes.Add((shapeName, m.GetBlendShapeFrameWeight(s, f), start, dv, dn, dt));
                    }
                }
            }

            //bones no part uses still need a bind pose: their rest pose
            Transform Bone(string name) => skeleton.FirstOrDefault(x => x.name == name);
            for (int c = 0; c < bones.Length; c++)
                if (!bound[c] && Bone(bones[c]) != null) bindposes[c] = Bone(bones[c]).worldToLocalMatrix * root.localToWorldMatrix;
            if (worstBind > 0.001f) Debug.LogWarning(premade + ": parts disagree on bind poses by up to " + worstBind);

            var mesh = new Mesh { name = assetName };
            mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = groups.Count;
            for (int g = 0; g < groups.Count; g++) mesh.SetTriangles(groups[g].triangles, g);
            mesh.bindposes = bindposes;
            using (var bpvAll = new NativeArray<byte>(perVertex.ToArray(), Allocator.Temp))
            using (var bwAll = new NativeArray<BoneWeight1>(weights.ToArray(), Allocator.Temp))
                mesh.SetBoneWeights(bpvAll, bwAll);

            foreach (var shape in shapes)
            {
                var dv = new Vector3[vertices.Count];
                var dn = new Vector3[vertices.Count];
                var dt = new Vector3[vertices.Count];
                System.Array.Copy(shape.dv, 0, dv, shape.start, shape.dv.Length);
                System.Array.Copy(shape.dn, 0, dn, shape.start, shape.dn.Length);
                System.Array.Copy(shape.dt, 0, dt, shape.start, shape.dt.Length);
                mesh.AddBlendShapeFrame(shape.name, shape.weight, dv, dn, dt);
            }
            mesh.RecalculateBounds();

            materials = groups.Select(g => g.material).ToArray();
            return SaveMesh(mesh, MeshDir + "/" + assetName + ".asset");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    //overwrites in place, so everything pointing at the mesh keeps pointing at it
    static Mesh SaveMesh(Mesh mesh, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        EditorUtility.CopySerialized(mesh, existing);
        existing.name = mesh.name;
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    //---------------------------------------------------------------- models

    static GameObject BuildFootModel(Mesh mesh, Material[] materials, Grip sword, Grip shield)
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var elf = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ElfPrefabs + "Elf_02_UE4_Var_01.prefab"), scene);
            PrefabUtility.UnpackPrefabInstance(elf, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            elf.name = "ElfFootSoldier";
            elf.transform.localScale = Vector3.one * ModelScale;
            Transform[] skeleton = elf.transform.Find("Armature").GetComponentsInChildren<Transform>(true);
            Transform rootBone = elf.GetComponentInChildren<SkinnedMeshRenderer>().rootBone;

            //the separate parts go; one body takes their place
            foreach (var part in elf.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Object.DestroyImmediate(part.gameObject);
            var body = new GameObject("Body");
            body.transform.SetParent(elf.transform, false);
            var skin = body.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.bones = skeleton;
            skin.rootBone = rootBone;
            skin.sharedMaterials = materials;
            skin.localBounds = new Bounds(rootBone.InverseTransformPoint(elf.transform.position + Vector3.up), new Vector3(3f, 3f, 3f));

            var animator = elf.GetComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Anims + "KnightHumanoid.overrideController");
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            elf.AddComponent<SoldierAnimatorDriver>();

            Hold(elf, "SM_Sword", sword);
            Hold(elf, "SM_Shield", shield);
            elf.AddComponent<ShieldWallBrace>().shield = elf.GetComponentsInChildren<Transform>(true).First(t => t.name == "SM_Shield");
            elf.AddComponent<IdleStance>();

            return PrefabUtility.SaveAsPrefabAsset(elf, FootModel);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static void Hold(GameObject elf, string weapon, Grip grip)
    {
        Transform hand = elf.GetComponentsInChildren<Transform>(true).First(t => t.name == grip.bone);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ElfPrefabs + weapon + ".prefab"), hand);
        go.transform.localPosition = grip.position;
        go.transform.localRotation = grip.rotation;
        go.transform.localScale = grip.scale;
        foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
    }

    //a prefab variant of the foot soldier: same skeleton, grips and animator, its own mesh and colours
    static GameObject BuildVariant(GameObject footModel, string path, Mesh mesh, Material[] materials)
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(footModel, scene);
            model.name = System.IO.Path.GetFileNameWithoutExtension(path);
            var skin = model.transform.Find("Body").GetComponent<SkinnedMeshRenderer>();
            if (mesh != null) skin.sharedMesh = mesh;
            skin.sharedMaterials = materials;
            return PrefabUtility.SaveAsPrefabAsset(model, path);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static GameObject BuildSpearVariant(GameObject footModel, Grip glaive)
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(footModel, scene);
            model.name = System.IO.Path.GetFileNameWithoutExtension(SpearModel);
            model.GetComponentsInChildren<Transform>(true).First(t => t.name == "SM_Sword").gameObject.SetActive(false);
            Hold(model, "SM_Glaive", glaive);
            //the spearman's back-row shield: upright, in front, as posed on the dummy
            var shieldBrace = model.GetComponent<ShieldWallBrace>();
            //both rows' shields tilted back about 26 degrees so they stack like tiles; the standing row's held further
            //forward and higher, over the kneeling row's (the pair on the dummies)
            shieldBrace.frontHold = new Vector3(-0.01f, 0.78f, 0.645f);
            shieldBrace.frontElbow = new Vector3(0.209f, 0.834f, 0.568f);
            shieldBrace.frontLean = -26.2f;
            shieldBrace.backHold = new Vector3(-0.085f, 1.172f, 0.801f);
            shieldBrace.backElbow = new Vector3(0.108f, 1.212f, 0.576f);
            shieldBrace.lean = -26.2f;
            var brace = model.AddComponent<SpearWallBrace>();
            brace.spear = model.GetComponentsInChildren<Transform>(true).First(t => t.name == "SM_Glaive");

            //with a pose captured off the dummies, the wall takes it exactly, bone for bone, instead of the IK braces
            var captured = AssetDatabase.LoadAssetAtPath<WallPoseData>(WallPoseCapture.Asset);
            if (captured != null)
            {
                var applier = model.AddComponent<WallPoseApplier>();
                applier.pose = captured;
                applier.shield = model.GetComponentsInChildren<Transform>(true).First(t => t.name == "SM_Shield");
                applier.spear = brace.spear;
                brace.enabled = false;
                shieldBrace.enabled = false;
            }
            return PrefabUtility.SaveAsPrefabAsset(model, SpearModel);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    //the glaive held like the sword, blade the same way, the hand a third of the way up its shaft
    static Grip SpearFromSword(Grip sword)
    {
        Vector3 swordGrip = new Vector3(0f, 0.035f, 0.17f), shaft = new Vector3(0f, 0.03f, 0.75f);
        return new Grip(sword.bone, sword.position + sword.rotation * (swordGrip - shaft), sword.rotation);
    }

    static Material Swap(Material m, params (string from, string to)[] swaps)
    {
        foreach (var (from, to) in swaps)
            if (m.name == from) return AssetDatabase.LoadAssetAtPath<Material>(ElfMaterials + to + ".mat") ?? m;
        return m;
    }

    //the grip a weapon has on the current foot soldier model, so a rebuild keeps tweaks
    static Grip? ReadGrip(string weapon, string modelPath = FootModel)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        Transform t = model != null ? model.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == weapon) : null;
        if (t == null) return null;
        return new Grip(t.parent.name, t.localPosition, t.localRotation) { scale = t.localScale };
    }

    //the shield's grip, set in the kneeling shield wall where it has to stand upright in front, face forward
    static Grip ShieldFromKneel()
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var elf = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ElfPrefabs + "Elf_02_UE4_Var_01.prefab"), scene);
            var animator = elf.GetComponent<Animator>();
            Clip("Knight_ShieldWall_Bottom").SampleAnimation(elf, Clip("Knight_ShieldWall_Bottom").length * 0.5f);

            Transform wrist = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Vector3 along = animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal).position - wrist.position;
            float length = along.magnitude;
            along /= length;
            Vector3 curl = animator.GetBoneTransform(HumanBodyBones.LeftMiddleDistal).position - animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal).position;
            Vector3 palm = (curl - along * Vector3.Dot(curl, along)).normalized;
            Vector3 fist = wrist.position + along * 0.85f * length + palm * 0.4f * length;

            //decorated face (-y) forward, top (+z) up, the fist on the handle at the back
            Quaternion rotation = Quaternion.LookRotation(Vector3.up, -Vector3.forward);
            Vector3 position = fist - rotation * new Vector3(0f, 0.06f, 0.68f);   //the handle on the shield's back
            return new Grip(wrist.name, wrist.InverseTransformPoint(position), Quaternion.Inverse(wrist.rotation) * rotation);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    //---------------------------------------------------------------- soldiers

    //the gameplay soldier (AI, health, navigation) with this model under Visual
    static void BuildSoldier(string from, string path, GameObject model)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(from);
        try
        {
            Transform visual = root.transform.Find("Visual");
            if (visual == null)
            {
                visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
            }
            foreach (Transform old in visual.Cast<Transform>().ToList()) Object.DestroyImmediate(old.gameObject);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, visual);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            var driver = instance.GetComponent<SoldierAnimatorDriver>();
            driver.soldier = root.GetComponent<SoldierAI>();
            driver.health = root.GetComponent<SoldierHealth>();
            root.GetComponent<SoldierAI>().spearman = path == SpearSoldier;
            root.GetComponent<SoldierAI>().captain = path == CaptainSoldier;

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    //---------------------------------------------------------------- the lineup

    //in the open scene, past the virus showcase: the three kinds side by side, then the foot soldier in each pose
    //its weapons have to look right in. not saved; undo removes it
    static void LineUp(GameObject foot, GameObject elite, GameObject captain, GameObject spear)
    {
        Scene scene = SceneManager.GetActiveScene();
        foreach (GameObject old in scene.GetRootGameObjects().Where(g => g.name == "SoldierShowcase")) Undo.DestroyObjectImmediate(old);

        var root = new GameObject("SoldierShowcase");
        Undo.RegisterCreatedObjectUndo(root, "Soldier showcase");

        Place(root.transform, foot, "Foot Soldier (green)", new Vector3(-4f, 0f, 46f), "Knight_Idle", 0f);
        Place(root.transform, elite, "Elite (blue)", new Vector3(0f, 0f, 46f), "Knight_Idle", 0f);
        Place(root.transform, captain, "Captain (red)", new Vector3(4f, 0f, 46f), "Knight_Idle", 0f);
        Place(root.transform, spear, "Spear Elf (green)", new Vector3(8f, 0f, 46f), "Knight_Idle", 0f);
        Place(root.transform, spear, "Spear Elf - Attack", new Vector3(12f, 0f, 46f), "Knight_Attack1", 0.45f);

        var poses = new (string clip, float time, string label)[]
        {
            ("Knight_Idle", 0f, "Idle"), ("Knight_Attack1", 0.45f, "Attack"), ("Knight_Block", 0.35f, "Block"),
            ("Knight_ShieldWall_Bottom", 0.5f, "Shield Wall Bottom"), ("Knight_ShieldWall_Top", 0.5f, "Shield Wall Top")
        };
        for (int i = 0; i < poses.Length; i++)
            Place(root.transform, foot, "Pose - " + poses[i].label, new Vector3(-8f + 4f * i, 0f, 50f), poses[i].clip, poses[i].time);

        Selection.activeGameObject = root;
    }

    static void Place(Transform parent, GameObject model, string label, Vector3 position, string clip, float time)
    {
        var holder = new GameObject(label);
        holder.transform.SetParent(parent, false);
        holder.transform.localPosition = position;
        holder.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, holder.transform);
        var pose = holder.AddComponent<ShowcasePose>();
        pose.target = instance.GetComponent<Animator>();
        pose.clip = Clip(clip);
        pose.time = time;
        pose.Apply();
    }

    //----------------------------------------------------------------

    static AnimationClip Clip(string name) =>
        AssetDatabase.LoadAllAssetsAtPath(Anims + name + ".fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));

    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        Folder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
