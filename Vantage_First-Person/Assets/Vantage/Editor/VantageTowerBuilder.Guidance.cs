using UnityEngine;

namespace Vantage.EditorTools
{
    /// <summary>
    /// Makes the way up unmistakable (playtest finding: players could not tell how to reach level 3).
    /// One colour means "this way": a glowing yellow line on the floor from where you arrive to the next way up,
    /// chevrons at every turn, yellow step edges on every stair and yellow frames round the fire escape doors.
    /// Nothing here has a collider, so none of it can trip the character.
    /// </summary>
    public static partial class VantageTowerBuilder
    {
        private const float StripeWidth = 0.22f;

        private static void buildGuidance(Transform parent)
        {
            // Level 1: entrance -> along the south wall -> foot of the stairwell.
            path(group(parent, "Level 1 - to the stairwell"), F1,
                new Vector2(0f, -8.5f), new Vector2(0f, -7.8f), new Vector2(7.3f, -7.8f), new Vector2(7.3f, -6.45f));
            chevron(parent, new Vector3(3.6f, F1, -7.8f), 90f);
            chevron(parent, new Vector3(7.3f, F1, -7.1f), 0f);

            // Level 2: stair landing -> north hall -> west hall -> fire escape door (the hall is never generated over).
            path(group(parent, "Level 2 - to the fire escape"), F2,
                new Vector2(7.2f, 3.95f), new Vector2(7.2f, 7.1f), new Vector2(-7f, 7.1f), new Vector2(-7f, -6.7f), new Vector2(-8.55f, -6.7f));
            chevron(parent, new Vector3(7.2f, F2, 5.4f), 0f);
            chevron(parent, new Vector3(2.5f, F2, 7.1f), -90f);
            chevron(parent, new Vector3(-3.5f, F2, 7.1f), -90f);
            chevron(parent, new Vector3(-7f, F2, 4.2f), 180f);
            chevron(parent, new Vector3(-7f, F2, -1.2f), 180f);
            chevron(parent, new Vector3(-7f, F2, -5.2f), 180f);
            doorFrame(parent, -Inner, 1f, -8f, -5.4f, F2, 9.8f);

            // Fire escape: out of the door, turn right, up the stairs, in through the framed door.
            path(group(parent, "Fire Escape - balcony"), F2,
                new Vector2(-9.05f, -6.7f), new Vector2(-10.3f, -6.7f), new Vector2(-10.3f, -4.95f));
            chevron(parent, new Vector3(-10.3f, F2, -5.7f), 0f);
            path(group(parent, "Fire Escape - landing"), F3,
                new Vector2(-10.3f, 1.05f), new Vector2(-10.3f, 2.5f), new Vector2(-9.05f, 2.5f));
            doorFrame(parent, -Half, -1f, 1.2f, 3.8f, F3, 13.9f);
            doorFrame(parent, -Inner, 1f, 1.2f, 3.8f, F3, 13.9f);
            light(parent, "Fire Escape Landing (way in)", new Vector3(-10.3f, F3 + 2.6f, 2.5f), new Color(1f, 0.9f, 0.7f), 4f, 8f);
            light(parent, "Fire Escape Balcony", new Vector3(-10.3f, F2 + 2.6f, -6.2f), new Color(1f, 0.9f, 0.7f), 3f, 7f);

            // Level 3: in from the fire escape -> round the debris -> past the railed shaft -> foot of the slab ramp.
            path(group(parent, "Level 3 - to the ramp"), F3,
                new Vector2(-8.45f, 2.5f), new Vector2(-3.5f, 2.5f), new Vector2(-3.5f, -3.7f), new Vector2(3.5f, -3.7f), new Vector2(3.5f, -3.25f));
            chevron(parent, new Vector3(-6f, F3, 2.5f), 90f);
            chevron(parent, new Vector3(-3.5f, F3, -0.6f), 180f);
            chevron(parent, new Vector3(0f, F3, -3.7f), 90f);

            // Up the middle of the collapsed slab ramp to the roof (same geometry as in buildCollapsedLevel).
            const float run = 8f, rise = FR - F3;
            var rotation = Quaternion.Euler(-Mathf.Atan2(rise, run) * Mathf.Rad2Deg, 0, 0);
            var length = Mathf.Sqrt(run * run + rise * rise);
            var surface = new Vector3(3.5f, (F3 + FR) / 2, 1f) - rotation * Vector3.forward * 0.15f + rotation * Vector3.up * 0.012f;
            glow(parent, "Guide Line - ramp", surface, new Vector3(StripeWidth, 0.01f, length - 0.6f), rotation);
        }

