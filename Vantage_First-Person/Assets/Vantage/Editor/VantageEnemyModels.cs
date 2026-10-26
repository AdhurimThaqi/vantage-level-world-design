using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vantage.EditorTools
{
    /// <summary>
    /// Asset Store models on the enemies, applied by Setup (and the menu item):
    /// - Soldiers: the Low Poly Soldiers body on the generated soldier prefab, by the same humanoid body swap as the
    ///   player (guns, hit boxes and sight move to the new bones). Scaled to the template soldier's height; the
    ///   model's own rifle (a submesh) is cut away, the template guns are the ones that fire.
    /// - Boss: the ISO Mech under the boss prefab's "Model" (scaled to the placeholder's height), with a URP copy of
    ///   its HDRP material and an Animator (idle, walk, death). Eye and Muzzle move onto its turret and gun.
    /// A missing pack leaves that enemy as it was.
    /// </summary>
    public static class VantageEnemyModels
    {
        public const string SoldierModel = "Assets/LowPolySoldiers_demo/models/Soldier_demo.FBX";
        public const string MechModel = "Assets/RSG_UnderWater_Pack/ISO/HDRP/Models/Mech/SK_ISO_Mech.fbx";
        private const string MechPack = "Assets/RSG_UnderWater_Pack/ISO/HDRP";
        private const string Folder = "Assets/Vantage/Characters";

        [MenuItem("Vantage/Enemies/Use Asset Store Models (boss; soldiers via Set Up Tower)", priority = 91)]
        public static void BossFromMenu() => Boss();

        #region Soldier

        /// <summary>Gives the soldier being built by Setup the Low Poly Soldiers body (unpacked: a variant can't lose bones).</summary>
        public static void SoldierBody(GameObject soldier)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SoldierModel) == null) return;
            if (PrefabUtility.IsPartOfPrefabInstance(soldier))
                PrefabUtility.UnpackPrefabInstance(soldier, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            if (VantagePlayerCharacter.ImportAsHumanoid(SoldierModel) == null) return;
            matchHeight(SoldierModel, heightOf(soldier.GetComponentsInChildren<SkinnedMeshRenderer>(true)));
            refreshAvatar(SoldierModel);
            if (!VantagePlayerCharacter.SwapBody(soldier, SoldierModel, false)) return;
            cutWeapon(soldier);
        }

        private static float heightOf(IEnumerable<Renderer> renderers)
        {
            var list = renderers.Where(r => r.enabled).ToList();
            if (list.Count == 0) return 0f;
            var b = list[0].bounds;
            foreach (var r in list) b.Encapsulate(r.bounds);
            return b.size.y;
        }

        /// <summary>Import scale so the model stands as tall as the body it replaces.</summary>
        private static void matchHeight(string path, float target)
        {
            if (target <= 0 || !(AssetImporter.GetAtPath(path) is ModelImporter importer)) return;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var h = heightOf(instance.GetComponentsInChildren<Renderer>(true));
            Object.DestroyImmediate(instance);
            if (h <= 0 || Mathf.Abs(h / target - 1f) < 0.03f) return;
            importer.globalScale *= target / h;
            importer.SaveAndReimport();
            Debug.Log($"[Vantage] {Path.GetFileName(path)}: {h:F2} m tall, import scale now {importer.globalScale:F3} to match {target:F2} m.");
        }

        /// <summary>
        /// The avatar's stored skeleton must match the model as it imports now: the body swap poses the bones from
        /// it, and a pack's settings can carry one saved at another unit scale (the soldier came out 60 m tall).
        /// A stale one is cleared, so the import maps the bones again.
        /// </summary>
        private static void refreshAvatar(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return;
            var stored = importer.humanDescription.skeleton;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var actual = instance.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First().localPosition);
            Object.DestroyImmediate(instance);
            var worst = stored.Where(s => actual.ContainsKey(s.name)).Select(s => Vector3.Distance(s.position, actual[s.name])).DefaultIfEmpty(0f).Max();
            if (stored.Length > 0 && worst < 0.01f) return;
            var description = importer.humanDescription;
            description.human = new HumanBone[0];
            description.skeleton = new SkeletonBone[0];
            importer.humanDescription = description;
            importer.SaveAndReimport();
            Debug.Log($"[Vantage] {Path.GetFileName(path)}: stored avatar skeleton was off by up to {worst:F2} m; bones mapped again ({importer.humanDescription.human.Length} human bones).");
        }

        /// <summary>The model's own rifle is a submesh with a weapon material: a copy of the mesh without it.</summary>
        private static void cutWeapon(GameObject soldier)
        {
            foreach (var r in soldier.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var materials = r.sharedMaterials;
                var weapon = System.Array.FindIndex(materials, m => m != null && m.name.ToLowerInvariant().Contains("weapon"));
                if (weapon < 0 || r.sharedMesh == null) continue;
                if (materials.Length == 1) { r.enabled = false; continue; }
                var source = r.sharedMesh;
                var keep = Enumerable.Range(0, source.subMeshCount).Where(i => i != weapon).ToList();
                var mesh = Object.Instantiate(source);
                mesh.name = source.name + " (no weapon)";
                mesh.subMeshCount = keep.Count;
                for (int k = 0; k < keep.Count; k++) mesh.SetTriangles(source.GetTriangles(keep[k]), k);
                var folder = ensureFolder($"{Folder}/{Path.GetFileNameWithoutExtension(SoldierModel)}");
                var path = $"{folder}/{mesh.name}.asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
                r.sharedMesh = mesh;
                r.sharedMaterials = keep.Select(i => materials[i]).ToArray();
                Debug.Log($"[Vantage] Soldier body: the model's own rifle ({materials[weapon].name}) cut from {source.name}.");
            }
        }

        #endregion

        #region Boss

        public static void Boss()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(MechModel);
            if (model == null || AssetDatabase.LoadAssetAtPath<GameObject>(VantageTower.BossPrefabPath) == null) return;
            var material = mechMaterial();
            var controller = mechController();
            var root = PrefabUtility.LoadPrefabContents(VantageTower.BossPrefabPath);
            try
            {
                var robot = root.GetComponent<VantageHeavyRobot>();
                var old = root.transform.Find("Model");
                if (robot == null || old == null) { Debug.LogWarning("[Vantage] Boss prefab has no VantageHeavyRobot or Model; not changed."); return; }
                // The current model's height is the boss's size (the placeholder's, or the mech's on a re-run).
                var height = heightOf(old.GetComponentsInChildren<Renderer>(true));
                Object.DestroyImmediate(old.gameObject);

                var mech = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                PrefabUtility.UnpackPrefabInstance(mech, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                mech.name = "Model";
                var renderers = mech.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                    r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
                var b = boundsIn(root.transform, renderers);
                var scale = height / b.size.y;
                mech.transform.localScale *= scale;
                b = boundsIn(root.transform, renderers);
                mech.transform.localPosition += new Vector3(-b.center.x, -b.min.y, -b.center.z);
                b = boundsIn(root.transform, renderers);

                var animator = mech.GetComponent<Animator>();
                if (animator == null) animator = mech.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                robot.Model = mech.transform;
                robot.Animator = animator;

                // Body collider and agent around the new shape.
                var capsule = root.GetComponent<CapsuleCollider>();
                if (capsule != null)
                {
                    capsule.height = b.size.y;
                    capsule.radius = Mathf.Min(b.extents.x, b.extents.z) * 0.8f;
                    capsule.center = new Vector3(0, b.size.y / 2, 0);
                }

                // Eye on the front of the turret, muzzle on the upper right gun: both turn with the animation.
                var top = find(mech.transform, "Top_M") ?? mech.transform;
                if (robot.Eye != null)
                {
                    robot.Eye.transform.SetParent(top, true);
                    robot.Eye.transform.position = root.transform.TransformPoint(new Vector3(0, b.max.y * 0.8f, b.max.z * 0.9f));
                }
                var gun = find(mech.transform, "ShotgunTop_R") ?? top;
                if (robot.Muzzle != null)
                {
                    robot.Muzzle.SetParent(gun, false);
                    robot.Muzzle.localPosition = Vector3.zero;
                    robot.Muzzle.rotation = root.transform.rotation;
                }
                PrefabUtility.SaveAsPrefabAsset(root, VantageTower.BossPrefabPath);
                Debug.Log($"[Vantage] Boss model: ISO Mech, {height:F2} m tall (scale {scale:F3}), footprint {b.size.x:F1} x {b.size.z:F1} m, {controller.animationClips.Length} animations.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Bounds boundsIn(Transform root, Renderer[] renderers)
        {
            var b = new Bounds();
            var first = true;
            foreach (var r in renderers)
            {
                var wb = r.bounds;
                foreach (var corner in new[] { wb.min, wb.max, new Vector3(wb.min.x, wb.min.y, wb.max.z), new Vector3(wb.max.x, wb.max.y, wb.min.z) })
                {
                    var p = root.InverseTransformPoint(corner);
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        private static Transform find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var f = find(c, name);
                if (f != null) return f;
            }
            return null;
        }

        /// <summary>URP Lit from the pack's HDRP textures (its mask map is metallic R, occlusion G, smoothness A).</summary>
        private static Material mechMaterial()
        {
            var folder = ensureFolder($"{Folder}/ISO_Mech");
            var path = $"{folder}/M_ISO_Mech_URP.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m; // kept, so hand-tuning survives
            Texture tex(string suffix) => AssetDatabase.FindAssets("T_Mech_ t:Texture2D", new[] { MechPack + "/Textures" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => Path.GetFileNameWithoutExtension(p).EndsWith("_" + suffix))
                .Select(AssetDatabase.LoadAssetAtPath<Texture>).FirstOrDefault();
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_ISO_Mech_URP" };
            var baseMap = tex("B");
            if (baseMap != null) { m.SetTexture("_BaseMap", baseMap); m.SetColor("_BaseColor", Color.white); }
            var normal = tex("N");
            if (normal != null)
            {
                if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal)) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
                {
                    ti.textureType = TextureImporterType.NormalMap;
                    ti.SaveAndReimport();
                }
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
            }
            var mask = tex("M");
            if (mask != null)
            {
                m.SetTexture("_MetallicGlossMap", mask);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetTexture("_OcclusionMap", mask);
                m.EnableKeyword("_OCCLUSIONMAP");
                m.SetFloat("_Smoothness", 0.6f);
            }
            var emission = tex("E");
            if (emission != null)
            {
                m.SetTexture("_EmissionMap", emission);
                m.SetColor("_EmissionColor", Color.white);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>Idle ↔ walk by "Speed", any state → death on "Die" (the pack's clips; idle and walk set to loop).</summary>
        private static AnimatorController mechController()
        {
            var folder = ensureFolder($"{Folder}/ISO_Mech");
            var path = $"{folder}/Boss.controller";
            AnimationClip clip(string name, bool loop)
            {
                var file = $"{MechPack}/Animations/Mech/A_ISO_Mech_{name}_01.fbx";
                if (loop && AssetImporter.GetAtPath(file) is ModelImporter importer)
                {
                    var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
                    if (clips.Any(c => !c.loopTime))
                    {
                        foreach (var c in clips) c.loopTime = true;
                        importer.clipAnimations = clips;
                        importer.SaveAndReimport();
                    }
                }
                return AssetDatabase.LoadAllAssetsAtPath(file).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
            }
            var idle = clip("Idle", true);
            var walk = clip("WalkForward", true);
            var death = clip("Death", false);

            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            var sm = controller.layers[0].stateMachine;
            var idleState = sm.AddState("Idle");
            idleState.motion = idle;
            var walkState = sm.AddState("Walk");
            walkState.motion = walk;
            var deathState = sm.AddState("Death");
            deathState.motion = death;
            sm.defaultState = idleState;
            var toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false; toWalk.duration = 0.2f;
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.3f, "Speed");
            var toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false; toIdle.duration = 0.25f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.2f, "Speed");
            var toDeath = sm.AddAnyStateTransition(deathState);
            toDeath.hasExitTime = false; toDeath.duration = 0.1f; toDeath.canTransitionToSelf = false;
            toDeath.AddCondition(AnimatorConditionMode.If, 0, "Die");
            AssetDatabase.SaveAssets();
            return controller;
        }

        #endregion

        /// <summary>
        /// Batch: the soldier (rifle pose) and the boss beside the player start, from the front and the side, for
        /// checking size, facing and the gun in the hand. -vantageShots folder. The scene is not saved.
        /// </summary>
        public static void ShotsBatch()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(VantageEditorUtil.ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            var folder = VantageEditorUtil.Arg("-vantageShots") ?? "EnemyShots";
            var player = Object.FindFirstObjectByType<CoverShooter.CharacterMotor>();
            var at = player != null ? player.transform.position : Vector3.zero;
            var forward = player != null ? player.transform.forward : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, forward);
            var soldier = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VantageTower.SoldierPrefabPath));
            // Out in the open, away from the tower the player faces.
            soldier.transform.SetPositionAndRotation(at - forward * 6f, Quaternion.LookRotation(forward));
            var boss = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VantageTower.BossPrefabPath));
            boss.transform.SetPositionAndRotation(at - forward * 16f + right * 4f, Quaternion.LookRotation(forward));
            if (player != null) player.gameObject.SetActive(false);

            var animator = soldier.GetComponent<Animator>();
            var clips = animator != null && animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.animationClips : new AnimationClip[0];
            var clip = clips.FirstOrDefault(c => c.isHumanMotion && c.name == "Rifle_Idle") ?? clips.FirstOrDefault(c => c.isHumanMotion && c.name.ToLowerInvariant().Contains("rifle"));
            var inventory = soldier.GetComponent<CoverShooter.CharacterInventory>();
            if (inventory != null && inventory.Weapons != null)
                foreach (var w in inventory.Weapons)
                    if (w.RightItem != null) w.RightItem.SetActive(w.RightItem.name.StartsWith("Rifle"));
            foreach (var r in soldier.GetComponentsInChildren<SkinnedMeshRenderer>())
                r.forceMatrixRecalculationPerRender = true;
            var chest = soldier.transform.position + Vector3.up * 1.2f;
            AnimationMode.StartAnimationMode();
            try
            {
                string facing()
                {
                    var l = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position; var r = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg).position;
                    var f = Vector3.Cross(r - l, Vector3.up).normalized;
                    var hand = animator.GetBoneTransform(HumanBodyBones.RightHand).position;
                    return $"hips forward·root {Vector3.Dot(f, soldier.transform.forward):F2}, right hand local {soldier.transform.InverseTransformPoint(hand):F2}, {gunToHand(soldier, animator)}";
                }
                Debug.Log($"[Vantage] Enemy shots: soldier bind pose: {facing()}");
                if (clip != null)
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(soldier, clip, clip.length * 0.5f);
                    AnimationMode.EndSampling();
                }
                Debug.Log($"[Vantage] Enemy shots: soldier in '{(clip != null ? clip.name : "bind pose")}': {facing()}.");
                VantageEditorUtil.Shot(folder, "soldier_front", chest + soldier.transform.forward * 2.8f + Vector3.up * 0.2f, chest, false, 0);
                VantageEditorUtil.Shot(folder, "soldier_front", chest + soldier.transform.forward * 2.8f + Vector3.up * 0.2f, chest, false, 0); // the first render can come out garbled
                var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (hand != null) VantageEditorUtil.Shot(folder, "soldier_hands", hand.position + soldier.transform.right * 1.1f + soldier.transform.forward * 0.4f + Vector3.up * 0.2f, hand.position, false, 0);
                VantageEditorUtil.Shot(folder, "soldier_side", chest + soldier.transform.right * 2.6f, chest, false, 0);
            }
            finally
            {
                AnimationMode.StopAnimationMode();
            }
            var bossCentre = boss.transform.position + Vector3.up * 1.8f;
            VantageEditorUtil.Shot(folder, "boss_front", bossCentre + boss.transform.forward * 8f + Vector3.up * 1.5f, bossCentre, false, 0);
            VantageEditorUtil.Shot(folder, "boss_side", bossCentre + boss.transform.right * 8f + Vector3.up * 1f, bossCentre, false, 0);
            VantageEditorUtil.Shot(folder, "enemies_together", at - forward * 1f + right * 2f + Vector3.up * 2.2f, at - forward * 12f + Vector3.up * 1.5f, false, 0);
            EditorApplication.Exit(0);
        }

        private static string gunToHand(GameObject character, Animator animator)
        {
            var inventory = character.GetComponent<CoverShooter.CharacterInventory>();
#pragma warning disable 0618
            var weapons = inventory != null && inventory.Weapons != null && inventory.Weapons.Length > 0 ? inventory.Weapons : character.GetComponent<CoverShooter.CharacterMotor>().Weapons;
#pragma warning restore 0618
            var rifle = weapons.Select(w => w.RightItem).FirstOrDefault(i => i != null && i.name.StartsWith("Rifle"));
            if (rifle == null) return "no rifle";
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            return $"rifle {Vector3.Distance(rifle.transform.position, hand.position):F2} m from the right hand (parent {rifle.transform.parent.name})";
        }

        private static string ensureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return folder;
            ensureFolder(Path.GetDirectoryName(folder).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
            return folder;
        }
    }
}
