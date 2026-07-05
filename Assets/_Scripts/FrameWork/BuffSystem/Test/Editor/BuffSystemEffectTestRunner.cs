using ECSFrameWork;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace BuffSystem.EditorTesting
{
    internal sealed class BuffSystemEffectTestRunner
    {
        internal const string DiscoveryCategory = "Discovery";
        internal const string RegistryCategory = "BuffEffectRegistry";
        internal const string SingleLifecycleCategory = "Single Effect Lifecycle";
        internal const string MissingInvalidCategory = "Missing / Invalid Effect";
        internal const string CompositeOrderCategory = "CompositeEffect Order";
        internal const string CompositeLifecycleCategory = "CompositeEffect Lifecycle";
        internal const string EventEffectCategory = "Event Effect";
        internal const string GraphStyleCategory = "Graph-generated Style";

        private const float FixedTickLength = 0.02f;
        private const int EffectId = 991301;
        private const int CompositeEffectId = 991302;
        private const int EventEffectId = 991303;
        private const int GraphStyleEffectId = 991304;
        private const int MissingEffectId = 991399;
        private const int BaseConfigId = 991300;
        private const int ProbeEventId = 991310;
        private const int WrongEventId = 991311;

        private BuffSystemEffectCapabilitySnapshot _capabilities;

        public BuffSystemEffectTestReport RunAll()
        {
            BuffSystemEffectTestReport report = BuffSystemEffectTestReport.Create();
            _capabilities = DiscoverCapabilities();
            report.ApplyCapabilities(_capabilities);

            RunDiscoveryTests(report);
            RunRegistryTests(report);
            RunSingleLifecycleTests(report);
            RunMissingInvalidTests(report);
            RunCompositeOrderTests(report);
            RunCompositeLifecycleTests(report);
            RunEventEffectTests(report);
            RunGraphStyleTests(report);

            report.WriteMarkdown();
            return report;
        }

        private void RunDiscoveryTests(BuffSystemEffectTestReport report)
        {
            RunCase(report, DiscoveryCategory, "EffectDiscovery_BuffEffectRegistry_Available", "BuffEffectRegistry type and core methods are discoverable.", () =>
            {
                if (!_capabilities.HasRegistry)
                    return NotSupported("BuffEffectRegistry was not found.");

                return Pass("Found: " + typeof(BuffEffectRegistry).FullName, 4);
            });

            RunCase(report, DiscoveryCategory, "EffectDiscovery_BuffEffectExecutorBase_Available", "BuffEffectExecutorBase is available.", () =>
            {
                if (!_capabilities.HasExecutorBase)
                    return NotSupported("BuffEffectExecutorBase was not found.");

                return Pass("Found: " + typeof(BuffEffectExecutorBase).FullName, 1);
            });

            RunCase(report, DiscoveryCategory, "EffectDiscovery_EventEffectInterface_Available", "IBuffEventEffectExecutor<TEvent> is available.", () =>
            {
                if (!_capabilities.HasEventEffectInterface)
                    return NotSupported("IBuffEventEffectExecutor<TEvent> was not found.");

                return Pass("Found: " + typeof(IBuffEventEffectExecutor<EffectProbeEvent>).FullName, 2);
            });

            RunCase(report, DiscoveryCategory, "EffectDiscovery_CompositeEffectPattern_DetectedOrDocumented", "CompositeEffect authoring pattern is detected or documented.", () =>
            {
                if (!_capabilities.HasCompositePattern)
                    return NotSupported("No CompositeEffect authoring pattern type was detected; behavior tests use test double only.");

                return Pass("Found: " + _capabilities.CompositePatternTypeName, 1);
            });

            RunCase(report, DiscoveryCategory, "EffectDiscovery_GeneratedGraphEffectPattern_DetectedOrDocumented", "Graph-generated style action pattern is detected or documented.", () =>
            {
                if (!_capabilities.HasGraphGeneratedPattern)
                    return NotSupported("No generated graph effect pattern was detected.");

                return Pass("Found: " + _capabilities.GraphGeneratedPatternTypeName, 1);
            });
        }

        private void RunRegistryTests(BuffSystemEffectTestReport report)
        {
            RunCase(report, RegistryCategory, "EffectRegistry_RegisterAndResolve_ReturnsExecutor", "Register then TryGet returns same executor.", () =>
            {
                int checks = 0;
                BuffEffectRegistry registry = new BuffEffectRegistry();
                CountingEffectExecutor effect = new CountingEffectExecutor();
                registry.Register(EffectId, effect);
                bool found = registry.TryGet(EffectId, out IBuffEffectExecutor resolved);
                Assert(ref checks, found, "TryGet should return true.");
                Assert(ref checks, ReferenceEquals(effect, resolved), "Resolved executor should be the registered instance.");
                return Pass("found=" + found + ", count=" + registry.Count, checks, EffectId, effect);
            });

            RunCase(report, RegistryCategory, "EffectRegistry_RegisterDuplicate_ReplaceOrDocumented", "Duplicate register replaces existing executor.", () =>
            {
                int checks = 0;
                BuffEffectRegistry registry = new BuffEffectRegistry();
                CountingEffectExecutor first = new CountingEffectExecutor();
                CountingEffectExecutor second = new CountingEffectExecutor();
                registry.Register(EffectId, first);
                registry.Register(EffectId, second);
                bool found = registry.TryGet(EffectId, out IBuffEffectExecutor resolved);
                Assert(ref checks, found, "TryGet should return true after duplicate register.");
                Assert(ref checks, ReferenceEquals(second, resolved), "Current registry semantics should replace duplicate effect id.");
                Assert(ref checks, registry.Count == 1, "Duplicate effect id should keep Count=1.");
                return Pass("duplicate replaced, count=" + registry.Count, checks, EffectId, second);
            });

            RunCase(report, RegistryCategory, "EffectRegistry_RemoveOrClear_IfSupported", "Remove and Clear are supported by BuffEffectRegistry.", () =>
            {
                int checks = 0;
                BuffEffectRegistry registry = new BuffEffectRegistry();
                registry.Register(EffectId, new CountingEffectExecutor());
                bool removed = registry.Remove(EffectId);
                bool foundAfterRemove = registry.TryGet(EffectId, out _);
                registry.Register(EffectId, new CountingEffectExecutor());
                registry.Clear();
                bool foundAfterClear = registry.TryGet(EffectId, out _);
                Assert(ref checks, removed, "Remove should return true for registered id.");
                Assert(ref checks, !foundAfterRemove, "Remove should delete effect.");
                Assert(ref checks, registry.Count == 0, "Clear should empty registry.");
                Assert(ref checks, !foundAfterClear, "TryGet after Clear should fail.");
                return Pass($"removed={removed}, foundAfterRemove={foundAfterRemove}, foundAfterClear={foundAfterClear}", checks, EffectId);
            });

            RunCase(report, RegistryCategory, "EffectRegistry_MissingEffectId_ReturnsNullOrDocumented", "Missing effect id does not resolve.", () =>
            {
                int checks = 0;
                BuffEffectRegistry registry = new BuffEffectRegistry();
                bool found = registry.TryGet(MissingEffectId, out IBuffEffectExecutor resolved);
                Assert(ref checks, !found, "Missing effect id should not resolve.");
                Assert(ref checks, resolved == null, "Missing effect should output null.");
                return Pass("found=false, resolved=null", checks, MissingEffectId);
            });

            RunCase(report, RegistryCategory, "EffectRegistry_TestRegistry_DoesNotAffectProductionBootstrap", "Local test registry stays isolated from production bootstrap.", () =>
            {
                int checks = 0;
                BuffEffectRegistry local = new BuffEffectRegistry();
                local.Register(EffectId, new CountingEffectExecutor());
                Assert(ref checks, local.Count == 1, "Local test registry should contain only local effect.");
                Assert(ref checks, FindTypeByName("BuffEffectRegistryBootstrap") != null, "Production bootstrap type should exist but is not invoked.");
                return Pass("Local registry only; bootstrap not invoked.", checks, EffectId);
            });
        }

        private void RunSingleLifecycleTests(BuffSystemEffectTestReport report)
        {
            RunCase(report, SingleLifecycleCategory, "Effect_Single_OnApply_CalledOnce", "Add + Tick calls OnApply once.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 10, EffectId, BuffTriggerType.Tick, BuffInstanceType.normal, 1, 8, 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 10, 1, 1);
                int checks = 0;
                Assert(ref checks, env.Effect.ApplyCount == 1, "OnApply should be called once.");
                return Pass(env.Describe(), checks, EffectId, env.Effect);
            });

            RunCase(report, SingleLifecycleCategory, "Effect_Single_OnTick_CalledExpectedTimes", "Tick trigger calls OnTick on advanced frames.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 11, EffectId, BuffTriggerType.Tick, BuffInstanceType.normal, 1, 8, 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 11, 1, 1);
                int before = env.Effect.TickCount;
                TickRange(env, 2, 4);
                int delta = env.Effect.TickCount - before;
                int checks = 0;
                Assert(ref checks, delta == 3, "TickInterval=1 should tick once per advanced frame.");
                return Pass("tickDelta=" + delta + ", " + env.Describe(), checks, EffectId, env.Effect);
            });

            RunCase(report, SingleLifecycleCategory, "Effect_Single_OnRemove_ManualRemove_CalledOnce", "Manual Remove calls OnRemove once.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 12, EffectId, BuffTriggerType.Tick, BuffInstanceType.normal, 1, 8, 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 12, 1, 1);
                RemoveAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 12, 1, false, true, 2);
                int checks = 0;
                Assert(ref checks, env.Effect.RemoveCount == 1, "Manual remove should call OnRemove once.");
                return Pass(env.Describe(), checks, EffectId, env.Effect);
            });

            RunCase(report, SingleLifecycleCategory, "Effect_Single_OnRemove_Expire_CalledOnce", "Expire calls OnRemove once.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 13, EffectId, BuffTriggerType.Tick, BuffInstanceType.normal, 1, 2, 1);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 13, 1, 1);
                bool removed = TickUntilMissing(env, env.TargetA, env.SourceA, BaseConfigId + 13, 2, 8);
                int checks = 0;
                Assert(ref checks, removed, "Buff should expire within bounded ticks.");
                Assert(ref checks, env.Effect.RemoveCount == 1, "Expire should call OnRemove once.");
                return Pass("removed=" + removed + ", " + env.Describe(), checks, EffectId, env.Effect);
            });

            RunCase(report, SingleLifecycleCategory, "Effect_Single_OnRefresh_CalledOnRefreshAll", "RefreshAll should call OnRefresh for existing layers.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 14, EffectId, BuffTriggerType.Tick, BuffInstanceType.parallel, 3, 8, 1, null, ParallelBuffStackUpPolicy.RefreshAll);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 14, 2, 1);
                int before = env.Effect.RefreshCount;
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 14, 1, 2);
                int delta = env.Effect.RefreshCount - before;
                int checks = 0;
                Assert(ref checks, delta > 0, "RefreshAll should refresh existing layers.");
                return Pass("refreshDelta=" + delta + ", " + env.Describe(), checks, EffectId, env.Effect);
            });

            RunCase(report, SingleLifecycleCategory, "Effect_Single_OnStackChanged_CalledWithExpectedDelta", "Append should call OnStackChanged.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                RegisterDefinition(env, BaseConfigId + 15, EffectId, BuffTriggerType.Tick, BuffInstanceType.parallel, 3, 8, 1, null, ParallelBuffStackUpPolicy.Append);
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 15, 1, 1);
                int before = env.Effect.StackChangedCount;
                AddAndTick(env, env.TargetA, env.SourceA, BaseConfigId + 15, 1, 2);
                int delta = env.Effect.StackChangedCount - before;
                int checks = 0;
                Assert(ref checks, delta > 0, "Stack append should call OnStackChanged.");
                Assert(ref checks, env.Effect.LastStackDelta > 0, "LastStackDelta should be positive for append.");
                return Pass("stackChangedDelta=" + delta + ", " + env.Describe(), checks, EffectId, env.Effect);
            });

            RunCase(report, SingleLifecycleCategory, "Effect_Single_Context_TargetSourceDefinitionCorrect", "Effect context carries target/source/definition.", () =>
            {
                TestEnvironment env = CreateEnvironment();
                int configId = BaseConfigId + 16;
                RegisterDefinition(env, configId, EffectId, BuffTriggerType.Tick, BuffInstanceType.normal, 1, 8, 1);
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                int checks = 0;
                Assert(ref checks, env.Effect.LastTarget.Equals(env.TargetA), "Context target should match.");
                Assert(ref checks, env.Effect.LastSource.Equals(env.SourceA), "Context source should match.");
                Assert(ref checks, env.Effect.LastConfigId == configId, "Context definition ConfigId should match.");
                return Pass(env.Describe(), checks, EffectId, env.Effect);
            });
        }

        private void RunMissingInvalidTests(BuffSystemEffectTestReport report)
        {
            RunCase(report, MissingInvalidCategory, "Effect_MissingEffectId_AddBuff_DoesNotCrashOrReportsError", "Missing effect id AddBuff does not crash.", () =>
            {
                TestEnvironment env = CreateEnvironment(null, 0);
                int configId = BaseConfigId + 20;
                RegisterDefinition(env, configId, MissingEffectId, BuffTriggerType.Tick, BuffInstanceType.normal, 1, 8, 1);
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                bool found = env.BuffSystem.TryGetBuff(env.TargetA, configId, env.SourceA, out _);
                return Pass("no exception, publicViewFound=" + found, 1, MissingEffectId);
            });

            RunCase(report, MissingInvalidCategory, "Effect_MissingEffectId_Tick_DoesNotCrashOrReportsError", "Missing effect id Tick does not crash.", () =>
            {
                TestEnvironment env = CreateEnvironment(null, 0);
                int configId = BaseConfigId + 21;
                RegisterDefinition(env, configId, MissingEffectId, BuffTriggerType.Tick, BuffInstanceType.normal, 1, 8, 1);
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                Tick(env, 2);
                bool found = env.BuffSystem.TryGetBuff(env.TargetA, configId, env.SourceA, out _);
                return Pass("no exception, publicViewFound=" + found, 1, MissingEffectId);
            });

            RunCase(report, MissingInvalidCategory, "Effect_MissingEffectId_Remove_DoesNotCrashOrReportsError", "Missing effect id Remove does not crash.", () =>
            {
                TestEnvironment env = CreateEnvironment(null, 0);
                int configId = BaseConfigId + 22;
                RegisterDefinition(env, configId, MissingEffectId, BuffTriggerType.Tick, BuffInstanceType.normal, 1, 8, 1);
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                RemoveAndTick(env, env.TargetA, env.SourceA, configId, 1, false, true, 2);
                return Pass("no exception on remove with missing effect", 1, MissingEffectId);
            });

            RunCase(report, MissingInvalidCategory, "Effect_InvalidEffectId_NegativeOrZero_HandledOrDocumented", "Zero or negative effect id is handled without crash.", () =>
            {
                TestEnvironment env = CreateEnvironment(null, 0);
                int zeroConfigId = BaseConfigId + 23;
                int negativeConfigId = BaseConfigId + 24;
                RegisterDefinition(env, zeroConfigId, 0, BuffTriggerType.Tick, BuffInstanceType.normal, 1, 8, 1);
                RegisterDefinition(env, negativeConfigId, -100, BuffTriggerType.Tick, BuffInstanceType.normal, 1, 8, 1);
                AddAndTick(env, env.TargetA, env.SourceA, zeroConfigId, 1, 1);
                AddAndTick(env, env.TargetB, env.SourceA, negativeConfigId, 1, 2);
                return Pass("no exception for effectId=0 and effectId=-100", 2, 0);
            });

            RunCase(report, MissingInvalidCategory, "Effect_MissingEffect_DoesNotBlockPublicBuffView_IfRuntimeDefinesSo", "Missing effect id should not block public view under current runtime.", () =>
            {
                TestEnvironment env = CreateEnvironment(null, 0);
                int configId = BaseConfigId + 25;
                RegisterDefinition(env, configId, MissingEffectId, BuffTriggerType.Tick, BuffInstanceType.normal, 1, 8, 1);
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                bool found = env.BuffSystem.TryGetBuff(env.TargetA, configId, env.SourceA, out BuffViewData view);
                int checks = 0;
                Assert(ref checks, found, "Current runtime should keep public view visible even when effect is missing.");
                return Pass("found=" + found + ", stack=" + view.Stack, checks, MissingEffectId);
            });
        }

        private void RunCompositeOrderTests(BuffSystemEffectTestReport report)
        {
            RunCase(report, CompositeOrderCategory, "CompositeEffect_OnApply_ActionsExecuteInDeclaredOrder", "OnApply actions execute in declared order.", () =>
            {
                CompositeTestEffect effect = CreateComposite("A", "B", "C");
                effect.OnApply(CreateManualContext(BaseConfigId + 30, CompositeEffectId));
                return AssertTrace(effect, "Apply:A>Apply:B>Apply:C", CompositeEffectId);
            });

            RunCase(report, CompositeOrderCategory, "CompositeEffect_OnTick_ActionsExecuteInDeclaredOrder", "OnTick actions execute in declared order.", () =>
            {
                CompositeTestEffect effect = CreateComposite("A", "B", "C");
                effect.OnTick(CreateManualContext(BaseConfigId + 31, CompositeEffectId));
                return AssertTrace(effect, "Tick:A>Tick:B>Tick:C", CompositeEffectId);
            });

            RunCase(report, CompositeOrderCategory, "CompositeEffect_OnRemove_ActionsExecuteInDeclaredOrder", "OnRemove actions execute in declared order.", () =>
            {
                CompositeTestEffect effect = CreateComposite("A", "B", "C");
                effect.OnRemove(CreateManualContext(BaseConfigId + 32, CompositeEffectId));
                return AssertTrace(effect, "Remove:A>Remove:B>Remove:C", CompositeEffectId);
            });

            RunCase(report, CompositeOrderCategory, "CompositeEffect_OnRefresh_ActionsExecuteInDeclaredOrder", "OnRefresh actions execute in declared order.", () =>
            {
                CompositeTestEffect effect = CreateComposite("A", "B", "C");
                effect.OnRefresh(CreateManualContext(BaseConfigId + 33, CompositeEffectId));
                return AssertTrace(effect, "Refresh:A>Refresh:B>Refresh:C", CompositeEffectId);
            });

            RunCase(report, CompositeOrderCategory, "CompositeEffect_OnStackChanged_ActionsExecuteInDeclaredOrder", "OnStackChanged actions execute in declared order.", () =>
            {
                CompositeTestEffect effect = CreateComposite("A", "B", "C");
                effect.OnStackChanged(CreateManualContext(BaseConfigId + 34, CompositeEffectId), 1);
                return AssertTrace(effect, "StackChanged:A>StackChanged:B>StackChanged:C", CompositeEffectId);
            });

            RunCase(report, CompositeOrderCategory, "CompositeEffect_MultipleLifecycle_TracesAreSeparated", "Lifecycle traces stay separated by phase.", () =>
            {
                CompositeTestEffect effect = CreateComposite("A", "B");
                BuffEffectContext context = CreateManualContext(BaseConfigId + 35, CompositeEffectId);
                effect.OnApply(context);
                effect.OnTick(context);
                int checks = 0;
                Assert(ref checks, effect.ExecutionOrderTrace == "Apply:A>Apply:B>Tick:A>Tick:B", "Trace should preserve lifecycle boundaries.");
                return Pass(effect.ExecutionOrderTrace, checks, CompositeEffectId, effect);
            });

            RunCase(report, CompositeOrderCategory, "CompositeEffect_EmptyActionList_DoesNotCrash", "CompositeEffect with no actions does not crash.", () =>
            {
                CompositeTestEffect effect = CreateComposite();
                effect.OnApply(CreateManualContext(BaseConfigId + 36, CompositeEffectId));
                int checks = 0;
                Assert(ref checks, effect.ApplyCount == 1, "Empty CompositeEffect should still record lifecycle count.");
                Assert(ref checks, string.IsNullOrEmpty(effect.ExecutionOrderTrace), "Empty action list should produce empty action trace.");
                return Pass("empty action list executed", checks, CompositeEffectId, effect);
            });
        }

        private void RunCompositeLifecycleTests(BuffSystemEffectTestReport report)
        {
            RunCase(report, CompositeLifecycleCategory, "CompositeEffect_AddTickRemove_DispatchesCorrectLifecycle", "BuffSystemCore dispatches apply/tick/remove to composite effect.", () =>
            {
                CompositeTestEffect effect = CreateComposite("A", "B");
                TestEnvironment env = CreateEnvironment(effect, CompositeEffectId);
                int configId = BaseConfigId + 40;
                RegisterDefinition(env, configId, CompositeEffectId, BuffTriggerType.Tick, BuffInstanceType.normal, 1, 8, 1);
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                Tick(env, 2);
                RemoveAndTick(env, env.TargetA, env.SourceA, configId, 1, false, true, 3);
                int checks = 0;
                Assert(ref checks, effect.ApplyCount == 1, "Composite OnApply should dispatch once.");
                Assert(ref checks, effect.TickCount > 0, "Composite OnTick should dispatch.");
                Assert(ref checks, effect.RemoveCount == 1, "Composite OnRemove should dispatch once.");
                return Pass(env.Describe(), checks, CompositeEffectId, effect);
            });

            RunCase(report, CompositeLifecycleCategory, "CompositeEffect_RefreshAll_DispatchesRefreshOnlyForExistingLayers", "RefreshAll dispatches refresh for existing layers and apply for incoming layer.", () =>
            {
                CompositeTestEffect effect = CreateComposite("A");
                TestEnvironment env = CreateEnvironment(effect, CompositeEffectId);
                int configId = BaseConfigId + 41;
                RegisterDefinition(env, configId, CompositeEffectId, BuffTriggerType.Tick, BuffInstanceType.parallel, 3, 8, 1, null, ParallelBuffStackUpPolicy.RefreshAll);
                AddAndTick(env, env.TargetA, env.SourceA, configId, 2, 1);
                effect.Reset();
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 2);
                int checks = 0;
                Assert(ref checks, effect.RefreshCount > 0, "RefreshAll should dispatch OnRefresh to existing layers.");
                Assert(ref checks, effect.ApplyCount == 1, "RefreshAll not-full should apply incoming layer once.");
                return Pass(env.Describe(), checks, CompositeEffectId, effect);
            });

            RunCase(report, CompositeLifecycleCategory, "CompositeEffect_Append_DispatchesApplyForIncomingLayer", "Append dispatches apply for incoming layer.", () =>
            {
                CompositeTestEffect effect = CreateComposite("A");
                TestEnvironment env = CreateEnvironment(effect, CompositeEffectId);
                int configId = BaseConfigId + 42;
                RegisterDefinition(env, configId, CompositeEffectId, BuffTriggerType.Tick, BuffInstanceType.parallel, 3, 8, 1, null, ParallelBuffStackUpPolicy.Append);
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                effect.Reset();
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 2);
                int checks = 0;
                Assert(ref checks, effect.ApplyCount == 1, "Append incoming layer should call OnApply once.");
                return Pass(env.Describe(), checks, CompositeEffectId, effect);
            });

            RunCase(report, CompositeLifecycleCategory, "CompositeEffect_ClearAll_DispatchesRemoveForAllLayersOrDocumentedAggregate", "ClearAll dispatches remove callbacks for current layers or aggregate behavior.", () =>
            {
                CompositeTestEffect effect = CreateComposite("A");
                TestEnvironment env = CreateEnvironment(effect, CompositeEffectId);
                int configId = BaseConfigId + 43;
                RegisterDefinition(env, configId, CompositeEffectId, BuffTriggerType.Tick, BuffInstanceType.parallel, 3, 8, 1, null, ParallelBuffStackUpPolicy.Append);
                AddAndTick(env, env.TargetA, env.SourceA, configId, 2, 1);
                effect.Reset();
                RemoveAndTick(env, env.TargetA, env.SourceA, configId, 1, false, true, 2);
                int checks = 0;
                Assert(ref checks, effect.RemoveCount > 0, "ClearAll should dispatch at least one remove callback.");
                return Pass(env.Describe(), checks, CompositeEffectId, effect);
            });

            RunCase(report, CompositeLifecycleCategory, "CompositeEffect_Expire_DispatchesRemoveOncePerLayerOrDocumentedAggregate", "Expire dispatches remove callbacks.", () =>
            {
                CompositeTestEffect effect = CreateComposite("A");
                TestEnvironment env = CreateEnvironment(effect, CompositeEffectId);
                int configId = BaseConfigId + 44;
                RegisterDefinition(env, configId, CompositeEffectId, BuffTriggerType.Tick, BuffInstanceType.parallel, 2, 2, 1, null, ParallelBuffStackUpPolicy.Append);
                AddAndTick(env, env.TargetA, env.SourceA, configId, 2, 1);
                effect.Reset();
                bool removed = TickUntilMissing(env, env.TargetA, env.SourceA, configId, 2, 8);
                int checks = 0;
                Assert(ref checks, removed, "Buff should expire within bounded ticks.");
                Assert(ref checks, effect.RemoveCount > 0, "Expire should dispatch remove callback.");
                return Pass("removed=" + removed + ", " + env.Describe(), checks, CompositeEffectId, effect);
            });
        }

        private void RunEventEffectTests(BuffSystemEffectTestReport report)
        {
            RunCase(report, EventEffectCategory, "Effect_EventTrigger_MatchingEvent_CallsEventEffect", "Matching event calls event effect.", () =>
            {
                CountingEventEffectExecutor effect = new CountingEventEffectExecutor();
                TestEnvironment env = CreateEnvironment(effect, EventEffectId);
                int configId = BaseConfigId + 50;
                RegisterDefinition(env, configId, EventEffectId, BuffTriggerType.EventTrigger, BuffInstanceType.normal, 1, 8, 0, new[] { ProbeEventId });
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                Raise(env, 2, ProbeEventId);
                int checks = 0;
                Assert(ref checks, effect.EventCount == 1, "Matching event should call OnEvent once.");
                return Pass(env.Describe(), checks, EventEffectId, effect);
            });

            RunCase(report, EventEffectCategory, "Effect_EventTrigger_WrongEvent_DoesNotCall", "Wrong event does not call event effect.", () =>
            {
                CountingEventEffectExecutor effect = new CountingEventEffectExecutor();
                TestEnvironment env = CreateEnvironment(effect, EventEffectId);
                int configId = BaseConfigId + 51;
                RegisterDefinition(env, configId, EventEffectId, BuffTriggerType.EventTrigger, BuffInstanceType.normal, 1, 8, 0, new[] { ProbeEventId });
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                Raise(env, 2, WrongEventId);
                int checks = 0;
                Assert(ref checks, effect.EventCount == 0, "Wrong event should not call OnEvent.");
                return Pass(env.Describe(), checks, EventEffectId, effect);
            });

            RunCase(report, EventEffectCategory, "Effect_EventTrigger_Tick_DoesNotCallEventEffect", "Tick does not call event effect.", () =>
            {
                CountingEventEffectExecutor effect = new CountingEventEffectExecutor();
                TestEnvironment env = CreateEnvironment(effect, EventEffectId);
                int configId = BaseConfigId + 52;
                RegisterDefinition(env, configId, EventEffectId, BuffTriggerType.EventTrigger, BuffInstanceType.normal, 1, 8, 0, new[] { ProbeEventId });
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                TickRange(env, 2, 4);
                int checks = 0;
                Assert(ref checks, effect.EventCount == 0, "Tick should not call OnEvent.");
                return Pass(env.Describe(), checks, EventEffectId, effect);
            });

            RunCase(report, EventEffectCategory, "Effect_EventTrigger_RemoveStopsEventEffect", "Removed EventTrigger buff stops receiving events.", () =>
            {
                CountingEventEffectExecutor effect = new CountingEventEffectExecutor();
                TestEnvironment env = CreateEnvironment(effect, EventEffectId);
                int configId = BaseConfigId + 53;
                RegisterDefinition(env, configId, EventEffectId, BuffTriggerType.EventTrigger, BuffInstanceType.normal, 1, 8, 0, new[] { ProbeEventId });
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                RemoveAndTick(env, env.TargetA, env.SourceA, configId, 1, false, true, 2);
                Raise(env, 3, ProbeEventId);
                int checks = 0;
                Assert(ref checks, effect.EventCount == 0, "Removed EventTrigger buff should not receive events.");
                return Pass(env.Describe(), checks, EventEffectId, effect);
            });

            RunCase(report, EventEffectCategory, "Effect_EventTrigger_ContextCorrect", "Event effect context carries target/source/definition/event id.", () =>
            {
                CountingEventEffectExecutor effect = new CountingEventEffectExecutor();
                TestEnvironment env = CreateEnvironment(effect, EventEffectId);
                int configId = BaseConfigId + 54;
                RegisterDefinition(env, configId, EventEffectId, BuffTriggerType.EventTrigger, BuffInstanceType.normal, 1, 8, 0, new[] { ProbeEventId });
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                Raise(env, 2, ProbeEventId);
                int checks = 0;
                Assert(ref checks, effect.LastTarget.Equals(env.TargetA), "Event context target should match.");
                Assert(ref checks, effect.LastSource.Equals(env.SourceA), "Event context source should match.");
                Assert(ref checks, effect.LastConfigId == configId, "Event context definition should match.");
                Assert(ref checks, effect.LastEventId == ProbeEventId, "Event id should match.");
                return Pass(env.Describe(), checks, EventEffectId, effect);
            });
        }

        private void RunGraphStyleTests(BuffSystemEffectTestReport report)
        {
            RunCase(report, GraphStyleCategory, "GraphStyleEffect_ReadonlyActions_ExecuteInOrder", "Readonly action fields execute in order.", () =>
            {
                GraphStyleEffect effect = new GraphStyleEffect(new RecordingGraphAction("First"), new RecordingGraphAction("Second"), new RecordingGraphAction("Third"));
                effect.OnApply(CreateManualContext(BaseConfigId + 60, GraphStyleEffectId));
                return AssertTrace(effect, "Apply:First>Apply:Second>Apply:Third", GraphStyleEffectId);
            });

            RunCase(report, GraphStyleCategory, "GraphStyleEffect_OnApply_CallsActionExecute", "OnApply calls action Execute.", () =>
            {
                RecordingGraphAction action = new RecordingGraphAction("A");
                GraphStyleEffect effect = new GraphStyleEffect(action);
                TestEnvironment env = CreateEnvironment(effect, GraphStyleEffectId);
                int configId = BaseConfigId + 61;
                RegisterDefinition(env, configId, GraphStyleEffectId, BuffTriggerType.Tick, BuffInstanceType.normal, 1, 8, 1);
                string traceBefore = effect.ExecutionOrderTrace;
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                env.Effects.TryGet(GraphStyleEffectId, out IBuffEffectExecutor resolvedExecutor);
                int checks = 0;
                Assert(ref checks, ReferenceEquals(effect, resolvedExecutor), "GraphStyle effect id should resolve to the GraphStyleEffect test double.");
                Assert(ref checks, effect.ApplyCount == 1, "OnApply should be dispatched once through BuffSystemCore.");
                Assert(ref checks, action.ExecuteCount > 0, "GraphStyle action should be executed.");
                Assert(ref checks, action.LastConfigId == configId, "GraphStyle action should receive current definition context.");
                Assert(ref checks, effect.ExecutionOrderTrace.Contains("Apply:A"), "Trace should contain Apply action even when same tick dispatches additional lifecycle callbacks.");
                string actual = BuildGraphStyleActual(
                    "TestExpectationWrong",
                    "AddAndTick dispatches OnApply during BuffSystemCore.Tick; the same frame may also dispatch StackChanged/Tick, so the test must check action execution instead of exact single-entry trace.",
                    GraphStyleEffectId,
                    resolvedExecutor,
                    traceBefore,
                    effect.ExecutionOrderTrace,
                    true,
                    1,
                    env.BuffSystem.TryGetBuff(env.TargetA, configId, env.SourceA, out _));
                return Pass(actual, checks, GraphStyleEffectId, effect);
            });

            RunCase(report, GraphStyleCategory, "GraphStyleEffect_OnTick_CallsActionExecute", "OnTick calls action Execute.", () =>
            {
                GraphStyleEffect effect = new GraphStyleEffect(new RecordingGraphAction("A"));
                TestEnvironment env = CreateEnvironment(effect, GraphStyleEffectId);
                int configId = BaseConfigId + 62;
                RegisterDefinition(env, configId, GraphStyleEffectId, BuffTriggerType.Tick, BuffInstanceType.normal, 1, 8, 1);
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                effect.Reset();
                Tick(env, 2);
                int checks = 0;
                Assert(ref checks, effect.ExecutionOrderTrace == "Tick:A", "OnTick should call action Execute.");
                return Pass(env.Describe(), checks, GraphStyleEffectId, effect);
            });

            RunCase(report, GraphStyleCategory, "GraphStyleEffect_OnRemove_CallsActionExecute", "OnRemove calls action Execute.", () =>
            {
                RecordingGraphAction action = new RecordingGraphAction("A");
                GraphStyleEffect effect = new GraphStyleEffect(action);
                TestEnvironment env = CreateEnvironment(effect, GraphStyleEffectId);
                int configId = BaseConfigId + 63;
                RegisterDefinition(env, configId, GraphStyleEffectId, BuffTriggerType.Tick, BuffInstanceType.normal, 1, 8, 1);
                AddAndTick(env, env.TargetA, env.SourceA, configId, 1, 1);
                bool visibleBeforeRemove = env.BuffSystem.TryGetBuff(env.TargetA, configId, env.SourceA, out _);
                int executeCountBeforeRemove = action.ExecuteCount;
                effect.Reset();
                string traceBefore = effect.ExecutionOrderTrace;
                RemoveAndTick(env, env.TargetA, env.SourceA, configId, 1, false, true, 2);
                env.Effects.TryGet(GraphStyleEffectId, out IBuffEffectExecutor resolvedExecutor);
                int checks = 0;
                Assert(ref checks, visibleBeforeRemove, "Remove test should warm up the buff before removal.");
                Assert(ref checks, ReferenceEquals(effect, resolvedExecutor), "GraphStyle effect id should resolve to the GraphStyleEffect test double.");
                Assert(ref checks, effect.RemoveCount == 1, "OnRemove should be dispatched once through BuffSystemCore.");
                Assert(ref checks, action.ExecuteCount > executeCountBeforeRemove, "GraphStyle action should be executed by OnRemove.");
                Assert(ref checks, effect.ExecutionOrderTrace.Contains("Remove:A"), "Trace should contain Remove action even when same tick dispatches additional lifecycle callbacks.");
                string actual = BuildGraphStyleActual(
                    "TestExpectationWrong",
                    "Remove path requires Add+Tick warmup first; RemoveAndTick may include lifecycle neighbors, so the test verifies Remove action presence and executor resolution.",
                    GraphStyleEffectId,
                    resolvedExecutor,
                    traceBefore,
                    effect.ExecutionOrderTrace,
                    true,
                    1,
                    visibleBeforeRemove);
                return Pass(actual, checks, GraphStyleEffectId, effect);
            });

            RunCase(report, GraphStyleCategory, "GraphStyleEffect_MissingAction_SkippedOrDocumented", "Missing action is skipped without crash.", () =>
            {
                GraphStyleEffect effect = new GraphStyleEffect(new RecordingGraphAction("A"), null, new RecordingGraphAction("C"));
                effect.OnApply(CreateManualContext(BaseConfigId + 64, GraphStyleEffectId));
                int checks = 0;
                Assert(ref checks, effect.ExecutionOrderTrace == "Apply:A>Apply:C", "Null action should be skipped.");
                return Pass(effect.ExecutionOrderTrace, checks, GraphStyleEffectId, effect);
            });
        }

        private void RunCase(BuffSystemEffectTestReport report, string category, string caseName, string expected, Func<EffectCaseExecution> action)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                EffectCaseExecution execution = action();
                stopwatch.Stop();
                report.Add(BuffSystemEffectTestCaseResult.FromOutcome(category, caseName, execution.Status, expected, execution.Outcome, stopwatch.Elapsed.TotalMilliseconds));
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                report.Add(BuffSystemEffectTestCaseResult.Failed(category, caseName, expected, string.Empty, 0, stopwatch.Elapsed.TotalMilliseconds, exception));
            }
        }

        private static EffectCaseExecution Pass(string actual, int invariantChecks, int effectId = 0, IEffectTestCounters counters = null)
        {
            return new EffectCaseExecution(BuffSystemEffectTestStatus.Passed, EffectCaseOutcome.Pass(actual, invariantChecks, effectId, counters));
        }

        private static EffectCaseExecution NotSupported(string reason, int effectId = 0)
        {
            return new EffectCaseExecution(BuffSystemEffectTestStatus.NotSupported, EffectCaseOutcome.NotSupported(reason, effectId));
        }

        private static EffectCaseExecution AssertTrace(IEffectTestCounters effect, string expectedTrace, int effectId)
        {
            int checks = 0;
            Assert(ref checks, effect.ExecutionOrderTrace == expectedTrace, "Trace mismatch. expected=" + expectedTrace + ", actual=" + effect.ExecutionOrderTrace);
            return Pass(effect.ExecutionOrderTrace, checks, effectId, effect);
        }

        private static string BuildGraphStyleActual(
            string classification,
            string keyEvidence,
            int effectId,
            IBuffEffectExecutor resolvedExecutor,
            string traceBeforeAction,
            string traceAfterAction,
            bool triggeredViaCore,
            int lifecycleWarmupFrames,
            bool publicViewVisible)
        {
            return $"Classification={classification}; KeyEvidence={keyEvidence}; EffectId={effectId}; ResolvedExecutorType={(resolvedExecutor != null ? resolvedExecutor.GetType().FullName : "<null>")}; TraceBeforeAction={traceBeforeAction}; TraceAfterAction={traceAfterAction}; TriggeredViaCore={triggeredViaCore}; LifecycleWarmupFrames={lifecycleWarmupFrames}; PublicViewVisibleBeforeRemove={publicViewVisible}";
        }

        private static TestEnvironment CreateEnvironment(IBuffEffectExecutor effect = null, int effectId = EffectId)
        {
            World world = new World();
            Entity targetA = world.CreateEntity();
            Entity targetB = world.CreateEntity();
            Entity sourceA = world.CreateEntity();
            Entity sourceB = world.CreateEntity();
            BuffDefinitionRegistry definitions = new BuffDefinitionRegistry();
            BuffEffectRegistry effects = new BuffEffectRegistry();
            IBuffEffectExecutor effectiveEffect = effect;
            CountingEffectExecutor countingEffect = effect as CountingEffectExecutor;
            if (effectiveEffect == null && effectId != 0)
            {
                countingEffect = new CountingEffectExecutor();
                effectiveEffect = countingEffect;
            }

            IEffectTestCounters counters = effectiveEffect as IEffectTestCounters;
            if (effectiveEffect != null && effectId != 0)
                effects.Register(effectId, effectiveEffect);

            BuffSystemCore buffSystem = new BuffSystemCore(definitions, effects);
            return new TestEnvironment(world, buffSystem, definitions, effects, counters, countingEffect, targetA, targetB, sourceA, sourceB);
        }

        private static void RegisterDefinition(
            TestEnvironment env,
            int configId,
            int effectId,
            BuffTriggerType triggerType,
            BuffInstanceType buffType,
            int maxStack,
            int durationFrames,
            int tickIntervalFrames,
            int[] eventIds = null,
            ParallelBuffStackUpPolicy stackUpPolicy = ParallelBuffStackUpPolicy.Append,
            ParallelBuffStackDownPolicy stackDownPolicy = ParallelBuffStackDownPolicy.RemoveEarliest,
            ParallelBuffStorageMode storageMode = ParallelBuffStorageMode.EntityPerStack)
        {
            env.Definitions.Register(CreateDefinition(configId, effectId, triggerType, buffType, maxStack, durationFrames, tickIntervalFrames, eventIds, stackUpPolicy, stackDownPolicy, storageMode));
        }

        private static BuffDefinition CreateDefinition(
            int configId,
            int effectId,
            BuffTriggerType triggerType,
            BuffInstanceType buffType,
            int maxStack,
            int durationFrames,
            int tickIntervalFrames,
            int[] eventIds = null,
            ParallelBuffStackUpPolicy stackUpPolicy = ParallelBuffStackUpPolicy.Append,
            ParallelBuffStackDownPolicy stackDownPolicy = ParallelBuffStackDownPolicy.RemoveEarliest,
            ParallelBuffStorageMode storageMode = ParallelBuffStorageMode.EntityPerStack)
        {
            return new BuffDefinition(
                configId,
                "EffectTest_" + configId,
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
                effectId,
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

        private static void TickRange(TestEnvironment env, int startFrame, int endFrame)
        {
            for (int frame = startFrame; frame <= endFrame; frame++)
                Tick(env, frame);
        }

        private static void Raise(TestEnvironment env, int frameNumber, int eventId)
        {
            SimulationContext context = new SimulationContext(frameNumber, FixedTickLength, false);
            EffectProbeEvent probeEvent = new EffectProbeEvent(frameNumber, eventId);
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

        private static BuffEffectContext CreateManualContext(int configId, int effectId)
        {
            World world = new World();
            Entity target = world.CreateEntity();
            Entity source = world.CreateEntity();
            Entity buffEntity = world.CreateEntity();
            BuffDefinition definition = CreateDefinition(configId, effectId, BuffTriggerType.Tick, BuffInstanceType.normal, 1, 8, 1);
            BuffRuntimeComponent runtime = new BuffRuntimeComponent(new AddBuffCommand(target, configId, source, 1), in definition, 1, 1);
            SimulationContext simulationContext = new SimulationContext(1, FixedTickLength, false);
            return new BuffEffectContext(world, in simulationContext, buffEntity, in runtime, in definition);
        }

        private static CompositeTestEffect CreateComposite(params string[] actionNames)
        {
            List<ICompositeTestAction> actions = new List<ICompositeTestAction>();
            for (int i = 0; i < actionNames.Length; i++)
                actions.Add(new RecordingCompositeAction(actionNames[i]));

            return new CompositeTestEffect(actions);
        }

        private static void Assert(ref int checks, bool condition, string message)
        {
            checks++;
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static Type FindTypeByName(params string[] names)
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
                    types = exception.Types.Where(type => type != null).ToArray();
                }

                for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    Type type = types[typeIndex];
                    for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
                    {
                        string name = names[nameIndex];
                        if (type.FullName == name || type.Name == name)
                            return type;
                    }
                }
            }

            return null;
        }

        private static BuffSystemEffectCapabilitySnapshot DiscoverCapabilities()
        {
            BuffSystemEffectCapabilitySnapshot snapshot = new BuffSystemEffectCapabilitySnapshot();
            snapshot.HasRegistry = typeof(BuffEffectRegistry).GetMethod("Register") != null &&
                typeof(BuffEffectRegistry).GetMethod("TryGet") != null &&
                typeof(BuffEffectRegistry).GetMethod("Remove") != null &&
                typeof(BuffEffectRegistry).GetMethod("Clear") != null;
            snapshot.HasExecutorBase = typeof(BuffEffectExecutorBase).GetMethod("OnApply") != null;
            snapshot.HasEventEffectInterface =
                typeof(IBuffEventEffectExecutor<EffectProbeEvent>).GetMethod("ShouldTrigger") != null &&
                typeof(IBuffEventEffectExecutor<EffectProbeEvent>).GetMethod("OnEvent") != null;

            Type compositeType = FindTypeByName(
                "BuffSystem.Editor.AuthoringGraphs.BuffGraphCompositeEffectPlanBuilder",
                "BuffGraphCompositeEffectPlanBuilder",
                "BuffSystem.Editor.AuthoringGraphs.BuffGraphCompositeEffectEmitter",
                "BuffGraphCompositeEffectEmitter");
            snapshot.HasCompositePattern = compositeType != null;
            snapshot.CompositePatternTypeName = compositeType != null ? compositeType.FullName : string.Empty;

            Type graphType = FindTypeByName(
                "BuffSystem.Editor.AuthoringGraphs.BuffGraphGenerateService",
                "BuffGraphGenerateService",
                "BuffSystem.IBuffGraphAction",
                "IBuffGraphAction");
            snapshot.HasGraphGeneratedPattern = graphType != null;
            snapshot.GraphGeneratedPatternTypeName = graphType != null ? graphType.FullName : string.Empty;

            snapshot.Notes.Add("Loaded assemblies count: " + AppDomain.CurrentDomain.GetAssemblies().Length);
            snapshot.Notes.Add("Composite pattern: " + (snapshot.HasCompositePattern ? snapshot.CompositePatternTypeName : "NotFound"));
            snapshot.Notes.Add("Graph-generated pattern: " + (snapshot.HasGraphGeneratedPattern ? snapshot.GraphGeneratedPatternTypeName : "NotFound"));
            return snapshot;
        }

        private readonly struct EffectCaseExecution
        {
            public readonly string Status;
            public readonly EffectCaseOutcome Outcome;

            public EffectCaseExecution(string status, EffectCaseOutcome outcome)
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
            public readonly IEffectTestCounters Counters;
            public readonly CountingEffectExecutor Effect;
            public readonly Entity TargetA;
            public readonly Entity TargetB;
            public readonly Entity SourceA;
            public readonly Entity SourceB;

            public TestEnvironment(
                World world,
                BuffSystemCore buffSystem,
                BuffDefinitionRegistry definitions,
                BuffEffectRegistry effects,
                IEffectTestCounters counters,
                CountingEffectExecutor effect,
                Entity targetA,
                Entity targetB,
                Entity sourceA,
                Entity sourceB)
            {
                World = world;
                BuffSystem = buffSystem;
                Definitions = definitions;
                Effects = effects;
                Counters = counters;
                Effect = effect;
                TargetA = targetA;
                TargetB = targetB;
                SourceA = sourceA;
                SourceB = sourceB;
            }

            public string Describe()
            {
                if (Counters == null)
                    return "no counters";

                return $"apply={Counters.ApplyCount}, tick={Counters.TickCount}, event={Counters.EventCount}, remove={Counters.RemoveCount}, refresh={Counters.RefreshCount}, stackChanged={Counters.StackChangedCount}, trace={Counters.ExecutionOrderTrace}, context={Counters.ContextSnapshot}";
            }
        }
    }

    internal sealed class BuffSystemEffectCapabilitySnapshot
    {
        public bool HasRegistry;
        public bool HasExecutorBase;
        public bool HasEventEffectInterface;
        public bool HasCompositePattern;
        public bool HasGraphGeneratedPattern;
        public string CompositePatternTypeName = string.Empty;
        public string GraphGeneratedPatternTypeName = string.Empty;
        public readonly List<string> Notes = new List<string>();
    }

    internal class CountingEffectExecutor : BuffEffectExecutorBase, IEffectTestCounters
    {
        private readonly List<string> _trace = new List<string>();

        public int ApplyCount { get; protected set; }
        public int TickCount { get; protected set; }
        public int RemoveCount { get; protected set; }
        public int RefreshCount { get; protected set; }
        public int StackChangedCount { get; protected set; }
        public int EventCount { get; protected set; }
        public int LastConfigId { get; private set; }
        public int LastStackDelta { get; private set; }
        public int LastEventId { get; protected set; }
        public Entity LastTarget { get; private set; }
        public Entity LastSource { get; private set; }
        public string ExecutionOrderTrace => string.Join(">", _trace);
        public string ContextSnapshot => $"target={LastTarget.ID}:{LastTarget.Version}, source={LastSource.ID}:{LastSource.Version}, configId={LastConfigId}, eventId={LastEventId}, delta={LastStackDelta}";

        public override void OnApply(in BuffEffectContext context)
        {
            ApplyCount++;
            Record("Apply", in context);
        }

        public override void OnTick(in BuffEffectContext context)
        {
            TickCount++;
            Record("Tick", in context);
        }

        public override void OnRemove(in BuffEffectContext context)
        {
            RemoveCount++;
            Record("Remove", in context);
        }

        public override void OnRefresh(in BuffEffectContext context)
        {
            RefreshCount++;
            Record("Refresh", in context);
        }

        public override void OnStackChanged(in BuffEffectContext context, int delta)
        {
            StackChangedCount++;
            LastStackDelta = delta;
            Record("StackChanged", in context);
        }

        public virtual void Reset()
        {
            ApplyCount = 0;
            TickCount = 0;
            RemoveCount = 0;
            RefreshCount = 0;
            StackChangedCount = 0;
            EventCount = 0;
            LastConfigId = 0;
            LastStackDelta = 0;
            LastEventId = 0;
            LastTarget = default;
            LastSource = default;
            _trace.Clear();
        }

        protected void Record(string phase, in BuffEffectContext context)
        {
            _trace.Add(phase);
            RecordContext(in context);
        }

        protected void RecordContext(in BuffEffectContext context)
        {
            LastTarget = context.Runtime.target;
            LastSource = context.Runtime.source;
            LastConfigId = context.Definition.ConfigId;
        }
    }

    internal sealed class CountingEventEffectExecutor : CountingEffectExecutor, IBuffEventEffectExecutor<EffectProbeEvent>
    {
        public int ShouldTriggerCount { get; private set; }

        public bool ShouldTrigger(in BuffEffectContext context, in EffectProbeEvent gameEvent)
        {
            ShouldTriggerCount++;
            return context.Definition.CanRespondToEvent(gameEvent.EventId);
        }

        public void OnEvent(in BuffEffectContext context, in EffectProbeEvent gameEvent)
        {
            EventCount++;
            LastEventId = gameEvent.EventId;
            Record("Event", in context);
        }
    }

    internal sealed class CompositeTestEffect : BuffEffectExecutorBase, IEffectTestCounters
    {
        private readonly List<ICompositeTestAction> _actions;
        private readonly List<string> _trace = new List<string>();

        public int ApplyCount { get; private set; }
        public int TickCount { get; private set; }
        public int RemoveCount { get; private set; }
        public int RefreshCount { get; private set; }
        public int StackChangedCount { get; private set; }
        public int EventCount => 0;
        public string ExecutionOrderTrace => string.Join(">", _trace);
        public string ContextSnapshot { get; private set; } = string.Empty;

        public CompositeTestEffect(IEnumerable<ICompositeTestAction> actions)
        {
            _actions = actions != null ? new List<ICompositeTestAction>(actions) : new List<ICompositeTestAction>();
        }

        public override void OnApply(in BuffEffectContext context)
        {
            ApplyCount++;
            Execute("Apply", in context);
        }

        public override void OnTick(in BuffEffectContext context)
        {
            TickCount++;
            Execute("Tick", in context);
        }

        public override void OnRemove(in BuffEffectContext context)
        {
            RemoveCount++;
            Execute("Remove", in context);
        }

        public override void OnRefresh(in BuffEffectContext context)
        {
            RefreshCount++;
            Execute("Refresh", in context);
        }

        public override void OnStackChanged(in BuffEffectContext context, int delta)
        {
            StackChangedCount++;
            Execute("StackChanged", in context);
        }

        public void Reset()
        {
            ApplyCount = 0;
            TickCount = 0;
            RemoveCount = 0;
            RefreshCount = 0;
            StackChangedCount = 0;
            ContextSnapshot = string.Empty;
            _trace.Clear();
        }

        private void Execute(string phase, in BuffEffectContext context)
        {
            ContextSnapshot = $"target={context.Runtime.target.ID}:{context.Runtime.target.Version}, source={context.Runtime.source.ID}:{context.Runtime.source.Version}, configId={context.Definition.ConfigId}";
            for (int i = 0; i < _actions.Count; i++)
                _actions[i].Execute(phase, in context, _trace);
        }
    }

    internal interface ICompositeTestAction
    {
        void Execute(string phase, in BuffEffectContext context, List<string> trace);
    }

    internal sealed class RecordingCompositeAction : ICompositeTestAction
    {
        private readonly string _name;

        public RecordingCompositeAction(string name)
        {
            _name = name;
        }

        public void Execute(string phase, in BuffEffectContext context, List<string> trace)
        {
            trace.Add(phase + ":" + _name);
        }
    }

    internal sealed class GraphStyleEffect : BuffEffectExecutorBase, IEffectTestCounters
    {
        private readonly IBuffGraphAction _first;
        private readonly IBuffGraphAction _second;
        private readonly IBuffGraphAction _third;
        private readonly List<string> _trace = new List<string>();

        public int ApplyCount { get; private set; }
        public int TickCount { get; private set; }
        public int RemoveCount { get; private set; }
        public int RefreshCount { get; private set; }
        public int StackChangedCount { get; private set; }
        public int EventCount => 0;
        public string ExecutionOrderTrace => string.Join(">", _trace);
        public string ContextSnapshot { get; private set; } = string.Empty;

        public GraphStyleEffect(IBuffGraphAction first, IBuffGraphAction second = null, IBuffGraphAction third = null)
        {
            _first = first;
            _second = second;
            _third = third;
        }

        public override void OnApply(in BuffEffectContext context)
        {
            ApplyCount++;
            Execute("Apply", in context);
        }

        public override void OnTick(in BuffEffectContext context)
        {
            TickCount++;
            Execute("Tick", in context);
        }

        public override void OnRemove(in BuffEffectContext context)
        {
            RemoveCount++;
            Execute("Remove", in context);
        }

        public override void OnRefresh(in BuffEffectContext context)
        {
            RefreshCount++;
            Execute("Refresh", in context);
        }

        public override void OnStackChanged(in BuffEffectContext context, int delta)
        {
            StackChangedCount++;
            Execute("StackChanged", in context);
        }

        public void Reset()
        {
            ApplyCount = 0;
            TickCount = 0;
            RemoveCount = 0;
            RefreshCount = 0;
            StackChangedCount = 0;
            ContextSnapshot = string.Empty;
            _trace.Clear();
        }

        private void Execute(string phase, in BuffEffectContext context)
        {
            ContextSnapshot = $"target={context.Runtime.target.ID}:{context.Runtime.target.Version}, source={context.Runtime.source.ID}:{context.Runtime.source.Version}, configId={context.Definition.ConfigId}";
            ExecuteAction(_first, phase, in context);
            ExecuteAction(_second, phase, in context);
            ExecuteAction(_third, phase, in context);
        }

        private void ExecuteAction(IBuffGraphAction action, string phase, in BuffEffectContext context)
        {
            if (action == null)
                return;

            if (action is RecordingGraphAction recording)
                recording.CurrentPhase = phase;

            action.Execute(in context);
            if (action is RecordingGraphAction recorded)
                _trace.Add(phase + ":" + recorded.Name);
        }
    }

    internal sealed class RecordingGraphAction : IBuffGraphAction
    {
        public readonly string Name;
        public string CurrentPhase;
        public int ExecuteCount;
        public int LastConfigId;

        public RecordingGraphAction(string name)
        {
            Name = name;
        }

        public void Execute(in BuffEffectContext context)
        {
            ExecuteCount++;
            LastConfigId = context.Definition.ConfigId;
        }
    }

    internal readonly struct EffectProbeEvent : IGameEvent
    {
        public int FrameNumber { get; }
        public int EventId { get; }

        public EffectProbeEvent(int frameNumber, int eventId)
        {
            FrameNumber = frameNumber;
            EventId = eventId;
        }
    }
}