        /// <summary>
        /// A glowing line on the floor through the given points (x, z) at height y.
        /// </summary>
        private static void path(Transform parent, float y, params Vector2[] points)
        {
            for (int i = 0; i < points.Length - 1; i++)
            {
                var a = new Vector3(points[i].x, y + 0.006f, points[i].y);
                var b = new Vector3(points[i + 1].x, y + 0.006f, points[i + 1].y);
                var length = Vector3.Distance(a, b) + StripeWidth; // overlaps at the corners
                glow(parent, "Guide Line", (a + b) / 2, new Vector3(StripeWidth, 0.01f, length), Quaternion.LookRotation(b - a));
            }
        }

        /// <summary>
        /// A double chevron on the floor pointing along 'yaw' (0 = +Z, 90 = +X).
        /// </summary>
        private static void chevron(Transform parent, Vector3 position, float yaw)
        {
            var c = group(parent, "Chevron");
            c.localPosition = position + Vector3.up * 0.008f;
            c.localRotation = Quaternion.Euler(0, yaw, 0);
            foreach (var offset in new[] { -0.25f, 0.25f })
            {
                glow(c, "Bar", new Vector3(-0.2f, 0, offset - 0.2f), new Vector3(0.14f, 0.01f, 0.62f), Quaternion.Euler(0, 45f, 0));
                glow(c, "Bar", new Vector3(0.2f, 0, offset - 0.2f), new Vector3(0.14f, 0.01f, 0.62f), Quaternion.Euler(0, -45f, 0));
            }
        }

        /// <summary>
        /// A glowing frame round a doorway in a wall running along Z at 'faceX'; 'side' is the direction it faces (+1 = +X).
        /// </summary>
        private static void doorFrame(Transform parent, float faceX, float side, float z0, float z1, float y0, float y1)
        {
            var f = group(parent, "Door Frame (way up)");
            const float w = 0.12f, t = 0.04f;
            var x = faceX + side * t / 2;
            glow(f, "Jamb", new Vector3(x, (y0 + y1 + w) / 2, z0 - w / 2), new Vector3(t, y1 - y0 + w, w), Quaternion.identity);
            glow(f, "Jamb", new Vector3(x, (y0 + y1 + w) / 2, z1 + w / 2), new Vector3(t, y1 - y0 + w, w), Quaternion.identity);
            glow(f, "Head", new Vector3(x, y1 + w / 2, (z0 + z1) / 2), new Vector3(t, w, z1 - z0 + 2 * w), Quaternion.identity);
        }

        /// <summary>
        /// Yellow nosing on every step, so stairs read as the way up from across the room.
        /// </summary>
        private static void stepEdges(Transform parent, string stairName, Vector3 corner, Vector3 size, int steps)
        {
            var edges = group(parent, stairName + " - Step Edges");
            var rise = size.y / steps;
            var run = size.z / steps;
            for (int i = 0; i < steps; i++)
                glow(edges, "Edge", corner + new Vector3(size.x / 2, (i + 1) * rise + 0.005f, i * run + 0.05f), new Vector3(size.x - 0.1f, 0.01f, 0.1f), Quaternion.identity);
        }

        /// <summary>
        /// A 1 m railing (posts, mid rail, top rail) from a to b on the floor. Unlike the guidance it is solid.
        /// </summary>
        private static void railing(Transform parent, Vector3 a, Vector3 b)
        {
            var r = group(parent, "Shaft Railing");
            var direction = b - a;
            var rotation = Quaternion.LookRotation(direction.normalized);
            foreach (var height in new[] { 0.5f, 1f })
                boxRotated(r, "Rail", (a + b) / 2 + Vector3.up * height, new Vector3(0.06f, 0.06f, direction.magnitude + 0.06f), rotation, _metal);
            var posts = Mathf.Max(2, Mathf.CeilToInt(direction.magnitude / 1.5f) + 1);
            for (int i = 0; i < posts; i++)
                box(r, "Post", a + direction * (i / (float)(posts - 1)) + new Vector3(-0.04f, 0, -0.04f), a + direction * (i / (float)(posts - 1)) + new Vector3(0.04f, 1.03f, 0.04f), _metal);
        }

        /// <summary>
        /// An emissive yellow cube with no collider and no shadows.
        /// </summary>
        private static GameObject glow(Transform parent, string name, Vector3 localCenter, Vector3 size, Quaternion localRotation)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.transform.localRotation = localRotation;
            go.transform.localScale = size;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = _guide;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
