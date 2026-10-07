using UnityEngine;

namespace CommandTowerKit
{
    /// Gameplay marker (enemy spawn, patrol point, drone point, entry, objective...).
    public class GameplayMarker : MonoBehaviour
    {
        public string markerType;   // EnemySpawn, Patrol, DroneSpawn, DronePatrol, PlayerEntry, Objective, Cover, SecurityCamera, Overwatch
        public string room;
        public string route;
        public int order = -1;
        public int floor = -1;
        [TextArea] public string note;

        Color GizmoColor()
        {
            switch (markerType)
            {
                case "EnemySpawn": return new Color(1f, 0.2f, 0.2f);
                case "Patrol": return new Color(1f, 0.6f, 0.1f);
                case "DroneSpawn": return new Color(0.2f, 0.8f, 1f);
                case "DronePatrol": return new Color(0.2f, 0.5f, 1f);
                case "PlayerEntry": return new Color(0.2f, 1f, 0.3f);
                case "Objective": return new Color(1f, 0.9f, 0.1f);
                case "Cover": return new Color(0.7f, 0.7f, 0.7f);
                case "SecurityCamera": return new Color(0.8f, 0.3f, 1f);
                default: return Color.white;
            }
        }
        void OnDrawGizmos()
        {
            Gizmos.color = GizmoColor();
            var p = transform.position;
            if (markerType == "DroneSpawn" || markerType == "DronePatrol") Gizmos.DrawWireSphere(p, 0.5f);
            else if (markerType == "Objective") Gizmos.DrawWireCube(p + Vector3.up * 0.5f, Vector3.one);
            else { Gizmos.DrawSphere(p + Vector3.up * 0.1f, 0.18f); Gizmos.DrawLine(p + Vector3.up * 0.1f, p + Vector3.up * 0.1f + transform.forward * 0.8f); }
            if (!string.IsNullOrEmpty(route) && transform.parent != null)
                foreach (Transform t in transform.parent)
                {
                    var m = t.GetComponent<GameplayMarker>();
                    if (m != null && m.route == route && m.order == order + 1) { Gizmos.DrawLine(p, t.position); break; }
                }
        }
    }
}
