using System;
using System.Collections.Generic;
using System.Text;
using ECSFrameWork;
using UnityEngine;

namespace BuffSystem
{
    /// <summary>
    /// Phase 3F 压缩并行 Buff 手动验证入口。
    /// </summary>
    public sealed class BuffSystemCompressedParallelValidationRunner : MonoBehaviour
    {
        private const float FixedTickLength = 0.02f;

        private const int CompressedAppendBuffId = 9301;
        private const int EventFallbackBuffId = 9302;
        private const int UnlimitedFallbackBuffId = 9303;
        private const int CapacityFallbackBuffId = 9304;
        private const int RefreshEarliestBuffId = 9305;
        private const int RefreshAllBuffId = 9306;
        private const int RemoveEarliestBuffId = 9307;
        private const int RemoveLatestBuffId = 9308;
        private const int ClearAllBuffId = 9309;
        private const int DurationOneBuffId = 9310;
        private const int DurationTwoBuffId = 9311;
        private const int ForeverBuffId = 9312;
        private const int ReplaceNotFullBuffId = 9313;
        private const int ReplaceFullBuffId = 9314;
        private const int CapacityBoundaryBuffId = 9315;

        private const int CompressedAppendEffectId = 9401;
        private const int EventFallbackEffectId = 9402;
        private const int UnlimitedFallbackEffectId = 9403;
        private const int CapacityFallbackEffectId = 9404;
        private const int RefreshEarliestEffectId = 9405;
        private const int RefreshAllEffectId = 9406;
        private const int RemoveEarliestEffectId = 9407;
        private const int RemoveLatestEffectId = 9408;
        private const int ClearAllEffectId = 9409;
        private const int DurationOneEffectId = 9410;
        private const int DurationTwoEffectId = 9411;
        private const int ForeverEffectId = 9412;
        private const int ReplaceNotFullEffectId = 9413;
        private const int ReplaceFullEffectId = 9414;
        private const int CapacityBoundaryEffectId = 9415;

        private const int EventFallbackEventId = 9501;
        private const int AppendDurationFrames = 10;

        [ContextMenu("Run Compressed Parallel Validation")]
        public void RunCompressedParallelValidation()
        {
            ValidationState state = new ValidationState();
            state.Log("========== BuffSystem Compressed Parallel Validation V1/V2/V3/V4 ==========");

            RunGateTrueAppendViewDataTest(state);
            RunEventTriggerFallbackTest(state);
            RunUnlimitedFallbackTest(state);
            RunCapacityFallbackTest(state);
            RunGateFalseFallbackTest(state);
            RunRefreshEarliestTest(state);
            RunRefreshAllTest(state);
            RunRemoveEarliestTest(state);
            RunRemoveLatestTest(state);
            RunClearAllTest(state);
            RunDurationOneTickExpireTest(state);
            RunDurationTwoTickExpireTest(state);
            RunForeverTickNoExpireTest(state);
            RunReplaceNotFullTest(state);
            RunReplaceFullTest(state);
            RunCapacityBoundaryTest(state);

            state.Log(state.HasFailure
                ? "========== Compressed Parallel Validation Result: FAIL =========="
                : "========== Compressed Parallel Validation Result: PASS ==========");

            Debug.Log(state.BuildOutput());
        }

        private static void RunGateTrueAppendViewDataTest(ValidationState state)
        {
            TestEnvironment env = CreateEnvironment(true);
            RegisterDefinition(env.Definitions, CreateParallelDefinition(
                CompressedAppendBuffId,
                "P3F4C_CompressedAppend",
                CompressedAppendEffectId,
                maxStack: 4,
                unlimited: false,
                triggerType: BuffTriggerType.Tick));

            env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, CompressedAppendBuffId, env.Source, 3));
            Tick(env, 1);

            int compressedCountF1 = CountCompressedRuntimeEntities(env.World);
            int entityPerStackCountF1 = CountRuntimeEntities(env.World);
            state.ExpectEqual("GateTrue Append creates compressed runtime", 1, compressedCountF1, 1);
            state.ExpectEqual("GateTrue Append does not create EntityPerStack runtime", 0, entityPerStackCountF1, 1);

            Tick(env, 2);

            if (!TryGetSingleCompressedRuntime(env.World, out CompressedParallelBuffRuntimeComponent runtime))
            {
                state.Fail("GateTrue Append compressed runtime readable", 2, "one compressed runtime", "not found");
                return;
            }

            int minLayerRuntimeHandle = GetMinLayerRuntimeHandle(in runtime);
            int expectedRemainingFrames = AppendDurationFrames - 1;
            bool tryGet = env.BuffSystem.TryGetBuff(env.Target, CompressedAppendBuffId, env.Source, out BuffViewData view);
            IReadOnlyList<BuffViewData> buffs = env.BuffSystem.GetBuffs(env.Target);

