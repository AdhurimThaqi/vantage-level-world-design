using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace Vantage.EditorTools
{
    /// <summary>
    /// Sentry routes from the baked NavMesh, so they follow the geometry through re-exports. The walkable surface is
    /// sampled on a grid at the route's floor height (whole NavMesh triangles are too coarse: on the broken slab
    /// they run on into the destroyed floor inside), the largest connected patch that passes the filter is kept,
    /// and its rim facing away from the tower is followed around the tower by angle (so an L-shaped ledge around a
    /// corner comes out in order). That rim is resampled to a few points, each moved a little toward the tower so
    /// the sentry walks along the edge, not on it.
    /// </summary>
    public static class VantageSentryRoutes
    {
        private const float Step = 0.75f;
        private const float AngleBin = 6f;

        /// <param name="search">World area to sample (x/z).</param>
        /// <param name="floorY">Height of the walkable surface the route is on.</param>
        /// <param name="include">Filter for a sampled walkable point.</param>
        /// <param name="towerCentre">The rim facing away from this point is used.</param>
        /// <param name="points">Number of route points.</param>
        /// <param name="inset">Metres each point moves toward the tower.</param>
        /// <param name="minArea">Patches smaller than this (m²) count as missing.</param>
        /// <param name="area">The kept patch's area in m² (0 when none).</param>
        public static List<Vector3> Trace(Bounds search, float floorY, Func<Vector3, bool> include, Vector3 towerCentre,
                                          int points, float inset, float minArea, out float area)
        {
            area = 0f;
            var route = new List<Vector3>();
            int nx = Mathf.CeilToInt(search.size.x / Step) + 1, nz = Mathf.CeilToInt(search.size.z / Step) + 1;
            var cells = new Vector3?[nx, nz];
            for (int i = 0; i < nx; i++)
                for (int k = 0; k < nz; k++)
                {
                    var probe = new Vector3(search.min.x + i * Step, floorY, search.min.z + k * Step);
                    if (!NavMesh.SamplePosition(probe, out var hit, 0.6f, NavMesh.AllAreas)) continue;
                    var p = hit.position;
                    if (Mathf.Abs(p.y - floorY) > 0.6f || new Vector2(p.x - probe.x, p.z - probe.z).magnitude > 0.4f || !include(p)) continue;
                    cells[i, k] = p;
                }

            // Largest 8-connected patch.
            var label = new int[nx, nz];
            var best = new List<Vector3>();
            var next = 1;
            for (int i = 0; i < nx; i++)
                for (int k = 0; k < nz; k++)
                {
                    if (cells[i, k] == null || label[i, k] != 0) continue;
                    var patch = new List<Vector3>();
                    var stack = new Stack<(int, int)>();
                    stack.Push((i, k)); label[i, k] = next;
                    while (stack.Count > 0)
                    {
                        var (a, b) = stack.Pop();
                        patch.Add(cells[a, b].Value);
                        for (int da = -1; da <= 1; da++)
                            for (int db = -1; db <= 1; db++)
                            {
                                int u = a + da, v = b + db;
                                if (u < 0 || v < 0 || u >= nx || v >= nz || cells[u, v] == null || label[u, v] != 0) continue;
                                label[u, v] = next;
                                stack.Push((u, v));
                            }
                    }
                    next++;
                    if (patch.Count > best.Count) best = patch;
                }
            area = best.Count * Step * Step;
            if (area < minArea) return route;

            // The rim facing away from the tower: the farthest sample in each angle slice around it.
            float angle(Vector3 p) => Mathf.Atan2(p.z - towerCentre.z, p.x - towerCentre.x) * Mathf.Rad2Deg;
            float flat(Vector3 p) => new Vector2(p.x - towerCentre.x, p.z - towerCentre.z).magnitude;
            var rimByBin = new SortedDictionary<int, Vector3>();
            foreach (var p in best)
            {
                var bin = Mathf.FloorToInt((angle(p) + 180f) / AngleBin);
                if (!rimByBin.TryGetValue(bin, out var q) || flat(p) > flat(q)) rimByBin[bin] = p;
            }
            var rim = rimByBin.Values.ToList();
            if (rim.Count < 2) return route;
            // Start after the widest gap in angle, so a ledge that crosses ±180° is still in order.
            var bins = rimByBin.Keys.ToList();
            int gapAt = 0, widest = -1;
            for (int j = 0; j < bins.Count; j++)
            {
                var gap = (bins[(j + 1) % bins.Count] - bins[j] + 360 / (int)AngleBin) % (360 / (int)AngleBin);
                if (gap > widest) { widest = gap; gapAt = j; }
            }
            rim = rim.Skip(gapAt + 1).Concat(rim.Take(gapAt + 1)).ToList();

            // Evenly spaced points along the rim, moved toward the tower, still in the patch's region.
            var cumulative = new List<float> { 0f };
            for (int j = 1; j < rim.Count; j++) cumulative.Add(cumulative[j - 1] + Vector3.Distance(rim[j - 1], rim[j]));
            var total = cumulative[cumulative.Count - 1];
            for (int n = 0; n < points; n++)
            {
                var s = total * n / Mathf.Max(1, points - 1);
                var j = Mathf.Clamp(cumulative.FindIndex(c => c >= s), 1, rim.Count - 1);
                var seg = cumulative[j] - cumulative[j - 1];
                var p = Vector3.Lerp(rim[j - 1], rim[j], seg > 0.001f ? (s - cumulative[j - 1]) / seg : 0f);
                var toTower = towerCentre - p; toTower.y = 0;
                var moved = p + toTower.normalized * inset;
                if (NavMesh.SamplePosition(moved, out var hit, 1f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - floorY) < 0.6f && include(hit.position))
                    p = hit.position;
                route.Add(p);
            }
            return route;
        }
    }
}
