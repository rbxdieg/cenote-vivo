#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace CenoteVivo
{
    // Sonda de rendimiento para builds de desarrollo (no se incluye en builds de release).
    // Se activa con el argumento "-cenoteMetrics <archivo>" y opcionalmente "-cenoteBaseline".
    // Recorre las 3 distancias de cámara, lee los contadores del Profiler y escribe un informe.
    public class MetricsProbe : MonoBehaviour
    {
        const int WarmupFrames = 90;
        const int SampleFrames = 240;

        string outputPath;
        bool baseline;
        bool antiAliasing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-cenoteMetrics");
            if (i < 0 || i + 1 >= args.Length) return;
            var probe = new GameObject("MetricsProbe").AddComponent<MetricsProbe>();
            probe.outputPath = args[i + 1];
            probe.baseline = args.Contains("-cenoteBaseline");
            probe.antiAliasing = args.Contains("-cenoteAA");
        }

        // Compara Sin AA, MSAA 2x/4x, FXAA y TAA en la vista "Nivel del agua" con tiempos reales de GPU.
        IEnumerator MeasureAntiAliasing(Camera cam)
        {
            var rp = (UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
            var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
            var modes = new (string, int, UnityEngine.Rendering.Universal.AntialiasingMode)[]
            {
                ("Sin AA", 1, UnityEngine.Rendering.Universal.AntialiasingMode.None),
                ("MSAA 2x", 2, UnityEngine.Rendering.Universal.AntialiasingMode.None),
                ("MSAA 4x", 4, UnityEngine.Rendering.Universal.AntialiasingMode.None),
                ("FXAA", 1, UnityEngine.Rendering.Universal.AntialiasingMode.FastApproximateAntialiasing),
                ("TAA", 1, UnityEngine.Rendering.Universal.AntialiasingMode.TemporalAntiAliasing),
            };
            var sb = new StringBuilder();
            sb.AppendLine($"GPU: {SystemInfo.graphicsDeviceName} | API: {SystemInfo.graphicsDeviceType} | Resolucion: {Screen.width}x{Screen.height}");
            sb.AppendLine("Tecnica | FPS | Frame ms | GPU ms | Gfx usada MB | Render Textures");
            var timings = new FrameTiming[1];
            foreach (var (name, msaa, post) in modes)
            {
                rp.msaaSampleCount = msaa;
                cam.allowMSAA = msaa > 1;
                data.antialiasing = post;
                data.renderPostProcessing = post != UnityEngine.Rendering.Universal.AntialiasingMode.None;
                data.antialiasingQuality = UnityEngine.Rendering.Universal.AntialiasingQuality.High;
                for (int f = 0; f < WarmupFrames; f++) yield return null;
                using var gfxMem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory");
                using var rts = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Render Textures Count");
                var frame = new List<float>(); var gpu = new List<float>();
                for (int f = 0; f < SampleFrames; f++)
                {
                    yield return null;
                    frame.Add(Time.unscaledDeltaTime * 1000f);
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0) gpu.Add((float)timings[0].gpuFrameTime);
                }
                float ms = Median(frame);
                sb.AppendLine($"{name} | {1000f / ms:F0} | {ms:F2} | {(gpu.Count > 0 ? Median(gpu).ToString("F3") : "n/d")} | {gfxMem.LastValue / 1048576f:F1} | {rts.LastValue}");
            }
            File.WriteAllText(outputPath, sb.ToString());
            Application.Quit();
        }

        IEnumerator Start()
        {
            // Misma configuración que en Android: perfil Mobile, sin límite de FPS para medir el costo real.
            QualitySettings.SetQualityLevel(System.Array.IndexOf(QualitySettings.names, "Mobile"), true);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            var ui = FindFirstObjectByType<UIManager>();
            var pollution = FindFirstObjectByType<PollutionSimulator>();
            var views = FindFirstObjectByType<PerspectiveCameraController>();
            var cam = Camera.main;
            yield return null;
            ui.StartExperience();
            pollution.SetImmediate(1f);
            if (baseline)
            {
                GraphicsSettings.useScriptableRenderPipelineBatching = false;
                foreach (var g in FindObjectsByType<LODGroup>()) g.ForceLOD(0);
            }
            if (antiAliasing)
            {
                pollution.SetImmediate(0f);
                views.GoTo(1, instant: true);
                yield return new WaitForSeconds(2f);
                yield return MeasureAntiAliasing(cam);
                yield break;
            }
            views.GoTo(0, instant: true);
            yield return new WaitForSeconds(3f);
            views.enabled = false;

            var far = (cam.transform.position, cam.transform.rotation);
            var water = GameObject.Find("Agua").transform.position;
            var rock = FindObjectsByType<LODGroup>().OrderBy(r => Vector3.Distance(r.transform.position, water + new Vector3(8f, 0f, -8f))).First().transform;
            var look = rock.position + Vector3.up * 0.6f;
            var close = look + (water - look).normalized * 3.5f + Vector3.up * 1.2f;
            var closeRot = Quaternion.LookRotation(look - close);
            var stops = new (string, Vector3, Quaternion)[]
            {
                ("Alejada (panoramica)", far.position, far.rotation),
                ("Acercandose (transicion)", Vector3.Lerp(far.position, close, 0.55f), Quaternion.Slerp(far.rotation, closeRot, 0.55f)),
                ("Acercada (primer plano)", close, closeRot),
            };

            var sb = new StringBuilder();
            sb.AppendLine($"Dispositivo: {SystemInfo.deviceModel} | GPU: {SystemInfo.graphicsDeviceName} | API: {SystemInfo.graphicsDeviceType} | CPU: {SystemInfo.processorType}");
            sb.AppendLine($"Escenario: {(baseline ? "SIN OPTIMIZAR (LOD0 forzado, sin SRP Batcher, sin static batching)" : "OPTIMIZADO (LOD Group + SRP Batcher + static batching)")}");
            sb.AppendLine($"Resolucion: {Screen.width}x{Screen.height} | Calidad: {QualitySettings.names[QualitySettings.GetQualityLevel()]} | MSAA: {QualitySettings.antiAliasing}x | LOD Bias: {QualitySettings.lodBias}");
            sb.AppendLine("Vista | FPS | Frame ms | CPU main ms | GPU ms | Batches | SetPass | Desglose draw calls | Triangulos | Vertices | RAM usada MB | Gfx MB");

            foreach (var (name, pos, rot) in stops)
            {
                cam.transform.SetPositionAndRotation(pos, rot);
                for (int f = 0; f < WarmupFrames; f++) yield return null;

                // En Unity 6.5 los draw calls se reparten por ruta de envío; "Batches" es la suma de todas.
                using var standardDraws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Standard Draw Calls Count", SampleFrames);
                using var srpDraws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SRP Batcher Draw Calls Count", SampleFrames);
                using var instancedDraws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Instanced Batched Draw Calls Count", SampleFrames);
                using var dynamicDraws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Dynamic Batched Draw Calls Count", SampleFrames);
                using var brgDraws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "BRG Draw Calls Count", SampleFrames);
                using var staticBatches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Static Batches Count", SampleFrames);
                using var srpInstances = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SRP Batcher Instances Count", SampleFrames);
                using var setpass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", SampleFrames);
                using var tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", SampleFrames);
                using var verts = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count", SampleFrames);
                using var mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", SampleFrames);
                using var usedMem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
                using var gfxMem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory");

                var frameMs = new List<float>();
                var gpuMs = new List<double>();
                var timings = new FrameTiming[1];
                for (int f = 0; f < SampleFrames; f++)
                {
                    yield return null;
                    frameMs.Add(Time.unscaledDeltaTime * 1000f);
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0) gpuMs.Add(timings[0].gpuFrameTime);
                }
                float ms = Median(frameMs);
                string gpu = gpuMs.Count > 0 ? Median(gpuMs.Select(x => (float)x).ToList()).ToString("F2") : "n/d";
                long batches = standardDraws.LastValue + srpDraws.LastValue + instancedDraws.LastValue + dynamicDraws.LastValue + brgDraws.LastValue;
                sb.AppendLine($"{name} | {1000f / ms:F0} | {ms:F2} | {Avg(mainThread) / 1e6:F2} | {gpu} | {batches} | {setpass.LastValue} | " +
                              $"std {standardDraws.LastValue} / srp {srpDraws.LastValue} (inst {srpInstances.LastValue}) / static batches {staticBatches.LastValue} | " +
                              $"{tris.LastValue} | {verts.LastValue} | {usedMem.LastValue / 1048576f:F0} | {gfxMem.LastValue / 1048576f:F0}");
            }

            File.WriteAllText(outputPath, sb.ToString());
            Debug.Log("CENOTE_METRICS\n" + sb);
            Application.Quit();
        }

        static float Median(List<float> v) { v.Sort(); return v[v.Count / 2]; }

        static double Avg(ProfilerRecorder r)
        {
            int n = r.Count;
            if (n == 0) return 0;
            double sum = 0;
            for (int i = 0; i < n; i++) sum += r.GetSample(i).Value;
            return sum / n;
        }
    }
}
#endif
