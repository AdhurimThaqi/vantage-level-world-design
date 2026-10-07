// Command Tower importer.  Menu: Tools > Command Tower > Create Prefab
// URP (recommended) or Built-in render pipeline. Unity 2021.3+.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CommandTowerKit.EditorTools
{
    public static class CommandTowerImporter
    {
        [Serializable] class TItem { public string k; public string c; public float[] p; public float[] q; public float[] s; public float closedYaw; }
        [Serializable] class TMarker { public string k; public string c; public float[] p; public float[] q; public string room; public string route; public int order; public int floor; public string note; }
        [Serializable] class TowerFile { public TItem[] structure; public TItem[] props; public TItem[] lights; public TMarker[] markers; public float[] pivot; }

        static string Root; static bool URP;
        static HashSet<string> AlphaKeys = new HashSet<string>(), TransparentKeys = new HashSet<string>();
        static Dictionary<string, Material> Mats = new Dictionary<string, Material>();

        // Blender (Z-up) -> Unity (Y-up)
        static Vector3 Pos(float[] p) => new Vector3(-p[0], p[2], -p[1]);
        static Quaternion Rot(float[] q) => new Quaternion(q[1], -q[3], q[2], q[0]);
        static Vector3 Scl(float[] s) => new Vector3(s[0], s[2], s[1]);
        static string Full(string a) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", a));

        [MenuItem("Tools/Command Tower/Create Prefab")]
        public static void CreatePrefab() { Run(false); }
        [MenuItem("Tools/Command Tower/Create Prefab + Place In Open Scene")]
        public static void CreatePrefabAndPlace() { Run(true); }

        static void Run(bool place)
        {
            try { Build(place); }
            finally { EditorUtility.ClearProgressBar(); }
        }

        static void Init()
        {
            var guid = AssetDatabase.FindAssets("CommandTowerImporter t:MonoScript").First();
            var p = AssetDatabase.GUIDToAssetPath(guid);                        // <Root>/Scripts/Editor/CommandTowerImporter.cs
            Root = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(p))).Replace("\\", "/");
            var rp = GraphicsSettings.currentRenderPipeline;
            URP = rp != null && rp.GetType().Name.Contains("Universal");
            if (rp != null && !URP) Debug.LogWarning("[CommandTower] Non-URP pipeline (HDRP?) - materials are made for URP/Built-in.");
            AlphaKeys = ReadList($"{Root}/Data/alpha_materials.txt");
            TransparentKeys = ReadList($"{Root}/Data/transparent_materials.txt");
            Mats.Clear();
        }
        static HashSet<string> ReadList(string a)
        {
            var h = new HashSet<string>();
            if (File.Exists(Full(a))) foreach (var l in File.ReadAllLines(Full(a))) if (l.Trim().Length > 0) h.Add(l.Trim());
            return h;
        }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace("\\", "/");
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        static string FindTex(string key, string suffix)
        {
            foreach (var f in new[] { "Textures/Tiling", "Textures/Props" })
            {
                var p = $"{Root}/{f}/{key}_{suffix}.png";
                if (File.Exists(Full(p))) return p;
            }
            return null;
        }
        static string StripKey(string n)
        {
            n = n.Trim();
            int dot = n.LastIndexOf('.');
            if (dot > 0 && n.Substring(dot + 1).All(char.IsDigit)) n = n.Substring(0, dot);
            if (n.StartsWith("TILE_")) n = n.Substring(5);
            return n.Replace(".", "_");
        }

        static void ConfigureTexture(string p)
        {
            var ti = AssetImporter.GetAtPath(p) as TextureImporter; if (ti == null) return;
            ti.maxTextureSize = 4096; ti.mipmapEnabled = true; ti.textureCompression = TextureImporterCompression.CompressedHQ;
            if (p.EndsWith("_Normal.png")) ti.textureType = TextureImporterType.NormalMap;
            else if (p.EndsWith("_MetallicSmoothness.png")) { ti.textureType = TextureImporterType.Default; ti.sRGBTexture = false; }
            else
            {
                ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true;
                var key = Path.GetFileNameWithoutExtension(p).Replace("_Albedo", "").Replace("_Emission", "");
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = AlphaKeys.Contains(key);
            }
            ti.wrapMode = p.Contains("/Tiling/") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.SaveAndReimport();
        }
        static void ConfigureModel(string p)
        {
            var mi = AssetImporter.GetAtPath(p) as ModelImporter; if (mi == null) return;
            mi.globalScale = 1f; mi.useFileScale = true;
            mi.importAnimation = false; mi.animationType = ModelImporterAnimationType.None;
            mi.importCameras = false; mi.importLights = false;
            mi.importNormals = ModelImporterNormals.Import; mi.importTangents = ModelImporterTangents.CalculateMikk;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.addCollider = true;
            mi.generateSecondaryUV = p.Contains("_Structure");
            mi.isReadable = false;
            mi.SaveAndReimport();
        }
        static Material MakeMaterial(string key)
        {
            if (Mats.TryGetValue(key, out var cached)) return cached;
            EnsureFolder($"{Root}/Materials");
            var path = $"{Root}/Materials/{key}.mat";
            var shader = URP ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); } else mat.shader = shader;
            Texture2D T(string suf) { var t = FindTex(key, suf); return t != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(t) : null; }
            var tA = T("Albedo"); var tN = T("Normal"); var tM = T("MetallicSmoothness"); var tE = T("Emission");
            bool alpha = AlphaKeys.Contains(key), transparent = TransparentKeys.Contains(key);
            if (URP)
            {
                mat.SetTexture("_BaseMap", tA); mat.SetColor("_BaseColor", Color.white);
                if (tN) { mat.SetTexture("_BumpMap", tN); mat.EnableKeyword("_NORMALMAP"); }
                if (tM) { mat.SetTexture("_MetallicGlossMap", tM); mat.SetFloat("_Smoothness", 1f); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); }
                else { mat.SetFloat("_Smoothness", 0.12f); mat.SetFloat("_Metallic", 0f); }
                if (alpha)
                {
                    mat.SetFloat("_AlphaClip", 1f); mat.SetFloat("_Cutoff", 0.45f); mat.EnableKeyword("_ALPHATEST_ON");
                    mat.SetFloat("_Cull", 0f); mat.SetOverrideTag("RenderType", "TransparentCutout"); mat.renderQueue = (int)RenderQueue.AlphaTest;
                }
                if (transparent)
                {
                    mat.SetFloat("_Surface", 1f); mat.SetFloat("_Blend", 0f);
                    mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); mat.SetFloat("_ZWrite", 0f);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.SetOverrideTag("RenderType", "Transparent");
                    mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.35f)); mat.SetFloat("_Smoothness", 0.9f); mat.renderQueue = (int)RenderQueue.Transparent;
                }
            }
            else
            {
                mat.SetTexture("_MainTex", tA); mat.SetColor("_Color", Color.white);
                if (tN) { mat.SetTexture("_BumpMap", tN); mat.EnableKeyword("_NORMALMAP"); }
                if (tM) { mat.SetTexture("_MetallicGlossMap", tM); mat.SetFloat("_GlossMapScale", 1f); mat.EnableKeyword("_METALLICGLOSSMAP"); }
                else mat.SetFloat("_Glossiness", 0.12f);
                if (alpha) { mat.SetFloat("_Mode", 1f); mat.SetFloat("_Cutoff", 0.45f); mat.EnableKeyword("_ALPHATEST_ON"); mat.SetOverrideTag("RenderType", "TransparentCutout"); mat.renderQueue = (int)RenderQueue.AlphaTest; }
                if (transparent)
                {
                    mat.SetFloat("_Mode", 3f); mat.SetInt("_SrcBlend", (int)BlendMode.One); mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); mat.SetInt("_ZWrite", 0);
                    mat.EnableKeyword("_ALPHAPREMULTIPLY_ON"); mat.SetColor("_Color", new Color(1f, 1f, 1f, 0.35f)); mat.renderQueue = (int)RenderQueue.Transparent;
                }
            }
            if (tE)
            {
                mat.SetTexture("_EmissionMap", tE); mat.SetColor("_EmissionColor", Color.white * 2.5f);
                mat.EnableKeyword("_EMISSION"); mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            mat.enableInstancing = true; EditorUtility.SetDirty(mat);
            Mats[key] = mat; return mat;
        }
        static void RemapModel(string p)
        {
            var mi = AssetImporter.GetAtPath(p) as ModelImporter; if (mi == null) return;
            var names = new HashSet<string>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p)) if (o is Material) names.Add(o.name);
            foreach (var kv in mi.GetExternalObjectMap()) if (kv.Key.type == typeof(Material)) names.Add(kv.Key.name);
            bool changed = false;
            foreach (var n in names)
            {
                var key = StripKey(n);
                if (FindTex(key, "Albedo") == null) { Debug.LogWarning($"[CommandTower] No textures for material '{n}' in {p}"); continue; }
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), MakeMaterial(key)); changed = true;
            }
            if (changed) mi.SaveAndReimport();
        }
        static GameObject Group(string name, Transform parent) { var g = new GameObject(name); g.transform.SetParent(parent, false); return g; }
        static GameObject Place(GameObject model, TItem it, Transform parent)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Pos(it.p); go.transform.localRotation = Rot(it.q); go.transform.localScale = Scl(it.s);
            SetStatic(go, true); return go;
        }
        static void SetStatic(GameObject go, bool on)
        {
            GameObjectUtility.SetStaticEditorFlags(go, on ? (StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.ContributeGI) : (StaticEditorFlags)0);
            foreach (Transform c in go.transform) SetStatic(c.gameObject, on);
        }

        static void Build(bool place)
        {
            Init();
            var D = JsonUtility.FromJson<TowerFile>(File.ReadAllText(Full($"{Root}/Data/tower.json")));
            AssetDatabase.Refresh();
            var texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { $"{Root}/Textures" });
            var modelGuids = AssetDatabase.FindAssets("t:Model", new[] { $"{Root}/Models" });
            AssetDatabase.StartAssetEditing();
            try
            {
                int i = 0;
                foreach (var g in texGuids) { EditorUtility.DisplayProgressBar("Command Tower", "Texture settings", 0.3f * i++ / texGuids.Length); ConfigureTexture(AssetDatabase.GUIDToAssetPath(g)); }
                i = 0;
                foreach (var g in modelGuids) { EditorUtility.DisplayProgressBar("Command Tower", "Model settings", 0.3f + 0.1f * i++ / modelGuids.Length); ConfigureModel(AssetDatabase.GUIDToAssetPath(g)); }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
            int n = 0;
            foreach (var g in modelGuids) { EditorUtility.DisplayProgressBar("Command Tower", "Materials", 0.4f + 0.2f * n++ / modelGuids.Length); RemapModel(AssetDatabase.GUIDToAssetPath(g)); }
            AssetDatabase.SaveAssets();

            var root = new GameObject("CommandTower");
            var structT = Group("Structure", root.transform).transform;
            var furnT = Group("Furniture", root.transform).transform;
            var doorT = Group("Doors", root.transform).transform;
            var lightT = Group("Lights", root.transform).transform;
            var markT = Group("GameplayMarkers", root.transform).transform;

            foreach (var it in D.structure)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/Structure/{it.k}.fbx");
                if (!model) { Debug.LogWarning("[CommandTower] Missing " + it.k); continue; }
                var go = Place(model, it, structT); go.name = it.k;
                if (it.c == "collider") foreach (var r in go.GetComponentsInChildren<Renderer>()) r.enabled = false;
                if (it.c == "glass") foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            }
            var cache = new Dictionary<string, GameObject>();
            n = 0;
            foreach (var it in D.props)
            {
                if (n++ % 40 == 0) EditorUtility.DisplayProgressBar("Command Tower", "Furniture & doors", 0.6f + 0.3f * n / D.props.Length);
                if (!cache.TryGetValue(it.k, out var model)) { model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/Props/{it.k}.fbx"); cache[it.k] = model; }
                if (!model) continue;
                bool isDoor = it.c == "door";
                var go = Place(model, it, isDoor ? doorT : furnT);
                if (isDoor)
                {
                    SetStatic(go, false);
                    var d = go.AddComponent<TowerDoor>();
                    d.openRotation = go.transform.localRotation;
                    d.closedRotation = Quaternion.Euler(0f, -it.closedYaw * Mathf.Rad2Deg, 0f);
                    d.isOpen = Quaternion.Angle(d.openRotation, d.closedRotation) > 5f;
                    if (!d.isOpen) d.openRotation = d.closedRotation * Quaternion.Euler(0f, -85f, 0f);
                }
            }
            int li = 0;
            foreach (var l in D.lights)
            {
                if ((li++ % 2) != 0) continue;
                var go = new GameObject("PointLight_" + li); go.transform.SetParent(lightT, false);
                go.transform.localPosition = Pos(l.p);
                var lt = go.AddComponent<Light>(); lt.type = LightType.Point; lt.range = 7.5f; lt.intensity = 1.6f;
                lt.color = new Color(1f, 0.95f, 0.85f); lt.shadows = LightShadows.Soft; lt.lightmapBakeType = LightmapBakeType.Baked;
            }
            var typeGroups = new Dictionary<string, Transform>(); var routeGroups = new Dictionary<string, Transform>();
            foreach (var mk in D.markers)
            {
                if (!typeGroups.TryGetValue(mk.c, out var tg)) { tg = Group(mk.c, markT).transform; typeGroups[mk.c] = tg; }
                Transform parent = tg;
                if (!string.IsNullOrEmpty(mk.route))
                {
                    if (!routeGroups.TryGetValue(mk.route, out var rg)) { rg = Group("Route_" + mk.route, tg).transform; routeGroups[mk.route] = rg; }
                    parent = rg;
                }
                var go = new GameObject(mk.k); go.transform.SetParent(parent, false);
                go.transform.localPosition = Pos(mk.p); go.transform.localRotation = Rot(mk.q);
                var g = go.AddComponent<GameplayMarker>();
                g.markerType = mk.c; g.room = mk.room; g.route = mk.route; g.order = mk.order; g.floor = mk.floor; g.note = mk.note;
            }
            // pivot = centre of the tower base
            var pivot = (D.pivot != null && D.pivot.Length == 3) ? Pos(D.pivot) : Vector3.zero;
            foreach (Transform c in root.transform) c.localPosition -= pivot;

            EnsureFolder($"{Root}/Prefabs");
            var prefabPath = $"{Root}/Prefabs/CommandTower.prefab";
            if (place)
            {
                var sv = SceneView.lastActiveSceneView;
                root.transform.position = sv != null ? new Vector3(sv.pivot.x, 0f, sv.pivot.z) : Vector3.zero;
                PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.AutomatedAction);
                Selection.activeGameObject = root;
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            else
            {
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                UnityEngine.Object.DestroyImmediate(root);
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[CommandTower] Prefab saved: {prefabPath}  ({D.structure.Length} parts, {D.props.Length} props, {D.markers.Length} markers)");
        }
    }
}
