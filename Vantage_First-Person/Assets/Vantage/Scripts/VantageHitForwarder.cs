using CoverShooter;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// Passes bullet hits on a child collider to the object that takes the damage. Weapons send OnHit to the
    /// collider they hit; a model with its own colliders (e.g. the user's Blender robot under "Model") would
    /// otherwise swallow every hit. Added at runtime by VantageHeavyRobot.
    /// </summary>
    public class VantageHitForwarder : MonoBehaviour
    {
        public GameObject Target;

        public void OnHit(Hit hit)
        {
            if (Target != null) Target.SendMessage("OnHit", hit, SendMessageOptions.DontRequireReceiver);
        }
    }
}
