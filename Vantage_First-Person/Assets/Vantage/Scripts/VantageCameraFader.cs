using System.Collections.Generic;
using CoverShooter;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vantage
{
    /// <summary>
    /// Makes walls and props between the third-person camera and the character see-through, then restores them.
    /// The template's own fader only lowers the colour alpha, which does nothing on opaque URP materials;
    /// this swaps in transparent copies of the materials instead.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class VantageCameraFader : MonoBehaviour
    {
        [Range(0, 1)] public float FadedAlpha = 0.22f;
        public float FadeSpeed = 6f;
        public float ProbeRadius = 0.25f;
        [Tooltip("Objects larger than this (e.g. the ground) are never faded.")]
        public float MaxObjectSize = 60f;

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
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private void LateUpdate()
        {
            foreach (var f in _faded.Values)
                f.Wanted = false;

            var player = VantageEvents.ActivePlayer();
            if (player != null)
            {
                var target = player.Collider != null ? player.Collider.bounds.center + Vector3.up * 0.5f : player.transform.position + Vector3.up * 1.4f;
                var vector = target - transform.position;
                var mask = ~((1 << 2) | (1 << VantageCoverUtil.CoverLayer) | (1 << 11));
                var count = Physics.SphereCastNonAlloc(transform.position, ProbeRadius, vector.normalized, _hits, vector.magnitude - 0.3f, mask, QueryTriggerInteraction.Ignore);

                for (int i = 0; i < count; i++)
                {
                    var hit = _hits[i].collider;
                    if (hit.transform.IsChildOf(player.transform))
                        continue;

                    foreach (var r in hit.GetComponentsInChildren<Renderer>())
                    {
                        if (r is ParticleSystemRenderer || r.bounds.size.magnitude > MaxObjectSize)
                            continue;
                        if (!_faded.TryGetValue(r, out var f))
                            f = _faded[r] = begin(r);
                        f.Wanted = true;
                    }
                }
            }

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

        /// <summary>
        /// URP Lit / Simple Lit / Unlit: switch the material copy to alpha-blended transparency.
        /// </summary>
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
