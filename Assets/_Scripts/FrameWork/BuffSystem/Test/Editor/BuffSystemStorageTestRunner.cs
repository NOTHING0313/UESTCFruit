using ECSFrameWork;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace BuffSystem.EditorTesting
{
    internal sealed class BuffSystemStorageTestRunner
    {
        internal const string DiscoveryCategory = "Discovery";
        internal const string EntityBaselineCategory = "EntityPerStack Baseline";
        internal const string CompressedEligibilityCategory = "Compressed Eligibility";
        internal const string CompareCategory = "EntityPerStack vs Compressed";
        internal const string RestoreHookCategory = "Restore Hook / Cache";
        internal const string ReproCategory = "Repro Cases";
        internal const string PerformanceCategory = "Performance Snapshot";

        private const float FixedTickLength = 0.02f;
        private const int EffectId = 98001;
        private const string ClassificationTestFixtureWrong = "TestFixtureWrong";
        private const string ClassificationTimingToleranceIssue = "TimingToleranceIssue";
        private const string ClassificationCompressedCacheOrQueryMismatch = "CompressedCacheOrQueryMismatch";
        private const string ClassificationCompressedAddDidNotCreateRuntime = "CompressedAddDidNotCreateRuntime";
        private const string ClassificationUnclassified = "Unclassified";

        private static readonly MethodInfo CompressedFactoryMethod =
            typeof(BuffSystemCore).GetMethod("CreateForCompressedParallelValidation", BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly MethodInfo CompressedEligibilityMethod =
            typeof(BuffSystemCore).GetMethod("IsCompressedParallelEligible", BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly MethodInfo WorldRestoredMethod =
            typeof(BuffSystemCore).GetMethod("OnWorldRestored", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        public BuffSystemStorageTestReport RunAll()
        {
            BuffSystemStorageTestReport report = BuffSystemStorageTestReport.Create();
            RunDiscovery(report);
            RunEntityPerStackBaseline(report);
            RunCompressedEligibility(report);
            RunCompressedComparisons(report);
            RunRestoreHookTests(report);
            RunFailureClassificationSummary(report);
            RunReproCases(report);
            RunPerformanceSnapshots(report);
            report.WriteMarkdown();
            return report;
        }

        private static void RunDiscovery(BuffSystemStorageTestReport report)
        {
            RunCase(report, DiscoveryCategory, "StorageDiscovery_EntityPerStack_Available", "EntityPerStack core can be created", "EntityPerStack", () =>
            {
                TestEnvironment env = CreateEnvironment(false, 9001, ParallelBuffStorageMode.EntityPerStack, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest, 3, 12, 1);
                return Pass("Created BuffSystemCore with in-memory registry", Counts(env, 0, 0), Counts(env, 0, 0), 1);
            });

            RunCase(report, DiscoveryCategory, "StorageDiscovery_CompressedParallel_AvailableOrManualRequired", "Compressed validation factory exists", "CompressedExpiryFrameList", () =>
            {
                if (CompressedFactoryMethod == null)
                    return Manual("Compressed validation factory is internal and not discoverable from Editor assembly.");

                TestEnvironment env = CreateEnvironment(true, 9301, ParallelBuffStorageMode.CompressedExpiryFrameList, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest, 3, 12, 1);
                AddAndTick(env, 9301, 2, 1);
                return Pass("Compressed factory invoked by reflection", "EntityPerStackRuntime=0, CompressedRuntime=1", Counts(env, 0, 1), 2);
            });

            RunCase(report, DiscoveryCategory, "StorageDiscovery_CompressedEligibilityUtility_Detected", "Eligibility method detected", "Reflection", () =>
            {
                if (CompressedEligibilityMethod == null)
                    return Manual("BuffSystemCore.IsCompressedParallelEligible was not found by reflection.");

                return Pass("Eligibility method detected", "Method=IsCompressedParallelEligible", "Method=IsCompressedParallelEligible", 1);
            });

            RunCase(report, DiscoveryCategory, "StorageDiscovery_ExistingCompressedRunners_Detected", "Existing compressed runners detected", "Reflection", () =>
            {
                string[] names =
                {
                    "BuffSystemCompressedParallelValidationRunner",
                    "BuffSystemStorageBehaviorConsistencyRunner",
                    "BuffSystemStoragePerformanceRunner"
                };

                List<string> found = new List<string>();
                for (int i = 0; i < names.Length; i++)
                {
                    Type type = FindTypeByName(names[i]);
                    if (type != null)
                        found.Add(type.FullName);
                }

                if (found.Count != names.Length)
                    throw new InvalidOperationException("Existing compressed runner discovery incomplete. Found=" + string.Join(", ", found));

                return Pass("All existing compressed runners detected", "Found=" + names.Length, string.Join(", ", found), found.Count);
            });

            RunCase(report, DiscoveryCategory, "StorageDiscovery_SafeEditorOnlyCreation_DetectedOrManualRequired", "Compressed runtime can be created without runtime edits", "CompressedExpiryFrameList", () =>
            {
                if (CompressedFactoryMethod == null)
                    return Manual("Compressed factory unavailable; run MonoBehaviour compressed runners manually.");

                TestEnvironment env = CreateEnvironment(true, 9301, ParallelBuffStorageMode.CompressedExpiryFrameList, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest, 3, 12, 1);
                AddAndTick(env, 9301, 3, 1);
                int entityCount = CountRuntimeEntities(env.World);
                int compressedCount = CountCompressedRuntimeEntities(env.World);
                AssertEqual(0, entityCount, "EntityPerStack runtime count");
                AssertEqual(1, compressedCount, "Compressed runtime count");
                return Pass("Reflection factory produced compressed runtime only", "EntityPerStackRuntime=0, CompressedRuntime=1", Counts(env, 0, 1), 3);
            });
        }

        private static void RunEntityPerStackBaseline(BuffSystemStorageTestReport report)
        {
            RunCase(report, EntityBaselineCategory, "Storage_EntityPerStack_AddTickRemove_Baseline", "Add / Tick / Remove works", "EntityPerStack", () =>
            {
                TestEnvironment env = CreateEnvironment(false, 9101, ParallelBuffStorageMode.EntityPerStack, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest, 3, 12, 1);
                AddAndTick(env, 9101, 1, 1);
                BuffViewData beforeRemove = RequireView(env, 9101);
                env.BuffSystem.RemoveBuff(new RemoveBuffCommand(env.Target, 9101, env.Source, 1));
                Tick(env, 2);
                AssertFalse(env.BuffSystem.TryGetBuff(env.Target, 9101, env.Source, out BuffViewData _), "Remove should hide view");
                return Pass("Stack=" + beforeRemove.Stack + ", RemainingBeforeRemove=" + beforeRemove.RemainingFrames, "Visible then removed", Counts(env, 0, 0), 4);
            });

            RunCase(report, EntityBaselineCategory, "Storage_EntityPerStack_Append_MaxStack_Baseline", "Append clamps to MaxStack", "EntityPerStack", () =>
            {
                TestEnvironment env = CreateEnvironment(false, 9102, ParallelBuffStorageMode.EntityPerStack, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest, 3, 12, 1);
                AddAndTick(env, 9102, 5, 1);
                BuffViewData view = RequireView(env, 9102);
                AssertEqual(3, view.Stack, "Stack should clamp to MaxStack");
                return Pass("Stack=3", ViewSummary(view), Counts(env, 3, 0), 3);
            });

            RunCase(report, EntityBaselineCategory, "Storage_EntityPerStack_RefreshAll_Baseline", "RefreshAll refreshes existing and may append while not full", "EntityPerStack", () =>
            {
                TestEnvironment env = CreateEnvironment(false, 9103, ParallelBuffStorageMode.EntityPerStack, ParallelBuffStackUpPolicy.RefreshAll, ParallelBuffStackDownPolicy.RemoveEarliest, 3, 10, 1);
                AddAndTick(env, 9103, 2, 1);
                Tick(env, 3);
                env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, 9103, env.Source, 1));
                Tick(env, 4);
                BuffViewData view = RequireView(env, 9103);
                AssertEqual(3, view.Stack, "RefreshAll not-full stack behavior");
                AssertTrue(view.RemainingFrames >= 8, "RefreshAll should refresh duration");
                return Pass("Stack=3, Remaining refreshed", ViewSummary(view), Counts(env, 3, 0), 4);
            });

            RunCase(report, EntityBaselineCategory, "Storage_EntityPerStack_ReplaceEarliestWhenFull_Baseline", "ReplaceEarliestWhenFull keeps MaxStack", "EntityPerStack", () =>
            {
                TestEnvironment env = CreateEnvironment(false, 9104, ParallelBuffStorageMode.EntityPerStack, ParallelBuffStackUpPolicy.ReplaceEarliestWhenFull, ParallelBuffStackDownPolicy.RemoveEarliest, 3, 10, 1);
                AddAndTick(env, 9104, 3, 1);
                Tick(env, 3);
                env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, 9104, env.Source, 1));
                Tick(env, 4);
                BuffViewData view = RequireView(env, 9104);
                AssertEqual(3, view.Stack, "Stack should remain MaxStack");
                return Pass("Stack remains 3", ViewSummary(view), Counts(env, 3, 0), 3);
            });

            RunCase(report, EntityBaselineCategory, "Storage_EntityPerStack_Expire_RemovesFromQuery_Baseline", "Expire removes view", "EntityPerStack", () =>
            {
                TestEnvironment env = CreateEnvironment(false, 9105, ParallelBuffStorageMode.EntityPerStack, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest, 2, 2, 1);
                AddAndTick(env, 9105, 1, 1);
                Tick(env, 2);
                Tick(env, 3);
                AssertFalse(env.BuffSystem.TryGetBuff(env.Target, 9105, env.Source, out BuffViewData _), "Expired buff should be hidden");
                return Pass("Expired after duration frames", "TryGetBuff=false", Counts(env, 0, 0), 3);
            });

            RunCase(report, EntityBaselineCategory, "Storage_EntityPerStack_SourceTargetIsolation_Baseline", "Target and source isolation works", "EntityPerStack", () =>
            {
                TestEnvironment env = CreateEnvironment(false, 9106, ParallelBuffStorageMode.EntityPerStack, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest, 3, 12, 1);
                Entity sourceB = env.World.CreateEntity();
                Entity targetB = env.World.CreateEntity();
                env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, 9106, env.Source, 1));
                env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, 9106, sourceB, 2));
                env.BuffSystem.AddBuff(new AddBuffCommand(targetB, 9106, env.Source, 1));
                Tick(env, 1);
                AssertEqual(1, RequireView(env, 9106, env.Target, env.Source).Stack, "Source A stack");
                AssertEqual(2, RequireView(env, 9106, env.Target, sourceB).Stack, "Source B stack");
                AssertEqual(1, CountBuffsWithConfig(env.BuffSystem.GetBuffs(targetB), 9106), "Target B count");
                return Pass("Different target/source are isolated", "TargetA views=2, TargetB views=1", Counts(env, 4, 0), 4);
            });
        }

        private static void RunCompressedEligibility(BuffSystemStorageTestReport report)
        {
            RunEligibilityCase(report, "Storage_CompressedEligibility_ValidConfig_Passes", CreateDefinition(9201, "Valid", EffectId, 3, 10, 1, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest, ParallelBuffStorageMode.CompressedExpiryFrameList), true, "Valid compressed config passes");
            RunEligibilityCase(report, "Storage_CompressedEligibility_InvalidEffect_FailsOrDocumentsReason", CreateDefinition(9202, "InvalidEffect", 0, 3, 10, 1, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest, ParallelBuffStorageMode.CompressedExpiryFrameList), true, "EffectId is not part of runtime compressed eligibility; effect registry validation is authoring-side.");
            RunEligibilityCase(report, "Storage_CompressedEligibility_EventTrigger_FailsOrDocumentsReason", CreateDefinition(9203, "EventTrigger", EffectId, 3, 10, 1, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest, ParallelBuffStorageMode.CompressedExpiryFrameList, BuffTriggerType.EventTrigger), false, "EventTrigger should not be compressed.");
            RunEligibilityCase(report, "Storage_CompressedEligibility_UnsupportedPolicy_FailsOrDocumentsReason", CreateDefinition(9204, "EntityPerStackStorage", EffectId, 3, 10, 1, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest, ParallelBuffStorageMode.EntityPerStack), false, "EntityPerStack storage mode should not be compressed.");
            RunEligibilityCase(report, "Storage_CompressedEligibility_991001_DebugSmoke_PassesIfStillExpected", CreateDefinition(991001, "Debug_CompressedParallel_TickSmoke", 990101, 3, 120, 60, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest, ParallelBuffStorageMode.CompressedExpiryFrameList), true, "991001 smoke pilot remains eligible.");
        }

        private static void RunCompressedComparisons(BuffSystemStorageTestReport report)
        {
            RunCompressedCase(report, "Storage_Compare_AddTickRemove_PublicViewsMatch", "Add / Tick / Remove public views match", pair =>
            {
                AddBothAndTick(pair, 9301, 1, 1);
                Tick(pair, 2);
                AssertPublicViewsMatch(pair, 9301, true);
                RemoveBothAndTick(pair, 9301, 1, 3);
                AssertNotFound(pair, 9301);
                return PairSummary(pair, 9301);
            });

            RunCompressedCase(report, "Storage_Compare_TryGetBuff_Matches", "TryGetBuff result matches", pair =>
            {
                AddBothAndTick(pair, 9302, 1, 1);
                Tick(pair, 2);
                AssertTryGetMatches(pair, 9302, true);
                return PairSummary(pair, 9302);
            });

            RunCompressedCase(report, "Storage_Compare_GetBuffsTarget_Matches", "GetBuffs(target) result matches", pair =>
            {
                AddBothAndTick(pair, 9303, 2, 1);
                Tick(pair, 2);
                AssertGetBuffsMatches(pair, 9303, true);
                return PairSummary(pair, 9303);
            });

            RunCompressedCase(report, "Storage_Compare_Append_MaxStack_Matches", "Append MaxStack behavior matches", pair =>
            {
                AddBothAndTick(pair, 9304, 5, 1);
                Tick(pair, 2);
                AssertPublicViewsMatch(pair, 9304, true);
                AssertEqual(3, RequireView(pair.EntityPerStack, 9304).Stack, "Entity stack");
                AssertEqual(3, RequireView(pair.Compressed, 9304).Stack, "Compressed stack");
                return PairSummary(pair, 9304);
            });

            RunCompressedCase(report, "Storage_Compare_RefreshAll_NotFull_Matches", "RefreshAll not-full behavior matches", pair =>
            {
                AddBothAndTick(pair, 9306, 2, 1);
                Tick(pair, 3);
                AddBothAndTick(pair, 9306, 1, 4);
                AssertPublicViewsMatch(pair, 9306, true);
                return PairSummary(pair, 9306);
            }, ParallelBuffStackUpPolicy.RefreshAll);

            RunCompressedCase(report, "Storage_Compare_RefreshAll_WhenFull_Matches", "RefreshAll full behavior matches", pair =>
            {
                AddBothAndTick(pair, 9306, 3, 1);
                Tick(pair, 3);
                AddBothAndTick(pair, 9306, 1, 4);
                AssertPublicViewsMatch(pair, 9306, true);
                AssertEqual(3, RequireView(pair.EntityPerStack, 9306).Stack, "Entity stack");
                AssertEqual(3, RequireView(pair.Compressed, 9306).Stack, "Compressed stack");
                return PairSummary(pair, 9306);
            }, ParallelBuffStackUpPolicy.RefreshAll);

            RunCompressedCase(report, "Storage_Compare_ReplaceEarliestWhenFull_Matches", "ReplaceEarliestWhenFull behavior matches", pair =>
            {
                AddBothAndTick(pair, 9314, 3, 1);
                Tick(pair, 2);
                Tick(pair, 3);
                AddBothAndTick(pair, 9314, 1, 4);
                AssertPublicViewsMatch(pair, 9314, true);
                return PairSummary(pair, 9314);
            }, ParallelBuffStackUpPolicy.ReplaceEarliestWhenFull);

            RunCompressedCase(report, "Storage_Compare_Expire_Matches", "Expire visibility matches", pair =>
            {
                AddBothAndTick(pair, 9310, 1, 1);
                Tick(pair, 2);
                Tick(pair, 3);
                AssertNotFound(pair, 9310);
                return PairSummary(pair, 9310);
            }, ParallelBuffStackUpPolicy.Append, durationFrames: 2);

            RunCompressedCase(report, "Storage_Compare_RemoveBySource_Matches", "Source-specific remove matches", pair =>
            {
                Entity entitySourceB = pair.EntityPerStack.World.CreateEntity();
                Entity compressedSourceB = pair.Compressed.World.CreateEntity();
                pair.EntityPerStack.BuffSystem.AddBuff(new AddBuffCommand(pair.EntityPerStack.Target, 9311, pair.EntityPerStack.Source, 2));
                pair.EntityPerStack.BuffSystem.AddBuff(new AddBuffCommand(pair.EntityPerStack.Target, 9311, entitySourceB, 1));
                pair.Compressed.BuffSystem.AddBuff(new AddBuffCommand(pair.Compressed.Target, 9311, pair.Compressed.Source, 2));
                pair.Compressed.BuffSystem.AddBuff(new AddBuffCommand(pair.Compressed.Target, 9311, compressedSourceB, 1));
                Tick(pair, 1);
                pair.EntityPerStack.BuffSystem.RemoveBuff(new RemoveBuffCommand(pair.EntityPerStack.Target, 9311, pair.EntityPerStack.Source, 1));
                pair.Compressed.BuffSystem.RemoveBuff(new RemoveBuffCommand(pair.Compressed.Target, 9311, pair.Compressed.Source, 1));
                Tick(pair, 2);
                AssertEqual(1, RequireView(pair.EntityPerStack, 9311, pair.EntityPerStack.Target, pair.EntityPerStack.Source).Stack, "Entity source A stack");
                AssertEqual(1, RequireView(pair.Compressed, 9311, pair.Compressed.Target, pair.Compressed.Source).Stack, "Compressed source A stack");
                AssertEqual(1, RequireView(pair.EntityPerStack, 9311, pair.EntityPerStack.Target, entitySourceB).Stack, "Entity source B stack");
                AssertEqual(1, RequireView(pair.Compressed, 9311, pair.Compressed.Target, compressedSourceB).Stack, "Compressed source B stack");
                return PairSummary(pair, 9311);
            });

            RunCompressedCase(report, "Storage_Compare_ClearAll_Matches", "ClearAll remove matches", pair =>
            {
                AddBothAndTick(pair, 9308, 3, 1);
                pair.EntityPerStack.BuffSystem.RemoveBuff(new RemoveBuffCommand(pair.EntityPerStack.Target, 9308, pair.EntityPerStack.Source, 1, false, true));
                pair.Compressed.BuffSystem.RemoveBuff(new RemoveBuffCommand(pair.Compressed.Target, 9308, pair.Compressed.Source, 1, false, true));
                Tick(pair, 2);
                AssertNotFound(pair, 9308);
                return PairSummary(pair, 9308);
            });
        }

        private static void RunRestoreHookTests(BuffSystemStorageTestReport report)
        {
            RunRestoreCase(report, "Storage_Compressed_RestoreHook_RebuildsQueryCache", "OnWorldRestored rebuilds query cache", false);
            RunRestoreCase(report, "Storage_Compressed_RestoreHook_ViewCacheNotStale", "OnWorldRestored clears stale view cache", true);
            RunRestoreCase(report, "Storage_EntityPerStack_RestoreHook_RebuildsLookup", "EntityPerStack OnWorldRestored keeps query visible", false, compressed: false);
        }

        private static void RunPerformanceSnapshots(BuffSystemStorageTestReport report)
        {
            RunPerformanceCase(report, "Storage_Perf_EntityPerStack_AddTickRemove_Snapshot", false);
            RunPerformanceCase(report, "Storage_Perf_Compressed_AddTickRemove_Snapshot", true);
            RunCase(report, PerformanceCategory, "Storage_Perf_Compare_Snapshot_Informational", "Performance snapshot exists; no threshold", "Mixed", () =>
            {
                if (CompressedFactoryMethod == null)
                    return Manual("Compressed factory unavailable; compare performance manually with BuffSystemStoragePerformanceRunner.");

                return Pass("Both storage performance snapshots are covered above", "No performance threshold asserted", "Informational", 1);
            });
        }

        private static void RunEligibilityCase(BuffSystemStorageTestReport report, string caseName, BuffDefinition definition, bool expected, string note)
        {
            RunCase(report, CompressedEligibilityCategory, caseName, note, "Reflection", () =>
            {
                if (CompressedEligibilityMethod == null)
                    return Manual("BuffSystemCore.IsCompressedParallelEligible was not found by reflection.");

                bool actual = InvokeCompressedEligibility(in definition);
                AssertEqual(expected ? 1 : 0, actual ? 1 : 0, "Eligibility");
                return Pass("Eligibility=" + expected, "Eligibility=" + actual + "; " + note, "DefinitionConfigId=" + definition.ConfigId, 2);
            });
        }

        private static void RunCompressedCase(
            BuffSystemStorageTestReport report,
            string caseName,
            string expected,
            Func<StoragePair, string> action,
            ParallelBuffStackUpPolicy stackUpPolicy = ParallelBuffStackUpPolicy.Append,
            int durationFrames = 10)
        {
            RunCase(report, CompareCategory, caseName, expected, "EntityPerStack vs CompressedExpiryFrameList", () =>
            {
                if (CompressedFactoryMethod == null)
                    return Manual("Compressed factory unavailable; run existing MonoBehaviour storage consistency runner manually.");

                StoragePair pair = CreatePair(GetConfigIdForCase(caseName), stackUpPolicy, ParallelBuffStackDownPolicy.RemoveEarliest, durationFrames);
                string actual = action(pair);
                return Pass(expected, actual, PairCounts(pair), 6);
            });
        }

        private static void RunRestoreCase(BuffSystemStorageTestReport report, string caseName, string expected, bool readBeforeRestore, bool compressed = true)
        {
            RunCase(report, RestoreHookCategory, caseName, expected, compressed ? "CompressedExpiryFrameList" : "EntityPerStack", () =>
            {
                if (WorldRestoredMethod == null)
                    return Manual("BuffSystemCore.OnWorldRestored is not discoverable from Editor assembly.");

                if (compressed && CompressedFactoryMethod == null)
                    return Manual("Compressed factory unavailable; restore-hook compressed cache test requires manual runner.");

                int configId = compressed ? 9301 : 9322;
                TestEnvironment env = CreateEnvironment(compressed, configId, compressed ? ParallelBuffStorageMode.CompressedExpiryFrameList : ParallelBuffStorageMode.EntityPerStack, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest, 3, 12, 1);
                AddAndTick(env, configId, 2, 1);
                if (readBeforeRestore)
                    Tick(env, 2);

                if (readBeforeRestore)
                    RequireView(env, configId);

                WorldRestoredMethod.Invoke(env.BuffSystem, new object[] { env.World });
                BuffViewData view = RequireView(env, configId);
                AssertEqual(2, view.Stack, "Stack after OnWorldRestored");
                return Pass("TryGetBuff remains visible after OnWorldRestored", ViewSummary(view), Counts(env, compressed ? 0 : 2, compressed ? 1 : 0), 4);
            });
        }

        private static void RunFailureClassificationSummary(BuffSystemStorageTestReport report)
        {
            const string visibilityEvidence = "Compressed runtime created during Tick frame 1 is not part of the tick-start runtime snapshot until the next Tick or OnWorldRestored capture; existing behavior runner queries at frame 2.";
            report.AddFailureClassification(
                "Storage_Compare_AddTickRemove_PublicViewsMatch",
                ClassificationTestFixtureWrong,
                visibilityEvidence,
                "Aligned query to frame 2 before asserting public views.");
            report.AddFailureClassification(
                "Storage_Compare_TryGetBuff_Matches",
                ClassificationTestFixtureWrong,
                visibilityEvidence,
                "Aligned TryGetBuff assertion to frame 2.");
            report.AddFailureClassification(
                "Storage_Compare_GetBuffsTarget_Matches",
                ClassificationTestFixtureWrong,
                visibilityEvidence,
                "Aligned GetBuffs assertion to frame 2.");
            report.AddFailureClassification(
                "Storage_Compare_Append_MaxStack_Matches",
                ClassificationTestFixtureWrong,
                visibilityEvidence,
                "Aligned MaxStack view assertion to frame 2.");
            report.AddFailureClassification(
                "Storage_Compare_ReplaceEarliestWhenFull_Matches",
                ClassificationTestFixtureWrong,
                "Previous fixture skipped the frame-2 public-view capture used by StorageBehaviorConsistencyRunner before the frame-4 replacement assertion.",
                "Aligned with the existing behavior runner tick sequence and kept strict RemainingFrames comparison.");
            report.AddFailureClassification(
                "Storage_Compressed_RestoreHook_ViewCacheNotStale",
                ClassificationTestFixtureWrong,
                "The stale-cache setup attempted to read compressed ViewData in the same frame as Add; the restore hook itself rebuilds runtime capture from ECS state.",
                "Warm the view cache at frame 2 before invoking OnWorldRestored.");
        }

        private static void RunReproCases(BuffSystemStorageTestReport report)
        {
            RunReproCase(report, "Storage_Repro_Compressed_AddTickRemove_9301", "Compressed 9301 same-frame visibility timeline", () =>
            {
                TestEnvironment env = CreateEnvironment(true, 9301, ParallelBuffStorageMode.CompressedExpiryFrameList, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest, 3, 10, 1);
                string timeline = BuildCompressedVisibilityTimeline(env, 9301, 1);
                string classification = ClassifyCompressedVisibilityTimeline(env, 9301);
                AssertEqual(1, env.BuffSystem.TryGetBuff(env.Target, 9301, env.Source, out BuffViewData _) ? 1 : 0, "Compressed 9301 frame-2 visibility");
                return ReproPass("ReproResult=Classified", classification, timeline, Snapshot(env, 9301, "final"), timeline, 4);
            });

            RunReproCase(report, "Storage_Repro_Compressed_Append_9304", "Compressed 9304 append MaxStack timeline", () =>
            {
                TestEnvironment env = CreateEnvironment(true, 9304, ParallelBuffStorageMode.CompressedExpiryFrameList, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest, 3, 10, 1);
                string timeline = BuildCompressedVisibilityTimeline(env, 9304, 5);
                string classification = ClassifyCompressedVisibilityTimeline(env, 9304);
                BuffViewData view = RequireView(env, 9304);
                AssertEqual(3, view.Stack, "Compressed 9304 MaxStack");
                return ReproPass("ReproResult=Classified", classification, timeline, Snapshot(env, 9304, "final"), timeline, 4);
            });

            RunReproCase(report, "Storage_Repro_Compressed_ReplaceRemaining_9308", "Compressed replacement RemainingFrames timeline", () =>
            {
                StoragePair pair = CreatePair(9308, ParallelBuffStackUpPolicy.ReplaceEarliestWhenFull, ParallelBuffStackDownPolicy.RemoveEarliest, 10);
                AddBothAndTick(pair, 9308, 3, 1);
                Tick(pair, 2);
                string frame2 = "frame2 Entity={" + Snapshot(pair.EntityPerStack, 9308, "entity") + "}; Compressed={" + Snapshot(pair.Compressed, 9308, "compressed") + "}";
                Tick(pair, 3);
                AddBothAndTick(pair, 9308, 1, 4);
                BuffViewData entityView = RequireView(pair.EntityPerStack, 9308);
                BuffViewData compressedView = RequireView(pair.Compressed, 9308);
                int delta = Math.Abs(entityView.RemainingFrames - compressedView.RemainingFrames);
                string classification = delta == 0 ? ClassificationTestFixtureWrong : ClassificationTimingToleranceIssue;
                AssertEqual(entityView.Stack, compressedView.Stack, "Replace repro Stack");
                AssertEqual(entityView.RemainingFrames, compressedView.RemainingFrames, "Replace repro RemainingFrames");
                string timeline = frame2 + "; frame4 Entity=" + ViewSummary(entityView) + "; Compressed=" + ViewSummary(compressedView);
                return ReproPass("ReproResult=StrictRemainingFramesMatch", classification, timeline, PairCounts(pair), timeline, 5);
            });

            RunReproCase(report, "Storage_Repro_Compressed_RestoreCache_9301", "Compressed restore cache timeline", () =>
            {
                if (WorldRestoredMethod == null)
                    return Manual("BuffSystemCore.OnWorldRestored is not discoverable from Editor assembly.");

                TestEnvironment env = CreateEnvironment(true, 9301, ParallelBuffStorageMode.CompressedExpiryFrameList, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest, 3, 12, 1);
                env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, 9301, env.Source, 2));
                string beforeTick = Snapshot(env, 9301, "beforeTick");
                Tick(env, 1);
                string frame1 = Snapshot(env, 9301, "frame1");
                Tick(env, 2);
                string frame2 = Snapshot(env, 9301, "frame2");
                WorldRestoredMethod.Invoke(env.BuffSystem, new object[] { env.World });
                string afterRestore = Snapshot(env, 9301, "afterRestore");
                BuffViewData view = RequireView(env, 9301);
                AssertEqual(2, view.Stack, "Restore repro Stack");
                string timeline = beforeTick + "; " + frame1 + "; " + frame2 + "; " + afterRestore;
                return ReproPass("ReproResult=RestoreRebuildsView", ClassificationTestFixtureWrong, timeline, Snapshot(env, 9301, "final"), timeline, 5);
            });
        }

        private static void RunPerformanceCase(BuffSystemStorageTestReport report, string caseName, bool compressed)
        {
            RunCase(report, PerformanceCategory, caseName, "Small Add / Tick / Remove timing snapshot", compressed ? "CompressedExpiryFrameList" : "EntityPerStack", () =>
            {
                if (compressed && CompressedFactoryMethod == null)
                    return Manual("Compressed factory unavailable; run BuffSystemStoragePerformanceRunner manually.");

                long beforeBytes = GetAllocatedBytes();
                Stopwatch stopwatch = Stopwatch.StartNew();
                int configId = compressed ? 9301 : 9331;
                TestEnvironment env = CreateEnvironment(compressed, configId, compressed ? ParallelBuffStorageMode.CompressedExpiryFrameList : ParallelBuffStorageMode.EntityPerStack, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest, 3, 24, 1);
                for (int i = 0; i < 64; i++)
                    env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, configId, env.Source, 3));

                for (int frame = 1; frame <= 30; frame++)
                    Tick(env, frame);

                env.BuffSystem.RemoveBuff(new RemoveBuffCommand(env.Target, configId, env.Source, 1, false, true));
                Tick(env, 31);
                stopwatch.Stop();
                long afterBytes = GetAllocatedBytes();

                return Pass("Performance sample completed", $"TotalMs={stopwatch.Elapsed.TotalMilliseconds:0.###}, GCBytes={Math.Max(0, afterBytes - beforeBytes)}", Counts(env, 0, 0), 1);
            });
        }

        private static void RunCase(BuffSystemStorageTestReport report, string category, string caseName, string expected, string storageMode, Func<CaseOutcome> action)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                CaseOutcome outcome = action();
                stopwatch.Stop();
                if (outcome.Status == BuffSystemStorageTestStatus.ManualRequired)
                {
                    report.Add(BuffSystemStorageTestCaseResult.ManualRequired(category, caseName, expected, outcome.Actual, outcome.Reason));
                    return;
                }

                if (outcome.Status == BuffSystemStorageTestStatus.Skipped)
                {
                    report.Add(BuffSystemStorageTestCaseResult.Skipped(category, caseName, expected, outcome.Actual, outcome.Reason));
                    return;
                }

                report.Add(BuffSystemStorageTestCaseResult.Passed(category, caseName, expected, outcome.Actual, storageMode, outcome.ExpectedCounts, outcome.ActualCounts, outcome.InvariantChecks, stopwatch.Elapsed.TotalMilliseconds));
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                report.Add(BuffSystemStorageTestCaseResult.Failed(category, caseName, expected, "Exception", storageMode, string.Empty, string.Empty, 0, stopwatch.Elapsed.TotalMilliseconds, exception));
            }
        }

        private static void RunReproCase(BuffSystemStorageTestReport report, string caseName, string expected, Func<CaseOutcome> action)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                if (CompressedFactoryMethod == null)
                {
                    CaseOutcome manual = Manual("Compressed factory unavailable; repro requires existing internal validation factory.");
                    report.Add(BuffSystemStorageTestCaseResult.ManualRequired(ReproCategory, caseName, expected, manual.Actual, manual.Reason));
                    report.AddReproCase(caseName, BuffSystemStorageTestStatus.ManualRequired, "FactoryUnavailable", manual.Reason);
                    return;
                }

                CaseOutcome outcome = action();
                stopwatch.Stop();
                if (outcome.Status == BuffSystemStorageTestStatus.ManualRequired)
                {
                    report.Add(BuffSystemStorageTestCaseResult.ManualRequired(ReproCategory, caseName, expected, outcome.Actual, outcome.Reason));
                    report.AddReproCase(caseName, BuffSystemStorageTestStatus.ManualRequired, "ManualRequired", outcome.Reason);
                    return;
                }

                string classification = ExtractField(outcome.Actual, "Classification=");
                string keyEvidence = ExtractField(outcome.Actual, "Timeline=");
                report.Add(BuffSystemStorageTestCaseResult.Passed(ReproCategory, caseName, expected, outcome.Actual, "CompressedExpiryFrameList", outcome.ExpectedCounts, outcome.ActualCounts, outcome.InvariantChecks, stopwatch.Elapsed.TotalMilliseconds)
                    .WithDiagnostics(classification, string.Empty, outcome.ActualCounts, keyEvidence, ExtractField(outcome.Actual, "ReproResult="), keyEvidence));
                report.AddReproCase(caseName, BuffSystemStorageTestStatus.Passed, classification, keyEvidence);
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                string classification = ClassifyException(exception);
                report.Add(BuffSystemStorageTestCaseResult.Failed(ReproCategory, caseName, expected, "Exception", "CompressedExpiryFrameList", string.Empty, string.Empty, 0, stopwatch.Elapsed.TotalMilliseconds, exception)
                    .WithDiagnostics(classification, string.Empty, string.Empty, string.Empty, "ReproResult=Failed", exception.Message));
                report.AddReproCase(caseName, BuffSystemStorageTestStatus.Failed, classification, exception.Message);
            }
        }

        private static CaseOutcome ReproPass(string reproResult, string classification, string timeline, string counts, string keyEvidence, int invariantChecks)
        {
            string actual = reproResult + "; Classification=" + classification + "; Timeline=" + timeline;
            return new CaseOutcome(BuffSystemStorageTestStatus.Passed, actual, counts, keyEvidence, invariantChecks, string.Empty);
        }

        private static string BuildCompressedVisibilityTimeline(TestEnvironment env, int configId, int stack)
        {
            string beforeAdd = Snapshot(env, configId, "beforeAdd");
            env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, configId, env.Source, stack));
            string afterAddBeforeTick = Snapshot(env, configId, "afterAddBeforeTick");
            Tick(env, 1);
            string afterTick1 = Snapshot(env, configId, "afterTick1");
            Tick(env, 2);
            string afterTick2 = Snapshot(env, configId, "afterTick2");
            return beforeAdd + "; " + afterAddBeforeTick + "; " + afterTick1 + "; " + afterTick2;
        }

        private static string ClassifyCompressedVisibilityTimeline(TestEnvironment env, int configId)
        {
            int compressedCount = CountCompressedRuntimeEntities(env.World);
            int entityCount = CountRuntimeEntities(env.World);
            bool found = env.BuffSystem.TryGetBuff(env.Target, configId, env.Source, out BuffViewData _);
            if (compressedCount <= 0)
                return ClassificationCompressedAddDidNotCreateRuntime;
            if (!found)
                return ClassificationCompressedCacheOrQueryMismatch;
            if (entityCount > 0)
                return ClassificationUnclassified;
            return ClassificationTestFixtureWrong;
        }

        private static string ClassifyException(Exception exception)
        {
            string message = exception != null ? exception.Message : string.Empty;
            if (message.Contains("Expected buff view not found") || message.Contains("visibility"))
                return ClassificationCompressedCacheOrQueryMismatch;
            if (message.Contains("RemainingFrames"))
                return ClassificationTimingToleranceIssue;
            return ClassificationUnclassified;
        }

        private static string Snapshot(TestEnvironment env, int configId, string label)
        {
            bool found = env.BuffSystem.TryGetBuff(env.Target, configId, env.Source, out BuffViewData view);
            int getBuffsCount = CountBuffsWithConfig(env.BuffSystem.GetBuffs(env.Target), configId);
            return label
                + ": ConfigId=" + configId
                + ", Target=" + env.Target
                + ", Source=" + env.Source
                + ", TryGet=" + found
                + ", View=" + (found ? ViewSummary(view) : "NotFound")
                + ", GetBuffsConfigCount=" + getBuffsCount
                + ", EntityRuntime=" + CountRuntimeEntities(env.World)
                + ", CompressedRuntime=" + CountCompressedRuntimeEntities(env.World)
                + ", FallbackDetected=" + (CountRuntimeEntities(env.World) > 0 && CountCompressedRuntimeEntities(env.World) == 0)
                + ", CompressedDetail={" + CompressedRuntimeSummary(env.World, configId) + "}";
        }

        private static string CompressedRuntimeSummary(World world, int configId)
        {
            List<Entity> entities = new List<Entity>();
            EntityQueryDescription query = world.Query().With<CompressedParallelBuffRuntimeComponent>().BuildDescription();
            world.FillQuery(query, entities, true);
            if (entities.Count == 0)
                return "None";

            List<string> parts = new List<string>();
            for (int i = 0; i < entities.Count; i++)
            {
                if (!world.TryGetComponent(entities[i], out CompressedParallelBuffRuntimeComponent runtime) || runtime.configId != configId)
                    continue;

                parts.Add("Entity=" + entities[i]
                    + ", Handle=" + runtime.compressedRuntimeHandle
                    + ", LayerCount=" + runtime.layerCount
                    + ", NextLayerId=" + runtime.nextLayerId
                    + ", Layers=" + LayerSummary(in runtime));
            }

            return parts.Count == 0 ? "NoMatchingConfig" : string.Join(" / ", parts);
        }

        private static string LayerSummary(in CompressedParallelBuffRuntimeComponent runtime)
        {
            if (runtime.layerCount <= 0)
                return "None";

            List<string> layers = new List<string>();
            int count = Math.Min(runtime.layerCount, CompressedParallelBuffLayerBuffer.Capacity);
            for (int i = 0; i < count; i++)
            {
                CompressedParallelBuffLayer layer = runtime.layers.Get(i);
                layers.Add("#" + i
                    + "(layerId=" + layer.layerId
                    + ", handle=" + layer.layerRuntimeHandle
                    + ", expire=" + layer.expireFrame
                    + ", elapsed=" + layer.elapsedFrames
                    + ", ticks=" + layer.ticks + ")");
            }

            return string.Join(",", layers);
        }

        private static string ExtractField(string value, string prefix)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(prefix))
                return string.Empty;

            int start = value.IndexOf(prefix, StringComparison.Ordinal);
            if (start < 0)
                return string.Empty;

            start += prefix.Length;
            int end = value.IndexOf("; ", start, StringComparison.Ordinal);
            if (end < 0)
                end = value.Length;

            return value.Substring(start, end - start);
        }

        private static TestEnvironment CreateEnvironment(bool compressedGate, int configId, ParallelBuffStorageMode storageMode, ParallelBuffStackUpPolicy stackUpPolicy, ParallelBuffStackDownPolicy stackDownPolicy, int maxStack, int durationFrames, int tickIntervalFrames)
        {
            World world = new World();
            Entity target = world.CreateEntity();
            Entity source = world.CreateEntity();
            BuffDefinitionRegistry definitions = new BuffDefinitionRegistry();
            BuffEffectRegistry effects = new BuffEffectRegistry();
            CountingEffect effect = new CountingEffect();
            BuffSystemCore buffSystem = compressedGate ? CreateCompressedCore(definitions, effects) : new BuffSystemCore(definitions, effects);
            BuffDefinition definition = CreateDefinition(configId, "StorageTest_" + configId, EffectId, maxStack, durationFrames, tickIntervalFrames, stackUpPolicy, stackDownPolicy, storageMode);
            definitions.Register(in definition);
            effects.Register(EffectId, effect);
            return new TestEnvironment(world, target, source, definitions, effects, effect, buffSystem);
        }

        private static StoragePair CreatePair(int configId, ParallelBuffStackUpPolicy stackUpPolicy, ParallelBuffStackDownPolicy stackDownPolicy, int durationFrames)
        {
            TestEnvironment entity = CreateEnvironment(false, configId, ParallelBuffStorageMode.EntityPerStack, stackUpPolicy, stackDownPolicy, 3, durationFrames, 1);
            TestEnvironment compressed = CreateEnvironment(true, configId, ParallelBuffStorageMode.CompressedExpiryFrameList, stackUpPolicy, stackDownPolicy, 3, durationFrames, 1);
            return new StoragePair(entity, compressed);
        }

        private static BuffSystemCore CreateCompressedCore(BuffDefinitionRegistry definitions, BuffEffectRegistry effects)
        {
            if (CompressedFactoryMethod == null)
                throw new InvalidOperationException("CreateForCompressedParallelValidation was not found.");

            return (BuffSystemCore)CompressedFactoryMethod.Invoke(null, new object[] { definitions, effects });
        }

        private static BuffDefinition CreateDefinition(
            int configId,
            string name,
            int effectId,
            int maxStack,
            int durationFrames,
            int tickIntervalFrames,
            ParallelBuffStackUpPolicy stackUpPolicy,
            ParallelBuffStackDownPolicy stackDownPolicy,
            ParallelBuffStorageMode storageMode,
            BuffTriggerType triggerType = BuffTriggerType.Tick,
            bool unlimited = false)
        {
            return new BuffDefinition(
                configId,
                name,
                0,
                maxStack,
                unlimited,
                false,
                durationFrames,
                tickIntervalFrames,
                0,
                triggerType,
                BuffInstanceType.parallel,
                NormalBuffStackPolicy.AddStackOnly,
                stackUpPolicy,
                stackDownPolicy,
                effectId,
                null,
                storageMode);
        }

        private static bool InvokeCompressedEligibility(in BuffDefinition definition)
        {
            object[] args = { definition };
            return (bool)CompressedEligibilityMethod.Invoke(null, args);
        }

        private static void AddAndTick(TestEnvironment env, int configId, int stack, int frameNumber)
        {
            env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, configId, env.Source, stack));
            Tick(env, frameNumber);
        }

        private static void AddBothAndTick(StoragePair pair, int configId, int stack, int frameNumber)
        {
            pair.EntityPerStack.BuffSystem.AddBuff(new AddBuffCommand(pair.EntityPerStack.Target, configId, pair.EntityPerStack.Source, stack));
            pair.Compressed.BuffSystem.AddBuff(new AddBuffCommand(pair.Compressed.Target, configId, pair.Compressed.Source, stack));
            Tick(pair, frameNumber);
        }

        private static void RemoveBothAndTick(StoragePair pair, int configId, int stackCount, int frameNumber)
        {
            pair.EntityPerStack.BuffSystem.RemoveBuff(new RemoveBuffCommand(pair.EntityPerStack.Target, configId, pair.EntityPerStack.Source, stackCount));
            pair.Compressed.BuffSystem.RemoveBuff(new RemoveBuffCommand(pair.Compressed.Target, configId, pair.Compressed.Source, stackCount));
            Tick(pair, frameNumber);
        }

        private static void Tick(StoragePair pair, int frameNumber)
        {
            Tick(pair.EntityPerStack, frameNumber);
            Tick(pair.Compressed, frameNumber);
        }

        private static void Tick(TestEnvironment env, int frameNumber)
        {
            env.BuffSystem.Tick(env.World, new SimulationContext(frameNumber, FixedTickLength, false));
        }

        private static BuffViewData RequireView(TestEnvironment env, int configId)
        {
            return RequireView(env, configId, env.Target, env.Source);
        }

        private static BuffViewData RequireView(TestEnvironment env, int configId, Entity target, Entity source)
        {
            if (!env.BuffSystem.TryGetBuff(target, configId, source, out BuffViewData view))
                throw new InvalidOperationException("Expected buff view not found. configId=" + configId);

            return view;
        }

        private static void AssertPublicViewsMatch(StoragePair pair, int configId, bool compareRemainingFrames)
        {
            AssertTryGetMatches(pair, configId, compareRemainingFrames);
            AssertGetBuffsMatches(pair, configId, compareRemainingFrames);
        }

        private static void AssertTryGetMatches(StoragePair pair, int configId, bool compareRemainingFrames)
        {
            bool entityFound = pair.EntityPerStack.BuffSystem.TryGetBuff(pair.EntityPerStack.Target, configId, pair.EntityPerStack.Source, out BuffViewData entityView);
            bool compressedFound = pair.Compressed.BuffSystem.TryGetBuff(pair.Compressed.Target, configId, pair.Compressed.Source, out BuffViewData compressedView);
            AssertEqual(entityFound ? 1 : 0, compressedFound ? 1 : 0, "TryGetBuff visibility");
            if (!entityFound || !compressedFound)
                return;

            AssertEqual(entityView.ConfigId, compressedView.ConfigId, "ConfigId");
            AssertEqual(entityView.Stack, compressedView.Stack, "Stack");
            if (compareRemainingFrames)
                AssertEqual(entityView.RemainingFrames, compressedView.RemainingFrames, "RemainingFrames");
        }

        private static void AssertGetBuffsMatches(StoragePair pair, int configId, bool compareRemainingFrames)
        {
            IReadOnlyList<BuffViewData> entityViews = pair.EntityPerStack.BuffSystem.GetBuffs(pair.EntityPerStack.Target);
            IReadOnlyList<BuffViewData> compressedViews = pair.Compressed.BuffSystem.GetBuffs(pair.Compressed.Target);
            AssertEqual(CountBuffsWithConfig(entityViews, configId), CountBuffsWithConfig(compressedViews, configId), "GetBuffs config count");
            bool entityFound = TryFindView(entityViews, configId, out BuffViewData entityView);
            bool compressedFound = TryFindView(compressedViews, configId, out BuffViewData compressedView);
            AssertEqual(entityFound ? 1 : 0, compressedFound ? 1 : 0, "GetBuffs visibility");
            if (!entityFound || !compressedFound)
                return;

            AssertEqual(entityView.Stack, compressedView.Stack, "GetBuffs Stack");
            if (compareRemainingFrames)
                AssertEqual(entityView.RemainingFrames, compressedView.RemainingFrames, "GetBuffs RemainingFrames");
        }

        private static void AssertNotFound(StoragePair pair, int configId)
        {
            AssertFalse(pair.EntityPerStack.BuffSystem.TryGetBuff(pair.EntityPerStack.Target, configId, pair.EntityPerStack.Source, out BuffViewData _), "EntityPerStack should not find buff");
            AssertFalse(pair.Compressed.BuffSystem.TryGetBuff(pair.Compressed.Target, configId, pair.Compressed.Source, out BuffViewData _), "Compressed should not find buff");
            AssertEqual(0, CountBuffsWithConfig(pair.EntityPerStack.BuffSystem.GetBuffs(pair.EntityPerStack.Target), configId), "Entity GetBuffs count");
            AssertEqual(0, CountBuffsWithConfig(pair.Compressed.BuffSystem.GetBuffs(pair.Compressed.Target), configId), "Compressed GetBuffs count");
        }

        private static bool TryFindView(IReadOnlyList<BuffViewData> views, int configId, out BuffViewData view)
        {
            if (views != null)
            {
                for (int i = 0; i < views.Count; i++)
                {
                    if (views[i].ConfigId == configId)
                    {
                        view = views[i];
                        return true;
                    }
                }
            }

            view = default;
            return false;
        }

        private static int CountBuffsWithConfig(IReadOnlyList<BuffViewData> views, int configId)
        {
            int count = 0;
            if (views == null)
                return count;

            for (int i = 0; i < views.Count; i++)
            {
                if (views[i].ConfigId == configId)
                    count++;
            }

            return count;
        }

        private static int CountRuntimeEntities(World world)
        {
            List<Entity> entities = new List<Entity>();
            EntityQueryDescription query = world.Query().With<BuffRuntimeComponent>().BuildDescription();
            world.FillQuery(query, entities, true);
            return entities.Count;
        }

        private static int CountCompressedRuntimeEntities(World world)
        {
            List<Entity> entities = new List<Entity>();
            EntityQueryDescription query = world.Query().With<CompressedParallelBuffRuntimeComponent>().BuildDescription();
            world.FillQuery(query, entities, true);
            return entities.Count;
        }

        private static string Counts(TestEnvironment env, int expectedEntity, int expectedCompressed)
        {
            return $"EntityPerStackRuntime={CountRuntimeEntities(env.World)} (expected {expectedEntity}), CompressedRuntime={CountCompressedRuntimeEntities(env.World)} (expected {expectedCompressed})";
        }

        private static string PairCounts(StoragePair pair)
        {
            return "Entity={" + Counts(pair.EntityPerStack, -1, -1) + "}; Compressed={" + Counts(pair.Compressed, -1, -1) + "}";
        }

        private static string PairSummary(StoragePair pair, int configId)
        {
            string entity = pair.EntityPerStack.BuffSystem.TryGetBuff(pair.EntityPerStack.Target, configId, pair.EntityPerStack.Source, out BuffViewData entityView)
                ? ViewSummary(entityView)
                : "NotFound";
            string compressed = pair.Compressed.BuffSystem.TryGetBuff(pair.Compressed.Target, configId, pair.Compressed.Source, out BuffViewData compressedView)
                ? ViewSummary(compressedView)
                : "NotFound";
            return "Entity=" + entity + "; Compressed=" + compressed + "; " + PairCounts(pair);
        }

        private static string ViewSummary(BuffViewData view)
        {
            return $"ConfigId={view.ConfigId}, Stack={view.Stack}, RemainingFrames={view.RemainingFrames}, RuntimeHandle={view.RuntimeHandle}";
        }

        private static int GetConfigIdForCase(string caseName)
        {
            if (caseName.Contains("RefreshAll"))
                return 9306;
            if (caseName.Contains("ReplaceEarliestWhenFull"))
                return 9314;
            if (caseName.Contains("Expire"))
                return 9310;
            if (caseName.Contains("RemoveBySource"))
                return 9311;
            if (caseName.Contains("ClearAll"))
                return 9308;
            if (caseName.Contains("MaxStack"))
                return 9304;
            if (caseName.Contains("GetBuffs"))
                return 9303;
            if (caseName.Contains("TryGetBuff"))
                return 9302;
            return 9301;
        }

        private static CaseOutcome Pass(string actual, string expectedCounts, string actualCounts, int invariantChecks)
        {
            return new CaseOutcome(BuffSystemStorageTestStatus.Passed, actual, expectedCounts, actualCounts, invariantChecks, string.Empty);
        }

        private static CaseOutcome Manual(string reason)
        {
            return new CaseOutcome(BuffSystemStorageTestStatus.ManualRequired, "Manual verification required", string.Empty, string.Empty, 0, reason);
        }

        private static void AssertEqual(int expected, int actual, string field)
        {
            if (expected != actual)
                throw new InvalidOperationException(field + " expected=" + expected + ", actual=" + actual);
        }

        private static void AssertTrue(bool value, string field)
        {
            if (!value)
                throw new InvalidOperationException(field + " expected=true, actual=false");
        }

        private static void AssertFalse(bool value, string field)
        {
            if (value)
                throw new InvalidOperationException(field + " expected=false, actual=true");
        }

        private static Type FindTypeByName(string name)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type[] types;
                try
                {
                    types = assemblies[i].GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                }

                if (types == null)
                    continue;

                for (int j = 0; j < types.Length; j++)
                {
                    Type type = types[j];
                    if (type == null)
                        continue;

                    if (type.Name == name || type.FullName == name)
                        return type;
                }
            }

            return null;
        }

        private static long GetAllocatedBytes()
        {
            MethodInfo method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Static | BindingFlags.Public);
            if (method == null)
                return 0;

            object value = method.Invoke(null, null);
            return value is long bytes ? bytes : 0;
        }

        private readonly struct CaseOutcome
        {
            public readonly string Status;
            public readonly string Actual;
            public readonly string ExpectedCounts;
            public readonly string ActualCounts;
            public readonly int InvariantChecks;
            public readonly string Reason;

            public CaseOutcome(string status, string actual, string expectedCounts, string actualCounts, int invariantChecks, string reason)
            {
                Status = status;
                Actual = actual;
                ExpectedCounts = expectedCounts;
                ActualCounts = actualCounts;
                InvariantChecks = invariantChecks;
                Reason = reason;
            }
        }

        private sealed class TestEnvironment
        {
            public readonly World World;
            public readonly Entity Target;
            public readonly Entity Source;
            public readonly BuffDefinitionRegistry Definitions;
            public readonly BuffEffectRegistry Effects;
            public readonly CountingEffect Effect;
            public readonly BuffSystemCore BuffSystem;

            public TestEnvironment(World world, Entity target, Entity source, BuffDefinitionRegistry definitions, BuffEffectRegistry effects, CountingEffect effect, BuffSystemCore buffSystem)
            {
                World = world;
                Target = target;
                Source = source;
                Definitions = definitions;
                Effects = effects;
                Effect = effect;
                BuffSystem = buffSystem;
            }
        }

        private readonly struct StoragePair
        {
            public readonly TestEnvironment EntityPerStack;
            public readonly TestEnvironment Compressed;

            public StoragePair(TestEnvironment entityPerStack, TestEnvironment compressed)
            {
                EntityPerStack = entityPerStack;
                Compressed = compressed;
            }
        }

        private sealed class CountingEffect : BuffEffectExecutorBase
        {
            public int ApplyCount;
            public int RefreshCount;
            public int StackChangedCount;
            public int TickCount;
            public int RemoveCount;

            public override void OnApply(in BuffEffectContext context)
            {
                ApplyCount++;
            }

            public override void OnRefresh(in BuffEffectContext context)
            {
                RefreshCount++;
            }

            public override void OnStackChanged(in BuffEffectContext context, int delta)
            {
                StackChangedCount++;
            }

            public override void OnTick(in BuffEffectContext context)
            {
                TickCount++;
            }

            public override void OnRemove(in BuffEffectContext context)
            {
                RemoveCount++;
            }
        }
    }
}
