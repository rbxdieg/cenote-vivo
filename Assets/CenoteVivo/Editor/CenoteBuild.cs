using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CenoteVivo.EditorTools
{
    // Compilación del APK de Android y de los players de medición (Windows, development build).
    public static class CenoteBuild
    {
        const string ApkPath = "Builds/Android/CenoteVivo.apk";

        [MenuItem("Cenote Vivo/Compilar APK (Android)")]
        public static void BuildApk()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Fail("Falta el módulo 'Android Build Support' (con OpenJDK y Android SDK & NDK Tools). " +
                     "Instálalo desde Unity Hub > Installs > 6000.5.7f1 > Add modules.");
                return;
            }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { CenoteVivoSetup.ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            Log(report);
        }

        // Dos players de Windows con la sonda MetricsProbe: optimizado y "antes" (sin static batching).
        public static void BuildMetricsPlayers()
        {
            PlayerSettings.enableFrameTimingStats = true;
            // El perfil "Mobile" está excluido de Standalone: se habilita solo mientras se compilan
            // los players de medición, para que midan con la misma configuración que el APK.
            SetMobileExcludedFromStandalone(false);
            try { BuildMetricsPlayersInternal(); }
            finally { SetMobileExcludedFromStandalone(true); }
        }

        static void SetMobileExcludedFromStandalone(bool excluded)
        {
            var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = so.FindProperty("m_QualitySettings");
            for (int i = 0; i < levels.arraySize; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                if (level.FindPropertyRelative("name").stringValue != "Mobile") continue;
                var list = level.FindPropertyRelative("excludedTargetPlatforms");
                for (int j = list.arraySize - 1; j >= 0; j--)
                    if (list.GetArrayElementAtIndex(j).stringValue == "Standalone") list.DeleteArrayElementAtIndex(j);
                if (excluded)
                {
                    list.InsertArrayElementAtIndex(list.arraySize);
                    list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = "Standalone";
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        static void BuildMetricsPlayersInternal()
        {
            Build(CenoteVivoSetup.ScenePath, "Builds/Metrics/Optimizado/CenoteVivo.exe");

            const string baselineScene = "Assets/CenoteVivo/Scenes/CenoteVivo_SinOptimizar.unity";
            AssetDatabase.DeleteAsset(baselineScene);
            AssetDatabase.CopyAsset(CenoteVivoSetup.ScenePath, baselineScene);
            var scene = EditorSceneManager.OpenScene(baselineScene);
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, GameObjectUtility.GetStaticEditorFlags(t.gameObject) & ~StaticEditorFlags.BatchingStatic);
            EditorSceneManager.SaveScene(scene);
            Build(baselineScene, "Builds/Metrics/SinOptimizar/CenoteVivo.exe");
            AssetDatabase.DeleteAsset(baselineScene);
        }

        static void Build(string scene, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            });
            Log(report);
        }

        static void Log(BuildReport report)
        {
            var s = report.summary;
            Debug.Log($"CENOTE_BUILD {s.result} -> {s.outputPath} | {s.totalSize / 1048576f:F1} MB | {s.totalTime.TotalSeconds:F0} s | errores {s.totalErrors}");
        }

        static void Fail(string message)
        {
            Debug.LogError("CENOTE_BUILD Failed: " + message);
            if (!Application.isBatchMode) EditorUtility.DisplayDialog("Cenote Vivo", message, "OK");
        }
    }
}
