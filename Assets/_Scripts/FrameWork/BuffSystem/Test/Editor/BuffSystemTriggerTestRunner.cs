using ECSFrameWork;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace BuffSystem.EditorTesting
{
    internal sealed class BuffSystemTriggerTestRunner
    {
        internal const string TriggerDiscoveryCategory = "Trigger Discovery";
        internal const string TriggerConfigCategory = "Trigger Config";
        internal const string TickTriggerCategory = "Tick Trigger Isolation";
        internal const string EventTriggerCategory = "EventTrigger Execution";
        internal const string TriggerContextCategory = "Trigger Context";
        internal const string LifecycleInterleavingCategory = "Lifecycle Interleaving";
        internal const string StorageEligibilityCategory = "Storage / Eligibility";
        internal const string BoundaryCategory = "Boundary";

        private const float FixedTickLength = 0.02f;
        private const int EffectId = 991201;
        private const int BaseConfigId = 991200;
        private const int ProbeEventId = 991210;
        private const int WrongEventId = 991211;
        private const string TriggerApiAvailable = "IBuffSystem.Raise<TEvent> + IBuffEventEffectExecutor<TEvent>";

        private static readonly MethodInfo CompressedFactoryMethod =
            typeof(BuffSystemCore).GetMethod("CreateForCompressedParallelValidation", BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly MethodInfo CompressedEligibilityMethod =
            typeof(BuffSystemCore).GetMethod("IsCompressedParallelEligible", BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly MethodInfo WorldRestoredMethod =
            typeof(BuffSystemCore).GetMethod("OnWorldRestored", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private BuffSystemTriggerCapabilitySnapshot _capabilities;

        public BuffSystemTriggerTestReport RunAll()
        {
            BuffSystemTriggerTestReport report = BuffSystemTriggerTestReport.Create();
            _capabilities = DiscoverCapabilities();
            report.ApplyCapabilities(_capabilities);

            RunDiscoveryTests(report);
            RunConfigTests(report);
            RunTickTriggerTests(report);
            RunEventTriggerTests(report);
            RunContextTests(report);
            RunLifecycleInterleavingTests(report);
            RunStorageEligibilityTests(report);
            RunBoundaryTests(report);

            report.WriteMarkdown();
            return report;
        }

        private void RunDiscoveryTests(BuffSystemTriggerTestReport report)
        {
            RunCase(report, TriggerDiscoveryCategory, "TriggerDiscovery_ConfigTriggerField_DetectedOrNotSupported", "BuffConfigData exposes BuffTriggerType and EventIds.", () =>
            {
                if (!_capabilities.HasConfigTriggerField)
                    return NotSupported("BuffConfigData.BuffTriggerType was not found.");

                return Pass("BuffConfigData.BuffTriggerType + EventIds found.", 2);
            });

            RunCase(report, TriggerDiscoveryCategory, "TriggerDiscovery_DefinitionTriggerField_DetectedOrNotSupported", "BuffDefinition exposes TriggerType / EventIds / CanRespondToEvent.", () =>
            {
                if (!_capabilities.HasDefinitionTriggerField)
                    return NotSupported("BuffDefinition TriggerType / EventIds were not found.");

                return Pass("BuffDefinition trigger fields found.", 3);
            });

            RunCase(report, TriggerDiscoveryCategory, "TriggerDiscovery_RuntimeEventTriggerApi_DetectedOrNotSupported", "IBuffSystem.Raise<TEvent> exists.", () =>
            {
                if (!_capabilities.HasRuntimeRaiseApi)
                    return NotSupported("IBuffSystem.Raise<TEvent> was not found.");

                return Pass("IBuffSystem.Raise<TEvent> found.", 1);
            });

            RunCase(report, TriggerDiscoveryCategory, "TriggerDiscovery_EffectEventCallback_DetectedOrNotSupported", "IBuffEventEffectExecutor<TEvent> exposes ShouldTrigger / OnEvent.", () =>
            {
                if (!_capabilities.HasEventEffectCallback)
                    return NotSupported("IBuffEventEffectExecutor<TEvent> callback contract was not found.");

                return Pass("IBuffEventEffectExecutor<TEvent> found.", 2);
            });

            RunCase(report, TriggerDiscoveryCategory, "TriggerDiscovery_ExistingTriggerRunners_DetectedOrNotSupported", "Existing smoke / performance event tests are discoverable.", () =>
            {
                if (!_capabilities.HasExistingTriggerRunner)
                    return NotSupported("Existing trigger runners were not found.");

                return Pass("Existing trigger smoke runners found.", _capabilities.ExistingRunnerCount);
            });
        }

        private void RunConfigTests(BuffSystemTriggerTestReport report)
        {
            RunCase(report, TriggerConfigCategory, "Trigger_Config_TickTrigger_CanBeStored", "BuffConfigData can store Tick trigger.", () =>
            {
                int checks = 0;
                BuffConfigData config = ScriptableObject.CreateInstance<BuffConfigData>();
                try
                {
                    config.BuffTriggerType = BuffTriggerType.Tick;
                    Assert(ref checks, config.BuffTriggerType == BuffTriggerType.Tick, "Tick trigger should be stored.");
                    return Pass("BuffTriggerType=Tick", checks, null, BuffTriggerType.Tick);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(config);
                }
            });

            RunCase(report, TriggerConfigCategory, "Trigger_Config_EventTrigger_CanBeStored", "BuffConfigData can store EventTrigger and EventIds.", () =>
            {
                int checks = 0;
                BuffConfigData config = ScriptableObject.CreateInstance<BuffConfigData>();
                try
                {
                    config.BuffTriggerType = BuffTriggerType.EventTrigger;
                    config.EventIds.Add(ProbeEventId);
                    Assert(ref checks, config.BuffTriggerType == BuffTriggerType.EventTrigger, "EventTrigger should be stored.");
                    Assert(ref checks, config.EventIds.Count == 1 && config.EventIds[0] == ProbeEventId, "EventIds should be stored.");
                    return Pass("BuffTriggerType=EventTrigger, EventIds=" + ProbeEventId, checks, null, BuffTriggerType.EventTrigger, ProbeEventId);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(config);
                }
            });

            RunCase(report, TriggerConfigCategory, "Trigger_Config_InvalidTrigger_HandledOrDocumented", "Invalid enum value should not respond to events.", () =>
            {
                int checks = 0;
                BuffDefinition definition = CreateDefinition(BaseConfigId + 1, (BuffTriggerType)999, BuffInstanceType.normal, 1, 4, 0, new[] { ProbeEventId });
                Assert(ref checks, !definition.CanRespondToEvent(ProbeEventId), "Invalid trigger must not respond to events.");
                return Pass("Invalid trigger stored as raw enum; CanRespondToEvent=false.", checks, null, (BuffTriggerType)999, ProbeEventId);
            });

            RunCase(report, TriggerConfigCategory, "Trigger_Config_CopyTo_PreservesTriggerFields", "CopyTo preserves trigger fields and EventIds.", () =>
            {
                int checks = 0;
                BuffConfigData source = ScriptableObject.CreateInstance<BuffConfigData>();
                BuffConfigData target = ScriptableObject.CreateInstance<BuffConfigData>();
                try
                {
                    source.BuffTriggerType = BuffTriggerType.EventTrigger;
                    source.EventIds.Add(ProbeEventId);
                    source.EventIds.Add(WrongEventId);
                    source.CopyTo(target);
                    Assert(ref checks, target.BuffTriggerType == BuffTriggerType.EventTrigger, "CopyTo should preserve BuffTriggerType.");
                    Assert(ref checks, target.EventIds.Count == 2, "CopyTo should preserve EventIds count.");
                    Assert(ref checks, target.EventIds[0] == ProbeEventId && target.EventIds[1] == WrongEventId, "CopyTo should preserve EventIds values.");
                    return Pass("Copied EventTrigger with two EventIds.", checks, null, BuffTriggerType.EventTrigger, ProbeEventId);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(source);
                    UnityEngine.Object.DestroyImmediate(target);
                }
            });

            RunCase(report, TriggerConfigCategory, "Trigger_Config_ToDefinition_PreservesOrDropsTriggerFieldsDocumented", "ToDefinition preserves EventTrigger fields.", () =>
            {
                int checks = 0;
                BuffConfigData config = ScriptableObject.CreateInstance<BuffConfigData>();
                try
                {
                    config.ID = BaseConfigId + 2;
                    config.Name = "Trigger_ToDefinition";
                    config.EffectId = EffectId;
                    config.BuffTriggerType = BuffTriggerType.EventTrigger;
                    config.BuffType = BuffInstanceType.normal;
                    config.Duration = 1f;
                    config.MaxStack = 1;
                    config.EventIds.Add(ProbeEventId);
                    BuffDefinition definition = config.ToDefinition(FixedTickLength);
                    Assert(ref checks, definition.TriggerType == BuffTriggerType.EventTrigger, "ToDefinition should preserve TriggerType.");
                    Assert(ref checks, definition.EventIds.Length == 1 && definition.EventIds[0] == ProbeEventId, "ToDefinition should preserve EventIds.");
                    Assert(ref checks, definition.TickIntervalFrames == 0, "EventTrigger TickIntervalFrames should be zero.");
                    return Pass("ToDefinition preserved EventTrigger and EventIds; TickIntervalFrames=0.", checks, null, BuffTriggerType.EventTrigger, ProbeEventId);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(config);
                }
            });
        }

        private void RunTickTriggerTests(BuffSystemTriggerTestReport report)
        {
            RunCase(report, TickTriggerCategory, "Trigger_TickOnly_TickInvokesOnTick", "Tick trigger should invoke OnTick on advanced frames.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                int configId = BaseConfigId + 10;
                RegisterDefinition(env, configId, BuffTriggerType.Tick, BuffInstanceType.normal, 1, 8, 1);
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                int beforeTick = env.Effect.TickCount;
                Tick(env, 2);
                AssertPositive(env, env.Effect.TickCount - beforeTick, "Tick trigger should invoke OnTick.");
                return Pass(env.Describe(), 1, env.Effect, BuffTriggerType.Tick);
            });

            RunCase(report, TickTriggerCategory, "Trigger_TickOnly_EventDoesNotInvokeOnTickOrEvent", "Tick trigger should ignore Raise event hot path.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                int configId = BaseConfigId + 11;
                RegisterDefinition(env, configId, BuffTriggerType.Tick, BuffInstanceType.normal, 1, 8, 1);
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                Raise(env, 2, ProbeEventId);
                int checks = 0;
                Assert(ref checks, env.Effect.EventCount == 0, "Tick trigger should not receive OnEvent.");
                return Pass(env.Describe(), checks, env.Effect, BuffTriggerType.Tick, ProbeEventId);
            });

            RunCase(report, TickTriggerCategory, "Trigger_TickOnly_RemoveStopsFutureTick", "Removed Tick buff should not tick again.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                int configId = BaseConfigId + 12;
                RegisterDefinition(env, configId, BuffTriggerType.Tick, BuffInstanceType.normal, 1, 8, 1);
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                RemoveAndTick(env, env.TargetA, env.SourceA, configId, 1, false, true, 2);
                int beforeTick = env.Effect.TickCount;
                Tick(env, 3);
                int checks = 0;
                Assert(ref checks, env.Effect.TickCount == beforeTick, "Removed tick buff should not receive future OnTick.");
                return Pass(env.Describe(), checks, env.Effect, BuffTriggerType.Tick);
            });

            RunCase(report, TickTriggerCategory, "Trigger_TickOnly_ExpireStopsFutureTick", "Expired Tick buff should not tick again.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                int configId = BaseConfigId + 13;
                RegisterDefinition(env, configId, BuffTriggerType.Tick, BuffInstanceType.normal, 1, 2, 1);
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                TickUntilMissing(env, env.TargetA, env.SourceA, configId, 2, 8);
                int beforeTick = env.Effect.TickCount;
                Tick(env, 10);
                int checks = 0;
                Assert(ref checks, env.Effect.TickCount == beforeTick, "Expired tick buff should not receive future OnTick.");
                return Pass(env.Describe(), checks, env.Effect, BuffTriggerType.Tick);
            });
        }

        private void RunEventTriggerTests(BuffSystemTriggerTestReport report)
        {
            RunCase(report, EventTriggerCategory, "Trigger_EventOnly_AddDoesNotImmediatelyInvokeEvent", "Add should not invoke OnEvent.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                int configId = BaseConfigId + 20;
                RegisterDefinition(env, configId, BuffTriggerType.EventTrigger, BuffInstanceType.normal, 1, 8, 0, new[] { ProbeEventId });
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                int checks = 0;
                Assert(ref checks, env.Effect.EventCount == 0, "Add should not invoke OnEvent.");
                return Pass(env.Describe(), checks, env.Effect, BuffTriggerType.EventTrigger, ProbeEventId);
            });

            RunCase(report, EventTriggerCategory, "Trigger_EventOnly_TickDoesNotInvokeEvent", "Tick should not invoke EventTrigger callback.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                int configId = BaseConfigId + 21;
                RegisterDefinition(env, configId, BuffTriggerType.EventTrigger, BuffInstanceType.normal, 1, 8, 0, new[] { ProbeEventId });
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                Tick(env, 2);
                Tick(env, 3);
                int checks = 0;
                Assert(ref checks, env.Effect.EventCount == 0, "Tick should not invoke OnEvent.");
                Assert(ref checks, env.Effect.TickCount == 0, "EventTrigger should not invoke OnTick.");
                return Pass(env.Describe(), checks, env.Effect, BuffTriggerType.EventTrigger, ProbeEventId);
            });

            RunCase(report, EventTriggerCategory, "Trigger_EventOnly_MatchingEvent_InvokesOnce", "Matching EventId should invoke OnEvent once.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                int configId = BaseConfigId + 22;
                RegisterDefinition(env, configId, BuffTriggerType.EventTrigger, BuffInstanceType.normal, 1, 8, 0, new[] { ProbeEventId });
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                Raise(env, 2, ProbeEventId);
                int checks = 0;
                Assert(ref checks, env.Effect.EventCount == 1, "Matching event should invoke once.");
                return Pass(env.Describe(), checks, env.Effect, BuffTriggerType.EventTrigger, ProbeEventId);
            });

            RunCase(report, EventTriggerCategory, "Trigger_EventOnly_WrongEvent_DoesNotInvoke", "Wrong EventId should not invoke OnEvent.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                int configId = BaseConfigId + 23;
                RegisterDefinition(env, configId, BuffTriggerType.EventTrigger, BuffInstanceType.normal, 1, 8, 0, new[] { ProbeEventId });
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                Raise(env, 2, WrongEventId);
                int checks = 0;
                Assert(ref checks, env.Effect.EventCount == 0, "Wrong event should not invoke.");
                return Pass(env.Describe(), checks, env.Effect, BuffTriggerType.EventTrigger, WrongEventId);
            });

            RunCase(report, EventTriggerCategory, "Trigger_EventOnly_RemoveStopsFutureEvent", "Removed EventTrigger buff should not receive future events.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                int configId = BaseConfigId + 24;
                RegisterDefinition(env, configId, BuffTriggerType.EventTrigger, BuffInstanceType.normal, 1, 8, 0, new[] { ProbeEventId });
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                RemoveAndTick(env, env.TargetA, env.SourceA, configId, 1, false, true, 2);
                Raise(env, 3, ProbeEventId);
                int checks = 0;
                Assert(ref checks, env.Effect.EventCount == 0, "Removed EventTrigger should not receive future OnEvent.");
                return Pass(env.Describe(), checks, env.Effect, BuffTriggerType.EventTrigger, ProbeEventId);
            });

            RunCase(report, EventTriggerCategory, "Trigger_EventOnly_ExpireStopsFutureEvent", "Expired EventTrigger buff should not receive future events.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                int configId = BaseConfigId + 25;
                RegisterDefinition(env, configId, BuffTriggerType.EventTrigger, BuffInstanceType.normal, 1, 2, 0, new[] { ProbeEventId });
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                TickUntilMissing(env, env.TargetA, env.SourceA, configId, 2, 8);
                Raise(env, 10, ProbeEventId);
                int checks = 0;
                Assert(ref checks, env.Effect.EventCount == 0, "Expired EventTrigger should not receive future OnEvent.");
                return Pass(env.Describe(), checks, env.Effect, BuffTriggerType.EventTrigger, ProbeEventId);
            });

            RunCase(report, EventTriggerCategory, "Trigger_EventOnly_MultipleStacks_EventCountMatchesStackOrDocumentedAggregate", "Multiple stacks should produce per-layer or documented aggregate event callbacks.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                int configId = BaseConfigId + 26;
                RegisterDefinition(env, configId, BuffTriggerType.EventTrigger, BuffInstanceType.parallel, 3, 8, 0, new[] { ProbeEventId });
                AddAndTick(env, env.TargetA, env.SourceA, configId, 3, 1);
                Raise(env, 2, ProbeEventId);
                int checks = 0;
                Assert(ref checks, env.Effect.EventCount == 3 || env.Effect.EventCount == 1, "Multiple stack event count should be per-layer or documented aggregate.");
                return Pass(env.Describe() + ", documentedEventCount=" + env.Effect.EventCount, checks, env.Effect, BuffTriggerType.EventTrigger, ProbeEventId);
            });
        }

        private void RunContextTests(BuffSystemTriggerTestReport report)
        {
            RunCase(report, TriggerContextCategory, "Trigger_Context_Event_TargetSourceDefinitionCorrect", "OnEvent context should contain target, source and definition.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                int configId = BaseConfigId + 30;
                RegisterDefinition(env, configId, BuffTriggerType.EventTrigger, BuffInstanceType.normal, 1, 8, 0, new[] { ProbeEventId });
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                Raise(env, 2, ProbeEventId);
                int checks = 0;
                Assert(ref checks, env.Effect.LastTarget.Equals(env.TargetA), "Context target mismatch.");
                Assert(ref checks, env.Effect.LastSource.Equals(env.SourceA), "Context source mismatch.");
                Assert(ref checks, env.Effect.LastConfigId == configId, "Context definition config mismatch.");
                return Pass(env.Describe(), checks, env.Effect, BuffTriggerType.EventTrigger, ProbeEventId);
            });

            RunCase(report, TriggerContextCategory, "Trigger_Context_Event_TriggerIdCorrect", "OnEvent should receive matching EventId.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                int configId = BaseConfigId + 31;
                RegisterDefinition(env, configId, BuffTriggerType.EventTrigger, BuffInstanceType.normal, 1, 8, 0, new[] { ProbeEventId });
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                Raise(env, 2, ProbeEventId);
                int checks = 0;
                Assert(ref checks, env.Effect.LastEventId == ProbeEventId, "EventId mismatch.");
                return Pass(env.Describe(), checks, env.Effect, BuffTriggerType.EventTrigger, ProbeEventId);
            });

            RunCase(report, TriggerContextCategory, "Trigger_Context_WrongSource_DoesNotLeak", "ShouldTrigger source filtering should prevent source leak.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                int configId = BaseConfigId + 32;
                RegisterDefinition(env, configId, BuffTriggerType.EventTrigger, BuffInstanceType.normal, 1, 8, 0, new[] { ProbeEventId });
                env.Effect.ExpectedSource = env.SourceA;
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                AddAndTick(env, env.TargetA, env.SourceB, configId, 1, 2);
                Raise(env, 3, ProbeEventId);
                int checks = 0;
                Assert(ref checks, env.Effect.EventCount == 1, "Only expected source should trigger.");
                Assert(ref checks, env.Effect.LastSource.Equals(env.SourceA), "Unexpected source leaked into OnEvent.");
                return Pass(env.Describe(), checks, env.Effect, BuffTriggerType.EventTrigger, ProbeEventId);
            });

            RunCase(report, TriggerContextCategory, "Trigger_Context_WrongTarget_DoesNotLeak", "ShouldTrigger target filtering should prevent target leak.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                int configId = BaseConfigId + 33;
                RegisterDefinition(env, configId, BuffTriggerType.EventTrigger, BuffInstanceType.normal, 1, 8, 0, new[] { ProbeEventId });
                env.Effect.ExpectedTarget = env.TargetA;
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                AddAndTick(env, env.TargetB, env.SourceA, configId, 1, 2);
                Raise(env, 3, ProbeEventId);
                int checks = 0;
                Assert(ref checks, env.Effect.EventCount == 1, "Only expected target should trigger.");
                Assert(ref checks, env.Effect.LastTarget.Equals(env.TargetA), "Unexpected target leaked into OnEvent.");
                return Pass(env.Describe(), checks, env.Effect, BuffTriggerType.EventTrigger, ProbeEventId);
            });
        }

        private void RunLifecycleInterleavingTests(BuffSystemTriggerTestReport report)
        {
            RunCase(report, LifecycleInterleavingCategory, "Trigger_Interleaving_AddEventRemove_NoDuplicateCallbacks", "Add -> Event -> Remove should not duplicate callbacks.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                int configId = BaseConfigId + 40;
                RegisterDefinition(env, configId, BuffTriggerType.EventTrigger, BuffInstanceType.normal, 1, 8, 0, new[] { ProbeEventId });
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                Raise(env, 2, ProbeEventId);
                RemoveAndTick(env, env.TargetA, env.SourceA, configId, 1, false, true, 3);
                Raise(env, 4, ProbeEventId);
                int checks = 0;
                Assert(ref checks, env.Effect.EventCount == 1, "Only first event should trigger.");
                Assert(ref checks, env.Effect.RemoveCount == 1, "Remove should be called once.");
                return Pass(env.Describe(), checks, env.Effect, BuffTriggerType.EventTrigger, ProbeEventId);
            });

            RunCase(report, LifecycleInterleavingCategory, "Trigger_Interleaving_AddEventExpire_NoDoubleRemove", "Expire after event should not double remove.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                int configId = BaseConfigId + 41;
                RegisterDefinition(env, configId, BuffTriggerType.EventTrigger, BuffInstanceType.normal, 1, 2, 0, new[] { ProbeEventId });
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                Raise(env, 2, ProbeEventId);
                TickUntilMissing(env, env.TargetA, env.SourceA, configId, 3, 8);
                Raise(env, 10, ProbeEventId);
                int checks = 0;
                Assert(ref checks, env.Effect.EventCount == 1, "Expired buff should not receive later event.");
                Assert(ref checks, env.Effect.RemoveCount <= 1, "Expire should not double OnRemove.");
                return Pass(env.Describe(), checks, env.Effect, BuffTriggerType.EventTrigger, ProbeEventId);
            });

            RunCase(report, LifecycleInterleavingCategory, "Trigger_Interleaving_RefreshThenEvent_ContextStable", "Refresh then event should keep context stable.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                int configId = BaseConfigId + 42;
                RegisterDefinition(env, configId, BuffTriggerType.EventTrigger, BuffInstanceType.normal, 3, 8, 0, new[] { ProbeEventId });
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 2);
                Raise(env, 3, ProbeEventId);
                int checks = 0;
                Assert(ref checks, env.Effect.EventCount == 1, "Refreshed normal buff should trigger once.");
                Assert(ref checks, env.Effect.LastConfigId == configId, "Refreshed context config mismatch.");
                return Pass(env.Describe(), checks, env.Effect, BuffTriggerType.EventTrigger, ProbeEventId);
            });

            RunCase(report, LifecycleInterleavingCategory, "Trigger_Interleaving_AppendThenEvent_CountDocumented", "Append then event should produce documented callback count.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                int configId = BaseConfigId + 43;
                RegisterDefinition(env, configId, BuffTriggerType.EventTrigger, BuffInstanceType.parallel, 3, 8, 0, new[] { ProbeEventId });
                AddAndTick(env, env.TargetA, env.SourceA, configId, 2, 1);
                Raise(env, 2, ProbeEventId);
                int checks = 0;
                Assert(ref checks, env.Effect.EventCount == 2 || env.Effect.EventCount == 1, "Append event count should be per-layer or documented aggregate.");
                return Pass(env.Describe() + ", documentedEventCount=" + env.Effect.EventCount, checks, env.Effect, BuffTriggerType.EventTrigger, ProbeEventId);
            });

            RunCase(report, LifecycleInterleavingCategory, "Trigger_Interleaving_ClearAllThenEvent_NoCallback", "ClearAll should stop future events.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                int configId = BaseConfigId + 44;
                RegisterDefinition(env, configId, BuffTriggerType.EventTrigger, BuffInstanceType.parallel, 3, 8, 0, new[] { ProbeEventId });
                AddAndTick(env, env.TargetA, env.SourceA, configId, 2, 1);
                RemoveAndTick(env, env.TargetA, env.SourceA, configId, 1, false, true, 2);
                Raise(env, 3, ProbeEventId);
                int checks = 0;
                Assert(ref checks, env.Effect.EventCount == 0, "ClearAll removed buff should not trigger.");
                return Pass(env.Describe(), checks, env.Effect, BuffTriggerType.EventTrigger, ProbeEventId);
            });
        }

        private void RunStorageEligibilityTests(BuffSystemTriggerTestReport report)
        {
            RunCase(report, StorageEligibilityCategory, "Trigger_Storage_EventTrigger_CompressedEligibilityFalse", "EventTrigger should not be compressed eligible.", () =>
            {
                if (CompressedEligibilityMethod == null)
                    return Manual("BuffSystemCore.IsCompressedParallelEligible was not found by reflection.");

                int checks = 0;
                BuffDefinition definition = CreateDefinition(BaseConfigId + 50, BuffTriggerType.EventTrigger, BuffInstanceType.parallel, 3, 8, 0, new[] { ProbeEventId }, ParallelBuffStorageMode.CompressedExpiryFrameList);
                bool eligible = InvokeCompressedEligibility(in definition);
                Assert(ref checks, !eligible, "EventTrigger compressed eligibility must be false.");
                return Pass("Eligibility=" + eligible, checks, null, BuffTriggerType.EventTrigger, ProbeEventId);
            });

            RunCase(report, StorageEligibilityCategory, "Trigger_Storage_EventTrigger_FallsBackToEntityPerStackOrDocumented", "EventTrigger with compressed storage request should fallback to EntityPerStack.", () =>
            {
                if (CompressedFactoryMethod == null)
                    return Manual("Compressed validation factory is not discoverable.");

                TestEnvironment env = CreateEnvironment(useCompressedFactory: true);
                int configId = 9301;
                RegisterDefinition(env, configId, BuffTriggerType.EventTrigger, BuffInstanceType.parallel, 3, 8, 0, new[] { ProbeEventId }, ParallelBuffStorageMode.CompressedExpiryFrameList);
                AddAndTick(env, env.TargetA, env.SourceA, configId, 2, 1);
                int entityCount = CountRuntimeEntities(env.World);
                int compressedCount = CountCompressedRuntimeEntities(env.World);
                int checks = 0;
                Assert(ref checks, entityCount > 0, "EventTrigger fallback should create EntityPerStack runtime.");
                Assert(ref checks, compressedCount == 0, "EventTrigger fallback should not create compressed runtime.");
                return Pass($"EntityPerStackRuntime={entityCount}, CompressedRuntime={compressedCount}", checks, env.Effect, BuffTriggerType.EventTrigger, ProbeEventId);
            });

            RunCase(report, StorageEligibilityCategory, "Trigger_Storage_TickTrigger_CompressedEligibilityIfValid", "Tick trigger valid compressed definition should be eligible.", () =>
            {
                if (CompressedEligibilityMethod == null)
                    return Manual("BuffSystemCore.IsCompressedParallelEligible was not found by reflection.");

                int checks = 0;
                BuffDefinition definition = CreateDefinition(BaseConfigId + 51, BuffTriggerType.Tick, BuffInstanceType.parallel, 3, 8, 1, null, ParallelBuffStorageMode.CompressedExpiryFrameList);
                bool eligible = InvokeCompressedEligibility(in definition);
                Assert(ref checks, eligible, "Tick compressed definition should be eligible.");
                return Pass("Eligibility=" + eligible, checks, null, BuffTriggerType.Tick);
            });
        }

        private void RunBoundaryTests(BuffSystemTriggerTestReport report)
        {
            RunCase(report, BoundaryCategory, "Trigger_Boundary_UnknownEvent_DoesNotTriggerOrDocumented", "Unknown EventId should not trigger.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                int configId = BaseConfigId + 60;
                RegisterDefinition(env, configId, BuffTriggerType.EventTrigger, BuffInstanceType.normal, 1, 8, 0, new[] { ProbeEventId });
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                Raise(env, 2, 1234567);
                int checks = 0;
                Assert(ref checks, env.Effect.EventCount == 0, "Unknown event should not trigger.");
                return Pass(env.Describe(), checks, env.Effect, BuffTriggerType.EventTrigger, 1234567);
            });

            RunCase(report, BoundaryCategory, "Trigger_Boundary_NullPayload_HandledOrDocumented", "IGameEvent payload is struct and cannot be null.", () =>
                NotSupported("IGameEvent is constrained to struct; null payload is not representable.", BuffTriggerType.EventTrigger, ProbeEventId));

            RunCase(report, BoundaryCategory, "Trigger_Boundary_ReentrantTrigger_DoesNotCorruptStateOrNotSupported", "Reentrant Raise should be documented or separately tested.", () =>
                NotSupported("No dedicated reentrant trigger contract exists in current public API; not exercised by this runner.", BuffTriggerType.EventTrigger, ProbeEventId));

            RunCase(report, BoundaryCategory, "Trigger_Boundary_TriggerAfterRemove_NoCallback", "Raise after remove should not callback.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                int configId = BaseConfigId + 61;
                RegisterDefinition(env, configId, BuffTriggerType.EventTrigger, BuffInstanceType.normal, 1, 8, 0, new[] { ProbeEventId });
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                RemoveAndTick(env, env.TargetA, env.SourceA, configId, 1, false, true, 2);
                Raise(env, 3, ProbeEventId);
                int checks = 0;
                Assert(ref checks, env.Effect.EventCount == 0, "Raise after remove should not callback.");
                return Pass(env.Describe(), checks, env.Effect, BuffTriggerType.EventTrigger, ProbeEventId);
            });

            RunCase(report, BoundaryCategory, "Trigger_Boundary_TriggerAfterWorldRestore_BehaviorDocumented", "OnWorldRestored should rebuild event lookup for restored ECS state.", () =>
            {
                if (WorldRestoredMethod == null)
                    return Manual("BuffSystemCore.OnWorldRestored was not found by reflection.");

                TestEnvironment env = CreateEnvironment();
                int configId = BaseConfigId + 62;
                RegisterDefinition(env, configId, BuffTriggerType.EventTrigger, BuffInstanceType.normal, 1, 8, 0, new[] { ProbeEventId });
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                WorldRestoredMethod.Invoke(env.BuffSystem, new object[] { env.World });
                Raise(env, 2, ProbeEventId);
                int checks = 0;
                Assert(ref checks, env.Effect.EventCount == 1, "Raise after OnWorldRestored should find restored runtime.");
                return Pass(env.Describe(), checks, env.Effect, BuffTriggerType.EventTrigger, ProbeEventId);
            });
        }

        private void RunCase(BuffSystemTriggerTestReport report, string category, string caseName, string expected, Func<TriggerCaseExecution> action)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                TriggerCaseExecution execution = action();
                stopwatch.Stop();
                report.Add(BuffSystemTriggerTestCaseResult.FromOutcome(category, caseName, execution.Status, expected, execution.Outcome, stopwatch.Elapsed.TotalMilliseconds, TriggerApiAvailable));
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                report.Add(BuffSystemTriggerTestCaseResult.Failed(category, caseName, expected, string.Empty, 0, stopwatch.Elapsed.TotalMilliseconds, exception, TriggerApiAvailable));
            }
        }

        private static TriggerCaseExecution Pass(string actual, int invariantChecks, CountingTriggerEffect effect = null, BuffTriggerType? triggerType = null, int eventId = 0)
        {
            return new TriggerCaseExecution(BuffSystemTriggerTestStatus.Passed, TriggerCaseOutcome.Pass(actual, invariantChecks, effect, triggerType, eventId));
        }

        private static TriggerCaseExecution NotSupported(string reason, BuffTriggerType? triggerType = null, int eventId = 0)
        {
            return new TriggerCaseExecution(BuffSystemTriggerTestStatus.NotSupported, TriggerCaseOutcome.NotSupported(reason, triggerType, eventId));
        }

        private static TriggerCaseExecution Manual(string reason, BuffTriggerType? triggerType = null, int eventId = 0)
        {
            return new TriggerCaseExecution(BuffSystemTriggerTestStatus.ManualRequired, TriggerCaseOutcome.ManualRequired(reason, triggerType, eventId));
        }

        private static TestEnvironment CreateEnvironment(bool useCompressedFactory = false)
        {
            World world = new World();
            Entity targetA = world.CreateEntity();
            Entity targetB = world.CreateEntity();
            Entity sourceA = world.CreateEntity();
            Entity sourceB = world.CreateEntity();
            BuffDefinitionRegistry definitions = new BuffDefinitionRegistry();
            BuffEffectRegistry effects = new BuffEffectRegistry();
            CountingTriggerEffect effect = new CountingTriggerEffect();
            effects.Register(EffectId, effect);
            BuffSystemCore buffSystem = useCompressedFactory
                ? (BuffSystemCore)CompressedFactoryMethod.Invoke(null, new object[] { definitions, effects })
                : new BuffSystemCore(definitions, effects);
            return new TestEnvironment(world, buffSystem, definitions, effects, effect, targetA, targetB, sourceA, sourceB);
        }

        private static void RegisterDefinition(
            TestEnvironment env,
            int configId,
            BuffTriggerType triggerType,
            BuffInstanceType buffType,
            int maxStack,
            int durationFrames,
            int tickIntervalFrames,
            int[] eventIds = null,
            ParallelBuffStorageMode storageMode = ParallelBuffStorageMode.EntityPerStack,
            ParallelBuffStackUpPolicy stackUpPolicy = ParallelBuffStackUpPolicy.Append,
            ParallelBuffStackDownPolicy stackDownPolicy = ParallelBuffStackDownPolicy.RemoveEarliest)
        {
            env.Definitions.Register(CreateDefinition(configId, triggerType, buffType, maxStack, durationFrames, tickIntervalFrames, eventIds, storageMode, stackUpPolicy, stackDownPolicy));
        }

        private static BuffDefinition CreateDefinition(
            int configId,
            BuffTriggerType triggerType,
            BuffInstanceType buffType,
            int maxStack,
            int durationFrames,
            int tickIntervalFrames,
            int[] eventIds = null,
            ParallelBuffStorageMode storageMode = ParallelBuffStorageMode.EntityPerStack,
            ParallelBuffStackUpPolicy stackUpPolicy = ParallelBuffStackUpPolicy.Append,
            ParallelBuffStackDownPolicy stackDownPolicy = ParallelBuffStackDownPolicy.RemoveEarliest)
        {
            return new BuffDefinition(
                configId,
                "TriggerTest_" + configId,
                0,
                maxStack,
                false,
                false,
                durationFrames,
                tickIntervalFrames,
                0,
                triggerType,
                buffType,
                NormalBuffStackPolicy.RefreshDuration,
                stackUpPolicy,
                stackDownPolicy,
                EffectId,
                eventIds,
                storageMode);
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

        private static void Tick(TestEnvironment env, int frameNumber)
        {
            env.BuffSystem.Tick(env.World, new SimulationContext(frameNumber, FixedTickLength, false));
        }

        private static void Raise(TestEnvironment env, int frameNumber, int eventId)
        {
            SimulationContext context = new SimulationContext(frameNumber, FixedTickLength, false);
            TriggerProbeEvent probeEvent = new TriggerProbeEvent(frameNumber, eventId);
            env.BuffSystem.Raise(env.World, context, in probeEvent);
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

        private static void Assert(ref int checks, bool condition, string message)
        {
            checks++;
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void AssertPositive(TestEnvironment env, int value, string message)
        {
            if (value <= 0)
                throw new InvalidOperationException(message + " " + env.Describe());
        }

        private static bool InvokeCompressedEligibility(in BuffDefinition definition)
        {
            object[] args = { definition };
            return (bool)CompressedEligibilityMethod.Invoke(null, args);
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

        private static BuffSystemTriggerCapabilitySnapshot DiscoverCapabilities()
        {
            BuffSystemTriggerCapabilitySnapshot snapshot = new BuffSystemTriggerCapabilitySnapshot();
            snapshot.HasConfigTriggerField =
                typeof(BuffConfigData).GetField("BuffTriggerType") != null &&
                typeof(BuffConfigData).GetField("EventIds") != null;
            snapshot.HasDefinitionTriggerField =
                typeof(BuffDefinition).GetField("TriggerType") != null &&
                typeof(BuffDefinition).GetField("EventIds") != null &&
                typeof(BuffDefinition).GetMethod("CanRespondToEvent") != null;
            snapshot.HasRuntimeRaiseApi = typeof(IBuffSystem).GetMethod("Raise") != null;
            snapshot.HasEventEffectCallback =
                typeof(IBuffEventEffectExecutor<TriggerProbeEvent>).GetMethod("ShouldTrigger") != null &&
                typeof(IBuffEventEffectExecutor<TriggerProbeEvent>).GetMethod("OnEvent") != null;

            string[] runnerNames =
            {
                "BuffSystemPhase2AValidationRunner",
                "BuffSystemRestoreHookValidationRunner",
                "BuffSystemStoragePerformanceRunner"
            };

            for (int i = 0; i < runnerNames.Length; i++)
            {
                Type type = FindTypeByName(runnerNames[i]);
                if (type != null)
                {
                    snapshot.ExistingRunnerCount++;
                    snapshot.Notes.Add("Found existing trigger runner: " + type.FullName);
                }
            }

            snapshot.HasExistingTriggerRunner = snapshot.ExistingRunnerCount > 0;
            snapshot.Notes.Add("ConfigTriggerField=" + snapshot.HasConfigTriggerField);
            snapshot.Notes.Add("DefinitionTriggerField=" + snapshot.HasDefinitionTriggerField);
            snapshot.Notes.Add("RuntimeRaiseApi=" + snapshot.HasRuntimeRaiseApi);
            snapshot.Notes.Add("EffectEventCallback=" + snapshot.HasEventEffectCallback);
            return snapshot;
        }

        private static Type FindTypeByName(string simpleName)
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

                    if (type.Name == simpleName || type.FullName == simpleName)
                        return type;
                }
            }

            return null;
        }

        private readonly struct TriggerCaseExecution
        {
            public readonly string Status;
            public readonly TriggerCaseOutcome Outcome;

            public TriggerCaseExecution(string status, TriggerCaseOutcome outcome)
            {
                Status = status;
                Outcome = outcome;
            }
        }

        private sealed class TestEnvironment
        {
            public readonly World World;
            public readonly BuffSystemCore BuffSystem;
            public readonly BuffDefinitionRegistry Definitions;
            public readonly BuffEffectRegistry Effects;
            public readonly CountingTriggerEffect Effect;
            public readonly Entity TargetA;
            public readonly Entity TargetB;
            public readonly Entity SourceA;
            public readonly Entity SourceB;

            public TestEnvironment(
                World world,
                BuffSystemCore buffSystem,
                BuffDefinitionRegistry definitions,
                BuffEffectRegistry effects,
                CountingTriggerEffect effect,
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
                return $"apply={Effect.ApplyCount}, tick={Effect.TickCount}, event={Effect.EventCount}, remove={Effect.RemoveCount}, refresh={Effect.RefreshCount}, stackChanged={Effect.StackChangedCount}, shouldTrigger={Effect.ShouldTriggerCount}, lastTarget={Effect.LastTarget}, lastSource={Effect.LastSource}, lastConfigId={Effect.LastConfigId}, lastEventId={Effect.LastEventId}";
            }
        }
    }

    internal sealed class BuffSystemTriggerCapabilitySnapshot
    {
        public bool HasConfigTriggerField;
        public bool HasDefinitionTriggerField;
        public bool HasRuntimeRaiseApi;
        public bool HasEventEffectCallback;
        public bool HasExistingTriggerRunner;
        public int ExistingRunnerCount;
        public readonly List<string> Notes = new List<string>();
    }

    internal sealed class CountingTriggerEffect : BuffEffectExecutorBase, IBuffEventEffectExecutor<TriggerProbeEvent>
    {
        public int ApplyCount;
        public int TickCount;
        public int EventCount;
        public int RemoveCount;
        public int RefreshCount;
        public int StackChangedCount;
        public int ShouldTriggerCount;
        public int LastConfigId;
        public int LastEventId;
        public Entity LastTarget;
        public Entity LastSource;
        public Entity ExpectedTarget = Entity.Invalid;
        public Entity ExpectedSource = Entity.Invalid;

        public override void OnApply(in BuffEffectContext context)
        {
            ApplyCount++;
            RecordContext(in context);
        }

        public override void OnTick(in BuffEffectContext context)
        {
            TickCount++;
            RecordContext(in context);
        }

        public override void OnRemove(in BuffEffectContext context)
        {
            RemoveCount++;
            RecordContext(in context);
        }

        public override void OnRefresh(in BuffEffectContext context)
        {
            RefreshCount++;
            RecordContext(in context);
        }

        public override void OnStackChanged(in BuffEffectContext context, int delta)
        {
            StackChangedCount++;
            RecordContext(in context);
        }

        public bool ShouldTrigger(in BuffEffectContext context, in TriggerProbeEvent gameEvent)
        {
            ShouldTriggerCount++;
            if (ExpectedTarget.IsValid && !context.Runtime.target.Equals(ExpectedTarget))
                return false;

            if (ExpectedSource.IsValid && !context.Runtime.source.Equals(ExpectedSource))
                return false;

            return context.Definition.CanRespondToEvent(gameEvent.EventId);
        }

        public void OnEvent(in BuffEffectContext context, in TriggerProbeEvent gameEvent)
        {
            EventCount++;
            LastEventId = gameEvent.EventId;
            RecordContext(in context);
        }

        private void RecordContext(in BuffEffectContext context)
        {
            LastTarget = context.Runtime.target;
            LastSource = context.Runtime.source;
            LastConfigId = context.Definition.ConfigId;
        }
    }

    internal readonly struct TriggerProbeEvent : IGameEvent
    {
        public int FrameNumber { get; }
        public int EventId { get; }

        public TriggerProbeEvent(int frameNumber, int eventId)
        {
            FrameNumber = frameNumber;
            EventId = eventId;
        }
    }
}
