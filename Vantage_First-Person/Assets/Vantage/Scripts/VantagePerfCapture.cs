using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Vantage
{
    /// <summary>
    /// Before/after performance evidence for the documentation (concept doc: performance optimization).
    /// F9 records 10 seconds of frame time, batches, set-pass calls and triangles plus a screenshot.
    /// F10 toggles occlusion culling on the active camera, so the same view can be captured with and without it.
    /// Results go to [project]/Playtests/Performance/ (editor) or persistentDataPath (build).
    /// </summary>
    public class VantagePerfCapture : MonoBehaviour
    {
        public float Duration = 10f;

        private ProfilerRecorder _batches, _setPass, _triangles, _drawCalls;
        private readonly List<float> _frames = new List<float>();
        private readonly List<long> _batchSamples = new List<long>(), _setPassSamples = new List<long>(), _triSamples = new List<long>();
        private float _until = -1f;
        private string _label;
        private string _status;
        private float _statusUntil;

        private void OnEnable()
        {
            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        }

        private void OnDisable()
        {
            _batches.Dispose();
            _setPass.Dispose();
            _triangles.Dispose();
            _drawCalls.Dispose();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var cam = Camera.main;

            if (keyboard != null && keyboard.f10Key.wasPressedThisFrame && cam != null)
            {
                cam.useOcclusionCulling = !cam.useOcclusionCulling;
                say("Occlusion culling " + (cam.useOcclusionCulling ? "ON" : "OFF"));
            }

            if (keyboard != null && keyboard.f9Key.wasPressedThisFrame && _until < 0)
            {
                _frames.Clear();
                _batchSamples.Clear();
                _setPassSamples.Clear();
                _triSamples.Clear();
                _label = cam != null && cam.useOcclusionCulling ? "occlusion-on" : "occlusion-off";
                _until = Time.unscaledTime + Duration;
                say($"Recording {Duration:F0}s ({_label})...");
            }

            if (_until < 0)
                return;

            _frames.Add(Time.unscaledDeltaTime * 1000f);
            _batchSamples.Add(_batches.LastValue);
            _setPassSamples.Add(_setPass.LastValue);
            _triSamples.Add(_triangles.LastValue);

            if (Time.unscaledTime >= _until)
            {
                _until = -1f;
                save();
            }
        }

        private void save()
        {
            var folder = Application.isEditor
                ? Path.Combine(Path.GetDirectoryName(Application.dataPath), "Playtests", "Performance")
                : Path.Combine(Application.persistentDataPath, "Playtests", "Performance");
            Directory.CreateDirectory(folder);

            var stamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var sorted = new List<float>(_frames);
            sorted.Sort();
            var avg = 0f;
            foreach (var f in _frames)
                avg += f;
            avg /= Mathf.Max(1, _frames.Count);
            var p99 = sorted.Count > 0 ? sorted[Mathf.Min(sorted.Count - 1, (int)(sorted.Count * 0.99f))] : 0f;
            var worst = sorted.Count > 0 ? sorted[sorted.Count - 1] : 0f;

            var cam = Camera.main;
            var report = new StringBuilder();
            report.Append($"VANTAGE performance capture {stamp} ({_label})\n");
            report.Append($"Camera at {(cam != null ? cam.transform.position.ToString("F1") : "?")}\n");
            report.Append($"Frames: {_frames.Count} over {Duration:F0}s\n");
            report.Append($"Frame time  avg {avg:F2} ms ({1000f / Mathf.Max(0.01f, avg):F0} fps)   99th pct {p99:F2} ms   worst {worst:F2} ms\n");
            report.Append($"Batches     avg {average(_batchSamples):F0}\n");
            report.Append($"SetPass     avg {average(_setPassSamples):F0}\n");
            report.Append($"Triangles   avg {average(_triSamples):F0}\n");
            report.Append("(Batches/SetPass/Triangles are only reported in the editor and development builds.)\n");

            var file = Path.Combine(folder, $"perf_{stamp}_{_label}");
            File.WriteAllText(file + ".txt", report.ToString());
            ScreenCapture.CaptureScreenshot(file + ".png");
            say($"Saved perf_{stamp}_{_label}  ({avg:F1} ms avg)");
            Debug.Log("[Vantage] " + report);
        }

        private static float average(List<long> values)
        {
            if (values.Count == 0)
                return 0;
            double sum = 0;
            foreach (var v in values)
                sum += v;
            return (float)(sum / values.Count);
        }

        private void say(string text)
        {
            _status = text;
            _statusUntil = Time.unscaledTime + 4f;
        }

        private void OnGUI()
        {
            if (Time.unscaledTime > _statusUntil && _until < 0)
                return;

            var style = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            style.normal.textColor = new Color(0.6f, 1f, 0.7f);
            var text = _until >= 0 ? $"REC {(_until - Time.unscaledTime):F1}s  ({_label})" : _status;
            GUI.Label(new Rect(10, 26, 600, 22), text, style);
        }
    }
}
