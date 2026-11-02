using System.Collections.Generic;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// Aircraft-warning lights on top of the tower and the helipad (built by VantageDusk): a slow red blink, all in
    /// step. One component for the whole group: the glows share a property block, the few real lights fade with them.
    /// </summary>
    public class VantageBlinkLights : MonoBehaviour
    {
        public float Period = 1.6f;
        [Range(0f, 1f)] public float OnFraction = 0.35f;
        public Color Glow = new Color(1f, 0.08f, 0.04f);
        public float GlowIntensity = 6f;
        public List<Renderer> Glows = new List<Renderer>();
        public List<Light> Lights = new List<Light>();

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock _block;
        private float[] _intensity;
        private bool _on = true;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _intensity = new float[Lights.Count];
            for (int i = 0; i < Lights.Count; i++) _intensity[i] = Lights[i] != null ? Lights[i].intensity : 0f;
        }

        private void Update()
        {
            var on = Mathf.Repeat(Time.time, Period) < Period * OnFraction;
            if (on == _on) return;
            _on = on;
            _block.SetColor(EmissionId, on ? Glow * GlowIntensity : Glow * 0.15f);
            foreach (var r in Glows)
                if (r != null) r.SetPropertyBlock(_block);
            for (int i = 0; i < Lights.Count; i++)
                if (Lights[i] != null) Lights[i].intensity = on ? _intensity[i] : 0f;
        }
    }
}
