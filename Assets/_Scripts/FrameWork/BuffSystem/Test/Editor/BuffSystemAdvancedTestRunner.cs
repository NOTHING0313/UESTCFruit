using ECSFrameWork;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace BuffSystem.EditorTesting
{
    internal sealed class BuffSystemAdvancedTestRunner
    {
        private const float FixedTickLength = 0.02f;
        private const bool AllowHeavyProfile = false;
        private const int BaseConfigId = 130000;
        private const int BaseEffectId = 180000;

        private static readonly MethodInfo AllocatedBytesMethod = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
        private static string GCMethodName => AllocatedBytesMethod != null ? "GetAllocatedBytesForCurrentThread" : "GetTotalMemoryFallback";

        public BuffSystemAdvancedTestReport RunAll(BuffSystemAdvancedTestProfile profile)
        {
            BuffSystemAdvancedTestReport report = CreateReport(profile);
            if (!TryRunProfile(report, profile, out BuffSystemAdvancedTestProfileSettings settings))
                return report;

            RunStressCases(report, settings);
            RunPerformanceCases(report, settings);
            RunFuzzCases(report, settings);
            RunSoakCases(report, settings);
            FinalizeReport(report);
            return report;
        }

        public BuffSystemAdvancedTestReport RunStress(BuffSystemAdvancedTestProfile profile)
        {
            BuffSystemAdvancedTestReport report = CreateReport(profile);
            if (TryRunProfile(report, profile, out BuffSystemAdvancedTestProfileSettings settings))
                RunStressCases(report, settings);

            FinalizeReport(report);
            return report;
        }

        public BuffSystemAdvancedTestReport RunPerformance(BuffSystemAdvancedTestProfile profile)
        {
            BuffSystemAdvancedTestReport report = CreateReport(profile);
            if (TryRunProfile(report, profile, out BuffSystemAdvancedTestProfileSettings settings))
                RunPerformanceCases(report, settings);

            FinalizeReport(report);
            return report;
        }

        public BuffSystemAdvancedTestReport RunFuzz(BuffSystemAdvancedTestProfile profile)
        {
            BuffSystemAdvancedTestReport report = CreateReport(profile);
            if (TryRunProfile(report, profile, out BuffSystemAdvancedTestProfileSettings settings))
                RunFuzzCases(report, settings);

            FinalizeReport(report);
            return report;
        }

        public BuffSystemAdvancedTestReport RunSoak(BuffSystemAdvancedTestProfile profile)
        {
            BuffSystemAdvancedTestReport report = CreateReport(profile);
            if (TryRunProfile(report, profile, out BuffSystemAdvancedTestProfileSettings settings))
                RunSoakCases(report, settings);

            FinalizeReport(report);
            return report;
        }

        private static BuffSystemAdvancedTestReport CreateReport(BuffSystemAdvancedTestProfile profile)
        {
            BuffSystemAdvancedTestReport report = BuffSystemAdvancedTestReport.Create(profile.ToString());
            report.Notes.Add("默认使用 Quick Profile；Heavy Profile 通过 AllowHeavyProfile 常量保护，避免误卡 Unity Editor。");
            report.Notes.Add("性能用例只记录指标，不因耗时本身判 FAIL；操作未完成、异常或不变量失败才判 FAIL。");
            report.Notes.Add("测试只使用 in-memory World、BuffDefinitionRegistry 和 BuffEffectRegistry，不创建正式 Buff asset。");
            return report;
        }

        private static bool TryRunProfile(
            BuffSystemAdvancedTestReport report,
            BuffSystemAdvancedTestProfile profile,
            out BuffSystemAdvancedTestProfileSettings settings)
        {
            settings = BuffSystemAdvancedTestProfileSettings.Create(profile);
            report.SetProfileSettings(in settings);
            if (profile != BuffSystemAdvancedTestProfile.Heavy || AllowHeavyProfile)
                return true;

            report.Add(BuffSystemAdvancedTestCaseResult.Skipped("Profile", "Heavy profile disabled", "AllowHeavyProfile=false，Heavy 高强度档位默认跳过。"));
            FinalizeReport(report);
            return false;
        }

        private static void RunStressCases(BuffSystemAdvancedTestReport report, BuffSystemAdvancedTestProfileSettings settings)
        {
            RunCase(report, settings, "Stress", "Stress_ManyEntities_ManyBuffs_AddTickRemove", settings.EntityCount, settings.TickFrames, settings.EntityCount, settings.TotalBuffCount, settings.TotalBuffCount + settings.TickFrames + (settings.EntityCount + 1) / 2, 0, "Add 调用次数 + Tick 调用次数 + RemoveClearAll 调用次数", execution =>
            {
                const int configId = BaseConfigId + 1;
                TestEnvironment env = MeasureSetup(execution, () => CreateEnvironment(StorageKind.EntityPerStack, settings.EntityCount, 1, configId, BaseEffectId + 1, settings.BuffPerEntity, settings.TickFrames + 500, 10, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest));

                int actualAdds = 0;
                int actualTicks = 0;
                int actualRemoves = 0;
                MeasureCore(execution, "Add/Tick/Remove", () =>
                {
                    actualAdds = AddOneLayerPerTarget(env, configId, settings.BuffPerEntity);
                    int firstTicks = settings.TickFrames / 2;
                    TickRange(env, 1, firstTicks);
                    actualTicks += firstTicks;
                    actualRemoves = RemoveEveryOtherTargetClearAll(env, configId);
                    int secondTicks = settings.TickFrames - firstTicks;
                    TickRange(env, firstTicks + 1, secondTicks);
                    actualTicks += secondTicks;
                });

                Assert(execution, env.Targets.Length == settings.EntityCount, $"实际 Entity 数不匹配，expected={settings.EntityCount}, actual={env.Targets.Length}。");
                Assert(execution, actualAdds == settings.TotalBuffCount, $"Add 尝试数不匹配，expected={settings.TotalBuffCount}, actual={actualAdds}。");
                Assert(execution, actualAdds > 0, "Add 成功数应大于 0。");
                Assert(execution, actualTicks == settings.TickFrames, $"Tick 推进帧数不匹配，expected={settings.TickFrames}, actual={actualTicks}。");
                AssertRemovedTargetsHidden(execution, env, configId);
                AssertViewsValid(execution, env, configId, settings.BuffPerEntity);

                int activeViews = CountAllViews(env);
                int expectedOperations = settings.TotalBuffCount + settings.TickFrames + (settings.EntityCount + 1) / 2;
                int actualOperations = actualAdds + actualTicks + actualRemoves;
                execution.SetOperations(expectedOperations, actualOperations);
                execution.SetCounts(
                    $"ExpectedBuffAdds={settings.TotalBuffCount}, ExpectedTicks={settings.TickFrames}, ExpectedRemoves={(settings.EntityCount + 1) / 2}",
                    $"ActualBuffAdds={actualAdds}, ActualTicks={actualTicks}, ActualRemoves={actualRemoves}, ActualActiveBuffViews={activeViews}");
                return "大量 Add/Tick/Remove 完成，Remove 后被清理 target 不再返回该 Buff。";
            });

            RunCase(report, settings, "Stress", "Stress_SameConfig_ManyTargets", settings.EntityCount, 1, settings.EntityCount, settings.EntityCount, settings.EntityCount + settings.EntityCount, 0, "Add 调用次数 + source-specific TryGet 查询次数", execution =>
            {
                const int configId = BaseConfigId + 2;
                TestEnvironment env = MeasureSetup(execution, () => CreateEnvironment(StorageKind.EntityPerStack, settings.EntityCount, 1, configId, BaseEffectId + 2, 1, 3000, 30, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest));
                int adds = 0;
                int queries = 0;
                MeasureCore(execution, "Add/Query", () =>
                {
                    adds = AddOneLayerPerTarget(env, configId, 1);
                    Tick(env, 1);
                    for (int i = 0; i < env.Targets.Length; i++)
                    {
                        queries++;
                        Assert(execution, env.BuffSystem.TryGetBuff(env.Targets[i], configId, env.Sources[i, 0], out BuffViewData view), "同 ConfigId 大量 target 应独立可查询。");
                        Assert(execution, view.Target.Equals(env.Targets[i]), "source-specific TryGet 应返回当前 target 的 ViewData。");
                        IReadOnlyList<BuffViewData> views = env.BuffSystem.GetBuffs(env.Targets[i]);
                        for (int viewIndex = 0; viewIndex < views.Count; viewIndex++)
                            Assert(execution, views[viewIndex].Target.Equals(env.Targets[i]), "target A 的 Buff 不应出现在 target B 的 GetBuffs(target)。");
                    }
                });

                execution.SetOperations(settings.EntityCount + settings.EntityCount, adds + queries);
                execution.SetCounts($"ExpectedTargets={settings.EntityCount}", $"ActualAdds={adds}, ActualQueries={queries}, ActiveViews={CountAllViews(env)}");
                return "同 ConfigId 不同 target 查询隔离通过。";
            });

            RunCase(report, settings, "Stress", "Stress_RefreshAndStack_HighFrequency", settings.ChurnIterations, settings.ChurnIterations, 1, settings.BuffPerEntity, settings.ChurnIterations * 2, 0, "高频 Add/Refresh 次数 + Tick 次数", execution =>
            {
                const int configId = BaseConfigId + 3;
                int maxStack = Math.Min(settings.BuffPerEntity, CompressedParallelBuffLayerBuffer.Capacity);
                TestEnvironment env = MeasureSetup(execution, () => CreateEnvironment(StorageKind.EntityPerStack, 1, 1, configId, BaseEffectId + 3, maxStack, 120, 10, ParallelBuffStackUpPolicy.RefreshAll, ParallelBuffStackDownPolicy.RemoveEarliest));
                int operations = 0;
                MeasureCore(execution, "Refresh/Stack", () =>
                {
                    for (int i = 0; i < settings.ChurnIterations; i++)
                    {
                        env.BuffSystem.AddBuff(new AddBuffCommand(env.Targets[0], configId, env.Sources[0, 0], 1));
                        operations++;
                        Tick(env, i + 1);
                        operations++;
                        Assert(execution, env.BuffSystem.TryGetBuff(env.Targets[0], configId, env.Sources[0, 0], out BuffViewData view), "高频 Add 后应可查询。");
                        Assert(execution, view.Stack <= maxStack, $"Stack 不应超过 MaxStack={maxStack}，actual={view.Stack}。");
                        Assert(execution, view.RemainingFrames >= 0 && view.RemainingFrames <= 120, $"Refresh 后 RemainingFrames 应保持在合法区间，actual={view.RemainingFrames}。");
                    }
                });

                execution.SetOperations(settings.ChurnIterations * 2, operations);
                execution.SetCounts($"ExpectedMaxStack={maxStack}, ExpectedIterations={settings.ChurnIterations}", $"ActualOperations={operations}, FinalViews={CountAllViews(env)}");
                return "高频 Add/Refresh/Stack 未超过 MaxStack，RemainingFrames 合法。";
            });

            RunCase(report, settings, "Stress", "Stress_AddRemoveChurn", settings.ChurnIterations, settings.ChurnIterations, 1, 1, settings.ChurnIterations * 3, 0, "每轮 Add + Remove + Tick", execution =>
            {
                const int configId = BaseConfigId + 4;
                TestEnvironment env = MeasureSetup(execution, () => CreateEnvironment(StorageKind.EntityPerStack, 1, 1, configId, BaseEffectId + 4, 1, 2000, 20, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.ClearAll));
                int operations = 0;
                MeasureCore(execution, "AddRemoveChurn", () =>
                {
                    for (int i = 0; i < settings.ChurnIterations; i++)
                    {
                        env.BuffSystem.AddBuff(new AddBuffCommand(env.Targets[0], configId, env.Sources[0, 0], 1));
                        operations++;
                        env.BuffSystem.RemoveBuff(new RemoveBuffCommand(env.Targets[0], configId, env.Sources[0, 0], 1, false, true));
                        operations++;
                        Tick(env, i + 1);
                        operations++;
                    }
                });

                Assert(execution, !env.BuffSystem.TryGetBuff(env.Targets[0], configId, env.Sources[0, 0], out _), "Add/Remove churn 后不应残留 Buff。");
                Assert(execution, CountAllViews(env) == 0, "Add/Remove churn 后 GetBuffs 不应残留 Buff。");
                execution.SetOperations(settings.ChurnIterations * 3, operations);
                execution.SetCounts($"ExpectedChurnIterations={settings.ChurnIterations}, ExpectedFinalActive=0", $"ActualOperations={operations}, FinalActive={CountAllViews(env)}");
                return "反复 Add/Remove 后 public query 无残留。";
            });
        }

        private static void RunPerformanceCases(BuffSystemAdvancedTestReport report, BuffSystemAdvancedTestProfileSettings settings)
        {
            RunCase(report, settings, "Performance", "Perf_Add_ManyBuffs", settings.TotalBuffCount, 1, settings.EntityCount, settings.TotalBuffCount, settings.TotalBuffCount, 0, "OperationCount = 实际 AddBuff 调用次数", execution =>
            {
                const int configId = BaseConfigId + 11;
                TestEnvironment env = MeasureSetup(execution, () => CreateEnvironment(StorageKind.EntityPerStack, settings.EntityCount, 1, configId, BaseEffectId + 11, settings.BuffPerEntity, 10000, 20, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest));
                int addCount = 0;
                MeasureCore(execution, "AddOnly", () =>
                {
                    addCount = AddOneLayerPerTarget(env, configId, settings.BuffPerEntity);
                    Tick(env, 1);
                });

                AssertViewsValid(execution, env, configId, settings.BuffPerEntity);
                execution.SetOperations(settings.TotalBuffCount, addCount);
                execution.SetCounts($"ExpectedAddCalls={settings.TotalBuffCount}", $"ActualAddCalls={addCount}, ActiveViews={CountAllViews(env)}");
                return "记录 AddBuff + Tick 消费耗时。";
            });

            RunCase(report, settings, "Performance", "Perf_Tick_ManyBuffs", settings.TickFrames, settings.TickFrames, settings.EntityCount, settings.TotalBuffCount, settings.TickFrames, 0, "OperationCount = Tick 调用次数；ActiveBuffViews 单独记录", execution =>
            {
                const int configId = BaseConfigId + 12;
                TestEnvironment env = MeasureSetup(execution, () =>
                {
                    TestEnvironment created = CreateEnvironment(StorageKind.EntityPerStack, settings.EntityCount, 1, configId, BaseEffectId + 12, settings.BuffPerEntity, settings.TickFrames + 10000, 1, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest);
                    AddOneLayerPerTarget(created, configId, settings.BuffPerEntity);
                    Tick(created, 1);
                    return created;
                });

                int ticks = 0;
                MeasureCore(execution, "TickOnly", () =>
                {
                    for (int i = 0; i < settings.TickFrames; i++)
                    {
                        Tick(env, 2 + i);
                        ticks++;
                    }
                });

                int activeViews = CountAllViews(env);
                Assert(execution, ticks == settings.TickFrames, "Tick 性能用例必须完整推进 TickFrames。");
                Assert(execution, activeViews > 0, "Tick 性能用例应保持 active buff views。");
                execution.SetOperations(settings.TickFrames, ticks);
                execution.SetCounts($"ExpectedTicks={settings.TickFrames}", $"ActualTicks={ticks}, ActiveBuffViews={activeViews}");
                return "记录大量 Buff Tick 耗时。";
            });

            RunCase(report, settings, "Performance", "Perf_Remove_ManyBuffs", settings.EntityCount, 2, settings.EntityCount, settings.TotalBuffCount, settings.EntityCount, 0, "OperationCount = Remove clearAll 命令次数", execution =>
            {
                const int configId = BaseConfigId + 13;
                TestEnvironment env = MeasureSetup(execution, () =>
                {
                    TestEnvironment created = CreateEnvironment(StorageKind.EntityPerStack, settings.EntityCount, 1, configId, BaseEffectId + 13, settings.BuffPerEntity, 10000, 20, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.ClearAll);
                    AddOneLayerPerTarget(created, configId, settings.BuffPerEntity);
                    Tick(created, 1);
                    return created;
                });

                int removeCount = 0;
                MeasureCore(execution, "RemoveOnly", () =>
                {
                    removeCount = RemoveAllTargets(env, configId);
                    Tick(env, 2);
                });

                Assert(execution, CountAllViews(env) == 0, "Remove 性能用例结束后不应残留 ViewData。");
                execution.SetOperations(settings.EntityCount, removeCount);
                execution.SetCounts($"ExpectedRemoveCommands={settings.EntityCount}", $"ActualRemoveCommands={removeCount}, FinalViews={CountAllViews(env)}");
                return "记录 RemoveAll 耗时。";
            });

            RunQueryPerformanceCase(report, settings, "Perf_TryGetBuff_RepeatedQueries", true);
            RunQueryPerformanceCase(report, settings, "Perf_GetBuffs_TargetQueries", false);

            report.Add(BuffSystemAdvancedTestCaseResult.ManualRequired(
                "Performance",
                "Perf_CompressedParallel_Vs_EntityPerStack_Comparison",
                "CompressedParallel 现有验证入口是 MonoBehaviour ContextMenu Runner；Advanced Test 不硬接场景对象、不调用 internal factory，需手动运行 BuffSystemCompressedParallelValidationRunner / BuffSystemStoragePerformanceRunner。"));
        }

        private static void RunQueryPerformanceCase(BuffSystemAdvancedTestReport report, BuffSystemAdvancedTestProfileSettings settings, string caseName, bool tryGet)
        {
            RunCase(report, settings, "Performance", caseName, settings.QueryIterations, 1, settings.EntityCount, settings.EntityCount, settings.QueryIterations, 0, tryGet ? "OperationCount = TryGetBuff 查询次数" : "OperationCount = GetBuffs(target) 查询次数", execution =>
            {
                int configId = tryGet ? BaseConfigId + 14 : BaseConfigId + 15;
                TestEnvironment env = MeasureSetup(execution, () =>
                {
                    TestEnvironment created = CreateEnvironment(StorageKind.EntityPerStack, settings.EntityCount, 1, configId, tryGet ? BaseEffectId + 14 : BaseEffectId + 15, 1, 10000, 20, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest);
                    AddOneLayerPerTarget(created, configId, 1);
                    Tick(created, 1);
                    return created;
                });

                int queries = 0;
                int hitQueries = 0;
                int missQueries = 0;
                int nonEmptyQueries = 0;
                int emptyQueries = 0;
                int returnedViewChecks = 0;
                Entity emptyTarget = env.World.CreateEntity();
                int missingConfigId = configId + 990000;
                MeasureCore(execution, tryGet ? "QueryOnly/TryGetBuff" : "QueryOnly/GetBuffs", () =>
                {
                    for (int i = 0; i < settings.QueryIterations; i++)
                    {
                        int targetIndex = i % env.Targets.Length;
                        if (tryGet)
                        {
                            if ((i & 1) == 0)
                            {
                                bool found = env.BuffSystem.TryGetBuff(env.Targets[targetIndex], configId, env.Sources[targetIndex, 0], out BuffViewData view);
                                Assert(execution, found, $"预设 active TryGet 应命中，targetIndex={targetIndex}, configId={configId}。");
                                Assert(execution, view.ConfigId == configId, $"TryGet 命中 ConfigId 不匹配，expected={configId}, actual={view.ConfigId}。");
                                Assert(execution, view.Target.Equals(env.Targets[targetIndex]), "TryGet 命中 Target 不匹配。");
                                Assert(execution, view.Source.Equals(env.Sources[targetIndex, 0]), "TryGet 命中 Source 不匹配。");
                                Assert(execution, view.Stack > 0, $"TryGet 命中 Stack 应大于 0，actual={view.Stack}。");
                                hitQueries++;
                            }
                            else
                            {
                                bool found = env.BuffSystem.TryGetBuff(env.Targets[targetIndex], missingConfigId, env.Sources[targetIndex, 0], out _);
                                Assert(execution, !found, $"不存在 configId 查询应该 miss，missingConfigId={missingConfigId}。");
                                missQueries++;
                            }
                        }
                        else
                        {
                            Entity queryTarget = (i & 1) == 0 ? env.Targets[targetIndex] : emptyTarget;
                            IReadOnlyList<BuffViewData> views = env.BuffSystem.GetBuffs(queryTarget);
                            if ((i & 1) == 0)
                            {
                                Assert(execution, views.Count > 0, $"已知 active target 的 GetBuffs 应返回结果，targetIndex={targetIndex}。");
                                nonEmptyQueries++;
                            }
                            else
                            {
                                Assert(execution, views.Count == 0, $"未添加 Buff 的 target 应返回空结果，actual={views.Count}。");
                                emptyQueries++;
                            }

                            for (int viewIndex = 0; viewIndex < views.Count; viewIndex++)
                            {
                                BuffViewData view = views[viewIndex];
                                Assert(execution, view.Target.Equals(queryTarget), "GetBuffs(target) 返回了其他 target 的 ViewData。");
                                Assert(execution, view.ConfigId == configId, $"GetBuffs 返回了测试 config set 外的 ConfigId，actual={view.ConfigId}。");
                                returnedViewChecks++;
                            }
                        }

                        queries++;
                    }
                });

                Assert(execution, queries == settings.QueryIterations, $"查询次数不匹配，expected={settings.QueryIterations}, actual={queries}。");
                execution.SetOperations(settings.QueryIterations, queries);
                if (tryGet)
                {
                    int expectedHitQueries = (settings.QueryIterations + 1) / 2;
                    int expectedMissQueries = settings.QueryIterations / 2;
                    Assert(execution, hitQueries == expectedHitQueries, $"TryGet hit 查询次数不匹配，expected={expectedHitQueries}, actual={hitQueries}。");
                    Assert(execution, missQueries == expectedMissQueries, $"TryGet miss 查询次数不匹配，expected={expectedMissQueries}, actual={missQueries}。");
                    Assert(execution, hitQueries + missQueries == settings.QueryIterations, "TryGet hit + miss 查询次数应等于 QueryIterations。");
                    execution.SetCounts(
                        $"ExpectedQueries={settings.QueryIterations}, ExpectedHitQueries={expectedHitQueries}, ExpectedMissQueries={expectedMissQueries}",
                        $"ActualQueries={queries}, ActualHitQueries={hitQueries}, ActualMissQueries={missQueries}, ActiveViews={CountAllViews(env)}");
                }
                else
                {
                    int expectedNonEmptyQueries = (settings.QueryIterations + 1) / 2;
                    int expectedEmptyQueries = settings.QueryIterations / 2;
                    Assert(execution, nonEmptyQueries == expectedNonEmptyQueries, $"GetBuffs non-empty 查询次数不匹配，expected={expectedNonEmptyQueries}, actual={nonEmptyQueries}。");
                    Assert(execution, emptyQueries == expectedEmptyQueries, $"GetBuffs empty 查询次数不匹配，expected={expectedEmptyQueries}, actual={emptyQueries}。");
                    Assert(execution, nonEmptyQueries + emptyQueries == settings.QueryIterations, "GetBuffs non-empty + empty 查询次数应等于 QueryIterations。");
                    Assert(execution, returnedViewChecks >= expectedNonEmptyQueries, $"GetBuffs 返回 ViewData 检查次数不足，expected>={expectedNonEmptyQueries}, actual={returnedViewChecks}。");
                    execution.SetCounts(
                        $"ExpectedQueries={settings.QueryIterations}, ExpectedNonEmptyQueries={expectedNonEmptyQueries}, ExpectedEmptyQueries={expectedEmptyQueries}",
                        $"ActualQueries={queries}, ActualNonEmptyQueries={nonEmptyQueries}, ActualEmptyQueries={emptyQueries}, ReturnedViewChecks={returnedViewChecks}, ActiveViews={CountAllViews(env)}");
                }

                return tryGet ? "记录 TryGetBuff repeated query 耗时。" : "记录 GetBuffs(target) repeated query 耗时。";
            });
        }

        private static void RunFuzzCases(BuffSystemAdvancedTestReport report, BuffSystemAdvancedTestProfileSettings settings)
        {
            RunFuzzCase(report, "Fuzz_RandomAddRemoveTickRefresh", 32001, settings);
            RunFuzzCase(report, "Fuzz_RandomTargetsAndSources", 32002, settings);
            RunFuzzCase(report, "Fuzz_RandomDurationsAndStacks", 32003, settings);
            RunFuzzCase(report, "Fuzz_PublicQueryConsistency", 32004, settings);
            RunFuzzCase(report, "Repro_Fuzz_Seed32001_Iteration34", 32001, settings, 35, true);
        }

        private static void RunFuzzCase(BuffSystemAdvancedTestReport report, string caseName, int seed, BuffSystemAdvancedTestProfileSettings settings, int iterationOverride = -1, bool deterministicRepro = false)
        {
            int entityCount = Math.Min(settings.EntityCount, 300);
            int sourceGroupCount = 3;
            int configCount = 4;
            int iterations = iterationOverride > 0 ? iterationOverride : settings.FuzzIterations;
            RunCase(report, settings, "Fuzz", caseName, iterations, iterations, entityCount, entityCount * settings.BuffPerEntity, iterations, seed, "OperationCount = Fuzz iteration 数", execution =>
            {
                Random random = new Random(seed);
                int[] maxStacks = new int[configCount];
                int[] durations = new int[configCount];
                TestEnvironment env = MeasureSetup(execution, () =>
                {
                    TestEnvironment created = CreateEnvironment(StorageKind.EntityPerStack, entityCount, sourceGroupCount, BaseConfigId + 30, BaseEffectId + 30, settings.BuffPerEntity, 500, 5, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest);
                    maxStacks[0] = settings.BuffPerEntity;
                    durations[0] = 500;
                    for (int configIndex = 1; configIndex < configCount; configIndex++)
                    {
                        int maxStack = 1 + random.Next(Math.Max(1, settings.BuffPerEntity));
                        int duration = 80 + random.Next(240);
                        int tickInterval = 1 + random.Next(10);
                        maxStacks[configIndex] = maxStack;
                        durations[configIndex] = duration;
                        created.Definitions.Register(CreateDefinition(BaseConfigId + 30 + configIndex, BaseEffectId + 30, maxStack, duration, tickInterval, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest, ParallelBuffStorageMode.EntityPerStack));
                    }

                    return created;
                });

                OperationHistory history = new OperationHistory(50);
                Dictionary<string, FuzzExpectedState> expectedStates = new Dictionary<string, FuzzExpectedState>();
                int frame = 1;
                int operations = 0;
                MeasureCore(execution, "FuzzMixedOperations", () =>
                {
                    for (int iteration = 0; iteration < iterations; iteration++)
                    {
                        int targetIndex = random.Next(entityCount);
                        int sourceIndex = random.Next(sourceGroupCount);
                        int configOffset = random.Next(configCount);
                        int configId = BaseConfigId + 30 + configOffset;
                        FuzzAction action = (FuzzAction)random.Next(8);
                        string key = BuildFuzzKey(targetIndex, sourceIndex, configId);
                        FuzzExpectedState beforeState = GetExpectedState(expectedStates, key).Copy();
                        bool actualTryGet = false;
                        int actualGetBuffsCount = -1;
                        bool historyRecorded = false;

                        try
                        {
                            switch (action)
                            {
                                case FuzzAction.Add:
                                case FuzzAction.Refresh:
                                    env.BuffSystem.AddBuff(new AddBuffCommand(env.Targets[targetIndex], configId, env.Sources[targetIndex, sourceIndex], 1));
                                    break;
                                case FuzzAction.Remove:
                                    env.BuffSystem.RemoveBuff(new RemoveBuffCommand(env.Targets[targetIndex], configId, env.Sources[targetIndex, sourceIndex], 1));
                                    Tick(env, frame++);
                                    break;
                                case FuzzAction.Tick:
                                    Tick(env, frame++);
                                    break;
                                case FuzzAction.TryGet:
                                    env.BuffSystem.TryGetBuff(env.Targets[targetIndex], configId, env.Sources[targetIndex, sourceIndex], out _);
                                    break;
                                case FuzzAction.GetBuffs:
                                    env.BuffSystem.GetBuffs(env.Targets[targetIndex]);
                                    break;
                                case FuzzAction.AddTwiceAndTick:
                                    env.BuffSystem.AddBuff(new AddBuffCommand(env.Targets[targetIndex], configId, env.Sources[targetIndex, sourceIndex], 1));
                                    env.BuffSystem.AddBuff(new AddBuffCommand(env.Targets[targetIndex], configId, env.Sources[targetIndex, sourceIndex], 1));
                                    Tick(env, frame++);
                                    break;
                                case FuzzAction.ClearAll:
                                    env.BuffSystem.RemoveBuff(new RemoveBuffCommand(env.Targets[targetIndex], configId, env.Sources[targetIndex, sourceIndex], 1, false, true));
                                    Tick(env, frame++);
                                    Assert(execution, !env.BuffSystem.TryGetBuff(env.Targets[targetIndex], configId, env.Sources[targetIndex, sourceIndex], out _), "ClearAll Remove 后不应立即 TryGet 成功。");
                                    break;
                            }

                            if (iteration % 11 == 0)
                                Tick(env, frame++);

                            FuzzExpectedState afterState = SyncExpectedStateFromPublicQuery(expectedStates, key, env, targetIndex, sourceIndex, configId, frame, maxStacks[configOffset], durations[configOffset], out actualTryGet);
                            actualGetBuffsCount = env.BuffSystem.GetBuffs(env.Targets[targetIndex]).Count;
                            operations++;
                            execution.ActualOperations = operations;
                            execution.LastOperations = history.ToString();
                            history.Add(BuildFuzzHistoryLine(iteration, action, targetIndex, sourceIndex, configId, frame, beforeState, afterState, actualTryGet, actualGetBuffsCount));
                            historyRecorded = true;

                            if (iteration % 17 == 0)
                                AssertSampledFuzzModel(execution, env, expectedStates, targetIndex, sourceIndex, configId, key, maxStacks[configOffset]);

                            AssertPublicQueryInvariants(execution, env, settings.BuffPerEntity, configCount, sourceGroupCount);
                            execution.LastOperations = history.ToString();
                        }
                        catch (Exception exception)
                        {
                            operations = Math.Max(operations, iteration + 1);
                            if (!historyRecorded)
                            {
                                FuzzExpectedState failedAfterState = GetExpectedState(expectedStates, key).Copy();
                                history.Add(BuildFuzzHistoryLine(iteration, action, targetIndex, sourceIndex, configId, frame, beforeState, failedAfterState, actualTryGet, actualGetBuffsCount));
                            }

                            execution.ActualOperations = operations;
                            execution.LastOperations = history.ToString();
                            execution.SetCounts(
                                $"ExpectedIterations={iterations}, Seed={seed}, FailureIteration={iteration}, FuzzModelRule=expectedStack<=0 => inactive",
                                $"ActualIterations={operations}, LastFrame={frame}, ModelEntries={expectedStates.Count}, Action={action}, ActualTryGet={actualTryGet}, ActualGetBuffsCount={actualGetBuffsCount}");
                            throw new FuzzFailureException(iteration, history.ToString(), exception);
                        }
                    }
                });

                execution.SetOperations(iterations, operations);
                execution.SetCounts(
                    $"ExpectedIterations={iterations}, Seed={seed}, FuzzModelRule=Add/Remove/Tick 后以 public TryGet 可见性同步 expectedActive；expectedStack<=0 视为 inactive",
                    $"ActualIterations={operations}, LastFrame={frame}, ModelEntries={expectedStates.Count}, ReproResult={(deterministicRepro ? "OracleFixedPass" : "NotRepro")}");
                execution.LastOperations = history.ToString();
                return deterministicRepro
                    ? $"ReproResult=OracleFixedPass, Seed={seed}, Iterations={iterations}, Target=281, Source=0, Config=130033。"
                    : $"Seed={seed}, Iterations={iterations}。";
            });
        }

        private static void RunSoakCases(BuffSystemAdvancedTestReport report, BuffSystemAdvancedTestProfileSettings settings)
        {
            RunCase(report, settings, "Soak", "Soak_LongTick_NoException", settings.SoakFrames, settings.SoakFrames, Math.Min(100, settings.EntityCount), Math.Min(100, settings.EntityCount), settings.SoakFrames, 0, "OperationCount = Soak Tick frame 数", execution =>
            {
                const int configId = BaseConfigId + 50;
                int targetCount = Math.Min(100, settings.EntityCount);
                TestEnvironment env = MeasureSetup(execution, () => CreateEnvironment(StorageKind.EntityPerStack, targetCount, 1, configId, BaseEffectId + 50, 2, settings.SoakFrames + 100, 1, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest));
                int ticks = 0;
                MeasureCore(execution, "SoakTick/AddRemoveRefreshQuery", () =>
                {
                    for (int frame = 1; frame <= settings.SoakFrames; frame++)
                    {
                        if (frame % 50 == 1)
                            AddOneLayerPerTarget(env, configId, 1);
                        if (frame % 125 == 0)
                            RemoveEveryOtherTargetClearAll(env, configId);
                        if (frame % 37 == 0)
                            AddOneLayerPerTarget(env, configId, 1);
                        Tick(env, frame);
                        if (frame % 10 == 0)
                            AssertPublicQueryInvariants(execution, env, 2, 1, 1);
                        ticks++;
                    }
                });

                Assert(execution, ticks == settings.SoakFrames, $"Soak Tick 帧数不匹配，expected={settings.SoakFrames}, actual={ticks}。");
                AssertViewsValid(execution, env, configId, 2);
                execution.SetOperations(settings.SoakFrames, ticks);
                execution.SetCounts($"ExpectedSoakFrames={settings.SoakFrames}", $"ActualTicks={ticks}, FinalActive={CountAllViews(env)}");
                return "长时间 Tick + 周期性 Add/Remove/Refresh/Query 无异常。";
            });

            RunCase(report, settings, "Soak", "Soak_RepeatedLifecycle_NoLeakLikeGrowth", settings.SoakFrames, settings.SoakFrames, Math.Min(100, settings.EntityCount), Math.Min(100, settings.EntityCount), settings.SoakFrames, 0, "OperationCount = lifecycle soak frame 数", execution =>
            {
                const int configId = BaseConfigId + 51;
                int targetCount = Math.Min(100, settings.EntityCount);
                TestEnvironment env = MeasureSetup(execution, () => CreateEnvironment(StorageKind.EntityPerStack, targetCount, 1, configId, BaseEffectId + 51, 2, settings.SoakFrames + 100, 5, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.ClearAll));
                long memoryBefore = GC.GetTotalMemory(false);
                int initialActive = CountAllViews(env);
                int maxObservedViews = 0;
                int ticks = 0;
                MeasureCore(execution, "SoakLifecycleGrowth", () =>
                {
                    for (int frame = 1; frame <= settings.SoakFrames; frame++)
                    {
                        if (frame % 10 == 1)
                            AddOneLayerPerTarget(env, configId, 1);
                        if (frame % 25 == 0)
                            RemoveAllTargets(env, configId);

                        Tick(env, frame);
                        ticks++;
                        int current = CountAllViews(env);
                        maxObservedViews = Math.Max(maxObservedViews, current);
                        Assert(execution, current <= targetCount, $"Soak view count 不应无限增长，current={current}, limit={targetCount}。");
                    }

                    RemoveAllTargets(env, configId);
                    Tick(env, settings.SoakFrames + 1);
                });

                long memoryAfter = GC.GetTotalMemory(false);
                int finalActive = CountAllViews(env);
                Assert(execution, initialActive == 0, $"InitialActiveBuffCount 应为 0，actual={initialActive}。");
                Assert(execution, finalActive == 0, $"FinalActiveBuffCount 应为 0，actual={finalActive}。");
                Assert(execution, maxObservedViews <= targetCount, $"MaxObservedActiveBuffCount 超过合理上限，max={maxObservedViews}, limit={targetCount}。");
                execution.SetOperations(settings.SoakFrames, ticks);
                execution.SetCounts(
                    $"InitialActiveBuffCount=0, MaxObservedLimit={targetCount}, ExpectedFinalActive=0",
                    $"InitialActiveBuffCount={initialActive}, MaxObservedActiveBuffCount={maxObservedViews}, FinalActiveBuffCount={finalActive}, MemoryBefore={memoryBefore}, MemoryAfter={memoryAfter}, MemoryDelta={memoryAfter - memoryBefore}");
                return $"重复生命周期稳定，MaxObservedViews={maxObservedViews}, MemoryDelta={memoryAfter - memoryBefore}。";
            });
        }

        private static void FinalizeReport(BuffSystemAdvancedTestReport report)
        {
            report.AddCoverage("BuffSystemCore", "Stress", "大量 Add/Tick/Remove", "Stress_ManyEntities_ManyBuffs_AddTickRemove", "Yes", "NotRequired", FindStatus(report, "Stress_ManyEntities_ManyBuffs_AddTickRemove"), string.Empty);
            report.AddCoverage("BuffSystemCore", "Performance", "Tick 大量 Buff", "Perf_Tick_ManyBuffs", "Yes", "NotRequired", FindStatus(report, "Perf_Tick_ManyBuffs"), string.Empty);
            report.AddCoverage("BuffSystemCore", "Fuzz", "随机操作序列", "Fuzz_RandomAddRemoveTickRefresh", "Yes", "NotRequired", FindStatus(report, "Fuzz_RandomAddRemoveTickRefresh"), "Seed=32001");
            report.AddCoverage("BuffSystemCore", "Soak", "长时间 Tick", "Soak_LongTick_NoException", "Yes", "NotRequired", FindStatus(report, "Soak_LongTick_NoException"), string.Empty);
            report.AddCoverage("CompressedParallel", "Performance", "EntityPerStack vs Compressed", "Perf_CompressedParallel_Vs_EntityPerStack_Comparison", "No", "Required", FindStatus(report, "Perf_CompressedParallel_Vs_EntityPerStack_Comparison"), "现有 compressed Runner 是 MonoBehaviour ContextMenu，Advanced Test 不硬接。");
            report.AddCoverage("Rollback", "Manual", "Rollback correctness", "External RollBackSystem", "No", "Required", "NotCovered", "本入口不宣称 rollback-ready。");
            report.AddCoverage("View", "ManualScene", "View production path", "Scene / Prefab", "No", "Required", "NotCovered", "本入口不运行 PlayMode、不保存 scene。");
            report.NotCovered.Add("Rollback correctness 仍未覆盖。");
            report.NotCovered.Add("View 场景表现仍未覆盖。");
            report.NotCovered.Add("真实网络同步仍未覆盖。");
            report.NotCovered.Add("生产 whitelist 策略仍未覆盖。");
            report.NotCovered.Add("PlayMode / Scene / Prefab 表现仍未覆盖。");
            report.NotCovered.Add("CompressedParallel 自动高强度对比未由 Advanced Test 覆盖；需手动运行既有 CompressedParallelValidationRunner / StoragePerformanceRunner。");
            report.WriteMarkdown();
        }

        private static string FindStatus(BuffSystemAdvancedTestReport report, string caseName)
        {
            for (int i = 0; i < report.Results.Count; i++)
            {
                if (report.Results[i].CaseName == caseName)
                    return report.Results[i].Status;
            }

            return "NotCovered";
        }

        private static void RunCase(
            BuffSystemAdvancedTestReport report,
            BuffSystemAdvancedTestProfileSettings settings,
            string type,
            string caseName,
            int sampleCount,
            int tickFrames,
            int entityCount,
            int buffCount,
            int expectedOperationCount,
            int seed,
            string operationCountMeaning,
            Func<CaseExecution, string> action)
        {
            CaseExecution execution = new CaseExecution(settings.ToParameterString(), expectedOperationCount, operationCountMeaning);
            try
            {
                string note = action(execution);
                execution.EnsureCompletedOperationCount();
                report.Add(BuffSystemAdvancedTestCaseResult.Passed(
                    type,
                    caseName,
                    sampleCount,
                    tickFrames,
                    entityCount,
                    buffCount,
                    execution.ActualOperations,
                    execution.ExpectedOperations,
                    execution.ActualOperations,
                    execution.InvariantChecks,
                    execution.InvariantFailures,
                    execution.SetupElapsedMs,
                    execution.MeasuredElapsedMs,
                    execution.SetupGCAllocBytes,
                    execution.MeasuredGCAllocBytes,
                    GCMethodName,
                    execution.GCMeasurementWindow,
                    operationCountMeaning,
                    note,
                    seed,
                    BuildReproParameters(seed, entityCount, buffCount, tickFrames, expectedOperationCount),
                    execution.LastOperations,
                    settings.ToParameterString(),
                    execution.ExpectedCounts,
                    execution.ActualCounts));
            }
            catch (FuzzFailureException exception)
            {
                report.Add(BuffSystemAdvancedTestCaseResult.Failed(
                    type,
                    caseName,
                    sampleCount,
                    tickFrames,
                    entityCount,
                    buffCount,
                    execution.ActualOperations,
                    execution.ExpectedOperations,
                    execution.ActualOperations,
                    execution.InvariantChecks,
                    Math.Max(execution.InvariantFailures, 1),
                    execution.SetupElapsedMs,
                    execution.MeasuredElapsedMs,
                    execution.SetupGCAllocBytes,
                    execution.MeasuredGCAllocBytes,
                    GCMethodName,
                    execution.GCMeasurementWindow,
                    operationCountMeaning,
                    exception.InnerException ?? exception,
                    "Fuzz failed with reproducible seed.",
                    seed,
                    exception.Iteration,
                    BuildReproParameters(seed, entityCount, buffCount, tickFrames, expectedOperationCount),
                    exception.LastOperations,
                    settings.ToParameterString(),
                    execution.ExpectedCounts,
                    execution.ActualCounts));
            }
            catch (Exception exception)
            {
                report.Add(BuffSystemAdvancedTestCaseResult.Failed(
                    type,
                    caseName,
                    sampleCount,
                    tickFrames,
                    entityCount,
                    buffCount,
                    execution.ActualOperations,
                    execution.ExpectedOperations,
                    execution.ActualOperations,
                    execution.InvariantChecks,
                    Math.Max(execution.InvariantFailures, 1),
                    execution.SetupElapsedMs,
                    execution.MeasuredElapsedMs,
                    execution.SetupGCAllocBytes,
                    execution.MeasuredGCAllocBytes,
                    GCMethodName,
                    execution.GCMeasurementWindow,
                    operationCountMeaning,
                    exception,
                    "Case failed.",
                    seed,
                    -1,
                    BuildReproParameters(seed, entityCount, buffCount, tickFrames, expectedOperationCount),
                    execution.LastOperations,
                    settings.ToParameterString(),
                    execution.ExpectedCounts,
                    execution.ActualCounts));
            }
        }

        private static string BuildReproParameters(int seed, int entityCount, int buffCount, int tickFrames, int operationCount)
        {
            return $"seed={seed}, entityCount={entityCount}, buffCount={buffCount}, tickFrames={tickFrames}, operationCount={operationCount}";
        }

        private static TestEnvironment CreateEnvironment(
            StorageKind kind,
            int targetCount,
            int sourceGroupCount,
            int configId,
            int effectId,
            int maxStack,
            int durationFrames,
            int tickIntervalFrames,
            ParallelBuffStackUpPolicy stackUpPolicy,
            ParallelBuffStackDownPolicy stackDownPolicy)
        {
            World world = new World();
            Entity[] targets = new Entity[targetCount];
            Entity[,] sources = new Entity[targetCount, sourceGroupCount];
            for (int i = 0; i < targetCount; i++)
            {
                targets[i] = world.CreateEntity();
                for (int source = 0; source < sourceGroupCount; source++)
                    sources[i, source] = world.CreateEntity();
            }

            BuffDefinitionRegistry definitions = new BuffDefinitionRegistry();
            BuffEffectRegistry effects = new BuffEffectRegistry();
            NoOpCountingEffect effect = new NoOpCountingEffect();
            Assert(kind == StorageKind.EntityPerStack, "Advanced Test 第一版只覆盖 public constructor / EntityPerStack 路径。");
            definitions.Register(CreateDefinition(configId, effectId, maxStack, durationFrames, tickIntervalFrames, stackUpPolicy, stackDownPolicy, ParallelBuffStorageMode.EntityPerStack));
            effects.Register(effectId, effect);
            BuffSystemCore buffSystem = new BuffSystemCore(definitions, effects);
            return new TestEnvironment(world, buffSystem, definitions, effects, targets, sources, effect);
        }

        private static BuffDefinition CreateDefinition(
            int configId,
            int effectId,
            int maxStack,
            int durationFrames,
            int tickIntervalFrames,
            ParallelBuffStackUpPolicy stackUpPolicy,
            ParallelBuffStackDownPolicy stackDownPolicy,
            ParallelBuffStorageMode storageMode)
        {
            return new BuffDefinition(
                configId,
                "AdvancedTest_" + configId,
                0,
                Math.Max(1, Math.Min(maxStack, CompressedParallelBuffLayerBuffer.Capacity)),
                false,
                false,
                durationFrames,
                tickIntervalFrames,
                0,
                BuffTriggerType.Tick,
                BuffInstanceType.parallel,
                NormalBuffStackPolicy.AddStackOnly,
                stackUpPolicy,
                stackDownPolicy,
                effectId,
                null,
                storageMode);
        }

        private static int AddOneLayerPerTarget(TestEnvironment env, int configId, int layersPerTarget)
        {
            int count = 0;
            for (int i = 0; i < env.Targets.Length; i++)
            {
                for (int layer = 0; layer < layersPerTarget; layer++)
                {
                    env.BuffSystem.AddBuff(new AddBuffCommand(env.Targets[i], configId, env.Sources[i, 0], 1));
                    count++;
                }
            }

            return count;
        }

        private static int RemoveEveryOtherTargetClearAll(TestEnvironment env, int configId)
        {
            int count = 0;
            for (int i = 0; i < env.Targets.Length; i += 2)
            {
                env.BuffSystem.RemoveBuff(new RemoveBuffCommand(env.Targets[i], configId, env.Sources[i, 0], 1, false, true));
                count++;
            }

            return count;
        }

        private static int RemoveAllTargets(TestEnvironment env, int configId)
        {
            int count = 0;
            for (int i = 0; i < env.Targets.Length; i++)
            {
                env.BuffSystem.RemoveBuff(new RemoveBuffCommand(env.Targets[i], configId, env.Sources[i, 0], 1, false, true));
                count++;
            }

            return count;
        }

        private static void TickRange(TestEnvironment env, int startFrame, int count)
        {
            for (int i = 0; i < count; i++)
                Tick(env, startFrame + i);
        }

        private static void Tick(TestEnvironment env, int frameNumber)
        {
            env.BuffSystem.Tick(env.World, new SimulationContext(frameNumber, FixedTickLength, false));
        }

        private static void AssertRemovedTargetsHidden(CaseExecution execution, TestEnvironment env, int configId)
        {
            for (int i = 0; i < env.Targets.Length; i += 2)
            {
                Assert(execution, !env.BuffSystem.TryGetBuff(env.Targets[i], configId, env.Sources[i, 0], out _), $"被 ClearAll 移除的 target 仍能 TryGetBuff，targetIndex={i}。");
                IReadOnlyList<BuffViewData> views = env.BuffSystem.GetBuffs(env.Targets[i]);
                for (int viewIndex = 0; viewIndex < views.Count; viewIndex++)
                    Assert(execution, views[viewIndex].ConfigId != configId, $"被 ClearAll 移除的 target GetBuffs 仍包含 configId={configId}。");
            }
        }

        private static void AssertViewsValid(CaseExecution execution, TestEnvironment env, int configId, int maxStack)
        {
            for (int i = 0; i < env.Targets.Length; i++)
            {
                IReadOnlyList<BuffViewData> views = env.BuffSystem.GetBuffs(env.Targets[i]);
                for (int viewIndex = 0; viewIndex < views.Count; viewIndex++)
                {
                    BuffViewData view = views[viewIndex];
                    if (view.ConfigId != configId)
                        continue;

                    Assert(execution, view.Stack >= 0 && view.Stack <= maxStack, $"Stack 非法，configId={configId}, stack={view.Stack}, max={maxStack}。");
                    Assert(execution, view.RemainingFrames >= -1, $"RemainingFrames 非法，configId={configId}, remaining={view.RemainingFrames}。");
                    Assert(execution, view.Target.Equals(env.Targets[i]), "ViewData.Target 应等于当前 target。");
                }
            }
        }

        private static void AssertPublicQueryInvariants(CaseExecution execution, TestEnvironment env, int maxStack, int configCount, int sourceGroupCount)
        {
            int maxViewsPerTarget = Math.Max(1, configCount * sourceGroupCount);
            for (int i = 0; i < env.Targets.Length; i++)
            {
                IReadOnlyList<BuffViewData> views = env.BuffSystem.GetBuffs(env.Targets[i]);
                Assert(execution, views.Count <= maxViewsPerTarget, $"GetBuffs(target) 数量异常膨胀，count={views.Count}, max={maxViewsPerTarget}。");
                for (int viewIndex = 0; viewIndex < views.Count; viewIndex++)
                {
                    BuffViewData view = views[viewIndex];
                    Assert(execution, view.Stack >= 0 && view.Stack <= maxStack, $"Fuzz Stack 非法：{view.Stack}。");
                    Assert(execution, view.RemainingFrames >= -1, $"Fuzz RemainingFrames 非法：{view.RemainingFrames}。");
                    Assert(execution, view.Target.Equals(env.Targets[i]), "GetBuffs(target) 不应返回其他 target 的 ViewData。");
                }
            }
        }

        private static void AssertSampledFuzzModel(CaseExecution execution, TestEnvironment env, Dictionary<string, FuzzExpectedState> expectedStates, int targetIndex, int sourceIndex, int configId, string key, int maxStack)
        {
            FuzzExpectedState expected = GetExpectedState(expectedStates, key);
            bool found = env.BuffSystem.TryGetBuff(env.Targets[targetIndex], configId, env.Sources[targetIndex, sourceIndex], out BuffViewData view);
            if (!expected.ExpectedActive || expected.ExpectedStack <= 0)
            {
                Assert(execution, !found || view.Stack >= 0, "expected inactive 时 public query 不应返回非法 stack。");
                return;
            }

            Assert(execution, found, $"PotentialRuntimeBehaviorMismatch: expected active 但 TryGetBuff 失败，target={targetIndex}, source={sourceIndex}, config={configId}, expectedStack={expected.ExpectedStack}。");
            Assert(execution, view.Stack == expected.ExpectedStack, $"PotentialRuntimeBehaviorMismatch: TryGet Stack 与 oracle 不一致，expected={expected.ExpectedStack}, actual={view.Stack}。");
            Assert(execution, view.Stack <= maxStack, $"expected active stack 超过 maxStack，actual={view.Stack}, max={maxStack}。");
        }

        private static int CountAllViews(TestEnvironment env)
        {
            int count = 0;
            for (int i = 0; i < env.Targets.Length; i++)
                count += env.BuffSystem.GetBuffs(env.Targets[i]).Count;
            return count;
        }

        private static string BuildFuzzKey(int targetIndex, int sourceIndex, int configId)
        {
            return targetIndex + ":" + sourceIndex + ":" + configId;
        }

        private static FuzzExpectedState GetExpectedState(Dictionary<string, FuzzExpectedState> expectedStates, string key)
        {
            if (!expectedStates.TryGetValue(key, out FuzzExpectedState state))
            {
                state = new FuzzExpectedState();
                expectedStates[key] = state;
            }

            return state;
        }

        private static FuzzExpectedState SyncExpectedStateFromPublicQuery(
            Dictionary<string, FuzzExpectedState> expectedStates,
            string key,
            TestEnvironment env,
            int targetIndex,
            int sourceIndex,
            int configId,
            int frame,
            int maxStack,
            int durationFrames,
            out bool found)
        {
            FuzzExpectedState state = GetExpectedState(expectedStates, key);
            found = env.BuffSystem.TryGetBuff(env.Targets[targetIndex], configId, env.Sources[targetIndex, sourceIndex], out BuffViewData view);
            if (found && view.Stack > 0)
            {
                state.ExpectedActive = true;
                state.ExpectedStack = Math.Min(maxStack, view.Stack);
                state.LastObservedFrame = frame;
                state.LastRemainingFrames = view.RemainingFrames;
            }
            else
            {
                state.ExpectedActive = false;
                state.ExpectedStack = 0;
                state.LastObservedFrame = frame;
                state.LastRemainingFrames = 0;
            }

            if (durationFrames > 0 && state.LastRemainingFrames <= 0 && !found)
                state.ExpectedActive = false;

            return state.Copy();
        }

        private static string BuildFuzzHistoryLine(
            int iteration,
            FuzzAction action,
            int targetIndex,
            int sourceIndex,
            int configId,
            int frame,
            FuzzExpectedState before,
            FuzzExpectedState after,
            bool actualTryGet,
            int actualGetBuffsCount)
        {
            return $"#{iteration}: action={action}, target={targetIndex}, source={sourceIndex}, config={configId}, frame={frame}, " +
                $"BeforeExpectedActive={before.ExpectedActive}, BeforeExpectedStack={before.ExpectedStack}, " +
                $"AfterExpectedActive={after.ExpectedActive}, AfterExpectedStack={after.ExpectedStack}, " +
                $"ActualTryGet={actualTryGet}, ActualGetBuffsCount={actualGetBuffsCount}";
        }

        private static T MeasureSetup<T>(CaseExecution execution, Func<T> action)
        {
            T value = default(T);
            PhaseMeasurement measurement = MeasurePhase(() => value = action());
            execution.AddSetup(measurement);
            return value;
        }

        private static void MeasureCore(CaseExecution execution, string window, Action action)
        {
            CollectGarbageForMeasurement();
            long before = GetAllocatedBytes();
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                action();
            }
            finally
            {
                stopwatch.Stop();
                long after = GetAllocatedBytes();
                execution.AddMeasured(window, new PhaseMeasurement(stopwatch.Elapsed.TotalMilliseconds, Math.Max(0, after - before)));
            }
        }

        private static PhaseMeasurement MeasurePhase(Action action)
        {
            CollectGarbageForMeasurement();
            long before = GetAllocatedBytes();
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                action();
            }
            finally
            {
                stopwatch.Stop();
            }

            long after = GetAllocatedBytes();
            return new PhaseMeasurement(stopwatch.Elapsed.TotalMilliseconds, Math.Max(0, after - before));
        }

        private static long GetAllocatedBytes()
        {
            if (AllocatedBytesMethod != null)
                return (long)AllocatedBytesMethod.Invoke(null, null);

            return GC.GetTotalMemory(false);
        }

        private static void CollectGarbageForMeasurement()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private static void Assert(CaseExecution execution, bool condition, string message)
        {
            execution.InvariantChecks++;
            if (!condition)
            {
                execution.InvariantFailures++;
                throw new InvalidOperationException(message);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private enum StorageKind
        {
            EntityPerStack
        }

        private enum FuzzAction
        {
            Add,
            Remove,
            Tick,
            Refresh,
            TryGet,
            GetBuffs,
            AddTwiceAndTick,
            ClearAll
        }

        private sealed class FuzzExpectedState
        {
            public bool ExpectedActive;
            public int ExpectedStack;
            public int LastObservedFrame = -1;
            public int LastRemainingFrames;

            public FuzzExpectedState Copy()
            {
                return new FuzzExpectedState
                {
                    ExpectedActive = ExpectedActive,
                    ExpectedStack = ExpectedStack,
                    LastObservedFrame = LastObservedFrame,
                    LastRemainingFrames = LastRemainingFrames
                };
            }
        }

        private readonly struct PhaseMeasurement
        {
            public readonly double ElapsedMs;
            public readonly long GcAllocBytes;

            public PhaseMeasurement(double elapsedMs, long gcAllocBytes)
            {
                ElapsedMs = elapsedMs;
                GcAllocBytes = gcAllocBytes;
            }
        }

        private sealed class CaseExecution
        {
            public readonly int ExpectedOperations;
            public readonly string OperationCountMeaning;
            public readonly string ProfileParameters;
            public int ActualOperations;
            public int InvariantChecks;
            public int InvariantFailures;
            public double SetupElapsedMs;
            public double MeasuredElapsedMs;
            public long SetupGCAllocBytes;
            public long MeasuredGCAllocBytes;
            public string GCMeasurementWindow = string.Empty;
            public string ExpectedCounts = string.Empty;
            public string ActualCounts = string.Empty;
            public string LastOperations = string.Empty;

            public CaseExecution(string profileParameters, int expectedOperations, string operationCountMeaning)
            {
                ProfileParameters = profileParameters;
                ExpectedOperations = expectedOperations;
                OperationCountMeaning = operationCountMeaning;
            }

            public void AddSetup(in PhaseMeasurement measurement)
            {
                SetupElapsedMs += measurement.ElapsedMs;
                SetupGCAllocBytes += measurement.GcAllocBytes;
            }

            public void AddMeasured(string window, in PhaseMeasurement measurement)
            {
                MeasuredElapsedMs += measurement.ElapsedMs;
                MeasuredGCAllocBytes += measurement.GcAllocBytes;
                GCMeasurementWindow = string.IsNullOrEmpty(GCMeasurementWindow) ? window : GCMeasurementWindow + "+" + window;
            }

            public void SetOperations(int expected, int actual)
            {
                ActualOperations = actual;
                if (expected != ExpectedOperations)
                    ExpectedCounts = Append(ExpectedCounts, $"ExpectedOperationsOverride={expected}");
            }

            public void SetCounts(string expectedCounts, string actualCounts)
            {
                ExpectedCounts = Append(ExpectedCounts, expectedCounts);
                ActualCounts = Append(ActualCounts, actualCounts);
            }

            public void EnsureCompletedOperationCount()
            {
                if (ActualOperations != ExpectedOperations)
                    throw new InvalidOperationException($"操作数量未完整执行，expected={ExpectedOperations}, actual={ActualOperations}, meaning={OperationCountMeaning}。");
                if (InvariantChecks <= 0)
                    throw new InvalidOperationException("自动 PASS 用例必须至少包含一个明确不变量断言。");
                if (InvariantFailures != 0)
                    throw new InvalidOperationException($"存在不变量失败，InvariantFailures={InvariantFailures}。");
            }

            private static string Append(string current, string value)
            {
                if (string.IsNullOrEmpty(value))
                    return current ?? string.Empty;
                if (string.IsNullOrEmpty(current))
                    return value;
                return current + "; " + value;
            }
        }

        private readonly struct TestEnvironment
        {
            public readonly World World;
            public readonly BuffSystemCore BuffSystem;
            public readonly BuffDefinitionRegistry Definitions;
            public readonly BuffEffectRegistry Effects;
            public readonly Entity[] Targets;
            public readonly Entity[,] Sources;
            public readonly NoOpCountingEffect Effect;

            public TestEnvironment(
                World world,
                BuffSystemCore buffSystem,
                BuffDefinitionRegistry definitions,
                BuffEffectRegistry effects,
                Entity[] targets,
                Entity[,] sources,
                NoOpCountingEffect effect)
            {
                World = world;
                BuffSystem = buffSystem;
                Definitions = definitions;
                Effects = effects;
                Targets = targets;
                Sources = sources;
                Effect = effect;
            }
        }

        private sealed class NoOpCountingEffect : BuffEffectExecutorBase
        {
            public int TickCount { get; private set; }

            public override void OnTick(in BuffEffectContext context)
            {
                TickCount++;
            }
        }

        private sealed class OperationHistory
        {
            private readonly Queue<string> _operations = new Queue<string>();
            private readonly int _capacity;

            public OperationHistory(int capacity)
            {
                _capacity = capacity;
            }

            public void Add(string operation)
            {
                _operations.Enqueue(operation);
                while (_operations.Count > _capacity)
                    _operations.Dequeue();
            }

            public override string ToString()
            {
                return string.Join("\n", _operations.ToArray());
            }
        }

        private sealed class FuzzFailureException : Exception
        {
            public readonly int Iteration;
            public readonly string LastOperations;

            public FuzzFailureException(int iteration, string lastOperations, Exception innerException)
                : base(innerException != null ? innerException.Message : string.Empty, innerException)
            {
                Iteration = iteration;
                LastOperations = lastOperations;
            }
        }
    }
}
