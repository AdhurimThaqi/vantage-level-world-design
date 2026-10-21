using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// Spawns the two sentries that walk the outside of the tower's top (the broken slab, the helipad platform) at
    /// scene start, whatever level the player is on: they are seen from the approach and enforce the warning ring
    /// (VantageBoundary). They are not level enemies: the level brain, the KILL lists and the HUD pips don't count
    /// them. Their routes are traced from the baked NavMesh by Setup (VantageSentryRoutes). Sentries killed earlier
    /// in the run (VantageRun.DeadSentries) are not spawned again after a death.
    /// </summary>
    public class VantageSentries : MonoBehaviour
    {
        [Serializable]
        public class Route
        {
            public string Name;
            [Tooltip("The sentry fights normally while the player is on this level (index into VantageTowerLevels.Levels).")]
            public int Level;
            public List<Vector3> Points = new List<Vector3>();
        }

        public GameObject SoldierPrefab;
        [Tooltip("Material for the ring shots' tracer and dust (Setup takes the drones' tracer material).")]
        public Material TracerMaterial;
        public List<Route> Routes = new List<Route>();

        private static readonly List<VantageSentry> _all = new List<VantageSentry>();
        /// <summary>The sentries of this scene, dead ones included.</summary>
        public static IReadOnlyList<VantageSentry> All => _all;

        private void Start()
        {
            _all.Clear();
            if (SoldierPrefab == null) return;
            foreach (var route in Routes)
            {
                var name = "SENTRY_" + route.Name;
                if (route.Points.Count < 2 || VantageRun.DeadSentries.Contains(name)) continue;
                var ahead = route.Points[1] - route.Points[0]; ahead.y = 0;
                var go = Instantiate(SoldierPrefab, route.Points[0], ahead.sqrMagnitude > 0.01f ? Quaternion.LookRotation(ahead) : Quaternion.identity, transform);
                go.name = name;
                // Not a level soldier: no hunting, no level rules. The sentry brain decides instead.
                var aggression = go.GetComponent<VantageEnemyAggression>();
                if (aggression != null) DestroyImmediate(aggression);
                var reporter = go.GetComponent<VantageKillReporter>();
                if (reporter != null) reporter.EnemyName = name;
                var sentry = go.AddComponent<VantageSentry>();
                sentry.Init(route, TracerMaterial);
                _all.Add(sentry);
            }
        }

        private void OnDestroy() => _all.Clear();

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f);
            foreach (var route in Routes)
                for (int i = 0; i < route.Points.Count; i++)
                {
                    Gizmos.DrawWireSphere(route.Points[i], 0.4f);
                    if (i > 0) Gizmos.DrawLine(route.Points[i - 1], route.Points[i]);
                }
        }
    }
}
