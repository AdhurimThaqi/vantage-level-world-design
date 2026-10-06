using System.Collections.Generic;
using System.IO;
using CoverShooter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Vantage.EditorTools
{
    /// <summary>
    /// Pulls the Cover Shooter third-person player (character, camera and HUD) out of the template's
    /// Night scene into one prefab, and wires it into a level next to the first-person player.
    /// </summary>
    public static class VantageThirdPerson
    {
        public const string RigPrefabPath = "Assets/Vantage/Prefabs/Third Person Rig.prefab";
        public const string SourceScenePath = "Assets/Vantage/_Import/TemplateNightScene.unity";
        private const string RigName = "Third Person Rig";
        private const string SwitchName = "Vantage View Switch";

        public static GameObject EnsureRigPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
            if (existing != null)
                return existing;

            if (!File.Exists(SourceScenePath))
            {
                Debug.LogError("[Vantage] Template scene not found at " + SourceScenePath + ", cannot create the third-person rig.");
                return null;
            }

            EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);

            var input = Object.FindFirstObjectByType<ThirdPersonInput>();
            var camera = Object.FindFirstObjectByType<ThirdPersonCamera>();
            if (input == null || camera == null)
            {
                Debug.LogError("[Vantage] Template scene has no ThirdPersonInput/ThirdPersonCamera.");
                return null;
            }

            var player = input.gameObject;
            Debug.Log("[Vantage] Template player: " + path(player.transform) + ", camera: " + path(camera.transform));

            // Every canvas that shows Cover Shooter UI (crosshair, ammo, health) is part of the player's HUD.
            var huds = new HashSet<GameObject>();
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour == null || behaviour.GetType().Namespace != "CoverShooter")
                    continue;

                var canvas = behaviour.GetComponentInParent<Canvas>(true);
                if (canvas != null)
                    huds.Add(canvas.rootCanvas.gameObject);
            }

            var rig = new GameObject(RigName);
            var parts = new List<GameObject> { player, camera.gameObject };
            parts.AddRange(huds);

            foreach (var part in parts)
            {
                if (part.transform.IsChildOf(rig.transform))
                    continue;

                Debug.Log("[Vantage] Adding to rig: " + path(part.transform));
                part.transform.SetParent(rig.transform, true);
            }

            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

            var actor = player.GetComponent<BaseActor>();
            if (actor != null)
                actor.Side = 1;

            reportOutsideReferences(rig);

            Directory.CreateDirectory(Path.GetDirectoryName(RigPrefabPath));
            var prefab = PrefabUtility.SaveAsPrefabAsset(rig, RigPrefabPath, out var success);
            Debug.Log(success ? "[Vantage] Saved " + RigPrefabPath : "[Vantage] Failed to save the third-person rig prefab.");
            return success ? prefab : null;
        }

        /// <summary>
        /// Adds the rig (inactive) and the V-key switch to the open scene. Replaces earlier copies.
        /// </summary>
        public static VantageViewSwitch AddToScene(VantagePlayer firstPerson, GameObject[] firstPersonCameras)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
            if (prefab == null || firstPerson == null)
                return null;

            foreach (var old in Object.FindObjectsByType<VantageViewSwitch>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (old.ThirdPersonRig != null)
                    Object.DestroyImmediate(old.ThirdPersonRig);
                Object.DestroyImmediate(old.gameObject);
            }

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var character = rig.GetComponentInChildren<CharacterMotor>(true);
            var start = firstPerson.transform;
            var yaw = Quaternion.Euler(0, start.eulerAngles.y, 0);
            character.transform.SetPositionAndRotation(start.position, yaw);

            var tpsCamera = rig.GetComponentInChildren<ThirdPersonCamera>(true);
            if (tpsCamera != null)
                tpsCamera.transform.SetPositionAndRotation(start.position - yaw * Vector3.forward * 4f + Vector3.up * 2f, yaw);

            rig.SetActive(false);

            var switcher = new GameObject(SwitchName).AddComponent<VantageViewSwitch>();
            switcher.FirstPersonPlayer = firstPerson;
            switcher.FirstPersonCameras = firstPersonCameras;
            switcher.ThirdPersonRig = rig;
            switcher.ThirdPersonCharacter = character;

            Debug.Log("[Vantage] Third-person rig added. Press V in play mode to switch view.");
            return switcher;
        }

        private static void reportOutsideReferences(GameObject rig)
        {
            foreach (var component in rig.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                    continue;

                var so = new SerializedObject(component);
                var property = so.GetIterator();

                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null)
                        continue;

                    GameObject target = null;
                    if (property.objectReferenceValue is GameObject go)
                        target = go;
                    else if (property.objectReferenceValue is Component c)
                        target = c.gameObject;

                    if (target != null && target.scene.IsValid() && !target.transform.IsChildOf(rig.transform))
                        Debug.LogWarning($"[Vantage] {path(component.transform)} ({component.GetType().Name}.{property.propertyPath}) points outside the rig to {path(target.transform)}; it will be empty in the prefab.");
                }
            }
        }

        private static string path(Transform t)
        {
            return t.parent == null ? t.name : path(t.parent) + "/" + t.name;
        }
    }
}
