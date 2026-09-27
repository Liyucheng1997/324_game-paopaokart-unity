using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KartGame.EditorTools
{
    /// <summary>
    /// Creates the single game scene. Tracks, karts, UI and effects are all
    /// generated at runtime (see WorldBuilder / RaceManager / MenuScreen), so the
    /// scene only holds a camera, a sun and the GameBootstrap.
    /// Menu: KartGame/Build Race Scene.
    /// </summary>
    public static class TrackBuilder
    {
        const string SceneDir = "Assets/KartGame/Scenes";
        const string ScenePath = SceneDir + "/RaceTrack.unity";
        const string OldGenerated = "Assets/KartGame/Generated";

        static readonly string[] RuntimeShaders =
        {
            "KartGame/Lit", "KartGame/Sky", "KartGame/Water", "KartGame/Particle", "Hidden/KartGame/PostFX",
        };

        [MenuItem("KartGame/Build Race Scene")]
        public static void BuildScene()
        {
            if (!AssetDatabase.IsValidFolder(SceneDir)) AssetDatabase.CreateFolder("Assets/KartGame", "Scenes");
            if (AssetDatabase.IsValidFolder(OldGenerated)) AssetDatabase.DeleteAsset(OldGenerated);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var cam = Camera.main;
            cam.transform.position = new Vector3(0, 3, -8);
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.farClipPlane = 1400f;
            var boot = new GameObject("GameBootstrap");
            boot.AddComponent<GameBootstrap>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            IncludeRuntimeShaders();
            AssetDatabase.SaveAssets();
            Debug.Log("KartGame: bootstrap scene saved to " + ScenePath);
        }

        /// <summary>Materials are created at runtime, so the shaders must be force-included in builds.</summary>
        public static void IncludeRuntimeShaders()
        {
            var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var list = graphics.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in RuntimeShaders)
            {
                var shader = Shader.Find(name);
                if (shader == null) { Debug.LogError("KartGame: shader not found " + name); continue; }
                bool exists = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) exists = true;
                if (exists) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            }
            graphics.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("KartGame/Quick Race (Play)")]
        public static void QuickRace()
        {
            SessionState.SetBool("KartGame.QuickRace", true);
            EditorApplication.isPlaying = true;
        }
    }
}
