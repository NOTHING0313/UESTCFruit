using System.IO;
using UnityEditor;
using UnityEngine;

namespace BuffSystem.EditorTesting
{
    public static class BuffSystemLifecycleTestEntry
    {
        private const string MenuRoot = "Tools/BuffSystem/Testing/";

        internal static BuffSystemLifecycleTestReport LastReport { get; private set; }

        public static void RunLifecycleTests()
        {
            BuffSystemLifecycleTestRunner runner = new BuffSystemLifecycleTestRunner();
            LastReport = runner.RunAll();

            string message =
                $"[BuffSystemLifecycleTestEntry] Summary={LastReport.Summary}, Total={LastReport.Total}, " +
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
            string path = Path.Combine(projectRoot, BuffSystemLifecycleTestReport.RelativeResultPath);
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("BuffSystem Lifecycle Tests", "No lifecycle test result found. Run tests first.", "OK");
                return;
            }

            EditorUtility.RevealInFinder(path);
        }

        [MenuItem(MenuRoot + "Run BuffSystem Lifecycle Tests")]
        private static void RunLifecycleTestsMenu()
        {
            RunLifecycleTests();
        }

        [MenuItem(MenuRoot + "Open BuffSystem Lifecycle Test Result")]
        private static void OpenLatestResultMenu()
        {
            OpenLatestResult();
        }
    }
}