            state.ExpectTrue("GateTrue TryGetBuff sees compressed ViewData", tryGet, 2);
            state.ExpectEqual("GateTrue GetBuffs returns one compressed aggregate", 1, CountBuffsWithConfig(buffs, CompressedAppendBuffId), 2);
            state.ExpectEqual("Append ViewData.Stack == layerCount", runtime.layerCount, tryGet ? view.Stack : -1, 2);
            state.ExpectEqual("Append ViewData.RemainingFrames == min(expireFrame-currentFrame)", expectedRemainingFrames, tryGet ? view.RemainingFrames : -1, 2);
            state.ExpectEqual("Append ViewData.RuntimeHandle == min(layerRuntimeHandle)", minLayerRuntimeHandle, tryGet ? view.RuntimeHandle : -1, 2);
        }

        private static void RunEventTriggerFallbackTest(ValidationState state)
        {
            TestEnvironment env = CreateEnvironment(true);
            RegisterDefinition(env.Definitions, CreateParallelDefinition(
                EventFallbackBuffId,
                "P3F4C_EventFallback",
                EventFallbackEffectId,
                maxStack: 3,
                unlimited: false,
                triggerType: BuffTriggerType.EventTrigger,
                eventIds: new[] { EventFallbackEventId }));

            env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, EventFallbackBuffId, env.Source, 2));
            Tick(env, 1);

            state.ExpectEqual("EventTrigger fallback compressed count", 0, CountCompressedRuntimeEntities(env.World), 1);
            state.ExpectEqual("EventTrigger fallback EntityPerStack count", 2, CountRuntimeEntities(env.World), 1);
        }

        private static void RunUnlimitedFallbackTest(ValidationState state)
        {
            TestEnvironment env = CreateEnvironment(true);
            RegisterDefinition(env.Definitions, CreateParallelDefinition(
                UnlimitedFallbackBuffId,
                "P3F4C_UnlimitedFallback",
                UnlimitedFallbackEffectId,
                maxStack: 4,
                unlimited: true,
                triggerType: BuffTriggerType.Tick));

            env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, UnlimitedFallbackBuffId, env.Source, 2));
            Tick(env, 1);

            state.ExpectEqual("Unlimited fallback compressed count", 0, CountCompressedRuntimeEntities(env.World), 1);
            state.ExpectEqual("Unlimited fallback EntityPerStack count", 2, CountRuntimeEntities(env.World), 1);
        }

        private static void RunCapacityFallbackTest(ValidationState state)
        {
            TestEnvironment env = CreateEnvironment(true);
            RegisterDefinition(env.Definitions, CreateParallelDefinition(
                CapacityFallbackBuffId,
                "P3F4C_CapacityFallback",
                CapacityFallbackEffectId,
                maxStack: CompressedParallelBuffLayerBuffer.Capacity + 1,
                unlimited: false,
                triggerType: BuffTriggerType.Tick));

            env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, CapacityFallbackBuffId, env.Source, 2));
            Tick(env, 1);

            state.ExpectEqual("MaxStack>Capacity fallback compressed count", 0, CountCompressedRuntimeEntities(env.World), 1);
            state.ExpectEqual("MaxStack>Capacity fallback EntityPerStack count", 2, CountRuntimeEntities(env.World), 1);
        }

        private static void RunGateFalseFallbackTest(ValidationState state)
        {
            TestEnvironment env = CreateEnvironment(false);
            RegisterDefinition(env.Definitions, CreateParallelDefinition(
                CompressedAppendBuffId,
                "P3F4C_GateFalseFallback",
                CompressedAppendEffectId,
                maxStack: 4,
                unlimited: false,
                triggerType: BuffTriggerType.Tick));

            env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, CompressedAppendBuffId, env.Source, 2));
            Tick(env, 1);

            state.ExpectEqual("GateFalse compressed count", 0, CountCompressedRuntimeEntities(env.World), 1);
            state.ExpectEqual("GateFalse EntityPerStack count", 2, CountRuntimeEntities(env.World), 1);
        }

        private static void RunRefreshEarliestTest(ValidationState state)
        {
            TestEnvironment env = CreateEnvironment(true);
            RegisterDefinition(env.Definitions, CreateParallelDefinition(
                RefreshEarliestBuffId,
                "P3F4D_RefreshEarliest",
                RefreshEarliestEffectId,
                maxStack: 3,
                unlimited: false,
                triggerType: BuffTriggerType.Tick,
                stackUpPolicy: ParallelBuffStackUpPolicy.RefreshEarliest,
                tickIntervalFrames: 1));

            env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, RefreshEarliestBuffId, env.Source, 3));
            Tick(env, 1);
            Tick(env, 2);
            Tick(env, 3);

            if (!TryGetSingleCompressedRuntime(env.World, out CompressedParallelBuffRuntimeComponent before))
            {
                state.Fail("RefreshEarliest setup compressed runtime readable", 3, "one compressed runtime", "not found");
                return;
            }

            LayerSnapshot earliestBefore = GetLayerSnapshot(in before, 0);
            LayerSnapshot secondBefore = GetLayerSnapshot(in before, 1);
            LayerSnapshot latestBefore = GetLayerSnapshot(in before, 2);

            env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, RefreshEarliestBuffId, env.Source, 1));
            Tick(env, 4);

            if (!TryGetSingleCompressedRuntime(env.World, out CompressedParallelBuffRuntimeComponent after))
            {
                state.Fail("RefreshEarliest compressed runtime readable after refresh", 4, "one compressed runtime", "not found");
                return;
            }

            bool foundEarliest = TryFindLayerById(in after, earliestBefore.LayerId, out LayerSnapshot refreshed);
            bool foundSecond = TryFindLayerById(in after, secondBefore.LayerId, out LayerSnapshot secondAfter);
            bool foundLatest = TryFindLayerById(in after, latestBefore.LayerId, out LayerSnapshot latestAfter);

            state.ExpectEqual("RefreshEarliest layerCount unchanged", 3, after.layerCount, 4);
            state.ExpectTrue("RefreshEarliest keeps refreshed layerId", foundEarliest, 4);
            state.ExpectEqual("RefreshEarliest keeps layerRuntimeHandle", earliestBefore.RuntimeHandle, foundEarliest ? refreshed.RuntimeHandle : -1, 4);
            state.ExpectEqual("RefreshEarliest updates earliest expireFrame", 14, foundEarliest ? refreshed.ExpireFrame : -1, 4);
            state.ExpectEqual("RefreshEarliest resets then ticks elapsedFrames", 1, foundEarliest ? refreshed.ElapsedFrames : -1, 4);
            state.ExpectEqual("RefreshEarliest resets then ticks ticks", 1, foundEarliest ? refreshed.Ticks : -1, 4);
            state.ExpectTrue("RefreshEarliest keeps second layer expireFrame", foundSecond && secondAfter.ExpireFrame == secondBefore.ExpireFrame, 4);
            state.ExpectTrue("RefreshEarliest keeps latest layer expireFrame", foundLatest && latestAfter.ExpireFrame == latestBefore.ExpireFrame, 4);

            bool tryGet = env.BuffSystem.TryGetBuff(env.Target, RefreshEarliestBuffId, env.Source, out BuffViewData view);
            state.ExpectTrue("RefreshEarliest ViewData remains queryable", tryGet, 4);
            state.ExpectEqual("RefreshEarliest ViewData.Stack unchanged", 3, tryGet ? view.Stack : -1, 4);
            state.ExpectEqual("RefreshEarliest ViewData.RemainingFrames uses ViewData min remaining", 7, tryGet ? view.RemainingFrames : -1, 4);
        }

        private static void RunRefreshAllTest(ValidationState state)
        {
            TestEnvironment env = CreateEnvironment(true);
            RegisterDefinition(env.Definitions, CreateParallelDefinition(
                RefreshAllBuffId,
                "P3F4D_RefreshAll",
                RefreshAllEffectId,
                maxStack: 3,
                unlimited: false,
                triggerType: BuffTriggerType.Tick,
                stackUpPolicy: ParallelBuffStackUpPolicy.RefreshAll,
                tickIntervalFrames: 1));

            AddOneLayerPerFrame(env, RefreshAllBuffId, 1, 3);

            if (!TryGetSingleCompressedRuntime(env.World, out CompressedParallelBuffRuntimeComponent before))
            {
                state.Fail("RefreshAll setup compressed runtime readable", 3, "one compressed runtime", "not found");
                return;
            }

            LayerSnapshot firstBefore = GetLayerSnapshot(in before, 0);
            LayerSnapshot secondBefore = GetLayerSnapshot(in before, 1);
            LayerSnapshot thirdBefore = GetLayerSnapshot(in before, 2);

            env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, RefreshAllBuffId, env.Source, 1));
            Tick(env, 4);

            if (!TryGetSingleCompressedRuntime(env.World, out CompressedParallelBuffRuntimeComponent after))
            {
                state.Fail("RefreshAll compressed runtime readable after refresh", 4, "one compressed runtime", "not found");
                return;
            }

            bool firstOk = TryFindLayerById(in after, firstBefore.LayerId, out LayerSnapshot firstAfter)
                && firstAfter.RuntimeHandle == firstBefore.RuntimeHandle
                && firstAfter.ExpireFrame == 14
                && firstAfter.ElapsedFrames == 1
                && firstAfter.Ticks == 1;
            bool secondOk = TryFindLayerById(in after, secondBefore.LayerId, out LayerSnapshot secondAfter)
                && secondAfter.RuntimeHandle == secondBefore.RuntimeHandle
                && secondAfter.ExpireFrame == 14
                && secondAfter.ElapsedFrames == 1
                && secondAfter.Ticks == 1;
            bool thirdOk = TryFindLayerById(in after, thirdBefore.LayerId, out LayerSnapshot thirdAfter)
                && thirdAfter.RuntimeHandle == thirdBefore.RuntimeHandle
                && thirdAfter.ExpireFrame == 14
                && thirdAfter.ElapsedFrames == 1
                && thirdAfter.Ticks == 1;

            state.ExpectEqual("RefreshAll layerCount unchanged", 3, after.layerCount, 4);
            state.ExpectTrue("RefreshAll refreshes first layer and keeps identity", firstOk, 4);
            state.ExpectTrue("RefreshAll refreshes second layer and keeps identity", secondOk, 4);
            state.ExpectTrue("RefreshAll refreshes third layer and keeps identity", thirdOk, 4);

            bool tryGet = env.BuffSystem.TryGetBuff(env.Target, RefreshAllBuffId, env.Source, out BuffViewData view);
            state.ExpectTrue("RefreshAll ViewData remains queryable", tryGet, 4);
            state.ExpectEqual("RefreshAll ViewData.Stack unchanged", 3, tryGet ? view.Stack : -1, 4);
        }

        private static void RunRemoveEarliestTest(ValidationState state)
        {
            TestEnvironment env = CreateEnvironment(true);
            RegisterDefinition(env.Definitions, CreateParallelDefinition(
                RemoveEarliestBuffId,
                "P3F4D_RemoveEarliest",
                RemoveEarliestEffectId,
                maxStack: 3,
                unlimited: false,
                triggerType: BuffTriggerType.Tick,
                stackDownPolicy: ParallelBuffStackDownPolicy.RemoveEarliest));

            AddOneLayerPerFrame(env, RemoveEarliestBuffId, 1, 3);

            if (!TryGetSingleCompressedRuntime(env.World, out CompressedParallelBuffRuntimeComponent before))
            {
                state.Fail("RemoveEarliest setup compressed runtime readable", 3, "one compressed runtime", "not found");
                return;
            }

            int removedLayerId = GetLayerSnapshot(in before, 0).LayerId;
            env.BuffSystem.RemoveBuff(new RemoveBuffCommand(env.Target, RemoveEarliestBuffId, env.Source, 1));
            Tick(env, 4);

            ValidateSingleLayerRemove(state, env, RemoveEarliestBuffId, removedLayerId, "RemoveEarliest", 4);
        }

        private static void RunRemoveLatestTest(ValidationState state)
        {
            TestEnvironment env = CreateEnvironment(true);
            RegisterDefinition(env.Definitions, CreateParallelDefinition(
                RemoveLatestBuffId,
                "P3F4D_RemoveLatest",
                RemoveLatestEffectId,
                maxStack: 3,
                unlimited: false,
                triggerType: BuffTriggerType.Tick,
                stackDownPolicy: ParallelBuffStackDownPolicy.RemoveLatest));

            AddOneLayerPerFrame(env, RemoveLatestBuffId, 1, 3);

            if (!TryGetSingleCompressedRuntime(env.World, out CompressedParallelBuffRuntimeComponent before))
            {
                state.Fail("RemoveLatest setup compressed runtime readable", 3, "one compressed runtime", "not found");
                return;
            }

            int removedLayerId = GetLayerSnapshot(in before, before.layerCount - 1).LayerId;
            env.BuffSystem.RemoveBuff(new RemoveBuffCommand(env.Target, RemoveLatestBuffId, env.Source, 1));
            Tick(env, 4);

            ValidateSingleLayerRemove(state, env, RemoveLatestBuffId, removedLayerId, "RemoveLatest", 4);
        }

        private static void RunClearAllTest(ValidationState state)
        {
            TestEnvironment env = CreateEnvironment(true);
            RegisterDefinition(env.Definitions, CreateParallelDefinition(
                ClearAllBuffId,
                "P3F4D_ClearAll",
                ClearAllEffectId,
                maxStack: 3,
                unlimited: false,
                triggerType: BuffTriggerType.Tick));

            AddOneLayerPerFrame(env, ClearAllBuffId, 1, 3);
            env.BuffSystem.RemoveBuff(new RemoveBuffCommand(env.Target, ClearAllBuffId, env.Source, 1, false, true));
            Tick(env, 4);

            bool tryGet = env.BuffSystem.TryGetBuff(env.Target, ClearAllBuffId, env.Source, out BuffViewData _);
            IReadOnlyList<BuffViewData> buffs = env.BuffSystem.GetBuffs(env.Target);

            state.ExpectTrue("ClearAll hides compressed ViewData", !tryGet, 4);
            state.ExpectEqual("ClearAll GetBuffs no longer contains buff", 0, CountBuffsWithConfig(buffs, ClearAllBuffId), 4);
        }

        private static void RunDurationOneTickExpireTest(ValidationState state)
        {
            TestEnvironment env = CreateEnvironment(true, state);
            RegisterDefinition(env.Definitions, CreateParallelDefinition(
                DurationOneBuffId,
                "P3F4E_DurationOne",
                DurationOneEffectId,
                maxStack: 1,
                unlimited: false,
                triggerType: BuffTriggerType.Tick,
                tickIntervalFrames: 1,
                durationFrames: 1));

            env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, DurationOneBuffId, env.Source, 1));
            Tick(env, 1);

            state.ExpectTrue("Duration1 F1 OnApply", state.HasEffect(DurationOneBuffId, "Apply", 1), 1);

            if (!TryGetSingleCompressedRuntimeEntity(env.World, out Entity runtimeEntity, out CompressedParallelBuffRuntimeComponent runtime))
            {
                state.Fail("Duration1 runtime exists before expire", 1, "one compressed runtime", "not found");
                return;
            }

            int compressedRuntimeHandle = runtime.compressedRuntimeHandle;
            int layerRuntimeHandle = runtime.layers.Get(0).layerRuntimeHandle;
            Tick(env, 2);

            state.ExpectEqual("Duration1 F2 Tick remainingFrames", 1, state.GetFirstRemaining(DurationOneBuffId, "Tick", 2), 2);
            state.ExpectEqual("Duration1 F2 Remove remainingFrames", 0, state.GetFirstRemaining(DurationOneBuffId, "Remove", 2), 2);
            state.ExpectTrue("Duration1 F2 StackChanged before Tick", state.GetFirstOrder(DurationOneBuffId, "StackChanged", 2) < state.GetFirstOrder(DurationOneBuffId, "Tick", 2), 2);
            state.ExpectTrue("Duration1 F2 Tick before Remove", state.GetFirstOrder(DurationOneBuffId, "Tick", 2) < state.GetFirstOrder(DurationOneBuffId, "Remove", 2), 2);
            state.ExpectEqual("Duration1 emits one layer Remove only", 1, state.CountEffects(DurationOneBuffId, "Remove"), 2);
            state.ExpectEqual("Duration1 Remove uses layer runtimeHandle", layerRuntimeHandle, state.GetFirstRuntimeHandle(DurationOneBuffId, "Remove", 2), 2);
            state.ExpectTrue("Duration1 Remove does not use compressedRuntimeHandle as aggregate callback", state.GetFirstRuntimeHandle(DurationOneBuffId, "Remove", 2) != compressedRuntimeHandle, 2);
            state.ExpectTrue("Duration1 OnRemove pending query hidden", state.WasQueryHiddenOnRemove(DurationOneBuffId), 2);
            state.ExpectTrue("Duration1 runtime destroyed after pending remove", !env.World.IsAlive(runtimeEntity), 2);
            state.ExpectEqual("Duration1 compressed runtime query empty after destroy", 0, CountCompressedRuntimeEntities(env.World), 2);

            env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, DurationOneBuffId, env.Source, 1));
            Tick(env, 3);
            state.ExpectEqual("Duration1 lookup cleanup allows re-add", 1, CountCompressedRuntimeEntities(env.World), 3);
        }

        private static void RunDurationTwoTickExpireTest(ValidationState state)
        {
            TestEnvironment env = CreateEnvironment(true, state);
            RegisterDefinition(env.Definitions, CreateParallelDefinition(
                DurationTwoBuffId,
                "P3F4E_DurationTwo",
                DurationTwoEffectId,
                maxStack: 1,
                unlimited: false,
                triggerType: BuffTriggerType.Tick,
                tickIntervalFrames: 1,
                durationFrames: 2));

            env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, DurationTwoBuffId, env.Source, 1));
            Tick(env, 1);
            Tick(env, 2);

            state.ExpectEqual("Duration2 F2 Tick remainingFrames", 2, state.GetFirstRemaining(DurationTwoBuffId, "Tick", 2), 2);
            state.ExpectEqual("Duration2 no Remove at F2", 0, state.CountEffects(DurationTwoBuffId, "Remove"), 2);
            state.ExpectEqual("Duration2 runtime remains before expiry", 1, CountCompressedRuntimeEntities(env.World), 2);

            Tick(env, 3);

            state.ExpectEqual("Duration2 F3 Tick remainingFrames", 1, state.GetFirstRemaining(DurationTwoBuffId, "Tick", 3), 3);
            state.ExpectEqual("Duration2 F3 Remove remainingFrames", 0, state.GetFirstRemaining(DurationTwoBuffId, "Remove", 3), 3);
            state.ExpectTrue("Duration2 F3 Tick before Remove", state.GetFirstOrder(DurationTwoBuffId, "Tick", 3) < state.GetFirstOrder(DurationTwoBuffId, "Remove", 3), 3);
            state.ExpectTrue("Duration2 OnRemove pending query hidden", state.WasQueryHiddenOnRemove(DurationTwoBuffId), 3);
        }

        private static void RunForeverTickNoExpireTest(ValidationState state)
        {
            TestEnvironment env = CreateEnvironment(true, state);
            RegisterDefinition(env.Definitions, CreateParallelDefinition(
                ForeverBuffId,
                "P3F4E_Forever",
                ForeverEffectId,
                maxStack: 1,
                unlimited: false,
                triggerType: BuffTriggerType.Tick,
                tickIntervalFrames: 1,
                isForever: true,
                durationFrames: 0));

            env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, ForeverBuffId, env.Source, 1));
            Tick(env, 1);
            Tick(env, 2);

            bool tryGetF2 = env.BuffSystem.TryGetBuff(env.Target, ForeverBuffId, env.Source, out BuffViewData viewF2);
            state.ExpectTrue("Forever F2 ViewData queryable", tryGetF2, 2);
            state.ExpectEqual("Forever F2 ViewData RemainingFrames=-1", -1, tryGetF2 ? viewF2.RemainingFrames : 0, 2);
            state.ExpectEqual("Forever F2 Tick snapshot remainingFrames=0", 0, state.GetFirstRemaining(ForeverBuffId, "Tick", 2), 2);

            if (!TryGetSingleCompressedRuntime(env.World, out CompressedParallelBuffRuntimeComponent beforeQuery))
            {
                state.Fail("Forever runtime readable before repeated query", 2, "one compressed runtime", "not found");
                return;
            }

            env.BuffSystem.TryGetBuff(env.Target, ForeverBuffId, env.Source, out BuffViewData _);
            env.BuffSystem.GetBuffs(env.Target);

            if (!TryGetSingleCompressedRuntime(env.World, out CompressedParallelBuffRuntimeComponent afterQuery))
            {
                state.Fail("Forever runtime readable after repeated query", 2, "one compressed runtime", "not found");
                return;
            }

            state.ExpectEqual("Query does not mutate compressed layerCount", beforeQuery.layerCount, afterQuery.layerCount, 2);

            Tick(env, 3);

            state.ExpectEqual("Forever F3 Tick snapshot remainingFrames=0", 0, state.GetFirstRemaining(ForeverBuffId, "Tick", 3), 3);
            state.ExpectEqual("Forever never naturally removes", 0, state.CountEffects(ForeverBuffId, "Remove"), 3);
            state.ExpectEqual("Forever runtime remains alive", 1, CountCompressedRuntimeEntities(env.World), 3);
        }

        private static void RunReplaceNotFullTest(ValidationState state)
        {
            TestEnvironment env = CreateEnvironment(true, state);
            RegisterDefinition(env.Definitions, CreateParallelDefinition(
                ReplaceNotFullBuffId,
                "P3F4F_ReplaceNotFull",
                ReplaceNotFullEffectId,
                maxStack: 3,
                unlimited: false,
                triggerType: BuffTriggerType.Tick,
                stackUpPolicy: ParallelBuffStackUpPolicy.ReplaceEarliestWhenFull));

            env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, ReplaceNotFullBuffId, env.Source, 2));
            Tick(env, 1);

            if (!TryGetSingleCompressedRuntime(env.World, out CompressedParallelBuffRuntimeComponent before))
            {
                state.Fail("ReplaceNotFull setup compressed runtime readable", 1, "one compressed runtime", "not found");
                return;
            }

            LayerSnapshot firstBefore = GetLayerSnapshot(in before, 0);
            LayerSnapshot secondBefore = GetLayerSnapshot(in before, 1);

            env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, ReplaceNotFullBuffId, env.Source, 1));
            Tick(env, 2);

            if (!TryGetSingleCompressedRuntime(env.World, out CompressedParallelBuffRuntimeComponent after))
            {
                state.Fail("ReplaceNotFull compressed runtime readable after append", 2, "one compressed runtime", "not found");
                return;
            }

            bool firstStillExists = TryFindLayerById(in after, firstBefore.LayerId, out LayerSnapshot _);
            bool secondStillExists = TryFindLayerById(in after, secondBefore.LayerId, out LayerSnapshot _);

            state.ExpectEqual("ReplaceNotFull appends when below MaxStack", 3, after.layerCount, 2);
            state.ExpectTrue("ReplaceNotFull does not replace first layer", firstStillExists, 2);
            state.ExpectTrue("ReplaceNotFull does not replace second layer", secondStillExists, 2);
            state.ExpectEqual("ReplaceNotFull does not emit Remove", 0, state.CountEffects(ReplaceNotFullBuffId, "Remove"), 2);

            bool tryGet = env.BuffSystem.TryGetBuff(env.Target, ReplaceNotFullBuffId, env.Source, out BuffViewData view);
            state.ExpectTrue("ReplaceNotFull ViewData queryable", tryGet, 2);
            state.ExpectEqual("ReplaceNotFull ViewData.Stack increases", 3, tryGet ? view.Stack : -1, 2);
        }

        private static void RunReplaceFullTest(ValidationState state)
        {
            TestEnvironment env = CreateEnvironment(true, state);
            RegisterDefinition(env.Definitions, CreateParallelDefinition(
                ReplaceFullBuffId,
                "P3F4F_ReplaceFull",
                ReplaceFullEffectId,
                maxStack: 3,
                unlimited: false,
                triggerType: BuffTriggerType.Tick,
                stackUpPolicy: ParallelBuffStackUpPolicy.ReplaceEarliestWhenFull));

            env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, ReplaceFullBuffId, env.Source, 3));
            Tick(env, 1);

            if (!TryGetSingleCompressedRuntime(env.World, out CompressedParallelBuffRuntimeComponent before))
            {
                state.Fail("ReplaceFull setup compressed runtime readable", 1, "one compressed runtime", "not found");
                return;
            }

            LayerSnapshot replacedBefore = GetLayerSnapshot(in before, 0);
            LayerSnapshot secondBefore = GetLayerSnapshot(in before, 1);
            LayerSnapshot thirdBefore = GetLayerSnapshot(in before, 2);

            env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, ReplaceFullBuffId, env.Source, 1));
            Tick(env, 2);

            if (!TryGetSingleCompressedRuntime(env.World, out CompressedParallelBuffRuntimeComponent after))
            {
                state.Fail("ReplaceFull compressed runtime readable after replace", 2, "one compressed runtime", "not found");
                return;
            }

            bool replacedStillExists = TryFindLayerById(in after, replacedBefore.LayerId, out LayerSnapshot _);
            bool secondStillExists = TryFindLayerById(in after, secondBefore.LayerId, out LayerSnapshot secondAfter);
            bool thirdStillExists = TryFindLayerById(in after, thirdBefore.LayerId, out LayerSnapshot thirdAfter);
            LayerSnapshot newLayer = GetLayerSnapshot(in after, after.layerCount - 1);

            state.Log("[F2] ReplaceFull identity oldRemovedLayerId=" + replacedBefore.LayerId
                + ", oldRemovedHandle=" + replacedBefore.RuntimeHandle
                + ", newLayerId=" + newLayer.LayerId
                + ", newLayerHandle=" + newLayer.RuntimeHandle);

            state.ExpectEqual("ReplaceFull layerCount remains MaxStack", 3, after.layerCount, 2);
            state.ExpectTrue("ReplaceFull removes earliest layer identity", !replacedStillExists, 2);
            state.ExpectTrue("ReplaceFull keeps second layer identity", secondStillExists && secondAfter.RuntimeHandle == secondBefore.RuntimeHandle, 2);
            state.ExpectTrue("ReplaceFull keeps third layer identity", thirdStillExists && thirdAfter.RuntimeHandle == thirdBefore.RuntimeHandle, 2);
            state.ExpectTrue("ReplaceFull creates new layer identity", newLayer.LayerId != replacedBefore.LayerId && newLayer.RuntimeHandle != replacedBefore.RuntimeHandle, 2);
            state.ExpectEqual("ReplaceFull new layer expireFrame", 12, newLayer.ExpireFrame, 2);

            bool tryGet = env.BuffSystem.TryGetBuff(env.Target, ReplaceFullBuffId, env.Source, out BuffViewData view);
            IReadOnlyList<BuffViewData> buffs = env.BuffSystem.GetBuffs(env.Target);
            state.ExpectTrue("ReplaceFull ViewData queryable", tryGet, 2);
            state.ExpectEqual("ReplaceFull GetBuffs returns one aggregate", 1, CountBuffsWithConfig(buffs, ReplaceFullBuffId), 2);
            state.ExpectEqual("ReplaceFull ViewData.Stack remains MaxStack", 3, tryGet ? view.Stack : -1, 2);
            state.ExpectEqual("ReplaceFull ViewData.RemainingFrames uses min active layer", 9, tryGet ? view.RemainingFrames : -1, 2);
            state.ExpectEqual("ReplaceFull ViewData.RuntimeHandle uses min active handle", GetMinLayerRuntimeHandle(in after), tryGet ? view.RuntimeHandle : -1, 2);

            state.ExpectTrue("ReplaceFull emits Apply for new layer", state.HasEffect(ReplaceFullBuffId, "Apply", 2), 2);
            state.ExpectTrue("ReplaceFull emits Remove for replaced layer", state.HasEffect(ReplaceFullBuffId, "Remove", 2), 2);
            state.ExpectEqual("ReplaceFull emits one Remove callback", 1, state.CountEffects(ReplaceFullBuffId, "Remove"), 2);
            state.ExpectEqual("ReplaceFull Remove uses replaced layer runtimeHandle", replacedBefore.RuntimeHandle, state.GetFirstRuntimeHandle(ReplaceFullBuffId, "Remove", 2), 2);
        }

        private static void RunCapacityBoundaryTest(ValidationState state)
        {
            TestEnvironment env = CreateEnvironment(true);
            RegisterDefinition(env.Definitions, CreateParallelDefinition(
                CapacityBoundaryBuffId,
                "P3F4F_CapacityBoundary",
                CapacityBoundaryEffectId,
                maxStack: CompressedParallelBuffLayerBuffer.Capacity,
                unlimited: false,
                triggerType: BuffTriggerType.Tick));

            env.BuffSystem.AddBuff(new AddBuffCommand(
                env.Target,
                CapacityBoundaryBuffId,
                env.Source,
                CompressedParallelBuffLayerBuffer.Capacity));
            Tick(env, 1);
            Tick(env, 2);

            bool tryGet = env.BuffSystem.TryGetBuff(env.Target, CapacityBoundaryBuffId, env.Source, out BuffViewData view);

            state.ExpectEqual("CapacityBoundary compressed runtime count", 1, CountCompressedRuntimeEntities(env.World), 2);
            state.ExpectEqual("CapacityBoundary EntityPerStack count", 0, CountRuntimeEntities(env.World), 2);
            state.ExpectTrue("CapacityBoundary ViewData queryable", tryGet, 2);
            state.ExpectEqual("CapacityBoundary ViewData.Stack == Capacity", CompressedParallelBuffLayerBuffer.Capacity, tryGet ? view.Stack : -1, 2);
        }

        private static TestEnvironment CreateEnvironment(bool enableCompressedGate, ValidationState state = null)
        {
            World world = new World();
            Entity target = world.CreateEntity();
            Entity source = world.CreateEntity();
            BuffDefinitionRegistry definitionRegistry = new BuffDefinitionRegistry();
            BuffEffectRegistry effectRegistry = new BuffEffectRegistry();

            BuffSystemCore buffSystem = enableCompressedGate
                ? BuffSystemCore.CreateForCompressedParallelValidation(definitionRegistry, effectRegistry)
                : new BuffSystemCore(definitionRegistry, effectRegistry);

            RegisterEffects(effectRegistry, buffSystem, state, target, source);
            return new TestEnvironment(world, target, source, definitionRegistry, buffSystem);
        }

        private static BuffDefinition CreateParallelDefinition(
            int configId,
            string name,
            int effectId,
            int maxStack,
            bool unlimited,
            BuffTriggerType triggerType,
            int[] eventIds = null,
            ParallelBuffStackUpPolicy stackUpPolicy = ParallelBuffStackUpPolicy.Append,
            ParallelBuffStackDownPolicy stackDownPolicy = ParallelBuffStackDownPolicy.RemoveEarliest,
            int tickIntervalFrames = 0,
            bool isForever = false,
            int durationFrames = AppendDurationFrames)
        {
            return new BuffDefinition(
                configId,
                name,
                0,
                maxStack,
                unlimited,
                isForever,
                durationFrames,
                tickIntervalFrames,
                0,
                triggerType,
                BuffInstanceType.parallel,
                NormalBuffStackPolicy.AddStackOnly,
                stackUpPolicy,
                stackDownPolicy,
                effectId,
                eventIds,
                ParallelBuffStorageMode.CompressedExpiryFrameList);
        }

        private static void RegisterDefinition(BuffDefinitionRegistry registry, in BuffDefinition definition)
        {
            registry.Register(in definition);
        }

        private static void RegisterEffects(
            BuffEffectRegistry registry,
            BuffSystemCore buffSystem,
            ValidationState state,
            Entity target,
            Entity source)
        {
            registry.Register(CompressedAppendEffectId, new ValidationEffect());
            registry.Register(EventFallbackEffectId, new ValidationEffect());
            registry.Register(UnlimitedFallbackEffectId, new ValidationEffect());
            registry.Register(CapacityFallbackEffectId, new ValidationEffect());
            registry.Register(RefreshEarliestEffectId, new ValidationEffect());
            registry.Register(RefreshAllEffectId, new ValidationEffect());
            registry.Register(RemoveEarliestEffectId, new ValidationEffect());
            registry.Register(RemoveLatestEffectId, new ValidationEffect());
            registry.Register(ClearAllEffectId, new ValidationEffect());
            registry.Register(DurationOneEffectId, new RecordingValidationEffect(buffSystem, state, target, source));
            registry.Register(DurationTwoEffectId, new RecordingValidationEffect(buffSystem, state, target, source));
            registry.Register(ForeverEffectId, new RecordingValidationEffect(buffSystem, state, target, source));
            registry.Register(ReplaceNotFullEffectId, new RecordingValidationEffect(buffSystem, state, target, source));
            registry.Register(ReplaceFullEffectId, new RecordingValidationEffect(buffSystem, state, target, source));
            registry.Register(CapacityBoundaryEffectId, new ValidationEffect());
        }

        private static void Tick(TestEnvironment env, int frameNumber)
        {
            SimulationContext context = new SimulationContext(frameNumber, FixedTickLength, false);
            env.BuffSystem.Tick(env.World, context);
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

        private static bool TryGetSingleCompressedRuntime(World world, out CompressedParallelBuffRuntimeComponent runtime)
        {
            List<Entity> entities = new List<Entity>();
            EntityQueryDescription query = world.Query().With<CompressedParallelBuffRuntimeComponent>().BuildDescription();
            world.FillQuery(query, entities, true);

            if (entities.Count == 1 && world.TryGetComponent(entities[0], out runtime))
                return true;

            runtime = default;
            return false;
        }

        private static bool TryGetSingleCompressedRuntimeEntity(
            World world,
            out Entity runtimeEntity,
            out CompressedParallelBuffRuntimeComponent runtime)
        {
            List<Entity> entities = new List<Entity>();
            EntityQueryDescription query = world.Query().With<CompressedParallelBuffRuntimeComponent>().BuildDescription();
            world.FillQuery(query, entities, true);

            if (entities.Count == 1 && world.TryGetComponent(entities[0], out runtime))
            {
                runtimeEntity = entities[0];
                return true;
            }

            runtimeEntity = Entity.Invalid;
            runtime = default;
            return false;
        }

        private static int GetMinLayerRuntimeHandle(in CompressedParallelBuffRuntimeComponent runtime)
        {
            int minHandle = int.MaxValue;

            for (int i = 0; i < runtime.layerCount; i++)
            {
                CompressedParallelBuffLayer layer = runtime.layers.Get(i);
                minHandle = Math.Min(minHandle, layer.layerRuntimeHandle);
            }

            return minHandle == int.MaxValue ? -1 : minHandle;
        }

        private static int CountBuffsWithConfig(IReadOnlyList<BuffViewData> buffs, int configId)
        {
            int count = 0;

            for (int i = 0; i < buffs.Count; i++)
            {
                if (buffs[i].ConfigId == configId)
                    count++;
            }

            return count;
        }

        private static void AddOneLayerPerFrame(TestEnvironment env, int configId, int firstFrame, int layerCount)
        {
            for (int i = 0; i < layerCount; i++)
            {
                env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, configId, env.Source, 1));
                Tick(env, firstFrame + i);
            }
        }

        private static void ValidateSingleLayerRemove(
            ValidationState state,
            TestEnvironment env,
            int configId,
            int removedLayerId,
            string testName,
            int frameNumber)
        {
            if (!TryGetSingleCompressedRuntime(env.World, out CompressedParallelBuffRuntimeComponent runtime))
            {
                state.Fail(testName + " compressed runtime remains readable", frameNumber, "one compressed runtime", "not found");
                return;
            }

            bool removedLayerStillExists = TryFindLayerById(in runtime, removedLayerId, out LayerSnapshot _);
            bool tryGet = env.BuffSystem.TryGetBuff(env.Target, configId, env.Source, out BuffViewData view);

            state.ExpectEqual(testName + " ViewData.Stack decreases", 2, tryGet ? view.Stack : -1, frameNumber);
            state.ExpectTrue(testName + " removed expected layer", !removedLayerStillExists, frameNumber);
            state.ExpectTrue(testName + " remaining layers still queryable", tryGet, frameNumber);
            state.ExpectEqual(testName + " runtime layerCount decreases", 2, runtime.layerCount, frameNumber);
        }

        private static LayerSnapshot GetLayerSnapshot(in CompressedParallelBuffRuntimeComponent runtime, int index)
        {
            CompressedParallelBuffLayer layer = runtime.layers.Get(index);
            return new LayerSnapshot(layer.layerId, layer.expireFrame, layer.elapsedFrames, layer.ticks, layer.layerRuntimeHandle);
        }

        private static bool TryFindLayerById(in CompressedParallelBuffRuntimeComponent runtime, int layerId, out LayerSnapshot snapshot)
        {
            for (int i = 0; i < runtime.layerCount; i++)
            {
                CompressedParallelBuffLayer layer = runtime.layers.Get(i);

                if (layer.layerId != layerId)
                    continue;

                snapshot = new LayerSnapshot(layer.layerId, layer.expireFrame, layer.elapsedFrames, layer.ticks, layer.layerRuntimeHandle);
                return true;
            }

            snapshot = default;
            return false;
        }

        private readonly struct TestEnvironment
        {
            public readonly World World;
            public readonly Entity Target;
            public readonly Entity Source;
            public readonly BuffDefinitionRegistry Definitions;
            public readonly BuffSystemCore BuffSystem;

            public TestEnvironment(
                World world,
                Entity target,
                Entity source,
                BuffDefinitionRegistry definitions,
                BuffSystemCore buffSystem)
            {
                World = world;
                Target = target;
                Source = source;
                Definitions = definitions;
                BuffSystem = buffSystem;
            }
        }

        private sealed class ValidationEffect : BuffEffectExecutorBase
        {
        }

        private sealed class RecordingValidationEffect : BuffEffectExecutorBase
        {
            private readonly BuffSystemCore _buffSystem;
            private readonly ValidationState _state;
            private readonly Entity _target;
            private readonly Entity _source;

            public RecordingValidationEffect(BuffSystemCore buffSystem, ValidationState state, Entity target, Entity source)
            {
                _buffSystem = buffSystem;
                _state = state;
                _target = target;
                _source = source;
            }

            public override void OnApply(in BuffEffectContext context)
            {
                _state?.RecordEffect("Apply", in context, 0);
            }

            public override void OnStackChanged(in BuffEffectContext context, int delta)
            {
                _state?.RecordEffect("StackChanged", in context, delta);
            }

            public override void OnTick(in BuffEffectContext context)
            {
                _state?.RecordEffect("Tick", in context, 0);
            }

            public override void OnRemove(in BuffEffectContext context)
            {
                _state?.RecordEffect("Remove", in context, 0);

                if (_state == null || _buffSystem == null)
                    return;

                bool tryGet = _buffSystem.TryGetBuff(_target, context.Definition.ConfigId, _source, out BuffViewData _);
                bool getBuffsContains = CountBuffsWithConfig(_buffSystem.GetBuffs(_target), context.Definition.ConfigId) > 0;
                _state.RecordRemoveQuery(context.Definition.ConfigId, !tryGet && !getBuffsContains);
            }
        }

        private readonly struct LayerSnapshot
        {
            public readonly int LayerId;
            public readonly int ExpireFrame;
            public readonly int ElapsedFrames;
            public readonly int Ticks;
            public readonly int RuntimeHandle;

            public LayerSnapshot(int layerId, int expireFrame, int elapsedFrames, int ticks, int runtimeHandle)
            {
                LayerId = layerId;
                ExpireFrame = expireFrame;
                ElapsedFrames = elapsedFrames;
                Ticks = ticks;
                RuntimeHandle = runtimeHandle;
            }
        }

        private sealed class ValidationState
        {
            private readonly StringBuilder _builder = new StringBuilder();
            private readonly List<EffectRecord> _effectRecords = new List<EffectRecord>();
            private readonly Dictionary<int, bool> _removeQueryHiddenByConfigId = new Dictionary<int, bool>();
            private int _effectOrder;

            public bool HasFailure { get; private set; }

            public void Log(string message)
            {
                _builder.AppendLine(message);
            }

            public void ExpectTrue(string testName, bool actual, int frameNumber)
            {
                if (actual)
                {
                    _builder.Append("[F").Append(frameNumber).Append("] ").Append(testName).AppendLine(": PASS");
                    return;
                }

                Fail(testName, frameNumber, "true", "false");
            }

            public void ExpectEqual(string testName, int expected, int actual, int frameNumber)
            {
                if (expected == actual)
                {
                    _builder.Append("[F").Append(frameNumber).Append("] ").Append(testName).AppendLine(": PASS");
                    return;
                }

                Fail(testName, frameNumber, expected.ToString(), actual.ToString());
            }

            public void Fail(string testName, int frameNumber, string expected, string actual)
            {
                HasFailure = true;
                _builder.Append("[F").Append(frameNumber).Append("] ")
                    .Append(testName)
                    .Append(": FAIL expected=")
                    .Append(expected)
                    .Append(", actual=")
                    .AppendLine(actual);
            }

            public void RecordEffect(string phase, in BuffEffectContext context, int stackDelta)
            {
                _effectOrder++;
                _effectRecords.Add(new EffectRecord(
                    context.Definition.ConfigId,
                    phase,
                    context.SimulationContext.frameNumber,
                    context.Runtime.remainingFrames,
                    context.Runtime.runtimeHandle,
                    stackDelta,
                    _effectOrder));
            }

            public void RecordRemoveQuery(int configId, bool hidden)
            {
                _removeQueryHiddenByConfigId[configId] = hidden;
            }

            public bool WasQueryHiddenOnRemove(int configId)
            {
                return _removeQueryHiddenByConfigId.TryGetValue(configId, out bool hidden) && hidden;
            }

            public bool HasEffect(int configId, string phase, int frameNumber)
            {
                return GetFirstRecord(configId, phase, frameNumber, out EffectRecord _);
            }

            public int CountEffects(int configId, string phase)
            {
                int count = 0;

                for (int i = 0; i < _effectRecords.Count; i++)
                {
                    EffectRecord record = _effectRecords[i];

                    if (record.ConfigId == configId && record.Phase == phase)
                        count++;
                }

                return count;
            }

            public int GetFirstRemaining(int configId, string phase, int frameNumber)
            {
                return GetFirstRecord(configId, phase, frameNumber, out EffectRecord record)
                    ? record.RemainingFrames
                    : -1;
            }

            public int GetFirstRuntimeHandle(int configId, string phase, int frameNumber)
            {
                return GetFirstRecord(configId, phase, frameNumber, out EffectRecord record)
                    ? record.RuntimeHandle
                    : -1;
            }

            public int GetFirstOrder(int configId, string phase, int frameNumber)
            {
                return GetFirstRecord(configId, phase, frameNumber, out EffectRecord record)
                    ? record.Order
                    : int.MaxValue;
            }

            private bool GetFirstRecord(int configId, string phase, int frameNumber, out EffectRecord record)
            {
                for (int i = 0; i < _effectRecords.Count; i++)
                {
                    EffectRecord candidate = _effectRecords[i];

                    if (candidate.ConfigId == configId && candidate.Phase == phase && candidate.FrameNumber == frameNumber)
                    {
                        record = candidate;
                        return true;
                    }
                }

                record = default;
                return false;
            }

            public string BuildOutput()
            {
                return _builder.ToString();
            }
        }

        private readonly struct EffectRecord
        {
            public readonly int ConfigId;
            public readonly string Phase;
            public readonly int FrameNumber;
            public readonly int RemainingFrames;
            public readonly int RuntimeHandle;
            public readonly int StackDelta;
            public readonly int Order;

            public EffectRecord(
                int configId,
                string phase,
                int frameNumber,
                int remainingFrames,
                int runtimeHandle,
                int stackDelta,
                int order)
            {
                ConfigId = configId;
                Phase = phase;
                FrameNumber = frameNumber;
                RemainingFrames = remainingFrames;
                RuntimeHandle = runtimeHandle;
                StackDelta = stackDelta;
                Order = order;
            }
        }
    }
}
