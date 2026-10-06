using System.Collections.Generic;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// Roof finale (concept doc): stepping onto the roof starts three waves of drones arriving over
    /// different edges while the mast turret denies the centre. Clearing the last wave ends the level.
    /// Positions are local to this object, which sits at the tower root.
    /// </summary>
    public class VantageWaveSpawner : MonoBehaviour
    {
        [System.Serializable]
        public class Wave
        {
            public string Edge = "South";
            public int Count = 2;
            [Tooltip("Where the drones appear, outside the parapet (local space).")]
            public Vector3 SpawnCenter;
            public float Spread = 4f;
        }

        public GameObject DronePrefab;
        public List<Wave> Waves = new List<Wave>();

        [Tooltip("Roof deck height (local). The waves start once the player stands above it.")]
        public float RoofHeight = 15.7f;
        public float HalfSize = 9f;
        public float DelayBetweenWaves = 3f;

        public int CurrentWave { get; private set; } = -1;
        public bool Completed { get; private set; }

        private readonly List<VantageDrone> _alive = new List<VantageDrone>();
        private float _nextWaveTime = -1f;
        private float _messageUntil;
        private string _message;

        private void Update()
        {
            if (Completed)
                return;

            if (CurrentWave < 0)
            {
                if (playerOnRoof())
                    startWave(0);
                return;
            }

            _alive.RemoveAll(d => d == null || d.IsDead);
            if (_alive.Count > 0)
                return;

            if (_nextWaveTime < 0)
                _nextWaveTime = Time.time + DelayBetweenWaves;

            if (Time.time < _nextWaveTime)
                return;

            _nextWaveTime = -1f;
            if (CurrentWave + 1 < Waves.Count)
                startWave(CurrentWave + 1);
            else
                complete();
        }

        private bool playerOnRoof()
        {
            var player = VantageEvents.ActivePlayer();
            if (player == null)
                return false;

            var local = transform.InverseTransformPoint(player.transform.position);
            return local.y > RoofHeight - 0.3f && Mathf.Abs(local.x) < HalfSize && Mathf.Abs(local.z) < HalfSize;
        }

        private void startWave(int index)
        {
            CurrentWave = index;
            var wave = Waves[index];

            for (int i = 0; i < wave.Count; i++)
            {
                var offset = new Vector3(Random.Range(-wave.Spread, wave.Spread), Random.Range(-1f, 1.5f), Random.Range(-wave.Spread, wave.Spread));
                var position = transform.TransformPoint(wave.SpawnCenter + offset);
                var drone = Instantiate(DronePrefab, position, Quaternion.LookRotation(transform.position - position)).GetComponent<VantageDrone>();

                // Wave drones know where the player is and come straight in.
                drone.DetectRange = 60f;
                drone.ChaseRange = 80f;
                _alive.Add(drone);
            }

            show($"WAVE {index + 1} / {Waves.Count}  -  {wave.Edge.ToUpper()}", 3f);
            VantageEvents.RaiseWaveStarted(index + 1, wave.Count);
        }

        private void complete()
        {
            Completed = true;
            show("THE TOWER IS SILENT", 8f);
            VantageEvents.RaiseLevelCompleted();
        }

        private void show(string text, float seconds)
        {
            _message = text;
            _messageUntil = Time.time + seconds;
        }

        private void OnGUI()
        {
            if (Time.time > _messageUntil || string.IsNullOrEmpty(_message))
                return;

            var style = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
            style.normal.textColor = new Color(1f, 0.85f, 0.6f);
            GUI.Label(new Rect(0, Screen.height * 0.18f, Screen.width, 60), _message, style);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);
            foreach (var wave in Waves)
                Gizmos.DrawWireSphere(transform.TransformPoint(wave.SpawnCenter), wave.Spread);
        }
    }
}
