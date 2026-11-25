using System.Collections.Generic;
using CoverShooter;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vantage
{
    /// <summary>
    /// Keeps the third-person view clear without ever seeing through the level (no see-through materials since
    /// 2026-11-25: the user didn't want them, and the scene's size limit of 60 m had faded whole walls and floors).
    /// After the template camera has moved (late execution order), a sphere cast from the character's head to the
    /// camera pulls the camera in front of anything solid. Only thin railings let it pass (their bars don't hide the
    /// player). The cast's radius shrinks until it starts clear: the head sits on the body's axis, as far from a wall as
    /// the body is wide, so a full-width sphere started inside the wall the player leaned on, Unity reports that as a
    /// hit at distance 0, and skipping those let the camera through exactly those walls. Back faces count (single-sided
    /// walls); the near plane follows the room left around the camera; pressed against the head, the player's body is
    /// hidden (its shadow stays) instead of clipped. The name is kept for the scene's component.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(1000)]
    public class VantageCameraFader : MonoBehaviour
    {
        [Tooltip("The camera stays at least this far from walls.")]
        public float Clearance = 0.3f;
        [Tooltip("The near plane never goes below this when the camera is squeezed against a wall.")]
        public float MinNearPlane = 0.02f;
        [Tooltip("Closer than this to the head, the player's body is hidden (its shadow stays) instead of clipped.")]
        public float HideWithin = 0.45f;

        private readonly RaycastHit[] _hits = new RaycastHit[32];
        private readonly Collider[] _overlaps = new Collider[16];
        private readonly Dictionary<Collider, bool> _blocks = new Dictionary<Collider, bool>();
        private readonly List<Renderer> _playerRenderers = new List<Renderer>();
        private readonly List<ShadowCastingMode> _playerModes = new List<ShadowCastingMode>();
        private Camera _camera;
        private float _baseNear = 0.1f;
        private Transform _playerRoot;
        private BaseActor _hiddenFor;
        private bool _hidden;
        private static int Mask => VantagePhysics.Solid;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _baseNear = _camera.nearClipPlane;
        }

        private void LateUpdate()
        {
            var player = VantageEvents.ActivePlayer();
            if (player == null) return;
            _playerRoot = player.transform;
            keepClear(player, VantagePhysics.Eye(player));
        }

        /// <summary>Pulls the camera in front of the nearest obstacle between the head and the camera.</summary>
        private void keepClear(BaseActor player, Vector3 head)
        {
            var toCamera = transform.position - head;
            var distance = toCamera.magnitude;
            if (distance < 0.01f)
                return;
            var dir = toCamera / distance;
            var backfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            var radius = clearRadius(head);
            var count = Physics.SphereCastNonAlloc(head, radius, dir, _hits, distance, Mask, QueryTriggerInteraction.Ignore);
            var nearest = distance;
            for (int i = 0; i < count; i++)
            {
                var h = _hits[i];
                if (!blocks(h.collider)) continue;
                nearest = Mathf.Min(nearest, Mathf.Max(0f, h.distance));
            }
            Physics.queriesHitBackfaces = backfaces;
            if (nearest < distance)
                transform.position = head + dir * nearest;

            // The near plane's corners must stay inside the clear sphere (about 1.6 × the near distance at 60°).
            _camera.nearClipPlane = Mathf.Clamp(radius / 1.6f, MinNearPlane, _baseNear);
            hidePlayer(player, nearest < HideWithin);
        }

        /// <summary>The widest sphere up to Clearance that doesn't start inside a blocking collider.</summary>
        private float clearRadius(Vector3 head)
        {
            var radius = Clearance;
            for (int step = 0; step < 4; step++, radius *= 0.6f)
            {
                var n = Physics.OverlapSphereNonAlloc(head, radius, _overlaps, Mask, QueryTriggerInteraction.Ignore);
                var clear = true;
                for (int i = 0; i < n && clear; i++)
                    if (blocks(_overlaps[i])) clear = false;
                if (clear) return radius;
            }
            return Mathf.Max(0.03f, radius);
        }

        /// <summary>Everything solid blocks, except the player and railings (named *Rail*, the model's and ours).</summary>
        private bool blocks(Collider c)
        {
            if (c == null || (_playerRoot != null && c.transform.IsChildOf(_playerRoot))) return false;
            if (_blocks.TryGetValue(c, out var b)) return b;
            b = true;
            for (var t = c.transform; t != null && b; t = t.parent)
                if (t.name.Contains("Rail")) b = false;
            _blocks[c] = b;
            return b;
        }

        /// <summary>With the camera pressed against the head, hide the body (shadow kept) instead of clipping into it.</summary>
        private void hidePlayer(BaseActor player, bool hide)
        {
            if (_hiddenFor != player)
            {
                showPlayer();
                _hiddenFor = player;
                _playerRenderers.Clear();
                _playerModes.Clear();
                foreach (var r in player.GetComponentsInChildren<Renderer>(true))
                    if (!(r is ParticleSystemRenderer)) { _playerRenderers.Add(r); _playerModes.Add(r.shadowCastingMode); }
                _hidden = false;
            }
            if (hide == _hidden) return;
            _hidden = hide;
            for (int i = 0; i < _playerRenderers.Count; i++)
                if (_playerRenderers[i] != null)
                    _playerRenderers[i].shadowCastingMode = hide ? ShadowCastingMode.ShadowsOnly : _playerModes[i];
        }

        private void showPlayer()
        {
            if (!_hidden) return;
            for (int i = 0; i < _playerRenderers.Count; i++)
                if (_playerRenderers[i] != null)
                    _playerRenderers[i].shadowCastingMode = _playerModes[i];
            _hidden = false;
        }

        private void OnDisable()
        {
            showPlayer();
            if (_camera != null) _camera.nearClipPlane = _baseNear;
        }
    }
}
