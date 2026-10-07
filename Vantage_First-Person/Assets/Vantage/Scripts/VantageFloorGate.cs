using UnityEngine;
using UnityEngine.AI;

namespace Vantage
{
    /// <summary>
    /// A locked security shutter on a stair flight (inner stairs or fire escape). While locked, an invisible box over
    /// the whole flight blocks the player and a carving NavMeshObstacle keeps soldiers on their level; the shutter
    /// panel at the bottom glows red. Open() lifts the panel, turns it green and clears both.
    /// Placed by Vantage → Tower → Set Up Command Tower; opened by VantageTowerLevels when a level is cleared.
    /// </summary>
    public class VantageFloorGate : MonoBehaviour
    {
        [Tooltip("Index of the level that must be cleared to open this gate (0 = level 1).")]
        public int Level;
        public BoxCollider Blocker;
        public NavMeshObstacle Obstacle;
        public Transform Panel;
        public Renderer PanelRenderer;
        public Light Lamp;
        public Color Locked = new Color(1f, 0.15f, 0.08f);
        public Color Unlocked = new Color(0.2f, 1f, 0.35f);

        public bool IsOpen { get; private set; }

        private float _openedAt = -1f;
        private Vector3 _panelStart;
        private MaterialPropertyBlock _block;

        private void Start()
        {
            if (Panel != null) _panelStart = Panel.localPosition;
            tint(Locked);
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            _openedAt = Time.time;
            if (Blocker != null) Blocker.enabled = false;
            if (Obstacle != null) Obstacle.enabled = false;
            tint(Unlocked);
        }

        private void Update()
        {
            if (!IsOpen || Panel == null) return;
            // The shutter rolls up over a second.
            var t = Mathf.Clamp01((Time.time - _openedAt) / 1.2f);
            Panel.localPosition = _panelStart + Vector3.up * Mathf.SmoothStep(0, 2.4f, t);
        }

        private void tint(Color c)
        {
            if (Lamp != null) Lamp.color = c;
            if (PanelRenderer == null) return;
            _block ??= new MaterialPropertyBlock();
            PanelRenderer.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", new Color(c.r * 0.25f, c.g * 0.25f, c.b * 0.25f, 1f));
            _block.SetColor("_EmissionColor", c * 0.9f);
            PanelRenderer.SetPropertyBlock(_block);
        }
    }
}
