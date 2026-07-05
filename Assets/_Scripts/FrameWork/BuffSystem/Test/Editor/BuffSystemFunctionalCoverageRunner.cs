using ECSFrameWork;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BuffSystem.EditorTesting
{
    internal sealed class BuffSystemFunctionalCoverageRunner
    {
        private const float FixedTickLength = 0.02f;
        private const int DefaultEffectId = 981001;
        private const int MissingEffectId = 981099;
        private const int BaseConfigId = 982000;

        private static readonly string AddQuery = "Add / Query";
        private static readonly string DurationExpire = "Duration / Expire";
        private static readonly string StackRefreshReplace = "Stack / Refresh / Replace";
        private static readonly string RemoveClear = "Remove / Clear";
        private static readonly string SourceTarget = "Source / Target";
        private static readonly string EffectLifecycle = "Effect / Lifecycle Basic";
        private static readonly string Boundary = "Boundary";

        public BuffSystemFunctionalCoverageReport RunAll()
        {
            BuffSystemFunctionalCoverageReport report = BuffSystemFunctionalCoverageReport.Create();
            RunAddQueryTests(report);
            RunDurationExpireTests(report);
            RunStackRefreshReplaceTests(report);
            RunRemoveClearTests(report);
            RunSourceTargetTests(report);
            RunEffectLifecycleTests(report);
            RunBoundaryTests(report);
            report.WriteMarkdown();
            return report;
        }

        private void RunAddQueryTests(BuffSystemFunctionalCoverageReport report)
        {
            RunCase(report, AddQuery, "Functional_AddBuff_TryGet_ReturnsTrue", "After Add + Tick, TryGetBuff returns true.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 1, 1, 1);
                bool found = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 1, env.SourceA, out BuffViewData view);
                context.Assert(found, "TryGetBuff should find added buff.");
                context.Assert(view.ConfigId == BaseConfigId + 1, "ConfigId should match.");
                context.Assert(view.Target.Equals(env.TargetA), "Target should match.");
                context.Assert(view.Source.Equals(env.SourceA), "Source should match.");
                context.Actual = $"found={found}, configId={view.ConfigId}, stack={view.Stack}";
            });

            RunCase(report, AddQuery, "Functional_AddBuff_GetBuffs_TargetContainsBuff", "GetBuffs(target) contains added config.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 2);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 2, 1, 1);
                IReadOnlyList<BuffViewData> views = env.BuffSystem.GetBuffs(env.TargetA);
                int count = CountConfig(views, BaseConfigId + 2);
                context.Assert(count == 1, "GetBuffs should contain exactly one aggregate view for the config.");
                context.Actual = $"viewCount={views.Count}, configCount={count}";
            });

            RunCase(report, AddQuery, "Functional_AddBuff_QueryWrongTarget_ReturnsFalse", "Wrong target cannot see buff.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 3);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 3, 1, 1);
                bool found = env.BuffSystem.TryGetBuff(env.TargetB, BaseConfigId + 3, env.SourceA, out _);
                context.Assert(!found, "Wrong target should not see the buff.");
                context.Actual = $"wrongTargetFound={found}";
            });

            RunCase(report, AddQuery, "Functional_AddBuff_QueryWrongSource_ReturnsFalse", "Wrong source cannot see source-specific buff.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 4);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 4, 1, 1);
                bool found = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 4, env.SourceB, out _);
                context.Assert(!found, "Wrong source should not see the buff.");
                context.Actual = $"wrongSourceFound={found}";
            });

            RunCase(report, AddQuery, "Functional_AddBuff_QueryWrongConfig_ReturnsFalse", "Wrong configId cannot be queried.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 5);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 5, 1, 1);
                bool found = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 105, env.SourceA, out _);
                context.Assert(!found, "Wrong configId should not be found.");
                context.Actual = $"wrongConfigFound={found}";
            });

            RunCase(report, AddQuery, "Functional_AddMultipleTargets_QueryIsolated", "Same buff on two targets is isolated by target.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 6);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 6, 1, 1);
                AddAndTick(env, env.TargetB, env.SourceA, BaseConfigId + 6, 1, 2);
                bool foundA = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 6, env.SourceA, out BuffViewData viewA);
                bool foundB = env.BuffSystem.TryGetBuff(env.TargetB, BaseConfigId + 6, env.SourceA, out BuffViewData viewB);
                context.Assert(foundA && foundB, "Both targets should own their own buff.");
                context.Assert(viewA.Target.Equals(env.TargetA), "TargetA view should point to TargetA.");
                context.Assert(viewB.Target.Equals(env.TargetB), "TargetB view should point to TargetB.");
                context.Actual = $"foundA={foundA}, foundB={foundB}";
            });

            RunCase(report, AddQuery, "Functional_AddMultipleSources_SourceSpecificQueryIsolated", "Same target/config across sources is queryable per source.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 7);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 7, 1, 1);
                AddAndTick(env, env.TargetA, env.SourceB, BaseConfigId + 7, 1, 2);
                bool foundA = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 7, env.SourceA, out BuffViewData viewA);
                bool foundB = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 7, env.SourceB, out BuffViewData viewB);
                context.Assert(foundA && foundB, "Both sources should be independently queryable.");
                context.Assert(!viewA.Source.Equals(viewB.Source), "Source handles should differ.");
                context.Actual = $"foundA={foundA}, foundB={foundB}, sourceA={viewA.Source.ID}, sourceB={viewB.Source.ID}";
            });
        }

        private void RunDurationExpireTests(BuffSystemFunctionalCoverageReport report)
        {
            RunCase(report, DurationExpire, "Functional_Duration_LimitedBuff_ExpiresAfterExpectedTicks", "Limited duration buff expires within bounded ticks.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 20, durationFrames: 3);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 20, 1, 1);
                bool removed = TickUntilMissing(env, env.TargetA, env.SourceA, BaseConfigId + 20, 2, 10);
                context.Assert(removed, "Buff should expire within the bounded tick window.");
                context.Actual = $"removed={removed}, removeCount={env.Effect.RemoveCount}";
            });

            RunCase(report, DurationExpire, "Functional_Duration_PermanentBuff_DoesNotExpire", "Forever buff stays visible and reports permanent remaining frames.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 21, durationFrames: 0, isForever: true);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 21, 1, 1);
                TickRange(env, 2, 20);
                bool found = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 21, env.SourceA, out BuffViewData view);
                context.Assert(found, "Forever buff should stay visible.");
                context.Assert(view.RemainingFrames == -1, "Forever buff should use RemainingFrames=-1.");
                context.Actual = $"found={found}, remaining={view.RemainingFrames}";
            });

            RunCase(report, DurationExpire, "Functional_Duration_Expire_RemovesFromPublicQuery", "Expired buff disappears from TryGetBuff and GetBuffs.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 22, durationFrames: 2);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 22, 1, 1);
                TickUntilMissing(env, env.TargetA, env.SourceA, BaseConfigId + 22, 2, 8);
                bool found = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 22, env.SourceA, out _);
                int count = CountConfig(env.BuffSystem.GetBuffs(env.TargetA), BaseConfigId + 22);
                context.Assert(!found, "TryGetBuff should not find expired buff.");
                context.Assert(count == 0, "GetBuffs should not contain expired buff.");
                context.Actual = $"found={found}, count={count}";
            });

            RunCase(report, DurationExpire, "Functional_Duration_Expire_CallsOnRemoveOnce", "Expire path calls OnRemove once.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 23, durationFrames: 2);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 23, 1, 1);
                TickUntilMissing(env, env.TargetA, env.SourceA, BaseConfigId + 23, 2, 8);
                context.Assert(env.Effect.RemoveCount == 1, "Expire should call OnRemove exactly once for one layer.");
                context.Actual = $"removeCount={env.Effect.RemoveCount}";
            });

            RunCase(report, DurationExpire, "Functional_Tick_BeforeExpire_StillVisible", "Limited buff is still visible before expire boundary.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 24, durationFrames: 5);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 24, 1, 1);
                Tick(env, 2);
                bool found = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 24, env.SourceA, out BuffViewData view);
                context.Assert(found, "Buff should still be visible before expiration.");
                context.Assert(view.RemainingFrames > 0, "RemainingFrames should stay positive before expiration.");
                context.Actual = $"found={found}, remaining={view.RemainingFrames}";
            });

            RunCase(report, DurationExpire, "Functional_Tick_AfterExpire_NotVisible", "Short duration buff becomes invisible after expire ticks.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 25, durationFrames: 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 25, 1, 1);
                bool removed = TickUntilMissing(env, env.TargetA, env.SourceA, BaseConfigId + 25, 2, 6);
                context.Assert(removed, "Duration=1 buff should expire quickly.");
                context.Actual = $"removed={removed}";
            });
        }

        private void RunStackRefreshReplaceTests(BuffSystemFunctionalCoverageReport report)
        {
            RunCase(report, StackRefreshReplace, "Functional_Stack_Append_IncreasesStackUntilMax", "Append increases aggregate Stack.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 40, maxStack: 3);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 40, 1, 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 40, 1, 2);
                bool found = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 40, env.SourceA, out BuffViewData view);
                context.Assert(found, "Stacked buff should be visible.");
                context.Assert(view.Stack == 2, "Stack should be 2 after two layers.");
                context.Actual = $"found={found}, stack={view.Stack}";
            });

            RunCase(report, StackRefreshReplace, "Functional_Stack_Append_DoesNotExceedMaxStack", "Append respects MaxStack.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 41, maxStack: 2);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 41, 1, 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 41, 1, 2);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 41, 1, 3);
                env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 41, env.SourceA, out BuffViewData view);
                context.Assert(view.Stack <= 2, "Stack should not exceed MaxStack.");
                context.Assert(view.Stack == 2, "Stack should stay at MaxStack=2.");
                context.Actual = $"stack={view.Stack}";
            });

            RunCase(report, StackRefreshReplace, "Functional_Stack_RefreshAll_NotFull_AppendsIncomingAndRefreshesExisting", "RefreshAll refreshes existing layers and appends incoming while under MaxStack.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 42, maxStack: 3, durationFrames: 8, stackUpPolicy: ParallelBuffStackUpPolicy.RefreshAll);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 42, 2, 1);
                Tick(env, 2);
                bool foundBefore = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 42, env.SourceA, out BuffViewData before);
                context.Assert(foundBefore, "RefreshAll not-full setup should be queryable before re-add.");
                context.Assert(before.Stack == 2, "RefreshAll not-full setup Stack should be 2.");
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 42, 1, 3);
                bool foundAfter = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 42, env.SourceA, out BuffViewData after);
                context.Assert(foundAfter, "RefreshAll not-full result should remain queryable.");
                context.Assert(after.Stack == Math.Min(before.Stack + 1, 3), "RefreshAll should append incoming while under MaxStack.");
                context.Assert(after.RemainingFrames >= before.RemainingFrames, "RefreshAll should restore or extend remaining frames.");
                context.Assert(env.Effect.RefreshCount >= 1, "RefreshAll should trigger OnRefresh.");
                context.Assert(env.Effect.StackChangedCount >= 1, "RefreshAll not-full append should trigger OnStackChanged.");
                context.Actual = $"foundBefore={foundBefore}, foundAfter={foundAfter}, beforeStack={before.Stack}, afterStack={after.Stack}, beforeRemaining={before.RemainingFrames}, afterRemaining={after.RemainingFrames}, refreshCount={env.Effect.RefreshCount}, stackChangedCount={env.Effect.StackChangedCount}";
            });

            RunCase(report, StackRefreshReplace, "Functional_Stack_RefreshAll_WhenFull_RefreshesWithoutAppending", "RefreshAll refreshes all existing layers without appending when MaxStack is full.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 46, maxStack: 3, durationFrames: 8, stackUpPolicy: ParallelBuffStackUpPolicy.RefreshAll);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 46, 3, 1);
                Tick(env, 2);
                bool foundBefore = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 46, env.SourceA, out BuffViewData before);
                context.Assert(foundBefore, "RefreshAll full setup should be queryable before re-add.");
                context.Assert(before.Stack == 3, "RefreshAll full setup Stack should equal MaxStack.");
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 46, 1, 3);
                bool foundAfter = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 46, env.SourceA, out BuffViewData after);
                context.Assert(foundAfter, "RefreshAll full result should remain queryable.");
                context.Assert(after.Stack == before.Stack, "RefreshAll should not append when MaxStack is already full.");
                context.Assert(after.RemainingFrames >= before.RemainingFrames, "RefreshAll full case should restore or extend remaining frames.");
                context.Assert(env.Effect.RefreshCount >= 1, "RefreshAll full case should trigger OnRefresh.");
                context.Actual = $"foundBefore={foundBefore}, foundAfter={foundAfter}, beforeStack={before.Stack}, afterStack={after.Stack}, beforeRemaining={before.RemainingFrames}, afterRemaining={after.RemainingFrames}, refreshCount={env.Effect.RefreshCount}";
            });

            RunCase(report, StackRefreshReplace, "Functional_Stack_Replace_ReplacesOldInstance", "ReplaceEarliestWhenFull keeps Stack at MaxStack and refreshes visibility.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 43, maxStack: 1, durationFrames: 8, stackUpPolicy: ParallelBuffStackUpPolicy.ReplaceEarliestWhenFull);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 43, 1, 1);
                Tick(env, 2);
                env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 43, env.SourceA, out BuffViewData before);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 43, 1, 3);
                env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 43, env.SourceA, out BuffViewData after);
                context.Assert(after.Stack == 1, "Stack should remain MaxStack=1.");
                context.Assert(after.RemainingFrames >= before.RemainingFrames, "Replace should keep a fresh visible instance.");
                context.Actual = $"beforeRemaining={before.RemainingFrames}, afterRemaining={after.RemainingFrames}, stack={after.Stack}";
            });

            RunCase(report, StackRefreshReplace, "Functional_Stack_OnStackChanged_CalledWhenStackChanges", "Stack growth triggers OnStackChanged.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 44, maxStack: 3);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 44, 1, 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 44, 1, 2);
                context.Assert(env.Effect.StackChangedCount >= 1, "Adding another layer should trigger stack changed callback.");
                context.Actual = $"stackChangedCount={env.Effect.StackChangedCount}";
            });

            RunCase(report, StackRefreshReplace, "Functional_Stack_ReAddSameBuff_DoesNotDuplicateBeyondPolicy", "MaxStack=1 prevents duplicated public stack.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 45, maxStack: 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 45, 1, 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 45, 1, 2);
                env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 45, env.SourceA, out BuffViewData view);
                context.Assert(view.Stack == 1, "Stack should stay at 1.");
                context.Actual = $"stack={view.Stack}";
            });
        }

        private void RunRemoveClearTests(BuffSystemFunctionalCoverageReport report)
        {
            RunCase(report, RemoveClear, "Functional_Remove_ExistingBuff_TryGetReturnsFalse", "Remove existing buff hides it from TryGetBuff.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 60);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 60, 1, 1);
                RemoveAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 60, 1, false, true, 2);
                bool found = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 60, env.SourceA, out _);
                context.Assert(!found, "Removed buff should be hidden.");
                context.Actual = $"found={found}";
            });

            RunCase(report, RemoveClear, "Functional_Remove_NonExistingBuff_DoesNotThrow", "Removing missing buff does not throw and leaves queries empty.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 61);
                RemoveAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 61, 1, false, true, 1);
                bool found = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 61, env.SourceA, out _);
                context.Assert(!found, "Missing buff removal should keep query empty.");
                context.Actual = $"found={found}";
            });

            RunCase(report, RemoveClear, "Functional_Remove_BySource_DoesNotRemoveOtherSource", "Source-specific remove keeps other source.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 62);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 62, 1, 1);
                AddAndTick(env, env.TargetA, env.SourceB, BaseConfigId + 62, 1, 2);
                RemoveAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 62, 1, false, true, 3);
                bool foundA = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 62, env.SourceA, out _);
                bool foundB = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 62, env.SourceB, out _);
                context.Assert(!foundA, "Removed source should be gone.");
                context.Assert(foundB, "Other source should remain.");
                context.Actual = $"foundA={foundA}, foundB={foundB}";
            });

            RunCase(report, RemoveClear, "Functional_Remove_ClearAll_RemovesAllForTarget", "ClearAll removes all layers for the source.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 63, maxStack: 3);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 63, 3, 1);
                RemoveAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 63, 1, false, true, 2);
                bool found = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 63, env.SourceA, out _);
                int count = CountConfig(env.BuffSystem.GetBuffs(env.TargetA), BaseConfigId + 63);
                context.Assert(!found, "ClearAll should hide TryGetBuff.");
                context.Assert(count == 0, "ClearAll should remove config from GetBuffs.");
                context.Actual = $"found={found}, count={count}";
            });

            RunCase(report, RemoveClear, "Functional_Remove_CallsOnRemoveOnce", "Manual remove calls OnRemove once for one layer.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 64);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 64, 1, 1);
                RemoveAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 64, 1, false, true, 2);
                context.Assert(env.Effect.RemoveCount == 1, "Manual remove should call OnRemove once.");
                context.Actual = $"removeCount={env.Effect.RemoveCount}";
            });

            RunCase(report, RemoveClear, "Functional_Remove_AfterExpire_DoesNotDoubleRemove", "Removing after expiry does not double-call OnRemove.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 65, durationFrames: 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 65, 1, 1);
                TickUntilMissing(env, env.TargetA, env.SourceA, BaseConfigId + 65, 2, 6);
                int removeAfterExpire = env.Effect.RemoveCount;
                RemoveAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 65, 1, false, true, 10);
                context.Assert(env.Effect.RemoveCount == removeAfterExpire, "Post-expire remove should not call OnRemove again.");
                context.Actual = $"removeAfterExpire={removeAfterExpire}, removeFinal={env.Effect.RemoveCount}";
            });
        }

        private void RunSourceTargetTests(BuffSystemFunctionalCoverageReport report)
        {
            RunCase(report, SourceTarget, "Functional_SourceTarget_SameConfigDifferentTargets_Isolated", "Same config on different targets stays isolated.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 80);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 80, 1, 1);
                AddAndTick(env, env.TargetB, env.SourceA, BaseConfigId + 80, 1, 2);
                RemoveAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 80, 1, false, true, 3);
                bool foundA = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 80, env.SourceA, out _);
                bool foundB = env.BuffSystem.TryGetBuff(env.TargetB, BaseConfigId + 80, env.SourceA, out _);
                context.Assert(!foundA && foundB, "Removing TargetA should not remove TargetB.");
                context.Actual = $"foundA={foundA}, foundB={foundB}";
            });

            RunCase(report, SourceTarget, "Functional_SourceTarget_SameTargetDifferentSources_Isolated", "Same target/config across sources stays isolated.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 81);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 81, 1, 1);
                AddAndTick(env, env.TargetA, env.SourceB, BaseConfigId + 81, 1, 2);
                bool foundA = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 81, env.SourceA, out _);
                bool foundB = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 81, env.SourceB, out _);
                context.Assert(foundA && foundB, "Both source-specific buffs should be visible.");
                context.Actual = $"foundA={foundA}, foundB={foundB}";
            });

            RunCase(report, SourceTarget, "Functional_SourceTarget_RemoveOneSource_OtherSourceStillActive", "Removing one source keeps another source active.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 82);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 82, 1, 1);
                AddAndTick(env, env.TargetA, env.SourceB, BaseConfigId + 82, 1, 2);
                RemoveAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 82, 1, false, true, 3);
                bool foundA = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 82, env.SourceA, out _);
                bool foundB = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 82, env.SourceB, out _);
                context.Assert(!foundA, "SourceA should be removed.");
                context.Assert(foundB, "SourceB should remain.");
                context.Actual = $"foundA={foundA}, foundB={foundB}";
            });

            RunCase(report, SourceTarget, "Functional_SourceTarget_GetBuffsTarget_ReturnsOnlyTarget", "GetBuffs target view excludes other target.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 83);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 83, 1, 1);
                AddAndTick(env, env.TargetB, env.SourceA, BaseConfigId + 83, 1, 2);
                IReadOnlyList<BuffViewData> viewsA = env.BuffSystem.GetBuffs(env.TargetA);
                for (int i = 0; i < viewsA.Count; i++)
                    context.Assert(viewsA[i].Target.Equals(env.TargetA), "Every TargetA view should point to TargetA.");
                context.Assert(CountConfig(viewsA, BaseConfigId + 83) == 1, "TargetA should have one aggregate view.");
                context.Actual = $"targetAViews={viewsA.Count}";
            });
        }

        private void RunEffectLifecycleTests(BuffSystemFunctionalCoverageReport report)
        {
            RunCase(report, EffectLifecycle, "Functional_Effect_OnApply_CalledOnceOnAdd", "OnApply is called once for one added layer.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 100);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 100, 1, 1);
                context.Assert(env.Effect.ApplyCount == 1, "OnApply should be called once.");
                context.Actual = $"applyCount={env.Effect.ApplyCount}";
            });

            RunCase(report, EffectLifecycle, "Functional_Effect_OnTick_CalledExpectedTimes", "OnTick is called at least once for tick buff.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 101, durationFrames: 12, tickIntervalFrames: 2);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 101, 1, 1);
                TickRange(env, 2, 6);
                context.Assert(env.Effect.TickCount >= 1, "OnTick should be called for tick interval buff.");
                context.Actual = $"tickCount={env.Effect.TickCount}";
            });

            RunCase(report, EffectLifecycle, "Functional_Effect_OnRemove_CalledOnceOnManualRemove", "Manual remove calls OnRemove once.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 102);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 102, 1, 1);
                RemoveAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 102, 1, false, true, 2);
                context.Assert(env.Effect.RemoveCount == 1, "OnRemove should be called once.");
                context.Actual = $"removeCount={env.Effect.RemoveCount}";
            });

            RunCase(report, EffectLifecycle, "Functional_Effect_OnRemove_CalledOnceOnExpire", "Expire remove calls OnRemove once.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 103, durationFrames: 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 103, 1, 1);
                TickUntilMissing(env, env.TargetA, env.SourceA, BaseConfigId + 103, 2, 6);
                context.Assert(env.Effect.RemoveCount == 1, "OnRemove should be called once on expire.");
                context.Actual = $"removeCount={env.Effect.RemoveCount}";
            });

            RunCase(report, EffectLifecycle, "Functional_Effect_OnRefresh_CalledOnRefreshPolicy", "Refresh policy calls OnRefresh.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 104, maxStack: 3, stackUpPolicy: ParallelBuffStackUpPolicy.RefreshEarliest);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 104, 1, 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 104, 1, 2);
                context.Assert(env.Effect.RefreshCount >= 1, "RefreshEarliest should call OnRefresh.");
                context.Actual = $"refreshCount={env.Effect.RefreshCount}";
            });

            RunCase(report, EffectLifecycle, "Functional_Effect_Context_TargetSourceDefinition_Correct", "Effect context exposes target, source, and definition.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 105);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 105, 1, 1);
                context.Assert(env.Effect.LastTarget.Equals(env.TargetA), "Effect context target should match.");
                context.Assert(env.Effect.LastSource.Equals(env.SourceA), "Effect context source should match.");
                context.Assert(env.Effect.LastConfigId == BaseConfigId + 105, "Effect context configId should match.");
                context.Actual = $"lastTarget={env.Effect.LastTarget.ID}, lastSource={env.Effect.LastSource.ID}, lastConfigId={env.Effect.LastConfigId}";
            });
        }

        private void RunBoundaryTests(BuffSystemFunctionalCoverageReport report)
        {
            RunCase(report, Boundary, "Functional_Boundary_MaxStackOne_ReAddDoesNotDuplicate", "MaxStack=1 keeps Stack at one.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 120, maxStack: 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 120, 1, 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 120, 1, 2);
                env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 120, env.SourceA, out BuffViewData view);
                context.Assert(view.Stack == 1, "Stack should be one.");
                context.Actual = $"stack={view.Stack}";
            });

            RunCase(report, Boundary, "Functional_Boundary_DurationOne_ExpiresOnNextTick", "Duration=1 expires quickly without stale view.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 121, durationFrames: 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 121, 1, 1);
                bool removed = TickUntilMissing(env, env.TargetA, env.SourceA, BaseConfigId + 121, 2, 6);
                context.Assert(removed, "Duration=1 should expire within bounded ticks.");
                context.Actual = $"removed={removed}";
            });

            RunCase(report, Boundary, "Functional_Boundary_TickIntervalGreaterThanDuration_TickBehaviorDocumented", "TickInterval greater than duration does not keep buff alive forever.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 122, durationFrames: 3, tickIntervalFrames: 10);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 122, 1, 1);
                bool removed = TickUntilMissing(env, env.TargetA, env.SourceA, BaseConfigId + 122, 2, 10);
                context.Assert(removed, "Buff should still expire even if tick interval is greater than duration.");
                context.Actual = $"removed={removed}, tickCount={env.Effect.TickCount}";
            });

            RunCase(report, Boundary, "Functional_Boundary_InvalidConfigId_RejectedOrIgnored", "Missing definition add is ignored or rejected without visible buff.", context =>
            {
                TestEnvironment env = CreateEnvironment();
                env.BuffSystem.AddBuff(new AddBuffCommand(env.TargetA, BaseConfigId + 123, env.SourceA, 1));
                Tick(env, 1);
                bool found = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 123, env.SourceA, out _);
                context.Assert(!found, "Unknown config should not create public ViewData.");
                context.Actual = $"found={found}";
            });

            RunCase(report, Boundary, "Functional_Boundary_MissingEffectId_DoesNotCrash", "Missing effect registration does not prevent public buff view from existing.", context =>
            {
                TestEnvironment env = CreateEnvironment(registerDefaultEffect: false);
                RegisterDefinition(env, BaseConfigId + 124, effectId: MissingEffectId);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 124, 1, 1);
                bool found = env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 124, env.SourceA, out BuffViewData view);
                context.Assert(found, "Missing effect should not prevent runtime ViewData.");
                context.Assert(view.ConfigId == BaseConfigId + 124, "ConfigId should match.");
                context.Actual = $"found={found}, configId={view.ConfigId}";
            });
        }

        private static void RunCase(BuffSystemFunctionalCoverageReport report, string category, string caseName, string expected, Action<CaseContext> body)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            CaseContext context = new CaseContext(expected);
            try
            {
                body(context);
                stopwatch.Stop();
                report.Add(BuffSystemFunctionalCoverageCaseResult.Passed(category, caseName, expected, context.Actual, context.InvariantChecks, stopwatch.Elapsed.TotalMilliseconds));
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                report.Add(BuffSystemFunctionalCoverageCaseResult.Failed(category, caseName, expected, context.Actual, context.InvariantChecks, stopwatch.Elapsed.TotalMilliseconds, exception));
            }
        }

        private static TestEnvironment CreateEnvironment(bool registerDefaultEffect = true)
        {
            World world = new World();
            Entity targetA = world.CreateEntity();
            Entity targetB = world.CreateEntity();
            Entity sourceA = world.CreateEntity();
            Entity sourceB = world.CreateEntity();
            BuffDefinitionRegistry definitions = new BuffDefinitionRegistry();
            BuffEffectRegistry effects = new BuffEffectRegistry();
            CountingBuffEffectExecutor effect = new CountingBuffEffectExecutor();
            if (registerDefaultEffect)
                effects.Register(DefaultEffectId, effect);

            BuffSystemCore buffSystem = new BuffSystemCore(definitions, effects);
            return new TestEnvironment(world, buffSystem, definitions, effects, effect, targetA, targetB, sourceA, sourceB);
        }

        private static void RegisterDefinition(
            TestEnvironment env,
            int configId,
            int maxStack = 3,
            int durationFrames = 20,
            int tickIntervalFrames = 1,
            ParallelBuffStackUpPolicy stackUpPolicy = ParallelBuffStackUpPolicy.Append,
            ParallelBuffStackDownPolicy stackDownPolicy = ParallelBuffStackDownPolicy.RemoveEarliest,
            bool isForever = false,
            int effectId = DefaultEffectId)
        {
            env.Definitions.Register(new BuffDefinition(
                configId,
                "FunctionalCoverage_" + configId,
                0,
                Math.Max(1, maxStack),
                false,
                isForever,
                isForever ? 0 : Math.Max(1, durationFrames),
                Math.Max(1, tickIntervalFrames),
                0,
                BuffTriggerType.Tick,
                BuffInstanceType.parallel,
                NormalBuffStackPolicy.AddStackOnly,
                stackUpPolicy,
                stackDownPolicy,
                effectId,
                null,
                ParallelBuffStorageMode.EntityPerStack));
        }

        private static void AddAndTick(TestEnvironment env, Entity target, Entity source, int configId, int stack, int frame)
        {
            env.BuffSystem.AddBuff(new AddBuffCommand(target, configId, source, stack));
            Tick(env, frame);
        }

        private static void RemoveAndTick(TestEnvironment env, Entity target, Entity source, int configId, int stackCount, bool matchAnySource, bool clearAll, int frame)
        {
            env.BuffSystem.RemoveBuff(new RemoveBuffCommand(target, configId, source, stackCount, matchAnySource, clearAll));
            Tick(env, frame);
        }

        private static void TickRange(TestEnvironment env, int startFrame, int count)
        {
            for (int i = 0; i < count; i++)
                Tick(env, startFrame + i);
        }

        private static bool TickUntilMissing(TestEnvironment env, Entity target, Entity source, int configId, int startFrame, int maxTicks)
        {
            for (int i = 0; i < maxTicks; i++)
            {
                Tick(env, startFrame + i);
                if (!env.BuffSystem.TryGetBuff(target, configId, source, out _))
                    return true;
            }

            return !env.BuffSystem.TryGetBuff(target, configId, source, out _);
        }

        private static void Tick(TestEnvironment env, int frameNumber)
        {
            env.BuffSystem.Tick(env.World, new SimulationContext(frameNumber, FixedTickLength, false));
        }

        private static int CountConfig(IReadOnlyList<BuffViewData> views, int configId)
        {
            int count = 0;
            for (int i = 0; i < views.Count; i++)
            {
                if (views[i].ConfigId == configId)
                    count++;
            }

            return count;
        }

        private sealed class TestEnvironment
        {
            public readonly World World;
            public readonly BuffSystemCore BuffSystem;
            public readonly BuffDefinitionRegistry Definitions;
            public readonly BuffEffectRegistry Effects;
            public readonly CountingBuffEffectExecutor Effect;
            public readonly Entity TargetA;
            public readonly Entity TargetB;
            public readonly Entity SourceA;
            public readonly Entity SourceB;

            public TestEnvironment(
                World world,
                BuffSystemCore buffSystem,
                BuffDefinitionRegistry definitions,
                BuffEffectRegistry effects,
                CountingBuffEffectExecutor effect,
                Entity targetA,
                Entity targetB,
                Entity sourceA,
                Entity sourceB)
            {
                World = world;
                BuffSystem = buffSystem;
                Definitions = definitions;
                Effects = effects;
                Effect = effect;
                TargetA = targetA;
                TargetB = targetB;
                SourceA = sourceA;
                SourceB = sourceB;
            }
        }

        private sealed class CountingBuffEffectExecutor : BuffEffectExecutorBase
        {
            public int ApplyCount;
            public int RefreshCount;
            public int StackChangedCount;
            public int TickCount;
            public int RemoveCount;
            public Entity LastTarget;
            public Entity LastSource;
            public int LastConfigId;

            public override void OnApply(in BuffEffectContext context)
            {
                ApplyCount++;
                Record(context);
            }

            public override void OnRefresh(in BuffEffectContext context)
            {
                RefreshCount++;
                Record(context);
            }

            public override void OnStackChanged(in BuffEffectContext context, int delta)
            {
                StackChangedCount++;
                Record(context);
            }

            public override void OnTick(in BuffEffectContext context)
            {
                TickCount++;
                Record(context);
            }

            public override void OnRemove(in BuffEffectContext context)
            {
                RemoveCount++;
                Record(context);
            }

            private void Record(in BuffEffectContext context)
            {
                LastTarget = context.Runtime.target;
                LastSource = context.Runtime.source;
                LastConfigId = context.Runtime.configId;
            }
        }

        private sealed class CaseContext
        {
            public readonly string Expected;
            public string Actual;
            public int InvariantChecks;

            public CaseContext(string expected)
            {
                Expected = expected;
                Actual = string.Empty;
            }

            public void Assert(bool condition, string message)
            {
                InvariantChecks++;
                if (!condition)
                    throw new InvalidOperationException(message);
            }
        }
    }
}
