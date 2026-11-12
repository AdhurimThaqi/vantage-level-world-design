using CoverShooter;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// Footsteps by surface, on the player and the soldiers (on their prefabs: the motor collects its listeners in
    /// Awake). The template calls OnFootstep on every step; a ray down finds what is underfoot: terrain → FS_Dirt, a
    /// material named like metal (Metal, Steel, Grate, Rail, Shutter) → FS_Metal, anything else → FS_Concrete.
    /// Invisible colliders (the smooth stair surfaces) are looked through to the real steps below. The template's own
    /// footstep clips on this character are cleared, so steps don't play twice.
    /// </summary>
    public class VantageFootsteps : MonoBehaviour, ICharacterPhysicsListener
    {
        private static readonly string[] MetalWords = { "metal", "steel", "grate", "rail", "shutter" };
        private static readonly RaycastHit[] _hits = new RaycastHit[8];

        private void Awake()
        {
            var sounds = GetComponent<CharacterSounds>();
            if (sounds != null) sounds.Footstep = new AudioClip[0];
        }

        public void OnFootstep(Vector3 position)
        {
            var surface = SurfaceAt(position + Vector3.up * 0.3f);
            // Until metal steps exist, metal floors use the concrete ones (not silence).
            if (surface == VantageSounds.FsMetal && !VantageAudio.Has(VantageSounds.FsMetal)) surface = VantageSounds.FsConcrete;
            VantageAudio.Play(surface, position);
        }
        public void OnLand() { }
        public void OnJump() { }

        /// <summary>The footstep event for the ground under a point (searched 1.5 m down).</summary>
        public static string SurfaceAt(Vector3 foot)
        {
            var n = Physics.RaycastNonAlloc(foot, Vector3.down, _hits, 1.5f, VantagePhysics.Solid, QueryTriggerInteraction.Ignore);
            var best = float.MaxValue;
            string surface = VantageSounds.FsConcrete;
            for (int i = 0; i < n; i++)
            {
                var hit = _hits[i];
                if (hit.distance >= best) continue;
                if (hit.collider is TerrainCollider) { best = hit.distance; surface = VantageSounds.FsDirt; continue; }
                var r = hit.collider.GetComponent<Renderer>();
                if (r == null || !r.enabled || r.sharedMaterial == null) continue;   // invisible surfaces: look through
                best = hit.distance;
                surface = IsMetal(r.sharedMaterial.name) ? VantageSounds.FsMetal : VantageSounds.FsConcrete;
            }
            return surface;
        }

        public static bool IsMetal(string materialName)
        {
            foreach (var w in MetalWords)
                if (materialName.IndexOf(w, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
    }
}
