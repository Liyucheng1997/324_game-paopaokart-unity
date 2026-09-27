using System;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace KartGame.Editor
{
    public static class PagesBuild
    {
        public static void Build()
        {
            // GitHub Pages cannot configure Content-Encoding response headers.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/KartGame/Scenes/RaceTrack.unity" },
                locationPathName = "Builds/WebGL",
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("WebGL build failed: " + report.summary.result);
        }
    }
}
