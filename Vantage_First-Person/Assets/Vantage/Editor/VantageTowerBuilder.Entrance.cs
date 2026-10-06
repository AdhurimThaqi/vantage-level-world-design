using UnityEditor;
using UnityEngine;

namespace Vantage.EditorTools
{
    /// <summary>
    /// Walkable stairs and the tower entrance.
    ///
    /// The template's character has no step-up: on flat ground its velocity is kept horizontal, so any ledge
    /// higher than a few centimetres stops it like a wall (the template's own levels have ramps, never stairs).
    /// Stairs therefore keep their steps visually but are walked on as an invisible ramp, and the lobby floor
    /// (20 cm above the yard) is reached by a ramp through a proper entrance.
    /// </summary>
    public static partial class VantageTowerBuilder
    {
        public const string WalkRampName = "Walk Ramp (invisible)";
        private const string EntranceName = "Entrance";
        private const string LobbyStairName = "Stairwell L1-L2", FireEscapeStairName = "Fire Escape Stairs";
        private const int LobbyStairSteps = 35, FireEscapeSteps = 20;

        /// <summary>
        /// Replaces a ProBuilder stair's step collider with a smooth ramp through the front edge of every step.
        /// ProBuilder stairs rise along +Z: step i covers z [i, i+1] * run at height (i+1) * rise.
        /// </summary>
        public static void AddWalkRamp(GameObject stair, int steps)
        {
            var meshCollider = stair.GetComponent<MeshCollider>();
            if (meshCollider != null)
                Object.DestroyImmediate(meshCollider);

            var old = stair.transform.Find(WalkRampName);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            var bounds = stair.GetComponent<MeshFilter>().sharedMesh.bounds;
            var run = bounds.size.z / steps;

            // Starts one step early, on the floor, so there is no lip at the bottom; ends on the top step.
            var low = new Vector3(bounds.center.x, bounds.min.y, bounds.min.z - run);
            var high = new Vector3(bounds.center.x, bounds.max.y, bounds.max.z - run);
            var along = high - low;
            var rotation = Quaternion.LookRotation(along.normalized, Vector3.up);
            const float thickness = 0.3f, extraBelow = 0.15f;

            var ramp = new GameObject(WalkRampName) { layer = stair.layer };
            ramp.transform.SetParent(stair.transform, false);
            ramp.transform.localRotation = rotation;
            // Extended only at the bottom (into the floor), so the top meets the landing without a bump.
            ramp.transform.localPosition = (low + high) / 2 - along.normalized * (extraBelow / 2) - rotation * Vector3.up * (thickness / 2);
            var box = ramp.AddComponent<BoxCollider>();
            box.size = new Vector3(bounds.size.x, thickness, along.magnitude + extraBelow);
            GameObjectUtility.SetStaticEditorFlags(ramp, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>
        /// The way in: a ramp up to the lobby floor, a steel door frame with its blast doors thrown open,
        /// a canopy with the brightest lamp on the facade (light marks the way, as everywhere else in the tower)
        /// and a sandbag chicane the defenders left behind. Nothing here blocks the 3.2 m doorway.
        /// </summary>
        private static void buildEntrance(Transform parent)
        {
            const float wallFront = -Half, doorHalf = 1.6f, rampHalf = 1.8f, rampLength = 3.6f, frame = 0.25f;
            const float front = wallFront - rampLength;

            Physics.SyncTransforms();
            var ground = localGround(parent, new Vector3(0, 0, front));

            // Ramp from the yard to the lobby floor (the yard is not perfectly level, so it adapts).
            if (F1 - ground > 0.02f)
            {
                var rise = F1 - ground;
                var slope = Mathf.Atan2(rise, rampLength);
                var rotation = Quaternion.Euler(-slope * Mathf.Rad2Deg, 0, 0);
                var length = Mathf.Sqrt(rampLength * rampLength + rise * rise) + 0.3f;
                var top = new Vector3(0, (ground + F1) / 2, (front + wallFront) / 2);
                boxRotated(parent, "Entrance Ramp", top - rotation * Vector3.up * 0.2f, new Vector3(rampHalf * 2, 0.4f, length), rotation, _concreteDark);
            }
            else
                Debug.Log($"[Vantage] Entrance: the yard is level with the lobby floor ({ground:F2} m), no ramp needed.");

            // Steel frame around the opening; the lintel sits above the 4.2 m opening, so the doorway stays clear.
            box(parent, "Door Frame Left", new Vector3(-doorHalf - frame, F1, wallFront - 0.15f), new Vector3(-doorHalf, 4.5f, wallFront + 0.55f), _metal);
            box(parent, "Door Frame Right", new Vector3(doorHalf, F1, wallFront - 0.15f), new Vector3(doorHalf + frame, 4.5f, wallFront + 0.55f), _metal);
            box(parent, "Door Lintel", new Vector3(-doorHalf - frame, 4.2f, wallFront - 0.15f), new Vector3(doorHalf + frame, 4.5f, wallFront + 0.55f), _metal);

            // Blast doors thrown open outward; the left one hangs off a broken hinge. Something forced its way in.
            leaf(parent, "Blast Door Right (open)", new Vector3(doorHalf + frame, ground, wallFront - 0.15f), 1f, 0f);
            leaf(parent, "Blast Door Left (off its hinge)", new Vector3(-doorHalf - frame, ground, wallFront - 0.15f), -1f, 7f);

            // Canopy and its hanger rods.
            box(parent, "Canopy", new Vector3(-2.8f, 4.7f, wallFront - 2.2f), new Vector3(2.8f, 4.9f, wallFront), _rust);
            rod(parent, "Canopy Rod", new Vector3(-2.6f, 4.9f, wallFront - 2.1f), new Vector3(-2.6f, 6.6f, wallFront), 0.07f, _metal);
            rod(parent, "Canopy Rod", new Vector3(2.6f, 4.9f, wallFront - 2.1f), new Vector3(2.6f, 6.6f, wallFront), 0.07f, _metal);
            box(parent, "Lamp Housing", new Vector3(-0.3f, 4.55f, wallFront - 1.2f), new Vector3(0.3f, 4.7f, wallFront - 0.8f), _metal);
            light(parent, "Entrance Lamp (way in)", new Vector3(0, 4.3f, wallFront - 1f), new Color(1f, 0.86f, 0.62f), 4.5f, 10f);

            // Sandbag chicane in front of the door: cover for the tutorial fight with the door drones.
            sandbagsIfClear(parent, new Vector3(-4.6f, 0, wallFront - 2.2f), new Vector3(-2.9f, 0, wallFront - 1.6f));
            sandbagsIfClear(parent, new Vector3(2.9f, 0, wallFront - 2.2f), new Vector3(4.6f, 0, wallFront - 1.6f));
        }

        /// <summary>
        /// One open door leaf, hinged at 'hinge', swung outward and a little to its side.
        /// </summary>
        private static void leaf(Transform parent, string name, Vector3 hinge, float side, float roll)
        {
            const float width = 1.6f, height = 4f, thickness = 0.12f;
            var direction = new Vector3(side * Mathf.Sin(25f * Mathf.Deg2Rad), 0, -Mathf.Cos(25f * Mathf.Deg2Rad));
            var rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 0, roll * side);
            var center = hinge + direction * (width / 2 + 0.05f) + Vector3.up * (height / 2 + 0.02f);
            boxRotated(parent, name, center, new Vector3(thickness, height, width), rotation, _rust);
        }

        private static void rod(Transform parent, string name, Vector3 a, Vector3 b, float thickness, Material material)
        {
            boxRotated(parent, name, (a + b) / 2, new Vector3(thickness, thickness, Vector3.Distance(a, b)), Quaternion.LookRotation(b - a), material);
        }

        private static void sandbagsIfClear(Transform parent, Vector3 min, Vector3 max)
        {
            var center = (min + max) / 2;
            var y = localGround(parent, center);
            min.y = y;
            max.y = y + 1.1f;

            var mask = ~((1 << 2) | (1 << 8) | (1 << 10) | (1 << 11));
            var worldCenter = parent.TransformPoint((min + max) / 2 + Vector3.up * 0.05f);
            var halfExtents = Vector3.Scale((max - min) / 2, parent.lossyScale) - Vector3.one * 0.05f;
            if (Physics.CheckBox(worldCenter, halfExtents, parent.rotation, mask, QueryTriggerInteraction.Ignore))
            {
                Debug.Log($"[Vantage] Entrance: sandbags at {center.x:F1}, {center.z:F1} skipped, something already stands there.");
                return;
            }

            box(parent, "Sandbag Wall", min, max, _sandbag);
        }

        private static float localGround(Transform parent, Vector3 local)
        {
            return parent.InverseTransformPoint(groundAt(parent.TransformPoint(local + Vector3.up * 20f), parent.TransformPoint(local))).y;
        }
    }
}
