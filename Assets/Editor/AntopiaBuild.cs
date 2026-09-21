using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Antopia.EditorTools
{
    // Genera materiales y la escena Main, y compila el APK. Se invoca por linea de comandos.
    public static class AntopiaBuild
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string MatDir = "Assets/Resources/Materials";
        public const string ApkPath = "Builds/Antopia.apk";

        static readonly (string name, Color color)[] Materials =
        {
            ("Ground", new Color(0.42f, 0.66f, 0.30f)),
            ("Mound", new Color(0.50f, 0.33f, 0.20f)),
            ("Despensa", new Color(0.95f, 0.75f, 0.25f)),
            ("Tuneles", new Color(0.55f, 0.55f, 0.60f)),
            ("Camara", new Color(0.62f, 0.35f, 0.70f)),
            ("Ant", new Color(0.13f, 0.09f, 0.08f)),
            ("Leaf", new Color(0.30f, 0.75f, 0.28f)),
            ("Twig", new Color(0.55f, 0.36f, 0.18f)),
        };

        [MenuItem("Antopia/Setup Project")]
        public static void Setup()
        {
            CreateMaterials();
            CreateScene();
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[AntopiaBuild] Setup done.");
        }

        static void CreateMaterials()
        {
            Directory.CreateDirectory(MatDir);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var (name, color) in Materials)
            {
                string path = $"{MatDir}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Smoothness", 0.1f);
                EditorUtility.SetDirty(mat);
            }
        }

        static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var go = new GameObject("Antopia");
            go.AddComponent<GameController>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Antopia";
            PlayerSettings.productName = "Antopia";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.antopia.game");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.bundleVersion = "0.1.0";
        }

        // -executeMethod Antopia.EditorTools.AntopiaBuild.BuildAndroid -buildTarget Android
        public static void BuildAndroid()
        {
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = true;
            Directory.CreateDirectory("Builds");
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var summary = report.summary;
            Debug.Log($"[AntopiaBuild] Result: {summary.result}, size: {summary.totalSize / (1024 * 1024)} MB, errors: {summary.totalErrors}");
            EditorApplication.Exit(summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }

        // Build de Windows para hacer capturas automaticas de las pantallas (ver ShotRunner). Se lanza con
        // -executeMethod Antopia.EditorTools.AntopiaBuild.BuildWindowsShots -buildTarget Win64
        public static void BuildWindowsShots()
        {
            Directory.CreateDirectory("Builds/Shots");
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Shots/Antopia.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var summary = BuildPipeline.BuildPlayer(opts).summary;
            Debug.Log($"[AntopiaBuild] Shots result: {summary.result}, errors: {summary.totalErrors}");
            EditorApplication.Exit(summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }
    }
}
