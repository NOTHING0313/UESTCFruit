using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BuffSystem.EditorTesting
{
    internal sealed class BuffSystemAdvancedTestReport
    {
        private const string RelativeResultPath = "Assets/_Scripts/FrameWork/BuffSystem/Test/测试结果.md";

        public readonly List<BuffSystemAdvancedTestCaseResult> Results = new List<BuffSystemAdvancedTestCaseResult>();
        public readonly List<BuffSystemAdvancedCoverageItem> Coverage = new List<BuffSystemAdvancedCoverageItem>();
        public readonly List<string> NotCovered = new List<string>();
        public readonly List<string> Notes = new List<string>();

        public string Profile;
        public string StartedAt;
        public string FinishedAt;
        public string UnityVersion;
        public string ProjectPath;
        public string ResultPath;
        public string ProfileParameters;
        public int EntityCount;
        public int BuffPerEntity;
        public int TickFrames;
        public int FuzzIterations;
        public int SoakFrames;
        public int QueryIterations;
        public int ChurnIterations;
        public int Total;
        public int Passed;
        public int Failed;
        public int Skipped;
        public int ManualRequired;
        public string Summary;

        public bool HasFailures => Failed > 0;

        public static BuffSystemAdvancedTestReport Create(string profile)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return new BuffSystemAdvancedTestReport
            {
                Profile = profile,
                StartedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                UnityVersion = Application.unityVersion,
                ProjectPath = projectRoot,
                ResultPath = Path.Combine(projectRoot, RelativeResultPath)
            };
        }

        public void SetProfileSettings(in BuffSystemAdvancedTestProfileSettings settings)
        {
            EntityCount = settings.EntityCount;
            BuffPerEntity = settings.BuffPerEntity;
            TickFrames = settings.TickFrames;
            FuzzIterations = settings.FuzzIterations;
            SoakFrames = settings.SoakFrames;
            QueryIterations = settings.QueryIterations;
            ChurnIterations = settings.ChurnIterations;
            ProfileParameters = settings.ToParameterString();
        }

        public void Add(BuffSystemAdvancedTestCaseResult result)
        {
            if (result == null)
                return;

            Results.Add(result);
            RecalculateSummary();
        }

        public void AddCoverage(string module, string testType, string coveredItem, string caseName, string autoCoverage, string manualCoverage, string status, string note)
        {
            Coverage.Add(new BuffSystemAdvancedCoverageItem(module, testType, coveredItem, caseName, autoCoverage, manualCoverage, status, note));
        }

        public void Finish()
        {
            FinishedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            RecalculateSummary();
            if (Failed > 0)
                Summary = "FAIL";
            else if (Profile == BuffSystemAdvancedTestProfile.Standard.ToString())
                Summary = "Standard Profile PASS / Heavy 未运行";
            else if (Profile == BuffSystemAdvancedTestProfile.Heavy.ToString())
                Summary = "Heavy Profile PASS";
            else
                Summary = "Quick Profile PASS / Standard 未运行 / Heavy 未运行";
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
            builder.AppendLine("# BuffSystem 高强度测试结果");
            builder.AppendLine();
            builder.AppendLine("## 1. 测试概要");
            builder.AppendLine();
            builder.AppendLine($"- 测试时间：{StartedAt} - {FinishedAt}");
            builder.AppendLine($"- Unity 版本：{UnityVersion}");
            builder.AppendLine($"- 项目路径：{ProjectPath}");
            builder.AppendLine($"- 测试 Profile：{Profile}");
            builder.AppendLine($"- 总用例数：{Total}");
            builder.AppendLine($"- 通过：{Passed}");
            builder.AppendLine($"- 失败：{Failed}");
            builder.AppendLine($"- 跳过：{Skipped}");
            builder.AppendLine($"- 需要手动验证：{ManualRequired}");
            builder.AppendLine($"- 最终结论：{Summary}");
            builder.AppendLine();

            builder.AppendLine("## 2. Profile 烈度参数");
            builder.AppendLine();
            builder.AppendLine("| Profile | EntityCount | BuffPerEntity | TotalBuffCount | TickFrames | FuzzIterations | SoakFrames | QueryIterations | ChurnIterations |");
            builder.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|");
            builder.AppendLine($"| {Escape(Profile)} | {EntityCount} | {BuffPerEntity} | {EntityCount * BuffPerEntity} | {TickFrames} | {FuzzIterations} | {SoakFrames} | {QueryIterations} | {ChurnIterations} |");
            builder.AppendLine();

            builder.AppendLine("## 3. 测试真实性说明");
            builder.AppendLine();
            builder.AppendLine("- PASS 是否包含断言：是。每个自动 PASS 用例均记录 InvariantChecks。");
            builder.AppendLine("- 是否包含状态不变量：是。覆盖 Stack、RemainingFrames、target/source 查询、Remove 后可见性、生命周期增长趋势。");
            builder.AppendLine("- 是否包含操作计数：是。ExpectedOperations / ActualOperations / OperationCountMeaning 均写入用例结果。");
            builder.AppendLine("- 是否包含 GC 统计窗口：是。Setup 与 Measured 分开记录。");
            builder.AppendLine("- 是否包含 seed：Fuzz 用例记录固定 seed 和 Last 50 operations。");
            builder.AppendLine("- 是否包含未覆盖边界：是。ManualRequired 与 NotCovered 单独列出。");
            builder.AppendLine("- Query Performance：TryGetBuff 记录 ExpectedHitQueries / ActualHitQueries / ExpectedMissQueries / ActualMissQueries；GetBuffs(target) 记录 ExpectedNonEmptyQueries / ActualNonEmptyQueries / ExpectedEmptyQueries / ActualEmptyQueries / ReturnedViewChecks。");
            builder.AppendLine("- Fuzz action 名称映射：Add、Remove、Tick、Refresh、TryGet、GetBuffs、AddTwiceAndTick、ClearAll。");
            builder.AppendLine("- Fuzz model 更新规则：Add / Remove / Tick / Refresh / Query 后以 public TryGet 可见性同步 expectedActive；expectedStack <= 0 一律视为 inactive；duration 不精确模拟时只做 public query 合法性弱不变量。");
            builder.AppendLine("- Fuzz 失败分类：包含 PotentialRuntimeBehaviorMismatch 字样时仅表示 public behavior 需要人工复核，不代表本入口已证明 runtime bug。");
            builder.AppendLine();

            AppendSection(builder, "## 4. Stress Test 结果", "Stress");
            AppendSection(builder, "## 5. Performance Test 结果", "Performance");
            AppendSection(builder, "## 6. Fuzz Test 结果", "Fuzz");
            AppendSection(builder, "## 7. Soak Test 结果", "Soak");

            builder.AppendLine("## 8. 性能指标汇总");
            builder.AppendLine();
            builder.AppendLine("| 类型 | 用例 | SetupElapsedMs | MeasuredElapsedMs | OperationCount | OperationCount 口径 | AverageMsPerOperation | SetupGCAllocBytes | MeasuredGCAllocBytes | GCMethod | GCWindow | GCZeroObserved | 状态 |");
            builder.AppendLine("|---|---|---:|---:|---:|---|---:|---:|---:|---|---|---|---|");
            for (int i = 0; i < Results.Count; i++)
            {
                BuffSystemAdvancedTestCaseResult result = Results[i];
                double average = result.OperationCount > 0 ? result.MeasuredElapsedMs / result.OperationCount : 0d;
                builder.AppendLine($"| {Escape(result.Type)} | {Escape(result.CaseName)} | {result.SetupElapsedMs:0.###} | {result.MeasuredElapsedMs:0.###} | {result.OperationCount} | {Escape(result.OperationCountMeaning)} | {average:0.######} | {result.SetupGCAllocBytes} | {result.MeasuredGCAllocBytes} | {Escape(result.GCMethod)} | {Escape(result.GCMeasurementWindow)} | {result.GCZeroObserved} | {Escape(result.Status)} |");
            }

            builder.AppendLine();
            builder.AppendLine("## 9. 失败用例详情");
            builder.AppendLine();
            bool hasFailure = false;
            for (int i = 0; i < Results.Count; i++)
            {
                BuffSystemAdvancedTestCaseResult result = Results[i];
                if (!result.IsFailed)
                    continue;

                hasFailure = true;
                builder.AppendLine($"### {Escape(result.Type)} / {Escape(result.CaseName)}");
                builder.AppendLine();
                builder.AppendLine($"- 失败原因：{Escape(result.FailureReason)}");
                builder.AppendLine($"- 异常类型：{Escape(result.ExceptionType)}");
                builder.AppendLine($"- 随机种子：{result.RandomSeed}");
                builder.AppendLine($"- 失败 iteration：{result.FailureIteration}");
                builder.AppendLine($"- 复现参数：{Escape(result.ReproParameters)}");
                builder.AppendLine($"- ExpectedOperations：{result.ExpectedOperations}");
                builder.AppendLine($"- ActualOperations：{result.ActualOperations}");
                builder.AppendLine($"- InvariantChecks：{result.InvariantChecks}");
                builder.AppendLine($"- ExpectedCounts：{Escape(result.ExpectedCounts)}");
                builder.AppendLine($"- ActualCounts：{Escape(result.ActualCounts)}");
                builder.AppendLine("- 最近操作：");
                builder.AppendLine("```text");
                builder.AppendLine(result.LastOperations ?? string.Empty);
                builder.AppendLine("```");
                builder.AppendLine("- 异常堆栈：");
                builder.AppendLine("```text");
                builder.AppendLine(result.ExceptionStack ?? string.Empty);
                builder.AppendLine("```");
                builder.AppendLine();
            }

            if (!hasFailure)
                builder.AppendLine("无失败用例。");

            builder.AppendLine();
            builder.AppendLine("## 10. 覆盖矩阵");
            builder.AppendLine();
            builder.AppendLine("| 模块 | 测试类型 | 覆盖项 | 用例 | 自动覆盖 | 手动覆盖 | 状态 | 备注 |");
            builder.AppendLine("|---|---|---|---|---|---|---|---|");
            for (int i = 0; i < Coverage.Count; i++)
            {
                BuffSystemAdvancedCoverageItem item = Coverage[i];
                builder.AppendLine($"| {Escape(item.Module)} | {Escape(item.TestType)} | {Escape(item.CoveredItem)} | {Escape(item.CaseName)} | {Escape(item.AutoCoverage)} | {Escape(item.ManualCoverage)} | {Escape(item.Status)} | {Escape(item.Note)} |");
            }

            builder.AppendLine();
            builder.AppendLine("## 11. 未覆盖项");
            builder.AppendLine();
            for (int i = 0; i < NotCovered.Count; i++)
                builder.AppendLine($"- {NotCovered[i]}");

            builder.AppendLine();
            builder.AppendLine("## 12. 风险与结论");
            builder.AppendLine();
            for (int i = 0; i < Notes.Count; i++)
                builder.AppendLine($"- {Notes[i]}");
            builder.AppendLine($"- 当前结论：{Summary}");
            builder.AppendLine("- Standard Profile 未默认运行，需人工菜单或后续专门入口触发。");
            builder.AppendLine("- Heavy Profile 仍由 AllowHeavyProfile=false 保护，默认不运行。");
            builder.AppendLine("- CompressedParallel 自动覆盖状态以覆盖矩阵为准；不能自动测的项目标记为 ManualRequired。");
            builder.AppendLine("- 本测试不证明 rollback-ready。");
            builder.AppendLine("- 本测试不证明 View 场景表现正确。");
            builder.AppendLine("- 本测试不证明 production whitelist 安全。");
            builder.AppendLine("- 本测试不等价于完整 PlayMode / 网络同步测试。");

            return builder.ToString();
        }

        private void AppendSection(StringBuilder builder, string title, string type)
        {
            builder.AppendLine(title);
            builder.AppendLine();
            builder.AppendLine("| 类型 | 用例 | 样例数量 | Tick 数 | Entity 数 | Buff 数 | ExpectedOperations | ActualOperations | InvariantChecks | InvariantFailures | 状态 | 备注 | ExpectedCounts | ActualCounts |");
            builder.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---|---|---|");
            for (int i = 0; i < Results.Count; i++)
            {
                BuffSystemAdvancedTestCaseResult result = Results[i];
                if (result.Type != type)
                    continue;

                builder.AppendLine($"| {Escape(result.Type)} | {Escape(result.CaseName)} | {result.SampleCount} | {result.TickFrames} | {result.EntityCount} | {result.BuffCount} | {result.ExpectedOperations} | {result.ActualOperations} | {result.InvariantChecks} | {result.InvariantFailures} | {Escape(result.Status)} | {Escape(result.Note)} | {Escape(result.ExpectedCounts)} | {Escape(result.ActualCounts)} |");
            }

            builder.AppendLine();
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
                if (Results[i].Status == BuffSystemAdvancedTestStatus.Passed)
                    Passed++;
                else if (Results[i].Status == BuffSystemAdvancedTestStatus.Failed)
                    Failed++;
                else if (Results[i].Status == BuffSystemAdvancedTestStatus.Skipped)
                    Skipped++;
                else if (Results[i].Status == BuffSystemAdvancedTestStatus.ManualRequired)
                    ManualRequired++;
            }
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        }
    }

    internal readonly struct BuffSystemAdvancedCoverageItem
    {
        public readonly string Module;
        public readonly string TestType;
        public readonly string CoveredItem;
        public readonly string CaseName;
        public readonly string AutoCoverage;
        public readonly string ManualCoverage;
        public readonly string Status;
        public readonly string Note;

        public BuffSystemAdvancedCoverageItem(string module, string testType, string coveredItem, string caseName, string autoCoverage, string manualCoverage, string status, string note)
        {
            Module = module;
            TestType = testType;
            CoveredItem = coveredItem;
            CaseName = caseName;
            AutoCoverage = autoCoverage;
            ManualCoverage = manualCoverage;
            Status = status;
            Note = note;
        }
    }
}
