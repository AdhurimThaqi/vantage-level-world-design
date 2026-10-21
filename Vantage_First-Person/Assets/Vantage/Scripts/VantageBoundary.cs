using System;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// The warning ring around the tower: the reason the player stays at the tower instead of walking off into the
    /// base. Measured from the tower plot (the tower's footprint + 8 m, set by Setup) as the distance outside it:
    /// free near the tower, a warning band where the sentries above fire warning shots, then a kill zone (also
    /// reached by standing in the warning band longer than the grace time). Inside the tower the ring is ignored.
    /// The HUD, the minimap and the sentries read the player's zone from here.
    /// </summary>
    public class VantageBoundary : MonoBehaviour
    {
        public enum Zone { Inside, Free, Warning, Kill }

        public static VantageBoundary Instance { get; private set; }

        [Tooltip("The tower plot on the ground (x, z), set by Setup: the tower's footprint plus 8 m.")]
        public Rect Plot;
        [Tooltip("Free up to this far past the plot.")]
        public float FreeDistance = 15f;
        [Tooltip("Warning band up to this far past the plot; beyond it the sentries shoot to hit.")]
        public float WarningDistance = 35f;
        [Tooltip("Seconds in the warning band before it counts as the kill zone (reset by going back to the free zone).")]
        public float GraceSeconds = 5f;

        /// <summary>The player's zone with the grace rule applied; Free while the ring is not enforced.</summary>
        public Zone PlayerZone { get; private set; } = Zone.Free;
        /// <summary>False during a cinematic or while the player is dead.</summary>
        public bool Enforcing { get; private set; }
        public event Action<Zone> PlayerZoneChanged;

        private const float CheckInterval = 0.2f;
        private float _nextCheck, _warningSince = -1f;
        private bool _graceUsed;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Distance outside the plot on the ground (0 inside it).</summary>
        public float OutsidePlot(Vector3 world)
        {
            var dx = Mathf.Max(Plot.xMin - world.x, 0f, world.x - Plot.xMax);
            var dz = Mathf.Max(Plot.yMin - world.z, 0f, world.z - Plot.yMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>The zone of a position from the geometry alone (no grace time).</summary>
        public Zone ZoneAt(Vector3 world)
        {
            var levels = VantageTowerLevels.Instance;
            if (levels != null && levels.LevelAt(world) >= 0)
                return Zone.Inside;
            var outside = OutsidePlot(world);
            return outside <= FreeDistance ? Zone.Free : outside <= WarningDistance ? Zone.Warning : Zone.Kill;
        }

        private void Update()
        {
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + CheckInterval;

            var player = VantageEvents.ActivePlayer();
            Enforcing = player != null && player.IsAlive && !VantageCinematic.Active;
            var zone = Enforcing ? ZoneAt(player.transform.position) : Zone.Free;

            // Grace: continuous time in the warning band. Only the free zone (or the tower) resets it.
            if (zone == Zone.Warning)
            {
                if (_warningSince < 0f && !_graceUsed) _warningSince = Time.time;
                if (_graceUsed || Time.time - _warningSince >= GraceSeconds) { _graceUsed = true; zone = Zone.Kill; }
            }
            else if (zone == Zone.Free || zone == Zone.Inside)
            {
                _warningSince = -1f;
                _graceUsed = false;
            }

            if (zone == PlayerZone) return;
            PlayerZone = zone;
            PlayerZoneChanged?.Invoke(zone);
        }

        private void OnDrawGizmosSelected()
        {
            var y = transform.position.y;
            drawRect(Plot, 0f, y, Color.white);
            drawRect(Plot, FreeDistance, y, Color.yellow);
            drawRect(Plot, WarningDistance, y, Color.red);
        }

        private static void drawRect(Rect r, float grow, float y, Color colour)
        {
            Gizmos.color = colour;
            var a = new Vector3(r.xMin - grow, y, r.yMin - grow);
            var b = new Vector3(r.xMax + grow, y, r.yMin - grow);
            var c = new Vector3(r.xMax + grow, y, r.yMax + grow);
            var d = new Vector3(r.xMin - grow, y, r.yMax + grow);
            Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
        }
    }
}
