using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// A field medkit at a level's entrance (2026-11-25, the user's request): walking into it brings the player back
    /// to full health. It only takes itself when the player is hurt, so one passed at full health stays for later.
    /// Spawned by VantageTowerLevels when a level begins (each life gets one). The look is built here: a white case
    /// with a red cross, turning slowly over a soft green light, readable in the dark rooms.
    /// </summary>
    public class VantageHealthKit : MonoBehaviour
    {
        public float SpinSpeed = 60f;
        public float BobHeight = 0.05f;
        public float Lift = 0.45f;
        public string Message = "HEALTH RESTORED";

        private Vector3 _home;
        private Transform _model;
        private Material _case, _cross;

        private void Awake()
        {
            gameObject.layer = 2;   // Ignore Raycast: no shot, camera cast or sight ray stops on it
            var trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.9f;
            trigger.center = Vector3.up * 0.5f;
            build();
        }

        private void Start() => _home = transform.position;

        private void build()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            _case = new Material(shader) { color = new Color(0.86f, 0.86f, 0.82f) };
            _cross = new Material(shader) { color = new Color(0.8f, 0.08f, 0.06f) };
            _cross.EnableKeyword("_EMISSION");
            _cross.SetColor("_EmissionColor", new Color(0.9f, 0.1f, 0.06f) * 1.6f);

            _model = new GameObject("Model").transform;
            _model.SetParent(transform, false);
            _model.localPosition = Vector3.up * Lift;
            part("Case", PrimitiveType.Cube, new Vector3(0.5f, 0.32f, 0.22f), Vector3.zero, _case);
            part("Handle", PrimitiveType.Cube, new Vector3(0.2f, 0.05f, 0.05f), new Vector3(0, 0.2f, 0), _case);
            foreach (var side in new[] { 1f, -1f })
            {
                var z = side * 0.112f;
                part("Cross V", PrimitiveType.Cube, new Vector3(0.07f, 0.22f, 0.01f), new Vector3(0, 0, z), _cross);
                part("Cross H", PrimitiveType.Cube, new Vector3(0.22f, 0.07f, 0.01f), new Vector3(0, 0, z), _cross);
            }

            var light = new GameObject("Glow").AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.transform.localPosition = Vector3.up * (Lift + 0.4f);
            light.type = LightType.Point;
            light.color = new Color(0.45f, 1f, 0.55f);
            light.range = 2.6f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
        }

        private void part(string name, PrimitiveType type, Vector3 size, Vector3 at, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_model, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void Update()
        {
            if (_model == null) return;
            _model.Rotate(Vector3.up, SpinSpeed * Time.deltaTime, Space.World);
            _model.localPosition = Vector3.up * (Lift + Mathf.Sin(Time.time * 2f) * BobHeight);
        }

        // Stay as well as Enter: a player who walked over it at full health and got hurt on it takes it then.
        private void OnTriggerEnter(Collider other) => tryTake(other);
        private void OnTriggerStay(Collider other) => tryTake(other);

        private void tryTake(Collider other)
        {
            var player = VantageEvents.ActivePlayer();
            if (player == null || other.GetComponentInParent<CoverShooter.BaseActor>() != player) return;
            var rules = player.GetComponent<VantageThirdPersonPlayer>();
            if (rules == null || !rules.Hurt) return;
            rules.Heal();
            VantageAudio.Play(VantageSounds.PickupWeapon, transform.position);
            VantageEvents.RaiseMessage(Message);
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_case != null) Destroy(_case);
            if (_cross != null) Destroy(_cross);
        }
    }
}
