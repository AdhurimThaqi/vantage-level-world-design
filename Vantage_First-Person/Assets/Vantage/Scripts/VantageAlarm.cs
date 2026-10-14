using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// The ground floor trap. The first alarm (the trigger inside the entrance, or the security camera spotting the
    /// player) slams the maintenance shutter, starts the siren, flashes the beacons and red lights, and sends every
    /// level 1 enemy after the player. It stops when level 1 is cleared (the shutter's kill list reopens it).
    /// Later alarms are ignored, and so is any alarm once level 1 is behind the player.
    /// </summary>
    public class VantageAlarm : MonoBehaviour
    {
        public static VantageAlarm Instance { get; private set; }

        [Tooltip("Door closed by the alarm.")]
        public string Shutter = "SHUTTER_Maintenance";
        [Tooltip("The beacon meshes (emissive while the alarm runs).")]
        public Renderer[] Beacons;
        [Tooltip("Red point lights at the beacons (no shadows).")]
        public Light[] Lights;
        [Tooltip("The siren (a VantageProceduralAudio with Sound = Siren), inactive until the alarm.")]
        public GameObject Siren;
        public Color Colour = new Color(1f, 0.08f, 0.04f);
        public float FlashRate = 1.5f;
        public float LightIntensity = 4f;

        public bool Active { get; private set; }
        /// <summary>How many times an alarm was raised (the bot checks that the trap fires once).</summary>
        public int Raised { get; private set; }

        private MaterialPropertyBlock _block;

        private void Awake()
        {
            Instance = this;
            setLights(0f);
            if (Siren != null) Siren.SetActive(false);
        }

        private void OnEnable()
        {
            VantageEvents.AlarmRaised += raise;
            VantageEvents.AlarmStopped += stop;
        }

        private void OnDisable()
        {
            VantageEvents.AlarmRaised -= raise;
            VantageEvents.AlarmStopped -= stop;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void raise(string source)
        {
            var levels = VantageTowerLevels.Instance;
            if (Active || (levels != null && levels.Current > 0)) return;
            Active = true;
            Raised++;
            var door = VantageDoor.Find(Shutter);
            if (door != null) door.CloseShutter();
            if (Siren != null) Siren.SetActive(true);
            if (levels != null) levels.StartHunt();
            VantageEvents.RaiseMessage(source == "camera" ? "Spotted! Lockdown" : "Lockdown");
            Debug.Log($"[Vantage] Alarm raised by the {source}.");
        }

        private void stop()
        {
            if (!Active) return;
            Active = false;
            if (Siren != null) Siren.SetActive(false);
            setLights(0f);
        }

        private void Update()
        {
            if (!Active) return;
            // Hard on/off flash with a short fade, like a rotating beacon passing.
            var phase = Mathf.Repeat(Time.time * FlashRate, 1f);
            setLights(Mathf.Clamp01(1f - Mathf.Abs(phase - 0.25f) * 4f));
        }

        private void setLights(float k)
        {
            if (Lights != null)
                foreach (var l in Lights)
                    if (l != null)
                    {
                        l.enabled = k > 0.01f;
                        l.intensity = LightIntensity * k;
                    }
            if (Beacons == null) return;
            _block ??= new MaterialPropertyBlock();
            foreach (var r in Beacons)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor("_EmissionColor", Colour * (0.2f + 3f * k));
                r.SetPropertyBlock(_block);
            }
        }
    }
}
