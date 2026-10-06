using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace Vantage.EditorTools
{
    /// <summary>
    /// Builds the VANTAGE watchtower blockout from the concept doc out of editable ProBuilder shapes.
    /// Local space: tower centred on x/z = 0, ground at y = 0, entrance on the -Z side.
    ///
    ///   Level 1  lobby, double height     floor 0.2   open stairwell up the east side
    ///   Level 2  corridors and offices    floor 7.2   exit by the fire escape (west, outside)
    ///   Level 3  collapsed level          floor 11.2  exit up the collapsed slab ramp
    ///   Roof     open deck, mast, radio   floor 15.7
    /// </summary>
    public static partial class VantageTowerBuilder
    {
        public const string TowerName = "VANTAGE Tower";

        private const float F1 = 0.2f, F2 = 7.2f, F3 = 11.2f, FR = 15.7f, ParapetTop = 16.8f;
        private const float Half = 9f, Wall = 0.4f, Inner = Half - Wall, SlabThickness = 0.4f;

        public enum EnemyKind { Soldier, Drone, SlowDrone, ApproachDrone, Turret }

        public struct EnemySpot
        {
            public Vector3 Position;
            public string Label;
            public EnemyKind Kind;
        }

        public class Result
        {
            public GameObject Root;
            public Vector3 PlayerStart;
            public Quaternion PlayerFacing;
            public VantageLevel2Generator Level2;
            public VantageWaveSpawner Waves;
            public readonly List<EnemySpot> EnemySpots = new List<EnemySpot>();

            public void Add(Transform space, Vector3 local, string label, EnemyKind kind)
            {
                EnemySpots.Add(new EnemySpot { Position = space.TransformPoint(local), Label = label, Kind = kind });
            }
        }

        /// <summary>
        /// Prefabs and assets the builder wires into the tower. Filled in by VantageEnhance.
        /// </summary>
        public static GameObject DronePrefab;

        private struct Opening
        {
            public float A0, A1, Y0, Y1;
            public Opening(float a0, float a1, float y0, float y1) { A0 = a0; A1 = a1; Y0 = y0; Y1 = y1; }
        }

        private struct Hole
        {
            public float X0, X1, Z0, Z1;
            public Hole(float x0, float x1, float z0, float z1) { X0 = x0; X1 = x1; Z0 = z0; Z1 = z1; }
            public bool Contains(float x, float z) => x > X0 && x < X1 && z > Z0 && z < Z1;
        }

        private static Material _concrete, _concreteDark, _floor, _metal, _rust, _sandbag, _wood, _fabric, _body, _mastRed, _glowRed, _glowGreen, _guide;

        #region Entry

        public static Result Build(Vector3 near)
        {
            createMaterials();

            // Rebuilding keeps the tower where it is; only a first build searches for a spot.
            Vector3 site;
            var old = GameObject.Find(TowerName);
            if (old != null)
            {
                site = old.transform.position;
                Object.DestroyImmediate(old);
            }
            else if (!findSite(near, out site))
                Debug.LogWarning("[Vantage] No fully clear flat spot found, building the tower next to the player anyway.");

            // Stand on whatever ground is under the footprint (the tower may have been moved onto the raised asphalt yard).
            var groundHeight = footprintGround(site);
            if (Mathf.Abs(groundHeight - site.y) > 0.05f)
                Debug.Log($"[Vantage] Tower base moved from height {site.y:F2} to the ground at {groundHeight:F2}.");
            site.y = groundHeight;

            var root = new GameObject(TowerName).transform;
            root.position = site;

            var result = new Result { Root = root.gameObject };

            buildShell(group(root, "Structure"));
            buildApproach(group(root, "Approach"), result);
            buildEntrance(group(root, EntranceName));
            buildLobby(group(root, "Level 1 - Lobby"), result);
            buildCorridors(group(root, "Level 2 - Corridors"), result);
            buildFireEscape(group(root, "Fire Escape"));
            buildCollapsedLevel(group(root, "Level 3 - Collapsed"), result);
            buildRoof(group(root, "Roof"), result);
            buildGuidance(group(root, "Guidance (way up)"));
            VantageAtmosphere.Build(group(root, "Atmosphere"));
            dressWithProps(group(root, "Props (industrial pack)"));
            buildCompound(group(root, "Military Compound"));
            addCovers(root);

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.GetComponent<ProBuilderMesh>() != null)
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

            var start = new Vector3(0, 0, -24f);
            result.PlayerStart = groundAt(root.TransformPoint(start + Vector3.up * 5f), root.TransformPoint(start));
            result.PlayerFacing = Quaternion.LookRotation(root.forward);

            Debug.Log($"[Vantage] Tower built at {site:F1}. {root.GetComponentsInChildren<ProBuilderMesh>().Length} ProBuilder shapes.");
            return result;
        }

        #endregion

        #region Levels

        private static void buildShell(Transform parent)
        {
            // Outer walls run from below the lobby floor up to the roof parapet.
            const float y0 = -0.2f, y1 = ParapetTop, z = Half - Wall / 2, x = Half - Wall / 2;

            wall(parent, "Wall South (entrance)", true, -z, -Half, Half, y0, y1, Wall, _concrete,
                new Opening(-1.6f, 1.6f, F1, 4.2f),          // entrance
                new Opening(-7.5f, -4f, 2f, 5.5f),           // lobby broken glass
                new Opening(3f, 5.4f, 2f, 5.5f),
                new Opening(-4.2f, -2.2f, 8.4f, 9.8f),       // L2 office windows
                new Opening(2.2f, 4.2f, 8.4f, 9.8f),
                new Opening(-6.5f, -1f, 12.2f, 14.6f));      // L3 blown-out wall

            wall(parent, "Wall North", true, z, -Half, Half, y0, y1, Wall, _concrete,
                new Opening(-6f, -2f, 2f, 5.5f),             // lobby glass
                new Opening(-3f, 1f, 8.2f, 10f),             // L2 corridor window
                new Opening(1.5f, 6.5f, 12.2f, 14.6f));      // L3 blown-out wall

            wall(parent, "Wall West", false, -x, -Inner, Inner, y0, y1, Wall, _concrete,
                new Opening(-2f, 4f, 2f, 5.5f),              // lobby glass
                new Opening(-8f, -5.4f, F2, 9.8f),           // door out to the fire escape (wide, framed in yellow)
                new Opening(1.2f, 3.8f, F3, 13.9f),          // door in from the fire escape (wide, framed in yellow)
                new Opening(-6f, -2f, 12.2f, 14.6f));

            wall(parent, "Wall East", false, x, -Inner, Inner, y0, y1, Wall, _concrete,
                new Opening(-4f, 4f, 3f, 6f),                // light over the stairwell
                new Opening(-3f, 0f, 8.4f, 9.8f),
                new Opening(-2f, 6f, 12.2f, 14.6f));

            var shaft = new Hole(1.5f, 4.5f, -7.5f, -4.5f);   // collapse shaft: L3 looks down to the lobby

            slab(parent, "Floor L1", F1, _floor);
            slab(parent, "Floor L2", F2, _floor, new Hole(5.8f, Inner, -6.4f, 3.8f), shaft);
            slab(parent, "Floor L3", F3, _floor, shaft, new Hole(-3f, -1f, 6f, 8f));
            slab(parent, "Roof Deck", FR, _concreteDark, new Hole(1.8f, 5.2f, -1.5f, 5f), new Hole(-3f, -1f, -3f, -1f), new Hole(-7f, -5f, -4f, -2f));
        }

        private static void buildLobby(Transform parent, Result result)
        {
            // Load-bearing pillars, double as long-range cover.
            foreach (var p in new[] { new Vector2(-4.5f, -3f), new Vector2(-4.5f, 3f), new Vector2(4.5f, -3f), new Vector2(4.5f, 3f) })
                box(parent, "Pillar", new Vector3(p.x - 0.4f, F1, p.y - 0.4f), new Vector3(p.x + 0.4f, F2 - SlabThickness, p.y + 0.4f), _concrete);

            // Reception desk: long-range cover in the middle of the lobby (the pistol now waits in the guard hut).
            var desk = group(parent, "Reception Desk");
            box(desk, "Desk Front", new Vector3(-2f, F1, -0.4f), new Vector3(2f, 1.3f, 0f), _wood);
            box(desk, "Desk Side L", new Vector3(-2f, F1, 0f), new Vector3(-1.6f, 1.3f, 1.4f), _wood);
            box(desk, "Desk Side R", new Vector3(1.6f, F1, 0f), new Vector3(2f, 1.3f, 1.4f), _wood);

            // Story prop 1: sandbags by the door, facing inward. They were defending against something already inside.
            var sandbags = group(parent, "Story 1 - Sandbags Facing Inward");
            box(sandbags, "Sandbag Wall", new Vector3(-7.8f, F1, -5.4f), new Vector3(-3.2f, 1.2f, -4.8f), _sandbag);
            box(sandbags, "Sandbag Side", new Vector3(-3.8f, F1, -6.8f), new Vector3(-3.2f, 1.2f, -5.4f), _sandbag);
            box(sandbags, "Ammo Crate", new Vector3(-6.6f, F1, -7.4f), new Vector3(-5.8f, 0.7f, -6.8f), _metal);
            box(sandbags, "Ammo Crate", new Vector3(-5.4f, F1, -7.6f), new Vector3(-4.6f, 0.7f, -7.0f), _metal);

            var cover = group(parent, "Cover");
            box(cover, "Crate", new Vector3(-2.6f, F1, 4f), new Vector3(-1.2f, 1.5f, 5.3f), _wood);
            box(cover, "Crate", new Vector3(2f, F1, 5f), new Vector3(3.4f, 1.2f, 6.2f), _wood);
            box(cover, "Crate", new Vector3(-6.6f, F1, 1f), new Vector3(-5.2f, 1.6f, 2.3f), _wood);
            box(cover, "Rubble", new Vector3(2f, F1, -6.2f), new Vector3(4.2f, 0.7f, -4.8f), _concreteDark); // fell down the shaft

            // Open stairwell up the east side, visible from the door.
            stair(parent, LobbyStairName, new Vector3(6f, F1, -6f), new Vector3(2.6f, F2 - F1, 9.8f), LobbyStairSteps, _concrete);

            light(parent, "Lobby Sun Bounce", new Vector3(-3f, 5f, -4f), new Color(1f, 0.72f, 0.45f), 3f, 14f);
            light(parent, "Lobby Back", new Vector3(2f, 5f, 4f), new Color(1f, 0.7f, 0.45f), 2.5f, 12f);
            light(parent, "Stairwell Bottom (way up)", new Vector3(7.3f, 2.5f, -5f), new Color(1f, 0.95f, 0.85f), 4f, 9f);

            // Concept doc: one slow drone, enough to teach what the enemy is and what cover does.
            result.Add(parent, new Vector3(-3f, 4.2f, 5.5f), "Level 1 slow drone", EnemyKind.SlowDrone);
        }

        private static void buildApproach(Transform parent, Result result)
        {
            // Guard hut on the approach: lit, so it is the brightest thing the player sees at spawn.
            var hutCenter = new Vector3(-7f, 0, -20f);
            var floor = parent.InverseTransformPoint(groundAt(parent.TransformPoint(hutCenter + Vector3.up * 20f), parent.TransformPoint(hutCenter))).y;
            var hut = group(parent, "Guard Hut");
            hut.localPosition = new Vector3(hutCenter.x, floor, hutCenter.z);

            // Nearly flush with the ground: the character cannot step up ledges, so a 10 cm lip would keep him out.
            box(hut, "Hut Floor", new Vector3(-1.6f, -0.27f, -1.6f), new Vector3(1.6f, 0.03f, 1.6f), _concreteDark);
            box(hut, "Hut Wall Back", new Vector3(-1.6f, 0.03f, 1.4f), new Vector3(1.6f, 2.6f, 1.6f), _concrete);
            box(hut, "Hut Wall Left", new Vector3(-1.6f, 0.03f, -1.6f), new Vector3(-1.4f, 2.6f, 1.4f), _concrete);
            box(hut, "Hut Wall Front", new Vector3(-1.6f, 0.03f, -1.6f), new Vector3(1.6f, 1.1f, -1.4f), _concrete);
            box(hut, "Hut Roof", new Vector3(-1.9f, 2.6f, -1.9f), new Vector3(1.9f, 2.8f, 1.9f), _rust);
            box(hut, "Hut Post", new Vector3(1.4f, 0.03f, -1.6f), new Vector3(1.6f, 2.6f, -1.4f), _concrete);
            box(hut, "Hut Desk", new Vector3(-1.3f, 0.03f, 0.4f), new Vector3(0.2f, 0.95f, 1.3f), _wood);
            VantageSetup.AddPickupAt("Pistol", hut.TransformPoint(new Vector3(-0.55f, 1.25f, 0.85f)), hut);
            light(hut, "Hut Bulb (go here)", new Vector3(0, 2.3f, 0), new Color(1f, 0.82f, 0.55f), 3.5f, 6f);

            // A little cover between the hut and the door for the tutorial fight.
            foreach (var p in new[] { new Vector3(-2.8f, 0, -13.5f), new Vector3(2.6f, 0, -12.2f), new Vector3(-0.2f, 0, -16.2f) })
            {
                var y = parent.InverseTransformPoint(groundAt(parent.TransformPoint(p + Vector3.up * 20f), parent.TransformPoint(p))).y;
                box(parent, "Concrete Barrier", new Vector3(p.x - 0.9f, y, p.z - 0.3f), new Vector3(p.x + 0.9f, y + 1.1f, p.z + 0.3f), _concrete);
            }

            // Two drones guard the door. Their short sight keeps the spawn and the hut safe.
            result.Add(parent, new Vector3(2.5f, 3.6f, -10.8f), "Approach drone east", EnemyKind.ApproachDrone);
            result.Add(parent, new Vector3(-3.5f, 4.2f, -10.2f), "Approach drone west", EnemyKind.ApproachDrone);
        }

        private static void buildCorridors(Transform parent, Result result)
        {
            const float y0 = F2, y1 = F3 - SlabThickness, t = 0.2f;

            // Authored: the sealed collapse room around the shaft, and the stairwell railing.
            wall(parent, "Collapsed Room Wall West", false, 0f, -Inner, -1.5f, y0, y1, t, _concreteDark);
            wall(parent, "Collapsed Room Wall North", true, -1.5f, 0f, 5.6f, y0, y1, t, _concreteDark);
            wall(parent, "Collapsed Room Wall East", false, 5.6f, -Inner, -1.5f, y0, y1, t, _concreteDark);
            wall(parent, "Stairwell Railing", false, 5.7f, -1.5f, 3.8f, y0, y0 + 1f, 0.15f, _metal);
            // Closes the generated rooms off from the stair landing, so the only way on is the hall (main route).
            wall(parent, "Landing Wall", false, 5.6f, 3.8f, 5.6f, y0, y1, t, _concreteDark);
            box(parent, "Collapse Debris", new Vector3(0.6f, y0, -8.2f), new Vector3(1.4f, y0 + 0.6f, -6.8f), _concreteDark);

            // Story prop 2: a door barricaded from the player's side. They were retreating upward too.
            // Against the outer wall so it never blocks a generated route; the generator keeps this spot clear.
            var barricade = group(parent, "Story 2 - Barricaded Door");
            box(barricade, "Door (sealed)", new Vector3(-Inner, y0, 1.8f), new Vector3(-Inner + 0.06f, y0 + 2.3f, 3.4f), _body);
            box(barricade, "Crate", new Vector3(-Inner + 0.06f, y0, 1.6f), new Vector3(-7.6f, y0 + 1f, 3.6f), _wood);
            box(barricade, "Crate", new Vector3(-Inner + 0.06f, y0 + 1f, 2f), new Vector3(-7.8f, y0 + 1.8f, 3.1f), _wood);
            boxRotated(barricade, "Plank", new Vector3(-7.5f, y0 + 1.5f, 2.6f), new Vector3(0.08f, 0.25f, 2.1f), Quaternion.Euler(35f, 0, 0), _wood);
            boxRotated(barricade, "Plank", new Vector3(-7.5f, y0 + 1.7f, 2.6f), new Vector3(0.08f, 0.25f, 2.1f), Quaternion.Euler(-30f, 0, 0), _wood);

            // Procedural: seeded room-and-corridor layout between the stair landing (entry) and the fire escape door (exit).
            // It also places the bunk room (story prop 3) and the three level 2 drones.
            var generator = new GameObject("Level 2 Generator").AddComponent<VantageLevel2Generator>();
            generator.transform.SetParent(parent, false);
            generator.FloorY = y0;
            generator.CeilingY = y1;
            generator.WallMaterial = _concreteDark;
            generator.WoodMaterial = _wood;
            generator.MetalMaterial = _metal;
            generator.FabricMaterial = _fabric;
            generator.DronePrefab = DronePrefab;
            result.Level2 = generator;

            light(parent, "Emergency A1", new Vector3(-5f, 10.4f, 7f), new Color(1f, 0.12f, 0.08f), 2.2f, 7f);
            light(parent, "Emergency A2", new Vector3(2f, 10.4f, 7f), new Color(1f, 0.12f, 0.08f), 2.2f, 7f);
            light(parent, "Emergency B1", new Vector3(-7f, 10.4f, -1f), new Color(1f, 0.12f, 0.08f), 2.2f, 7f);
            light(parent, "Emergency B2", new Vector3(-4f, 10.4f, -6f), new Color(1f, 0.12f, 0.08f), 1.8f, 6f);
            light(parent, "Emergency Office", new Vector3(-2.5f, 10.4f, 2f), new Color(1f, 0.15f, 0.1f), 1.5f, 6f);
            light(parent, "Fire Escape Door (way up)", new Vector3(-7.4f, 10f, -6.7f), new Color(0.85f, 0.92f, 1f), 4f, 7f);
            light(parent, "Stairwell Top", new Vector3(7.3f, 10.2f, 5f), new Color(1f, 0.95f, 0.85f), 3f, 8f);
        }

        private static void buildFireEscape(Transform parent)
        {
            box(parent, "Balcony L2", new Vector3(-11.6f, F2 - 0.3f, -8.2f), new Vector3(-Half, F2, -4.6f), _rust);
            box(parent, "Rail", new Vector3(-11.6f, F2, -8.2f), new Vector3(-11.5f, F2 + 1.1f, -4.6f), _rust);
            box(parent, "Rail", new Vector3(-11.6f, F2, -8.2f), new Vector3(-Half, F2 + 1.1f, -8.1f), _rust);

            stair(parent, FireEscapeStairName, new Vector3(-11.5f, F2, -4.6f), new Vector3(2.4f, F3 - F2, 5.6f), FireEscapeSteps, _rust);
            var slope = Mathf.Atan2(F3 - F2, 5.6f) * Mathf.Rad2Deg;
            boxRotated(parent, "Stair Rail", new Vector3(-11.55f, (F2 + F3) / 2 + 1.05f, -1.8f), new Vector3(0.08f, 0.08f, 6.9f), Quaternion.Euler(-slope, 0, 0), _rust);

            box(parent, "Landing L3", new Vector3(-11.6f, F3 - 0.3f, 1f), new Vector3(-Half, F3, 4f), _rust);
            box(parent, "Rail", new Vector3(-11.6f, F3, 1f), new Vector3(-11.5f, F3 + 1.1f, 4f), _rust);
            box(parent, "Rail", new Vector3(-11.6f, F3, 3.9f), new Vector3(-Half, F3 + 1.1f, 4f), _rust);
        }

        private static void buildCollapsedLevel(Transform parent, Result result)
        {
            const float y0 = F3, y1 = FR - SlabThickness;

            // Story prop 4: the rifle in a lit alcove, a body beside it. Someone got this far and no further.
            var alcove = group(parent, "Story 4 - Rifle Alcove");
            box(alcove, "Alcove Wall", new Vector3(-6.65f, y0, 6.8f), new Vector3(-6.35f, y1, Inner), _concreteDark);
            box(alcove, "Alcove Wall", new Vector3(-3.15f, y0, 6.8f), new Vector3(-2.85f, y1, Inner), _concreteDark);
            VantageSetup.AddPickupAt("Rifle", parent.TransformPoint(new Vector3(-4.6f, y0 + 0.7f, 7.9f)), alcove, 0f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(alcove, false);
            body.transform.localPosition = new Vector3(-5.6f, y0 + 0.25f, 7.5f);
            body.transform.localRotation = Quaternion.Euler(0, 20f, 90f);
            body.transform.localScale = new Vector3(0.5f, 0.9f, 0.45f);
            body.GetComponent<Renderer>().sharedMaterial = _body;

            spot(alcove, "Alcove Light", new Vector3(-4.7f, y1 - 0.2f, 7.8f), new Color(1f, 0.9f, 0.7f), 12f, 7f, 70f);

            // The collapsed slab that forms the ramp to the roof.
            const float run = 8f, rise = FR - F3;
            var angle = Mathf.Atan2(rise, run);
            var length = Mathf.Sqrt(run * run + rise * rise) + 0.3f;
            var rotation = Quaternion.Euler(-angle * Mathf.Rad2Deg, 0, 0);
            var mid = new Vector3(3.5f, (F3 + FR) / 2, 1f) - rotation * Vector3.up * 0.175f - rotation * Vector3.forward * 0.15f;
            boxRotated(parent, "Collapsed Slab Ramp", mid, new Vector3(3f, 0.35f, length), rotation, _concrete);

            var debris = group(parent, "Debris And Cover");
            boxRotated(debris, "Fallen Slab", new Vector3(6.6f, y0 + 0.6f, -6.2f), new Vector3(2.6f, 0.3f, 2.2f), Quaternion.Euler(10f, 25f, 28f), _concrete);
            boxRotated(debris, "Fallen Slab", new Vector3(-1.8f, y0 + 0.5f, -6.1f), new Vector3(2.4f, 0.3f, 1.8f), Quaternion.Euler(-22f, -15f, 8f), _concrete);
            box(debris, "Block", new Vector3(-7f, y0, -1.5f), new Vector3(-5.6f, y0 + 1.2f, -0.4f), _concreteDark);
            box(debris, "Block", new Vector3(6f, y0, 0.5f), new Vector3(7.4f, y0 + 1.2f, 1.6f), _concreteDark);
            box(debris, "Block", new Vector3(-2f, y0, 2.5f), new Vector3(-0.8f, y0 + 1f, 3.5f), _concreteDark);
            box(debris, "Rubble", new Vector3(0.6f, y0, -8.2f), new Vector3(1.4f, y0 + 0.5f, -7.6f), _concreteDark);

            // Railing round the collapse shaft: the view down to the lobby stays, falling back down does not.
            railing(parent, new Vector3(1.5f, y0, -4.5f), new Vector3(4.5f, y0, -4.5f));
            railing(parent, new Vector3(1.5f, y0, -7.5f), new Vector3(4.5f, y0, -7.5f));
            railing(parent, new Vector3(1.5f, y0, -7.5f), new Vector3(1.5f, y0, -4.5f));
            railing(parent, new Vector3(4.5f, y0, -7.5f), new Vector3(4.5f, y0, -4.5f));

            light(parent, "Daylight Fill", new Vector3(-2f, y1 - 0.3f, -2f), new Color(0.85f, 0.9f, 1f), 2f, 10f);
            light(parent, "Ramp Top (way up)", new Vector3(3.5f, FR + 0.6f, 4f), new Color(1f, 0.9f, 0.7f), 5f, 10f);

            // Mixed engagement: soldiers on this floor, a drone rising out of the collapse shaft below.
            result.Add(parent, new Vector3(7f, y0, -2.5f), "Level 3 soldier east", EnemyKind.Soldier);
            result.Add(parent, new Vector3(-7f, y0, -6.5f), "Level 3 soldier south-west", EnemyKind.Soldier);
            result.Add(parent, new Vector3(3f, 9.6f, -6f), "Level 3 drone in the shaft", EnemyKind.Drone);
            result.Add(parent, new Vector3(-0.5f, y0 + 2.4f, 4.5f), "Level 3 drone north", EnemyKind.Drone);
        }

        private static void buildRoof(Transform parent, Result result)
        {
            // Radio mast: the landmark seen from outside, from level 2 and through the level 3 holes.
            var mast = group(parent, "Radio Mast");
            var mastBase = new Vector3(-4.5f, FR, 4.5f);
            const float mastHeight = 18f, leg = 0.8f;

            foreach (var corner in new[] { new Vector2(-leg, -leg), new Vector2(leg, -leg), new Vector2(-leg, leg), new Vector2(leg, leg) })
                box(mast, "Mast Leg", mastBase + new Vector3(corner.x - 0.12f, 0, corner.y - 0.12f), mastBase + new Vector3(corner.x + 0.12f, mastHeight, corner.y + 0.12f), (corner.x > 0) == (corner.y > 0) ? _mastRed : _metal);

            for (float h = 3f; h < mastHeight; h += 3f)
            {
                box(mast, "Mast Brace", mastBase + new Vector3(-leg, h, -leg - 0.06f), mastBase + new Vector3(leg, h + 0.12f, -leg + 0.06f), _metal);
                box(mast, "Mast Brace", mastBase + new Vector3(-leg, h, leg - 0.06f), mastBase + new Vector3(leg, h + 0.12f, leg + 0.06f), _metal);
                box(mast, "Mast Brace", mastBase + new Vector3(-leg - 0.06f, h, -leg), mastBase + new Vector3(-leg + 0.06f, h + 0.12f, leg), _metal);
                box(mast, "Mast Brace", mastBase + new Vector3(leg - 0.06f, h, -leg), mastBase + new Vector3(leg + 0.06f, h + 0.12f, leg), _metal);
            }

            var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beacon.name = "Beacon";
            Object.DestroyImmediate(beacon.GetComponent<Collider>());
            beacon.transform.SetParent(mast, false);
            beacon.transform.localPosition = mastBase + Vector3.up * (mastHeight + 0.4f);
            beacon.transform.localScale = Vector3.one * 0.6f;
            beacon.GetComponent<Renderer>().sharedMaterial = _glowRed;
            light(mast, "Beacon Light", mastBase + Vector3.up * (mastHeight + 0.4f), new Color(1f, 0.1f, 0.05f), 4f, 12f);

            // The "turret": a gun position on the mast that covers the centre of the roof.
            box(mast, "Turret Platform", mastBase + new Vector3(-1.4f, 4.3f, -1.4f), mastBase + new Vector3(1.4f, 4.5f, 1.4f), _rust);
            box(mast, "Platform Rail", mastBase + new Vector3(-1.4f, 4.5f, -1.4f), mastBase + new Vector3(1.4f, 5.4f, -1.3f), _rust);
            box(mast, "Platform Rail", mastBase + new Vector3(1.3f, 4.5f, -1.4f), mastBase + new Vector3(1.4f, 5.4f, 1.4f), _rust);
            result.Add(parent, mastBase + new Vector3(0, 4.5f, -0.9f), "Roof mast turret", EnemyKind.Turret);

            // Story prop 5: the radio, still powered, transmitting to nobody.
            var radio = group(parent, "Story 5 - Radio Still Transmitting");
            box(radio, "Radio", new Vector3(5.6f, FR, 5.6f), new Vector3(6.8f, FR + 0.6f, 6.2f), _metal);
            box(radio, "Radio Display", new Vector3(5.8f, FR + 0.3f, 5.55f), new Vector3(6.4f, FR + 0.5f, 5.6f), _glowGreen);
            box(radio, "Antenna", new Vector3(6.6f, FR + 0.6f, 6f), new Vector3(6.65f, FR + 2.4f, 6.05f), _metal);
            light(radio, "Radio Glow", new Vector3(6.1f, FR + 0.6f, 5.2f), new Color(0.3f, 1f, 0.4f), 1.5f, 3f);

            result.Add(parent, new Vector3(-6.5f, FR, -6.5f), "Roof soldier south-west", EnemyKind.Soldier);

            // Three waves of drones over different edges; the turret on the mast denies the centre.
            var waves = new GameObject("Roof Waves").AddComponent<VantageWaveSpawner>();
            waves.transform.SetParent(parent.parent, false);
            waves.DronePrefab = DronePrefab;
            waves.RoofHeight = FR;
            waves.HalfSize = Half;
            waves.Waves.Add(new VantageWaveSpawner.Wave { Edge = "South", Count = 2, SpawnCenter = new Vector3(0, FR + 4f, -17f), Spread = 3f });
            waves.Waves.Add(new VantageWaveSpawner.Wave { Edge = "East", Count = 3, SpawnCenter = new Vector3(17f, FR + 4f, 0), Spread = 4f });
            waves.Waves.Add(new VantageWaveSpawner.Wave { Edge = "North and West", Count = 4, SpawnCenter = new Vector3(-12f, FR + 5f, 12f), Spread = 5f });
            result.Waves = waves;
        }

        #endregion

        #region Props and cover

        private const string Industrial = "Assets/RPG_FPS_game_assets_industrial/";
        private const string CoverProp = "Prop (cover) - ";

        /// <summary>
        /// Real props from the industrial pack on top of the blockout. Names starting with "Prop (cover)" get covers.
        /// The roof stays nearly empty on purpose: the concept doc's roof takes cover away.
        /// </summary>
        private static void dressWithProps(Transform parent)
        {
            // Approach
            prop(parent, "Barrels/Barrel_v2/Barrel_v2_quadro.prefab", new Vector3(-4.2f, 0, -18.6f), 20f, true, true);
            prop(parent, "Other_props/Palets/Palet_v1/Palet_v1_set.prefab", new Vector3(5f, 0, -17.5f), -15f, true, true);
            prop(parent, "Other_props/Generators/Generator_v1/Generator_v1.prefab", new Vector3(5.5f, 0, -11.5f), 90f, true, true);
            prop(parent, "Fences/Road_blocks/Road_block_v1/Road_block_v1.prefab", new Vector3(-6f, 0, -12.5f), 10f, true, true);

            // Lobby
            prop(parent, "Barrels/Barrel_v1/Barrel_v1_LD1.prefab", new Vector3(-7.6f, F1, 7.5f), 0f, false, true);
            prop(parent, "Barrels/Barrel_v1/Barrel_v1_LD1.prefab", new Vector3(-6.7f, F1, 7.8f), 40f, false, true);
            prop(parent, "Boxes/Wooden_box_v1/Wooden_box_v1_LD1square.prefab", new Vector3(-7.4f, F1, -1.4f), 15f, false, true);
            prop(parent, "Other_props/Palets/Bags_on_pallet_v1/Bags_on_pallet_v1_1.prefab", new Vector3(-2.6f, F1, -5.4f), 0f, false, true);
            prop(parent, "Other_props/Electric_box/Electric_box_v1/Electric_box_v1.prefab", new Vector3(-8.1f, F1, 5.2f), 90f, false, false);

            // Level 3
            prop(parent, "Barrels/Barrel_v3/Barrel_v3_single.prefab", new Vector3(6.9f, F3, 4.6f), 0f, false, true);
            prop(parent, "Boxes/Wooden_box_v1/Wooden_box_v1_LD1.prefab", new Vector3(-4f, F3, -7.4f), 30f, false, true);
            prop(parent, "Other_props/Generators/Generator_v1/Generator_v1.prefab", new Vector3(6.4f, F3, -7.4f), 0f, false, true);

            // Roof: machinery at the edges only, no cover.
            prop(parent, "Other_props/Conditioners/Conditioner_v1/Conditioner_v1.prefab", new Vector3(6.8f, FR, -7.4f), 180f, false, false);
            prop(parent, "Other_props/Electric_box/Electric_box_v1/Electric_box_v1.prefab", new Vector3(7.8f, FR, 6.9f), -90f, false, false);
        }

        private static GameObject prop(Transform parent, string path, Vector3 local, float yaw, bool snapToGround, bool cover)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Industrial + path);
            if (prefab == null)
            {
                Debug.LogWarning("[Vantage] Prop not found: " + path);
                return null;
            }

            if (snapToGround)
                local.y = parent.InverseTransformPoint(groundAt(parent.TransformPoint(local + Vector3.up * 20f), parent.TransformPoint(local))).y;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = (cover ? CoverProp : "Prop - ") + prefab.name;
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return go;
        }

        private static readonly HashSet<string> CoverPieces = new HashSet<string>
        {
            "Pillar", "Desk Front", "Desk Side L", "Desk Side R", "Crate", "Rubble", "Sandbag Wall", "Sandbag Side",
            "Concrete Barrier", "Block", "Hut Wall Front", "Collapse Debris",
        };

        /// <summary>
        /// Cover Shooter covers for every piece of cover in the tower, so the character and the soldiers can use them.
        /// </summary>
        private static void addCovers(Transform root)
        {
            Physics.SyncTransforms();
            var container = group(root, "Covers");
            var count = 0;

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (CoverPieces.Contains(t.name) || t.name.StartsWith(CoverProp))
                    count += VantageCoverUtil.AddFor(t.gameObject, container);

            Debug.Log($"[Vantage] Added {count} covers.");
        }

        #endregion

        #region Site

        /// <summary>
        /// Nearest flat spot to 'near' where the tower, fire escape and approach fit without hitting anything.
        /// </summary>
        private static bool findSite(Vector3 near, out Vector3 site)
        {
            Physics.SyncTransforms();
            var mask = ~((1 << 2) | (1 << 10) | (1 << 11));
            var best = float.MaxValue;
            site = groundAt(near + Vector3.forward * 40f + Vector3.up * 200f, near + Vector3.forward * 40f);
            var found = false;

            for (float dx = -240f; dx <= 240f; dx += 6f)
            for (float dz = -240f; dz <= 240f; dz += 6f)
            {
                var distance = dx * dx + dz * dz;
                if (distance >= best || distance < 25f * 25f)
                    continue;

                var c = new Vector3(near.x + dx, near.y, near.z + dz);
                if (!Physics.Raycast(c + Vector3.up * 300f, Vector3.down, out var hit, 600f, mask, QueryTriggerInteraction.Ignore))
                    continue;

                var h = hit.point.y;
                var flat = true;
                foreach (var o in new[] { new Vector2(-12, -10), new Vector2(10, -10), new Vector2(-12, 10), new Vector2(10, 10), new Vector2(0, -26) })
                {
                    if (!Physics.Raycast(new Vector3(c.x + o.x, h + 50f, c.z + o.y), Vector3.down, out var cornerHit, 100f, mask, QueryTriggerInteraction.Ignore) || Mathf.Abs(cornerHit.point.y - h) > 0.7f)
                    {
                        flat = false;
                        break;
                    }
                }
                if (!flat)
                    continue;

                if (Physics.CheckBox(new Vector3(c.x - 1.3f, h + 9.6f, c.z), new Vector3(11.5f, 8.8f, 10.3f), Quaternion.identity, mask, QueryTriggerInteraction.Ignore))
                    continue;

                if (Physics.CheckBox(new Vector3(c.x, h + 2.6f, c.z - 18f), new Vector3(3f, 1.8f, 7.5f), Quaternion.identity, mask, QueryTriggerInteraction.Ignore))
                    continue;

                best = distance;
                site = new Vector3(c.x, h, c.z);
                found = true;
            }

            return found;
        }

        /// <summary>
        /// Median ground height under the tower footprint (5 x 5 samples), ignoring characters, covers and triggers.
        /// </summary>
        private static float footprintGround(Vector3 site)
        {
            Physics.SyncTransforms();
            var mask = ~((1 << 2) | (1 << 8) | (1 << 10) | (1 << 11));
            var heights = new List<float>();
            for (float dx = -8f; dx <= 8f; dx += 4f)
                for (float dz = -8f; dz <= 8f; dz += 4f)
                    if (Physics.Raycast(new Vector3(site.x + dx, site.y + 60f, site.z + dz), Vector3.down, out var hit, 200f, mask, QueryTriggerInteraction.Ignore))
                        heights.Add(hit.point.y);

            if (heights.Count == 0)
                return site.y;
            heights.Sort();
            return heights[heights.Count / 2];
        }

        private static Vector3 groundAt(Vector3 from, Vector3 fallback)
        {
            var mask = ~((1 << 2) | (1 << 10) | (1 << 11));
            return Physics.Raycast(from, Vector3.down, out var hit, 400f, mask, QueryTriggerInteraction.Ignore) ? hit.point : fallback;
        }

        #endregion

        #region Shapes

        private static Transform group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject box(Transform parent, string name, Vector3 min, Vector3 max, Material material)
        {
            return boxRotated(parent, name, (min + max) / 2, max - min, Quaternion.identity, material);
        }

        private static GameObject boxRotated(Transform parent, string name, Vector3 center, Vector3 size, Quaternion rotation, Material material)
        {
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            var mesh = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            return finish(mesh, parent, name, center, rotation, material);
        }

        private static GameObject stair(Transform parent, string name, Vector3 corner, Vector3 size, int steps, Material material)
        {
            var mesh = ShapeGenerator.GenerateStair(PivotLocation.FirstVertex, size, steps, true);
            var go = finish(mesh, parent, name, corner, Quaternion.identity, material);
            AddWalkRamp(go, steps);
            stepEdges(parent, name, corner, size, steps);
            return go;
        }

        private static GameObject finish(ProBuilderMesh mesh, Transform parent, string name, Vector3 localPosition, Quaternion localRotation, Material material)
        {
            mesh.gameObject.name = name;
            mesh.transform.SetParent(parent, false);
            mesh.transform.localPosition = localPosition;
            mesh.transform.localRotation = localRotation;
            mesh.SetMaterial(mesh.faces, material);

            // World-space UVs: the concrete runs on continuously across wall pieces instead of restarting
            // (and visibly jumping) at every seam around windows and doors.
            foreach (var face in mesh.faces)
            {
                var uv = face.uv;
                uv.useWorldSpace = true;
                face.uv = uv;
            }

            mesh.ToMesh();
            mesh.Refresh();

            var collider = mesh.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh.GetComponent<MeshFilter>().sharedMesh;
            return mesh.gameObject;
        }

        /// <summary>
        /// A straight wall along X (constant z) or along Z (constant x), cut around rectangular openings.
        /// </summary>
        private static void wall(Transform parent, string name, bool alongX, float fixedCoord, float a0, float a1, float y0, float y1, float thickness, Material material, params Opening[] openings)
        {
            var w = group(parent, name);
            var sorted = new List<Opening>(openings);
            sorted.Sort((p, q) => p.A0.CompareTo(q.A0));

            var cursor = a0;
            foreach (var o in sorted)
            {
                if (o.A0 > cursor)
                    wallPiece(w, alongX, fixedCoord, cursor, o.A0, y0, y1, thickness, material);
                if (o.Y0 > y0)
                    wallPiece(w, alongX, fixedCoord, o.A0, o.A1, y0, o.Y0, thickness, material);
                if (o.Y1 < y1)
                    wallPiece(w, alongX, fixedCoord, o.A0, o.A1, o.Y1, y1, thickness, material);
                cursor = Mathf.Max(cursor, o.A1);
            }

            if (cursor < a1)
                wallPiece(w, alongX, fixedCoord, cursor, a1, y0, y1, thickness, material);
        }

        private static void wallPiece(Transform parent, bool alongX, float fixedCoord, float s0, float s1, float y0, float y1, float t, Material material)
        {
            if (s1 - s0 < 0.01f || y1 - y0 < 0.01f)
                return;

            if (alongX)
                box(parent, "Wall", new Vector3(s0, y0, fixedCoord - t / 2), new Vector3(s1, y1, fixedCoord + t / 2), material);
            else
                box(parent, "Wall", new Vector3(fixedCoord - t / 2, y0, s0), new Vector3(fixedCoord + t / 2, y1, s1), material);
        }

        /// <summary>
        /// A floor slab filling the inside of the outer walls, with rectangular holes cut out.
        /// </summary>
        private static void slab(Transform parent, string name, float top, Material material, params Hole[] holes)
        {
            var s = group(parent, name);
            var xs = new SortedSet<float> { -Inner, Inner };
            var zs = new SortedSet<float> { -Inner, Inner };
            foreach (var h in holes)
            {
                xs.Add(h.X0); xs.Add(h.X1);
                zs.Add(h.Z0); zs.Add(h.Z1);
            }

            var xl = new List<float>(xs);
            var zl = new List<float>(zs);

            for (int zi = 0; zi < zl.Count - 1; zi++)
            {
                var start = -1;
                for (int xi = 0; xi <= xl.Count - 1; xi++)
                {
                    var solid = xi < xl.Count - 1 && !isHole(holes, (xl[xi] + xl[xi + 1]) / 2, (zl[zi] + zl[zi + 1]) / 2);

                    if (solid && start < 0)
                        start = xi;
                    else if (!solid && start >= 0)
                    {
                        box(s, "Slab", new Vector3(xl[start], top - SlabThickness, zl[zi]), new Vector3(xl[xi], top, zl[zi + 1]), material);
                        start = -1;
                    }
                }
            }
        }

        private static bool isHole(Hole[] holes, float x, float z)
        {
            foreach (var h in holes)
                if (h.Contains(x, z))
                    return true;
            return false;
        }

        private static void light(Transform parent, string name, Vector3 localPosition, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.Soft;
        }

        private static void spot(Transform parent, string name, Vector3 localPosition, Color color, float intensity, float range, float angle)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.spotAngle = angle;
            l.shadows = LightShadows.Soft;
        }

        #endregion

        #region Materials

        private static void createMaterials()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Vantage/Materials"))
                AssetDatabase.CreateFolder("Assets/Vantage", "Materials");

            // Tileable concrete from the industrial pack; ProBuilder UVs are in metres, so 0.3 = one repeat every ~3 m.
            const string concrete = "Assets/RPG_FPS_game_assets_industrial/Textures/Concrete_wall/UNIConcrete_walls/";
            _concrete = material("Concrete", new Color(0.93f, 0.91f, 0.87f));
            texture(_concrete, concrete + "UNIConcrete_wall_v1/UNIConcrete_wall_v1.tga", 0.3f);
            _concreteDark = material("Concrete Dark", new Color(0.62f, 0.62f, 0.64f));
            texture(_concreteDark, concrete + "UNIConcrete_wall_v3/UNIConcrete_wall_v3.tga", 0.3f);
            _floor = material("Floor", new Color(0.78f, 0.76f, 0.72f));
            texture(_floor, concrete + "UNIConcrete_wall_v2/UNIConcrete_wall_v2.tga", 0.35f);
            _metal = material("Metal", new Color(0.24f, 0.26f, 0.29f), 0.7f, 0.45f);
            _rust = material("Rusted Metal", new Color(0.42f, 0.24f, 0.15f), 0.5f, 0.25f);
            _sandbag = material("Sandbag", new Color(0.56f, 0.49f, 0.34f));
            _wood = material("Wood", new Color(0.43f, 0.3f, 0.18f));
            _fabric = material("Fabric", new Color(0.3f, 0.34f, 0.24f));
            _body = material("Body", new Color(0.17f, 0.19f, 0.15f));
            _mastRed = material("Mast Red", new Color(0.62f, 0.12f, 0.08f), 0.3f, 0.3f);
            _glowRed = material("Glow Red", new Color(1f, 0.2f, 0.1f), 0, 0.5f, new Color(4f, 0.3f, 0.15f));
            _glowGreen = material("Glow Green", new Color(0.3f, 1f, 0.4f), 0, 0.5f, new Color(0.4f, 2.5f, 0.6f));
            _guide = material("Guide Yellow", new Color(1f, 0.78f, 0.1f), 0, 0.3f, new Color(2.2f, 1.5f, 0.15f));
            AssetDatabase.SaveAssets();
        }

        private static void texture(Material m, string path, float tiling)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogWarning("[Vantage] Texture not found: " + path);
                return;
            }
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", Vector2.one * tiling);
            EditorUtility.SetDirty(m);
        }

        private static Material material(string name, Color color, float metallic = 0f, float smoothness = 0.15f, Color? emission = null)
        {
            var path = "Assets/Vantage/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }

            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);

            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            EditorUtility.SetDirty(m);
            return m;
        }

        #endregion
    }
}
