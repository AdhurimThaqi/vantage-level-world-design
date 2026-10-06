using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Vantage.EditorTools
{
    /// <summary>
    /// Lists files in the third-party packs that nothing in the game depends on, with the space they take.
    /// Report only: nothing is deleted. Scripts, shaders and editor resources always count as used,
    /// because code can load them by name.
    /// </summary>
    public static class VantageUnusedAssets
    {
        private static readonly string[] Packs =
        {
            "Assets/ThirdPersonCoverShooter", "Assets/PistolAnimsetPro", "Assets/LoafbrrAssets",
            "Assets/RPG_FPS_game_assets_industrial", "Assets/AlterunaFPS", "Assets/TutorialInfo",
        };

        private static readonly string[] AlwaysKeepExtensions = { ".cs", ".shader", ".cginc", ".hlsl", ".asmdef", ".asmref", ".compute", ".dll" };

        [MenuItem("Vantage/Project/Report Unused Pack Assets", priority = 60)]
        public static void RunFromMenu()
        {
            var path = Write();
            EditorUtility.RevealInFinder(path);
        }

        public static void RunBatch()
        {
            var exitCode = 0;
            try { Write(); }
            catch (System.Exception e) { Debug.LogException(e); exitCode = 1; }
            EditorApplication.Exit(exitCode);
        }

        public static string Write()
        {
            // Roots: the level scene, all of our own assets, project settings assets, Resources and editor resources.
            var roots = new List<string> { VantageAutoSetup.ScenePath };
            roots.AddRange(AssetDatabase.FindAssets("", new[] { "Assets/Vantage", "Assets/Settings" }).Select(AssetDatabase.GUIDToAssetPath));
            roots.AddRange(AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && (p.Contains("/Resources/") || p.Contains("/Editor Default Resources/"))));

            // Assets our editor tools load by path (gun models for pickups, soldier, sounds) count as used too.
            roots.AddRange(pathsNamedInEditorScripts());
            var used = new HashSet<string>(AssetDatabase.GetDependencies(roots.Distinct().ToArray(), true));
            LastUnused.Clear();
            var unused = new Dictionary<string, List<(string path, long size)>>();
            var usedSize = new Dictionary<string, long>();

            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                var pack = Packs.FirstOrDefault(p => path.StartsWith(p + "/"));
                if (pack == null || AssetDatabase.IsValidFolder(path) || !File.Exists(path))
                    continue;

                var size = new FileInfo(path).Length;
                var keep = used.Contains(path) || AlwaysKeepExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

                if (keep)
                {
                    usedSize[pack] = (usedSize.TryGetValue(pack, out var s) ? s : 0) + size;
                    continue;
                }

                if (!unused.TryGetValue(pack, out var list))
                    unused[pack] = list = new List<(string, long)>();
                list.Add((path, size));
                LastUnused.Add(path);
            }

            // Non-asset files (zip, blend sources) that Unity does not import still take space in the repository.
            foreach (var pack in Packs)
            {
                if (!Directory.Exists(pack))
                    continue;
                foreach (var file in Directory.GetFiles(pack, "*.zip", SearchOption.AllDirectories).Concat(Directory.GetFiles(pack, "*.blend*", SearchOption.AllDirectories)))
                {
                    var p = file.Replace('\\', '/');
                    if (!unused.TryGetValue(pack, out var list))
                        unused[pack] = list = new List<(string, long)>();
                    if (!list.Any(x => x.path == p))
                    {
                        list.Add((p, new FileInfo(p).Length));
                        LastUnused.Add(p);
                    }
                }
            }

            var report = new StringBuilder();
            report.Append("VANTAGE unused pack assets  ").Append(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append('\n');
            report.Append("Nothing has been deleted. 'Used' = needed by the level scene, Assets/Vantage, settings or Resources.\n\n");

            long totalUnused = 0, totalUsed = 0;
            foreach (var pack in Packs)
            {
                var u = unused.TryGetValue(pack, out var list) ? list.Sum(x => x.size) : 0;
                var k = usedSize.TryGetValue(pack, out var s) ? s : 0;
                totalUnused += u;
                totalUsed += k;
                report.Append($"{pack,-45} used {mb(k),8}   unused {mb(u),8}   ({(list?.Count ?? 0)} files)\n");
            }
            report.Append($"\nTOTAL                                         used {mb(totalUsed),8}   unused {mb(totalUnused),8}\n");

            foreach (var pack in Packs)
            {
                if (!unused.TryGetValue(pack, out var list) || list.Count == 0)
                    continue;
                report.Append($"\n== {pack} (largest first)\n");
                foreach (var (path, size) in list.OrderByDescending(x => x.size))
                    report.Append($"{mb(size),8}  {path}\n");
            }

            var output = Path.Combine(Path.GetDirectoryName(Application.dataPath), "UnusedAssetsReport.txt");
            File.WriteAllText(output, report.ToString());
            Debug.Log($"[Vantage] Unused pack assets: {mb(totalUnused)} unused, {mb(totalUsed)} used. Report: {output}");
            return output;
        }

        /// <summary>Every unused file found by the last Write(), relative to the project folder.</summary>
        private static readonly List<string> LastUnused = new List<string>();

        /// <summary>
        /// Asset paths written as string literals in our editor scripts (e.g. "Pistol.prefab", "Barrels/.../Barrel_v1_LD1.prefab").
        /// </summary>
        private static IEnumerable<string> pathsNamedInEditorScripts()
        {
            var names = new HashSet<string>();
            foreach (var script in Directory.GetFiles("Assets/Vantage/Editor", "*.cs"))
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(script), "\"([^\"]+\\.(prefab|wav|tga|png|mat|fbx|FBX|asset))\""))
                    names.Add(m.Groups[1].Value.Replace('\\', '/'));

            foreach (var path in AssetDatabase.GetAllAssetPaths())
                foreach (var name in names)
                    if (path == name || path.EndsWith("/" + name))
                        yield return path;
        }

        /// <summary>
        /// Moves every unused pack file (and its .meta) to ../_UnusedAssets, keeping the folder structure.
        /// Counts missing references before and after; if the count rises, everything is moved back.
        /// </summary>
        public static void MoveBatch()
        {
            var exitCode = 0;
            try
            {
                var before = countMissingReferences();
                Write();

                var project = Path.GetDirectoryName(Application.dataPath);
                var target = Path.Combine(Path.GetDirectoryName(project), "_UnusedAssets");
                var moved = new List<(string from, string to)>();
                long bytes = 0;

                foreach (var relative in LastUnused)
                {
                    var from = Path.Combine(project, relative);
                    if (!File.Exists(from))
                        continue;
                    var to = Path.Combine(target, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(to));
                    bytes += new FileInfo(from).Length;
                    File.Move(from, to);
                    moved.Add((from, to));
                    if (File.Exists(from + ".meta"))
                    {
                        File.Move(from + ".meta", to + ".meta");
                        moved.Add((from + ".meta", to + ".meta"));
                    }
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var after = countMissingReferences();
                Debug.Log($"[Vantage] Moved {moved.Count} files ({mb(bytes)}). Missing references before: {before}, after: {after}.");

                if (after > before)
                {
                    foreach (var (from, to) in moved)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(from));
                        File.Move(to, from);
                    }
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    Debug.LogError("[Vantage] The move broke references, so every file was moved back. Nothing changed.");
                    exitCode = 2;
                }
                else
                {
                    removeEmptyFolders(Path.Combine(project, "Assets"));
                    AssetDatabase.Refresh();
                    File.WriteAllLines(Path.Combine(target, "MOVED_FILES.txt"), moved.Select(m => m.to));
                    Debug.Log("[Vantage] Clean-up done. Moved files are in " + target + " (list in MOVED_FILES.txt).");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }

        /// <summary>
        /// Missing scripts plus object references that point at assets which no longer exist, in the level scene
        /// and in our own prefabs.
        /// </summary>
        private static int countMissingReferences()
        {
            var count = 0;
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(VantageAutoSetup.ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                count += missingIn(root);

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Vantage" }))
            {
                var contents = PrefabUtility.LoadPrefabContents(AssetDatabase.GUIDToAssetPath(guid));
                count += missingIn(contents);
                PrefabUtility.UnloadPrefabContents(contents);
            }

            return count;
        }

        private static int missingIn(GameObject root)
        {
            var count = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                foreach (var component in t.GetComponents<Component>())
                {
                    if (component == null)
                    {
                        count++;
                        continue;
                    }

                    var so = new SerializedObject(component);
                    var property = so.GetIterator();
                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                            count++;
                }
            }
            return count;
        }

        private static void removeEmptyFolders(string folder)
        {
            foreach (var child in Directory.GetDirectories(folder))
                removeEmptyFolders(child);

            if (Directory.GetFileSystemEntries(folder).Length == 0)
            {
                Directory.Delete(folder);
                if (File.Exists(folder + ".meta"))
                    File.Delete(folder + ".meta");
            }
        }

        private static string mb(long bytes) => (bytes / 1048576f).ToString("F1") + " MB";
    }
}
