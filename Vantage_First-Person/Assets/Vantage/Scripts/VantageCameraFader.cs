using System.Collections.Generic;
using CoverShooter;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vantage
{
    /// <summary>
    /// Keeps the third-person view clear without seeing through the level:
    /// - Clearance: after the template camera has moved (late execution order), a sphere cast from the character's
    ///   head to the camera pulls the camera in front of any wall, with a radius wider than the near plane, so the
    ///   view never clips into geometry. (The template's own check uses 0.1 m rays, the same as the near plane.)
    /// - Fading: only small objects (props, furniture) between camera and character turn see-through. Large meshes
    ///   are never faded: the command tower's walls, floors and stairs are one mesh per storey, and fading them
    ///   made whole floors transparent.
    /// The template's own fader only lowers colour alpha, which does nothing on opaque URP materials; this swaps
    /// in transparent copies of the materials instead.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(1000)]
    public class VantageCameraFader : MonoBehaviour
    {
        [Range(0, 1)] public float FadedAlpha = 0.25f;
        public float FadeSpeed = 6f;
        public float ProbeRadius = 0.2f;
        [Tooltip("Only objects smaller than this are faded; bigger ones block the camera instead.")]
        public float MaxObjectSize = 5f;
        [Tooltip("The camera stays at least this far from walls.")]
        public float Clearance = 0.3f;

        private class Faded
        {
            public Renderer Renderer;
            public Material[] Original;
            public Material[] Transparent;
            public float Alpha = 1f;
            public bool Wanted;
        }

        private readonly Dictionary<Renderer, Faded> _faded = new Dictionary<Renderer, Faded>();
        private readonly RaycastHit[] _hits = new RaycastHit[32];
        private readonly List<Renderer> _remove = new List<Renderer>();
        private readonly Dictionary<Collider, Renderer[]> _renderersOf = new Dictionary<Collider, Renderer[]>();
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static int Mask => VantagePhysics.Solid;

        private void LateUpdate()
        {
            foreach (var f in _faded.Values)
                f.Wanted = false;

            var player = VantageEvents.ActivePlayer();
            if (player != null)
            {
                var head = VantagePhysics.Eye(player);
                keepClear(head);
                fadeBetween(player, head);
            }
            updateFades();
        }

        /// <summary>Pulls the camera in front of the nearest large obstacle between the head and the camera.</summary>
        private void keepClear(Vector3 head)
        {
            var toCamera = transform.position - head;
            var distance = toCamera.magnitude;
            if (distance < 0.01f)
                return;
            var dir = toCamera / distance;
            var count = Physics.SphereCastNonAlloc(head, Clearance, dir, _hits, distance, Mask, QueryTriggerInteraction.Ignore);
            var nearest = distance;
            for (int i = 0; i < count; i++)
            {
                var h = _hits[i];
                if (h.distance <= 0f || isSmall(h.collider)) continue; // small props fade instead
                nearest = Mathf.Min(nearest, h.distance);
            }
            if (nearest < distance)
                transform.position = head + dir * Mathf.Max(0.2f, nearest);
        }

        private void fadeBetween(BaseActor player, Vector3 head)
        {
            var vector = head - transform.position;
            var count = Physics.SphereCastNonAlloc(transform.position, ProbeRadius, vector.normalized, _hits, Mathf.Max(0f, vector.magnitude - 0.3f), Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i].collider;
                if (hit.transform.IsChildOf(player.transform) || !isSmall(hit))
                    continue;
                if (!_renderersOf.TryGetValue(hit, out var renderers))
                    _renderersOf[hit] = renderers = hit.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    if (r is ParticleSystemRenderer || r.bounds.size.magnitude > MaxObjectSize)
                        continue;
                    // Never fade what the character stands on or inside of.
                    if (r.bounds.Contains(player.transform.position + Vector3.up * 0.1f))
                        continue;
                    if (!_faded.TryGetValue(r, out var f))
                        f = _faded[r] = begin(r);
                    f.Wanted = true;
                }
            }
        }

        private bool isSmall(Collider c) => c.bounds.size.magnitude <= MaxObjectSize;

        private void updateFades()
        {
            _remove.Clear();
            foreach (var f in _faded.Values)
            {
                f.Alpha = Mathf.MoveTowards(f.Alpha, f.Wanted ? FadedAlpha : 1f, Time.deltaTime * FadeSpeed);
                foreach (var m in f.Transparent)
                {
                    if (m == null || !m.HasProperty(BaseColor))
                        continue;
                    var c = m.GetColor(BaseColor);
                    c.a = f.Alpha;
                    m.SetColor(BaseColor, c);
                }

                if (!f.Wanted && f.Alpha >= 1f)
                    _remove.Add(f.Renderer);
            }

            foreach (var r in _remove)
            {
                var f = _faded[r];
                if (r != null)
                    r.sharedMaterials = f.Original;
                foreach (var m in f.Transparent)
                    Destroy(m);
                _faded.Remove(r);
            }
        }

        private static Faded begin(Renderer r)
        {
            var original = r.sharedMaterials;
            var transparent = new Material[original.Length];
            for (int i = 0; i < original.Length; i++)
            {
                if (original[i] == null)
                    continue;
                var m = new Material(original[i]);
                makeTransparent(m);
                transparent[i] = m;
            }

            r.sharedMaterials = transparent;
            return new Faded { Renderer = r, Original = original, Transparent = transparent };
        }

        /// <summary>URP Lit / Simple Lit / Unlit: switch the material copy to alpha-blended transparency.</summary>
        private static void makeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        private void OnDisable()
        {
            foreach (var f in _faded.Values)
            {
                if (f.Renderer != null)
                    f.Renderer.sharedMaterials = f.Original;
                foreach (var m in f.Transparent)
                    Destroy(m);
            }
            _faded.Clear();
        }
    }
}
