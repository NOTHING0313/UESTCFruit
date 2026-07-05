using System.IO;
using UnityEditor;
using UnityEngine;

namespace BuffSystem.EditorTesting
{
    public static class BuffSystemFunctionalCoverageEntry
    {
        private const string MenuRoot = "Tools/BuffSystem/Testing/";

        internal static BuffSystemFunctionalCoverageReport LastReport { get; private set; }

        public static void RunFunctionalCoverageTests()
        {
            BuffSystemFunctionalCoverageRunner runner = new BuffSystemFunctionalCoverageRunner();
            LastReport = runner.RunAll();

            string message =
                $"[BuffSystemFunctionalCoverageEntry] Summary={LastReport.Summary}, Total={LastReport.Total}, " +
                $"Passed={LastReport.Passed}, Failed={LastReport.Failed}, Skipped={LastReport.Skipped}, Report={LastReport.ResultPath}";

            if (LastReport.HasFailures)
                Debug.LogError(message);
            else
                Debug.Log(message);

            if (Application.isBatchMode && LastReport.HasFailures)
                EditorApplication.Exit(1);
        }

        public static void OpenLatestResult()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string path = Path.Combine(projectRoot, BuffSystemFunctionalCoverageReport.RelativeResultPath);
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("BuffSystem Functional Coverage", "No functional coverage result found. Run tests first.", "OK");
                return;
            }

            EditorUtility.RevealInFinder(path);
        }

        [MenuItem(MenuRoot + "Run BuffSystem Functional Coverage Tests")]
        private static void RunFunctionalCoverageTestsMenu()
        {
            RunFunctionalCoverageTests();
        }

        [MenuItem(MenuRoot + "Open BuffSystem Functional Coverage Result")]
        private static void OpenLatestResultMenu()
        {
            OpenLatestResult();
        }
    }
}
