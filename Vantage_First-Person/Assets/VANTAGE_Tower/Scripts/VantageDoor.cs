using System.Collections.Generic;
using UnityEngine;

namespace Vantage
{
    public enum DoorType { Auto, Locked, OneWay, Shutter }

    /// Slide-up military door. Auto doors open when the player is close,
    /// locked doors open after Unlock(), one-way doors only open from one side,
    /// the shutter starts open, closes fast on the alarm and reopens when its kill list is cleared.
    public class VantageDoor : MonoBehaviour
    {
        static readonly Dictionary<string, VantageDoor> registry = new Dictionary<string, VantageDoor>();
        public static VantageDoor Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            registry.TryGetValue(name.Trim(), out var d);
            return d;
        }

        public DoorType type = DoorType.Auto;
        public bool locked;
        [Tooltip("Door name/interaction that unlocks it, or KILL:name1,name2,prefix*")]
        public string unlockBy;
        [Tooltip("One-way doors only open when the player is on the same side as this object")]
        public string oneWaySideRefName;
        public float openRadius = 2.6f;
        public float openSpeed = 4f;

        [Header("Shutter")]
        public float shutterDrop = 2.75f;          // open_z - closed_z from Blender
        public float shutterClosedHeight = 2.9f;
        public float shutterCloseTime = 0.6f;
        public float shutterOpenTime = 1.5f;

        Vector3 closedPos, openPos;
        Transform player, oneWayRef;
        Vector3 doorNormal = Vector3.forward;
        bool openedFromAllowedSide;

        // shutter state: 1 = open (rolled up), 0 = closed
        float shutterT = 1f, shutterTarget = 1f;
        Vector3 shutterOpenPos, shutterOpenScale, shutterClosedScale;
        List<string> killList;

        void Awake() { registry[name] = this; }
        void OnDestroy() { if (registry.TryGetValue(name, out var d) && d == this) registry.Remove(name); }

        void Start()
        {
            closedPos = transform.position;
            var r = GetComponentInChildren<Renderer>();
            Bounds b = r ? r.bounds : new Bounds(transform.position, new Vector3(1.2f, 2.3f, 0.12f));
            openPos = closedPos + Vector3.up * (b.size.y + 0.05f);
            doorNormal = b.size.x < b.size.z ? Vector3.right : Vector3.forward;

            if (type == DoorType.Shutter)
            {
                shutterOpenPos = transform.position;
                shutterOpenScale = transform.localScale;
                shutterClosedScale = shutterOpenScale;
                // the local axis that points up in the world gets stretched to close the shutter
                Vector3 up = transform.InverseTransformDirection(Vector3.up);
                int axis = 0;
                float best = Mathf.Abs(up.x);
                if (Mathf.Abs(up.y) > best) { axis = 1; best = Mathf.Abs(up.y); }
                if (Mathf.Abs(up.z) > best) axis = 2;
                float k = shutterClosedHeight / Mathf.Max(0.01f, b.size.y);
                shutterClosedScale[axis] = shutterOpenScale[axis] * k;
            }

            if (!string.IsNullOrEmpty(unlockBy) && unlockBy.StartsWith("KILL:"))
            {
                killList = new List<string>(unlockBy.Substring(5).Split(','));
                InvokeRepeating(nameof(CheckKills), 1f, 0.5f);
            }
            if (!string.IsNullOrEmpty(oneWaySideRefName))
            {
                var g = GameObject.Find(oneWaySideRefName);
                if (g) oneWayRef = g.transform;
            }
        }

        public void Unlock()
        {
            locked = false;
            if (type == DoorType.Shutter) OpenShutter();
        }

        public void CloseShutter() { shutterTarget = 0f; }
        public void OpenShutter() { shutterTarget = 1f; }
        public bool ShutterClosed => shutterT <= 0.001f;

        void CheckKills()
        {
            if (killList == null) return;
            if (type == DoorType.Shutter && !ShutterClosed) return;   // only reopen after it has closed
            if (!locked && type != DoorType.Shutter) { CancelInvoke(nameof(CheckKills)); return; }
            var mgr = VantageLevelManager.Instance;
            foreach (var k in killList)
                if (mgr != null && mgr.IsAlive(k.Trim())) return;
            Unlock();
            CancelInvoke(nameof(CheckKills));
        }

        void Update()
        {
            if (type == DoorType.Shutter) { UpdateShutter(); return; }

            if (player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (!p) return;
                player = p.transform;
            }

            bool want = false;
            if (!locked)
            {
                Vector3 d = player.position - closedPos;
                float flat = new Vector2(d.x, d.z).magnitude;
                want = flat < openRadius && Mathf.Abs(d.y) < 2.5f;
                if (want && type == DoorType.OneWay && oneWayRef && !openedFromAllowedSide)
                {
                    Vector3 toRef = oneWayRef.position - closedPos;
                    bool sameSide = Mathf.Sign(Vector3.Dot(d, doorNormal)) == Mathf.Sign(Vector3.Dot(toRef, doorNormal));
                    want = sameSide;
                    if (sameSide) openedFromAllowedSide = true;
                }
                if (!want) openedFromAllowedSide = false;
            }
            transform.position = Vector3.MoveTowards(transform.position, want ? openPos : closedPos, openSpeed * Time.deltaTime);
        }

        void UpdateShutter()
        {
            float dur = shutterTarget < shutterT ? shutterCloseTime : shutterOpenTime;
            shutterT = Mathf.MoveTowards(shutterT, shutterTarget, Time.deltaTime / Mathf.Max(0.01f, dur));
            transform.position = Vector3.Lerp(shutterOpenPos + Vector3.down * shutterDrop, shutterOpenPos, shutterT);
            transform.localScale = Vector3.Lerp(shutterClosedScale, shutterOpenScale, shutterT);
        }
    }
}
