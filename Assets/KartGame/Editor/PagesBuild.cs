using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace KartGame.Editor
{
    public static class PagesBuild
    {
        public static void Build()
        {
            KartGame.EditorTools.TrackBuilder.IncludeRuntimeShaders();
            // GitHub Pages cannot configure Content-Encoding response headers.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.productName = "PaoPao Kart";
            PlayerSettings.bundleVersion = "1.1";
            // Effects are instantiated at runtime and are absent from the saved scene.
            PlayerSettings.stripEngineCode = true;
            var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var shaders = graphics.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in new[] { "Sprites/Default", "Legacy Shaders/Particles/Additive", "UI/Default" })
            {
                var shader = Shader.Find(name);
                if (shader == null) throw new Exception("Required shader not found: " + name);
                bool exists = false;
                for (int i = 0; i < shaders.arraySize; i++)
                    if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader) exists = true;
                if (!exists)
                {
                    shaders.InsertArrayElementAtIndex(shaders.arraySize);
                    shaders.GetArrayElementAtIndex(shaders.arraySize - 1).objectReferenceValue = shader;
                }
            }
            graphics.ApplyModifiedPropertiesWithoutUndo();
            graphics.Dispose();
            AssetDatabase.SaveAssets();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/KartGame/Scenes/RaceTrack.unity" },
                locationPathName = "Builds/WebGL",
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("WebGL build failed: " + report.summary.result);
            // Fit the default Unity template into smaller desktop browser panels.
            var htmlPath = "Builds/WebGL/index.html";
            var html = File.ReadAllText(htmlPath);
            html = html.Replace("fullscreenContainer.style.width = \"960px\";",
                "fullscreenContainer.style.width = \"min(960px, 100vw, calc((100vh - 38px) * 1.6))\";")
                .Replace("fullscreenContainer.style.height = \"600px\";",
                "fullscreenContainer.style.height = \"auto\"; fullscreenContainer.style.aspectRatio = \"8 / 5\";");
            File.WriteAllText(htmlPath, html);
        }
    }
}
