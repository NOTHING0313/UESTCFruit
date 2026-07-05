using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BuffSystem.EditorTesting
{
    internal sealed class BuffSystemTriggerTestReport
    {
        internal const string RelativeResultPath = "Assets/_Scripts/FrameWork/BuffSystem/Test/\u89E6\u53D1\u5668\u6D4B\u8BD5\u7ED3\u679C.md";

        public readonly List<BuffSystemTriggerTestCaseResult> Results = new List<BuffSystemTriggerTestCaseResult>();
        public readonly List<string> CapabilityNotes = new List<string>();
        public readonly List<string> NotCovered = new List<string>();
        public readonly List<string> Notes = new List<string>();

        public string StartedAt;
        public string FinishedAt;
        public string UnityVersion;
        public string ProjectPath;
        public string ResultPath;
        public string ConfigTriggerSupport;
        public string DefinitionTriggerSupport;
        public string RuntimeEventTriggerApi;
        public string EffectEventCallbackSupport;
        public string ExistingTriggerRunnerSupport;
        public bool RuntimeTriggerTested;
        public int Total;
        public int Passed;
        public int Failed;
        public int Skipped;
        public int NotSupported;
        public int ManualRequired;
        public string Summary;

        public bool HasFailures => Failed > 0;

        public static BuffSystemTriggerTestReport Create()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            BuffSystemTriggerTestReport report = new BuffSystemTriggerTestReport
            {
                StartedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                UnityVersion = Application.unityVersion,
                ProjectPath = projectRoot,
                ResultPath = Path.Combine(projectRoot, RelativeResultPath),
                ConfigTriggerSupport = "Unknown",
                DefinitionTriggerSupport = "Unknown",
                RuntimeEventTriggerApi = "Unknown",
                EffectEventCallbackSupport = "Unknown",
                ExistingTriggerRunnerSupport = "Unknown"
            };

            report.NotCovered.Add("Tag：由 Phase 3I-12G Tag runner 覆盖。");
            report.NotCovered.Add("CompressedParallel 深度一致性：由 Phase 3I-12I Storage runner 覆盖；本 runner 只验证 EventTrigger eligibility fallback。");
            report.NotCovered.Add("Rollback：仅记录 OnWorldRestored 后事件索引行为，不宣称 RollBackSystem ready。");
            report.NotCovered.Add("View / Scene / Prefab：不运行 PlayMode，不保存 scene。");
            report.Notes.Add("本 runner 只使用 Editor-only in-memory World / BuffDefinitionRegistry / BuffEffectRegistry / BuffSystemCore。");
            report.Notes.Add("本 runner 不创建 Buff asset，不生成 Effect.cs，不写 ID Registry，不修改 Bootstrap，不修改 whitelist。");
            report.Notes.Add("如果当前 runtime trigger API 缺失，相关用例标记为 NOT_SUPPORTED，而不是修改 runtime。");
            return report;
        }

        public void ApplyCapabilities(BuffSystemTriggerCapabilitySnapshot capabilities)
        {
            if (capabilities == null)
                return;

            ConfigTriggerSupport = capabilities.HasConfigTriggerField ? "Found" : "NotFound";
            DefinitionTriggerSupport = capabilities.HasDefinitionTriggerField ? "Found" : "NotFound";
            RuntimeEventTriggerApi = capabilities.HasRuntimeRaiseApi ? "Found" : "NotFound";
            EffectEventCallbackSupport = capabilities.HasEventEffectCallback ? "Found" : "NotFound";
            ExistingTriggerRunnerSupport = capabilities.HasExistingTriggerRunner ? "Found" : "NotFound";
            RuntimeTriggerTested = capabilities.HasRuntimeRaiseApi && capabilities.HasEventEffectCallback;

            CapabilityNotes.Clear();
            CapabilityNotes.AddRange(capabilities.Notes);
        }

        public void Add(BuffSystemTriggerTestCaseResult result)
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
            else if (ManualRequired > 0)
                Summary = "PARTIAL_MANUAL_REQUIRED";
            else if (!RuntimeTriggerTested)
                Summary = "TRIGGER_RUNTIME_API_NOT_FOUND";
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
            builder.AppendLine("# BuffSystem Trigger / EventTrigger 专项测试结果");
            builder.AppendLine();
            AppendSummary(builder);
            AppendDiscovery(builder);
            AppendEnvironment(builder);
            AppendCoverageMatrix(builder);
            AppendCategory(builder, "## 5. Trigger Discovery", BuffSystemTriggerTestRunner.TriggerDiscoveryCategory);
            AppendCategory(builder, "## 6. Trigger Config", BuffSystemTriggerTestRunner.TriggerConfigCategory);
            AppendCategory(builder, "## 7. Tick Trigger Isolation", BuffSystemTriggerTestRunner.TickTriggerCategory);
            AppendCategory(builder, "## 8. EventTrigger Execution", BuffSystemTriggerTestRunner.EventTriggerCategory);
            AppendCategory(builder, "## 9. Trigger Context", BuffSystemTriggerTestRunner.TriggerContextCategory);
            AppendCategory(builder, "## 10. Lifecycle Interleaving", BuffSystemTriggerTestRunner.LifecycleInterleavingCategory);
            AppendCategory(builder, "## 11. Storage / Eligibility", BuffSystemTriggerTestRunner.StorageEligibilityCategory);
            AppendCategory(builder, "## 12. Boundary", BuffSystemTriggerTestRunner.BoundaryCategory);
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
            builder.AppendLine($"- ManualRequired：{ManualRequired}");
            builder.AppendLine($"- 最终结论：{Summary}");
            builder.AppendLine();
        }

        private void AppendDiscovery(StringBuilder builder)
        {
            builder.AppendLine("## 2. Trigger 能力发现");
            builder.AppendLine();
            builder.AppendLine($"- BuffConfigData trigger 字段：{ConfigTriggerSupport}");
            builder.AppendLine($"- BuffDefinition trigger 字段：{DefinitionTriggerSupport}");
            builder.AppendLine($"- IBuffSystem.Raise<TEvent>：{RuntimeEventTriggerApi}");
            builder.AppendLine($"- IBuffEventEffectExecutor<TEvent>：{EffectEventCallbackSupport}");
            builder.AppendLine($"- 既有 Trigger runner / smoke：{ExistingTriggerRunnerSupport}");
            builder.AppendLine();

            for (int i = 0; i < CapabilityNotes.Count; i++)
                builder.AppendLine($"- {CapabilityNotes[i]}");

            builder.AppendLine();
        }

        private static void AppendEnvironment(StringBuilder builder)
        {
            builder.AppendLine("## 3. 测试环境");
            builder.AppendLine();
            builder.AppendLine("- Runner：Unity Editor-only menu and executeMethod entry.");
            builder.AppendLine("- Data source：in-memory World、BuffDefinitionRegistry、BuffEffectRegistry、BuffSystemCore。");
            builder.AppendLine("- Effect：测试自有 CountingTriggerEffect，同时实现生命周期回调和 IBuffEventEffectExecutor<TEvent>。");
            builder.AppendLine("- Write scope：markdown report only.");
            builder.AppendLine();
        }

        private void AppendCoverageMatrix(StringBuilder builder)
        {
            builder.AppendLine("## 4. Trigger 覆盖矩阵");
            builder.AppendLine();
            builder.AppendLine("| 模块 | 用例数量 | PASS | FAIL | SKIP | NOT_SUPPORTED | MANUAL_REQUIRED | 覆盖状态 | 备注 |");
            builder.AppendLine("|---|---:|---:|---:|---:|---:|---:|---|---|");
            AppendCoverageRow(builder, BuffSystemTriggerTestRunner.TriggerDiscoveryCategory, "Trigger 字段、Raise API、事件 Effect 接口和既有 runner 探测。");
            AppendCoverageRow(builder, BuffSystemTriggerTestRunner.TriggerConfigCategory, "BuffConfigData / BuffDefinition 触发字段复制与转换。");
            AppendCoverageRow(builder, BuffSystemTriggerTestRunner.TickTriggerCategory, "Tick-only 与 EventTrigger hot path 隔离。");
            AppendCoverageRow(builder, BuffSystemTriggerTestRunner.EventTriggerCategory, "EventTrigger Add / Tick / Raise / Remove / Expire / stack。");
            AppendCoverageRow(builder, BuffSystemTriggerTestRunner.TriggerContextCategory, "事件回调上下文 Target / Source / Definition / EventId。");
            AppendCoverageRow(builder, BuffSystemTriggerTestRunner.LifecycleInterleavingCategory, "Add / Event / Remove / Expire / Refresh / Append / ClearAll 交错。");
            AppendCoverageRow(builder, BuffSystemTriggerTestRunner.StorageEligibilityCategory, "EventTrigger compressed eligibility false 与 fallback。");
            AppendCoverageRow(builder, BuffSystemTriggerTestRunner.BoundaryCategory, "未知事件、空 payload 概念、重入边界、restore hook 后 Raise。");
            builder.AppendLine("| Tag | 0 | 0 | 0 | 0 | 0 | 0 | NotCovered | 由 Tag runner 覆盖。 |");
            builder.AppendLine("| Rollback | 0 | 0 | 0 | 0 | 0 | 0 | NotCovered | 不宣称 rollback-ready。 |");
            builder.AppendLine("| View | 0 | 0 | 0 | 0 | 0 | 0 | NotCovered | 不运行 PlayMode，不保存 Scene。 |");
            builder.AppendLine();
        }

        private void AppendCoverageRow(StringBuilder builder, string category, string note)
        {
            CountCategory(category, out int total, out int passed, out int failed, out int skipped, out int notSupported, out int manualRequired);
            string state = total == 0 ? "NotCovered" :
                failed > 0 ? "Failed" :
                manualRequired > 0 ? "PartialManualRequired" :
                notSupported == total ? "NotSupported" :
                notSupported > 0 || skipped > 0 ? "Partial" : "Covered";
            builder.AppendLine($"| {Escape(category)} | {total} | {passed} | {failed} | {skipped} | {notSupported} | {manualRequired} | {state} | {Escape(note)} |");
        }

        private void AppendCategory(StringBuilder builder, string title, string category)
        {
            builder.AppendLine(title);
            builder.AppendLine();
            builder.AppendLine("| CaseName | Category | Status | Expected | Actual | InvariantChecks | TriggerApiAvailability | TriggerType | EventId/TriggerId | ApplyCount | TickCount | EventCount | RemoveCount | RefreshCount | StackChangedCount | FailureReason | Exception | DurationMs | ManualRequiredReason |");
            builder.AppendLine("|---|---|---|---|---|---:|---|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---|");
            for (int i = 0; i < Results.Count; i++)
            {
                BuffSystemTriggerTestCaseResult result = Results[i];
                if (result.Category != category)
                    continue;

                builder.AppendLine($"| {Escape(result.CaseName)} | {Escape(result.Category)} | {Escape(result.Status)} | {Escape(result.Expected)} | {Escape(result.Actual)} | {result.InvariantChecks} | {Escape(result.TriggerApiAvailability)} | {Escape(result.TriggerType)} | {Escape(result.EventIdOrTriggerId)} | {result.ApplyCount} | {result.TickCount} | {result.EventCount} | {result.RemoveCount} | {result.RefreshCount} | {result.StackChangedCount} | {Escape(result.FailureReason)} | {Escape(result.ExceptionType)} | {result.DurationMs:0.###} | {Escape(result.ManualRequiredReason)} |");
            }

            builder.AppendLine();
        }

        private void AppendFailures(StringBuilder builder)
        {
            builder.AppendLine("## 13. 失败用例详情");
            builder.AppendLine();
            bool hasFailure = false;
            for (int i = 0; i < Results.Count; i++)
            {
                BuffSystemTriggerTestCaseResult result = Results[i];
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
            builder.AppendLine("## 14. 未覆盖项");
            builder.AppendLine();
            for (int i = 0; i < NotCovered.Count; i++)
                builder.AppendLine($"- {NotCovered[i]}");

            builder.AppendLine();
        }

        private void AppendConclusion(StringBuilder builder)
        {
            builder.AppendLine("## 15. 风险与结论");
            builder.AppendLine();
            for (int i = 0; i < Notes.Count; i++)
                builder.AppendLine($"- {Notes[i]}");

            builder.AppendLine($"- Current result: {Summary}");
            builder.AppendLine("- EventTrigger 当前按设计不会进入 CompressedParallel production path。");
            builder.AppendLine("- This runner does not modify BuffSystem runtime.");
            builder.AppendLine("- This runner does not prove rollback-ready.");
        }

        private void RecalculateSummary()
        {
            Total = Results.Count;
            Passed = 0;
            Failed = 0;
            Skipped = 0;
            NotSupported = 0;
            ManualRequired = 0;
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
                else if (Results[i].IsManualRequired)
                    ManualRequired++;
            }
        }

        private void CountCategory(string category, out int total, out int passed, out int failed, out int skipped, out int notSupported, out int manualRequired)
        {
            total = 0;
            passed = 0;
            failed = 0;
            skipped = 0;
            notSupported = 0;
            manualRequired = 0;
            for (int i = 0; i < Results.Count; i++)
            {
                BuffSystemTriggerTestCaseResult result = Results[i];
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
                else if (result.IsManualRequired)
                    manualRequired++;
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
