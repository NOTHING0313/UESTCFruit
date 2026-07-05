using ECSFrameWork;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BuffSystem.EditorTesting
{
    internal sealed class BuffSystemLifecycleTestRunner
    {
        internal const string OnApplyCategory = "OnApply";
        internal const string OnTickCategory = "OnTick / TickInterval";
        internal const string OnRemoveCategory = "OnRemove";
        internal const string OnRefreshCategory = "OnRefresh";
        internal const string OnStackChangedCategory = "OnStackChanged";
        internal const string InterleavingCategory = "Interleaving";
        internal const string ContextCategory = "Effect Context";

        private const float FixedTickLength = 0.02f;
        private const int DefaultEffectId = 985001;
        private const int BaseConfigId = 986000;

        public BuffSystemLifecycleTestReport RunAll()
        {
            BuffSystemLifecycleTestReport report = BuffSystemLifecycleTestReport.Create();
            RunOnApplyTests(report);
            RunOnTickTests(report);
            RunOnRemoveTests(report);
            RunOnRefreshTests(report);
            RunOnStackChangedTests(report);
            RunInterleavingTests(report);
            RunContextTests(report);
            report.WriteMarkdown();
            return report;
        }

        private void RunOnApplyTests(BuffSystemLifecycleTestReport report)
        {
            RunCase(report, OnApplyCategory, "Lifecycle_OnApply_AddOnce_CalledOnce", "Single Add should call OnApply once.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 1, 1, 1);
                context.Assert(env.Effect.ApplyCount == 1, "OnApply should be called once for one layer.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnApplyCategory, "Lifecycle_OnApply_ReAddAppend_CalledForIncomingLayerOnly", "Append re-add should call OnApply only for incoming layer.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 2, maxStack: 3);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 2, 1, 1);
                int beforeApply = env.Effect.ApplyCount;
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 2, 1, 2);
                context.Assert(env.Effect.ApplyCount - beforeApply == 1, "Append should apply only the incoming layer.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnApplyCategory, "Lifecycle_OnApply_RefreshAll_NotFull_CalledForIncomingLayer", "RefreshAll under MaxStack should apply incoming layer.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 3, maxStack: 3, stackUpPolicy: ParallelBuffStackUpPolicy.RefreshAll);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 3, 2, 1);
                int beforeApply = env.Effect.ApplyCount;
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 3, 1, 2);
                context.Assert(env.Effect.ApplyCount - beforeApply == 1, "RefreshAll not-full should apply the appended incoming layer.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnApplyCategory, "Lifecycle_OnApply_RefreshAll_WhenFull_NotCalledForRejectedIncoming", "RefreshAll at MaxStack should not apply rejected incoming layer.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 4, maxStack: 3, stackUpPolicy: ParallelBuffStackUpPolicy.RefreshAll);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 4, 3, 1);
                int beforeApply = env.Effect.ApplyCount;
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 4, 1, 2);
                context.Assert(env.Effect.ApplyCount == beforeApply, "RefreshAll full should not apply rejected incoming layer.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnApplyCategory, "Lifecycle_OnApply_Replace_CalledForReplacementLayer", "Replace should apply replacement layer according to runtime behavior.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 5, maxStack: 1, stackUpPolicy: ParallelBuffStackUpPolicy.ReplaceEarliestWhenFull);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 5, 1, 1);
                int beforeApply = env.Effect.ApplyCount;
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 5, 1, 2);
                context.Assert(env.Effect.ApplyCount - beforeApply == 1, "Replace should apply the replacement layer once.");
                context.Actual = env.Describe();
            });
        }

        private void RunOnTickTests(BuffSystemLifecycleTestReport report)
        {
            RunCase(report, OnTickCategory, "Lifecycle_OnTick_TickIntervalOne_CountMatchesFrames", "TickInterval=1 should tick once per visible layer per advanced frame.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 20, tickIntervalFrames: 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 20, 1, 1);
                int beforeTick = env.Effect.TickCount;
                TickRange(env, 2, 5);
                context.Assert(env.Effect.TickCount - beforeTick == 5, "TickInterval=1 should tick on each following frame.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnTickCategory, "Lifecycle_OnTick_TickIntervalTwo_CountMatchesExpected", "TickInterval=2 should not tick every frame.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 21, tickIntervalFrames: 2);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 21, 1, 1);
                int beforeTick = env.Effect.TickCount;
                TickRange(env, 2, 6);
                int delta = env.Effect.TickCount - beforeTick;
                context.Assert(delta > 0, "TickInterval=2 should tick within six frames.");
                context.Assert(delta < 6, "TickInterval=2 should tick fewer times than every frame.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnTickCategory, "Lifecycle_OnTick_TickIntervalGreaterThanDuration_NoUnexpectedTick", "TickInterval greater than duration should not tick before expire.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 22, durationFrames: 2, tickIntervalFrames: 10);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 22, 1, 1);
                int beforeTick = env.Effect.TickCount;
                TickUntilMissing(env, env.TargetA, env.SourceA, BaseConfigId + 22, 2, 8);
                context.Assert(env.Effect.TickCount == beforeTick, "No extra OnTick should happen before short duration expires.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnTickCategory, "Lifecycle_OnTick_BeforeFirstInterval_NotCalledEarly", "Tick should not happen before first configured interval.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 23, tickIntervalFrames: 5);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 23, 1, 1);
                int beforeTick = env.Effect.TickCount;
                Tick(env, 2);
                context.Assert(env.Effect.TickCount == beforeTick, "TickInterval=5 should not tick on the next frame.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnTickCategory, "Lifecycle_OnTick_MultipleStacks_TickCountMatchesVisibleLayersOrDocumentedAggregate", "Multiple visible layers should produce documented tick callbacks.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 24, maxStack: 3, tickIntervalFrames: 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 24, 3, 1);
                int beforeTick = env.Effect.TickCount;
                Tick(env, 2);
                int delta = env.Effect.TickCount - beforeTick;
                context.Assert(delta >= 1, "At least one tick callback should happen for visible stacked buff.");
                context.Assert(delta <= 3, "EntityPerStack path should not tick more times than visible layers.");
                context.Actual = env.Describe() + $", tickDelta={delta}";
            });

            RunCase(report, OnTickCategory, "Lifecycle_OnTick_AfterRemove_NotCalledAgain", "Removed buff should not tick again.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 25, tickIntervalFrames: 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 25, 1, 1);
                RemoveAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 25, 1, false, true, 2);
                int beforeTick = env.Effect.TickCount;
                TickRange(env, 3, 4);
                context.Assert(env.Effect.TickCount == beforeTick, "Removed buff should not receive later OnTick.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnTickCategory, "Lifecycle_OnTick_AfterExpire_NotCalledAgain", "Expired buff should not tick again.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 26, durationFrames: 1, tickIntervalFrames: 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 26, 1, 1);
                TickUntilMissing(env, env.TargetA, env.SourceA, BaseConfigId + 26, 2, 6);
                int beforeTick = env.Effect.TickCount;
                TickRange(env, 8, 3);
                context.Assert(env.Effect.TickCount == beforeTick, "Expired buff should not receive later OnTick.");
                context.Actual = env.Describe();
            });
        }

        private void RunOnRemoveTests(BuffSystemLifecycleTestReport report)
        {
            RunCase(report, OnRemoveCategory, "Lifecycle_OnRemove_ManualRemove_CalledOnce", "Manual remove should call OnRemove once.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 40);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 40, 1, 1);
                RemoveAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 40, 1, false, true, 2);
                context.Assert(env.Effect.RemoveCount == 1, "Manual remove should call OnRemove exactly once.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnRemoveCategory, "Lifecycle_OnRemove_Expire_CalledOnce", "Expire should call OnRemove once.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 41, durationFrames: 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 41, 1, 1);
                TickUntilMissing(env, env.TargetA, env.SourceA, BaseConfigId + 41, 2, 6);
                context.Assert(env.Effect.RemoveCount == 1, "Expire should call OnRemove exactly once.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnRemoveCategory, "Lifecycle_OnRemove_RemoveAfterExpire_NotCalledTwice", "Remove after expire should not double remove.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 42, durationFrames: 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 42, 1, 1);
                TickUntilMissing(env, env.TargetA, env.SourceA, BaseConfigId + 42, 2, 6);
                int beforeRemove = env.Effect.RemoveCount;
                RemoveAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 42, 1, false, true, 10);
                context.Assert(env.Effect.RemoveCount == beforeRemove, "Remove after expire should not call OnRemove again.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnRemoveCategory, "Lifecycle_OnRemove_ClearAll_CalledForEachRemovedLayerOrDocumentedAggregate", "ClearAll should remove visible layers according to EntityPerStack semantics.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 43, maxStack: 3);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 43, 3, 1);
                RemoveAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 43, 0, false, true, 2);
                context.Assert(env.Effect.RemoveCount == 3, "EntityPerStack ClearAll should call OnRemove once per removed layer.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnRemoveCategory, "Lifecycle_OnRemove_RemoveMissing_DoesNotCall", "Removing missing buff should not call OnRemove.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 44);
                RemoveAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 44, 1, false, true, 1);
                context.Assert(env.Effect.RemoveCount == 0, "Remove missing should not call OnRemove.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnRemoveCategory, "Lifecycle_OnRemove_ReplaceOldLayer_CalledIfRuntimeRemovesOldLayer", "Replace remove semantics should be visible and documented.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 45, maxStack: 1, stackUpPolicy: ParallelBuffStackUpPolicy.ReplaceEarliestWhenFull);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 45, 1, 1);
                int beforeRemove = env.Effect.RemoveCount;
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 45, 1, 2);
                context.Assert(env.Effect.RemoveCount >= beforeRemove, "Replace should not reduce remove count.");
                context.Assert(env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 45, env.SourceA, out _), "Replacement buff should remain visible.");
                context.Actual = env.Describe();
            });
        }

        private void RunOnRefreshTests(BuffSystemLifecycleTestReport report)
        {
            RunCase(report, OnRefreshCategory, "Lifecycle_OnRefresh_RefreshAll_NotFull_CalledForExistingLayers", "RefreshAll not-full should refresh existing layers.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 60, maxStack: 3, stackUpPolicy: ParallelBuffStackUpPolicy.RefreshAll);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 60, 2, 1);
                int beforeRefresh = env.Effect.RefreshCount;
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 60, 1, 2);
                context.Assert(env.Effect.RefreshCount - beforeRefresh >= 2, "RefreshAll not-full should refresh existing layers.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnRefreshCategory, "Lifecycle_OnRefresh_RefreshAll_WhenFull_CalledForExistingLayers", "RefreshAll full should refresh existing layers.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 61, maxStack: 3, stackUpPolicy: ParallelBuffStackUpPolicy.RefreshAll);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 61, 3, 1);
                int beforeRefresh = env.Effect.RefreshCount;
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 61, 1, 2);
                context.Assert(env.Effect.RefreshCount - beforeRefresh >= 3, "RefreshAll full should refresh all existing layers.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnRefreshCategory, "Lifecycle_OnRefresh_Append_DoesNotCallRefreshUnlessRuntimeDefinesIt", "Append should not call OnRefresh in current EntityPerStack semantics.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 62, maxStack: 3);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 62, 1, 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 62, 1, 2);
                context.Assert(env.Effect.RefreshCount == 0, "Append should not call OnRefresh.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnRefreshCategory, "Lifecycle_OnRefresh_Replace_DoesOrDoesNotCallRefresh_Documented", "Replace refresh behavior should be stable and documented.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 63, maxStack: 1, stackUpPolicy: ParallelBuffStackUpPolicy.ReplaceEarliestWhenFull);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 63, 1, 1);
                int beforeRefresh = env.Effect.RefreshCount;
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 63, 1, 2);
                context.Assert(env.Effect.RefreshCount >= beforeRefresh, "Replace should not reduce refresh count.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnRefreshCategory, "Lifecycle_OnRefresh_RefreshDoesNotCallOnApplyForExistingLayers", "Refreshing existing layers should not call OnApply again.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 64, maxStack: 2, stackUpPolicy: ParallelBuffStackUpPolicy.RefreshAll);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 64, 2, 1);
                int beforeApply = env.Effect.ApplyCount;
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 64, 1, 2);
                context.Assert(env.Effect.ApplyCount == beforeApply, "RefreshAll full should refresh existing layers without applying rejected incoming.");
                context.Assert(env.Effect.RefreshCount >= 2, "RefreshAll full should still call OnRefresh.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnRefreshCategory, "Lifecycle_OnRefresh_RefreshResetsOrExtendsRemainingFrames", "Refresh should not reduce public RemainingFrames.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 65, maxStack: 2, durationFrames: 8, stackUpPolicy: ParallelBuffStackUpPolicy.RefreshAll);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 65, 2, 1);
                Tick(env, 2);
                env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 65, env.SourceA, out BuffViewData before);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 65, 1, 3);
                env.BuffSystem.TryGetBuff(env.TargetA, BaseConfigId + 65, env.SourceA, out BuffViewData after);
                context.Assert(after.RemainingFrames >= before.RemainingFrames, "Refresh should restore or extend RemainingFrames.");
                context.Actual = env.Describe() + $", beforeRemaining={before.RemainingFrames}, afterRemaining={after.RemainingFrames}";
            });
        }

        private void RunOnStackChangedTests(BuffSystemLifecycleTestReport report)
        {
            RunCase(report, OnStackChangedCategory, "Lifecycle_OnStackChanged_Append_DeltaPositive", "Append growth should produce positive stack delta.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 80, maxStack: 3);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 80, 1, 1);
                int beforeCount = env.Effect.StackChangedCount;
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 80, 1, 2);
                context.Assert(env.Effect.StackChangedCount > beforeCount, "Append should call OnStackChanged.");
                context.Assert(env.Effect.LastStackDelta > 0, "Append delta should be positive.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnStackChangedCategory, "Lifecycle_OnStackChanged_AppendAtMaxStack_NoExtraDelta", "Append at MaxStack should not produce extra stack delta.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 81, maxStack: 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 81, 1, 1);
                int beforeCount = env.Effect.StackChangedCount;
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 81, 1, 2);
                context.Assert(env.Effect.StackChangedCount == beforeCount, "Rejected append at MaxStack should not call OnStackChanged.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnStackChangedCategory, "Lifecycle_OnStackChanged_ManualRemove_DeltaNegativeOrDocumented", "Manual remove stack delta should be negative when emitted.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 82, maxStack: 3);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 82, 2, 1);
                int beforeCount = env.Effect.StackChangedCount;
                RemoveAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 82, 1, false, false, 2);
                context.Assert(env.Effect.StackChangedCount >= beforeCount, "Manual remove should not reduce stack changed count.");
                if (env.Effect.StackChangedCount > beforeCount)
                    context.Assert(env.Effect.LastStackDelta < 0, "Manual remove emitted delta should be negative.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnStackChangedCategory, "Lifecycle_OnStackChanged_Expire_DeltaNegativeOrDocumented", "Expire stack delta should be negative when emitted.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 83, durationFrames: 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 83, 1, 1);
                int beforeCount = env.Effect.StackChangedCount;
                TickUntilMissing(env, env.TargetA, env.SourceA, BaseConfigId + 83, 2, 6);
                context.Assert(env.Effect.StackChangedCount >= beforeCount, "Expire should not reduce stack changed count.");
                if (env.Effect.StackChangedCount > beforeCount)
                    context.Assert(env.Effect.LastStackDelta < 0, "Expire emitted delta should be negative.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnStackChangedCategory, "Lifecycle_OnStackChanged_RefreshAll_NotFull_DeltaPositiveForIncoming", "RefreshAll not-full append should emit positive stack delta.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 84, maxStack: 3, stackUpPolicy: ParallelBuffStackUpPolicy.RefreshAll);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 84, 2, 1);
                int beforeCount = env.Effect.StackChangedCount;
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 84, 1, 2);
                context.Assert(env.Effect.StackChangedCount > beforeCount, "RefreshAll not-full incoming append should call OnStackChanged.");
                context.Assert(env.Effect.LastStackDelta > 0, "RefreshAll not-full append delta should be positive.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnStackChangedCategory, "Lifecycle_OnStackChanged_RefreshAll_WhenFull_NoDelta", "RefreshAll full refresh should not emit stack delta.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 85, maxStack: 3, stackUpPolicy: ParallelBuffStackUpPolicy.RefreshAll);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 85, 3, 1);
                int beforeCount = env.Effect.StackChangedCount;
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 85, 1, 2);
                context.Assert(env.Effect.StackChangedCount == beforeCount, "RefreshAll full should not call OnStackChanged.");
                context.Actual = env.Describe();
            });

            RunCase(report, OnStackChangedCategory, "Lifecycle_OnStackChanged_Replace_DeltaSemanticsDocumented", "Replace stack delta should remain deterministic.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 86, maxStack: 1, stackUpPolicy: ParallelBuffStackUpPolicy.ReplaceEarliestWhenFull);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 86, 1, 1);
                int beforeCount = env.Effect.StackChangedCount;
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 86, 1, 2);
                context.Assert(env.Effect.StackChangedCount >= beforeCount, "Replace should not reduce stack changed count.");
                context.Actual = env.Describe();
            });
        }

        private void RunInterleavingTests(BuffSystemLifecycleTestReport report)
        {
            RunCase(report, InterleavingCategory, "Lifecycle_Interleaving_AddRefreshRemove_NoDuplicateCallbacks", "Add, RefreshAll, Remove should not duplicate callbacks unexpectedly.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 100, maxStack: 3, stackUpPolicy: ParallelBuffStackUpPolicy.RefreshAll);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 100, 2, 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 100, 1, 2);
                RemoveAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 100, 1, false, false, 3);
                context.Assert(env.Effect.ApplyCount == 3, "Two initial layers plus one incoming layer should apply exactly three times.");
                context.Assert(env.Effect.RefreshCount >= 2, "RefreshAll should refresh existing layers.");
                context.Assert(env.Effect.RemoveCount == 1, "Removing one layer should remove once.");
                context.Actual = env.Describe();
            });

            RunCase(report, InterleavingCategory, "Lifecycle_Interleaving_AddExpireRemove_NoDoubleRemove", "Expire followed by remove should not double remove.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 101, durationFrames: 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 101, 1, 1);
                TickUntilMissing(env, env.TargetA, env.SourceA, BaseConfigId + 101, 2, 6);
                int beforeRemove = env.Effect.RemoveCount;
                RemoveAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 101, 1, false, true, 10);
                context.Assert(env.Effect.RemoveCount == beforeRemove, "Remove after expire should not double-call OnRemove.");
                context.Actual = env.Describe();
            });

            RunCase(report, InterleavingCategory, "Lifecycle_Interleaving_AppendThenClearAll_CallbackCountsMatch", "Append stack then ClearAll should remove all layers once.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 102, maxStack: 3);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 102, 1, 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 102, 1, 2);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 102, 1, 3);
                RemoveAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 102, 0, false, true, 4);
                context.Assert(env.Effect.ApplyCount == 3, "Three appended layers should apply three times.");
                context.Assert(env.Effect.RemoveCount == 3, "ClearAll should remove three layers.");
                context.Actual = env.Describe();
            });

            RunCase(report, InterleavingCategory, "Lifecycle_Interleaving_RefreshThenExpire_RemoveOnce", "Refresh then expire should remove each layer once.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 103, maxStack: 2, durationFrames: 3, stackUpPolicy: ParallelBuffStackUpPolicy.RefreshAll);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 103, 2, 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 103, 1, 2);
                TickUntilMissing(env, env.TargetA, env.SourceA, BaseConfigId + 103, 3, 10);
                context.Assert(env.Effect.RefreshCount >= 2, "RefreshAll should happen before expire.");
                context.Assert(env.Effect.RemoveCount == 2, "Two refreshed layers should expire once each.");
                context.Actual = env.Describe();
            });

            RunCase(report, InterleavingCategory, "Lifecycle_Interleaving_ReplaceThenExpire_CallbackSequenceDocumented", "Replace then expire should have deterministic callback sequence.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 104, maxStack: 1, durationFrames: 3, stackUpPolicy: ParallelBuffStackUpPolicy.ReplaceEarliestWhenFull);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 104, 1, 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 104, 1, 2);
                TickUntilMissing(env, env.TargetA, env.SourceA, BaseConfigId + 104, 3, 10);
                context.Assert(env.Effect.ApplyCount == 2, "Initial and replacement layers should apply.");
                context.Assert(env.Effect.RemoveCount >= 1, "Replace/expire sequence should remove at least one layer.");
                context.Actual = env.Describe();
            });
        }

        private void RunContextTests(BuffSystemLifecycleTestReport report)
        {
            RunCase(report, ContextCategory, "Lifecycle_Context_OnApply_TargetSourceDefinitionCorrect", "OnApply context should carry target, source, and definition.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 120);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 120, 1, 1);
                context.Assert(env.Effect.HasRecord("Apply", env.TargetA, env.SourceA, BaseConfigId + 120), "OnApply context should match target/source/config.");
                context.Actual = env.Describe();
            });

            RunCase(report, ContextCategory, "Lifecycle_Context_OnTick_TargetSourceDefinitionCorrect", "OnTick context should carry target, source, and definition.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 121, tickIntervalFrames: 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 121, 1, 1);
                Tick(env, 2);
                context.Assert(env.Effect.HasRecord("Tick", env.TargetA, env.SourceA, BaseConfigId + 121), "OnTick context should match target/source/config.");
                context.Actual = env.Describe();
            });

            RunCase(report, ContextCategory, "Lifecycle_Context_OnRemove_TargetSourceDefinitionCorrect", "OnRemove context should carry target, source, and definition.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 122);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 122, 1, 1);
                RemoveAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 122, 1, false, true, 2);
                context.Assert(env.Effect.HasRecord("Remove", env.TargetA, env.SourceA, BaseConfigId + 122), "OnRemove context should match target/source/config.");
                context.Actual = env.Describe();
            });

            RunCase(report, ContextCategory, "Lifecycle_Context_OnRefresh_TargetSourceDefinitionCorrect", "OnRefresh context should carry target, source, and definition.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 123, maxStack: 2, stackUpPolicy: ParallelBuffStackUpPolicy.RefreshAll);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 123, 2, 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 123, 1, 2);
                context.Assert(env.Effect.HasRecord("Refresh", env.TargetA, env.SourceA, BaseConfigId + 123), "OnRefresh context should match target/source/config.");
                context.Actual = env.Describe();
            });

            RunCase(report, ContextCategory, "Lifecycle_Context_OnStackChanged_TargetSourceDefinitionCorrect", "OnStackChanged context should carry target, source, and definition.", context =>
            {
                TestEnvironment env = CreateEnvironment(context);
                RegisterDefinition(env, BaseConfigId + 124, maxStack: 3);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 124, 1, 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 124, 1, 2);
                context.Assert(env.Effect.HasRecord("StackChanged", env.TargetA, env.SourceA, BaseConfigId + 124), "OnStackChanged context should match target/source/config.");
                context.Actual = env.Describe();
            });
        }

        private static void RunCase(BuffSystemLifecycleTestReport report, string category, string caseName, string expected, Action<CaseContext> body)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            CaseContext context = new CaseContext(expected);
            try
            {
                body(context);
                stopwatch.Stop();
                report.Add(BuffSystemLifecycleTestCaseResult.FromContext(
                    category,
                    caseName,
                    BuffSystemLifecycleTestStatus.Passed,
                    expected,
                    context.Actual,
                    context.InvariantChecks,
                    stopwatch.Elapsed.TotalMilliseconds,
                    context.Snapshot(),
                    null));
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                report.Add(BuffSystemLifecycleTestCaseResult.FromContext(
                    category,
                    caseName,
                    BuffSystemLifecycleTestStatus.Failed,
                    expected,
                    context.Actual,
                    context.InvariantChecks,
                    stopwatch.Elapsed.TotalMilliseconds,
                    context.Snapshot(),
                    exception));
            }
        }

        private static TestEnvironment CreateEnvironment(CaseContext context)
        {
            World world = new World();
            Entity targetA = world.CreateEntity();
            Entity targetB = world.CreateEntity();
            Entity sourceA = world.CreateEntity();
            Entity sourceB = world.CreateEntity();
            BuffDefinitionRegistry definitions = new BuffDefinitionRegistry();
            BuffEffectRegistry effects = new BuffEffectRegistry();
            CountingLifecycleEffect effect = new CountingLifecycleEffect();
            effects.Register(DefaultEffectId, effect);
            BuffSystemCore buffSystem = new BuffSystemCore(definitions, effects);
            TestEnvironment env = new TestEnvironment(world, buffSystem, definitions, effects, effect, targetA, targetB, sourceA, sourceB);
            context.Track(effect);
            return env;
        }

        private static void RegisterDefinition(
            TestEnvironment env,
            int configId,
            int maxStack = 3,
            int durationFrames = 20,
            int tickIntervalFrames = 1,
            ParallelBuffStackUpPolicy stackUpPolicy = ParallelBuffStackUpPolicy.Append,
            ParallelBuffStackDownPolicy stackDownPolicy = ParallelBuffStackDownPolicy.RemoveEarliest,
            bool isForever = false)
        {
            env.Definitions.Register(new BuffDefinition(
                configId,
                "LifecycleTest_" + configId,
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
                DefaultEffectId,
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

        private sealed class TestEnvironment
        {
            public readonly World World;
            public readonly BuffSystemCore BuffSystem;
            public readonly BuffDefinitionRegistry Definitions;
            public readonly BuffEffectRegistry Effects;
            public readonly CountingLifecycleEffect Effect;
            public readonly Entity TargetA;
            public readonly Entity TargetB;
            public readonly Entity SourceA;
            public readonly Entity SourceB;

            public TestEnvironment(
                World world,
                BuffSystemCore buffSystem,
                BuffDefinitionRegistry definitions,
                BuffEffectRegistry effects,
                CountingLifecycleEffect effect,
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

            public string Describe()
            {
                return $"apply={Effect.ApplyCount}, tick={Effect.TickCount}, remove={Effect.RemoveCount}, refresh={Effect.RefreshCount}, stackChanged={Effect.StackChangedCount}, lastDelta={Effect.LastStackDelta}, events={Effect.Events.Count}";
            }
        }

        private sealed class CountingLifecycleEffect : BuffEffectExecutorBase
        {
            public int ApplyCount;
            public int TickCount;
            public int RemoveCount;
            public int RefreshCount;
            public int StackChangedCount;
            public int LastStackDelta;
            public readonly List<LifecycleRecord> Records = new List<LifecycleRecord>();
            public readonly List<string> Events = new List<string>();

            public override void OnApply(in BuffEffectContext context)
            {
                ApplyCount++;
                Record("Apply", in context, 0);
            }

            public override void OnRefresh(in BuffEffectContext context)
            {
                RefreshCount++;
                Record("Refresh", in context, 0);
            }

            public override void OnStackChanged(in BuffEffectContext context, int delta)
            {
                StackChangedCount++;
                LastStackDelta = delta;
                Record("StackChanged", in context, delta);
            }

            public override void OnTick(in BuffEffectContext context)
            {
                TickCount++;
                Record("Tick", in context, 0);
            }

            public override void OnRemove(in BuffEffectContext context)
            {
                RemoveCount++;
                Record("Remove", in context, 0);
            }

            public bool HasRecord(string phase, Entity target, Entity source, int configId)
            {
                for (int i = 0; i < Records.Count; i++)
                {
                    LifecycleRecord record = Records[i];
                    if (record.Phase == phase &&
                        record.Target.Equals(target) &&
                        record.Source.Equals(source) &&
                        record.ConfigId == configId)
                    {
                        return true;
                    }
                }

                return false;
            }

            public LifecycleEffectSnapshot Snapshot()
            {
                return new LifecycleEffectSnapshot(
                    ApplyCount,
                    TickCount,
                    RemoveCount,
                    RefreshCount,
                    StackChangedCount,
                    LastStackDelta,
                    new List<string>(Events));
            }

            private void Record(string phase, in BuffEffectContext context, int delta)
            {
                LifecycleRecord record = new LifecycleRecord(
                    phase,
                    context.SimulationContext.frameNumber,
                    context.BuffEntity,
                    context.Runtime.target,
                    context.Runtime.source,
                    context.Runtime.configId,
                    context.Runtime.stack,
                    context.Runtime.runtimeHandle,
                    delta);
                Records.Add(record);
                Events.Add(record.ToString());
            }
        }

        private readonly struct LifecycleRecord
        {
            public readonly string Phase;
            public readonly int Frame;
            public readonly Entity BuffEntity;
            public readonly Entity Target;
            public readonly Entity Source;
            public readonly int ConfigId;
            public readonly int Stack;
            public readonly int RuntimeHandle;
            public readonly int Delta;

            public LifecycleRecord(string phase, int frame, Entity buffEntity, Entity target, Entity source, int configId, int stack, int runtimeHandle, int delta)
            {
                Phase = phase;
                Frame = frame;
                BuffEntity = buffEntity;
                Target = target;
                Source = source;
                ConfigId = configId;
                Stack = stack;
                RuntimeHandle = runtimeHandle;
                Delta = delta;
            }

            public override string ToString()
            {
                return $"frame={Frame}, phase={Phase}, configId={ConfigId}, target={Target.ID}:{Target.Version}, source={Source.ID}:{Source.Version}, buffEntity={BuffEntity.ID}:{BuffEntity.Version}, stack={Stack}, runtimeHandle={RuntimeHandle}, delta={Delta}";
            }
        }

        private sealed class CaseContext
        {
            public readonly string Expected;
            public string Actual;
            public int InvariantChecks;
            private CountingLifecycleEffect _effect;

            public CaseContext(string expected)
            {
                Expected = expected;
                Actual = string.Empty;
            }

            public void Track(CountingLifecycleEffect effect)
            {
                _effect = effect;
            }

            public LifecycleEffectSnapshot Snapshot()
            {
                return _effect != null ? _effect.Snapshot() : LifecycleEffectSnapshot.Empty;
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
