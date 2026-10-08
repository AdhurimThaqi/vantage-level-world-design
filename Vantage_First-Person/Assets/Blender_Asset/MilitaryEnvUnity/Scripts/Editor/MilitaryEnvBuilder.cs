// Military Training Environment - Unity importer
// Menu: Tools > Military Env > Build Everything
// Works with URP (recommended) and Built-in. HDRP: materials need manual conversion.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class MilitaryEnvBuilder
{
    // ---------------- JSON data classes ----------------
    [Serializable] class Item { public string k; public string c; public float[] p; public float[] q; public float[] s; }
    [Serializable] class LightItem { public string k; public float[] p; public float[] fwd; public float[] up; public float energy; public float[] color; }
    [Serializable] class CamItem { public string k; public float[] p; public float[] fwd; public float[] up; public float lens; }
    [Serializable] class TerrainInfo { public float[] size; public float hmin; public float hmax; public int height_res; public int splat_res; public int detail_res; public string[] layers; public float layer_tile_m; }
    [Serializable] class Placements
    {
        public Item[] props; public Item[] buildings; public Item[] unique;
        public string[] tree_names; public float[] trees; public float[] distant;
        public LightItem[] lights; public CamItem[] cameras; public TerrainInfo terrain;
    }

    static string Root;          // e.g. Assets/MilitaryEnv
    static bool URP;
    static HashSet<string> AlphaKeys = new HashSet<string>();
    static Dictionary<string, Material> Mats = new Dictionary<string, Material>();

    // ---------------- helpers ----------------
    static string FindRoot()
    {
        var guid = AssetDatabase.FindAssets("MilitaryEnvBuilder t:MonoScript").FirstOrDefault();
        if (guid == null) throw new Exception("MilitaryEnvBuilder script not found");
        var p = AssetDatabase.GUIDToAssetPath(guid);            // <Root>/Scripts/Editor/MilitaryEnvBuilder.cs
        return Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(p))).Replace("\\", "/");
    }
    static string Full(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
    static Vector3 Pos(float[] p) => new Vector3(-p[0], p[2], -p[1]);
    static Vector3 Dir(float[] d) => new Vector3(-d[0], d[2], -d[1]);
    static Quaternion Rot(float[] q) => new Quaternion(q[1], -q[3], q[2], q[0]);
    static Vector3 Scl(float[] s) => new Vector3(s[0], s[2], s[1]);
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace("\\", "/");
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    static string StripKey(string n)
    {
        n = n.Trim();
        int dot = n.LastIndexOf('.');
        if (dot > 0 && n.Substring(dot + 1).All(char.IsDigit)) n = n.Substring(0, dot);
        if (n.EndsWith(" (Instance)")) n = n.Replace(" (Instance)", "");
        if (n.StartsWith("TILE_")) n = n.Substring(5);
        if (n.StartsWith("VEG_")) n = n.Substring(4);
        return n.Replace(".", "_");
    }
    static string FindTex(string key, string suffix)
    {
        foreach (var f in new[] { "Textures/Assets", "Textures/Tiling", "Textures/Vegetation" })
        {
            var p = $"{Root}/{f}/{key}_{suffix}.png";
            if (File.Exists(Full(p))) return p;
        }
        return null;
    }

    // ---------------- menu ----------------
    [MenuItem("Tools/Military Env/Build Everything")]
    public static void BuildAll()
    {
        try
        {
            Init();
            Step1_ImportSettings();
            Step2_Materials();
            Step3_RemapModels();
            Step4_BuildScene();
        }
        finally { EditorUtility.ClearProgressBar(); }
    }
    [MenuItem("Tools/Military Env/1 - Import Settings + Materials")]
    public static void MenuMaterials()
    {
        try { Init(); Step1_ImportSettings(); Step2_Materials(); Step3_RemapModels(); }
        finally { EditorUtility.ClearProgressBar(); }
    }
    [MenuItem("Tools/Military Env/2 - Build Scene")]
    public static void MenuScene()
    {
        try { Init(); LoadMaterials(); Step4_BuildScene(); }
        finally { EditorUtility.ClearProgressBar(); }
    }

    static void Init()
    {
        Root = FindRoot();
        URP = GraphicsSettings.currentRenderPipeline != null && GraphicsSettings.currentRenderPipeline.GetType().Name.Contains("Universal");
        if (GraphicsSettings.currentRenderPipeline != null && !URP)
            Debug.LogWarning("[MilitaryEnv] Non-URP render pipeline detected (HDRP?). Materials are created for URP/Built-in and may need conversion.");
        AlphaKeys.Clear();
        var a = $"{Root}/Data/alpha_materials.txt";
        if (File.Exists(Full(a))) foreach (var l in File.ReadAllLines(Full(a))) if (l.Trim().Length > 0) AlphaKeys.Add(l.Trim());
        Debug.Log($"[MilitaryEnv] Root={Root}  URP={URP}");
    }

    // ---------------- step 1: importer settings ----------------
    static void Step1_ImportSettings()
    {
        AssetDatabase.Refresh();
        var texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { $"{Root}/Textures", $"{Root}/Terrain" });
        AssetDatabase.StartAssetEditing();
        try
        {
            int i = 0;
            foreach (var g in texGuids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                EditorUtility.DisplayProgressBar("Military Env", "Texture settings " + Path.GetFileName(p), (float)i++ / texGuids.Length);
                var ti = AssetImporter.GetAtPath(p) as TextureImporter; if (ti == null) continue;
                ti.maxTextureSize = 4096;
                ti.mipmapEnabled = true;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                if (p.EndsWith("_Normal.png")) { ti.textureType = TextureImporterType.NormalMap; }
                else if (p.EndsWith("_MetallicSmoothness.png")) { ti.textureType = TextureImporterType.Default; ti.sRGBTexture = false; }
                else if (p.EndsWith("_Albedo.png"))
                {
                    ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true;
                    var key = Path.GetFileNameWithoutExtension(p).Replace("_Albedo", "");
                    ti.alphaSource = TextureImporterAlphaSource.FromInput;
                    ti.alphaIsTransparency = AlphaKeys.Contains(key);
                    if (AlphaKeys.Contains(key)) ti.mipMapsPreserveCoverage = true;
                }
                if (p.Contains("/Tiling/") || p.Contains("/Vegetation/Bark") || p.Contains("Terrain_")) ti.wrapMode = TextureWrapMode.Repeat;
                else ti.wrapMode = TextureWrapMode.Clamp;
                ti.SaveAndReimport();
            }
            var modelGuids = AssetDatabase.FindAssets("t:Model", new[] { $"{Root}/Models" });
            i = 0;
            foreach (var g in modelGuids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                EditorUtility.DisplayProgressBar("Military Env", "Model settings " + Path.GetFileName(p), (float)i++ / modelGuids.Length);
                var mi = AssetImporter.GetAtPath(p) as ModelImporter; if (mi == null) continue;
                bool veg = p.Contains("/Vegetation/");
                bool solid = p.Contains("/Buildings/") || p.Contains("/Props/") || (p.Contains("/World/") && !p.Contains("Decal_") && !p.Contains("Markings") && !p.Contains("Distant"));
                mi.globalScale = 1f; mi.useFileScale = true;
                mi.importAnimation = false; mi.animationType = ModelImporterAnimationType.None;
                mi.importCameras = false; mi.importLights = false;
                mi.importNormals = ModelImporterNormals.Import;
                mi.importTangents = ModelImporterTangents.CalculateMikk;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                mi.addCollider = solid && !veg;
                mi.generateSecondaryUV = p.Contains("/Buildings/");
                mi.isReadable = false;
                mi.meshCompression = ModelImporterMeshCompression.Off;
                mi.SaveAndReimport();
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh();
    }

    // ---------------- step 2: materials ----------------
    static Material MakeMaterial(string key)
    {
        if (Mats.TryGetValue(key, out var cached)) return cached;
        EnsureFolder($"{Root}/Materials");
        var path = $"{Root}/Materials/{key}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = URP ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        else mat.shader = shader;
        var alb = FindTex(key, "Albedo"); var nrm = FindTex(key, "Normal"); var ms = FindTex(key, "MetallicSmoothness");
        var tA = alb != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(alb) : null;
        var tN = nrm != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(nrm) : null;
        var tM = ms != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(ms) : null;
        bool alpha = AlphaKeys.Contains(key);
        if (URP)
        {
            mat.SetTexture("_BaseMap", tA); mat.SetColor("_BaseColor", Color.white);
            if (tN) { mat.SetTexture("_BumpMap", tN); mat.SetFloat("_BumpScale", 1f); mat.EnableKeyword("_NORMALMAP"); }
            if (tM) { mat.SetTexture("_MetallicGlossMap", tM); mat.SetFloat("_Smoothness", 1f); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            else { mat.SetFloat("_Smoothness", 0.12f); mat.SetFloat("_Metallic", 0f); }
            if (alpha)
            {
                mat.SetFloat("_AlphaClip", 1f); mat.SetFloat("_Cutoff", 0.45f); mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetFloat("_Cull", 0f); mat.SetOverrideTag("RenderType", "TransparentCutout"); mat.renderQueue = (int)RenderQueue.AlphaTest;
            }
        }
        else
        {
            mat.SetTexture("_MainTex", tA); mat.SetColor("_Color", Color.white);
            if (tN) { mat.SetTexture("_BumpMap", tN); mat.EnableKeyword("_NORMALMAP"); }
            if (tM) { mat.SetTexture("_MetallicGlossMap", tM); mat.SetFloat("_GlossMapScale", 1f); mat.EnableKeyword("_METALLICGLOSSMAP"); }
            else { mat.SetFloat("_Glossiness", 0.12f); }
            if (alpha)
            {
                mat.SetFloat("_Mode", 1f); mat.SetFloat("_Cutoff", 0.45f); mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetOverrideTag("RenderType", "TransparentCutout"); mat.renderQueue = (int)RenderQueue.AlphaTest;
            }
        }
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        Mats[key] = mat;
        return mat;
    }
    static void Step2_Materials()
    {
        Mats.Clear();
        var albs = AssetDatabase.FindAssets("_Albedo t:Texture2D", new[] { $"{Root}/Textures" });
        int i = 0;
        foreach (var g in albs)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            var key = Path.GetFileNameWithoutExtension(p).Replace("_Albedo", "");
            EditorUtility.DisplayProgressBar("Military Env", "Material " + key, (float)i++ / albs.Length);
            MakeMaterial(key);
        }
        AssetDatabase.SaveAssets();
    }
    static void LoadMaterials()
    {
        Mats.Clear();
        foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { $"{Root}/Materials" }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
            if (m) Mats[m.name] = m;
        }
    }

    // ---------------- step 3: remap model materials ----------------
    static void Step3_RemapModels()
    {
        var modelGuids = AssetDatabase.FindAssets("t:Model", new[] { $"{Root}/Models" });
        int i = 0, missing = 0;
        foreach (var g in modelGuids)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            EditorUtility.DisplayProgressBar("Military Env", "Assign materials " + Path.GetFileName(p), (float)i++ / modelGuids.Length);
            var mi = AssetImporter.GetAtPath(p) as ModelImporter; if (mi == null) continue;
            var names = new HashSet<string>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p)) if (o is Material) names.Add(o.name);
            foreach (var kv in mi.GetExternalObjectMap()) if (kv.Key.type == typeof(Material)) names.Add(kv.Key.name);
            foreach (var r in AssetDatabase.LoadAllAssetsAtPath(p).OfType<Renderer>())
                foreach (var sm in r.sharedMaterials) if (sm) names.Add(sm.name);
            bool changed = false;
            foreach (var n in names)
            {
                var key = StripKey(n);
                Material m;
                if (!Mats.TryGetValue(key, out m)) { if (FindTex(key, "Albedo") != null) m = MakeMaterial(key); }
                if (m == null) { missing++; Debug.LogWarning($"[MilitaryEnv] No texture set for material '{n}' in {p}"); continue; }
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), m); changed = true;
            }
            if (changed) mi.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[MilitaryEnv] Material remap done. Missing: {missing}");
    }

    // ---------------- step 4: scene ----------------
    static GameObject LoadModel(string assetPath) => AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
    static GameObject Group(string name, Transform parent = null)
    {
        var go = new GameObject(name); if (parent) go.transform.SetParent(parent, false); return go;
    }
    static void SetStatic(GameObject go)
    {
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
        foreach (Transform c in go.transform) SetStatic(c.gameObject);
    }
    static GameObject Place(GameObject model, Item it, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Pos(it.p);
        go.transform.localRotation = Rot(it.q);
        go.transform.localScale = Scl(it.s);
        SetStatic(go);
        return go;
    }

    static void Step4_BuildScene()
    {
        var json = File.ReadAllText(Full($"{Root}/Data/placements.json"));
        var P = JsonUtility.FromJson<Placements>(json);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = Group("MilitaryTrainingEnvironment").transform;
        var groups = new Dictionary<string, Transform>();
        Func<string, Transform> G = gn => { if (!groups.ContainsKey(gn)) groups[gn] = Group(gn, root).transform; return groups[gn]; };

        // ---- terrain ----
        EditorUtility.DisplayProgressBar("Military Env", "Terrain", 0.05f);
        var terrain = BuildTerrain(P);
        terrain.transform.SetParent(G("01_TERRAIN"), true);

        // ---- buildings ----
        int k = 0;
        foreach (var it in P.buildings)
        {
            EditorUtility.DisplayProgressBar("Military Env", "Buildings", 0.4f + 0.05f * k++ / P.buildings.Length);
            var m = LoadModel($"{Root}/Models/Buildings/{it.k}.fbx"); if (!m) { Debug.LogWarning("Missing building " + it.k); continue; }
            Place(m, it, G("03_BUILDINGS")).name = it.k;
        }
        // ---- props / defence / vehicles / training ----
        var cache = new Dictionary<string, GameObject>();
        k = 0;
        foreach (var it in P.props)
        {
            if (k % 50 == 0) EditorUtility.DisplayProgressBar("Military Env", "Props", 0.45f + 0.25f * k / P.props.Length);
            k++;
            if (!cache.TryGetValue(it.k, out var m)) { m = LoadModel($"{Root}/Models/Props/{it.k}.fbx"); cache[it.k] = m; }
            if (!m) continue;
            var go = Place(m, it, G(it.c));
            if (it.k.Contains("Truck") || it.k.Contains("APC") || it.k.Contains("Utility_4x4")) go.transform.SetParent(G("06_VEHICLES"), true);
        }
        // ---- unique world meshes (roads, pads, decals, berms, trenches, horizon) ----
        foreach (var it in P.unique)
        {
            var safe = it.k.Replace(".", "_");
            var m = LoadModel($"{Root}/Models/World/{safe}.fbx"); if (!m) { Debug.LogWarning("Missing world mesh " + safe); continue; }
            var go = Place(m, it, G(string.IsNullOrEmpty(it.c) ? "02_ROADS" : it.c)); go.name = safe;
            if (safe.StartsWith("Decal_") || safe.Contains("Markings") || safe.Contains("Distant"))
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
        }
        // ---- distant forest (GPU instanced) ----
        EditorUtility.DisplayProgressBar("Military Env", "Distant forest", 0.8f);
        var farModel = LoadModel($"{Root}/Models/Vegetation/T_Far_Conifer.fbx");
        if (farModel && P.distant != null && P.distant.Length > 0)
        {
            var df = Group("DistantForest_Instanced", G("07_FOREST_LARGE"));
            var comp = df.AddComponent<DistantForestRenderer>();
            comp.mesh = farModel.GetComponentInChildren<MeshFilter>().sharedMesh;
            comp.material = farModel.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            if (comp.material) comp.material.enableInstancing = true;
            int n = P.distant.Length / 5; var d = new float[n * 5];
            for (int i = 0; i < n; i++)
            {
                int o = i * 5;
                d[o] = -P.distant[o]; d[o + 1] = P.distant[o + 2]; d[o + 2] = -P.distant[o + 1];
                d[o + 3] = -P.distant[o + 3] * Mathf.Rad2Deg; d[o + 4] = P.distant[o + 4];
            }
            comp.data = d; comp.Build();
        }
        // ---- lighting ----
        foreach (var l in P.lights)
        {
            var go = new GameObject("Sun_Directional"); go.transform.SetParent(G("12_LIGHTING"), false);
            var lt = go.AddComponent<Light>(); lt.type = LightType.Directional;
            lt.color = new Color(l.color[0], l.color[1], l.color[2]); lt.intensity = 1.4f; lt.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.LookRotation(Dir(l.fwd), Dir(l.up));
            RenderSettings.sun = lt;
        }
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0016f; RenderSettings.fogColor = new Color(0.62f, 0.66f, 0.70f);
        RenderSettings.ambientMode = AmbientMode.Skybox; RenderSettings.ambientIntensity = 1f;
        var sky = new Material(Shader.Find("Skybox/Procedural"));
        sky.SetFloat("_AtmosphereThickness", 1.3f); sky.SetFloat("_Exposure", 1.1f); sky.SetColor("_SkyTint", new Color(0.6f, 0.62f, 0.66f));
        EnsureFolder($"{Root}/Materials"); AssetDatabase.CreateAsset(sky, $"{Root}/Materials/Sky_Overcast.mat");
        RenderSettings.skybox = sky;
        // ---- cameras ----
        bool first = true;
        foreach (var c in P.cameras)
        {
            var go = new GameObject(c.k); go.transform.SetParent(G("13_CAMERAS"), false);
            var cam = go.AddComponent<Camera>();
            float hfov = 2f * Mathf.Atan(18f / c.lens);
            cam.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(hfov / 2f) * 9f / 16f) * Mathf.Rad2Deg;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 4000f;
            go.transform.position = Pos(c.p);
            go.transform.rotation = Quaternion.LookRotation(Dir(c.fwd), Dir(c.up));
            if (first) { go.tag = "MainCamera"; go.AddComponent<AudioListener>(); first = false; }
            else go.SetActive(false);
        }
        EnsureFolder($"{Root}/Scenes");
        EditorSceneManager.SaveScene(scene, $"{Root}/Scenes/MilitaryTrainingEnvironment.unity");
        Debug.Log("[MilitaryEnv] Scene built and saved.");
    }

    // ---------------- terrain ----------------
    static Terrain BuildTerrain(Placements P)
    {
        var T = P.terrain; int hr = T.height_res; float sizeY = T.hmax - T.hmin;
        EnsureFolder($"{Root}/Terrain/Generated");
        var td = new TerrainData();
        td.heightmapResolution = hr;
        td.size = new Vector3(T.size[0], sizeY, T.size[1]);
        // heights
        var raw = File.ReadAllBytes(Full($"{Root}/Terrain/Heightmap_1025_u16.raw"));
        var h = new float[hr, hr];
        for (int r = 0; r < hr; r++) for (int c = 0; c < hr; c++) h[r, c] = BitConverter.ToUInt16(raw, (r * hr + c) * 2) / 65535f;
        td.SetHeights(0, 0, h);
        // layers
        var layers = new List<TerrainLayer>();
        foreach (var ln in T.layers)
        {
            var lp = $"{Root}/Terrain/Generated/{ln}.terrainlayer";
            var tl = AssetDatabase.LoadAssetAtPath<TerrainLayer>(lp);
            if (tl == null) { tl = new TerrainLayer(); AssetDatabase.CreateAsset(tl, lp); }
            var a = FindTex(ln, "Albedo"); var n = FindTex(ln, "Normal");
            tl.diffuseTexture = a != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(a) : null;
            tl.normalMapTexture = n != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(n) : null;
            tl.tileSize = new Vector2(T.layer_tile_m, T.layer_tile_m);
            tl.smoothness = ln.Contains("Rock") ? 0.15f : 0.05f; tl.metallic = 0f;
            EditorUtility.SetDirty(tl); layers.Add(tl);
        }
        td.terrainLayers = layers.ToArray();
        // splat
        int sr = T.splat_res; td.alphamapResolution = sr;
        var sraw = File.ReadAllBytes(Full($"{Root}/Terrain/Splat_1024x1024x5_u8.raw"));
        int L = layers.Count; var am = new float[sr, sr, L];
        for (int r = 0; r < sr; r++) for (int c = 0; c < sr; c++) for (int l = 0; l < L; l++) am[r, c, l] = sraw[(r * sr + c) * L + l] / 255f;
        td.SetAlphamaps(0, 0, am);
        // details (grass cards + ferns)
        EditorUtility.DisplayProgressBar("Military Env", "Terrain details", 0.2f);
        var detailModels = new List<GameObject>();
        for (int i = 0; i < 4; i++) { var g = LoadModel($"{Root}/Models/Vegetation/G_GrassCard_{i}.fbx"); if (g) detailModels.Add(g); }
        int grassCount = detailModels.Count;
        for (int i = 0; i < 2; i++) { var g = LoadModel($"{Root}/Models/Vegetation/G_Fern_{i}.fbx"); if (g) detailModels.Add(g); }
        int dr = T.detail_res;
        td.SetDetailResolution(dr, 32);
#if UNITY_2022_2_OR_NEWER
        td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
#endif
        var protos = new List<DetailPrototype>();
        foreach (var g in detailModels)
        {
            var dp = new DetailPrototype();
            dp.prototype = g; dp.usePrototypeMesh = true; dp.renderMode = DetailRenderMode.VertexLit;
#if UNITY_2022_2_OR_NEWER
            dp.useInstancing = true;
#endif
            dp.minWidth = 0.8f; dp.maxWidth = 1.3f; dp.minHeight = 0.8f; dp.maxHeight = 1.3f;
            dp.healthyColor = Color.white; dp.dryColor = new Color(0.9f, 0.88f, 0.8f); dp.noiseSpread = 0.3f;
            protos.Add(dp);
        }
        td.detailPrototypes = protos.ToArray();
        var graw = File.ReadAllBytes(Full($"{Root}/Terrain/DetailGrass_1024_u8.raw"));
        var fraw = File.ReadAllBytes(Full($"{Root}/Terrain/DetailFern_1024_u8.raw"));
        for (int pi = 0; pi < protos.Count; pi++)
        {
            var layer = new int[dr, dr];
            bool isGrass = pi < grassCount;
            int nProto = isGrass ? grassCount : protos.Count - grassCount; int sub = isGrass ? pi : pi - grassCount;
            for (int r = 0; r < dr; r++) for (int c = 0; c < dr; c++)
            {
                int v = isGrass ? graw[r * dr + c] : fraw[r * dr + c];
                if (v == 0) continue;
                // spread each cell's count across the prototype variants
                if (((r * 7 + c * 13) % nProto) == sub) layer[r, c] = isGrass ? v * 2 : v;
            }
            td.SetDetailLayer(0, 0, pi, layer);
        }
        // trees (trees, understory, debris, rocks)
        EditorUtility.DisplayProgressBar("Military Env", "Terrain trees", 0.3f);
        var tprotos = new List<TreePrototype>(); var protoIndex = new int[P.tree_names.Length];
        for (int i = 0; i < P.tree_names.Length; i++)
        {
            var n = P.tree_names[i];
            var path = n.StartsWith("A_") ? $"{Root}/Models/Props/{n.Substring(2)}.fbx" : $"{Root}/Models/Vegetation/{n}.fbx";
            var g = LoadModel(path);
            if (!g) { protoIndex[i] = -1; Debug.LogWarning("Missing tree model " + path); continue; }
            protoIndex[i] = tprotos.Count; tprotos.Add(new TreePrototype { prefab = g, bendFactor = 0f });
        }
        td.treePrototypes = tprotos.ToArray();
        var trees = new List<TreeInstance>(P.trees.Length / 6);
        for (int i = 0; i + 5 < P.trees.Length; i += 6)
        {
            int pidx = protoIndex[(int)P.trees[i]]; if (pidx < 0) continue;
            float bx = P.trees[i + 1], by = P.trees[i + 2], bz = P.trees[i + 3];
            float ux = -bx, uz = -by;
            var ti = new TreeInstance();
            ti.prototypeIndex = pidx;
            ti.position = new Vector3((ux + T.size[0] / 2f) / T.size[0], Mathf.Clamp01((bz - T.hmin) / sizeY), (uz + T.size[1] / 2f) / T.size[1]);
            ti.rotation = -P.trees[i + 4];
            ti.widthScale = ti.heightScale = P.trees[i + 5];
            ti.color = Color.white; ti.lightmapColor = Color.white;
            trees.Add(ti);
        }
        td.treeInstances = trees.ToArray();
        AssetDatabase.CreateAsset(td, $"{Root}/Terrain/Generated/TerrainData_Main.asset");
        var go = Terrain.CreateTerrainGameObject(td); go.name = "Terrain_Main";
        go.transform.position = new Vector3(-T.size[0] / 2f, T.hmin, -T.size[1] / 2f);
        var terrain = go.GetComponent<Terrain>();
        terrain.treeDistance = 1500f; terrain.treeBillboardDistance = 400f; terrain.treeMaximumFullLODCount = 2000;
        terrain.detailObjectDistance = 120f; terrain.detailObjectDensity = 1f; terrain.heightmapPixelError = 4f; terrain.basemapDistance = 300f;
        if (URP)
        {
            var ts = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (ts) { var tm = new Material(ts); AssetDatabase.CreateAsset(tm, $"{Root}/Terrain/Generated/TerrainMat_URP.mat"); terrain.materialTemplate = tm; }
        }
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        AssetDatabase.SaveAssets();
        return terrain;
    }
}
