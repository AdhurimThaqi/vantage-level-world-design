using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace Vantage.EditorTools
{
    /// <summary>
    /// The overrun military position around the tower: perimeter wall with a checkpoint, sandbag nests, containers,
    /// tents, floodlight masts, a flag, supplies, generators and environment sound.
    /// Every piece is snapped to the ground and skipped if it would hang over the edge or collide with the existing map.
    /// The yard stays non-combat (concept doc); it frames the tower and is what the roof looks down on.
    /// </summary>
    public static partial class VantageTowerBuilder
    {
        private const float CompoundMinX = -28f, CompoundMaxX = 26f, CompoundMinZ = -36f, CompoundMaxZ = 22f;
        private const int PlacementMask = ~((1 << 2) | (1 << 8) | (1 << 10) | (1 << 11));

        private static Material _military, _canvas, _hazard;
        private static int _placed, _skipped;

        private static void buildCompound(Transform parent)
        {
            _military = material("Military Green", new Color(0.29f, 0.33f, 0.2f), 0.1f, 0.25f);
            _canvas = material("Tent Canvas", new Color(0.36f, 0.37f, 0.25f), 0f, 0.05f);
            _hazard = material("Hazard Yellow", new Color(0.9f, 0.68f, 0.08f), 0f, 0.3f);
            _placed = _skipped = _diagnostics = 0;
            Physics.SyncTransforms();

            perimeter(group(parent, "Perimeter Wall"));
            checkpoint(group(parent, "Checkpoint"));

            var nests = group(parent, "Sandbag Nests");
            sandbagNest(nests, new Vector3(12f, 0, -29f), 0f);
            sandbagNest(nests, new Vector3(-13f, 0, -30f), 0f);
            sandbagNest(nests, new Vector3(20f, 0, 2f), -90f);

            var depot = group(parent, "Supply Depot");
            compoundProp(depot, "Containers/Cargo_container_v1/Cargo_container_v1_LD1close.prefab", new Vector3(17f, 0, -20f), 90f, true);
            compoundProp(depot, "Containers/Cargo_container_v1/Cargo_container_v1_LD1open.prefab", new Vector3(17f, 0, -12.5f), 90f, true);
            compoundProp(depot, "Boxes/Wooden_box_v1/Wooden_box_v1_LD2square.prefab", new Vector3(13.5f, 0, -6f), 10f, true);
            compoundProp(depot, "Boxes/Wooden_box_v1/Wooden_box_v1_LD1square.prefab", new Vector3(14.8f, 0, -4.4f), -20f, true);
            compoundProp(depot, "Other_props/Palets/Bags_on_pallet_v1/Bags_on_pallet_v1_2.prefab", new Vector3(12.5f, 0, 9f), 0f, true);
            compoundProp(depot, "Other_props/Palets/Palet_v1/Palet_v1_set.prefab", new Vector3(18f, 0, 7f), 35f, true);
            compoundProp(depot, "Barrels/Barrel_v3/Barrel_v3_quadro.prefab", new Vector3(-15f, 0, 2f), 0f, true);
            compoundProp(depot, "Barrels/Barrel_v2/Barrel_v2_quadro.prefab", new Vector3(-16.5f, 0, -3.5f), 45f, true);
            compoundProp(depot, "Oil_tanks/Oil_tank_v1/Oil_tank_v1.prefab", new Vector3(-21f, 0, 14f), 0f, false);
            compoundProp(depot, "Dumpsters/Dumpsters_v1/Dumpsters_v1_empty.prefab", new Vector3(9f, 0, 16f), 180f, true);
            var gen = compoundProp(depot, "Other_props/Generators/Generator_v1/Generator_v1.prefab", new Vector3(-14f, 0, -8f), 90f, true);
            if (gen != null)
                sound(gen.transform, "Generator Sound", Vector3.up, VantageProceduralAudio.Kind.Generator, 0.55f, 2f, 18f, 1f);

            var camp = group(parent, "Camp");
            tent(camp, new Vector3(-18f, 0, -21f), 90f);
            tent(camp, new Vector3(-18f, 0, -13f), 90f);
            flagPole(camp, new Vector3(-7f, 0, -29f));

            var lights = group(parent, "Floodlights");
            floodlight(lights, new Vector3(CompoundMinX + 2.5f, 0, CompoundMinZ + 2.5f));
            floodlight(lights, new Vector3(CompoundMaxX - 2.5f, 0, CompoundMinZ + 2.5f));
            floodlight(lights, new Vector3(CompoundMaxX - 2.5f, 0, CompoundMaxZ - 2.5f));
            floodlight(lights, new Vector3(CompoundMinX + 2.5f, 0, CompoundMaxZ - 2.5f));

            environmentSound(group(parent, "Environment Sound"));

            Debug.Log($"[Vantage] Military compound: {_placed} pieces placed, {_skipped} skipped (off the ground or colliding with the map).");
        }

        #region Pieces

        private static void perimeter(Transform parent)
        {
            const string wall = "Fences/Concrete_fences/Concrete_fence_v2/Concrete_fence_v2_S.prefab";
            // South side leaves a gap for the checkpoint gate.
            fenceLine(parent, wall, new Vector3(CompoundMinX, 0, CompoundMinZ), new Vector3(-5.5f, 0, CompoundMinZ));
            fenceLine(parent, wall, new Vector3(5.5f, 0, CompoundMinZ), new Vector3(CompoundMaxX, 0, CompoundMinZ));
            fenceLine(parent, wall, new Vector3(CompoundMaxX, 0, CompoundMinZ), new Vector3(CompoundMaxX, 0, CompoundMaxZ));
            fenceLine(parent, wall, new Vector3(CompoundMaxX, 0, CompoundMaxZ), new Vector3(CompoundMinX, 0, CompoundMaxZ));
            fenceLine(parent, wall, new Vector3(CompoundMinX, 0, CompoundMaxZ), new Vector3(CompoundMinX, 0, CompoundMinZ));
        }

        private static void checkpoint(Transform parent)
        {
            compoundProp(parent, "Fences/Road_blocks/Road_block_v1/Road_block_v1.prefab", new Vector3(-4.2f, 0, CompoundMinZ - 1.5f), 0f, true);
            compoundProp(parent, "Fences/Road_blocks/Road_block_v1/Road_block_v1.prefab", new Vector3(4.2f, 0, CompoundMinZ - 1.5f), 0f, true);

            var y = groundY(parent, new Vector3(-3.6f, 0, CompoundMinZ));
            if (float.IsNaN(y))
                return;

            // Guard post and a raised barrier arm: the checkpoint was abandoned open.
            box(parent, "Barrier Post", new Vector3(-3.8f, y, CompoundMinZ - 0.2f), new Vector3(-3.4f, y + 1.2f, CompoundMinZ + 0.2f), _hazard);
            boxRotated(parent, "Barrier Arm (raised)", new Vector3(-3.6f, y + 1.2f + 2.6f, CompoundMinZ), new Vector3(0.15f, 5.4f, 0.15f), Quaternion.Euler(0, 0, -12f), _hazard);
            box(parent, "Guard Booth", new Vector3(-8.2f, y, CompoundMinZ + 0.6f), new Vector3(-6.2f, y + 2.5f, CompoundMinZ + 2.6f), _military);
            box(parent, "Guard Booth Roof", new Vector3(-8.5f, y + 2.5f, CompoundMinZ + 0.3f), new Vector3(-5.9f, y + 2.7f, CompoundMinZ + 2.9f), _rust);
            light(parent, "Checkpoint Lamp", new Vector3(-7.2f, y + 3.2f, CompoundMinZ - 0.4f), new Color(1f, 0.85f, 0.6f), 2.5f, 9f);
            _placed += 4;
        }

        private static void sandbagNest(Transform parent, Vector3 local, float yaw)
        {
            var y = groundY(parent, local);
            if (float.IsNaN(y) || !clear(parent.TransformPoint(local + new Vector3(0, y + 0.8f, 0)), new Vector3(1.9f, 0.5f, 1.4f)))
            {
                _skipped++;
                return;
            }

            var nest = group(parent, "Sandbag Nest");
            nest.localPosition = new Vector3(local.x, y, local.z);
            nest.localRotation = Quaternion.Euler(0, yaw, 0);
            box(nest, "Sandbag Wall", new Vector3(-1.7f, 0, -1.2f), new Vector3(1.7f, 1.1f, -0.5f), _sandbag);
            box(nest, "Sandbag Side", new Vector3(-1.7f, 0, -0.5f), new Vector3(-1.0f, 1.1f, 1.2f), _sandbag);
            box(nest, "Sandbag Side", new Vector3(1.0f, 0, -0.5f), new Vector3(1.7f, 1.1f, 1.2f), _sandbag);
            box(nest, "Ammo Crate", new Vector3(-0.4f, 0, 0.4f), new Vector3(0.4f, 0.45f, 0.9f), _military);
            _placed++;
        }

        private static void tent(Transform parent, Vector3 local, float yaw)
        {
            var y = groundY(parent, local);
            if (float.IsNaN(y) || !clear(parent.TransformPoint(local + new Vector3(0, y + 1.5f, 0)), new Vector3(2.6f, 1.2f, 2.8f)))
            {
                _skipped++;
                return;
            }

            const float halfWidth = 2f, height = 2.6f, length = 5f;
            var angle = Mathf.Atan2(height, halfWidth) * Mathf.Rad2Deg;
            var slant = Mathf.Sqrt(halfWidth * halfWidth + height * height);

            var t = group(parent, "Tent");
            t.localPosition = new Vector3(local.x, y, local.z);
            t.localRotation = Quaternion.Euler(0, yaw, 0);
            boxRotated(t, "Tent Roof L", new Vector3(-halfWidth / 2, height / 2, 0), new Vector3(slant, 0.06f, length), Quaternion.Euler(0, 0, angle), _canvas);
            boxRotated(t, "Tent Roof R", new Vector3(halfWidth / 2, height / 2, 0), new Vector3(slant, 0.06f, length), Quaternion.Euler(0, 0, -angle), _canvas);
            box(t, "Cot", new Vector3(-1.4f, 0, -1.8f), new Vector3(-0.6f, 0.45f, 0.2f), _military);
            box(t, "Cot", new Vector3(0.6f, 0, -1.8f), new Vector3(1.4f, 0.45f, 0.2f), _military);
            box(t, "Footlocker", new Vector3(-0.3f, 0, 1.2f), new Vector3(0.3f, 0.4f, 1.9f), _military);
            _placed++;
        }

        private static void flagPole(Transform parent, Vector3 local)
        {
            var y = groundY(parent, local);
            if (float.IsNaN(y))
            {
                _skipped++;
                return;
            }

            var f = group(parent, "Flag Pole");
            f.localPosition = new Vector3(local.x, y, local.z);
            box(f, "Pole", new Vector3(-0.06f, 0, -0.06f), new Vector3(0.06f, 8f, 0.06f), _metal);
            boxRotated(f, "Flag (torn)", new Vector3(0.85f, 7.3f, 0), new Vector3(1.6f, 0.95f, 0.03f), Quaternion.Euler(0, 0, -6f), _military);
            _placed++;
        }

        private static void floodlight(Transform parent, Vector3 local)
        {
            var y = groundY(parent, local);
            if (float.IsNaN(y) || !clear(parent.TransformPoint(local + new Vector3(0, y + 3.5f, 0)), new Vector3(0.4f, 3f, 0.4f)))
            {
                _skipped++;
                return;
            }

            var mast = group(parent, "Floodlight Mast");
            mast.localPosition = new Vector3(local.x, y, local.z);
            box(mast, "Mast", new Vector3(-0.15f, 0, -0.15f), new Vector3(0.15f, 7f, 0.15f), _metal);
            box(mast, "Lamp Head", new Vector3(-0.6f, 7f, -0.3f), new Vector3(0.6f, 7.5f, 0.3f), _metal);

            // Aim at the tower: the compound's light points the eye at the climb.
            var lamp = new GameObject("Floodlight").transform;
            lamp.SetParent(mast, false);
            lamp.localPosition = new Vector3(0, 7.1f, 0);
            var target = parent.parent.parent.TransformPoint(new Vector3(0, 6f, 0));
            lamp.rotation = Quaternion.LookRotation(target - lamp.position);
            var l = lamp.gameObject.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = new Color(1f, 0.93f, 0.8f);
            l.intensity = 6f;
            l.range = 45f;
            l.spotAngle = 55f;
            l.shadows = LightShadows.None;

            sound(mast, "Ballast Buzz", new Vector3(0, 7.2f, 0), VantageProceduralAudio.Kind.Buzz, 0.15f, 1f, 9f, 1f);
            _placed++;
        }

        /// <summary>
        /// Concept doc: drone hum and wind pull the player upward; the yard itself sounds abandoned and exposed.
        /// </summary>
        private static void environmentSound(Transform parent)
        {
            // Outdoor wind bed from the template, quiet and non-directional.
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ThirdPersonCoverShooter/Sounds/104320__proxima4__desert-simple.wav");
            if (clip != null)
            {
                var bed = new GameObject("Ambience - wind bed (2D)");
                bed.transform.SetParent(parent, false);
                var source = bed.AddComponent<AudioSource>();
                source.clip = clip;
                source.loop = true;
                source.playOnAwake = true;
                source.spatialBlend = 0f;
                source.volume = 0.22f;
            }

            // The sea beyond the north and west edges of the site.
            sound(parent, "Sea - north", new Vector3(0, 0, 40f), VantageProceduralAudio.Kind.Sea, 0.7f, 10f, 75f, 1f);
            sound(parent, "Sea - west", new Vector3(-40f, 0, 0), VantageProceduralAudio.Kind.Sea, 0.7f, 10f, 75f, 0.92f);
            sound(parent, "Sea - north-west", new Vector3(-32f, 0, 32f), VantageProceduralAudio.Kind.Sea, 0.6f, 10f, 70f, 1.06f);
            sound(parent, "Wind - roof", new Vector3(0, 19f, 0), VantageProceduralAudio.Kind.Wind, 0.5f, 4f, 22f, 1.3f);

            // The generator on the approach and the one on level 3 are running.
            foreach (var t in parent.parent.parent.GetComponentsInChildren<Transform>(true))
                if (t.name.Contains("Generator_v1") && t.parent.name.StartsWith("Props"))
                    sound(t, "Generator Sound", Vector3.up, VantageProceduralAudio.Kind.Generator, 0.5f, 2f, 16f, 0.95f);
        }

        private static void sound(Transform parent, string name, Vector3 local, VantageProceduralAudio.Kind kind, float volume, float min, float max, float pitch)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.AddComponent<AudioSource>();
            var audio = go.AddComponent<VantageProceduralAudio>();
            audio.Sound = kind;
            audio.Volume = volume;
            audio.MinDistance = min;
            audio.MaxDistance = max;
            audio.Pitch = pitch;
        }

        #endregion

        #region Placement

        /// <summary>
        /// Tiles a fence prefab along a line. Measures the prefab once to find its length and long axis.
        /// </summary>
        private static void fenceLine(Transform parent, string path, Vector3 from, Vector3 to)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Industrial + path);
            if (prefab == null)
            {
                Debug.LogWarning("[Vantage] Fence prefab not found: " + path);
                return;
            }

            var probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var b = rendererBounds(probe);
            Object.DestroyImmediate(probe);

            var alongX = b.size.x >= b.size.z;
            var pieceLength = Mathf.Max(1f, alongX ? b.size.x : b.size.z);
            var pivotOffset = b.center;

            var direction = (to - from);
            var count = Mathf.Max(1, Mathf.FloorToInt(direction.magnitude / pieceLength));
            direction.Normalize();
            var rotation = Quaternion.FromToRotation(alongX ? Vector3.right : Vector3.forward, direction);

            for (int i = 0; i < count; i++)
            {
                var center = from + direction * (pieceLength * (i + 0.5f));
                var y = groundY(parent, center);
                if (float.IsNaN(y))
                {
                    _skipped++;
                    continue;
                }

                var world = parent.TransformPoint(new Vector3(center.x, y, center.z));
                var half = new Vector3(pieceLength * 0.45f, b.size.y * 0.4f, Mathf.Max(0.2f, (alongX ? b.size.z : b.size.x) * 0.4f));
                if (!clear(world + Vector3.up * (b.size.y * 0.5f + 0.3f), half, parent.rotation * rotation * (alongX ? Quaternion.identity : Quaternion.Euler(0, 90, 0))))
                {
                    _skipped++;
                    continue;
                }

                var piece = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                piece.transform.rotation = parent.rotation * rotation;
                var offset = piece.transform.rotation * new Vector3(pivotOffset.x, 0, pivotOffset.z);
                piece.transform.position = world - offset;
                _placed++;
                Physics.SyncTransforms();
            }
        }

        private static GameObject compoundProp(Transform parent, string path, Vector3 local, float yaw, bool cover)
        {
            var y = groundY(parent, local);
            if (float.IsNaN(y))
            {
                _skipped++;
                return null;
            }

            var go = prop(parent, path, new Vector3(local.x, y, local.z), yaw, false, cover);
            if (go == null)
                return null;

            // Check the space it occupies, ignoring its own colliders.
            var colliders = go.GetComponentsInChildren<Collider>();
            foreach (var c in colliders)
                c.enabled = false;
            var b = rendererBounds(go);
            var free = clear(b.center + Vector3.up * 0.25f, Vector3.Max(b.extents * 0.85f - Vector3.up * 0.25f, Vector3.one * 0.1f));
            foreach (var c in colliders)
                c.enabled = true;

            if (!free)
            {
                Object.DestroyImmediate(go);
                _skipped++;
                return null;
            }

            _placed++;
            Physics.SyncTransforms();
            return go;
        }

        /// <summary>
        /// Ground height (local) under a point, or NaN if there is no ground there or it is not level with the tower base.
        /// </summary>
        private static float groundY(Transform parent, Vector3 local)
        {
            var origin = parent.TransformPoint(new Vector3(local.x, 30f, local.z));
            if (!Physics.Raycast(origin, Vector3.down, out var hit, 60f, PlacementMask, QueryTriggerInteraction.Ignore))
            {
                diagnose($"no ground at {local:F0}");
                return float.NaN;
            }

            var y = parent.InverseTransformPoint(hit.point).y;
            if (Mathf.Abs(y) > 1.2f)
            {
                diagnose($"ground at {local:F0} is {hit.collider.name} at local height {y:F1}");
                return float.NaN;
            }
            return y;
        }

        private static bool clear(Vector3 worldCenter, Vector3 halfExtents, Quaternion? rotation = null)
        {
            var hits = Physics.OverlapBox(worldCenter, halfExtents, rotation ?? Quaternion.identity, PlacementMask, QueryTriggerInteraction.Ignore);
            if (hits.Length > 0)
                diagnose($"blocked at {worldCenter:F0} by {hits[0].name} (parent {(hits[0].transform.parent != null ? hits[0].transform.parent.name : "-")})");
            return hits.Length == 0;
        }

        private static int _diagnostics;

        private static void diagnose(string message)
        {
            if (_diagnostics++ < 25)
                Debug.Log("[Vantage] Compound skip: " + message);
        }

        private static Bounds rendererBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.one);
            var b = renderers[0].bounds;
            foreach (var r in renderers)
                b.Encapsulate(r.bounds);
            return b; // world space; fenceLine measures its probe at the origin, so there it equals the pivot offset
        }

        #endregion
    }
}
