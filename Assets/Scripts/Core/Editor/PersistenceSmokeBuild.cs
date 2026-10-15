using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Simpiens.Core.Editor
{
    /// <summary>
    /// Builds a Linux player with the persistence smoke test compiled in (define <c>SIMPIENS_PERSISTENCE_SMOKE</c>,
    /// for this build only). Run by <c>scripts/run-persistence-smoke.sh</c>:
    /// <c>-executeMethod Simpiens.Core.Editor.PersistenceSmokeBuild.BuildLinux -smokeBuildPath &lt;player file&gt;</c>.
    /// Exits 0 when the build succeeds, 1 when it fails and 2 without a path.
    /// </summary>
    public static class PersistenceSmokeBuild
    {
        public static void BuildLinux()
        {
            string path = ArgumentAfter("-smokeBuildPath");
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[PersistenceSmokeBuild] Missing -smokeBuildPath <player file>.");
                EditorApplication.Exit(2);
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = path,
                target = BuildTarget.StandaloneLinux64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
                extraScriptingDefines = new[] { "SIMPIENS_PERSISTENCE_SMOKE" },
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[PersistenceSmokeBuild] {report.summary.result}: {report.summary.totalErrors} error(s), {report.summary.totalSize} bytes, {path}");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        private static string ArgumentAfter(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }
            return null;
        }
    }
}
