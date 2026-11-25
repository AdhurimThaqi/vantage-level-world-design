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
        [Tooltip("VANTAGE change (2026-11-25): an open doorway that keeps its leaf where the model has it (standing open), instead of hiding it. The tower's front doors.")]
        public bool KeepLeaf;

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
            if (latched) transform.position = openPos;   // VANTAGE change: forced open (checkpoint) before Start: up, silently
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
            // VANTAGE change (2026-11-25): a locked gate that isn't a doorway (bars rolling up) stays up once unlocked.
            if (type == DoorType.Locked && !Doorway) latched = true;
            if (type == DoorType.Shutter) OpenShutter();
            else if (Doorway && started)
            {
                // VANTAGE change (2026-11-25): a locked gate opening is heard (the user's gate take); doorways that
                // stand open from the start or are opened by a checkpoint (ForceOpen) stay silent.
                if (!doorwayOpen) VantageAudio.Play(VantageSounds.ShutterRoll, transform.position);
                setDoorway(true);
            }
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
            if (type == DoorType.Locked && !Doorway)
            {
                // Checkpoint: the gate is up already, silently.
                latched = want = true;
                if (started) transform.position = openPos;
            }
            if (type == DoorType.Shutter)
            {
                shutterT = shutterTarget = 1f;
                if (started) UpdateShutter();
            }
            else if (Doorway && started) setDoorway(true);
            updateObstacle();
        }

        /// <summary>
        /// VANTAGE change (2026-11-25): a level's wave door (L2 Office) shut and locked at runtime until the level brain
        /// opens it with OpenNow(): the leaf and its colliders back, and a carving obstacle so no one paths through it.
        /// </summary>
        public void Lock()
        {
            locked = true;
            pendingUnlock = false;
            killList = null;
            if (Doorway) setDoorway(false);
            swingShut();
            if (obstacle == null)
            {
                obstacle = gameObject.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                var box = GetComponent<BoxCollider>();
                var mesh = GetComponent<MeshFilter>();
                if (box != null) { obstacle.center = box.center; obstacle.size = box.size + new Vector3(0.1f, 0f, 0.1f); }
                else if (mesh != null && mesh.sharedMesh != null) { obstacle.center = mesh.sharedMesh.bounds.center; obstacle.size = mesh.sharedMesh.bounds.size + new Vector3(0.1f, 0f, 0.1f); }
                obstacle.carving = true;
                obstacle.carveOnlyStationary = false;
            }
            updateObstacle();
        }

        /// <summary>
        /// VANTAGE change (2026-11-25): the model has some leaves standing open (the L2 Office door is turned ~70° into
        /// the hall), so a locked one showed open. Turns the leaf about its pivot (the hinge) into the gap of its
        /// doorway: from the hinge, the gap is the axis direction where the wall comes back about one leaf length away.
        /// </summary>
        void swingShut()
        {
            if (swungFrom.HasValue) return;
            var r = GetComponentInChildren<Renderer>();
            if (r == null) return;
            var hinge = transform.position;
            var leaf = r.bounds.center - hinge; leaf.y = 0;
            var length = leaf.magnitude * 2f;
            if (length < 0.3f) return;
            var colliders = GetComponentsInChildren<Collider>();
            foreach (var c in colliders) c.enabled = false;
            var origin = hinge + Vector3.up * 1.1f;
            Vector3? best = null;
            var bestError = float.MaxValue;
            foreach (var dir in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            {
                var d = Physics.Raycast(origin, dir, out var hit, length + 1f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : float.MaxValue;
                var error = Mathf.Abs(d - length);
                if (d >= length * 0.7f && d <= length + 0.6f && error < bestError) { bestError = error; best = dir; }
            }
            foreach (var c in colliders) c.enabled = true;
            if (!best.HasValue)
            {
                Debug.LogWarning($"[Vantage] {name}: no doorway gap found beside its hinge; the leaf stays as modelled.");
                return;
            }
            swungFrom = transform.rotation;
            transform.rotation = Quaternion.FromToRotation(leaf.normalized, best.Value) * transform.rotation;
        }
        Quaternion? swungFrom;
        bool latched;

        /// <summary>VANTAGE change (2026-11-25): opens a door locked by Lock() now, whatever its ClearLevel, with the gate sound.</summary>
        public void OpenNow()
        {
            pendingUnlock = false;
            if (!locked) return;
            locked = false;
            if (swungFrom.HasValue) { transform.rotation = swungFrom.Value; swungFrom = null; }
            if (Doorway)
            {
                VantageAudio.Play(VantageSounds.ShutterRoll, transform.position);
                setDoorway(true);
            }
            updateObstacle();
        }

        public void CloseShutter() { shutterTarget = 0f; updateObstacle(); }
        public void OpenShutter()
        {
            // VANTAGE change: the roll-up sound
            if (shutterTarget < 1f) VantageAudio.Play(VantageSounds.ShutterRoll, transform.position);
            shutterTarget = 1f;
            updateObstacle();
        }
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
                want = !locked && !pendingUnlock && (latched || someoneNear());
            }
            var target = want ? openPos : closedPos;
            if (transform.position == target) return;
            // VANTAGE change (2026-11-25): a locked gate (the L2 stairwell gate) is heard as it starts to rise, and rises
            // exactly as long as its sound lasts, so the motion and the sound end together.
            if (latched && want && transform.position == closedPos)
            {
                var sound = VantageAudio.Play(VantageSounds.GateOpen, transform.position);
                if (sound != null && sound.clip != null)
                    openSpeed = Vector3.Distance(closedPos, openPos) / Mathf.Max(0.2f, sound.clip.length / Mathf.Max(0.01f, sound.pitch));
            }
            transform.position = Vector3.MoveTowards(transform.position, target, openSpeed * Time.deltaTime);
        }

        /// <summary>An open doorway: the leaf and its colliders are switched off (or back on when shut).</summary>
        void setDoorway(bool open)
        {
            doorwayOpen = open;
            if (KeepLeaf) return;   // the model's leaf stands open: nothing to hide
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
