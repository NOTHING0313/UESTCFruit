using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace BuffSystem.EditorTesting
{
    /// <summary>
    /// BuffSystem Editor 测试报告；报告写入 Temp，不作为 Unity 资源或运行时真状态。
    /// </summary>
    [Serializable]
    public sealed class BuffSystemTestReport
    {
        public string TaskName;
        public string Profile;
        public string StartedAt;
        public string FinishedAt;
        public string UnityVersion;
        public string ProjectPath;
        public bool RunDestructiveWriteSmoke;
        public int Total;
        public int Passed;
        public int Failed;
        public int Skipped;
        public string Summary;
        public string JsonPath;
        public string MarkdownPath;
        public List<BuffSystemTestCaseResult> Results = new List<BuffSystemTestCaseResult>();
        public List<BuffSystemCoverageItem> Coverage = new List<BuffSystemCoverageItem>();
        public List<string> ManualSceneItems = new List<string>();
        public List<string> Notes = new List<string>();

        public bool HasFailures => Failed > 0;

        public static BuffSystemTestReport Create(string profile, bool runDestructiveWriteSmoke)
        {
            return new BuffSystemTestReport
            {
                TaskName = "Phase 3I-12A BuffSystem MCP Test Orchestration",
                Profile = profile,
                StartedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                UnityVersion = Application.unityVersion,
                ProjectPath = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath,
                RunDestructiveWriteSmoke = runDestructiveWriteSmoke
            };
        }

        public void AddResult(BuffSystemTestCaseResult result)
        {
            if (result == null)
                return;

            Results.Add(result);
            RecalculateSummary();
        }

        public void AddCoverage(string area, string category, string status, string evidence, string notes)
        {
            Coverage.Add(new BuffSystemCoverageItem
            {
                Area = area,
                Category = category,
                Status = status,
                Evidence = evidence,
                Notes = notes
            });
        }

        public void Finish()
        {
            FinishedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            RecalculateSummary();
            Summary = Failed > 0
                ? $"FAIL: total={Total}, passed={Passed}, failed={Failed}, skipped={Skipped}"
                : $"PASS: total={Total}, passed={Passed}, failed={Failed}, skipped={Skipped}";
        }

        public void WriteLatestFiles()
        {
            Finish();

            string root = Path.Combine(ProjectPath, "Temp", "BuffSystemTestReports");
            Directory.CreateDirectory(root);

            JsonPath = Path.Combine(root, "latest.json");
            MarkdownPath = Path.Combine(root, "latest.md");

            File.WriteAllText(JsonPath, JsonUtility.ToJson(this, true), Encoding.UTF8);
            File.WriteAllText(MarkdownPath, ToMarkdown(), Encoding.UTF8);
        }

        public string ToMarkdown()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# BuffSystem Test Report");
            builder.AppendLine();
            builder.AppendLine($"- Task: {TaskName}");
            builder.AppendLine($"- Profile: {Profile}");
            builder.AppendLine($"- StartedAt: {StartedAt}");
            builder.AppendLine($"- FinishedAt: {FinishedAt}");
            builder.AppendLine($"- UnityVersion: {UnityVersion}");
            builder.AppendLine($"- RunDestructiveWriteSmoke: {RunDestructiveWriteSmoke}");
            builder.AppendLine($"- Summary: {Summary}");
            builder.AppendLine();

            builder.AppendLine("## Case Results");
            builder.AppendLine();
            builder.AppendLine("| Category | Name | Status | DurationMs | Message |");
            builder.AppendLine("|---|---|---|---:|---|");
            for (int i = 0; i < Results.Count; i++)
            {
                BuffSystemTestCaseResult result = Results[i];
                builder.AppendLine($"| {Escape(result.Category)} | {Escape(result.Name)} | {Escape(result.Status)} | {result.DurationMs:0.###} | {Escape(result.Message)} |");
            }

            builder.AppendLine();
            builder.AppendLine("## Coverage Matrix");
            builder.AppendLine();
            builder.AppendLine("| Area | Category | Status | Evidence | Notes |");
            builder.AppendLine("|---|---|---|---|---|");
            for (int i = 0; i < Coverage.Count; i++)
            {
                BuffSystemCoverageItem item = Coverage[i];
                builder.AppendLine($"| {Escape(item.Area)} | {Escape(item.Category)} | {Escape(item.Status)} | {Escape(item.Evidence)} | {Escape(item.Notes)} |");
            }

            builder.AppendLine();
            builder.AppendLine("## Manual Scene Items");
            builder.AppendLine();
            for (int i = 0; i < ManualSceneItems.Count; i++)
                builder.AppendLine($"- {ManualSceneItems[i]}");

            builder.AppendLine();
            builder.AppendLine("## Notes");
            builder.AppendLine();
            for (int i = 0; i < Notes.Count; i++)
                builder.AppendLine($"- {Notes[i]}");

            return builder.ToString();
        }

        private void RecalculateSummary()
        {
            Total = Results.Count;
            Passed = 0;
            Failed = 0;
            Skipped = 0;

            for (int i = 0; i < Results.Count; i++)
            {
                switch (Results[i].Status)
                {
                    case BuffSystemTestStatus.Passed:
                        Passed++;
                        break;
                    case BuffSystemTestStatus.Failed:
                        Failed++;
                        break;
                    case BuffSystemTestStatus.Skipped:
                        Skipped++;
                        break;
                }
            }
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        }
    }

    /// <summary>
    /// 覆盖矩阵单项；用于明确自动覆盖、未覆盖和手动验证边界。
    /// </summary>
    [Serializable]
    public sealed class BuffSystemCoverageItem
    {
        public string Area;
        public string Category;
        public string Status;
        public string Evidence;
        public string Notes;
    }
}
