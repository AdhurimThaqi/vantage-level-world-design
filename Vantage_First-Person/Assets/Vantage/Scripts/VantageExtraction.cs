using CoverShooter;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// The ending. When the boss dies, the extraction helicopter flies in over the valley and lands on the helipad;
    /// the player's controls are switched off and the camera pulls back to watch it come down, then the end screen
    /// shows (VantagePlayerHUD). Enter starts a new run.
    /// The helicopter is a prefab with a "Model" child (placeholder until the user's Blender model replaces it) and
    /// "Main Rotor" / "Tail Rotor" parts that spin.
    /// </summary>
    public class VantageExtraction : MonoBehaviour
    {
        public GameObject HelicopterPrefab;
        [Tooltip("Where it lands (the helipad).")]
        public Transform Pad;
        [Tooltip("Horizontal direction it comes from (away from the tower).")]
        public Vector3 ApproachFrom = Vector3.right;
        public float StartDistance = 120f;
        public float StartHeight = 45f;
        public float FlyTime = 7f;
        public float CameraPullTime = 4f;
        public float WinDelay = 1.2f;

        public bool Running { get; private set; }
        public bool Landed { get; private set; }

        private Transform _heli, _mainRotor, _tailRotor;
        private Vector3 _start, _control, _land;
        private float _startTime = -1f, _landedAt = -1f;
        private bool _won;
        private Camera _camera;
        private Vector3 _camFrom, _camTo;
        private Quaternion _camFromRot;

        private void OnEnable()
        {
            if (VantageTowerLevels.Instance != null) VantageTowerLevels.Instance.BossDefeated += Begin;
        }

        private void Start()
        {
            // The level brain may wake after this; subscribe again once both exist.
            if (VantageTowerLevels.Instance != null)
            {
                VantageTowerLevels.Instance.BossDefeated -= Begin;
                VantageTowerLevels.Instance.BossDefeated += Begin;
            }
        }

        private void OnDisable()
        {
            if (VantageTowerLevels.Instance != null) VantageTowerLevels.Instance.BossDefeated -= Begin;
        }

        /// <summary>Starts the extraction (boss dead).</summary>
        public void Begin()
        {
            if (Running || Pad == null) return;
            Running = true;
            var away = ApproachFrom; away.y = 0;
            away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.right;
            _land = Pad.position;
            _start = _land + away * StartDistance + Vector3.up * StartHeight;
            _control = _land + away * 30f + Vector3.up * 25f;
            if (HelicopterPrefab != null)
            {
                _heli = Instantiate(HelicopterPrefab, _start, Quaternion.LookRotation(-away)).transform;
                _mainRotor = findChild(_heli, "Main Rotor");
                _tailRotor = findChild(_heli, "Tail Rotor");
            }
            _startTime = Time.time;
            freezePlayer();
            VantageEvents.RaiseMessage("Extraction inbound");
            Debug.Log("[Vantage] Extraction started.");
        }

        private static Transform findChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        /// <summary>Controls off, can't be hurt; the camera stops following and will pull back. Never ended: the end screen follows.</summary>
        private void freezePlayer()
        {
            VantageCinematic.Begin();
            _camera = Camera.main;
            if (_camera == null) return;
            _camFrom = _camera.transform.position;
            _camFromRot = _camera.transform.rotation;
            var side = Vector3.Cross(Vector3.up, (_start - _land).normalized);
            _camTo = _land + side * 18f + Vector3.up * 9f - (_start - _land).normalized * 6f;
        }

        private void Update()
        {
            if (Running && !_won && _startTime >= 0) fly();
            if (_won && confirmPressed()) PlayAgain();
        }

        /// <summary>Enter on the end screen: a new run from the start.</summary>
        public void PlayAgain()
        {
            VantageRun.Reset();
            VantageRun.ReloadLevel();
        }

        private void fly()
        {
            var t = Mathf.Clamp01((Time.time - _startTime) / FlyTime);
            var eased = 1f - (1f - t) * (1f - t);   // slows down for the landing
            if (_heli != null)
            {
                var a = Vector3.Lerp(_start, _control, eased);
                var b = Vector3.Lerp(_control, _land, eased);
                var p = Vector3.Lerp(a, b, eased);
                var ahead = p - _heli.position; ahead.y = 0;
                _heli.position = p;
                if (ahead.sqrMagnitude > 0.0001f)
                    _heli.rotation = Quaternion.Slerp(_heli.rotation, Quaternion.LookRotation(ahead) * Quaternion.Euler(8f * (1f - t), 0, 0), Time.deltaTime * 2f);
                var spin = Mathf.Lerp(1500f, 900f, t) * Time.deltaTime;
                if (_mainRotor != null) _mainRotor.Rotate(0, spin, 0, Space.Self);
                if (_tailRotor != null) _tailRotor.Rotate(spin, 0, 0, Space.Self);
            }
            if (_camera != null)
            {
                var c = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.time - _startTime) / CameraPullTime));
                _camera.transform.position = Vector3.Lerp(_camFrom, _camTo, c);
                var look = (_heli != null ? _heli.position : _land) - _camera.transform.position;
                if (look.sqrMagnitude > 0.01f)
                    _camera.transform.rotation = Quaternion.Slerp(_camFromRot, Quaternion.LookRotation(look), c);
            }
            if (t >= 1f && _landedAt < 0) { _landedAt = Time.time; Landed = true; }
            if (_landedAt >= 0 && Time.time - _landedAt >= WinDelay)
            {
                _won = true;
                VantageEvents.RaiseGameWon();
                VantageEvents.RaiseLevelCompleted();
                Debug.Log("[Vantage] Extraction complete: the tower is silent.");
            }
        }

        private static bool confirmPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var k = UnityEngine.InputSystem.Keyboard.current;
            return k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Return);
#endif
        }
    }
}
