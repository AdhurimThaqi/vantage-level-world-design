using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Vantage
{
    public enum DoorType { Auto, Locked, OneWay, Shutter }

    /// Slide-up military door. Auto doors open when the player is close,
    /// locked doors open after Unlock(), one-way doors only open from one side,
    /// the shutter starts open, closes fast on the alarm and reopens when its kill list is cleared.
    // VANTAGE change: doors open for any living character (soldiers path through them too), checked a few times a
    // second instead of looking the player up every frame; KILL lists are re-checked on kills (RecheckKills) instead
    // of polling; Unlock() can wait for a level to be cleared (ClearLevel); ForceOpen() for checkpoint restores;
    // the carving NavMeshObstacle (added by Vantage Setup) follows the locked/closed state; ordinary doors are open
    // doorways (Doorway, set by Vantage Setup): open from the start, or once unlocked, with the leaf hidden.
    public class VantageDoor : MonoBehaviour
    {
        static readonly Dictionary<string, VantageDoor> registry = new Dictionary<string, VantageDoor>();
        static readonly List<VantageDoor> all = new List<VantageDoor>();
        public static IReadOnlyList<VantageDoor> All => all;

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
        [Tooltip("VANTAGE: Unlock() waits until this level (VantageTowerLevels index) is cleared; -1 = no wait.")]
        public int ClearLevel = -1;
        [Tooltip("VANTAGE: an open doorway (leaf hidden) from the start, or once unlocked; no motion. Off for the shutter and gates.")]
        public bool Doorway;

        [Header("Shutter")]
        public float shutterDrop = 2.75f;          // open_z - closed_z from Blender
        public float shutterClosedHeight = 2.9f;
        public float shutterCloseTime = 0.6f;
        public float shutterOpenTime = 1.5f;

        const float SenseInterval = 0.15f;

        Vector3 closedPos, openPos;
        Transform oneWayRef;
        Vector3 doorNormal = Vector3.forward;
        bool openedFromAllowedSide, want, pendingUnlock, started;
        float nextSense;
        NavMeshObstacle obstacle;

        bool doorwayOpen;

        // shutter state: 1 = open (rolled up), 0 = closed
        float shutterT = 1f, shutterTarget = 1f;
        Vector3 shutterOpenPos, shutterOpenScale, shutterClosedScale;
        List<string> killList;

        public bool HasKillList => killList != null;
        public bool IsLocked => locked || pendingUnlock;
        /// <summary>0 = shut, 1 = fully open (for the autoplay bot and the HUD).</summary>
        public float OpenAmount => !started ? 0f : type == DoorType.Shutter ? shutterT
            : Doorway ? (doorwayOpen ? 1f : 0f)
            : Mathf.Clamp01((transform.position.y - closedPos.y) / Mathf.Max(0.01f, openPos.y - closedPos.y));

        void Awake()
        {
            registry[name] = this;
            all.Add(this);
            obstacle = GetComponent<NavMeshObstacle>();
        }

        void OnDestroy()
        {
            if (registry.TryGetValue(name, out var d) && d == this) registry.Remove(name);
            all.Remove(this);
        }

        void Start()
        {
            closedPos = transform.position;
            var r = GetComponentInChildren<Renderer>();
            Bounds b = r ? r.bounds : new Bounds(transform.position, new Vector3(1.2f, 2.3f, 0.12f));
            openPos = closedPos + Vector3.up * (b.size.y + 0.05f);
            doorNormal = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
            nextSense = Time.time + Random.value * SenseInterval;

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
                killList = new List<string>(unlockBy.Substring(5).Split(','));
            if (!string.IsNullOrEmpty(oneWaySideRefName))
            {
                var g = GameObject.Find(oneWaySideRefName);
                if (g) oneWayRef = g.transform;
            }
            started = true;
            if (Doorway && type != DoorType.Shutter && !locked) setDoorway(true);
            updateObstacle();
        }

        public void Unlock()
        {
            var levels = VantageTowerLevels.Instance;
            if (ClearLevel >= 0 && levels != null && !levels.IsCleared(ClearLevel))
            {
                // Remembered; ApplyPending opens it when the level is cleared.
                pendingUnlock = true;
                return;
            }
            pendingUnlock = false;
            locked = false;
            if (type == DoorType.Shutter) OpenShutter();
            else if (Doorway && started) setDoorway(true);
            updateObstacle();
        }

        /// <summary>
        /// A level was cleared: doors whose Unlock() was waiting for it open, and so do locked doors with no other
        /// way to open them that belong to it (the exit to the helipad).
        /// </summary>
        public static void ApplyPending(int clearedLevel)
        {
            for (int i = 0; i < all.Count; i++)
            {
                var d = all[i];
                if (d.ClearLevel < 0 || d.ClearLevel > clearedLevel) continue;
                if (d.pendingUnlock || (d.locked && string.IsNullOrEmpty(d.unlockBy)))
                    d.Unlock();
            }
        }

        /// <summary>Checkpoint restore: unlocked (and the shutter rolled up) at once, whatever its conditions.</summary>
        public void ForceOpen()
        {
            pendingUnlock = false;
            locked = false;
            killList = null;
            if (type == DoorType.Shutter)
            {
                shutterT = shutterTarget = 1f;
                if (started) UpdateShutter();
            }
            else if (Doorway && started) setDoorway(true);
            updateObstacle();
        }

        public void CloseShutter() { shutterTarget = 0f; updateObstacle(); }
        public void OpenShutter() { shutterTarget = 1f; updateObstacle(); }
        public bool ShutterClosed => shutterT <= 0.001f;

        /// <summary>Re-checks every door's KILL list (called by the level brain when an enemy dies or a level starts).</summary>
        public static void RecheckKills()
        {
            var mgr = VantageTowerLevels.Instance;
            if (mgr == null) return;
            for (int i = 0; i < all.Count; i++)
            {
                var d = all[i];
                if (d.killList == null || !d.started) continue;
                if (d.type == DoorType.Shutter ? !d.ShutterClosed : !d.locked) continue;   // shutter only reopens after closing
                if (!mgr.KillListDone(d.killList)) continue;
                d.killList = null;
                d.Unlock();
            }
        }

        void updateObstacle()
        {
            if (obstacle == null) return;
            obstacle.enabled = type == DoorType.Shutter ? shutterTarget < 0.5f : locked || pendingUnlock;
        }

        void Update()
        {
            if (type == DoorType.Shutter)
            {
                if (shutterT != shutterTarget) UpdateShutter();
                return;
            }
            if (Doorway) return;   // open doorways don't move

            if (Time.time >= nextSense)
            {
                nextSense = Time.time + SenseInterval;
                want = !locked && !pendingUnlock && someoneNear();
            }
            var target = want ? openPos : closedPos;
            if (transform.position != target)
                transform.position = Vector3.MoveTowards(transform.position, target, openSpeed * Time.deltaTime);
        }

        /// <summary>An open doorway: the leaf and its colliders are switched off (or back on when shut).</summary>
        void setDoorway(bool open)
        {
            doorwayOpen = open;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = !open;
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = !open;
        }

        /// <summary>A living character close to the doorway; for one-way doors the player only from the allowed side.</summary>
        bool someoneNear()
        {
            var player = VantageEvents.ActivePlayer();
            var any = false;
            for (int i = 0; i < CoverShooter.Actors.Count; i++)
            {
                var actor = CoverShooter.Actors.Get(i);
                if (actor == null || !actor.IsAlive || !actor.isActiveAndEnabled) continue;
                Vector3 d = actor.transform.position - closedPos;
                if (new Vector2(d.x, d.z).sqrMagnitude > openRadius * openRadius || Mathf.Abs(d.y) > 2.5f) continue;
                if (actor == player && type == DoorType.OneWay && oneWayRef && !openedFromAllowedSide)
                {
                    Vector3 toRef = oneWayRef.position - closedPos;
                    if (Mathf.Sign(Vector3.Dot(d, doorNormal)) != Mathf.Sign(Vector3.Dot(toRef, doorNormal))) continue;
                    openedFromAllowedSide = true;
                }
                any = true;
            }
            if (!any) openedFromAllowedSide = false;
            return any;
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
