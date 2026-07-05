using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BuffSystem.EditorTesting
{
    internal sealed class BuffSystemStorageTestReport
    {
        internal const string RelativeResultPath = "Assets/_Scripts/FrameWork/BuffSystem/Test/\u5B58\u50A8\u6A21\u5F0F\u6D4B\u8BD5\u7ED3\u679C.md";

        public readonly List<BuffSystemStorageTestCaseResult> Results = new List<BuffSystemStorageTestCaseResult>();
        public readonly List<string> NotCovered = new List<string>();
        public readonly List<string> ManualScene = new List<string>();
        public readonly List<string> SmokeOnly = new List<string>();
        public readonly List<string> Notes = new List<string>();
        public readonly List<FailureClassificationEntry> FailureClassifications = new List<FailureClassificationEntry>();
        public readonly List<ReproCaseEntry> ReproCases = new List<ReproCaseEntry>();

        public string StartedAt;
        public string FinishedAt;
        public string UnityVersion;
        public string ProjectPath;
        public string ResultPath;
        public int Total;
        public int Passed;
        public int Failed;
        public int Skipped;
        public int ManualRequired;
        public string Summary;

        public bool HasFailures => Failed > 0;
        public bool HasManualRequired => ManualRequired > 0;

        public static BuffSystemStorageTestReport Create()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            BuffSystemStorageTestReport report = new BuffSystemStorageTestReport
            {
                StartedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                UnityVersion = Application.unityVersion,
                ProjectPath = projectRoot,
                ResultPath = Path.Combine(projectRoot, RelativeResultPath)
            };

            report.NotCovered.Add("EventTrigger: compressed path intentionally falls back to EntityPerStack and is covered by trigger-specific phases.");
            report.NotCovered.Add("Rollback: external RollBackSystem restore readiness is not claimed by this storage runner.");
            report.NotCovered.Add("View / Scene / Prefab: not covered by this Editor-only storage runner.");
            report.SmokeOnly.Add("991001 Debug_CompressedParallel_TickSmoke remains a production smoke pilot, not a gameplay Buff candidate.");
            report.Notes.Add("This runner uses in-memory World, BuffDefinitionRegistry, BuffEffectRegistry, and BuffSystemCore only.");
            report.Notes.Add("This runner does not create Buff assets, generate Effect files, write registry entries, modify Bootstrap, modify whitelist, or save scenes.");
            report.Notes.Add("Compressed automation uses reflection to call the existing internal validation factory; if unavailable, compressed cases become MANUAL_REQUIRED.");
            return report;
        }

        public void Add(BuffSystemStorageTestCaseResult result)
        {
            if (result == null)
                return;

            Results.Add(result);
            RecalculateSummary();
        }

        public void AddFailureClassification(string caseName, string classification, string evidence, string recommendedNextStep)
        {
            FailureClassifications.Add(new FailureClassificationEntry(caseName, classification, evidence, recommendedNextStep));
        }

        public void AddReproCase(string reproCase, string status, string classification, string keyEvidence)
        {
            ReproCases.Add(new ReproCaseEntry(reproCase, status, classification, keyEvidence));
        }

        public void Finish()
        {
            FinishedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            RecalculateSummary();
            Summary = Failed > 0 ? "FAIL" : ManualRequired > 0 ? "PARTIAL_MANUAL_REQUIRED" : "PASS";
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
            builder.AppendLine("# BuffSystem Storage / CompressedParallel 自动化测试结果");
            builder.AppendLine();
            AppendSummary(builder);
            AppendEnvironment(builder);
            AppendCoverageMatrix(builder);
            AppendCategory(builder, "## 4. Discovery", BuffSystemStorageTestRunner.DiscoveryCategory);
            AppendCategory(builder, "## 5. EntityPerStack Baseline", BuffSystemStorageTestRunner.EntityBaselineCategory);
            AppendCategory(builder, "## 6. Compressed Eligibility", BuffSystemStorageTestRunner.CompressedEligibilityCategory);
            AppendCategory(builder, "## 7. EntityPerStack vs Compressed", BuffSystemStorageTestRunner.CompareCategory);
            AppendCategory(builder, "## 8. Restore Hook / Cache", BuffSystemStorageTestRunner.RestoreHookCategory);
            AppendCategory(builder, "## 9. Repro Cases", BuffSystemStorageTestRunner.ReproCategory);
            AppendCategory(builder, "## 10. Performance Snapshot", BuffSystemStorageTestRunner.PerformanceCategory);
            AppendFailureClassificationSummary(builder);
            AppendReproCases(builder);
            AppendFailures(builder);
            AppendManualRequired(builder);
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
            builder.AppendLine($"- ManualRequired: {ManualRequired}");
            builder.AppendLine($"- Result: {Summary}");
            builder.AppendLine();
        }

        private void AppendEnvironment(StringBuilder builder)
        {
            builder.AppendLine("## 2. Environment");
            builder.AppendLine();
            builder.AppendLine("- Runner: Unity Editor-only menu and executeMethod entry.");
            builder.AppendLine("- Data source: in-memory World, BuffDefinitionRegistry, BuffEffectRegistry, and BuffSystemCore.");
            builder.AppendLine("- Write scope: markdown report only.");
            builder.AppendLine("- Compressed path: reflected internal validation factory when available; no runtime code is modified.");
            builder.AppendLine();
        }

        private void AppendCoverageMatrix(StringBuilder builder)
        {
            builder.AppendLine("## 3. Coverage Matrix");
            builder.AppendLine();
            builder.AppendLine("| Area | Cases | Passed | Failed | Skipped | ManualRequired | Status | Notes |");
            builder.AppendLine("|---|---:|---:|---:|---:|---:|---|---|");
            AppendCoverageRow(builder, BuffSystemStorageTestRunner.DiscoveryCategory, "Storage factories, eligibility reflection, existing runner discovery.");
            AppendCoverageRow(builder, BuffSystemStorageTestRunner.EntityBaselineCategory, "EntityPerStack Add / Tick / Remove / Refresh / Replace / Expire / isolation baseline.");
            AppendCoverageRow(builder, BuffSystemStorageTestRunner.CompressedEligibilityCategory, "Compressed eligibility utility for valid, invalid, fallback, and 991001 smoke definitions.");
            AppendCoverageRow(builder, BuffSystemStorageTestRunner.CompareCategory, "Public API consistency between EntityPerStack and CompressedParallel when safe automation is available.");
            AppendCoverageRow(builder, BuffSystemStorageTestRunner.RestoreHookCategory, "OnWorldRestored cache rebuild behavior when safe reflection access is available.");
            AppendCoverageRow(builder, BuffSystemStorageTestRunner.ReproCategory, "Minimal repro diagnostics for storage timing, restore-cache, and remaining-frame cases.");
            AppendCoverageRow(builder, BuffSystemStorageTestRunner.PerformanceCategory, "Small editor-only timing snapshot; time is informational and does not fail by threshold.");
            builder.AppendLine("| Tag Runtime Query | 0 | 0 | 0 | 0 | 0 | NotCovered | Covered by Phase 3I-12G dedicated Tag runner. |");
            builder.AppendLine("| EventTrigger | 0 | 0 | 0 | 0 | 0 | NotCovered | EventTrigger is not a compressed whitelist candidate. |");
            builder.AppendLine("| Rollback | 0 | 0 | 0 | 0 | 0 | NotCovered | External RollBackSystem restore is out of scope. |");
            builder.AppendLine("| View | 0 | 0 | 0 | 0 | 0 | NotCovered | Scene and Prefab behavior are out of scope. |");
            builder.AppendLine();
        }

        private void AppendCoverageRow(StringBuilder builder, string category, string note)
        {
            CountCategory(category, out int total, out int passed, out int failed, out int skipped, out int manualRequired);
            string state = total == 0 ? "NotCovered" :
                failed > 0 ? "Failed" :
                manualRequired > 0 || skipped > 0 ? "Partial" : "Covered";
            builder.AppendLine($"| {Escape(category)} | {total} | {passed} | {failed} | {skipped} | {manualRequired} | {state} | {Escape(note)} |");
        }

        private void AppendCategory(StringBuilder builder, string title, string category)
        {
            builder.AppendLine(title);
            builder.AppendLine();
            builder.AppendLine("| CaseName | Status | StorageMode | Expected | Actual | ExpectedCounts | ActualCounts | InvariantChecks | DurationMs | Classification | KeyEvidence | ManualRequiredReason | FailureReason |");
            builder.AppendLine("|---|---|---|---|---|---|---|---:|---:|---|---|---|---|");
            for (int i = 0; i < Results.Count; i++)
            {
                BuffSystemStorageTestCaseResult result = Results[i];
                if (result.Category != category)
                    continue;

                builder.AppendLine($"| {Escape(result.CaseName)} | {Escape(result.Status)} | {Escape(result.StorageMode)} | {Escape(result.Expected)} | {Escape(result.Actual)} | {Escape(result.ExpectedCounts)} | {Escape(result.ActualCounts)} | {result.InvariantChecks} | {result.DurationMs:0.###} | {Escape(result.Classification)} | {Escape(result.KeyEvidence)} | {Escape(result.ManualRequiredReason)} | {Escape(result.FailureReason)} |");
            }

            builder.AppendLine();
        }

        private void AppendFailures(StringBuilder builder)
        {
            builder.AppendLine("## 12. Failed Cases");
            builder.AppendLine();
            bool hasFailure = false;
            for (int i = 0; i < Results.Count; i++)
            {
                BuffSystemStorageTestCaseResult result = Results[i];
                if (!result.IsFailed)
                    continue;

                hasFailure = true;
                builder.AppendLine($"### {Escape(result.Category)} / {Escape(result.CaseName)}");
                builder.AppendLine();
                builder.AppendLine($"- Expected: {Escape(result.Expected)}");
                builder.AppendLine($"- Actual: {Escape(result.Actual)}");
                builder.AppendLine($"- StorageMode: {Escape(result.StorageMode)}");
                builder.AppendLine($"- ExpectedCounts: {Escape(result.ExpectedCounts)}");
                builder.AppendLine($"- ActualCounts: {Escape(result.ActualCounts)}");
                builder.AppendLine($"- Classification: {Escape(result.Classification)}");
                builder.AppendLine($"- EntitySnapshot: {Escape(result.EntitySnapshot)}");
                builder.AppendLine($"- CompressedSnapshot: {Escape(result.CompressedSnapshot)}");
                builder.AppendLine($"- Timeline: {Escape(result.Timeline)}");
                builder.AppendLine($"- ReproResult: {Escape(result.ReproResult)}");
                builder.AppendLine($"- KeyEvidence: {Escape(result.KeyEvidence)}");
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

        private void AppendFailureClassificationSummary(StringBuilder builder)
        {
            builder.AppendLine("## 10. Failure Classification Summary");
            builder.AppendLine();
            builder.AppendLine("| Case | Classification | Evidence | Recommended Next Step |");
            builder.AppendLine("|---|---|---|---|");
            if (FailureClassifications.Count == 0)
            {
                builder.AppendLine("| None | None | No classified failures recorded. | None |");
                builder.AppendLine();
                return;
            }

            for (int i = 0; i < FailureClassifications.Count; i++)
            {
                FailureClassificationEntry entry = FailureClassifications[i];
                builder.AppendLine($"| {Escape(entry.CaseName)} | {Escape(entry.Classification)} | {Escape(entry.Evidence)} | {Escape(entry.RecommendedNextStep)} |");
            }

            builder.AppendLine();
        }

        private void AppendReproCases(StringBuilder builder)
        {
            builder.AppendLine("## 11. Repro Cases");
            builder.AppendLine();
            builder.AppendLine("| ReproCase | Status | Classification | Key Evidence |");
            builder.AppendLine("|---|---|---|---|");
            if (ReproCases.Count == 0)
            {
                builder.AppendLine("| None | None | None | No repro cases recorded. |");
                builder.AppendLine();
                return;
            }

            for (int i = 0; i < ReproCases.Count; i++)
            {
                ReproCaseEntry entry = ReproCases[i];
                builder.AppendLine($"| {Escape(entry.ReproCase)} | {Escape(entry.Status)} | {Escape(entry.Classification)} | {Escape(entry.KeyEvidence)} |");
            }

            builder.AppendLine();
        }

        private void AppendManualRequired(StringBuilder builder)
        {
            builder.AppendLine("## 13. ManualRequired");
            builder.AppendLine();
            bool hasManual = false;
            for (int i = 0; i < Results.Count; i++)
            {
                BuffSystemStorageTestCaseResult result = Results[i];
                if (!result.IsManualRequired)
                    continue;

                hasManual = true;
                builder.AppendLine($"- {Escape(result.Category)} / {Escape(result.CaseName)}: {Escape(result.ManualRequiredReason)}");
            }

            if (!hasManual)
                builder.AppendLine("No manual-required cases.");

            builder.AppendLine();
        }

        private void AppendNotCovered(StringBuilder builder)
        {
            builder.AppendLine("## 14. NotCovered / ManualScene / SmokeOnly");
            builder.AppendLine();
            builder.AppendLine("### NotCovered");
            for (int i = 0; i < NotCovered.Count; i++)
                builder.AppendLine($"- {NotCovered[i]}");

            builder.AppendLine();
            builder.AppendLine("### ManualScene");
            if (ManualScene.Count == 0)
                builder.AppendLine("- None.");
            else
            {
                for (int i = 0; i < ManualScene.Count; i++)
                    builder.AppendLine($"- {ManualScene[i]}");
            }

            builder.AppendLine();
            builder.AppendLine("### SmokeOnly");
            for (int i = 0; i < SmokeOnly.Count; i++)
                builder.AppendLine($"- {SmokeOnly[i]}");

            builder.AppendLine();
        }

        private void AppendConclusion(StringBuilder builder)
        {
            builder.AppendLine("## 15. Risk And Conclusion");
            builder.AppendLine();
            for (int i = 0; i < Notes.Count; i++)
                builder.AppendLine($"- {Notes[i]}");

            builder.AppendLine($"- Current result: {Summary}");
            builder.AppendLine("- This runner does not modify BuffSystem runtime.");
            builder.AppendLine("- This runner does not modify compressed whitelist / eligibility.");
            builder.AppendLine("- This runner does not prove rollback-ready.");
        }

        private void RecalculateSummary()
        {
            Total = Results.Count;
            Passed = 0;
            Failed = 0;
            Skipped = 0;
            ManualRequired = 0;

            for (int i = 0; i < Results.Count; i++)
            {
                BuffSystemStorageTestCaseResult result = Results[i];
                if (result.IsPassed)
                    Passed++;
                else if (result.IsFailed)
                    Failed++;
                else if (result.IsSkipped)
                    Skipped++;
                else if (result.IsManualRequired)
                    ManualRequired++;
            }
        }

        private void CountCategory(string category, out int total, out int passed, out int failed, out int skipped, out int manualRequired)
        {
            total = 0;
            passed = 0;
            failed = 0;
            skipped = 0;
            manualRequired = 0;

            for (int i = 0; i < Results.Count; i++)
            {
                BuffSystemStorageTestCaseResult result = Results[i];
                if (result.Category != category)
                    continue;

                total++;
                if (result.IsPassed)
                    passed++;
                else if (result.IsFailed)
                    failed++;
                else if (result.IsSkipped)
                    skipped++;
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

        internal readonly struct FailureClassificationEntry
        {
            public readonly string CaseName;
            public readonly string Classification;
            public readonly string Evidence;
            public readonly string RecommendedNextStep;

            public FailureClassificationEntry(string caseName, string classification, string evidence, string recommendedNextStep)
            {
                CaseName = caseName;
                Classification = classification;
                Evidence = evidence;
                RecommendedNextStep = recommendedNextStep;
            }
        }

        internal readonly struct ReproCaseEntry
        {
            public readonly string ReproCase;
            public readonly string Status;
            public readonly string Classification;
            public readonly string KeyEvidence;

            public ReproCaseEntry(string reproCase, string status, string classification, string keyEvidence)
            {
                ReproCase = reproCase;
                Status = status;
                Classification = classification;
                KeyEvidence = keyEvidence;
            }
        }
    }
}
