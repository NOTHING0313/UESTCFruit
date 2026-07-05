using UnityEditor;
using UnityEngine;

namespace BuffSystem.EditorTesting
{
    /// <summary>
    /// BuffSystem 高强度 Editor-only 测试入口；可由菜单、MCP execute_menu_item 或 Unity batchmode executeMethod 调用。
    /// </summary>
    public static class BuffSystemAdvancedTestEntry
    {
        private const string MenuRoot = "Tools/BuffSystem/Testing/";

        internal static BuffSystemAdvancedTestReport LastReport { get; private set; }

        public static void RunAllAdvancedBuffSystemTests()
        {
            RunAndLog("Advanced Quick", runner => runner.RunAll(BuffSystemAdvancedTestProfile.Quick));
        }

        public static void RunStressTests()
        {
            RunAndLog("Stress Quick", runner => runner.RunStress(BuffSystemAdvancedTestProfile.Quick));
        }

        public static void RunPerformanceTests()
        {
            RunAndLog("Performance Quick", runner => runner.RunPerformance(BuffSystemAdvancedTestProfile.Quick));
        }

        public static void RunFuzzTests()
        {
            RunAndLog("Fuzz Quick", runner => runner.RunFuzz(BuffSystemAdvancedTestProfile.Quick));
        }

        public static void RunSoakTests()
        {
            RunAndLog("Soak Quick", runner => runner.RunSoak(BuffSystemAdvancedTestProfile.Quick));
        }

        public static void OpenLatestResult()
        {
            string path = BuffSystemAdvancedTestReport.Create("Quick").ResultPath;
            if (!System.IO.File.Exists(path))
            {
                EditorUtility.DisplayDialog("BuffSystem Advanced Test Result", "尚未找到 BuffSystem 高强度测试结果，请先运行 Advanced 测试。", "OK");
                return;
            }

            EditorUtility.RevealInFinder(path);
        }

        [MenuItem(MenuRoot + "Run Advanced BuffSystem Tests")]
        private static void RunAllAdvancedBuffSystemTestsMenu()
        {
            RunAllAdvancedBuffSystemTests();
        }

        [MenuItem(MenuRoot + "Run BuffSystem Stress Tests")]
        private static void RunStressTestsMenu()
        {
            RunStressTests();
        }

        [MenuItem(MenuRoot + "Run BuffSystem Performance Tests")]
        private static void RunPerformanceTestsMenu()
        {
            RunPerformanceTests();
        }

        [MenuItem(MenuRoot + "Run BuffSystem Fuzz Tests")]
        private static void RunFuzzTestsMenu()
        {
            RunFuzzTests();
        }

        [MenuItem(MenuRoot + "Run BuffSystem Soak Tests")]
        private static void RunSoakTestsMenu()
        {
            RunSoakTests();
        }

        [MenuItem(MenuRoot + "Open BuffSystem Advanced Test Result")]
        private static void OpenLatestResultMenu()
        {
            OpenLatestResult();
        }

        private static void RunAndLog(string profileName, System.Func<BuffSystemAdvancedTestRunner, BuffSystemAdvancedTestReport> run)
        {
            BuffSystemAdvancedTestRunner runner = new BuffSystemAdvancedTestRunner();
            LastReport = run(runner);

            string message = $"[BuffSystemAdvancedTestEntry] Profile={profileName}, Summary={LastReport.Summary}, Total={LastReport.Total}, Failed={LastReport.Failed}, ManualRequired={LastReport.ManualRequired}, Report={LastReport.ResultPath}";
            if (LastReport.HasFailures)
                Debug.LogError(message);
            else
                Debug.Log(message);

            if (Application.isBatchMode && LastReport.HasFailures)
                EditorApplication.Exit(1);
        }
    }
}
