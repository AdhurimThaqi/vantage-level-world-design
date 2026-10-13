using UnityEditor;
using UnityEngine;
using Vantage;

public static class VantageTowerSetup
{
    [MenuItem("VANTAGE/Add Tower To Open Scene")]
    public static void AddTower()
    {
        var level = Place("VANTAGE_Tower_Level");
        var gameplay = Place("VANTAGE_Tower_Gameplay");
        if (level == null || gameplay == null)
        {
            EditorUtility.DisplayDialog("VANTAGE", "Could not find VANTAGE_Tower_Level.fbx / VANTAGE_Tower_Gameplay.fbx in the project.", "OK");
            return;
        }

        var root = new GameObject("VANTAGE_Tower");
        Undo.RegisterCreatedObjectUndo(root, "Add VANTAGE Tower");
        level.transform.SetParent(root.transform, false);
        gameplay.transform.SetParent(root.transform, false);

        if (Object.FindAnyObjectByType<VantageLevelManager>() == null)
        {
            var mgr = new GameObject("VANTAGE_LevelManager");
            mgr.AddComponent<VantageLevelManager>();
            mgr.transform.SetParent(root.transform, false);
        }

        // Player start: just outside the main entrance, facing the building
        var l = GameObject.Find("DOOR_Entrance_L");
        var r = GameObject.Find("DOOR_Entrance_R");
        if (l && r)
        {
            Vector3 door = (l.transform.position + r.transform.position) * 0.5f;
            Bounds b = new Bounds(door, Vector3.zero);
            foreach (var rend in level.GetComponentsInChildren<Renderer>()) b.Encapsulate(rend.bounds);
            Vector3 outDir = door - new Vector3(b.center.x, door.y, b.center.z);
            Vector3 n = Mathf.Abs(outDir.x) > Mathf.Abs(outDir.z) ? new Vector3(Mathf.Sign(outDir.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(outDir.z));
            var start = new GameObject("PlayerStart (move your player here)");
            start.transform.SetParent(root.transform, false);
            start.transform.position = new Vector3(door.x, b.min.y, door.z) + n * 3f;
            start.transform.rotation = Quaternion.LookRotation(-n, Vector3.up);
        }
        Selection.activeGameObject = root;
        Debug.Log("VANTAGE tower added. Move 'PlayerStart' onto your terrain and put your player there (tag: Player).");
    }

    static GameObject Place(string modelName)
    {
        foreach (var guid in AssetDatabase.FindAssets(modelName + " t:Model"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(modelName + ".fbx")) continue;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return (GameObject)PrefabUtility.InstantiatePrefab(asset);
        }
        return null;
    }

    [MenuItem("VANTAGE/Reimport Tower Models")]
    public static void Reimport()
    {
        foreach (var name in new[] { "VANTAGE_Tower_Level", "VANTAGE_Tower_Gameplay" })
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Model"))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
    }
}
