using UnityEngine;

namespace CommandTowerKit
{
    /// Simple hinged door. Call Toggle()/Open()/Close() from interaction or AI code.
    public class TowerDoor : MonoBehaviour
    {
        public Quaternion closedRotation;
        public Quaternion openRotation;
        public bool isOpen = true;
        public float speed = 4f;
        public bool locked = false;

        public void Open()   { if (!locked) isOpen = true; }
        public void Close()  { isOpen = false; }
        public void Toggle() { if (!locked) isOpen = !isOpen; }

        void Update()
        {
            var target = isOpen ? openRotation : closedRotation;
            if (Quaternion.Angle(transform.localRotation, target) > 0.1f)
                transform.localRotation = Quaternion.Slerp(transform.localRotation, target, Time.deltaTime * speed);
        }
    }
}
