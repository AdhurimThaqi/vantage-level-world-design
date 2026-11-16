using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VANTAGE change (2026-11-16): removes doubled faces from VANTAGE_Tower_Level on import. The Blender model has many
/// surfaces twice in the same place (the ground floor once in TX_Floor and once in T_MacroVariation, the level 2 floor
/// in both LVL2_Structure and LVL2_FloorSlab, LVL3_Structure over LVL2_Ceiling, sills over inner walls); the two
/// z-fight, so their textures flicker as the camera moves. A triangle is removed only when it is fully covered by
/// coplanar (within Gap), same-facing triangles of higher priority; partly covered ones stay, so no hole can open.
/// Priority, lowest first: T_MacroVariation, TX_WallInner, any other material; on a tie a "*_Structure" object gives
/// way to a separate named one (LVL2_FloorSlab, LVL2_Ceiling, the broken slabs: the game finds those by name); then
/// the later object or triangle gives way. A partly covered triangle can't go (it would leave a hole): it gets its
/// own vertices, moved Recede back along its normal, so the depth test always lets the other surface win.
/// </summary>
public static class VantageTowerDuplicateFaces
{
    const float Gap = 0.004f, MinArea = 0.0005f, Inside = 0.002f, Recede = 0.005f;

    struct Tri
    {
        public MeshFilter Owner; public int Sub, Index;      // Index: first of its three indices in the submesh
        public Vector3 A, B, C, N; public float D, Area;
        public long Priority;
    }

