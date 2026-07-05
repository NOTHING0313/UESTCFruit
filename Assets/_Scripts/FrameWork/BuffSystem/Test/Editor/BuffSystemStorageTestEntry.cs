using System.IO;
using UnityEditor;
using UnityEngine;

namespace BuffSystem.EditorTesting
{
    public static class BuffSystemStorageTestEntry
    {
        private const string MenuRoot = "Tools/BuffSystem/Testing/";

        internal static BuffSystemStorageTestReport LastReport { get; private set; }

        public static void RunStorageTests()
        {
            BuffSystemStorageTestRunner runner = new BuffSystemStorageTestRunner();
            LastReport = runner.RunAll();

            string message =
                $"[BuffSystemStorageTestEntry] Summary={LastReport.Summary}, Total={LastReport.Total}, " +
                $"Passed={LastReport.Passed}, Failed={LastReport.Failed}, Skipped={LastReport.Skipped}, " +
                $"ManualRequired={LastReport.ManualRequired}, Report={LastReport.ResultPath}";

            if (LastReport.HasFailures)
                Debug.LogError(message);
            else if (LastReport.HasManualRequired)
                Debug.LogWarning(message);
            else
                Debug.Log(message);

            if (Application.isBatchMode && LastReport.HasFailures)
                EditorApplication.Exit(1);
        }

        public static void OpenLatestResult()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string path = Path.Combine(projectRoot, BuffSystemStorageTestReport.RelativeResultPath);
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("BuffSystem Storage Tests", "No storage test result found. Run tests first.", "OK");
                return;
            }

            EditorUtility.RevealInFinder(path);
        }

        [MenuItem(MenuRoot + "Run BuffSystem Storage Tests")]
        private static void RunStorageTestsMenu()
        {
            RunStorageTests();
        }

        [MenuItem(MenuRoot + "Open BuffSystem Storage Test Result")]
        private static void OpenLatestResultMenu()
        {
            OpenLatestResult();
        }
    }
}
