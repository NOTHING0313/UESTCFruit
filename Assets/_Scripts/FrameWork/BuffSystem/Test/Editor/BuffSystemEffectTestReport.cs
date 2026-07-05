using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BuffSystem.EditorTesting
{
    internal sealed class BuffSystemEffectTestReport
    {
        internal const string RelativeResultPath = "Assets/_Scripts/FrameWork/BuffSystem/Test/\u6548\u679C\u6D4B\u8BD5\u7ED3\u679C.md";

        public readonly List<BuffSystemEffectTestCaseResult> Results = new List<BuffSystemEffectTestCaseResult>();
        public readonly List<string> CapabilityNotes = new List<string>();
        public readonly List<string> NotCovered = new List<string>();
        public readonly List<string> Notes = new List<string>();

        public string StartedAt;
        public string FinishedAt;
        public string UnityVersion;
        public string ProjectPath;
        public string ResultPath;
        public string RegistrySupport;
        public string ExecutorBaseSupport;
        public string EventEffectInterfaceSupport;
        public string CompositePatternSupport;
        public string GraphGeneratedPatternSupport;
        public int Total;
        public int Passed;
        public int Failed;
        public int Skipped;
        public int NotSupported;
        public string Summary;

        public bool HasFailures => Failed > 0;
        public bool HasNotSupported => NotSupported > 0;

        public static BuffSystemEffectTestReport Create()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            BuffSystemEffectTestReport report = new BuffSystemEffectTestReport
            {
                StartedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                UnityVersion = Application.unityVersion,
                ProjectPath = projectRoot,
                ResultPath = Path.Combine(projectRoot, RelativeResultPath),
                RegistrySupport = "Unknown",
                ExecutorBaseSupport = "Unknown",
                EventEffectInterfaceSupport = "Unknown",
                CompositePatternSupport = "Unknown",
                GraphGeneratedPatternSupport = "Unknown"
            };

            report.NotCovered.Add("Tag runtime query：Phase 3I-12G 已确认 TAG_RUNTIME_API_NOT_FOUND；本 runner 不补 runtime API。");
            report.NotCovered.Add("CompressedParallel：Phase 3I-12I 已确认 PASS；本 runner 只记录与 Effect 分发相关行为。");
            report.NotCovered.Add("Trigger / EventTrigger：Phase 3I-12L 已确认 PASS；本 runner 只补充 Event Effect 行为。");
            report.NotCovered.Add("Rollback：不修改 RollBackSystem，不宣称 rollback-ready。");
            report.NotCovered.Add("View / Scene / Prefab：不运行 PlayMode，不保存 scene。");
            report.Notes.Add("本 runner 只使用 Editor-only in-memory World / BuffDefinitionRegistry / BuffEffectRegistry / BuffSystemCore。");
            report.Notes.Add("本 runner 不创建 Buff asset，不生成 Effect.cs，不写 ID Registry，不修改 Bootstrap，不修改 whitelist。");
            report.Notes.Add("CompositeEffect 当前通过测试内 double 验证调用顺序；不新增 runtime CompositeEffect 基类。");
            return report;
        }

        public void ApplyCapabilities(BuffSystemEffectCapabilitySnapshot capabilities)
        {
            if (capabilities == null)
                return;

            RegistrySupport = capabilities.HasRegistry ? "Found" : "NotFound";
            ExecutorBaseSupport = capabilities.HasExecutorBase ? "Found" : "NotFound";
            EventEffectInterfaceSupport = capabilities.HasEventEffectInterface ? "Found" : "NotFound";
            CompositePatternSupport = capabilities.HasCompositePattern ? "Found" : "NotFound";
            GraphGeneratedPatternSupport = capabilities.HasGraphGeneratedPattern ? "Found" : "NotFound";
            CapabilityNotes.Clear();
            CapabilityNotes.AddRange(capabilities.Notes);
        }

        public void Add(BuffSystemEffectTestCaseResult result)
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

            if (Failed > 0)
                Summary = "FAIL";
            else if (NotSupported > 0)
                Summary = "PARTIAL_NOT_SUPPORTED";
            else
                Summary = "PASS";
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
            builder.AppendLine("# BuffSystem Effect / CompositeEffect 专项测试结果");
            builder.AppendLine();
            AppendSummary(builder);
            AppendEnvironment(builder);
            AppendDiscovery(builder);
            AppendCoverageMatrix(builder);
            AppendCategory(builder, "## 5. BuffEffectRegistry 测试", BuffSystemEffectTestRunner.RegistryCategory);
            AppendCategory(builder, "## 6. 单 Effect 生命周期测试", BuffSystemEffectTestRunner.SingleLifecycleCategory);
            AppendCategory(builder, "## 7. Missing Effect / Invalid Effect 测试", BuffSystemEffectTestRunner.MissingInvalidCategory);
            AppendCategory(builder, "## 8. CompositeEffect 顺序测试", BuffSystemEffectTestRunner.CompositeOrderCategory);
            AppendCategory(builder, "## 9. CompositeEffect 生命周期分发测试", BuffSystemEffectTestRunner.CompositeLifecycleCategory);
            AppendCategory(builder, "## 10. Event Effect 测试", BuffSystemEffectTestRunner.EventEffectCategory);
            AppendCategory(builder, "## 11. Graph-generated style 调用链测试", BuffSystemEffectTestRunner.GraphStyleCategory);
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
            builder.AppendLine($"- NotSupported：{NotSupported}");
            builder.AppendLine($"- 最终结论：{Summary}");
            builder.AppendLine();
        }

        private static void AppendEnvironment(StringBuilder builder)
        {
            builder.AppendLine("## 2. 测试环境");
            builder.AppendLine();
            builder.AppendLine("- Runner：Unity Editor-only menu and executeMethod entry.");
            builder.AppendLine("- Data source：in-memory World、BuffDefinitionRegistry、BuffEffectRegistry、BuffSystemCore。");
            builder.AppendLine("- Test doubles：CountingEffect、CountingEventEffect、CompositeTestEffect、GraphStyleEffect。");
            builder.AppendLine("- Write scope：markdown report only.");
            builder.AppendLine();
        }

        private void AppendDiscovery(StringBuilder builder)
        {
            builder.AppendLine("## 3. Effect 能力发现");
            builder.AppendLine();
            builder.AppendLine($"- BuffEffectRegistry：{RegistrySupport}");
            builder.AppendLine($"- BuffEffectExecutorBase：{ExecutorBaseSupport}");
            builder.AppendLine($"- IBuffEventEffectExecutor<TEvent>：{EventEffectInterfaceSupport}");
            builder.AppendLine($"- CompositeEffect authoring pattern：{CompositePatternSupport}");
            builder.AppendLine($"- Graph-generated style pattern：{GraphGeneratedPatternSupport}");
            builder.AppendLine();

            for (int i = 0; i < CapabilityNotes.Count; i++)
                builder.AppendLine($"- {CapabilityNotes[i]}");

            builder.AppendLine();
            AppendCaseTable(builder, BuffSystemEffectTestRunner.DiscoveryCategory);
            builder.AppendLine();
        }

        private void AppendCoverageMatrix(StringBuilder builder)
        {
            builder.AppendLine("## 4. 覆盖矩阵");
            builder.AppendLine();
            builder.AppendLine("| 模块 | 用例数量 | PASS | FAIL | SKIP | NOT_SUPPORTED | 覆盖状态 | 备注 |");
            builder.AppendLine("|---|---:|---:|---:|---:|---:|---|---|");
            AppendCoverageRow(builder, BuffSystemEffectTestRunner.DiscoveryCategory, "Effect registry、executor base、event effect、CompositeEffect 和 graph-generated style 探测。");
            AppendCoverageRow(builder, BuffSystemEffectTestRunner.RegistryCategory, "Register / duplicate replace / Remove / Clear / missing id / local registry isolation。");
            AppendCoverageRow(builder, BuffSystemEffectTestRunner.SingleLifecycleCategory, "OnApply / OnTick / OnRemove / OnRefresh / OnStackChanged / context。");
            AppendCoverageRow(builder, BuffSystemEffectTestRunner.MissingInvalidCategory, "Missing EffectId、invalid EffectId 与 public view 行为文档化。");
            AppendCoverageRow(builder, BuffSystemEffectTestRunner.CompositeOrderCategory, "Composite action declared order and lifecycle-separated traces。");
            AppendCoverageRow(builder, BuffSystemEffectTestRunner.CompositeLifecycleCategory, "通过 BuffSystemCore 验证 CompositeEffect 生命周期分发。");
            AppendCoverageRow(builder, BuffSystemEffectTestRunner.EventEffectCategory, "EventTrigger matching / wrong event / tick isolation / remove stop / context。");
            AppendCoverageRow(builder, BuffSystemEffectTestRunner.GraphStyleCategory, "Graph-generated style readonly action fields and Execute(in context) 调用链。");
            builder.AppendLine("| Tag Runtime Query | 0 | 0 | 0 | 0 | 0 | NotSupported | Phase 3I-12G 已确认 TAG_RUNTIME_API_NOT_FOUND。 |");
            builder.AppendLine("| CompressedParallel | 0 | 0 | 0 | 0 | 0 | Covered | Phase 3I-12I 已确认 PASS。 |");
            builder.AppendLine("| Trigger / EventTrigger | 0 | 0 | 0 | 0 | 0 | Covered | Phase 3I-12L 已确认 PASS。 |");
            builder.AppendLine("| Rollback | 0 | 0 | 0 | 0 | 0 | NotCovered | 不宣称 rollback-ready。 |");
            builder.AppendLine("| View | 0 | 0 | 0 | 0 | 0 | NotCovered | 不运行 PlayMode，不保存 Scene。 |");
            builder.AppendLine();
        }

        private void AppendCoverageRow(StringBuilder builder, string category, string note)
        {
            CountCategory(category, out int total, out int passed, out int failed, out int skipped, out int notSupported);
            string state = total == 0 ? "NotCovered" :
                failed > 0 ? "Failed" :
                notSupported == total ? "NotSupported" :
                notSupported > 0 || skipped > 0 ? "Partial" : "Covered";
            builder.AppendLine($"| {Escape(category)} | {total} | {passed} | {failed} | {skipped} | {notSupported} | {state} | {Escape(note)} |");
        }

        private void AppendCategory(StringBuilder builder, string title, string category)
        {
            builder.AppendLine(title);
            builder.AppendLine();
            AppendCaseTable(builder, category);
            builder.AppendLine();
        }

        private void AppendCaseTable(StringBuilder builder, string category)
        {
            builder.AppendLine("| CaseName | Category | Status | Expected | Actual | EffectId | ApplyCount | TickCount | RemoveCount | RefreshCount | StackChangedCount | EventCount | ExecutionOrderTrace | ContextSnapshot | InvariantChecks | FailureReason | Exception | DurationMs |");
            builder.AppendLine("|---|---|---|---|---|---:|---:|---:|---:|---:|---:|---:|---|---|---:|---|---|---:|");
            for (int i = 0; i < Results.Count; i++)
            {
                BuffSystemEffectTestCaseResult result = Results[i];
                if (result.Category != category)
                    continue;

                builder.AppendLine($"| {Escape(result.CaseName)} | {Escape(result.Category)} | {Escape(result.Status)} | {Escape(result.Expected)} | {Escape(result.Actual)} | {result.EffectId} | {result.ApplyCount} | {result.TickCount} | {result.RemoveCount} | {result.RefreshCount} | {result.StackChangedCount} | {result.EventCount} | {Escape(result.ExecutionOrderTrace)} | {Escape(result.ContextSnapshot)} | {result.InvariantChecks} | {Escape(result.FailureReason)} | {Escape(result.ExceptionType)} | {result.DurationMs:0.###} |");
            }
        }

        private void AppendFailures(StringBuilder builder)
        {
            builder.AppendLine("## 12. 失败用例详情");
            builder.AppendLine();
            bool hasFailure = false;
            for (int i = 0; i < Results.Count; i++)
            {
                BuffSystemEffectTestCaseResult result = Results[i];
                if (!result.IsFailed)
                    continue;

                hasFailure = true;
                builder.AppendLine($"### {Escape(result.Category)} / {Escape(result.CaseName)}");
                builder.AppendLine();
                builder.AppendLine($"- Expected: {Escape(result.Expected)}");
                builder.AppendLine($"- Actual: {Escape(result.Actual)}");
                builder.AppendLine($"- FailureReason: {Escape(result.FailureReason)}");
                builder.AppendLine($"- ExceptionType: {Escape(result.ExceptionType)}");
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
            builder.AppendLine("## 13. 未覆盖项");
            builder.AppendLine();
            for (int i = 0; i < NotCovered.Count; i++)
                builder.AppendLine($"- {NotCovered[i]}");

            builder.AppendLine();
        }

        private void AppendConclusion(StringBuilder builder)
        {
            builder.AppendLine("## 14. 风险与结论");
            builder.AppendLine();
            for (int i = 0; i < Notes.Count; i++)
                builder.AppendLine($"- {Notes[i]}");

            builder.AppendLine($"- Current result: {Summary}");
            builder.AppendLine("- This runner does not modify BuffSystem runtime.");
            builder.AppendLine("- This runner does not register production effects.");
            builder.AppendLine("- This runner does not prove rollback-ready.");
        }

        private void RecalculateSummary()
        {
            Total = Results.Count;
            Passed = 0;
            Failed = 0;
            Skipped = 0;
            NotSupported = 0;
            for (int i = 0; i < Results.Count; i++)
            {
                if (Results[i].IsPassed)
                    Passed++;
                else if (Results[i].IsFailed)
                    Failed++;
                else if (Results[i].IsSkipped)
                    Skipped++;
                else if (Results[i].IsNotSupported)
                    NotSupported++;
            }
        }

        private void CountCategory(string category, out int total, out int passed, out int failed, out int skipped, out int notSupported)
        {
            total = 0;
            passed = 0;
            failed = 0;
            skipped = 0;
            notSupported = 0;
            for (int i = 0; i < Results.Count; i++)
            {
                BuffSystemEffectTestCaseResult result = Results[i];
                if (result.Category != category)
                    continue;

                total++;
                if (result.IsPassed)
                    passed++;
                else if (result.IsFailed)
                    failed++;
                else if (result.IsSkipped)
                    skipped++;
                else if (result.IsNotSupported)
                    notSupported++;
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