    public static void Remove(GameObject root)
    {
        var tris = new List<Tri>();
        var filters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int f = 0; f < filters.Length; f++)
        {
            var mf = filters[f];
            var mesh = mf.sharedMesh;
            var renderer = mf.GetComponent<MeshRenderer>();
            if (mesh == null || renderer == null) continue;
            var v = mesh.vertices;
            var m = root.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            var structure = mf.name.Contains("_Structure");
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var mats = renderer.sharedMaterials;
                var mat = s < mats.Length && mats[s] != null ? mats[s].name : "";
                var rank = mat.StartsWith("T_MacroVariation") ? 0 : mat.StartsWith("TX_WallInner") ? 1 : 2;
                var idx = mesh.GetTriangles(s);
                for (int i = 0; i + 2 < idx.Length; i += 3)
                {
                    var a = m.MultiplyPoint3x4(v[idx[i]]); var b = m.MultiplyPoint3x4(v[idx[i + 1]]); var c = m.MultiplyPoint3x4(v[idx[i + 2]]);
                    var cross = Vector3.Cross(b - a, c - a);
                    var area = cross.magnitude * 0.5f;
                    if (area < MinArea) continue;
                    var n = cross / (area * 2f);
                    // Higher wins: material rank, then a separate object over a Structure, then the earlier object/triangle.
                    var priority = ((long)rank << 50) | ((long)(structure ? 0 : 1) << 49) | ((long)(4095 - Mathf.Min(f, 4095)) << 30) | (long)(0x3FFFFFFF - tris.Count);
                    tris.Add(new Tri { Owner = mf, Sub = s, Index = i, A = a, B = b, C = c, N = n, D = Vector3.Dot(n, a), Area = area, Priority = priority });
                }
            }
        }

        // Buckets of near-coplanar, same-facing triangles.
        var buckets = new Dictionary<(int, int, int, int), List<int>>();
        for (int i = 0; i < tris.Count; i++)
        {
            var key = keyOf(tris[i]);
            if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = new List<int>();
            list.Add(i);
        }

        var removed = new HashSet<(MeshFilter, int, int)>();
        var receded = new HashSet<(MeshFilter, int, int)>();
        var candidates = new List<int>();
        for (int i = 0; i < tris.Count; i++)
        {
            var t = tris[i];
            var (x, y, z, w) = keyOf(t);
            candidates.Clear();
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dw = -1; dw <= 1; dw++)
                            if (buckets.TryGetValue((x + dx, y + dy, z + dz, w + dw), out var list))
                                foreach (var j in list)
                                {
                                    var o = tris[j];
                                    if (o.Priority > t.Priority && Vector3.Dot(o.N, t.N) > 0.999f && Mathf.Abs(o.D - t.D) <= Gap) candidates.Add(j);
                                }
            if (candidates.Count == 0) continue;
            var inside = covered(t, candidates, tris);
            if (inside == Samples) removed.Add((t.Owner, t.Sub, t.Index));
            else if (inside >= 2 || candidates.Exists(j => inT(t, tris[j]) >= 2)) receded.Add((t.Owner, t.Sub, t.Index));
        }
        if (removed.Count == 0 && receded.Count == 0) return;

        // Rebuild the touched meshes: covered triangles dropped, partly covered ones on their own receded vertices.
        int count = 0, moved = 0;
        foreach (var mf in filters)
        {
            var mesh = mf.sharedMesh;
            if (mesh == null) continue;
            var touched = false;
            var verts = new List<Vector3>(); mesh.GetVertices(verts);
            var normals = new List<Vector3>(); mesh.GetNormals(normals);
            var tangents = new List<Vector4>(); mesh.GetTangents(tangents);
            var colors = new List<Color>(); mesh.GetColors(colors);
            var uvs = new List<Vector4>[4];
            for (int c = 0; c < 4; c++) { uvs[c] = new List<Vector4>(); mesh.GetUVs(c, uvs[c]); }
            var subs = new List<int[]>();
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var idx = mesh.GetTriangles(s);
                var keep = new List<int>(idx.Length);
                for (int i = 0; i + 2 < idx.Length; i += 3)
                {
                    if (removed.Contains((mf, s, i))) { touched = true; count++; continue; }
                    if (receded.Contains((mf, s, i)))
                    {
                        touched = true; moved++;
                        // Recede metres in the tower's space (the mesh's own units may be scaled).
                        var a = verts[idx[i]]; var n = Vector3.Cross(verts[idx[i + 1]] - a, verts[idx[i + 2]] - a);
                        var toRoot = root.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                        var back = toRoot.inverse.MultiplyVector(toRoot.MultiplyVector(n).normalized * Recede);
                        for (int k = 0; k < 3; k++)
                        {
                            var src = idx[i + k];
                            keep.Add(verts.Count);
                            verts.Add(verts[src] - back);
                            if (normals.Count > 0) normals.Add(normals[src]);
                            if (tangents.Count > 0) tangents.Add(tangents[src]);
                            if (colors.Count > 0) colors.Add(colors[src]);
                            for (int c = 0; c < 4; c++) if (uvs[c].Count > 0) uvs[c].Add(uvs[c][src]);
                        }
                        continue;
                    }
                    keep.Add(idx[i]); keep.Add(idx[i + 1]); keep.Add(idx[i + 2]);
                }
                subs.Add(keep.ToArray());
            }
            if (!touched) continue;
            if (verts.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            if (normals.Count > 0) mesh.SetNormals(normals);
            if (tangents.Count > 0) mesh.SetTangents(tangents);
            if (colors.Count > 0) mesh.SetColors(colors);
            for (int c = 0; c < 4; c++) if (uvs[c].Count > 0) mesh.SetUVs(c, uvs[c]);
            for (int s = 0; s < subs.Count; s++) mesh.SetTriangles(subs[s], s, false);
            mesh.RecalculateBounds();
        }
        Debug.Log($"[Vantage] Tower import: {count} doubled faces removed, {moved} partly doubled moved {Recede * 1000:F0} mm back (z-fighting).");
    }

    static (int, int, int, int) keyOf(Tri t) =>
        (Mathf.RoundToInt(t.N.x * 50), Mathf.RoundToInt(t.N.y * 50), Mathf.RoundToInt(t.N.z * 50), Mathf.RoundToInt(t.D / Gap));

    const int Samples = 10;

    /// <summary>How many of t's Samples points (corners pulled in a little, edge midpoints, inner points) lie in a candidate.</summary>
    static int covered(Tri t, List<int> candidates, List<Tri> tris)
    {
        var count = 0;
        foreach (var p in pointsOf(t))
            foreach (var j in candidates)
                if (contains(tris[j], p)) { count++; break; }
        return count;
    }

    /// <summary>How many of the rival's sample points lie in t (a big rival only partly over t).</summary>
    static int inT(Tri t, Tri rival)
    {
        var count = 0;
        foreach (var p in pointsOf(rival)) if (contains(t, p)) count++;
        return count;
    }

    static Vector3[] pointsOf(Tri t)
    {
        var centre = (t.A + t.B + t.C) / 3f;
        return new[]
        {
            Vector3.Lerp(t.A, centre, 0.05f), Vector3.Lerp(t.B, centre, 0.05f), Vector3.Lerp(t.C, centre, 0.05f),
            Vector3.Lerp((t.A + t.B) / 2, centre, 0.05f), Vector3.Lerp((t.B + t.C) / 2, centre, 0.05f), Vector3.Lerp((t.C + t.A) / 2, centre, 0.05f),
            centre, Vector3.Lerp(t.A, centre, 0.5f), Vector3.Lerp(t.B, centre, 0.5f), Vector3.Lerp(t.C, centre, 0.5f),
        };
    }

    static bool contains(Tri o, Vector3 p)
    {
        // Barycentric test in o's plane, with a small tolerance.
        Vector3 v0 = o.B - o.A, v1 = o.C - o.A, v2 = p - o.A;
        float d00 = Vector3.Dot(v0, v0), d01 = Vector3.Dot(v0, v1), d11 = Vector3.Dot(v1, v1), d20 = Vector3.Dot(v2, v0), d21 = Vector3.Dot(v2, v1);
        var den = d00 * d11 - d01 * d01;
        if (Mathf.Abs(den) < 1e-12f) return false;
        var v = (d11 * d20 - d01 * d21) / den;
        var w = (d00 * d21 - d01 * d20) / den;
        var tol = Inside / Mathf.Sqrt(Mathf.Max(d00, d11));
        return v >= -tol && w >= -tol && v + w <= 1f + tol;
    }
}
