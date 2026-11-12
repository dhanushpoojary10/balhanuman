using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BalHanuman.EditorTools
{
    /// <summary>One-click Android APK build: Bal Hanuman/Build Android APK. Output: Builds/BalHanuman.apk (debug-signed, for testing).</summary>
    public static class AndroidBuilder
    {
        const string OutDir = "Builds";
        const string OutFile = "Builds/BalHanuman.apk";
        const string StatusFile = "Builds/build_status.txt";

        [MenuItem("Bal Hanuman/Build Android APK")]
        public static void Build()
        {
            Directory.CreateDirectory(OutDir);
            File.WriteAllText(StatusFile, "RUNNING");

            var scenes = new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Level01_MissingFruitOffering.unity" };
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = false;
            if (File.Exists(OutFile)) File.Delete(OutFile);

            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutFile,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            string msg = s.result + " size=" + (s.totalSize / (1024 * 1024)) + "MB time=" + s.totalTime.TotalMinutes.ToString("0.0") + "min errors=" + s.totalErrors;
            File.WriteAllText(StatusFile, msg);
            Debug.Log("[APK] " + msg + " -> " + Path.GetFullPath(OutFile));
        }
    }
}
