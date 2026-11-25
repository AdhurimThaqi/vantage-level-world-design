using CoverShooter;
using UnityEngine;

namespace Vantage
{
    /// <summary>
    /// A bucket of grenades (2026-11-25): walking into it fills the player up to Count grenades and starts the grenade
    /// how-to (VantageGrenades). Spawned by VantageTowerLevels when its level begins (L3: in the robot room, on the way
    /// to the breach, right before the patrols behind the rubble). It stays when the player already carries that many.
    /// The look is built here: a dented metal bucket with the template's own grenade models heaped in it, under a warm
    /// light so it reads in the dark room.
    /// </summary>
    public class VantageGrenadeCrate : MonoBehaviour
    {
        public int Count = 3;
        public string Message = "GRENADES";

        private Material _metal, _rim;

        private void Awake()
        {
            gameObject.layer = 2;   // Ignore Raycast: no shot, camera cast or sight ray stops on it
            var trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1f;
            trigger.center = Vector3.up * 0.5f;
        }

        private void Start() => build();

        private void build()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            _metal = new Material(shader) { color = new Color(0.32f, 0.34f, 0.3f) };
            _metal.SetFloat("_Metallic", 0.6f);
            _metal.SetFloat("_Smoothness", 0.35f);
            _rim = new Material(shader) { color = new Color(0.08f, 0.08f, 0.07f) };

            var model = new GameObject("Model").transform;
            model.SetParent(transform, false);
            part(model, "Bucket", PrimitiveType.Cylinder, new Vector3(0.46f, 0.21f, 0.46f), new Vector3(0, 0.21f, 0), _metal);
            part(model, "Inside", PrimitiveType.Cylinder, new Vector3(0.42f, 0.005f, 0.42f), new Vector3(0, 0.4f, 0), _rim);
            part(model, "Rim", PrimitiveType.Cylinder, new Vector3(0.48f, 0.02f, 0.48f), new Vector3(0, 0.42f, 0), _metal);
            var handle = part(model, "Handle", PrimitiveType.Cube, new Vector3(0.5f, 0.025f, 0.025f), new Vector3(0, 0.62f, 0), _metal);
            handle.localRotation = Quaternion.Euler(0, 35f, 0);
            foreach (var side in new[] { -1f, 1f })
            {
                var arm = part(model, "Handle Arm", PrimitiveType.Cube, new Vector3(0.02f, 0.22f, 0.02f), Quaternion.Euler(0, 35f, 0) * new Vector3(side * 0.24f, 0.52f, 0), _metal);
                arm.localRotation = Quaternion.Euler(0, 35f, 0);
            }

            // The heap: the grenade the player throws, six of them on top.
            var prefab = grenadePrefab();
            for (int i = 0; i < 6; i++)
            {
                var angle = i * 60f + (i % 2) * 17f;
                var radius = i == 0 ? 0f : 0.12f;
                var at = Quaternion.Euler(0, angle, 0) * Vector3.forward * radius + Vector3.up * (0.45f + (i == 0 ? 0.06f : 0f));
                grenade(model, prefab, at, Quaternion.Euler(Random.Range(-40f, 40f), angle, Random.Range(-40f, 40f)));
            }

            var light = new GameObject("Glow").AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.transform.localPosition = Vector3.up * 1.4f;
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.38f);
            light.range = 3.2f;
            light.intensity = 1.8f;
            light.shadows = LightShadows.None;
        }

        private static GameObject grenadePrefab()
        {
            var player = VantageEvents.ActivePlayer();
            var motor = player != null ? player.GetComponent<CharacterMotor>() : null;
            return motor != null ? (motor.Grenade.Right != null ? motor.Grenade.Right : motor.Grenade.Left) : null;
        }

        /// <summary>A look-only copy of the grenade (no script, body or collider), about 11 cm long.</summary>
        private void grenade(Transform parent, GameObject prefab, Vector3 at, Quaternion rotation)
        {
            Transform t;
            if (prefab != null)
            {
                var holder = new GameObject("Grenade");
                holder.SetActive(false);   // its scripts never wake
                holder.transform.SetParent(parent, false);
                var copy = Instantiate(prefab, holder.transform);
                // Look only: scripts first, then what depends on the rest, then the rest (bodies, colliders, particles).
                foreach (var c in copy.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(c);
                foreach (var c in copy.GetComponentsInChildren<ParticleSystem>(true)) DestroyImmediate(c);
                foreach (var c in copy.GetComponentsInChildren<Renderer>(true)) if (!(c is MeshRenderer)) DestroyImmediate(c);
                foreach (var c in copy.GetComponentsInChildren<Component>(true))
                    if (!(c is Transform) && !(c is MeshFilter) && !(c is MeshRenderer)) DestroyImmediate(c);
                copy.transform.localPosition = Vector3.zero;
                copy.transform.localRotation = Quaternion.identity;
                copy.transform.localScale = Vector3.one;
                holder.SetActive(true);
                var size = 0f;
                foreach (var r in copy.GetComponentsInChildren<Renderer>()) size = Mathf.Max(size, r.bounds.size.magnitude);
                if (size > 0.001f) copy.transform.localScale = Vector3.one * (0.13f / size);
                t = holder.transform;
            }
            else
                t = part(parent, "Grenade", PrimitiveType.Sphere, Vector3.one * 0.09f, Vector3.zero, _rim);
            t.localPosition = at;
            t.localRotation = rotation;
            foreach (var r in t.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Transform part(Transform parent, string name, PrimitiveType type, Vector3 size, Vector3 at, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go.transform;
        }

        private void OnTriggerEnter(Collider other) => tryTake(other);
        private void OnTriggerStay(Collider other) => tryTake(other);

        private void tryTake(Collider other)
        {
            var player = VantageEvents.ActivePlayer();
            if (player == null || other.GetComponentInParent<BaseActor>() != player || !player.IsAlive) return;
            if (!VantageGrenades.FillTo(Count)) return;
            VantageAudio.Play(VantageSounds.PickupWeapon, transform.position);
            VantageEvents.RaiseMessage($"{Message} x{Count}");
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_metal != null) Destroy(_metal);
            if (_rim != null) Destroy(_rim);
        }
    }
}
