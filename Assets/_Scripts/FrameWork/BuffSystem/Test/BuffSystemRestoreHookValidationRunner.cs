using System.Collections.Generic;
using System.Text;
using ECSFrameWork;
using UnityEngine;

namespace BuffSystem
{
    /// <summary>
    /// BuffSystemCore.OnWorldRestored 的 Unity Editor 手动验证入口。
    /// </summary>
    public sealed class BuffSystemRestoreHookValidationRunner : MonoBehaviour
    {
        private const float FixedTickLength = 0.02f;

        private const int EntityPerStackBuffId = 9601;
        private const int CompressedBuffId = 9301;
        private const int EventBuffId = 9603;
        private const int ViewCacheBuffId = 9604;

        private const int EntityPerStackEffectId = 9701;
        private const int CompressedEffectId = 9401;
        private const int EventEffectId = 9703;
        private const int ViewCacheEffectId = 9704;
        private const int RestoreHookEventId = 9801;

        [ContextMenu("运行 Restore Hook 验证")]
        public void RunRestoreHookValidation()
        {
            ValidationState state = new ValidationState();
            state.Log("========== BuffSystem Restore Hook 验证 ==========");

            RunEntityPerStackRestoreHookTest(state);
            RunCompressedRestoreHookTest(state);
            RunEventTriggerRestoreHookTest(state);
            RunViewCacheStaleDataTest(state);
            RunNoSideEffectTest(state);

            state.Log(state.HasFailure
                ? "========== Result: FAIL =========="
                : "========== Result: PASS ==========");

            Debug.Log(state.BuildOutput());
        }

        private static void RunEntityPerStackRestoreHookTest(ValidationState state)
        {
            TestEnvironment env = CreateEnvironment(false);
            RegisterDefinition(env.Definitions, CreateParallelDefinition(
                EntityPerStackBuffId,
                "RestoreHook_EntityPerStack",
                EntityPerStackEffectId,
                maxStack: 4,
                storageMode: ParallelBuffStorageMode.EntityPerStack));
            env.Effects.Register(EntityPerStackEffectId, new RecordingEffect(state));

            env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, EntityPerStackBuffId, env.Source, 2));
            Tick(env, 1);

            bool beforeTryGet = env.BuffSystem.TryGetBuff(env.Target, EntityPerStackBuffId, env.Source, out BuffViewData beforeView);
            int beforeCount = CountBuffsWithConfig(env.BuffSystem.GetBuffs(env.Target), EntityPerStackBuffId);

            env.BuffSystem.OnWorldRestored(env.World);

            bool afterTryGet = env.BuffSystem.TryGetBuff(env.Target, EntityPerStackBuffId, env.Source, out BuffViewData afterView);
            int afterCount = CountBuffsWithConfig(env.BuffSystem.GetBuffs(env.Target), EntityPerStackBuffId);

