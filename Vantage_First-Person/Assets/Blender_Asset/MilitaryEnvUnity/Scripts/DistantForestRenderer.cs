using System.Collections.Generic;
using UnityEngine;

/// Draws the far-away horizon forest with GPU instancing (no GameObjects).
[ExecuteAlways]
public class DistantForestRenderer : MonoBehaviour
{
    public Mesh mesh;
    public Material material;
    [HideInInspector] public float[] data;          // Unity-space: x,y,z,yawDeg,scale per tree
    public float maxDrawDistance = 4000f;
    public UnityEngine.Rendering.ShadowCastingMode shadows = UnityEngine.Rendering.ShadowCastingMode.Off;

    List<Matrix4x4[]> batches;

    void OnEnable() { Build(); }
    void OnValidate() { batches = null; }

    public void Build()
    {
        batches = new List<Matrix4x4[]>();
        if (data == null || data.Length < 5) return;
        int n = data.Length / 5;
        var cur = new List<Matrix4x4>(1023);
        for (int i = 0; i < n; i++)
        {
            int o = i * 5;
            var m = Matrix4x4.TRS(new Vector3(data[o], data[o + 1], data[o + 2]),
                                  Quaternion.Euler(0f, data[o + 3], 0f),
                                  Vector3.one * data[o + 4]);
            cur.Add(transform.localToWorldMatrix * m);
            if (cur.Count == 1023) { batches.Add(cur.ToArray()); cur.Clear(); }
        }
        if (cur.Count > 0) batches.Add(cur.ToArray());
    }

    void Update()
    {
        if (mesh == null || material == null) return;
        if (batches == null) Build();
        foreach (var b in batches)
            Graphics.DrawMeshInstanced(mesh, 0, material, b, b.Length, null, shadows, false, gameObject.layer);
    }
}
