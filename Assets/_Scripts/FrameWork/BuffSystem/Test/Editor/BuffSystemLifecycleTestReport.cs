using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BuffSystem.EditorTesting
{
    internal sealed class BuffSystemLifecycleTestReport
    {
        internal const string RelativeResultPath = "Assets/_Scripts/FrameWork/BuffSystem/Test/\u751F\u547D\u5468\u671F\u6D4B\u8BD5\u7ED3\u679C.md";

        public readonly List<BuffSystemLifecycleTestCaseResult> Results = new List<BuffSystemLifecycleTestCaseResult>();
        public readonly List<string> NotCovered = new List<string>();
        public readonly List<string> Notes = new List<string>();

        public string StartedAt;
        public string FinishedAt;
        public string UnityVersion;
        public string ProjectPath;
        public string ResultPath;
        public int Total;
        public int Passed;
        public int Failed;
        public int Skipped;
        public string Summary;

        public bool HasFailures => Failed > 0;

        public static BuffSystemLifecycleTestReport Create()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            BuffSystemLifecycleTestReport report = new BuffSystemLifecycleTestReport
            {
                StartedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                UnityVersion = Application.unityVersion,
                ProjectPath = projectRoot,
                ResultPath = Path.Combine(projectRoot, RelativeResultPath)
            };

            report.NotCovered.Add("EventTrigger: reserved for a later Trigger dedicated phase.");
            report.NotCovered.Add("Tag: reserved for Phase 3I-12G or an equivalent dedicated tag phase.");
            report.NotCovered.Add("CompressedParallel: reserved for Phase 3I-12I or an equivalent dedicated storage lifecycle phase.");
            report.NotCovered.Add("Rollback: external RollBackSystem restore is not claimed ready by this test.");
            report.NotCovered.Add("View / Scene / Prefab: not covered by this Editor-only lifecycle runner.");
            report.Notes.Add("This runner uses in-memory World, BuffDefinitionRegistry, BuffEffectRegistry, and BuffSystemCore instances only.");
            report.Notes.Add("This runner records lifecycle callback counts and event order through a test-owned CountingLifecycleEffect.");
            report.Notes.Add("This runner does not create Buff assets, generate Effect files, write registry entries, modify Bootstrap, modify whitelist, or save scenes.");
            return report;
        }

        public void Add(BuffSystemLifecycleTestCaseResult result)
        {
            if (result == null)
                return;

            Results.Add(result);
            RecalculateSummary();
        }

        public void Finish()
        {
            FinishedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            RecalculateSummary();
            Summary = Failed > 0 ? "FAIL" : "PASS";
        }

        public void WriteMarkdown()
        {
            Finish();
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, ToMarkdown(), Encoding.UTF8);
            AssetDatabase.Refresh();
        }

        public string ToMarkdown()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# BuffSystem 生命周期专项测试结果");
            builder.AppendLine();
            AppendSummary(builder);
            AppendEnvironment(builder);
            AppendCoverageMatrix(builder);
            AppendCategory(builder, "## 4. OnApply 测试", BuffSystemLifecycleTestRunner.OnApplyCategory);
            AppendCategory(builder, "## 5. OnTick / TickInterval 测试", BuffSystemLifecycleTestRunner.OnTickCategory);
            AppendCategory(builder, "## 6. OnRemove 测试", BuffSystemLifecycleTestRunner.OnRemoveCategory);
            AppendCategory(builder, "## 7. OnRefresh 测试", BuffSystemLifecycleTestRunner.OnRefreshCategory);
            AppendCategory(builder, "## 8. OnStackChanged 测试", BuffSystemLifecycleTestRunner.OnStackChangedCategory);
            AppendCategory(builder, "## 9. 生命周期交错测试", BuffSystemLifecycleTestRunner.InterleavingCategory);
            AppendCategory(builder, "## 10. Effect Context 测试", BuffSystemLifecycleTestRunner.ContextCategory);
            AppendFailures(builder);
            AppendNotCovered(builder);
            AppendConclusion(builder);
            return builder.ToString();
        }

        private void AppendSummary(StringBuilder builder)
        {
            builder.AppendLine("## 1. 测试概要");
            builder.AppendLine();
            builder.AppendLine($"- 测试时间：{StartedAt} -> {FinishedAt}");
            builder.AppendLine($"- Unity 版本：{UnityVersion}");
            builder.AppendLine($"- 项目路径：{ProjectPath}");
            builder.AppendLine($"- 总用例数：{Total}");
            builder.AppendLine($"- 通过：{Passed}");
            builder.AppendLine($"- 失败：{Failed}");
            builder.AppendLine($"- 跳过：{Skipped}");
            builder.AppendLine($"- 最终结论：{Summary}");
            builder.AppendLine();
        }

        private void AppendEnvironment(StringBuilder builder)
        {
            builder.AppendLine("## 2. 测试环境");
            builder.AppendLine();
            builder.AppendLine("- Runner：Unity Editor-only menu and executeMethod entry.");
            builder.AppendLine("- Data source：in-memory World、BuffDefinitionRegistry、BuffEffectRegistry、BuffSystemCore。");
            builder.AppendLine("- Test effect：CountingLifecycleEffect 只注册到测试内部 BuffEffectRegistry。");
            builder.AppendLine("- Write scope：markdown report only.");
            builder.AppendLine();
        }

        private void AppendCoverageMatrix(StringBuilder builder)
        {
            builder.AppendLine("## 3. 生命周期覆盖矩阵");
            builder.AppendLine();
            builder.AppendLine("| 生命周期模块 | 用例数量 | Passed | Failed | Skipped | 覆盖状态 | 备注 |");
            builder.AppendLine("|---|---:|---:|---:|---:|---|---|");
            AppendCoverageRow(builder, BuffSystemLifecycleTestRunner.OnApplyCategory, "Add、Append、RefreshAll、Replace 的 OnApply 触发边界。");
            AppendCoverageRow(builder, BuffSystemLifecycleTestRunner.OnTickCategory, "TickInterval、移除后 Tick、过期后 Tick、多层 Tick 口径。");
            AppendCoverageRow(builder, BuffSystemLifecycleTestRunner.OnRemoveCategory, "Manual Remove、Expire、ClearAll、Remove missing、Replace remove 语义。");
            AppendCoverageRow(builder, BuffSystemLifecycleTestRunner.OnRefreshCategory, "RefreshAll、Append、Replace 与 RemainingFrames 刷新语义。");
            AppendCoverageRow(builder, BuffSystemLifecycleTestRunner.OnStackChangedCategory, "Append、MaxStack、Remove、Expire、RefreshAll、Replace 的 stack delta。");
            AppendCoverageRow(builder, BuffSystemLifecycleTestRunner.InterleavingCategory, "Add/Refresh/Remove/Expire/ClearAll/Replace 交错回调。");
            AppendCoverageRow(builder, BuffSystemLifecycleTestRunner.ContextCategory, "OnApply/OnTick/OnRemove/OnRefresh/OnStackChanged 的 context 字段。");
            builder.AppendLine("| EventTrigger | 0 | 0 | 0 | 0 | NotCovered | 可留到 Trigger 专项。 |");
            builder.AppendLine("| Tag | 0 | 0 | 0 | 0 | NotCovered | 留到 12G。 |");
            builder.AppendLine("| CompressedParallel | 0 | 0 | 0 | 0 | NotCovered | 留到 12I。 |");
            builder.AppendLine();
        }

        private void AppendCoverageRow(StringBuilder builder, string category, string note)
        {
            CountCategory(category, out int total, out int passed, out int failed, out int skipped);
            string state = total == 0 ? "NotCovered" : failed > 0 || skipped > 0 ? "Partial" : "Covered";
            builder.AppendLine($"| {Escape(category)} | {total} | {passed} | {failed} | {skipped} | {state} | {Escape(note)} |");
        }

        private void AppendCategory(StringBuilder builder, string title, string category)
        {
            builder.AppendLine(title);
            builder.AppendLine();
            builder.AppendLine("| CaseName | Status | Expected | Actual | Apply | Tick | Remove | Refresh | StackChanged | LastDelta | Invariants | DurationMs | FailureReason |");
            builder.AppendLine("|---|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---|");
            for (int i = 0; i < Results.Count; i++)
            {
                BuffSystemLifecycleTestCaseResult result = Results[i];
                if (result.Category != category)
                    continue;

                builder.AppendLine($"| {Escape(result.CaseName)} | {Escape(result.Status)} | {Escape(result.Expected)} | {Escape(result.Actual)} | {result.ApplyCount} | {result.TickCount} | {result.RemoveCount} | {result.RefreshCount} | {result.StackChangedCount} | {result.LastStackDelta} | {result.InvariantChecks} | {result.DurationMs:0.###} | {Escape(result.FailureReason)} |");
            }

            builder.AppendLine();
        }

        private void AppendFailures(StringBuilder builder)
        {
            builder.AppendLine("## 11. 失败用例详情");
            builder.AppendLine();
            bool hasFailure = false;
            for (int i = 0; i < Results.Count; i++)
            {
                BuffSystemLifecycleTestCaseResult result = Results[i];
                if (!result.IsFailed)
                    continue;

                hasFailure = true;
                builder.AppendLine($"### {Escape(result.Category)} / {Escape(result.CaseName)}");
                builder.AppendLine();
                builder.AppendLine($"- Expected: {Escape(result.Expected)}");
                builder.AppendLine($"- Actual: {Escape(result.Actual)}");
                builder.AppendLine($"- CallbackCounts: Apply={result.ApplyCount}, Tick={result.TickCount}, Remove={result.RemoveCount}, Refresh={result.RefreshCount}, StackChanged={result.StackChangedCount}, LastDelta={result.LastStackDelta}");
                builder.AppendLine($"- FailureReason: {Escape(result.FailureReason)}");
                builder.AppendLine($"- ExceptionType: {Escape(result.ExceptionType)}");
                builder.AppendLine("- Events:");
                builder.AppendLine("```text");
                for (int e = 0; e < result.Events.Count; e++)
                    builder.AppendLine(result.Events[e]);
                builder.AppendLine("```");
                builder.AppendLine("- Exception:");
                builder.AppendLine("```text");
                builder.AppendLine(result.ExceptionStack ?? string.Empty);
                builder.AppendLine("```");
                builder.AppendLine();
            }

            if (!hasFailure)
                builder.AppendLine("No failed cases.");

            builder.AppendLine();
        }

        private void AppendNotCovered(StringBuilder builder)
        {
            builder.AppendLine("## 12. 未覆盖项");
            builder.AppendLine();
            for (int i = 0; i < NotCovered.Count; i++)
                builder.AppendLine($"- {NotCovered[i]}");

            builder.AppendLine();
        }

        private void AppendConclusion(StringBuilder builder)
        {
            builder.AppendLine("## 13. 风险与结论");
            builder.AppendLine();
            for (int i = 0; i < Notes.Count; i++)
                builder.AppendLine($"- {Notes[i]}");

            builder.AppendLine($"- Current result: {Summary}");
            builder.AppendLine("- This runner does not modify BuffSystem runtime.");
            builder.AppendLine("- This runner does not prove rollback-ready.");
            builder.AppendLine("- This runner does not cover EventTrigger / Tag / CompressedParallel / View behavior.");
        }

        private void RecalculateSummary()
        {
            Total = Results.Count;
            Passed = 0;
            Failed = 0;
            Skipped = 0;
            for (int i = 0; i < Results.Count; i++)
            {
                if (Results[i].IsPassed)
                    Passed++;
                else if (Results[i].IsFailed)
                    Failed++;
                else if (Results[i].IsSkipped)
                    Skipped++;
            }
        }

        private void CountCategory(string category, out int total, out int passed, out int failed, out int skipped)
        {
            total = 0;
            passed = 0;
            failed = 0;
            skipped = 0;
            for (int i = 0; i < Results.Count; i++)
            {
                BuffSystemLifecycleTestCaseResult result = Results[i];
                if (result.Category != category)
                    continue;

                total++;
                if (result.IsPassed)
                    passed++;
                else if (result.IsFailed)
                    failed++;
                else if (result.IsSkipped)
                    skipped++;
            }
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        }
    }
}
