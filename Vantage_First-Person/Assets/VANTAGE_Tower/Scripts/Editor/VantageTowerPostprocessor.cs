using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Vantage;

/// Runs automatically when VANTAGE_Tower_Level.fbx / VANTAGE_Tower_Gameplay.fbx are imported:
/// import settings, colliders, static flags, doors, pickups, triggers, interactables and material fixes.
public class VantageTowerPostprocessor : AssetPostprocessor
{
    bool IsLevel => assetPath.Contains("VANTAGE_Tower_Level");
    bool IsGameplay => assetPath.Contains("VANTAGE_Tower_Gameplay");
    bool IsOurs => IsLevel || IsGameplay;

    static readonly string[] noCollider =
    { "CeilingLights", "DoorLamps", "AlarmBeacons", "_TXT_", "TXT_", "SIGN_", "FINAL_Helipad", "_PATROL_" };

    void OnPreprocessModel()
    {
        if (!IsOurs) return;
        var mi = (ModelImporter)assetImporter;
        mi.globalScale = 1f;
        mi.useFileScale = true;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importAnimation = false;
        mi.animationType = ModelImporterAnimationType.None;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
        mi.importNormals = ModelImporterNormals.Import;
        mi.importTangents = ModelImporterTangents.CalculateMikk;
        mi.meshCompression = ModelImporterMeshCompression.Off;
        mi.addCollider = false;
        mi.isReadable = true;   // needed for the breakable floor (ending A) and runtime collider changes
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("VANTAGE_Tower")) return;
        var ti = (TextureImporter)assetImporter;
        string f = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
        if (f.Contains("_nor") || f.Contains("normal")) ti.textureType = TextureImporterType.NormalMap;
        else if (f.Contains("rough") || f.Contains("metallic") || f.Contains("displacement")) ti.sRGBTexture = false;
        ti.maxTextureSize = 1024;
    }

    void OnPostprocessGameObjectWithUserProperties(GameObject go, string[] names, object[] values)
    {
        if (!IsOurs) return;
        var tag = go.GetComponent<VantageTag>();
        if (tag == null) tag = go.AddComponent<VantageTag>();
        for (int i = 0; i < names.Length; i++)
        {
            string v = values[i] is float f ? f.ToString(CultureInfo.InvariantCulture) : values[i]?.ToString();
            tag.Set(names[i], v);
        }
    }

    void OnPostprocessModel(GameObject root)
    {
        if (!IsOurs) return;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var go = mf.gameObject;
            var tag = go.GetComponent<VantageTag>();
            string n = go.name;
            string mt = tag ? tag.MarkerType : "";
            string doorType = tag ? tag.Get("door_type") : null;

            if (!string.IsNullOrEmpty(doorType)) { SetupDoor(go, mf, tag, doorType); continue; }

            if (mt == "Pickup") { SetupPickup(go, tag); continue; }

            if (mt == "Trigger" || n.StartsWith("TRIGGER") || n.Contains("_TRIGGER_")) { SetupTrigger(go, mf, tag); continue; }

            if (mt == "Interact")
            {
                AddMeshCollider(go, mf);
                var it = go.AddComponent<VantageInteract>();
                it.unlocksDoor = tag.Get("unlocks", "");
                it.requiresItem = tag.Get("requires", "");
                if (n.Contains("SWITCH_Shutter")) { it.opensShutter = true; it.unlocksDoor = "SHUTTER_Maintenance"; }
                continue;
            }

            if (mt == "PatrolPath")
            {
                var pp = go.AddComponent<VantagePatrolPath>();
                pp.blenderPoints = tag.Get("points", "");
                pp.pingPong = tag.Get("mode", "pingpong") == "pingpong";
                pp.calibrationMarker = n.Replace("L3_PATROL_", "L3_MK_Enemy_Patrol_");
                continue;
            }

            if (n.Contains("BreakableFloor"))
            {
                AddMeshCollider(go, mf);
                go.AddComponent<VantageBreakable>();
                continue;
            }

            if (IsGameplay) continue;   // markers: no colliders

            bool skip = false;
            foreach (var s in noCollider) if (n.Contains(s)) { skip = true; break; }
            if (!skip) AddMeshCollider(go, mf);
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI |
                StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.ReflectionProbeStatic);
        }
    }

    static void AddMeshCollider(GameObject go, MeshFilter mf)
    {
        var mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mf.sharedMesh;
    }

    static void SetupDoor(GameObject go, MeshFilter mf, VantageTag tag, string doorType)
    {
        var bc = go.AddComponent<BoxCollider>();
        bc.center = mf.sharedMesh.bounds.center;
        bc.size = mf.sharedMesh.bounds.size;
        var d = go.AddComponent<VantageDoor>();
        switch (doorType)
        {
            case "locked": d.type = DoorType.Locked; d.locked = true; break;
            case "oneway": d.type = DoorType.OneWay; break;
            case "shutter": d.type = DoorType.Shutter; break;
            default: d.type = DoorType.Auto; break;
        }
        d.unlockBy = doorType == "shutter" ? tag.Get("open_by", "") : tag.Get("unlock_by", "");
        d.oneWaySideRefName = tag.Get("open_side_ref", "");
        if (doorType == "shutter")
        {
            float.TryParse(tag.Get("open_z", "2.75"), NumberStyles.Float, CultureInfo.InvariantCulture, out float oz);
            float.TryParse(tag.Get("closed_z", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out float cz);
            float.TryParse(tag.Get("close_time_s", "0.6"), NumberStyles.Float, CultureInfo.InvariantCulture, out float ct);
            d.shutterDrop = oz - cz;
            d.shutterCloseTime = ct;
        }
    }

    static void SetupPickup(GameObject go, VantageTag tag)
    {
        var sc = go.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 1.0f;
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        go.AddComponent<VantagePickup>().item = tag.Get("item", go.name.Replace("L2_PICKUP_", ""));
    }

    static void SetupTrigger(GameObject go, MeshFilter mf, VantageTag tag)
    {
        var bc = go.AddComponent<BoxCollider>();
        bc.center = mf.sharedMesh.bounds.center;
        bc.size = mf.sharedMesh.bounds.size;
        bc.isTrigger = true;
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        var r = go.GetComponent<Renderer>();
        if (r) r.enabled = false;
        go.AddComponent<VantageTrigger>().action = tag ? tag.Get("action", "") : "";
    }

    void OnPostprocessMaterial(Material m)
    {
        if (!IsOurs) return;
        string n = m.name;
        SetF(m, "_Smoothness", n.Contains("Glass") ? 0.9f : 0.15f);
        SetF(m, "_Glossiness", n.Contains("Glass") ? 0.9f : 0.15f);
        if (n.Contains("Metal")) SetF(m, "_Metallic", 0.5f);

        if (n.StartsWith("BO_Light") || n == "BO_Screen")
        {
            Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white);
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * 2f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        if (n == "BO_Glass") MakeTransparent(m, 0.18f);
    }

    static void SetF(Material m, string p, float v) { if (m.HasProperty(p)) m.SetFloat(p, v); }

    static void MakeTransparent(Material m, float alpha)
    {
        Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white);
        c.a = alpha;
        if (m.HasProperty("_Surface"))          // URP Lit
        {
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", 0);
            m.SetColor("_BaseColor", c);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }
        else if (m.HasProperty("_Mode"))        // Built-in Standard
        {
            m.SetFloat("_Mode", 3);
            m.SetColor("_Color", c);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
