using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BuffSystem.EditorTesting
{
    internal sealed class BuffSystemFunctionalCoverageReport
    {
        internal const string RelativeResultPath = "Assets/_Scripts/FrameWork/BuffSystem/Test/\u529F\u80FD\u8986\u76D6\u6D4B\u8BD5\u7ED3\u679C.md";

        public readonly List<BuffSystemFunctionalCoverageCaseResult> Results = new List<BuffSystemFunctionalCoverageCaseResult>();
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

        public static BuffSystemFunctionalCoverageReport Create()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            BuffSystemFunctionalCoverageReport report = new BuffSystemFunctionalCoverageReport
            {
                StartedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                UnityVersion = Application.unityVersion,
                ProjectPath = projectRoot,
                ResultPath = Path.Combine(projectRoot, RelativeResultPath)
            };

            report.NotCovered.Add("Tag: reserved for a later dedicated coverage phase.");
            report.NotCovered.Add("CompressedParallel: reserved for Phase 3I-12I or an equivalent dedicated storage coverage phase.");
            report.NotCovered.Add("Rollback: external RollBackSystem restore is not claimed ready by this test.");
            report.NotCovered.Add("View / Scene / Prefab: not covered by this Editor-only functional runner.");
            report.Notes.Add("This runner uses in-memory World, BuffDefinitionRegistry, BuffEffectRegistry, and BuffSystemCore instances only.");
            report.Notes.Add("This runner does not create Buff assets, generate Effect files, write registry entries, modify Bootstrap, modify whitelist, or save scenes.");
            return report;
        }

        public void Add(BuffSystemFunctionalCoverageCaseResult result)
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
            builder.AppendLine("# BuffSystem Functional Coverage Test Result");
            builder.AppendLine();
            AppendSummary(builder);
            AppendEnvironment(builder);
            AppendCoverageMatrix(builder);
            AppendCategory(builder, "## 4. Basic Add / Query Tests", "Add / Query");
            AppendCategory(builder, "## 5. Duration / Tick / Expire Tests", "Duration / Expire");
            AppendCategory(builder, "## 6. Stack / Refresh / Replace Tests", "Stack / Refresh / Replace");
            AppendCategory(builder, "## 7. Remove / Clear Tests", "Remove / Clear");
            AppendCategory(builder, "## 8. Source / Target Isolation Tests", "Source / Target");
            AppendCategory(builder, "## 9. Effect / Lifecycle Basic Tests", "Effect / Lifecycle Basic");
            AppendCategory(builder, "## 10. Boundary Tests", "Boundary");
            AppendFailures(builder);
            AppendNotCovered(builder);
            AppendConclusion(builder);
            return builder.ToString();
        }

        private void AppendSummary(StringBuilder builder)
        {
            builder.AppendLine("## 1. Summary");
            builder.AppendLine();
            builder.AppendLine($"- StartedAt: {StartedAt}");
            builder.AppendLine($"- FinishedAt: {FinishedAt}");
            builder.AppendLine($"- UnityVersion: {UnityVersion}");
            builder.AppendLine($"- ProjectPath: {ProjectPath}");
            builder.AppendLine($"- Total: {Total}");
            builder.AppendLine($"- Passed: {Passed}");
            builder.AppendLine($"- Failed: {Failed}");
            builder.AppendLine($"- Skipped: {Skipped}");
            builder.AppendLine($"- Result: {Summary}");
            builder.AppendLine();
        }

        private void AppendEnvironment(StringBuilder builder)
        {
            builder.AppendLine("## 2. Environment");
            builder.AppendLine();
            builder.AppendLine("- Runner: Unity Editor-only menu and executeMethod entry.");
            builder.AppendLine("- Data source: in-memory World, BuffDefinitionRegistry, BuffEffectRegistry, and BuffSystemCore.");
            builder.AppendLine("- Test effect: CountingBuffEffectExecutor registered only inside test-owned registries.");
            builder.AppendLine("- Write scope: markdown report only.");
            builder.AppendLine();
        }

        private void AppendCoverageMatrix(StringBuilder builder)
        {
            builder.AppendLine("## 3. Coverage Matrix");
            builder.AppendLine();
            builder.AppendLine("| Area | Cases | Passed | Failed | Skipped | Status | Notes |");
            builder.AppendLine("|---|---:|---:|---:|---:|---|---|");
            AppendCoverageRow(builder, "Add / Query", "AddBuff, TryGetBuff, GetBuffs, wrong target/source/config queries.");
            AppendCoverageRow(builder, "Duration / Expire", "Limited duration, permanent duration, expire removal, expire OnRemove.");
            AppendCoverageRow(builder, "Stack / Refresh / Replace", "Append, MaxStack, RefreshAll, ReplaceEarliestWhenFull, stack callbacks.");
            AppendCoverageRow(builder, "Remove / Clear", "Manual remove, ClearAll, missing buff remove, double-remove boundary.");
            AppendCoverageRow(builder, "Source / Target", "Same config across targets and sources.");
            AppendCoverageRow(builder, "Effect / Lifecycle Basic", "OnApply, OnTick, OnRemove, OnRefresh, OnStackChanged, context.");
            AppendCoverageRow(builder, "Boundary", "MaxStack=1, Duration=1, TickInterval>Duration, invalid config, missing effect.");
            builder.AppendLine("| Tag | 0 | 0 | 0 | 0 | NotCovered | Later dedicated phase. |");
            builder.AppendLine("| CompressedParallel | 0 | 0 | 0 | 0 | NotCovered | Later dedicated storage coverage phase. |");
            builder.AppendLine("| Rollback | 0 | 0 | 0 | 0 | NotCovered | External restore readiness is not claimed. |");
            builder.AppendLine("| View | 0 | 0 | 0 | 0 | NotCovered | Scene and Prefab behavior are out of scope. |");
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
            builder.AppendLine("| Category | CaseName | Status | Expected | Actual | InvariantChecks | DurationMs | FailureReason |");
            builder.AppendLine("|---|---|---|---|---|---:|---:|---|");
            for (int i = 0; i < Results.Count; i++)
            {
                BuffSystemFunctionalCoverageCaseResult result = Results[i];
                if (result.Category != category)
                    continue;

                builder.AppendLine($"| {Escape(result.Category)} | {Escape(result.CaseName)} | {Escape(result.Status)} | {Escape(result.Expected)} | {Escape(result.Actual)} | {result.InvariantChecks} | {result.DurationMs:0.###} | {Escape(result.FailureReason)} |");
            }

            builder.AppendLine();
        }

        private void AppendFailures(StringBuilder builder)
        {
            builder.AppendLine("## 11. Failed Cases");
            builder.AppendLine();
            bool hasFailure = false;
            for (int i = 0; i < Results.Count; i++)
            {
                BuffSystemFunctionalCoverageCaseResult result = Results[i];
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
            builder.AppendLine("## 12. NotCovered");
            builder.AppendLine();
            for (int i = 0; i < NotCovered.Count; i++)
                builder.AppendLine($"- {NotCovered[i]}");

            builder.AppendLine();
        }

        private void AppendConclusion(StringBuilder builder)
        {
            builder.AppendLine("## 13. Risk And Conclusion");
            builder.AppendLine();
            for (int i = 0; i < Notes.Count; i++)
                builder.AppendLine($"- {Notes[i]}");

            builder.AppendLine($"- Current result: {Summary}");
            builder.AppendLine("- This runner does not modify BuffSystem runtime.");
            builder.AppendLine("- This runner does not prove rollback-ready.");
            builder.AppendLine("- This runner does not cover View / Scene / Prefab behavior.");
        }

        private void CountCategory(string category, out int total, out int passed, out int failed, out int skipped)
        {
            total = 0;
            passed = 0;
            failed = 0;
            skipped = 0;

            for (int i = 0; i < Results.Count; i++)
            {
                BuffSystemFunctionalCoverageCaseResult result = Results[i];
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

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        }
    }
}
