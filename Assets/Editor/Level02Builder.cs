using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BalHanuman.EditorTools
{
    /// <summary>Builds the Level 2 fight scene (Patala Yuddha). Everything inside it is created at runtime by FightDirector,
    /// so the scene only holds the camera, the global 2D light and the director. Menu: Bal Hanuman > Build Level 2 (Fight).</summary>
    public static class Level02Builder
    {
        public const string ScenePath = "Assets/Scenes/Level02_PatalaYuddha.unity";

        [MenuItem("Bal Hanuman/Build Level 2 (Fight)")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 4.3f; cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.03f, 0.09f);
            camGo.transform.position = new Vector3(0, 2.7f, -10);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<Fight.FightCamera>();

            var lg = new GameObject("Global Light 2D").AddComponent<Light2D>();
            lg.lightType = Light2D.LightType.Global; lg.intensity = 1f;

            var dir = new GameObject("FightDirector").AddComponent<Fight.FightDirector>();
            dir.skin = AssetDatabase.LoadAssetAtPath<UiSkin>("Assets/Prefabs/UI/UiSkin.asset");
            if (dir.skin == null) Debug.LogError("UiSkin not found - build Level 1 first (Bal Hanuman > Build Level 1 (HQ)).");

            EditorSceneManager.SaveScene(scene, ScenePath);

            var list = EditorBuildSettings.scenes.ToList();
            if (!list.Any(s => s.path == ScenePath)) list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("Level 2 (Patala Yuddha) built: " + ScenePath);
        }
    }
}
