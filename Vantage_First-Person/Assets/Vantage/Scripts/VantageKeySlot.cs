using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// The key slot by the stairwell gate (L2_KEYSLOT_InsertKey). When the key is used here, a copy of the key slides
    /// in and turns a quarter, and the slot lamp goes from red to green. The gate itself waits for level 2 to be
    /// cleared (VantageDoor.ClearLevel); until then a hint says so.
    /// </summary>
    [RequireComponent(typeof(VantageInteract))]
    public class VantageKeySlot : MonoBehaviour
    {
        [Tooltip("Mesh of the key, shown in the slot when it's used.")]
        public Mesh KeyMesh;
        public Material KeyMaterial;
        [Tooltip("Lamp beside the slot (emissive), red until the key is in.")]
        public Renderer Lamp;
        public Light LampLight;
        [Tooltip("Level whose clearing opens the gate (for the hint).")]
        public int GateLevel = 1;
        public Color Locked = new Color(1f, 0.1f, 0.05f);
        public Color Unlocked = new Color(0.2f, 1f, 0.35f);
        public float TurnTime = 0.4f;

        private Transform _key;
        private float _turnStart = -1f;
        private Quaternion _keyStart;
        private MaterialPropertyBlock _block;

        private void Awake() => GetComponent<VantageInteract>().onUsed.AddListener(used);

        private void Start() => tint(Locked);

        private void used()
        {
            if (KeyMesh != null)
            {
                var go = new GameObject("Key In Slot");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = KeyMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = KeyMaterial;
                _key = go.transform;
                _keyStart = _key.localRotation;
            }
            _turnStart = Time.time;
            var levels = VantageTowerLevels.Instance;
            if (levels != null && !levels.IsCleared(GateLevel))
                VantageEvents.RaiseMessage("Key in. Clear the floor to open the gate");
        }

        private void Update()
        {
            if (_turnStart < 0) return;
            var t = Mathf.Clamp01((Time.time - _turnStart) / TurnTime);
            if (_key != null) _key.localRotation = _keyStart * Quaternion.Euler(0, 0, 90f * Mathf.SmoothStep(0, 1, t));
            if (t >= 1f)
            {
                tint(Unlocked);
                _turnStart = -1f;
                enabled = false;
            }
        }

        private void tint(Color c)
        {
            if (LampLight != null) LampLight.color = c;
            if (Lamp == null) return;
            _block ??= new MaterialPropertyBlock();
            Lamp.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", c * 0.3f);
            _block.SetColor("_EmissionColor", c * 2f);
            Lamp.SetPropertyBlock(_block);
        }
    }
}