            state.ExpectTrue("EntityPerStack TryGetBuff restore 前可见", beforeTryGet, 1, EntityPerStackBuffId, env.Target, env.Source, beforeTryGet ? beforeView.RuntimeHandle : 0);
            state.ExpectTrue("EntityPerStack TryGetBuff restore 后仍可见", afterTryGet, 1, EntityPerStackBuffId, env.Target, env.Source, afterTryGet ? afterView.RuntimeHandle : 0);
            state.ExpectEqual("EntityPerStack GetBuffs restore 前后数量一致", beforeCount, afterCount, 1, EntityPerStackBuffId, env.Target, env.Source, afterTryGet ? afterView.RuntimeHandle : 0);
            state.ExpectEqual("EntityPerStack Stack restore 前后一致", beforeTryGet ? beforeView.Stack : -1, afterTryGet ? afterView.Stack : -1, 1, EntityPerStackBuffId, env.Target, env.Source, afterTryGet ? afterView.RuntimeHandle : 0);
            state.ExpectEqual("EntityPerStack RuntimeHandle restore 前后一致", beforeTryGet ? beforeView.RuntimeHandle : -1, afterTryGet ? afterView.RuntimeHandle : -1, 1, EntityPerStackBuffId, env.Target, env.Source, afterTryGet ? afterView.RuntimeHandle : 0);
        }

        private static void RunCompressedRestoreHookTest(ValidationState state)
        {
            TestEnvironment env = CreateEnvironment(true);
            RegisterDefinition(env.Definitions, CreateParallelDefinition(
                CompressedBuffId,
                "RestoreHook_Compressed",
                CompressedEffectId,
                maxStack: 4,
                storageMode: ParallelBuffStorageMode.CompressedExpiryFrameList));
            env.Effects.Register(CompressedEffectId, new RecordingEffect(state));

            env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, CompressedBuffId, env.Source, 3));
            Tick(env, 1);
            Tick(env, 2);

            if (!TryGetSingleCompressedRuntime(env.World, out CompressedParallelBuffRuntimeComponent beforeRuntime))
            {
                state.Fail("Compressed runtime restore 前存在", 2, "one compressed runtime", "not found", CompressedBuffId, env.Target, env.Source, 0);
                return;
            }

            bool beforeTryGet = env.BuffSystem.TryGetBuff(env.Target, CompressedBuffId, env.Source, out BuffViewData beforeView);

            env.BuffSystem.OnWorldRestored(env.World);

            if (!TryGetSingleCompressedRuntime(env.World, out CompressedParallelBuffRuntimeComponent afterRuntime))
            {
                state.Fail("Compressed runtime restore 后存在", 2, "one compressed runtime", "not found", CompressedBuffId, env.Target, env.Source, 0);
                return;
            }

            bool afterTryGet = env.BuffSystem.TryGetBuff(env.Target, CompressedBuffId, env.Source, out BuffViewData afterView);
            int afterCount = CountBuffsWithConfig(env.BuffSystem.GetBuffs(env.Target), CompressedBuffId);

            state.ExpectTrue("Compressed aggregate restore 前可见", beforeTryGet, 2, CompressedBuffId, env.Target, env.Source, beforeTryGet ? beforeView.RuntimeHandle : 0);
            state.ExpectTrue("Compressed aggregate restore 后可见", afterTryGet, 2, CompressedBuffId, env.Target, env.Source, afterTryGet ? afterView.RuntimeHandle : 0);
            state.ExpectEqual("Compressed layerCount restore 前后一致", beforeRuntime.layerCount, afterRuntime.layerCount, 2, CompressedBuffId, env.Target, env.Source, afterTryGet ? afterView.RuntimeHandle : 0);
            state.ExpectEqual("Compressed ViewData.Stack == layerCount", afterRuntime.layerCount, afterTryGet ? afterView.Stack : -1, 2, CompressedBuffId, env.Target, env.Source, afterTryGet ? afterView.RuntimeHandle : 0);
            state.ExpectEqual("Compressed GetBuffs 只有一条聚合 ViewData", 1, afterCount, 2, CompressedBuffId, env.Target, env.Source, afterTryGet ? afterView.RuntimeHandle : 0);
        }

        private static void RunEventTriggerRestoreHookTest(ValidationState state)
        {
            TestEnvironment env = CreateEnvironment(false);
            RegisterDefinition(env.Definitions, new BuffDefinition(
                EventBuffId,
                "RestoreHook_EventTrigger",
                0,
                1,
                false,
                true,
                0,
                0,
                0,
                BuffTriggerType.EventTrigger,
                BuffInstanceType.normal,
                NormalBuffStackPolicy.AddStackOnly,
                ParallelBuffStackUpPolicy.Append,
                ParallelBuffStackDownPolicy.RemoveEarliest,
                EventEffectId,
                new[] { RestoreHookEventId }));

            RestoreHookEventEffect effect = new RestoreHookEventEffect(state);
            env.Effects.Register(EventEffectId, effect);

            env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, EventBuffId, env.Source));
            Tick(env, 1);

            int eventCountBeforeHook = effect.EventCount;
            env.BuffSystem.OnWorldRestored(env.World);
            int eventCountAfterHook = effect.EventCount;

            SimulationContext context = new SimulationContext(2, FixedTickLength, false);
            RestoreHookProbeEvent probeEvent = new RestoreHookProbeEvent(context.frameNumber, RestoreHookEventId);
            env.BuffSystem.Raise(env.World, context, in probeEvent);

            state.ExpectEqual("OnWorldRestored 不触发 EventTrigger", eventCountBeforeHook, eventCountAfterHook, 2, EventBuffId, env.Target, env.Source, 0);
            state.ExpectEqual("OnWorldRestored 后 Raise 仍触发 EventTrigger", eventCountAfterHook + 1, effect.EventCount, 2, EventBuffId, env.Target, env.Source, effect.LastRuntimeHandle);
        }

        private static void RunViewCacheStaleDataTest(ValidationState state)
        {
            TestEnvironment env = CreateEnvironment(false);
            RegisterDefinition(env.Definitions, CreateParallelDefinition(
                ViewCacheBuffId,
                "RestoreHook_ViewCache",
                ViewCacheEffectId,
                maxStack: 4,
                storageMode: ParallelBuffStorageMode.EntityPerStack));
            env.Effects.Register(ViewCacheEffectId, new RecordingEffect(state));

            env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, ViewCacheBuffId, env.Source, 1));
            Tick(env, 1);

            bool beforeTryGet = env.BuffSystem.TryGetBuff(env.Target, ViewCacheBuffId, env.Source, out BuffViewData beforeView);

            if (!TryGetSingleRuntime(env.World, out Entity runtimeEntity, out BuffRuntimeComponent runtime))
            {
                state.Fail("ViewCache stale setup runtime 存在", 1, "one runtime", "not found", ViewCacheBuffId, env.Target, env.Source, 0);
                return;
            }

            runtime.stack = 3;
            env.World.SetComponent(runtimeEntity, runtime);

            bool staleTryGet = env.BuffSystem.TryGetBuff(env.Target, ViewCacheBuffId, env.Source, out BuffViewData staleView);
            env.BuffSystem.OnWorldRestored(env.World);
            bool afterTryGet = env.BuffSystem.TryGetBuff(env.Target, ViewCacheBuffId, env.Source, out BuffViewData afterView);

            state.ExpectTrue("ViewCache stale setup restore 前可见", beforeTryGet && staleTryGet, 1, ViewCacheBuffId, env.Target, env.Source, beforeTryGet ? beforeView.RuntimeHandle : 0);
            state.ExpectEqual("ViewCache restore 前保持旧缓存", beforeTryGet ? beforeView.Stack : -1, staleTryGet ? staleView.Stack : -1, 1, ViewCacheBuffId, env.Target, env.Source, staleTryGet ? staleView.RuntimeHandle : 0);
            state.ExpectEqual("OnWorldRestored 后 ViewCache 读取恢复后组件真状态", 3, afterTryGet ? afterView.Stack : -1, 1, ViewCacheBuffId, env.Target, env.Source, afterTryGet ? afterView.RuntimeHandle : 0);
        }

        private static void RunNoSideEffectTest(ValidationState state)
        {
            TestEnvironment env = CreateEnvironment(false);
            RegisterDefinition(env.Definitions, CreateParallelDefinition(
                EntityPerStackBuffId,
                "RestoreHook_NoSideEffect",
                EntityPerStackEffectId,
                maxStack: 4,
                storageMode: ParallelBuffStorageMode.EntityPerStack));

            RecordingEffect effect = new RecordingEffect(state);
            env.Effects.Register(EntityPerStackEffectId, effect);

            env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, EntityPerStackBuffId, env.Source, 1));
            Tick(env, 1);

            int applyBefore = effect.ApplyCount;
            int tickBefore = effect.TickCount;
            int removeBefore = effect.RemoveCount;
            int eventBefore = effect.EventLikeCount;

            env.BuffSystem.OnWorldRestored(env.World);

            state.ExpectEqual("OnWorldRestored 不触发 OnApply", applyBefore, effect.ApplyCount, 1, EntityPerStackBuffId, env.Target, env.Source, effect.LastRuntimeHandle);
            state.ExpectEqual("OnWorldRestored 不触发 OnTick", tickBefore, effect.TickCount, 1, EntityPerStackBuffId, env.Target, env.Source, effect.LastRuntimeHandle);
            state.ExpectEqual("OnWorldRestored 不触发 OnRemove", removeBefore, effect.RemoveCount, 1, EntityPerStackBuffId, env.Target, env.Source, effect.LastRuntimeHandle);
            state.ExpectEqual("OnWorldRestored 不触发 OnEvent", eventBefore, effect.EventLikeCount, 1, EntityPerStackBuffId, env.Target, env.Source, effect.LastRuntimeHandle);
        }

        private static TestEnvironment CreateEnvironment(bool compressedGate)
        {
            World world = new World();
            Entity target = world.CreateEntity();
            Entity source = world.CreateEntity();
            BuffDefinitionRegistry definitions = new BuffDefinitionRegistry();
            BuffEffectRegistry effects = new BuffEffectRegistry();
            BuffSystemCore buffSystem = compressedGate
                ? BuffSystemCore.CreateForCompressedParallelValidation(definitions, effects)
                : new BuffSystemCore(definitions, effects);
            return new TestEnvironment(world, target, source, definitions, effects, buffSystem);
        }

        private static BuffDefinition CreateParallelDefinition(
            int configId,
            string name,
            int effectId,
            int maxStack,
            ParallelBuffStorageMode storageMode)
        {
            return new BuffDefinition(
                configId,
                name,
                0,
                maxStack,
                false,
                false,
                30,
                0,
                0,
                BuffTriggerType.Tick,
                BuffInstanceType.parallel,
                NormalBuffStackPolicy.AddStackOnly,
                ParallelBuffStackUpPolicy.Append,
                ParallelBuffStackDownPolicy.RemoveEarliest,
                effectId,
                null,
                storageMode);
        }

        private static void RegisterDefinition(BuffDefinitionRegistry registry, in BuffDefinition definition)
        {
            registry.Register(in definition);
        }

        private static void Tick(TestEnvironment env, int frameNumber)
        {
            SimulationContext context = new SimulationContext(frameNumber, FixedTickLength, false);
            env.BuffSystem.Tick(env.World, context);
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

        private static bool TryGetSingleRuntime(World world, out Entity runtimeEntity, out BuffRuntimeComponent runtime)
        {
            List<Entity> entities = new List<Entity>();
            EntityQueryDescription query = world.Query().With<BuffRuntimeComponent>().BuildDescription();
            world.FillQuery(query, entities, true);

            runtimeEntity = Entity.Invalid;
            runtime = default(BuffRuntimeComponent);

            if (entities.Count != 1)
                return false;

            runtimeEntity = entities[0];
            return world.TryGetComponent(runtimeEntity, out runtime);
        }

        private static bool TryGetSingleCompressedRuntime(World world, out CompressedParallelBuffRuntimeComponent runtime)
        {
            List<Entity> entities = new List<Entity>();
            EntityQueryDescription query = world.Query().With<CompressedParallelBuffRuntimeComponent>().BuildDescription();
            world.FillQuery(query, entities, true);

            runtime = default(CompressedParallelBuffRuntimeComponent);

            if (entities.Count != 1)
                return false;

            return world.TryGetComponent(entities[0], out runtime);
        }

        private readonly struct TestEnvironment
        {
            public readonly World World;
            public readonly Entity Target;
            public readonly Entity Source;
            public readonly BuffDefinitionRegistry Definitions;
            public readonly BuffEffectRegistry Effects;
            public readonly BuffSystemCore BuffSystem;

            public TestEnvironment(
                World world,
                Entity target,
                Entity source,
                BuffDefinitionRegistry definitions,
                BuffEffectRegistry effects,
                BuffSystemCore buffSystem)
            {
                World = world;
                Target = target;
                Source = source;
                Definitions = definitions;
                Effects = effects;
                BuffSystem = buffSystem;
            }
        }

        private readonly struct RestoreHookProbeEvent : IGameEvent
        {
            public readonly int frameNumber;
            public int EventId { get; }
            // 与 Raise 使用的 SimulationContext 保持同一逻辑帧，避免事件帧号和上下文帧号分离。
            public int FrameNumber => frameNumber;

            public RestoreHookProbeEvent(int frameNumber, int eventId)
            {
                this.frameNumber = frameNumber;
                EventId = eventId;
            }
        }

        private sealed class RecordingEffect : BuffEffectExecutorBase
        {
            public int ApplyCount { get; private set; }
            public int TickCount { get; private set; }
            public int RemoveCount { get; private set; }
            public int EventLikeCount { get; private set; }
            public int LastRuntimeHandle { get; private set; }

            private readonly ValidationState _state;

            public RecordingEffect(ValidationState state)
            {
                _state = state;
            }

            public override void OnApply(in BuffEffectContext context)
            {
                ApplyCount++;
                LastRuntimeHandle = context.Runtime.runtimeHandle;
                _state.LogFrame(context.SimulationContext.frameNumber, $"记录 Effect OnApply configId={context.Definition.ConfigId} runtimeHandle={context.Runtime.runtimeHandle}");
            }

            public override void OnTick(in BuffEffectContext context)
            {
                TickCount++;
                LastRuntimeHandle = context.Runtime.runtimeHandle;
                _state.LogFrame(context.SimulationContext.frameNumber, $"记录 Effect OnTick configId={context.Definition.ConfigId} runtimeHandle={context.Runtime.runtimeHandle}");
            }

            public override void OnRemove(in BuffEffectContext context)
            {
                RemoveCount++;
                LastRuntimeHandle = context.Runtime.runtimeHandle;
                _state.LogFrame(context.SimulationContext.frameNumber, $"记录 Effect OnRemove configId={context.Definition.ConfigId} runtimeHandle={context.Runtime.runtimeHandle}");
            }
        }

        private sealed class RestoreHookEventEffect : BuffEffectExecutorBase, IBuffEventEffectExecutor<RestoreHookProbeEvent>
        {
            private readonly ValidationState _state;

            public int EventCount { get; private set; }
            public int LastRuntimeHandle { get; private set; }

            public RestoreHookEventEffect(ValidationState state)
            {
                _state = state;
            }

            public bool ShouldTrigger(in BuffEffectContext context, in RestoreHookProbeEvent gameEvent)
            {
                return gameEvent.EventId == RestoreHookEventId;
            }

            public void OnEvent(in BuffEffectContext context, in RestoreHookProbeEvent gameEvent)
            {
                EventCount++;
                LastRuntimeHandle = context.Runtime.runtimeHandle;
                _state.LogFrame(gameEvent.frameNumber, $"记录 EventTrigger OnEvent configId={context.Definition.ConfigId} runtimeHandle={context.Runtime.runtimeHandle}");
            }
        }

        private sealed class ValidationState
        {
            private readonly StringBuilder _builder = new StringBuilder();

            public bool HasFailure { get; private set; }

            public void Log(string message)
            {
                _builder.AppendLine(message);
            }

            public void LogFrame(int frameNumber, string message)
            {
                _builder.Append("[F").Append(frameNumber).Append("] ").AppendLine(message);
            }

            public void ExpectTrue(string testName, bool actual, int frameNumber, int configId, Entity target, Entity source, int runtimeHandle)
            {
                if (actual)
                {
                    AppendPass(testName, frameNumber, configId, target, source, runtimeHandle);
                    return;
                }

                Fail(testName, frameNumber, "true", "false", configId, target, source, runtimeHandle);
            }

            public void ExpectEqual(string testName, int expected, int actual, int frameNumber, int configId, Entity target, Entity source, int runtimeHandle)
            {
                if (expected == actual)
                {
                    AppendPass(testName, frameNumber, configId, target, source, runtimeHandle);
                    return;
                }

                Fail(testName, frameNumber, expected.ToString(), actual.ToString(), configId, target, source, runtimeHandle);
            }

            public void Fail(string testName, int frameNumber, string expected, string actual, int configId, Entity target, Entity source, int runtimeHandle)
            {
                HasFailure = true;
                _builder.Append("[F").Append(frameNumber).Append("] ").Append(testName)
                    .Append(": FAIL expected=").Append(expected)
                    .Append(", actual=").Append(actual)
                    .Append(", configId=").Append(configId)
                    .Append(", target=").Append(target.ID).Append('/').Append(target.Version)
                    .Append(", source=").Append(source.ID).Append('/').Append(source.Version)
                    .Append(", runtimeHandle=").Append(runtimeHandle)
                    .AppendLine();
            }

            public string BuildOutput()
            {
                return _builder.ToString();
            }

            private void AppendPass(string testName, int frameNumber, int configId, Entity target, Entity source, int runtimeHandle)
            {
                _builder.Append("[F").Append(frameNumber).Append("] ").Append(testName)
                    .Append(": PASS configId=").Append(configId)
                    .Append(", target=").Append(target.ID).Append('/').Append(target.Version)
                    .Append(", source=").Append(source.ID).Append('/').Append(source.Version)
                    .Append(", runtimeHandle=").Append(runtimeHandle)
                    .AppendLine();
            }
        }
    }
}
