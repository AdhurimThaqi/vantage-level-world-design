using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// Sound as wayfinding: one diegetic loop at the player's next goal (a PA speaker at the end of the lockdown route,
    /// a radio by the rifle, a phone by the key, the key slot beeping, the stairs up, wind through the L3 breach, the
    /// helicopter's radio on the pad). Only the current step plays, only while the player is on that step's floor, and
    /// never in a cutscene, on the title screen or while dead. Fights have none: the enemies lead there.
    /// The steps' anchors are set by Vantage → Tower → Set Up Tower (Blender names); the order is <see cref="evaluate"/>.
    /// Design: docs/superpowers/specs/2026-11-09-guide-sound-design.md.
    /// </summary>
    public class VantageGoalSound : MonoBehaviour
    {
        [Serializable]
        public class Step
        {
            public string Name;
            public string Event;
            [Tooltip("The object the sound plays from (set by Setup).")]
            public Transform Anchor;
            [Tooltip("The level the player must be on to hear it (0 = level 1, 3 = the helipad).")]
            public int Floor;
        }

        // Indices into Steps (the spec's order).
        public const int L1Lockdown = 0, L1Cleared = 1, FindRifle = 2, FindKey = 3, UseKey = 4, L2GateOpen = 5, RobotRoomOpen = 6, L3Cleared = 7, ReachPad = 8;

        public List<Step> Steps = new List<Step>
        {
            new Step { Name = "L1 lockdown", Event = VantageSounds.GuidePa, Floor = 0 },
            new Step { Name = "L1 cleared", Event = VantageSounds.GuideStairs, Floor = 0 },
            new Step { Name = "Find the rifle", Event = VantageSounds.GuideRadio, Floor = 1 },
            new Step { Name = "Find the key", Event = VantageSounds.GuidePhone, Floor = 1 },
            new Step { Name = "Use the key", Event = VantageSounds.KeySlotBeep, Floor = 1 },
            new Step { Name = "L2 gate open", Event = VantageSounds.GuideStairs, Floor = 1 },
            new Step { Name = "Robot room open", Event = VantageSounds.GuideWind, Floor = 2 },
            new Step { Name = "L3 cleared", Event = VantageSounds.GuideStairs, Floor = 2 },
            new Step { Name = "Reach the pad", Event = VantageSounds.GuideHeliRadio, Floor = 3 },
        };

        [Tooltip("Blender names the state is read from (doors, trigger, key slot).")]
        public string RobotRoomDoor = "L3_DOOR_RobotRoom_LOCKED";
        public string OpenAreaTrigger = "TRIGGER_L3_OpenArea_Enter";
        public string Rifle = "Rifle";
        public string Key = "Key";
        public float PollInterval = 0.25f;
        public float StepFade = 1f;
        public float FloorFade = 0.5f;
        [Tooltip("A new floor under the player counts after it has held this long (no churn when the player hesitates on the stairs between two floors).")]
        public float FloorDelay = 0.75f;

        public static VantageGoalSound Instance { get; private set; }
        /// <summary>The current step (−1 = none).</summary>
        public int Current { get; private set; } = -1;
        public string CurrentName => Current >= 0 ? Steps[Current].Name : "none";
        public Transform Anchor => Current >= 0 ? Steps[Current].Anchor : null;
        /// <summary>Whether the current step is heard now (on its floor, not silenced).</summary>
        public bool Audible { get; private set; }
        /// <summary>Step changes since the scene loaded (for the bot).</summary>
        public int Changes { get; private set; }
        public AudioSource Source => _source;

        private AudioSource _source;
        private float _baseVolume = 1f, _nextPoll, _floorSince;
        private int _floor = int.MinValue, _rawFloor = int.MinValue;
        private AudioClip _pulse;
        private VantageArsenal _arsenal;
        private VantageInteract _slot;
        private VantageTrigger _openArea;
        private VantageDoor _robotDoor;

        private void Awake()
        {
            Instance = this;
            _pulse = VantageProceduralAudio.Create(VantageProceduralAudio.Kind.GuidePulse, 0);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            var slot = FindAnyObjectByType<VantageKeySlot>();
            _slot = slot != null ? slot.GetComponent<VantageInteract>() : null;
            foreach (var t in FindObjectsByType<VantageTrigger>(FindObjectsSortMode.None))
                if (t.name == OpenAreaTrigger) _openArea = t;
        }

        private void Update()
        {
            if (Time.time >= _nextPoll)
            {
                _nextPoll = Time.time + PollInterval;
                poll();
            }
            if (_source != null)
            {
                var target = Audible ? _baseVolume : 0f;
                _source.volume = Mathf.MoveTowards(_source.volume, target, _baseVolume * Time.deltaTime / Mathf.Max(0.01f, FloorFade));
            }
        }

        private void poll()
        {
            var player = VantageEvents.ActivePlayer();
            var levels = VantageTowerLevels.Instance;
            if (_arsenal == null && player != null) _arsenal = player.GetComponent<VantageArsenal>();
            if (_robotDoor == null) _robotDoor = VantageDoor.Find(RobotRoomDoor);

            // The player's floor, debounced: a new floor counts once it has held FloorDelay (the first reading at once).
            var raw = player != null && levels != null ? levels.LevelAt(player.transform.position) : -1;
            if (raw != _rawFloor) { _rawFloor = raw; _floorSince = Time.time; }
            if (_floor == int.MinValue || Time.time - _floorSince >= FloorDelay) _floor = raw;
            var floor = _floor;
            var step = levels != null ? evaluate(levels, floor) : -1;
            if (step != Current) change(step);

            var menu = VantageMainMenu.Instance != null && VantageMainMenu.Instance.Showing;
            Audible = Current >= 0 && player != null && !VantageCinematic.Active && !menu && floor == Steps[Current].Floor;
        }

        /// <summary>The spec's goal ladder: the first step whose condition holds, or −1.</summary>
        private int evaluate(VantageTowerLevels levels, int floor)
        {
            if (VantageAlarm.Instance != null && VantageAlarm.Instance.Active) return L1Lockdown;
            var current = levels.Current;
            var finale = levels.Levels.Count - 1;
            if (current == 1)
            {
                if (floor <= 0) return L1Cleared;   // still below level 2 (also just outside the footprint): go up
                if (_arsenal != null && !_arsenal.IsUnlocked(Rifle)) return FindRifle;
                var used = _slot != null && _slot.Used;
                if (!used && !VantageInventory.Has(Key)) return FindKey;
                if (!used) return UseKey;
                return -1;   // key in: clear the floor
            }
            if (current == 2)
            {
                if (floor == 1) return L2GateOpen;
                var open = _robotDoor != null && !_robotDoor.IsLocked;
                return open && (_openArea == null || !_openArea.Fired) ? RobotRoomOpen : -1;
            }
            if (current == finale)
            {
                if (floor >= 0 && floor < finale) return L3Cleared;
                return !levels.BossSpawned ? ReachPad : -1;
            }
            return -1;
        }

        private void change(int step)
        {
            if (_source != null) VantageAudio.Stop(_source, StepFade);
            _source = null;
            Current = step;
            Changes++;
            if (step < 0 || Steps[step].Anchor == null) return;
            _source = VantageAudio.PlayLoop(Steps[step].Event, Steps[step].Anchor, _pulse);
            if (_source == null) return;
            _baseVolume = _source.volume;
            _source.volume = 0f;   // fades in (Update), also when it starts off-floor
        }
    }
}
