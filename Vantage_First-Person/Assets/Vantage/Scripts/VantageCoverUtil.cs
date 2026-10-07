using CoverShooter;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// Adds Cover Shooter cover markers around an obstacle so the character (and the AI) can take cover at it.
    /// Matches the template's own covers: a thin trigger box on the Cover layer (8), flat against each side face,
    /// with its forward axis pointing into the obstacle.
    /// </summary>
    public static class VantageCoverUtil
    {
        public const int CoverLayer = 8;
        private const float MinWidth = 0.9f;
        private const float MinHeight = 0.7f;
        private const float Thickness = 0.1f;

        /// <summary>
        /// One cover per side face of the obstacle's axis-aligned bounds that is wide and tall enough.
        /// Covers are parented to 'container' (must not be scaled). Returns how many were made.
        /// </summary>
        public static int AddFor(GameObject obstacle, Transform container)
        {
            if (!tryBounds(obstacle, out var bounds))
                return 0;

            var made = 0;
            made += face(obstacle.name, bounds, Vector3.right, bounds.size.z, container);
            made += face(obstacle.name, bounds, Vector3.left, bounds.size.z, container);
            made += face(obstacle.name, bounds, Vector3.forward, bounds.size.x, container);
            made += face(obstacle.name, bounds, Vector3.back, bounds.size.x, container);
            return made;
        }

        /// <summary>
        /// Like AddFor, but for props turned to any angle: the box is measured in the obstacle's own yaw,
        /// so the covers lie flat against its faces instead of around its world-aligned bounds.
        /// </summary>
        public static int AddForOriented(GameObject obstacle, Transform container)
        {
            var frame = Quaternion.Euler(0, obstacle.transform.eulerAngles.y, 0);
            var toFrame = Quaternion.Inverse(frame);
            var found = false;
            var local = default(Bounds);
            foreach (var collider in obstacle.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger)
                    continue;
                var b = collider is MeshCollider mc && mc.sharedMesh != null ? mc.sharedMesh.bounds : new Bounds(collider.bounds.center, collider.bounds.size);
                var t = collider is MeshCollider ? collider.transform : null;
                for (var i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = toFrame * (t != null ? t.TransformPoint(corner) : corner);
                    if (!found) { local = new Bounds(p, Vector3.zero); found = true; }
                    else local.Encapsulate(p);
                }
            }
            if (!found)
                return 0;

            var made = 0;
            made += face(obstacle.name, local, Vector3.right, local.size.z, container, frame);
            made += face(obstacle.name, local, Vector3.left, local.size.z, container, frame);
            made += face(obstacle.name, local, Vector3.forward, local.size.x, container, frame);
            made += face(obstacle.name, local, Vector3.back, local.size.x, container, frame);
            return made;
        }

        private static int face(string name, Bounds bounds, Vector3 normal, float width, Transform container) =>
            face(name, bounds, normal, width, container, Quaternion.identity);

        /// <summary>'bounds' and 'normal' are in a frame turned by 'frame' around the world origin.</summary>
        private static int face(string name, Bounds bounds, Vector3 normal, float width, Transform container, Quaternion frame)
        {
            var height = bounds.size.y;
            if (width < MinWidth || height < MinHeight)
                return 0;

            var center = frame * (bounds.center + Vector3.Scale(normal, bounds.extents) + normal * (Thickness / 2));
            var cover = new GameObject("Cover - " + name) { layer = CoverLayer };
            cover.transform.SetParent(container, true);
            cover.transform.SetPositionAndRotation(center, Quaternion.LookRotation(frame * -normal));
            cover.transform.localScale = new Vector3(width, height, Thickness);

            var box = cover.AddComponent<BoxCollider>();
            box.isTrigger = true;
            cover.AddComponent<Cover>();
            return 1;
        }

        private static bool tryBounds(GameObject obstacle, out Bounds bounds)
        {
            bounds = default;
            var found = false;
            foreach (var collider in obstacle.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger)
                    continue;
                if (!found)
                {
                    bounds = collider.bounds;
                    found = true;
                }
                else
                    bounds.Encapsulate(collider.bounds);
            }
            return found;
        }
    }
}
