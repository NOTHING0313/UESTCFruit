using System.IO;
using UnityEditor;
using UnityEngine;

namespace BuffSystem.EditorTesting
{
    public static class BuffSystemTriggerTestEntry
    {
        private const string MenuRoot = "Tools/BuffSystem/Testing/";

        internal static BuffSystemTriggerTestReport LastReport { get; private set; }

        public static void RunTriggerTests()
        {
            BuffSystemTriggerTestRunner runner = new BuffSystemTriggerTestRunner();
            LastReport = runner.RunAll();

            string message =
                $"[BuffSystemTriggerTestEntry] Summary={LastReport.Summary}, Total={LastReport.Total}, " +
                $"Passed={LastReport.Passed}, Failed={LastReport.Failed}, Skipped={LastReport.Skipped}, " +
                $"NotSupported={LastReport.NotSupported}, ManualRequired={LastReport.ManualRequired}, Report={LastReport.ResultPath}";

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
            string path = Path.Combine(projectRoot, BuffSystemTriggerTestReport.RelativeResultPath);
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("BuffSystem Trigger Tests", "No trigger test result found. Run tests first.", "OK");
                return;
            }

            EditorUtility.RevealInFinder(path);
        }

        [MenuItem(MenuRoot + "Run BuffSystem Trigger Tests")]
        private static void RunTriggerTestsMenu()
        {
            RunTriggerTests();
        }

        [MenuItem(MenuRoot + "Open BuffSystem Trigger Test Result")]
        private static void OpenLatestResultMenu()
        {
            OpenLatestResult();
        }
    }
}
