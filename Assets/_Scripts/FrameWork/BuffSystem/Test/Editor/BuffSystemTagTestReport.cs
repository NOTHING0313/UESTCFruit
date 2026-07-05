using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BuffSystem.EditorTesting
{
    internal sealed class BuffSystemTagTestReport
    {
        internal const string RelativeResultPath = "Assets/_Scripts/FrameWork/BuffSystem/Test/\u6807\u7B7E\u6D4B\u8BD5\u7ED3\u679C.md";

        public readonly List<BuffSystemTagTestCaseResult> Results = new List<BuffSystemTagTestCaseResult>();
        public readonly List<string> CapabilityNotes = new List<string>();
        public readonly List<string> NotCovered = new List<string>();
        public readonly List<string> Notes = new List<string>();

        public string StartedAt;
        public string FinishedAt;
        public string UnityVersion;
        public string ProjectPath;
        public string ResultPath;
        public string TagRuntimeApi;
        public string TagCoverage;
        public string DefinitionTagSupport;
        public string ConfigTagSupport;
        public string PublicQueryByTagSupport;
        public string CleanupSupport;
        public int Total;
        public int Passed;
        public int Failed;
        public int Skipped;
        public int NotSupported;
        public string Summary;

        public bool HasFailures => Failed > 0;

        public static BuffSystemTagTestReport Create()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            BuffSystemTagTestReport report = new BuffSystemTagTestReport
            {
                StartedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                UnityVersion = Application.unityVersion,
                ProjectPath = projectRoot,
                ResultPath = Path.Combine(projectRoot, RelativeResultPath),
                TagRuntimeApi = "Unknown",
                TagCoverage = "Unknown",
                DefinitionTagSupport = "Unknown",
                ConfigTagSupport = "Unknown",
                PublicQueryByTagSupport = "Unknown",
                CleanupSupport = "Unknown"
            };

            report.NotCovered.Add("EventTrigger: not covered by this Tag runner; reserved for a Trigger dedicated phase.");
            report.NotCovered.Add("CompressedParallel: not covered by this Tag runner; reserved for Phase 3I-12I.");
            report.NotCovered.Add("Rollback: external RollBackSystem restore is not claimed ready by this test.");
            report.NotCovered.Add("View / Scene / Prefab: not covered by this Editor-only Tag runner.");
            report.Notes.Add("This runner discovers existing Tag support and does not add runtime Tag API.");
            report.Notes.Add("This runner does not create Buff assets, generate Effect files, write registry entries, modify Bootstrap, modify whitelist, or save scenes.");
            return report;
        }

        public void ApplyCapabilities(BuffSystemTagCapabilitySnapshot capabilities)
        {
            if (capabilities == null)
                return;

            TagRuntimeApi = capabilities.HasRuntimeTagQueryApi ? "Implemented" : "NotImplemented / NotFound";
            TagCoverage = capabilities.HasRuntimeTagQueryApi ? "RuntimeTagQueryAvailable" : "NotSupportedByCurrentRuntime";
            DefinitionTagSupport = capabilities.HasDefinitionTagField ? "Found" : "NotFound";
            ConfigTagSupport = capabilities.HasConfigTagField ? "Found" : "NotFound";
            PublicQueryByTagSupport = capabilities.HasRuntimeTagQueryApi ? "Found" : "NotFound";
            CleanupSupport = capabilities.HasRuntimeTagQueryApi && capabilities.HasRuntimeTagCleanupSignal ? "Found" : "NotFound";

            CapabilityNotes.Clear();
            CapabilityNotes.AddRange(capabilities.Notes);
        }

        public void Add(BuffSystemTagTestCaseResult result)
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
            Summary = Failed > 0 ? "FAIL" : TagRuntimeApi == "Implemented" ? "PASS" : "TAG_RUNTIME_API_NOT_FOUND";
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
            builder.AppendLine("# BuffSystem Tag 专项测试结果");
            builder.AppendLine();
            AppendSummary(builder);
            AppendDiscovery(builder);
            AppendEnvironment(builder);
            AppendCoverageMatrix(builder);
            AppendCategory(builder, "## 5. Tag 配置测试", BuffSystemTagTestRunner.TagConfigCategory);
            AppendCategory(builder, "## 6. Tag 查询测试", BuffSystemTagTestRunner.TagQueryCategory);
            AppendCategory(builder, "## 7. 多 Tag 测试", BuffSystemTagTestRunner.MultiTagCategory);
            AppendCategory(builder, "## 8. Target / Source 隔离测试", BuffSystemTagTestRunner.IsolationCategory);
            AppendCategory(builder, "## 9. Stack / Refresh / Replace 与 Tag 测试", BuffSystemTagTestRunner.StackCategory);
            AppendCategory(builder, "## 10. Remove / Expire 与 Tag 清理测试", BuffSystemTagTestRunner.CleanupCategory);
            AppendCategory(builder, "## 11. 边界测试", BuffSystemTagTestRunner.BoundaryCategory);
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

        private void AppendDiscovery(StringBuilder builder)
        {
            builder.AppendLine("## 2. Tag 能力发现");
            builder.AppendLine();
            builder.AppendLine($"- TagRuntimeApi：{TagRuntimeApi}");
            builder.AppendLine($"- TagCoverage：{TagCoverage}");
            builder.AppendLine($"- BuffDefinition Tag 字段：{DefinitionTagSupport}");
            builder.AppendLine($"- BuffConfigData Tag 字段：{ConfigTagSupport}");
            builder.AppendLine($"- BuffSystem public Tag query API：{PublicQueryByTagSupport}");
            builder.AppendLine($"- runtime Tag cleanup 行为：{CleanupSupport}");
            builder.AppendLine();

            for (int i = 0; i < CapabilityNotes.Count; i++)
                builder.AppendLine($"- {CapabilityNotes[i]}");

            builder.AppendLine();
        }

        private void AppendEnvironment(StringBuilder builder)
        {
            builder.AppendLine("## 3. 测试环境");
            builder.AppendLine();
            builder.AppendLine("- Runner：Unity Editor-only menu and executeMethod entry.");
            builder.AppendLine("- Discovery：reflection over BuffConfigData、BuffDefinition、IBuffSystem、BuffConfigDataLoader、TagRegistry、BuffSystemCore。");
            builder.AppendLine("- Data source：Config tests only create temporary in-memory BuffConfigData instances and destroy them immediately.");
            builder.AppendLine("- Write scope：markdown report only.");
            builder.AppendLine();
        }

        private void AppendCoverageMatrix(StringBuilder builder)
        {
            builder.AppendLine("## 4. Tag 覆盖矩阵");
            builder.AppendLine();
            builder.AppendLine("| Tag 模块 | 用例数量 | Passed | Failed | Skipped | NotSupported | 覆盖状态 | 备注 |");
            builder.AppendLine("|---|---:|---:|---:|---:|---:|---|---|");
            AppendCoverageRow(builder, BuffSystemTagTestRunner.TagDiscoveryCategory, "Tag 能力发现，不存在能力记为 NotSupported。");
            AppendCoverageRow(builder, BuffSystemTagTestRunner.TagConfigCategory, "BuffConfigData authoring Tags 字段与 CopyTo 行为。");
            AppendCoverageRow(builder, BuffSystemTagTestRunner.TagQueryCategory, "需要 runtime public Tag query API；当前不强造。");
            AppendCoverageRow(builder, BuffSystemTagTestRunner.MultiTagCategory, "需要 runtime public Tag query API 与 Any / All 语义。");
            AppendCoverageRow(builder, BuffSystemTagTestRunner.IsolationCategory, "需要 runtime public Tag query API。");
            AppendCoverageRow(builder, BuffSystemTagTestRunner.StackCategory, "需要 runtime public Tag query API 与 stack 语义。");
            AppendCoverageRow(builder, BuffSystemTagTestRunner.CleanupCategory, "需要 runtime public Tag query API 与 cleanup index。");
            AppendCoverageRow(builder, BuffSystemTagTestRunner.BoundaryCategory, "需要 runtime public Tag query API 或明确 Tag 类型。");
            builder.AppendLine("| EventTrigger | 0 | 0 | 0 | 0 | 0 | NotCovered | 留到 Trigger 专项。 |");
            builder.AppendLine("| CompressedParallel | 0 | 0 | 0 | 0 | 0 | NotCovered | 留到 12I。 |");
            builder.AppendLine("| Rollback | 0 | 0 | 0 | 0 | 0 | NotCovered | 后续大阶段。 |");
            builder.AppendLine("| View | 0 | 0 | 0 | 0 | 0 | NotCovered | 后续大阶段。 |");
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
            builder.AppendLine("| CaseName | Status | Expected | Actual | InvariantChecks | DurationMs | TagApiAvailability | FailureReason |");
            builder.AppendLine("|---|---|---|---|---:|---:|---|---|");
            for (int i = 0; i < Results.Count; i++)
            {
                BuffSystemTagTestCaseResult result = Results[i];
                if (result.Category != category)
                    continue;

                builder.AppendLine($"| {Escape(result.CaseName)} | {Escape(result.Status)} | {Escape(result.Expected)} | {Escape(result.Actual)} | {result.InvariantChecks} | {result.DurationMs:0.###} | {Escape(result.TagApiAvailability)} | {Escape(result.FailureReason)} |");
            }

            builder.AppendLine();
        }

        private void AppendFailures(StringBuilder builder)
        {
            builder.AppendLine("## 12. 失败用例详情");
            builder.AppendLine();
            bool hasFailure = false;
            for (int i = 0; i < Results.Count; i++)
            {
                BuffSystemTagTestCaseResult result = Results[i];
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
            builder.AppendLine("- Current runtime Tag conclusion: BuffConfigDataLoader has authoring/config Tag lookup, but IBuffSystem does not expose live runtime Tag query.");
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
                BuffSystemTagTestCaseResult result = Results[i];
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
