using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vantage.EditorTools
{
    /// <summary>
    /// Asset Store models on the machines, applied by Setup (and the menu item), like the boss in VantageEnemyModels:
    /// - Drone: the VoodooPlay sci-fi drone under Head/"Model", as wide as the body it replaces. Its emissive panels are
    ///   the telegraph "eye" (red, flaring yellow before a shot). One mesh, so no rotors spin.
    /// - Turret: Sci-fi turrets "Canon_lvl_3": its base under "Model", its gun head under Head (turned so the barrels
    ///   point along +Z, the way VantageDrone aims), the barrels kick back on each shot. As tall as the model it replaces.
    ///   A URP copy of the pack's Built-in material (which is pink in URP).
    /// - Helicopter: the Raptor3D MSH_N2 (woodland camo) under "Model", as long as the model it replaces, with
    ///   "Main Rotor" / "Tail Rotor" pivots put on the rotor hubs so they spin in place.
    /// Gameplay values on the prefabs (health, fire, effects, sounds) are kept; only the looks, eye, muzzle, collider
    /// and the parts listed below are rebuilt on every run. A missing pack leaves that machine as it is.
    /// </summary>
    public static class VantageMachineModels
    {
        public const string DroneModel = "Assets/VoodooPlay/Models/Drone/Drone.FBX";
        public const string TurretModel = "Assets/Sci-fi turrets/Prefabs/Canon_lvl_3.prefab";
        private const string TurretMaterialSource = "Assets/Sci-fi turrets/Models/Turrets.mat";
        public const string HelicopterModel = "Assets/Raptor3D/MSH_N2/0_Prefabs/MSH_N2_Color_3_Prefab.prefab";
        private const string Folder = "Assets/Vantage/Characters";

        /// <summary>The drone model's nose is along this local yaw (degrees) from +Z; set from the screenshots.</summary>
        private const float DroneFacingYaw = 0f;

        /// <summary>Children kept when the looks are rebuilt (everything else on the root and Head is a model part).</summary>
        private static readonly HashSet<string> KeepOnRoot = new HashSet<string> { "Head", "Hum" };
        private static readonly HashSet<string> KeepOnHead = new HashSet<string> { "Muzzle", "Eye Light", "Eye" };

        [MenuItem("Vantage/Enemies/Use Asset Store Models (drone, turret, helicopter)", priority = 92)]
        public static void All()
        {
            Drone();
            Turret();
            Helicopter();
        }

        #region Drone

        public static void Drone()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(DroneModel);
            if (model == null) return;
            var root = PrefabUtility.LoadPrefabContents(VantageEditorUtil.DronePrefabPath);
            try
            {
                var drone = root.GetComponent<VantageDrone>();
                var head = drone != null ? drone.Head : null;
                if (head == null) { Debug.LogWarning("[Vantage] Drone prefab has no VantageDrone with a Head; not changed."); return; }
                var width = boundsIn(root.transform, meshRenderers(root)).size.x;
                clearLooks(root.transform, head, keepEye: false);

                var go = place(model, head, "Model");
                go.transform.localRotation = Quaternion.Euler(0, -DroneFacingYaw, 0);
                var renderers = go.GetComponentsInChildren<Renderer>(true);
                var scale = fit(go.transform, boundsIn(head, renderers).size.x, width);
                var b = boundsIn(head, renderers);
                go.transform.localPosition -= b.center;   // the head turns about the body's middle
                b = boundsIn(head, renderers);

                // The emissive panels are the eye: VantageDrone tints the renderer's emission.
                var eye = renderers.FirstOrDefault(r => r.sharedMaterial != null && r.sharedMaterial.IsKeywordEnabled("_EMISSION"));
                drone.Eye = eye;
                drone.Rotors = new Transform[0];
                drone.Recoil = new Transform[0];
                if (drone.Muzzle != null) drone.Muzzle.localPosition = new Vector3(0, b.center.y - b.extents.y * 0.3f, b.max.z + 0.05f);
                if (drone.EyeLight != null) drone.EyeLight.transform.localPosition = new Vector3(0, b.center.y, b.max.z + 0.15f);

                var sphere = root.GetComponent<SphereCollider>();
                if (sphere != null)
                {
                    sphere.center = root.transform.InverseTransformPoint(head.TransformPoint(b.center));
                    sphere.radius = Mathf.Max(b.extents.x, b.extents.z) * 0.85f;
                }
                PrefabUtility.SaveAsPrefabAsset(root, VantageEditorUtil.DronePrefabPath);
                Debug.Log($"[Vantage] Drone model: {Path.GetFileNameWithoutExtension(DroneModel)}, {b.size.x:F2} x {b.size.y:F2} x {b.size.z:F2} m (scale {scale:F3}), eye {(eye != null ? eye.sharedMaterial.name : "none (no emissive material)")}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        #endregion

        #region Turret

        public static void Turret()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(TurretModel);
            if (model == null) return;
            var material = turretMaterial();
            var root = PrefabUtility.LoadPrefabContents(VantageEditorUtil.TurretPrefabPath);
            try
            {
                var turret = root.GetComponent<VantageDrone>();
                var head = turret != null ? turret.Head : null;
                if (head == null) { Debug.LogWarning("[Vantage] Turret prefab has no VantageDrone with a Head; not changed."); return; }
                var height = boundsIn(root.transform, meshRenderers(root, exclude: turret.Eye)).size.y;
                clearLooks(root.transform, head, keepEye: true);
                head.localRotation = Quaternion.identity;

                var go = place(model, root.transform, "Model");
                foreach (var a in go.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a); // its shoot clip fights the aim
                var renderers = go.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers) r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
                var scale = fit(go.transform, boundsIn(root.transform, renderers).size.y, height);
                var b = boundsIn(root.transform, renderers);
                go.transform.localPosition += new Vector3(-b.center.x, -b.min.y, -b.center.z);

                // The gun head (the base's only child) turns in Head; its barrels are the "gunpoint" children.
                var gun = go.transform.Cast<Transform>().FirstOrDefault(t => t.GetComponent<Renderer>() != null);
                var barrels = gun != null ? gun.Cast<Transform>().Where(t => t.name.StartsWith("gunpoint")).ToArray() : new Transform[0];
                if (gun == null || barrels.Length == 0) { Debug.LogWarning($"[Vantage] Turret model {TurretModel}: no gun head with gunpoint barrels; not changed."); return; }
                head.position = gun.position;
                gun.SetParent(head, true);
                // Barrels along +Z: turn the gun about its pivot by the yaw from where they point now.
                var aim = boundsIn(head, barrels.Select(t => t.GetComponent<Renderer>()).ToArray()).center;
                aim.y = 0;
                if (aim.sqrMagnitude > 0.0001f) gun.RotateAround(head.position, head.up, -Vector3.SignedAngle(Vector3.forward, aim, Vector3.up));

                var barrelBounds = boundsIn(head, barrels.Select(t => t.GetComponent<Renderer>()).ToArray());
                var gunBounds = boundsIn(head, new[] { gun.GetComponent<Renderer>() });
                if (turret.Muzzle != null) turret.Muzzle.localPosition = new Vector3(barrelBounds.center.x, barrelBounds.center.y, barrelBounds.max.z + 0.03f);
                if (turret.Eye != null)
                {
                    // A small red sensor on the front of the gun head, between the barrels.
                    turret.Eye.transform.localPosition = new Vector3(0, gunBounds.center.y + gunBounds.extents.y * 0.35f, gunBounds.max.z - 0.02f);
                    turret.Eye.transform.localScale = Vector3.one * Mathf.Max(0.06f, gunBounds.size.y * 0.16f);
                }
                if (turret.EyeLight != null) turret.EyeLight.transform.localPosition = turret.Eye != null ? turret.Eye.transform.localPosition + Vector3.forward * 0.1f : turret.Muzzle.localPosition;
                turret.Recoil = barrels;

                var all = boundsIn(root.transform, renderers);
                var box = root.GetComponent<BoxCollider>();
                if (box != null) { box.center = all.center; box.size = all.size; }
                PrefabUtility.SaveAsPrefabAsset(root, VantageEditorUtil.TurretPrefabPath);
                Debug.Log($"[Vantage] Turret model: {Path.GetFileNameWithoutExtension(TurretModel)}, {all.size.x:F2} x {all.size.y:F2} x {all.size.z:F2} m (scale {scale:F3}), {barrels.Length} barrels, gun pivot {head.localPosition.y:F2} m up.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>URP Lit (specular setup) from the pack's Built-in Standard (Specular) material.</summary>
        private static Material turretMaterial()
        {
            var folder = ensureFolder($"{Folder}/SciFi_Turret");
            var path = $"{folder}/M_SciFi_Turret_URP.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m; // kept, so hand-tuning survives
            var source = AssetDatabase.LoadAssetAtPath<Material>(TurretMaterialSource);
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_SciFi_Turret_URP" };
            if (source != null)
            {
                Texture tex(string name) => source.HasProperty(name) ? source.GetTexture(name) : null;
                m.SetTexture("_BaseMap", tex("_MainTex"));
                m.SetColor("_BaseColor", source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
                if (tex("_BumpMap") != null) { m.SetTexture("_BumpMap", tex("_BumpMap")); m.EnableKeyword("_NORMALMAP"); }
                if (tex("_OcclusionMap") != null) { m.SetTexture("_OcclusionMap", tex("_OcclusionMap")); m.EnableKeyword("_OCCLUSIONMAP"); }
                m.SetFloat("_WorkflowMode", 0f); // specular
                m.EnableKeyword("_SPECULAR_SETUP");
                m.SetColor("_SpecColor", source.HasProperty("_SpecColor") ? source.GetColor("_SpecColor") : Color.gray);
                if (tex("_SpecGlossMap") != null) { m.SetTexture("_SpecGlossMap", tex("_SpecGlossMap")); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
                m.SetFloat("_Smoothness", source.HasProperty("_GlossMapScale") ? source.GetFloat("_GlossMapScale") : 0.5f);
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        #endregion

        #region Helicopter

        public static void Helicopter()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(HelicopterModel);
            if (model == null || AssetDatabase.LoadAssetAtPath<GameObject>(VantagePlaceholders.HelicopterPrefabPath) == null) return;
            var root = PrefabUtility.LoadPrefabContents(VantagePlaceholders.HelicopterPrefabPath);
            try
            {
                var old = root.transform.Find("Model");
                if (old == null) { Debug.LogWarning("[Vantage] Helicopter prefab has no Model; not changed."); return; }
                var length = boundsIn(root.transform, old.GetComponentsInChildren<Renderer>(true)).size.z;
                Object.DestroyImmediate(old.gameObject);

                var go = place(model, root.transform, "Model");
                var renderers = go.GetComponentsInChildren<Renderer>(true);
                var scale = fit(go.transform, boundsIn(root.transform, renderers).size.z, length);

                var main = pivot(go.transform, root.transform, "4_Screw_Main", "Main Rotor");
                var tail = pivot(go.transform, root.transform, "3_Screw_Back", "Tail Rotor");
                // Skids on the ground, the main rotor's hub over the landing point.
                var b = boundsIn(root.transform, renderers);
                var hub = main != null ? root.transform.InverseTransformPoint(main.position) : b.center;
                go.transform.localPosition += new Vector3(-hub.x, -b.min.y, -hub.z);
                b = boundsIn(root.transform, renderers);
                PrefabUtility.SaveAsPrefabAsset(root, VantagePlaceholders.HelicopterPrefabPath);
                Debug.Log($"[Vantage] Helicopter model: {Path.GetFileNameWithoutExtension(HelicopterModel)}, {b.size.x:F1} x {b.size.y:F1} x {b.size.z:F1} m (scale {scale:F3}), main rotor {(main != null ? "ok" : "MISSING")}, tail rotor {(tail != null ? "ok" : "MISSING")}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// A pivot named <paramref name="name"/> on the rotor's hub (the centre of its vertices: the blades are spread
        /// evenly round it), turned like the prefab root, with the rotor node under it: VantageExtraction and the
        /// menu spin the main rotor about its Y and the tail rotor about its X.
        /// </summary>
        private static Transform pivot(Transform model, Transform root, string prefix, string name)
        {
            var node = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith(prefix) && t.name.Contains("_Lods"));
            if (node == null) return null;
            var filters = node.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToList();
            var lod0 = filters.Where(f => f.name.Contains("LOD0")).ToList();
            if (lod0.Count > 0) filters = lod0;
            if (filters.Count == 0) return null;
            var sum = Vector3.zero;
            var count = 0;
            foreach (var f in filters)
                foreach (var v in f.sharedMesh.vertices) { sum += f.transform.TransformPoint(v); count++; }
            var p = new GameObject(name).transform;
            p.SetParent(model, false);
            p.SetPositionAndRotation(sum / count, root.rotation);
            p.localScale = new Vector3(1f / model.lossyScale.x, 1f / model.lossyScale.y, 1f / model.lossyScale.z);
            node.SetParent(p, true);
            return p;
        }

        #endregion

        #region Shared

        /// <summary>Removes the old looks: every child of the root and Head except the kept parts.</summary>
        private static void clearLooks(Transform root, Transform head, bool keepEye)
        {
            foreach (var t in root.Cast<Transform>().ToList())
                if (!KeepOnRoot.Contains(t.name) || (t != head && t.name == "Head")) Object.DestroyImmediate(t.gameObject);
            foreach (var t in head.Cast<Transform>().ToList())
                if (!KeepOnHead.Contains(t.name) || (!keepEye && t.name == "Eye")) Object.DestroyImmediate(t.gameObject);
        }

        /// <summary>An unpacked instance of the model under the parent, named <paramref name="name"/>.</summary>
        private static GameObject place(GameObject model, Transform parent, string name)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = name;
            go.transform.localPosition = Vector3.zero;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            return go;
        }

        /// <summary>Scales the model so a measured size becomes the target; returns the scale factor.</summary>
        private static float fit(Transform model, float measured, float target)
        {
            if (measured <= 0.0001f || target <= 0.0001f) return 1f;
            var factor = target / measured;
            model.localScale *= factor;
            return factor;
        }

        private static Renderer[] meshRenderers(GameObject root, Renderer exclude = null) =>
            root.GetComponentsInChildren<Renderer>(true).Where(r => (r is MeshRenderer || r is SkinnedMeshRenderer) && r != exclude).ToArray();

        private static Bounds boundsIn(Transform space, Renderer[] renderers)
        {
            var b = new Bounds();
            var first = true;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var wb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? wb.min.x : wb.max.x, (i & 2) == 0 ? wb.min.y : wb.max.y, (i & 4) == 0 ? wb.min.z : wb.max.z);
                    var p = space.InverseTransformPoint(corner);
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        private static string ensureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return folder;
            ensureFolder(Path.GetDirectoryName(folder).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
            return folder;
        }

        #endregion

        /// <summary>
        /// Batch: applies the models, then the drone, turret and helicopter beside the player start from the front and
        /// the side (facing, size, muzzle). -vantageShots folder. The scene is not saved.
        /// </summary>
        public static void ShotsBatch()
        {
            All();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(VantageEditorUtil.ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            var folder = VantageEditorUtil.Arg("-vantageShots") ?? "MachineShots";
            var player = Object.FindFirstObjectByType<CoverShooter.CharacterMotor>();
            var at = player != null ? player.transform.position : Vector3.zero;
            var forward = player != null ? player.transform.forward : Vector3.forward;
            forward.y = 0; forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            if (player != null) player.gameObject.SetActive(false);
            GameObject spawn(string path, Vector3 pos)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(forward));
                return go;
            }
            var turret = spawn(VantageEditorUtil.TurretPrefabPath, at - forward * 5f);
            var drone = spawn(VantageEditorUtil.DronePrefabPath, at - forward * 5f + right * 3f + Vector3.up * 2f);
            var heli = spawn(VantagePlaceholders.HelicopterPrefabPath, at - forward * 30f);
            void two(string name, Transform t, float distance, float up)
            {
                var c = t.position + Vector3.up * up;
                VantageEditorUtil.Shot(folder, name + "_front", c + t.forward * distance + Vector3.up * distance * 0.25f, c, false, 0);
                VantageEditorUtil.Shot(folder, name + "_side", c + t.right * distance + Vector3.up * distance * 0.15f, c, false, 0);
            }
            two("turret", turret.transform, 3f, 0.6f);
            two("turret", turret.transform, 3f, 0.6f); // the first render can come out garbled
            two("drone", drone.transform, 3f, 0f);
            two("heli", heli.transform, 22f, 2f);
            // A rotor on its hub keeps its centre when turned (as VantageExtraction spins it).
            foreach (var (rotor, axis) in new[] { ("Main Rotor", Vector3.up), ("Tail Rotor", Vector3.right) })
            {
                var t = heli.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == rotor);
                if (t == null) { Debug.Log($"[Vantage] Machine shots: {rotor} MISSING"); continue; }
                var rs = t.GetComponentsInChildren<Renderer>().Where(r => r.name.Contains("LOD0")).ToArray();
                var before = boundsIn(heli.transform, rs).center;
                t.Rotate(axis * 90f, Space.Self);
                Debug.Log($"[Vantage] Machine shots: {rotor} centre moves {Vector3.Distance(before, boundsIn(heli.transform, rs).center):F2} m when turned 90°.");
            }
            VantageEditorUtil.Shot(folder, "machines_together", at + Vector3.up * 2f + right * 2f, at - forward * 8f + Vector3.up, false, 0);
            EditorApplication.Exit(0);
        }
    }
}
