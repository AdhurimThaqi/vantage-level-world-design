using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vantage.EditorTools
{
    /// <summary>
    /// Puts a humanoid model on the player rig in place of the current body, keeping everything the template hangs
    /// on the skeleton (guns in the hands, holsters, hit boxes, sight, eyes):
    /// 1. the model is imported as Humanoid and its materials are extracted to Assets/Vantage/Characters/&lt;model&gt;,
    ///    so they can be tuned without touching the source FBX;
    /// 2. old and new skeleton are both put in their avatar's T-pose, so a hand means the same thing on both;
    /// 3. every object attached to an old bone moves to the matching new bone (humanoid mapping, then bone name),
    ///    keeping its world pose, and references to old bones are pointed at the new ones;
    /// 4. the old body mesh and skeleton are deleted and the Animator gets the new avatar.
    /// Re-running with the same model rebuilds the body from the FBX.
    /// Menu: Vantage → Player → Use Character Model (uses the selected model, else DefaultModel).
    /// Batch: -executeMethod Vantage.EditorTools.VantagePlayerCharacter.ApplyBatch [-vantageModel path] [-vantageShots folder]
    /// </summary>
    public static class VantagePlayerCharacter
    {
        public const string DefaultModel = "Assets/Blender_Asset/AdhurimCharacter/Adhurim_Avatar.fbx";
        private const string CharactersFolder = "Assets/Vantage/Characters";

        [MenuItem("Vantage/Player/Use Character Model", priority = 20)]
        public static void ApplyFromMenu()
        {
            var selected = Selection.activeObject != null ? AssetDatabase.GetAssetPath(Selection.activeObject) : null;
            var model = AssetImporter.GetAtPath(selected ?? "") is ModelImporter ? selected : DefaultModel;
            if (!EditorUtility.DisplayDialog("Use character model", $"Replace the player's body with\n{model}?", "Replace", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (Apply(model))
                EditorSceneManager.OpenScene(VantageEditorUtil.ScenePath, OpenSceneMode.Single);
        }

        public static void ApplyBatch()
        {
            var ok = false;
            try
            {
                ok = Apply(VantageEditorUtil.Arg("-vantageModel") ?? DefaultModel);
                var shots = VantageEditorUtil.Arg("-vantageShots");
                if (ok && shots != null)
                    Shots(shots);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Apply(string modelPath)
        {
            var avatar = importAsHumanoid(modelPath);
            if (avatar == null)
                return false;
            extractMaterials(modelPath);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);

            var root = PrefabUtility.LoadPrefabContents(VantageEditorUtil.PlayerRigPath);
            try
            {
                var animator = root.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.GetComponent<CoverShooter.CharacterMotor>() != null);
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                {
                    Debug.LogError("[Vantage] Player rig: no CharacterMotor with a humanoid Animator found.");
                    return false;
                }
                swap(animator, model, avatar);
                PrefabUtility.SaveAsPrefabAsset(root, VantageEditorUtil.PlayerRigPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            // Re-save the level so overrides on removed bones are dropped.
            var scene = EditorSceneManager.OpenScene(VantageEditorUtil.ScenePath, OpenSceneMode.Single);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Vantage] Player character is now {Path.GetFileNameWithoutExtension(modelPath)}.");
            return true;
        }

        #region Model import

        private static Avatar importAsHumanoid(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
            {
                Debug.LogError($"[Vantage] {path} is not a model.");
                return null;
            }

            if (importer.animationType != ModelImporterAnimationType.Human || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel
                || importer.importAnimation || importer.importCameras || importer.importLights)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.SaveAndReimport();
            }

            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogError($"[Vantage] {path} could not be mapped to a humanoid avatar. Configure the avatar in its import settings.");
                return null;
            }
            return avatar;
        }

        private static string folderFor(string modelPath) => $"{CharactersFolder}/{Path.GetFileNameWithoutExtension(modelPath)}";

        /// <summary>
        /// Embedded textures and materials → files in Assets/Vantage/Characters/&lt;model&gt;, so they can be tuned
        /// without touching the FBX. Unity can't link the extracted textures itself (Blender names packed images
        /// "Image_1" without extension), so the links are read from the FBX (FbxTextureLinks) and set here.
        /// Existing material files are kept, so hand-tuning survives a re-run.
        /// </summary>
        private static void extractMaterials(string path)
        {
            var folder = folderFor(path);
            var textures = folder + "/Textures";
            ensureFolder(folder);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);

            if (!AssetDatabase.IsValidFolder(textures) || AssetDatabase.FindAssets("t:Texture", new[] { textures }).Length == 0)
            {
                AssetDatabase.DeleteAsset(textures);
                ensureFolder(textures);
                if (!importer.ExtractTextures(textures))
                    Debug.LogWarning($"[Vantage] {path} has no embedded textures to extract.");
                addImageExtensions(textures);
                AssetDatabase.Refresh();
            }

            // Material files deleted by hand are extracted again.
            var dangling = importer.GetExternalObjectMap().Where(r => r.Key.type == typeof(Material) && r.Value == null).ToList();
            foreach (var remap in dangling)
                importer.RemoveRemap(remap.Key);
            if (dangling.Count > 0)
                importer.SaveAndReimport();

            var links = FbxTextureLinks.Read(path);
            var created = false;
            foreach (var embedded in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
            {
                var target = $"{folder}/{embedded.name}.mat";
                if (File.Exists(target)) continue;
                var error = AssetDatabase.ExtractAsset(embedded, target);
                if (!string.IsNullOrEmpty(error)) { Debug.LogWarning($"[Vantage] Extracting {embedded.name}: {error}"); continue; }
                created = true;
                var m = AssetDatabase.LoadAssetAtPath<Material>(target);
                var cutout = false;
                foreach (var (property, file) in links.Where(l => l.material == m.name).Select(l => (l.property, l.file)))
                {
                    var texture = findTexture(textures, file);
                    if (texture == null) { Debug.LogWarning($"[Vantage] {m.name}: texture {file} not found."); continue; }
                    if (property == "DiffuseColor") { m.SetTexture("_BaseMap", texture); m.SetTexture("_MainTex", texture); m.SetColor("_BaseColor", Color.white); }
                    else if (property == "NormalMap") { markNormalMap(texture); m.SetTexture("_BumpMap", texture); m.EnableKeyword("_NORMALMAP"); }
                    else if (property == "TransparencyFactor") cutout = true;
                }
                setSurface(m, cutout);
            }
            if (created)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();

            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                var maps = string.Join(", ", m.GetTexturePropertyNames().Where(p => p != "_MainTex" && m.GetTexture(p) != null).Select(p => $"{p}={m.GetTexture(p).name}"));
                Debug.Log($"[Vantage] Character material {m.name}: {m.shader.name}, {(m.IsKeywordEnabled("_ALPHATEST_ON") ? "cutout" : "opaque")}, {maps}");
            }
        }

        /// <summary>
        /// Opaque, or alpha-clipped and two-sided for hair (sorts and casts shadows correctly, unlike blending).
        /// Glossy imports are toned down so skin and cloth don't mirror the sky.
        /// </summary>
        private static void setSurface(Material m, bool cutout)
        {
            m.SetFloat("_Surface", 0f);
            m.SetOverrideTag("RenderType", cutout ? "TransparentCutout" : "Opaque");
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.Zero);
            m.SetFloat("_ZWrite", 1f);
            m.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            if (cutout) m.EnableKeyword("_ALPHATEST_ON"); else m.DisableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cutoff", 0.4f);
            m.SetFloat("_Cull", cutout ? (float)CullMode.Off : (float)CullMode.Back);
            m.renderQueue = cutout ? (int)RenderQueue.AlphaTest : -1;
            if (m.HasProperty("_Smoothness"))
                m.SetFloat("_Smoothness", Mathf.Min(m.GetFloat("_Smoothness"), 0.35f));
            EditorUtility.SetDirty(m);
        }

        private static Texture findTexture(string folder, string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name, new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name)
                    return AssetDatabase.LoadAssetAtPath<Texture>(path);
            }
            return null;
        }

        /// <summary>Blender packs images without an extension; Unity only imports them as textures with one.</summary>
        private static void addImageExtensions(string folder)
        {
            foreach (var file in Directory.GetFiles(folder).Where(f => Path.GetExtension(f) == ""))
            {
                var head = new byte[4];
                using (var s = File.OpenRead(file)) s.Read(head, 0, 4);
                var extension = head[0] == 0xFF && head[1] == 0xD8 ? ".jpg" : head[0] == 0x89 && head[1] == 0x50 ? ".png" : null;
                if (extension == null) continue;
                File.Delete(file + ".meta");
                File.Move(file, file + extension);
            }
        }

        /// <summary>Textures used as normal maps must be imported as normal maps.</summary>
        private static void markNormalMap(Texture texture)
        {
            if (texture == null || !(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) is TextureImporter ti)
                || ti.textureType == TextureImporterType.NormalMap) return;
            ti.textureType = TextureImporterType.NormalMap;
            ti.SaveAndReimport();
        }

        private static void ensureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            ensureFolder(Path.GetDirectoryName(folder).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
        }

        #endregion

        #region Body swap

        private static void swap(Animator animator, GameObject model, Avatar avatar)
        {
            var body = animator.transform;
            var oldDescription = animator.avatar.humanDescription;
            var newDescription = avatar.humanDescription;

            // The old skeleton: transforms named in the old avatar, reached from the body through bones only
            // (so a gun's "LeftHand" grip marker is never taken for a bone).
            var oldNames = new HashSet<string>(oldDescription.skeleton.Select(s => s.name));
            var oldBones = new List<Transform>();
            collectBones(body, oldNames, oldBones);
            var oldBoneSet = new HashSet<Transform>(oldBones);
            var oldRenderers = body.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                                   .Where(r => r.rootBone != null && oldBoneSet.Contains(r.rootBone)).ToList();
            var template = oldRenderers.FirstOrDefault();
            var bodyLayer = template != null ? template.gameObject.layer : body.gameObject.layer;
            var boneLayer = oldBones.Count > 0 ? oldBones[0].gameObject.layer : body.gameObject.layer;

            // The new body goes directly under the Animator, as in the model file, so the avatar's bone paths match.
            var instance = Object.Instantiate(model);
            var added = instance.transform.Cast<Transform>().ToList();
            foreach (var child in added)
                child.SetParent(body, false);
            Object.DestroyImmediate(instance);
            var newBones = added.SelectMany(c => c.GetComponentsInChildren<Transform>(true)).ToList();
            var newByName = new Dictionary<string, Transform>();
            foreach (var t in newBones)
                newByName[t.name] = t;

            var oldHeight = height(body, oldBones, oldDescription);
            var newHeight = height(body, newBones, newDescription);
            applyTPose(body, oldBones, oldDescription);
            applyTPose(body, newBones, newDescription);

            // Old bone → new bone: the humanoid mapping first, then the same name without a rig prefix (mixamorig:).
            var oldToHuman = oldDescription.human.ToDictionary(h => h.boneName, h => h.humanName);
            var humanToNew = newDescription.human.ToDictionary(h => h.humanName, h => h.boneName);
            Transform counterpart(Transform old)
            {
                if (oldToHuman.TryGetValue(old.name, out var human) && humanToNew.TryGetValue(human, out var name) && newByName.TryGetValue(name, out var t))
                    return t;
                var bare = old.name.Substring(old.name.LastIndexOf(':') + 1);
                return newByName.TryGetValue(bare, out t) ? t : null;
            }
            Transform nearest(Transform old)
            {
                for (var t = old; t != null && t != body; t = t.parent)
                {
                    var c = oldBoneSet.Contains(t) ? counterpart(t) : null;
                    if (c != null) return c;
                }
                return newByName.TryGetValue(newDescription.human.First(h => h.humanName == "Hips").boneName, out var hips) ? hips : body;
            }

            // References to old bones and old body meshes from the rest of the rig (template components, Vantage scripts).
            var oldRendererObjects = new HashSet<Transform>(oldRenderers.Select(r => r.transform));
            var references = new List<(SerializedObject so, string path, Object value)>();
            foreach (var component in body.root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform || oldRendererObjects.Contains(component.transform)) continue;
                var so = new SerializedObject(component);
                var p = so.GetIterator();
                while (p.Next(true))
                {
                    if (p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue == null) continue;
                    var value = p.objectReferenceValue;
                    var t = value is GameObject g ? g.transform : value is Component c ? c.transform : null;
                    if (t != null && (oldBoneSet.Contains(t) && (value is GameObject || value is Transform) || oldRendererObjects.Contains(t)))
                        references.Add((so, p.propertyPath, value));
                }
            }
            var referenced = new HashSet<Transform>(references.Select(r => r.value is GameObject g ? g.transform : ((Component)r.value).transform));

            // Attachments: anything on the old skeleton that isn't a bone, plus bones the new model has no match for
            // but something still uses (template eye/jaw helpers).
            var moved = 0;
            foreach (var bone in oldBones)
            {
                if (!isUnderOldSkeleton(bone, body, oldBoneSet)) continue; // inside a subtree that was already moved
                foreach (var child in bone.Cast<Transform>().ToList())
                {
                    var isBone = oldBoneSet.Contains(child);
                    if (isBone && (counterpart(child) != null || !(referenced.Contains(child) || hasAttachment(child, oldBoneSet))))
                        continue;
                    child.SetParent(nearest(bone), true);
                    moved++;
                }
            }

            // The new body's main mesh (most vertices) stands in for the old body mesh.
            var mainRenderer = added.SelectMany(c => c.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                                    .OrderByDescending(r => r.sharedMesh != null ? r.sharedMesh.vertexCount : 0).FirstOrDefault();
            var remapped = 0;
            foreach (var (so, path, value) in references)
            {
                Object target;
                if (value is SkinnedMeshRenderer || value is Renderer) target = mainRenderer;
                else
                {
                    var bone = value is GameObject g ? g.transform : (Transform)value;
                    if (oldRendererObjects.Contains(bone)) target = mainRenderer != null ? mainRenderer.transform : null;
                    else if (!isUnderOldSkeleton(bone, body, oldBoneSet)) continue; // moved as an attachment: still valid
                    else target = counterpart(bone) ?? nearest(bone);
                    if (target != null && value is GameObject) target = ((Transform)target).gameObject;
                }
                if (target == null) continue;
                so.Update();
                so.FindProperty(path).objectReferenceValue = target;
                so.ApplyModifiedPropertiesWithoutUndo();
                remapped++;
            }

            // The new renderers take over the old ones' settings and layer (the Scope layer hides the body when aiming down a scope).
            foreach (var r in added.SelectMany(c => c.GetComponentsInChildren<SkinnedMeshRenderer>(true)))
            {
                if (template != null)
                {
                    r.shadowCastingMode = template.shadowCastingMode;
                    r.receiveShadows = template.receiveShadows;
                    r.quality = template.quality;
                    r.updateWhenOffscreen = template.updateWhenOffscreen;
                    r.skinnedMotionVectors = template.skinnedMotionVectors;
                    r.lightProbeUsage = template.lightProbeUsage;
                }
                r.gameObject.layer = bodyLayer;
            }
            foreach (var t in newBones)
                if (t.GetComponent<Renderer>() == null)
                    t.gameObject.layer = boneLayer;

            foreach (var r in oldRenderers)
                Object.DestroyImmediate(r.transform.childCount == 0 && r.GetComponents<Component>().Length == 2 ? r.gameObject : (Object)r);
            foreach (var top in oldBones.Where(b => b != null && b.parent == body).ToList())
                Object.DestroyImmediate(top.gameObject);

            disableFaceWithoutShapes(body.root);
            animator.avatar = avatar;
            body.name = model.name;
            Debug.Log($"[Vantage] Body swap: {oldRenderers.Count} old meshes and {oldBones.Count} old bones replaced by {added.Count} objects from {model.name}; "
                      + $"{moved} attachments moved, {remapped} references remapped; height {oldHeight:F2} m → {newHeight:F2} m.");
        }

        /// <summary>
        /// The template's CharacterFace sets blend shapes 0–9 by index (the Cowboy's angry, closed-eyes, mouth shapes …).
        /// A body without them would throw every frame, so the face is switched off; one with ten or more shapes keeps
        /// it, with a note that the indices may mean something else on that mesh.
        /// </summary>
        private static void disableFaceWithoutShapes(Transform root)
        {
            const int shapesUsed = 10;
            foreach (var face in root.GetComponentsInChildren<CoverShooter.CharacterFace>(true))
            {
                var mesh = face.Mesh != null ? face.Mesh.GetComponent<SkinnedMeshRenderer>() : null;
                var count = mesh != null && mesh.sharedMesh != null ? mesh.sharedMesh.blendShapeCount : 0;
                if (count >= shapesUsed)
                {
                    Debug.Log($"[Vantage] CharacterFace drives the first {shapesUsed} blend shapes of {mesh.name}; check they are facial expressions.");
                    continue;
                }
                face.Mesh = null;
                face.enabled = false;
                Debug.Log($"[Vantage] CharacterFace switched off: the body has {count} blend shapes, the face needs {shapesUsed}.");
            }
        }

        private static void collectBones(Transform parent, HashSet<string> names, List<Transform> bones)
        {
            foreach (Transform child in parent)
                if (names.Contains(child.name))
                {
                    bones.Add(child);
                    collectBones(child, names, bones);
                }
        }

        private static bool hasAttachment(Transform bone, HashSet<Transform> bones) =>
            bone.GetComponentsInChildren<Transform>(true).Any(t => !bones.Contains(t));

        /// <summary>Still part of the old skeleton (not moved onto the new one as an attachment)?</summary>
        private static bool isUnderOldSkeleton(Transform t, Transform body, HashSet<Transform> bones)
        {
            for (var p = t.parent; p != null; p = p.parent)
            {
                if (p == body) return true;
                if (!bones.Contains(p)) return false;
            }
            return false;
        }

        /// <summary>Puts the bones in the pose the avatar was built from (its T-pose).</summary>
        private static void applyTPose(Transform body, List<Transform> bones, HumanDescription description)
        {
            var byName = new Dictionary<string, Transform>();
            foreach (var t in bones) byName[t.name] = t;
            foreach (var s in description.skeleton)
            {
                if (!byName.TryGetValue(s.name, out var t)) continue;
                t.localPosition = s.position;
                t.localRotation = s.rotation;
                t.localScale = s.scale;
            }
        }

        /// <summary>Head height above the character's feet, for the log.</summary>
        private static float height(Transform body, List<Transform> bones, HumanDescription description)
        {
            var head = description.human.FirstOrDefault(h => h.humanName == "Head").boneName;
            var t = bones.FirstOrDefault(b => b.name == head);
            return t != null ? body.InverseTransformPoint(t.position).y : 0f;
        }

        #endregion

        /// <summary>The player from the front and from the camera's side, for checking without playing.</summary>
        public static void Shots(string folder)
        {
            var player = Object.FindFirstObjectByType<CoverShooter.CharacterMotor>();
            if (player == null) return;
            var t = player.transform;
            var chest = t.position + Vector3.up * 1.2f;
            VantageEditorUtil.Shot(folder, "player_front", chest + t.forward * 2.6f + Vector3.up * 0.2f, chest, false, 0);
            VantageEditorUtil.Shot(folder, "player_back", chest - t.forward * 3f + t.right * 0.8f + Vector3.up * 0.5f, chest + t.forward * 2f, false, 0);
            VantageEditorUtil.Shot(folder, "player_side", chest + t.right * 2.4f, chest, false, 0);
        }
    }

    /// <summary>
    /// Reads which texture file feeds which material slot from a binary FBX (7.x): Texture objects carry the file
    /// name, connections ("OP") tie them to a material property (DiffuseColor, NormalMap, TransparencyFactor).
    /// Only the object headers and connections are read; geometry arrays are skipped by their offsets.
    /// </summary>
    public static class FbxTextureLinks
    {
        private class Node
        {
            public string Name;
            public readonly List<object> Properties = new List<object>();
            public readonly List<Node> Children = new List<Node>();
        }

        public static List<(string material, string property, string file)> Read(string path)
        {
            var result = new List<(string, string, string)>();
            var data = File.ReadAllBytes(path);
            if (data.Length < 27 || System.Text.Encoding.ASCII.GetString(data, 0, 18) != "Kaydara FBX Binary")
            {
                Debug.LogWarning($"[Vantage] {path} is not a binary FBX; texture links not read.");
                return result;
            }
            var wide = System.BitConverter.ToUInt32(data, 23) >= 7500;
            var top = new List<Node>();
            for (long o = 27; o < data.Length;)
            {
                var n = node(data, ref o, wide);
                if (n == null) break;
                top.Add(n);
            }

            var names = new Dictionary<long, (string type, string name, string file)>();
            foreach (var n in top.Where(n => n.Name == "Objects").SelectMany(n => n.Children))
            {
                if (n.Properties.Count < 2 || !(n.Properties[0] is long id) || !(n.Properties[1] is string name)) continue;
                var file = n.Children.FirstOrDefault(c => c.Name == "RelativeFilename")?.Properties.FirstOrDefault() as string ?? "";
                names[id] = (n.Name, name.Split('\0')[0], Path.GetFileNameWithoutExtension(file.Replace('\\', '/')));
            }
            foreach (var c in top.Where(n => n.Name == "Connections").SelectMany(n => n.Children))
            {
                if (c.Properties.Count < 4 || (c.Properties[0] as string) != "OP") continue;
                if (!(c.Properties[1] is long from) || !(c.Properties[2] is long to)) continue;
                if (names.TryGetValue(from, out var texture) && texture.type == "Texture" && names.TryGetValue(to, out var material) && material.type == "Material")
                    result.Add((material.name, (string)c.Properties[3], texture.file));
            }
            return result;
        }

        private static Node node(byte[] d, ref long o, bool wide)
        {
            long end, count, length;
            if (wide) { end = (long)System.BitConverter.ToUInt64(d, (int)o); count = (long)System.BitConverter.ToUInt64(d, (int)o + 8); length = (long)System.BitConverter.ToUInt64(d, (int)o + 16); o += 24; }
            else { end = System.BitConverter.ToUInt32(d, (int)o); count = System.BitConverter.ToUInt32(d, (int)o + 4); length = System.BitConverter.ToUInt32(d, (int)o + 8); o += 12; }
            int nameLength = d[o];
            var n = new Node { Name = System.Text.Encoding.ASCII.GetString(d, (int)o + 1, nameLength) };
            o += 1 + nameLength;
            if (end == 0) return null;

            var p = o;
            for (long i = 0; i < count; i++)
                n.Properties.Add(property(d, ref p));
            o += length;
            var sentinel = wide ? 25 : 13;
            while (o < end - sentinel)
            {
                var child = node(d, ref o, wide);
                if (child == null) break;
                n.Children.Add(child);
            }
            o = end;
            return n;
        }

        private static object property(byte[] d, ref long o)
        {
            var type = (char)d[o++];
            var at = (int)o;
            switch (type)
            {
                case 'Y': o += 2; return (long)System.BitConverter.ToInt16(d, at);
                case 'C': o += 1; return (long)d[at];
                case 'I': o += 4; return (long)System.BitConverter.ToInt32(d, at);
                case 'L': o += 8; return System.BitConverter.ToInt64(d, at);
                case 'F': o += 4; return (double)System.BitConverter.ToSingle(d, at);
                case 'D': o += 8; return System.BitConverter.ToDouble(d, at);
                case 'S':
                case 'R':
                    var size = System.BitConverter.ToInt32(d, at);
                    o += 4 + size;
                    return type == 'S' ? System.Text.Encoding.UTF8.GetString(d, at + 4, size) : null;
                default: // arrays: count, encoding, byte length, data
                    o += 12 + System.BitConverter.ToUInt32(d, at + 8);
                    return null;
            }
        }
    }
}
