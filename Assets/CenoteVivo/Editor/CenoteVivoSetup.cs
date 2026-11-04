using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CenoteVivo.EditorTools
{
    // Construye la escena "CenoteVivo" a partir de una copia de CENOTE.unity (el original no se toca):
    // materiales y shaders, rocas con LOD Group, efectos de contaminación, luz día/noche,
    // puntos de vista de cámara e interfaz completa. Ejecutable desde el menú o en batchmode.
    public static class CenoteVivoSetup
    {
        const string Root = "Assets/CenoteVivo";
        const string SourceScene = "Assets/Scenes/CENOTE.unity";
        public const string ScenePath = Root + "/Scenes/CenoteVivo.unity";
        const string MatDir = Root + "/Materials";
        const string PrefabDir = Root + "/Prefabs";
        const string TexDir = Root + "/Textures";

        // LOD Group: 100–32.5 % LOD0, 32.5–12.5 % LOD1, 12.5–1 % LOD2, < 1 % culled.
        static readonly float[] LodTransitions = { 0.325f, 0.125f, 0.01f };

        static readonly Color Teal = new Color(0.043f, 0.239f, 0.271f, 0.86f);
        static readonly Color Accent = new Color(0.18f, 0.77f, 0.71f);
        static Font font;

        [MenuItem("Cenote Vivo/Construir escena")]
        public static void Run()
        {
            foreach (var d in new[] { Root + "/Scenes", MatDir, PrefabDir, TexDir })
                Directory.CreateDirectory(d);
            AssetDatabase.Refresh();
            foreach (var guid in AssetDatabase.FindAssets("Filtracion_ t:Mesh", new[] { PrefabDir }))
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));

            ConfigureRenderPipeline();
            var normals = PrepareTextures();
            var mats = CreateMaterials(normals);
            var rockPrefabs = CreateRockPrefabs(mats["roca"]);

            AssetDatabase.DeleteAsset(ScenePath);
            AssetDatabase.CopyAsset(SourceScene, ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var water = GameObject.Find("Plane");
            water.name = "Agua";
            water.GetComponent<Renderer>().sharedMaterial = mats["agua"];
            water.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var cave = GameObject.Find("CENOTE/Cueva");
            cave.GetComponent<Renderer>().sharedMaterial = mats["cueva"];
            GameObject.Find("CENOTE/Plataforma").GetComponent<Renderer>().sharedMaterial = mats["plataforma"];

            var tempColliders = AddTempColliders();
            var sun = CreateSun();
            var rocks = PlaceRocks(rockPrefabs, water.transform.position);
            var effects = CreatePollutionEffects(mats, water.transform.position);
            var bio = CreateBioluminescence(mats, water.transform.position);
            foreach (var c in tempColliders) Object.DestroyImmediate(c);

            MarkStatic(rocks);

            var cam = Camera.main;
            // La cámara original del proyecto (vista desde la boca del cenote) se conserva como Vista 1.
            var originalPose = (cam.transform.position, cam.transform.rotation, cam.fieldOfView);
            cam.GetComponent<Transform>().localScale = Vector3.one;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 400f;
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.antialiasing = AntialiasingMode.None;
            camData.renderPostProcessing = false;

            var systems = new GameObject("CenoteVivo_Sistemas");
            var dayNight = systems.AddComponent<DayNightController>();
            var pollution = systems.AddComponent<PollutionSimulator>();
            var fog = systems.AddComponent<UnderwaterFog>();
            var debris = effects.debris;

            Set(dayNight, "sun", sun);
            Set(dayNight, "targetCamera", cam);
            Set(dayNight, "bioluminescence", bio.ps);
            Set(dayNight, "bioluminescenceLight", bio.light);
            Set(dayNight, "pollution", pollution);

            Set(pollution, "waterRenderer", water.GetComponent<Renderer>());
            SetArray(pollution, "leachates", effects.leachates.Cast<Object>().ToArray());
            Set(pollution, "floatingTrash", effects.trash);
            Set(pollution, "debris", debris);

            Set(fog, "cam", cam);
            Set(fog, "waterSurface", water.transform);
            Set(fog, "pollution", pollution);
            Set(fog, "dayNight", dayNight);

            var camController = cam.gameObject.AddComponent<PerspectiveCameraController>();
            var pivot = new GameObject("Pivote_Orbita").transform;
            pivot.position = water.transform.position;
            CreateViews(camController, cam, water.transform.position, pivot, originalPose);

            var ui = BuildUI(dayNight, pollution, camController);
            ui.transform.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("CENOTE_SETUP_OK");
        }

        // ---------- Render pipeline ----------

        static void ConfigureRenderPipeline()
        {
            foreach (var path in new[] { "Assets/Settings/Mobile_RPAsset.asset", "Assets/Settings/PC_RPAsset.asset" })
            {
                var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (rp == null) continue;
                // El agua necesita profundidad (absorción) y la imagen opaca (refracción ligera).
                rp.supportsCameraDepthTexture = true;
                rp.supportsCameraOpaqueTexture = true;
                var so = new SerializedObject(rp);
                so.FindProperty("m_OpaqueDownsampling").intValue = (int)Downsampling._2xBilinear;
                so.ApplyModifiedPropertiesWithoutUndo();
                rp.useSRPBatcher = true;
                if (path.Contains("Mobile"))
                {
                    rp.msaaSampleCount = 4;
                    rp.shadowDistance = 60f;
                }
                EditorUtility.SetDirty(rp);
            }
        }

        // ---------- Texturas ----------

        static Dictionary<string, Texture2D> PrepareTextures()
        {
            var result = new Dictionary<string, Texture2D>();
            result["roca"] = MakeNormalFromHeight("Assets/Textures/Rocas.png", TexDir + "/Rocas_normal.png", 0.06f);
            result["cueva"] = MakeNormalFromHeight("Assets/Textures/Cueva.jpg", TexDir + "/Cueva_normal.jpg", 0.05f);
            result["agua"] = MakeNormalFromHeight("Assets/Textures/water.png", TexDir + "/Agua_normal.png", 0.12f);
            foreach (var path in new[] { "Assets/Textures/Rocas.png", "Assets/Textures/Cueva.jpg", "Assets/Textures/water.png",
                                         "Assets/Textures/Escaleras.jpg", "Assets/Textures/Wood 1.jpg", "Assets/Textures/Wood.jpg" })
                SetAndroidASTC(path, 1024);
            return result;
        }

        // Copia la textura de color y la importa como mapa de normales generado desde la escala de grises.
        static Texture2D MakeNormalFromHeight(string src, string dst, float bumpiness)
        {
            AssetDatabase.DeleteAsset(dst);
            AssetDatabase.CopyAsset(src, dst);
            var imp = (TextureImporter)AssetImporter.GetAtPath(dst);
            imp.textureType = TextureImporterType.NormalMap;
            imp.convertToNormalmap = true;
            imp.heightmapScale = bumpiness;
            imp.normalmapFilter = TextureImporterNormalFilter.Sobel;
            imp.mipmapEnabled = true;
            imp.maxTextureSize = 512;
            imp.SaveAndReimport();
            SetAndroidASTC(dst, 512);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        }

        static void SetAndroidASTC(string path, int maxSize)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            var s = imp.GetPlatformTextureSettings("Android");
            s.overridden = true;
            s.maxTextureSize = maxSize;
            s.format = imp.textureType == TextureImporterType.NormalMap ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6;
            s.compressionQuality = 50;
            imp.SetPlatformTextureSettings(s);
            imp.SaveAndReimport();
        }

        // ---------- Materiales ----------

        static Dictionary<string, Material> CreateMaterials(Dictionary<string, Texture2D> normals)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var simple = Shader.Find("Universal Render Pipeline/Simple Lit");
            var m = new Dictionary<string, Material>();

            m["agua"] = Save(new Material(Shader.Find("CenoteVivo/CenoteWater")), "M_CenoteAgua");
            m["agua"].SetTexture("_NormalMap", normals["agua"]);

            m["roca"] = RockMaterial(lit, "M_Roca", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Rocas.png"), normals["roca"], 1f, 0.12f);
            m["cueva"] = RockMaterial(lit, "M_Cueva", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Cueva.jpg"), normals["cueva"], 1f, 0.1f);
            var plat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/15137168371673791376rock-texture.hi.mat");
            m["plataforma"] = RockMaterial(lit, "M_Plataforma", plat != null ? (Texture2D)plat.GetTexture("_BaseMap") : null, normals["roca"], 0.8f, 0.15f);
            if (plat != null) m["plataforma"].SetTextureScale("_BaseMap", plat.GetTextureScale("_BaseMap"));

            m["filtracion"] = Save(new Material(Shader.Find("CenoteVivo/CenoteSeep")), "M_Filtracion");
            m["filtracion"].enableInstancing = true;

            m["lixiviado"] = Particle("M_Lixiviado", new Color(0.33f, 0.24f, 0.07f, 0.85f), additive: false);
            m["residuo"] = Particle("M_Residuo", new Color(0.24f, 0.25f, 0.12f, 0.75f), additive: false);
            m["bio"] = Particle("M_Bioluminiscencia", new Color(0.2f, 1f, 0.85f, 1f), additive: true);

            m["botella"] = Save(new Material(simple), "M_Basura_Botella");
            m["botella"].SetColor("_BaseColor", new Color(0.55f, 0.75f, 0.7f));
            m["botella"].enableInstancing = true;
            m["bolsa"] = Save(new Material(simple), "M_Basura_Bolsa");
            m["bolsa"].SetColor("_BaseColor", new Color(0.82f, 0.8f, 0.72f));
            m["bolsa"].enableInstancing = true;
            m["lata"] = Save(new Material(simple), "M_Basura_Lata");
            m["lata"].SetColor("_BaseColor", new Color(0.7f, 0.18f, 0.12f));
            m["lata"].enableInstancing = true;

            AssetDatabase.SaveAssets();
            return m;
        }

        static Material RockMaterial(Shader lit, string name, Texture2D albedo, Texture2D normal, float bumpScale, float smoothness)
        {
            var mat = new Material(lit);
            if (albedo != null) mat.SetTexture("_BaseMap", albedo);
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", bumpScale);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            // Sin reflejos especulares de entorno en roca: ahorra muestras del cubemap.
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            mat.enableInstancing = true;
            return Save(mat, name);
        }

        static Material Particle(string name, Color tint, bool additive)
        {
            var mat = new Material(Shader.Find("CenoteVivo/ParticleSoft"));
            mat.SetColor("_Tint", tint);
            mat.SetFloat("_SrcBlend", (float)BlendMode.One); // el shader entrega color premultiplicado
            mat.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            return Save(mat, name);
        }

        static Material Save(Material mat, string name)
        {
            string path = $"{MatDir}/{name}.mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ---------- Rocas con LOD ----------

        static List<GameObject> CreateRockPrefabs(Material rockMat)
        {
            const string fbx = "Assets/Models/Rocas.fbx";
            var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.meshCompression = ModelImporterMeshCompression.Medium;
            imp.isReadable = false;
            imp.importNormals = ModelImporterNormals.Import;
            imp.importAnimation = false;
            imp.SaveAndReimport();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var prefabs = new List<GameObject>();
            foreach (var name in new[] { "roca", "roca2" })
            {
                var src = model.transform.Find(name);
                var go = Object.Instantiate(src.gameObject);
                go.name = name;
                go.transform.localPosition = Vector3.zero;
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                {
                    r.sharedMaterial = rockMat;
                    r.shadowCastingMode = ShadowCastingMode.On;
                }

                var group = go.GetComponent<LODGroup>();
                if (group == null) group = go.AddComponent<LODGroup>();
                var lods = new LOD[3];
                for (int i = 0; i < 3; i++)
                {
                    var child = go.transform.Find($"{name}_LOD{i}");
                    lods[i] = new LOD(LodTransitions[i], new Renderer[] { child.GetComponent<Renderer>() });
                    // LOD2 no proyecta sombra: a esa distancia la sombra no aporta y ahorra un draw call.
                    if (i == 2) child.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
                group.SetLODs(lods);
                group.fadeMode = LODFadeMode.None;
                group.RecalculateBounds();

                string path = $"{PrefabDir}/{name}.prefab";
                prefabs.Add(PrefabUtility.SaveAsPrefabAsset(go, path));
                Object.DestroyImmediate(go);
            }
            return prefabs;
        }

        static List<Collider> AddTempColliders()
        {
            var list = new List<Collider>();
            foreach (var path in new[] { "CENOTE/Cueva", "CENOTE/Plataforma" })
                list.Add(GameObject.Find(path).AddComponent<MeshCollider>());
            Physics.queriesHitBackfaces = true;
            Physics.SyncTransforms();
            return list;
        }

        static GameObject PlaceRocks(List<GameObject> prefabs, Vector3 waterCenter)
        {
            var parent = new GameObject("Rocas_LOD");
            var rng = new System.Random(7);
            int placed = 0;
            var platform = GameObject.Find("CENOTE/Plataforma").GetComponent<Renderer>().bounds.center;
            for (int i = 0; i < 40 && placed < 16; i++)
            {
                float angle = i / 16f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.25f;
                float radius = 12f + (float)rng.NextDouble() * 3f;
                // El rayo nace dentro de la caverna, por debajo del borde superior, para no caer en el exterior.
                var origin = platform + new Vector3(Mathf.Cos(angle) * radius, 6f, Mathf.Sin(angle) * radius);
                if (!Physics.Raycast(origin, Vector3.down, out var hit, 12f)) continue;
                // Solo sobre la plataforma que rodea el agua, en superficies casi horizontales.
                if (hit.normal.y < 0.6f || hit.point.y < waterCenter.y - 0.5f || hit.point.y > waterCenter.y + 3f) continue;
                var prefab = prefabs[i % prefabs.Count];
                var rock = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
                rock.name = $"{prefab.name}_{placed:00}";
                rock.transform.position = hit.point - Vector3.up * 0.15f;
                rock.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                rock.transform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.5f, (float)rng.NextDouble());
                placed++;
            }
            Debug.Log($"Rocas colocadas: {placed}");
            return parent;
        }

        static void MarkStatic(GameObject rocks)
        {
            var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
            foreach (var name in new[] { "CENOTE", "Muelle", "Escaleras", "Lianas" })
                foreach (var t in GameObject.Find(name).GetComponentsInChildren<Transform>())
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
            foreach (var t in rocks.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        }

        // ---------- Luz ----------

        static Light CreateSun()
        {
            var go = new GameObject("Sol");
            var sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Hard;
            sun.shadowStrength = 0.85f;
            sun.intensity = 2.2f;
            sun.useColorTemperature = true;
            sun.colorTemperature = 5600f;
            go.transform.rotation = Quaternion.Euler(62f, 35f, 0f);
            RenderSettings.sun = sun;
            return sun;
        }

        // ---------- Contaminación ----------

        struct PollutionFx
        {
            public ParticleSystem[] leachates;
            public ParticleSystem trash;
            public FloatingDebris debris;
        }

        static PollutionFx CreatePollutionEffects(Dictionary<string, Material> mats, Vector3 waterCenter)
        {
            var parent = new GameObject("Contaminacion");
            var seepRoot = new GameObject("Filtraciones_Pared");
            seepRoot.transform.SetParent(parent.transform);
            var leachates = new List<ParticleSystem>();

            const int count = 10;
            for (int i = 0; i < count; i++)
            {
                float a = (i + 0.35f) / count * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var center = new Vector3(waterCenter.x, 0f, waterCenter.z);
                if (!Physics.Raycast(center + Vector3.up * 58f, dir, out var top, 60f)) continue;
                if (!Physics.Raycast(center + Vector3.up * (waterCenter.y + 2.5f), dir, out var bottom, 60f))
                    if (!Physics.Raycast(center + Vector3.up * (waterCenter.y + 6f), dir, out bottom, 60f)) continue;

                var seep = new GameObject($"Filtracion_{i:00}");
                seep.transform.SetParent(seepRoot.transform);
                seep.AddComponent<MeshFilter>().sharedMesh = WallStripMesh(top, bottom, 1.4f + (i % 3) * 0.6f);
                var mr = seep.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mats["filtracion"];
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;

                var drip = MakeParticles($"Lixiviado_{i:00}", mats["lixiviado"], parent.transform);
                drip.transform.position = bottom.point + bottom.normal * 0.15f;
                drip.transform.rotation = Quaternion.LookRotation(Vector3.down);
                var main = drip.main;
                main.startLifetime = 1.6f;
                main.startSpeed = 0.4f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
                main.gravityModifier = 0.35f;
                main.maxParticles = 60;
                var shape = drip.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 8f;
                shape.radius = 0.6f;
                leachates.Add(drip);
            }

            var trash = MakeParticles("Residuos_Superficie", mats["residuo"], parent.transform);
            trash.transform.position = waterCenter + Vector3.up * 0.04f;
            trash.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            {
                var main = trash.main;
                main.startLifetime = 14f;
                main.startSpeed = 0.05f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 2.2f);
                main.maxParticles = 120;
                var shape = trash.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 11f;
                var r = trash.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
                var col = trash.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
            }

            var debrisGo = new GameObject("Basura_Flotante");
            debrisGo.transform.SetParent(parent.transform);
            debrisGo.transform.position = waterCenter;
            var debris = debrisGo.AddComponent<FloatingDebris>();
            var so = new SerializedObject(debris);
            var kinds = so.FindProperty("kinds");
            kinds.arraySize = 3;
            SetKind(kinds.GetArrayElementAtIndex(0), BuiltinMesh("Cylinder"), mats["botella"], new Vector3(0.12f, 0.16f, 0.12f), 90f);
            SetKind(kinds.GetArrayElementAtIndex(1), BuiltinMesh("Cube"), mats["bolsa"], new Vector3(0.55f, 0.03f, 0.45f), 0f);
            SetKind(kinds.GetArrayElementAtIndex(2), BuiltinMesh("Cylinder"), mats["lata"], new Vector3(0.08f, 0.07f, 0.08f), 90f);
            so.FindProperty("radius").floatValue = 10.5f;
            so.FindProperty("count").intValue = 26;
            so.ApplyModifiedPropertiesWithoutUndo();

            return new PollutionFx { leachates = leachates.ToArray(), trash = trash, debris = debris };
        }

        static void SetKind(SerializedProperty p, Mesh mesh, Material mat, Vector3 scale, float tilt)
        {
            p.FindPropertyRelative("mesh").objectReferenceValue = mesh;
            p.FindPropertyRelative("material").objectReferenceValue = mat;
            p.FindPropertyRelative("scale").vector3Value = scale;
            p.FindPropertyRelative("tilt").floatValue = tilt;
        }

        static Mesh BuiltinMesh(string primitive)
        {
            var tmp = GameObject.CreatePrimitive(primitive == "Cube" ? PrimitiveType.Cube : PrimitiveType.Cylinder);
            var mesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
            return mesh;
        }

        // Franja que sigue la pared entre dos impactos (arriba/abajo), separada por la normal.
        static Mesh WallStripMesh(RaycastHit top, RaycastHit bottom, float width)
        {
            Vector3 side = Vector3.Cross(Vector3.up, top.normal).normalized * (width * 0.5f);
            Vector3 t = top.point + top.normal * 0.12f;
            Vector3 b = bottom.point + bottom.normal * 0.12f;
            var mesh = new Mesh { name = "FiltracionStrip" };
            mesh.vertices = new[] { b - side, b + side, t - side, t + side };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string path = $"{PrefabDir}/Filtracion_{Mathf.Abs(top.point.GetHashCode()):X}.asset";
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static ParticleSystem MakeParticles(string name, Material mat, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            // El shader ParticleSoft dibuja el círculo a partir de las UV: hay que enviarlas explícitamente.
            r.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
                { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV });
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static (ParticleSystem ps, Light light) CreateBioluminescence(Dictionary<string, Material> mats, Vector3 waterCenter)
        {
            var ps = MakeParticles("Bioluminiscencia", mats["bio"], null);
            ps.transform.position = waterCenter + Vector3.up * 0.4f;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = 0.08f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
            main.maxParticles = 200;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(22f, 0.6f, 20f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 0.3f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var lightGo = new GameObject("Bioluminiscencia_Luz");
            lightGo.transform.position = waterCenter + Vector3.up * 0.8f;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.2f, 1f, 0.85f);
            light.range = 18f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            return (ps, light);
        }

        // ---------- Cámaras ----------

        static void CreateViews(PerspectiveCameraController controller, Camera cam, Vector3 water, Transform pivot,
                                (Vector3 pos, Quaternion rot, float fov) original)
        {
            var root = new GameObject("Vistas_Camara").transform;
            Transform Anchor(string name, Vector3 pos, Vector3 lookAt)
            {
                var t = new GameObject(name).transform;
                t.SetParent(root);
                t.position = pos;
                t.rotation = Quaternion.LookRotation(lookAt - pos, Vector3.up);
                return t;
            }

            var v1 = Anchor("Vista_1_Panoramica", original.pos, original.pos + original.rot * Vector3.forward);
            var v2 = Anchor("Vista_2_NivelAgua", water + new Vector3(8.5f, 0.9f, -7.5f), water + new Vector3(-9f, 2.5f, 7f));
            var v3 = Anchor("Vista_3_Subterranea", water + new Vector3(3f, -4.5f, -6f), water + new Vector3(-4f, -1.2f, 9f));

            var so = new SerializedObject(controller);
            so.FindProperty("cam").objectReferenceValue = cam;
            so.FindProperty("orbitPivot").objectReferenceValue = pivot;
            var views = so.FindProperty("views");
            views.arraySize = 3;
            void SetView(int i, string label, Transform anchor, float fov)
            {
                var p = views.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("label").stringValue = label;
                p.FindPropertyRelative("anchor").objectReferenceValue = anchor;
                p.FindPropertyRelative("fieldOfView").floatValue = fov;
            }
            SetView(0, "Panorámica", v1, original.fov);
            SetView(1, "Nivel del agua", v2, 62f);
            SetView(2, "Subterránea", v3, 70f);
            so.ApplyModifiedPropertiesWithoutUndo();
            cam.transform.SetPositionAndRotation(v1.position, v1.rotation);
            cam.fieldOfView = original.fov;
        }

        // ---------- Interfaz ----------

        static GameObject BuildUI(DayNightController dayNight, PollutionSimulator pollution, PerspectiveCameraController cameras)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGo = new GameObject("UI_CenoteVivo", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var safe = Rect("AreaSegura", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeArea>();

            // --- Menú de inicio ---
            var menu = Panel("Menu_Inicio", canvasGo.transform, new Color(0.01f, 0.08f, 0.1f, 0.62f));
            Stretch(menu);
            var menuCol = Rect("Columna", menu, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-700, -380), new Vector2(700, 380));
            var title = Label("Titulo", menuCol, 150, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            Place(title.rectTransform, 0, 1, 1, 1, 0, -230, 0, 0);
            var subtitle = Label("Subtitulo", menuCol, 46, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(0.65f, 0.93f, 0.9f));
            Place(subtitle.rectTransform, 0, 1, 1, 1, 0, -320, 0, -230);
            var divider = Panel("Divisor", menuCol, Accent);
            Place(divider, 0.5f, 1, 0.5f, 1, -120, -350, 120, -344);
            var phraseRect = Rect("Frase", menuCol, new Vector2(0, 1), new Vector2(1, 1), new Vector2(60, -540), new Vector2(-60, -380));
            var phraseGroup = phraseRect.gameObject.AddComponent<CanvasGroup>();
            var phrase = Label("Texto", phraseRect, 44, FontStyle.Normal, TextAnchor.MiddleCenter, Color.white);
            Stretch(phrase.rectTransform);
            var start = MakeButton("Boton_Explorar", menuCol, "Explorar el cenote", 46, Accent, new Color(0.02f, 0.13f, 0.15f));
            Place((RectTransform)start.transform, 0.5f, 0, 0.5f, 0, -260, 40, 260, 160);
            var credit = Label("Credito", menu, 26, FontStyle.Normal, TextAnchor.LowerCenter, new Color(1, 1, 1, 0.6f));
            credit.text = "Proyecto Cenote Vivo · Gráficas por Computadora";
            Place(credit.rectTransform, 0, 0, 1, 0, 0, 24, 0, 70);

            // --- HUD ---
            var hud = Rect("HUD", safe, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var banner = Panel("Banner", hud, Teal);
            Place(banner, 0, 1, 1, 1, 24, -130, -24, -20);
            var bannerText = Label("Texto", banner, 34, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white);
            Place(bannerText.rectTransform, 0, 0, 1, 1, 36, 0, -360, 0);
            var info = MakeButton("Boton_Info", banner, "¿Por qué importa?", 32, Accent, new Color(0.02f, 0.13f, 0.15f));
            Place((RectTransform)info.transform, 1, 0.5f, 1, 0.5f, -340, -42, -20, 42);

            var bar = Panel("Barra_Controles", hud, Teal);
            Place(bar, 0, 0, 1, 0, 24, 20, -24, 230);

            var dn = MakeButton("Boton_DiaNoche", bar, "", 34, new Color(0.96f, 0.78f, 0.35f), new Color(0.15f, 0.1f, 0.02f));
            Place((RectTransform)dn.transform, 0, 0.5f, 0, 0.5f, 28, -75, 318, 75);
            var dnCaption = Label("Titulo", dn.transform, 24, FontStyle.Normal, TextAnchor.UpperCenter, new Color(0.15f, 0.1f, 0.02f));
            dnCaption.text = "Iluminación";
            Place(dnCaption.rectTransform, 0, 1, 1, 1, 0, -44, 0, -8);
            var dnLabel = dn.GetComponentInChildren<Text>();
            dnLabel.fontSize = 44; dnLabel.fontStyle = FontStyle.Bold;
            Place(dnLabel.rectTransform, 0, 0, 1, 1, 0, 0, 0, -30);

            var poll = Rect("Contaminacion", bar, new Vector2(0, 0), new Vector2(1, 1), new Vector2(350, 16), new Vector2(-350, -16));
            var pollLabel = Label("Estado", poll, 34, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);
            Place(pollLabel.rectTransform, 0, 1, 1, 1, 10, -50, -260, 0);
            var slider = MakeSlider(poll, out var fill);
            Place((RectTransform)slider.transform, 0, 0.5f, 1, 0.5f, 10, -22, -260, 30);
            var limpio = Label("Min", poll, 22, FontStyle.Normal, TextAnchor.UpperLeft, new Color(1, 1, 1, 0.7f));
            limpio.text = AwarenessContent.CleanState;
            Place(limpio.rectTransform, 0, 0.5f, 0.5f, 0.5f, 10, -60, 0, -26);
            var sucio = Label("Max", poll, 22, FontStyle.Normal, TextAnchor.UpperRight, new Color(1, 1, 1, 0.7f));
            sucio.text = AwarenessContent.PollutedState;
            Place(sucio.rectTransform, 0.5f, 0.5f, 1, 0.5f, 0, -60, -260, -26);
            var caption = Label("Descripcion", poll, 26, FontStyle.Italic, TextAnchor.LowerLeft, new Color(0.8f, 0.95f, 0.93f));
            Place(caption.rectTransform, 0, 0, 1, 0, 10, 0, -260, 44);
            var toggle = MakeButton("Boton_Contaminar", poll, "Contaminar", 34, new Color(0.78f, 0.55f, 0.18f), Color.white);
            Place((RectTransform)toggle.transform, 1, 0.5f, 1, 0.5f, -240, -70, 0, 70);

            var vb = MakeButton("Boton_Vista", bar, "", 34, new Color(0.85f, 0.93f, 0.95f), new Color(0.02f, 0.13f, 0.15f));
            Place((RectTransform)vb.transform, 1, 0.5f, 1, 0.5f, -318, -75, -28, 75);
            var vCaption = Label("Titulo", vb.transform, 24, FontStyle.Normal, TextAnchor.UpperCenter, new Color(0.02f, 0.13f, 0.15f));
            vCaption.text = "Cambiar vista  >";
            Place(vCaption.rectTransform, 0, 1, 1, 1, 0, -44, 0, -8);
            var vLabel = vb.GetComponentInChildren<Text>();
            vLabel.fontSize = 36; vLabel.fontStyle = FontStyle.Bold;
            Place(vLabel.rectTransform, 0, 0, 1, 1, 0, 0, 0, -30);

            // --- Panel informativo ---
            var infoPanel = Panel("Panel_Info", canvasGo.transform, new Color(0, 0, 0, 0.55f));
            Stretch(infoPanel);
            var card = Panel("Tarjeta", infoPanel, new Color(0.03f, 0.16f, 0.18f, 0.97f));
            Place(card, 0.5f, 0.5f, 0.5f, 0.5f, -760, -470, 760, 470);
            var infoTitle = Label("Titulo", card, 50, FontStyle.Bold, TextAnchor.MiddleLeft, Accent);
            Place(infoTitle.rectTransform, 0, 1, 1, 1, 50, -110, -300, -20);
            var close = MakeButton("Boton_Cerrar", card, "Cerrar", 34, Accent, new Color(0.02f, 0.13f, 0.15f));
            Place((RectTransform)close.transform, 1, 1, 1, 1, -260, -105, -40, -25);
            var scroll = MakeScroll(card, out var body);
            Place((RectTransform)scroll.transform, 0, 0, 1, 1, 50, 40, -40, -130);

            var mgr = canvasGo.AddComponent<UIManager>();
            Set(mgr, "dayNight", dayNight);
            Set(mgr, "pollution", pollution);
            Set(mgr, "cameras", cameras);
            Set(mgr, "menuPanel", menu.gameObject);
            Set(mgr, "titleText", title);
            Set(mgr, "subtitleText", subtitle);
            Set(mgr, "phraseText", phrase);
            Set(mgr, "phraseGroup", phraseGroup);
            Set(mgr, "startButton", start);
            Set(mgr, "hudPanel", hud.gameObject);
            Set(mgr, "bannerText", bannerText);
            Set(mgr, "infoButton", info);
            Set(mgr, "dayNightButton", dn);
            Set(mgr, "dayNightLabel", dnLabel);
            Set(mgr, "viewButton", vb);
            Set(mgr, "viewLabel", vLabel);
            Set(mgr, "pollutionSlider", slider);
            Set(mgr, "pollutionLabel", pollLabel);
            Set(mgr, "pollutionCaption", caption);
            Set(mgr, "pollutionToggle", toggle);
            Set(mgr, "pollutionToggleLabel", toggle.GetComponentInChildren<Text>());
            Set(mgr, "pollutionFill", fill);
            Set(mgr, "infoPanel", infoPanel.gameObject);
            Set(mgr, "infoTitle", infoTitle);
            Set(mgr, "infoBody", body);
            Set(mgr, "closeInfoButton", close);
            Set(mgr, "infoScroll", scroll);

            // Texto inicial para que la escena se vea completa también en el editor.
            title.text = AwarenessContent.Title;
            subtitle.text = AwarenessContent.Subtitle;
            phrase.text = AwarenessContent.ImpactPhrases[0];
            bannerText.text = AwarenessContent.Banner;
            infoTitle.text = AwarenessContent.InfoTitle;
            body.text = AwarenessContent.InfoBody;
            dnLabel.text = "Día";
            vLabel.text = "Panorámica";
            pollLabel.text = "Estado del cenote: Limpio (0%)";
            caption.text = AwarenessContent.PollutionCaption(0f);

            hud.gameObject.SetActive(false);
            infoPanel.gameObject.SetActive(false);
            return canvasGo;
        }

        static RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
            return rt;
        }

        static void Place(RectTransform rt, float axMin, float ayMin, float axMax, float ayMax, float l, float b, float r, float t)
        {
            rt.anchorMin = new Vector2(axMin, ayMin);
            rt.anchorMax = new Vector2(axMax, ayMax);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(r, t);
        }

        static void Stretch(RectTransform rt) => Place(rt, 0, 0, 1, 1, 0, 0, 0, 0);

        static RectTransform Panel(string name, Transform parent, Color color)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            return rt;
        }

        static Text Label(string name, Transform parent, int size, FontStyle style, TextAnchor anchor, Color color)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = anchor;
            t.color = color;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static Button MakeButton(string name, Transform parent, string text, int size, Color bg, Color fg)
        {
            var rt = Panel(name, parent, bg);
            var btn = rt.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1, 1, 1, 0.92f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;
            var label = Label("Texto", rt, size, FontStyle.Bold, TextAnchor.MiddleCenter, fg);
            label.text = text;
            Stretch(label.rectTransform);
            return btn;
        }

        static Slider MakeSlider(Transform parent, out Image fill)
        {
            var rt = Rect("Slider_Contaminacion", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bg = Panel("Fondo", rt, new Color(1, 1, 1, 0.18f));
            Place(bg, 0, 0.5f, 1, 0.5f, 0, -10, 0, 10);
            var fillArea = Rect("Area_Relleno", rt, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -10), new Vector2(0, 10));
            var fillRt = Panel("Relleno", fillArea, Accent);
            fill = fillRt.GetComponent<Image>();
            var handleArea = Rect("Area_Asa", rt, Vector2.zero, Vector2.one, new Vector2(20, 0), new Vector2(-20, 0));
            var handle = Panel("Asa", handleArea, Color.white);
            handle.sizeDelta = new Vector2(52, 52);
            handle.anchorMin = handle.anchorMax = new Vector2(0, 0.5f);
            handle.GetComponent<Image>().sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            handle.GetComponent<Image>().type = Image.Type.Simple;
            var slider = rt.gameObject.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0; slider.maxValue = 1; slider.value = 0;
            return slider;
        }

        static ScrollRect MakeScroll(Transform parent, out Text body)
        {
            var rt = Rect("Scroll", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var scroll = rt.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Viewport", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Contenido", viewport, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(0, 30, 0, 20);
            body = Label("Cuerpo", content, 31, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.92f, 0.97f, 0.96f));
            body.lineSpacing = 1.1f;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return scroll;
        }

        // ---------- Player / Android ----------

        static void ConfigurePlayer()
        {
            PlayerSettings.productName = "Cenote Vivo";
            PlayerSettings.companyName = "Marista";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.marista.cenotevivo");
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29; // Android 10
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetMobileMTRendering(UnityEditor.Build.NamedBuildTarget.Android, true);
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            EditorUserBuildSettings.buildAppBundle = false;

            // Android usa el perfil de calidad "Mobile" (URP Mobile_RPAsset).
            var names = QualitySettings.names;
            int mobile = System.Array.IndexOf(names, "Mobile");
            if (mobile >= 0)
            {
                var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
                var perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
                for (int i = 0; i < perPlatform.arraySize; i++)
                {
                    var e = perPlatform.GetArrayElementAtIndex(i);
                    if (e.FindPropertyRelative("first").stringValue == "Android")
                        e.FindPropertyRelative("second").intValue = mobile;
                }
                // LOD Bias 1: las transiciones ocurren exactamente en 32.5 % / 12.5 % / 1 % de pantalla.
                var level = so.FindProperty("m_QualitySettings").GetArrayElementAtIndex(mobile);
                level.FindPropertyRelative("lodBias").floatValue = 1f;
                level.FindPropertyRelative("maximumLODLevel").intValue = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ---------- utilidades de serialización ----------

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"Campo {field} no existe en {target.GetType().Name}"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
