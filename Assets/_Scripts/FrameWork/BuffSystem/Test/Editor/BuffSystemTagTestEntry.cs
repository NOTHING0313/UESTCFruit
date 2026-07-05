using System.IO;
using UnityEditor;
using UnityEngine;

namespace BuffSystem.EditorTesting
{
    public static class BuffSystemTagTestEntry
    {
        private const string MenuRoot = "Tools/BuffSystem/Testing/";

        internal static BuffSystemTagTestReport LastReport { get; private set; }

        public static void RunTagTests()
        {
            BuffSystemTagTestRunner runner = new BuffSystemTagTestRunner();
            LastReport = runner.RunAll();

            string message =
                $"[BuffSystemTagTestEntry] Summary={LastReport.Summary}, Total={LastReport.Total}, " +
                $"Passed={LastReport.Passed}, Failed={LastReport.Failed}, Skipped={LastReport.Skipped}, " +
                $"NotSupported={LastReport.NotSupported}, Report={LastReport.ResultPath}";

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
            string path = Path.Combine(projectRoot, BuffSystemTagTestReport.RelativeResultPath);
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("BuffSystem Tag Tests", "No tag test result found. Run tests first.", "OK");
                return;
            }

            EditorUtility.RevealInFinder(path);
        }

        [MenuItem(MenuRoot + "Run BuffSystem Tag Tests")]
        private static void RunTagTestsMenu()
        {
            RunTagTests();
        }

        [MenuItem(MenuRoot + "Open BuffSystem Tag Test Result")]
        private static void OpenLatestResultMenu()
        {
            OpenLatestResult();
        }
    }
}
