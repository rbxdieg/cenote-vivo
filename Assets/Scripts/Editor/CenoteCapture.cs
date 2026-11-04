using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace CenoteVivo.EditorTools
{
    // Entra en Play Mode (también en batchmode) y genera la evidencia de los entregables:
    // capturas de UI/estados, comparativa de Anti-Aliasing, LOD en alámbrico y métricas tipo Profiler.
    //   -executeMethod CenoteVivo.EditorTools.CenoteCapture.RunOptimized
    //   -executeMethod CenoteVivo.EditorTools.CenoteCapture.RunBaseline   (sin LOD / sin batching)
    public static class CenoteCapture
    {
        const string OutDir = "Tools/captures";
        const int W = 1600, H = 900;

        static IEnumerator routine;
        static float waitUntil;
        static bool baseline;
        static readonly StringBuilder report = new StringBuilder();

        [MenuItem("Cenote Vivo/Capturas y métricas")]
        public static void RunOptimized() => Begin(false);

        public static void RunBaseline() => Begin(true);

        static void Begin(bool isBaseline)
        {
            baseline = isBaseline;
            Directory.CreateDirectory(OutDir);
            EditorSceneManager.OpenScene(CenoteVivoSetup.ScenePath, OpenSceneMode.Single);
            if (baseline)
            {
                // Escena "antes": sin static batching (se quita la marca solo en memoria, sin guardar).
                foreach (var go in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Select(t => t.gameObject))
                    GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(go) & ~StaticEditorFlags.BatchingStatic);
            }
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayMode(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.EnteredPlayMode)
            {
                routine = baseline ? Baseline() : Optimized();
                waitUntil = 0f;
                EditorApplication.update += Tick;
            }
            else if (s == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.playModeStateChanged -= OnPlayMode;
                EditorSettings.enterPlayModeOptionsEnabled = false;
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        }

        // Mini corrutina sobre EditorApplication.update: "yield return segundos" o "yield return null" (un frame).
        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();

        static void Tick()
        {
            if (Time.realtimeSinceStartup < waitUntil) return;
            if (stack.Count == 0 && routine != null) { stack.Push(routine); routine = null; }
            try
            {
                while (stack.Count > 0)
                {
                    var top = stack.Peek();
                    if (!top.MoveNext()) { stack.Pop(); continue; }
                    if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
                    if (top.Current is float secs) waitUntil = Time.realtimeSinceStartup + secs;
                    return;
                }
            }
            catch (Exception e) { Debug.LogException(e); stack.Clear(); }
            EditorApplication.update -= Tick;
            File.WriteAllText(Path.Combine(OutDir, baseline ? "metrics_baseline.txt" : "metrics_optimized.txt"), report.ToString());
            Debug.Log("CENOTE_CAPTURE_DONE\n" + report);
            EditorApplication.ExitPlaymode();
        }

        // ------------------------------------------------------------------

        static Camera cam;
        static UIManager ui;
        static DayNightController dayNight;
        static PollutionSimulator pollution;
        static PerspectiveCameraController views;
        static Canvas canvas;
        static UniversalRenderPipelineAsset rp;

        static void Bind()
        {
            cam = Camera.main;
            ui = UnityEngine.Object.FindFirstObjectByType<UIManager>();
            dayNight = UnityEngine.Object.FindFirstObjectByType<DayNightController>();
            pollution = UnityEngine.Object.FindFirstObjectByType<PollutionSimulator>();
            views = UnityEngine.Object.FindFirstObjectByType<PerspectiveCameraController>();
            canvas = ui.GetComponent<Canvas>();
            // Se fuerza el pipeline y el LOD Bias del perfil "Mobile" (el que usa el APK de Android);
            // en el editor el nivel activo sigue la plataforma Standalone ("PC").
            rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            QualitySettings.renderPipeline = rp;
            QualitySettings.lodBias = 1f; // valor del perfil Mobile (ProjectSettings/QualitySettings.asset)
            report.AppendLine($"GPU: {SystemInfo.graphicsDeviceName} | API: {SystemInfo.graphicsDeviceType} | CPU: {SystemInfo.processorType}");
            report.AppendLine($"RP: {((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).name} | LOD Bias: {QualitySettings.lodBias} | Resolución: {W}x{H}");
        }

        static IEnumerator Optimized()
        {
            Bind();
            yield return 1f;

            // ---------- Capturas de interfaz (Entregable 3) ----------
            yield return 2f;
            Shot("ui_01_menu", withUI: true);
            ui.StartExperience();
            yield return 2.2f;
            Shot("ui_02_hud_dia_vista1", true);
            views.GoTo(1, true); yield return 0.5f;
            Shot("ui_03_hud_vista2_limpio", true);
            views.GoTo(2, true); yield return 0.5f;
            Shot("ui_04_hud_vista3_limpio", true);

            pollution.SetImmediate(0.5f); yield return 5f;
            views.GoTo(1, true); yield return 0.3f;
            Shot("ui_05_hud_vista2_medio", true);

            pollution.SetImmediate(1f); yield return 6f;
            Shot("ui_06_hud_vista2_contaminado", true);
            views.GoTo(2, true); yield return 0.3f;
            Shot("ui_07_hud_vista3_contaminado", true);
            views.GoTo(0, true); yield return 0.3f;
            Shot("ui_08_hud_vista1_contaminado", true);

            pollution.SetImmediate(0f);
            dayNight.SetNight(true, instant: true); yield return 6f;
            views.GoTo(1, true); yield return 0.3f;
            Shot("ui_09_hud_noche_limpio", true);
            views.GoTo(0, true); yield return 0.3f;
            Shot("ui_10_hud_noche_vista1", true);
            pollution.SetImmediate(1f); yield return 5f;
            views.GoTo(1, true); yield return 0.3f;
            Shot("ui_11_hud_noche_contaminado", true);
            dayNight.SetNight(false, instant: true);
            pollution.SetImmediate(0f); yield return 1f;
            ui.ShowInfo(true); yield return 0.5f;
            Shot("ui_12_panel_info", true);
            ui.ShowInfo(false);

            // ---------- Shader del agua sin UI (Entregable 5) ----------
            views.GoTo(1, true);
            foreach (var p in new[] { 0f, 0.5f, 1f })
            {
                pollution.SetImmediate(p); yield return 4f;
                Shot($"agua_contaminacion_{Mathf.RoundToInt(p * 100):000}", false);
            }
            pollution.SetImmediate(0f); yield return 1f;

            // ---------- Anti-Aliasing (Entregable 2) ----------
            yield return AntiAliasing();

            // ---------- LOD (Entregable 4) ----------
            yield return LodEvidence();

            // ---------- Métricas a 3 distancias (Entregable 6) ----------
            yield return Metrics("optimizado");
        }

        static IEnumerator Baseline()
        {
            Bind();
            ui.StartExperience();
            rp.useSRPBatcher = false;
            foreach (var g in UnityEngine.Object.FindObjectsByType<LODGroup>()) g.ForceLOD(0);
            yield return 2f;
            yield return Metrics("sin_optimizar");
            rp.useSRPBatcher = true;
            EditorUtility.SetDirty(rp);
        }

        // ------------------------------------------------------------------

        static IEnumerator AntiAliasing()
        {
            var data = cam.GetUniversalAdditionalCameraData();
            views.GoTo(1, true);
            pollution.SetImmediate(0f);
            yield return 2f;
            Time.timeScale = 0f; // misma imagen en todas las técnicas

            var modes = new (string name, int msaa, AntialiasingMode post)[]
            {
                ("sin_aa", 1, AntialiasingMode.None),
                ("msaa_2x", 2, AntialiasingMode.None),
                ("msaa_4x", 4, AntialiasingMode.None),
                ("fxaa", 1, AntialiasingMode.FastApproximateAntialiasing),
                ("taa", 1, AntialiasingMode.TemporalAntiAliasing),
            };
            int savedMsaa = rp.msaaSampleCount;
            report.AppendLine("\n[ANTI-ALIASING] vista Nivel del agua, " + W + "x" + H + " (mediana de 80 frames tras calentamiento)");
            // Pasada de calentamiento: compila variantes de shader y estabiliza relojes de la GPU.
            foreach (var m in modes)
            {
                rp.msaaSampleCount = m.msaa; data.antialiasing = m.post; cam.allowMSAA = m.msaa > 1;
                data.renderPostProcessing = m.post != AntialiasingMode.None;
                var warm = NewRT(m.msaa);
                for (int i = 0; i < 30; i++) { RenderTo(warm, false); yield return null; }
                warm.Release();
            }
            foreach (var m in modes)
            {
                rp.msaaSampleCount = m.msaa;
                data.antialiasing = m.post;
                // FXAA y TAA son pasadas de post-proceso: URP solo las ejecuta con post-proceso activo.
                data.renderPostProcessing = m.post != AntialiasingMode.None;
                data.antialiasingQuality = AntialiasingQuality.High;
                if (m.post == AntialiasingMode.TemporalAntiAliasing) data.taaSettings.quality = TemporalAAQuality.High;
                cam.allowMSAA = m.msaa > 1;
                // TAA necesita historial: varios frames reales con jitter antes de medir.
                var rt = NewRT(m.msaa);
                for (int i = 0; i < 24; i++) { RenderTo(rt, false); yield return null; }
                var times = new List<double>();
                for (int i = 0; i < 80; i++)
                {
                    times.Add(TimedRender(rt));
                    yield return null;
                }
                Save(rt, $"aa_{m.name}");
                long fbBytes = (long)W * H * (4 + 4) * m.msaa;            // color RGBA8 + depth/stencil 32 bits por muestra
                long extra = m.post == AntialiasingMode.TemporalAntiAliasing ? (long)W * H * (8 + 4) : 0; // historial + vectores de movimiento
                report.AppendLine($"{m.name}: render+sync mediana {Median(times):F2} ms (min {times.Min():F2}), framebuffer estimado {(fbBytes + extra) / 1048576f:F1} MB");
                rt.Release();
            }
            rp.msaaSampleCount = savedMsaa;
            data.antialiasing = AntialiasingMode.None;
            data.renderPostProcessing = false;
            Time.timeScale = 1f;
        }

        static IEnumerator LodEvidence()
        {
            var group = UnityEngine.Object.FindObjectsByType<LODGroup>().OrderBy(g => g.name).First();
            var lods = group.GetLODs();
            report.AppendLine($"\n[LOD] objeto {group.name} size={group.size:F2} escala={group.transform.lossyScale.x:F2}");
            // En Play Mode el static batching sustituye las mallas por "Combined Mesh": se leen las del prefab.
            var prefabName = group.name.Substring(0, group.name.LastIndexOf('_'));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/{prefabName}.prefab");
            var prefabLods = prefab.GetComponent<LODGroup>().GetLODs();
            for (int i = 0; i < prefabLods.Length; i++)
            {
                var mesh = prefabLods[i].renderers[0].GetComponent<MeshFilter>().sharedMesh;
                report.AppendLine($"LOD{i}: transición {prefabLods[i].screenRelativeTransitionHeight * 100f:F1}% | {mesh.name} | tris {mesh.triangles.Length / 3} | verts {mesh.vertexCount}");
            }

            // Aislar la roca: el resto de renderers se ocultan temporalmente.
            var hidden = UnityEngine.Object.FindObjectsByType<Renderer>().Where(r => r.enabled && !r.transform.IsChildOf(group.transform)).ToList();
            foreach (var r in hidden) r.enabled = false;
            var savedClear = cam.clearFlags; var savedBg = cam.backgroundColor; var savedFov = cam.fieldOfView;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.93f, 0.93f, 0.93f);
            cam.fieldOfView = 50f;
            var center = group.transform.TransformPoint(group.localReferencePoint);
            float worldSize = group.size * group.transform.lossyScale.x;
            float half = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

            // Altura relativa en pantalla objetivo para caer en cada banda del LOD Group.
            var targets = new[] { (0.60f, "cerca"), (0.22f, "media"), (0.06f, "lejos"), (0.006f, "culled") };
            var rt = NewRT(1);
            foreach (var (rel, label) in targets)
            {
                // Unity multiplica la altura relativa por el LOD Bias antes de compararla con los umbrales.
                float dist = worldSize * QualitySettings.lodBias / (2f * half * rel);
                var dir = (cam.transform.position - center); dir.y = Mathf.Max(dir.y, 0.3f * dir.magnitude);
                cam.transform.position = center + dir.normalized * dist;
                cam.transform.LookAt(center);
                yield return null;
                GL.wireframe = false; RenderTo(rt, false);
                int active = -1;
                for (int i = 0; i < lods.Length; i++) if (lods[i].renderers[0].isVisible) active = i;
                Save(rt, $"lod_{label}_solido");
                GL.wireframe = true; RenderTo(rt, false); GL.wireframe = false;
                Save(rt, $"lod_{label}_alambre");
                report.AppendLine($"Distancia {dist:F1} m (altura en pantalla {rel * 100f:F1}%): LOD activo = {(active < 0 ? "Culled" : "LOD" + active)}");
                yield return null;
            }
            rt.Release();
            foreach (var r in hidden) r.enabled = true;
            cam.clearFlags = savedClear; cam.backgroundColor = savedBg; cam.fieldOfView = savedFov;
        }

        static IEnumerator Metrics(string label)
        {
            views.GoTo(0, true);
            yield return 1f;
            var far = cam.transform.position;
            var farRot = cam.transform.rotation;
            var water = GameObject.Find("Agua").transform.position;
            var rocks = UnityEngine.Object.FindObjectsByType<LODGroup>();
            var nearRock = rocks.OrderBy(r => Vector3.Distance(r.transform.position, water + new Vector3(8f, 0f, -8f))).First().transform;
            var closeLook = nearRock.position + Vector3.up * 0.6f;
            var closePos = closeLook + (water - closeLook).normalized * 3.5f + Vector3.up * 1.2f;
            var stops = new (string name, Vector3 pos, Quaternion rot)[]
            {
                ("Alejada (panorámica)", far, farRot),
                ("Acercándose (transición)", Vector3.Lerp(far, closePos, 0.55f), Quaternion.Slerp(farRot, Quaternion.LookRotation(closeLook - closePos), 0.55f)),
                ("Acercada (primer plano)", closePos, Quaternion.LookRotation(closeLook - closePos)),
            };

            pollution.SetImmediate(1f); // escenario más costoso: partículas y basura activas
            yield return 4f;

            Profiler.enabled = true;
            var rt = NewRT(rp.msaaSampleCount);
            report.AppendLine($"\n[METRICAS:{label}] SRPBatcher={rp.useSRPBatcher} MSAA={rp.msaaSampleCount}x contaminación=100%");
            report.AppendLine("Vista | ms (render+sync) | FPS equiv. | CPU ms | Batches | SetPass | StaticBatches/Instanced | Tris | Verts | ShadowCasters | RAM total MB | Gfx driver MB");
            foreach (var s in stops)
            {
                cam.transform.SetPositionAndRotation(s.pos, s.rot);
                for (int i = 0; i < 10; i++) { RenderTo(rt, false); yield return null; }
                var total = new List<double>(); var cpu = new List<double>();
                var b = new List<long>(); var sp = new List<long>(); var dc = new List<long>(); var tr = new List<long>(); var vt = new List<long>(); var sc = new List<long>();
                for (int i = 0; i < 60; i++)
                {
                    var sw = Stopwatch.StartNew();
                    RenderTo(rt, false);
                    cpu.Add(sw.Elapsed.TotalMilliseconds);
                    AsyncGPUReadback.Request(rt, 0, 0, 1, 0, 1, 0, 1).WaitForCompletion();
                    total.Add(sw.Elapsed.TotalMilliseconds);
                    // UnityStats expone los mismos contadores que el panel "Rendering" del Profiler.
                    // "Batches" de la ventana Stats = UnityStats.drawCalls; se agregan los lotes estáticos e instanciados.
                    b.Add(UnityStats.drawCalls); sp.Add(UnityStats.setPassCalls); dc.Add(UnityStats.staticBatches * 1000L + UnityStats.instancedBatches);
                    tr.Add(UnityStats.triangles); vt.Add(UnityStats.vertices); sc.Add(UnityStats.shadowCasters);
                    yield return null;
                }
                double ms = Median(total);
                long sb = Mode(dc);
                report.AppendLine($"{s.name} | {ms:F2} | {1000.0 / ms:F0} | {Median(cpu):F2} | {Mode(b)} | {Mode(sp)} | {sb / 1000}/{sb % 1000} | {Mode(tr)} | {Mode(vt)} | {Mode(sc)} | " +
                                  $"{Profiler.GetTotalAllocatedMemoryLong() / 1048576f:F0} | {Profiler.GetAllocatedMemoryForGraphicsDriver() / 1048576f:F0}");
                Save(rt, $"metricas_{label}_{Array.IndexOf(stops, s)}");
            }
            rt.Release();
            pollution.SetImmediate(0f);
        }

        // ------------------------------------------------------------------

        static RenderTexture NewRT(int msaa)
        {
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = Mathf.Max(1, msaa) };
            rt.Create();
            return rt;
        }

        static void RenderTo(RenderTexture rt, bool withUI)
        {
            canvas.renderMode = withUI ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = withUI ? cam : null;
            canvas.planeDistance = 0.5f;
            canvas.enabled = withUI;
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            cam.targetTexture = null;
            canvas.enabled = true;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        static double TimedRender(RenderTexture rt)
        {
            var sw = Stopwatch.StartNew();
            RenderTo(rt, false);
            AsyncGPUReadback.Request(rt, 0, 0, 1, 0, 1, 0, 1).WaitForCompletion();
            return sw.Elapsed.TotalMilliseconds;
        }

        static void Shot(string name, bool withUI)
        {
            var rt = NewRT(rp.msaaSampleCount);
            RenderTo(rt, withUI);
            RenderTo(rt, withUI);
            Save(rt, name);
            rt.Release();
        }

        static void Save(RenderTexture rt, string name)
        {
            var resolved = RenderTexture.GetTemporary(rt.width, rt.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(rt, resolved);
            var prev = RenderTexture.active;
            RenderTexture.active = resolved;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(resolved);
            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        static double Median(List<double> v) { var s = v.OrderBy(x => x).ToList(); return s[s.Count / 2]; }
        static long Mode(List<long> v) => v.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key;
    }
}
