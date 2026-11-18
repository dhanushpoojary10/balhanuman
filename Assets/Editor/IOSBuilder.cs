using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace BalHanuman.EditorTools
{
    /// <summary>
    /// iOS builds.
    ///   Bal Hanuman/Build iOS (Simulator)  -> Builds/iOS-Simulator (Xcode project, simulator SDK, unsigned)
    ///   Bal Hanuman/Build iOS (TestFlight) -> Builds/iOS (Xcode project, device SDK, automatic signing, build number bumped)
    /// Status of the last build is written to Builds/ios_build_status.txt.
    /// </summary>
    public static class IOSBuilder
    {
        public const string BundleId = "com.codecraft.balhanumangame";
        public const string TeamId = "JEAXKNJ584"; // CodeCraft Technologies Private Limited
        const string MarketingVersion = "1.0";
        const string MinIOS = "15.0";

        const string SimOut = "Builds/iOS-Simulator";
        const string DeviceOut = "Builds/iOS";
        const string StatusFile = "Builds/ios_build_status.txt";

        static readonly string[] Scenes =
        {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/Level01_MissingFruitOffering.unity",
            "Assets/Scenes/Level02_PatalaYuddha.unity",
        };

        [MenuItem("Bal Hanuman/Build iOS (Simulator)")]
        public static void BuildSimulator() => Build(simulator: true);

        [MenuItem("Bal Hanuman/Build iOS (TestFlight)")]
        public static void BuildDevice() => Build(simulator: false);

        static void ApplyCommonSettings()
        {
            var ios = NamedBuildTarget.iOS;
            PlayerSettings.SetApplicationIdentifier(ios, BundleId);
            PlayerSettings.SetScriptingBackend(ios, ScriptingImplementation.IL2CPP);
            PlayerSettings.bundleVersion = MarketingVersion;
            PlayerSettings.iOS.targetOSVersionString = MinIOS;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.appleDeveloperTeamID = TeamId;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.iOS.requiresFullScreen = true; // landscape-only on iPad requires full screen
            PlayerSettings.iOS.hideHomeButton = false;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            if (string.IsNullOrEmpty(PlayerSettings.iOS.buildNumber) || PlayerSettings.iOS.buildNumber == "0")
                PlayerSettings.iOS.buildNumber = "1";
        }

        static void Build(bool simulator)
        {
            Directory.CreateDirectory("Builds");
            File.WriteAllText(StatusFile, "RUNNING " + (simulator ? "simulator" : "device"));

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);

            ApplyCommonSettings();
            if (simulator)
            {
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
                PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
            }
            else
            {
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
                // TestFlight rejects a build number it has already seen for this version.
                int n = int.TryParse(PlayerSettings.iOS.buildNumber, out var cur) ? cur + 1 : 1;
                PlayerSettings.iOS.buildNumber = n.ToString();
            }
            EditorUserBuildSettings.development = false;
            AssetDatabase.SaveAssets();

            string outDir = simulator ? SimOut : DeviceOut;
            var opts = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = outDir,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None,
            };

            BuildReport report;
            try { report = BuildPipeline.BuildPlayer(opts); }
            finally
            {
                // Leave the project on the device SDK so a menu build defaults to the shippable configuration.
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
                AssetDatabase.SaveAssets();
            }

            var s = report.summary;
            string msg = s.result + " " + (simulator ? "simulator" : "device") +
                         " version=" + PlayerSettings.bundleVersion + "(" + PlayerSettings.iOS.buildNumber + ")" +
                         " time=" + s.totalTime.TotalMinutes.ToString("0.0") + "min errors=" + s.totalErrors +
                         " out=" + Path.GetFullPath(outDir);
            File.WriteAllText(StatusFile, msg);
            Debug.Log("[iOS] " + msg);
            if (s.result != BuildResult.Succeeded) throw new Exception("[iOS] build failed: " + msg);
        }

#if UNITY_IOS
        /// <summary>Info.plist keys App Store Connect asks for on every upload.</summary>
        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;
            // The game uses only standard HTTPS/OS crypto: skips the export-compliance prompt in TestFlight.
            root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            root.SetString("CFBundleDisplayName", "Bal Hanuman");
            root.SetString("LSApplicationCategoryType", "public.app-category.action-games");
            plist.WriteToFile(plistPath);
        }
#endif
    }
}
