using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Vantage
{
    /// Outside patrol routes. Points come from Blender and are converted to world space.
    public class VantagePatrolPath : MonoBehaviour
    {
        public string blenderPoints;      // "[(x, y), ...]" in Blender coordinates
        public float floorHeight = 8.6f;  // Blender Z of the floor
        public string calibrationMarker;  // enemy standing on the first point
        public bool pingPong = true;

        List<Vector3> cached;

        public List<Vector3> GetPoints()
        {
            if (cached != null) return cached;
            var raw = new List<Vector2>();
            var nums = new List<float>();
            foreach (var tok in blenderPoints.Replace("[", "").Replace("]", "").Replace("(", "").Replace(")", "").Split(','))
                if (float.TryParse(tok.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f)) nums.Add(f);
            for (int i = 0; i + 1 < nums.Count; i += 2) raw.Add(new Vector2(nums[i], nums[i + 1]));

            // Blender (x, y, z) -> Unity: pick the axis mapping that puts point 0 on the calibration marker
            System.Func<Vector2, Vector3>[] maps =
            {
                p => new Vector3(-p.x, floorHeight, -p.y),
                p => new Vector3(p.x, floorHeight, p.y),
                p => new Vector3(p.x, floorHeight, -p.y),
                p => new Vector3(-p.x, floorHeight, p.y),
            };
            int bestMap = 0;
            var cal = string.IsNullOrEmpty(calibrationMarker) ? null : GameObject.Find(calibrationMarker);
            if (cal && raw.Count > 0)
            {
                float best = float.MaxValue;
                for (int m = 0; m < maps.Length; m++)
                {
                    float d = Vector3.Distance(transform.root.TransformPoint(maps[m](raw[0])), cal.transform.position);
                    if (d < best) { best = d; bestMap = m; }
                }
            }
            cached = new List<Vector3>();
            foreach (var p in raw) cached.Add(transform.root.TransformPoint(maps[bestMap](p)));
            return cached;
        }

        void OnDrawGizmos()
        {
            if (string.IsNullOrEmpty(blenderPoints)) return;
            cached = null;
            var pts = GetPoints();
            Gizmos.color = Color.cyan;
            for (int i = 0; i + 1 < pts.Count; i++) Gizmos.DrawLine(pts[i], pts[i + 1]);
        }
    }
}
