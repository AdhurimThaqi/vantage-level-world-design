using CoverShooter;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// Shared ray masks and body points, so no script repeats layer numbers or assumes a character's height.
    /// The layers come from CoverShooter.Layers (8 Cover, 10 Character, 11 Zones are hard-coded in the template).
    /// </summary>
    public static class VantagePhysics
    {
        public const int IgnoreRaycastLayer = 2;

        /// <summary>Level geometry and props: everything except characters, cover markers and zones.</summary>
        public static readonly int Solid = ~((1 << IgnoreRaycastLayer) | Layers.Cover | Layers.Character | Layers.Zones);

        /// <summary>What a line of sight can hit: geometry and characters (hitting the target means seeing it).</summary>
        public static readonly int Sight = ~((1 << IgnoreRaycastLayer) | Layers.Cover | Layers.Zones);

        /// <summary>Head of a character, from the top of its collider (follows crouching and any body height).</summary>
        public static Vector3 Eye(Component character)
        {
            var collider = character is BaseActor actor && actor.Collider != null ? actor.Collider : character.GetComponent<Collider>();
            var position = character.transform.position;
            if (collider == null)
                return position + Vector3.up * 1.6f;
            position.y = collider.bounds.max.y - 0.15f;
            return position;
        }

        /// <summary>Where shots aim: upper chest, a little above the collider's centre.</summary>
        public static Vector3 AimPoint(BaseActor actor)
        {
            var collider = actor.Collider;
            if (collider == null)
                return actor.transform.position + Vector3.up * 1.4f;
            var bounds = collider.bounds;
            return bounds.center + Vector3.up * bounds.extents.y * 0.4f;
        }
    }
}
