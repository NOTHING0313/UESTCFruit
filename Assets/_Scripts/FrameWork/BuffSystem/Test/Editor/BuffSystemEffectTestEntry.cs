using System.IO;
using UnityEditor;
using UnityEngine;

namespace BuffSystem.EditorTesting
{
    public static class BuffSystemEffectTestEntry
    {
        private const string MenuRoot = "Tools/BuffSystem/Testing/";

        internal static BuffSystemEffectTestReport LastReport { get; private set; }

        public static void RunEffectTests()
        {
            BuffSystemEffectTestRunner runner = new BuffSystemEffectTestRunner();
            LastReport = runner.RunAll();

            string message =
                $"[BuffSystemEffectTestEntry] Summary={LastReport.Summary}, Total={LastReport.Total}, " +
                $"Passed={LastReport.Passed}, Failed={LastReport.Failed}, Skipped={LastReport.Skipped}, " +
                $"NotSupported={LastReport.NotSupported}, Report={LastReport.ResultPath}";

            if (LastReport.HasFailures)
                Debug.LogError(message);
            else if (LastReport.HasNotSupported)
                Debug.LogWarning(message);
            else
                Debug.Log(message);

            if (Application.isBatchMode && LastReport.HasFailures)
                EditorApplication.Exit(1);
        }

        public static void OpenLatestResult()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string path = Path.Combine(projectRoot, BuffSystemEffectTestReport.RelativeResultPath);
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("BuffSystem Effect Tests", "No effect test result found. Run tests first.", "OK");
                return;
            }

            EditorUtility.RevealInFinder(path);
        }

        [MenuItem(MenuRoot + "Run BuffSystem Effect Tests")]
        private static void RunEffectTestsMenu()
        {
            RunEffectTests();
        }

        [MenuItem(MenuRoot + "Open BuffSystem Effect Test Result")]
        private static void OpenLatestResultMenu()
        {
            OpenLatestResult();
        }
    }
}
