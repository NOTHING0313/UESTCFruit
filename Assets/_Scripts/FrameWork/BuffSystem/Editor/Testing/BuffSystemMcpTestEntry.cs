using System.IO;
using UnityEditor;
using UnityEngine;

namespace BuffSystem.EditorTesting
{
    /// <summary>
    /// Unity MCP / executeMethod 可调用的 BuffSystem 测试入口；只编排 Editor 测试，不修改运行时。
    /// </summary>
    public static class BuffSystemMcpTestEntry
    {
        private const string MenuRoot = "Tools/BuffSystem/Testing/";

        public static BuffSystemTestReport LastReport { get; private set; }

        public static BuffSystemTestReport RunAllBuffSystemTests()
        {
            return RunAndLog("All", runner => runner.RunAll());
        }

        public static BuffSystemTestReport RunUnitTests()
        {
            return RunAndLog("Unit", runner => runner.RunUnit());
        }

        public static BuffSystemTestReport RunIntegrationTests()
        {
            return RunAndLog("Integration", runner => runner.RunIntegration());
        }

        public static BuffSystemTestReport RunWhiteBoxTests()
        {
            return RunAndLog("WhiteBox", runner => runner.RunWhiteBox());
        }

        public static BuffSystemTestReport RunBlackBoxTests()
        {
            return RunAndLog("BlackBox", runner => runner.RunBlackBox());
        }

        public static BuffSystemTestReport RunSmokeTests()
        {
            return RunAndLog("Smoke", runner => runner.RunSmoke());
        }

        public static BuffSystemTestReport RunAuthoringSmokeTests()
        {
            return RunAndLog("AuthoringSmoke", runner => runner.RunAuthoringSmoke());
        }

        [MenuItem(MenuRoot + "Open Last BuffSystem Test Report")]
        public static void OpenLastBuffSystemTestReport()
        {
            string path = GetLatestMarkdownReportPath();

            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("BuffSystem Test Report", "尚未找到 Temp/BuffSystemTestReports/latest.md。请先运行测试。", "OK");
                return;
            }

            EditorUtility.RevealInFinder(path);
        }

        [MenuItem(MenuRoot + "Run All BuffSystem Tests")]
        private static void RunAllBuffSystemTestsMenu()
        {
            RunAllBuffSystemTests();
        }

        [MenuItem(MenuRoot + "Run BuffSystem Smoke Tests")]
        private static void RunSmokeTestsMenu()
        {
            RunSmokeTests();
        }

        private static BuffSystemTestReport RunAndLog(string profile, System.Func<BuffSystemFullTestRunner, BuffSystemTestReport> run)
        {
            // 默认不执行任何会创建 asset / 生成 .cs / 写 registry 的破坏性 smoke。
            BuffSystemFullTestRunner runner = new BuffSystemFullTestRunner(false);
            LastReport = run(runner);

            string message = $"[BuffSystemMcpTestEntry] Profile={profile}, Summary={LastReport.Summary}, Report={LastReport.MarkdownPath}";
            if (LastReport.HasFailures)
                Debug.LogError(message);
            else
                Debug.Log(message);

            if (Application.isBatchMode && LastReport.HasFailures)
                EditorApplication.Exit(1);

            return LastReport;
        }

        private static string GetLatestMarkdownReportPath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, "Temp", "BuffSystemTestReports", "latest.md");
        }
    }
}
