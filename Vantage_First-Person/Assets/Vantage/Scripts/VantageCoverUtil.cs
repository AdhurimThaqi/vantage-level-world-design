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

        private static int face(string name, Bounds bounds, Vector3 normal, float width, Transform container)
        {
            var height = bounds.size.y;
            if (width < MinWidth || height < MinHeight)
                return 0;

            var center = bounds.center + Vector3.Scale(normal, bounds.extents) + normal * (Thickness / 2);
            var cover = new GameObject("Cover - " + name) { layer = CoverLayer };
            cover.transform.SetParent(container, true);
            cover.transform.SetPositionAndRotation(center, Quaternion.LookRotation(-normal));
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
