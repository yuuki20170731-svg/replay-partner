using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace ReplayPartner.Editor
{
    public static class ReplayProjectSetup
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string PanelPath = "Assets/Resources/ReplayPanel.asset";

        [InitializeOnLoadMethod]
        private static void OnImport()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode)
                    Setup();
            };
        }

        [MenuItem("Replay Partner/Setup Project")]
        public static void Setup()
        {
            if (AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath) == null)
            {
                var panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1280, 720);
                AssetDatabase.CreateAsset(panel, PanelPath);
            }
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!File.Exists(ScenePath))
            {
                if (!Application.isBatchMode && EditorSceneManager.GetActiveScene().isDirty)
                {
                    Debug.LogWarning("Save the current scene, then run Replay Partner > Setup Project again.");
                    return;
                }
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                camera.tag = "MainCamera";
                camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
                camera.GetComponent<Camera>().backgroundColor = new Color32(7, 23, 32, 255);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "Yuki Tanaka";
            PlayerSettings.productName = "Replay Partner";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 800;
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Replay Partner/Build WebGL")]
        public static void BuildWebGL() => Build(BuildTarget.WebGL, "Build/WebGL");

        [MenuItem("Replay Partner/Build Windows")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Build/Windows-RC6/ReplayPartner.exe");

        private static void Build(BuildTarget target, string output)
        {
            Setup();
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = target,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Unity build failed: " + report.summary.result);
            Debug.Log("Replay Partner build: " + report.summary.outputPath);
            if (target == BuildTarget.StandaloneWindows64)
            {
                string directory = Path.GetDirectoryName(output);
                File.Copy("DISTRIBUTION.md", Path.Combine(directory, "README.md"), true);
                File.Copy("Assets/Resources/NotoSans-LICENSE.txt", Path.Combine(directory, "NotoSans-LICENSE.txt"), true);
                File.Copy("TECHNICAL.md", Path.Combine(directory, "TECHNICAL.md"), true);
                File.Copy("QA.md", Path.Combine(directory, "QA.md"), true);
                File.Copy("RELEASE_CHECKLIST.md", Path.Combine(directory, "RELEASE_CHECKLIST.md"), true);
            }
        }
    }
}
