using System.Collections.Generic;
using System.Text;
using ECSFrameWork;
using UnityEngine;

namespace BuffSystem
{
    /// <summary>
    /// Phase 2A 生命周期 EffectRequest Pipeline 的 Unity Editor 手动验证入口。
    /// </summary>
    public sealed class BuffSystemPhase2AValidationRunner : MonoBehaviour
    {
        private const float FixedTickLength = 0.02f;

        private const int BuffAId = 9001;
        private const int BuffBId = 9002;
        private const int BuffDId = 9004;
        private const int BuffEId = 9005;
        private const int BuffFId = 9006;

        private const int EffectAId = 9101;
        private const int EffectBId = 9102;
        private const int EffectDId = 9104;
        private const int EffectEId = 9105;
        private const int EffectFId = 9106;

        private const int ProbeEventId = 9901;

        [ContextMenu("Run Phase 2A Validation")]
        public void RunPhase2AValidation()
        {
            ValidationState state = new ValidationState();
            World world = new World();
            Entity target = world.CreateEntity();
            Entity source = world.CreateEntity();

            BuffDefinitionRegistry definitionRegistry = new BuffDefinitionRegistry();
            RegisterDefinitions(definitionRegistry);

            BuffEffectRegistry effectRegistry = new BuffEffectRegistry();
            BuffSystemCore buffSystem = new BuffSystemCore(definitionRegistry, effectRegistry);
            RegisterEffects(effectRegistry, buffSystem, state, target, source);

            state.Log("========== BuffSystem Phase 2A Validation ==========");

            RunApplyNonRecursiveTest(buffSystem, world, state, target, source);
            RunTickCommandNonRecursiveTest(buffSystem, world, state, target, source);
            RunEventBypassTest(buffSystem, world, state, target, source);

            state.Log(state.HasFailure ? "========== Result: FAIL ==========" : "========== Result: PASS ==========");
            Debug.Log(state.BuildOutput());
        }

        private static void RegisterDefinitions(BuffDefinitionRegistry registry)
        {
            registry.Register(CreateDefinition(BuffAId, "P2A_ApplyAddsB", EffectAId, 1, true, 0, BuffTriggerType.Tick, null));
            registry.Register(CreateDefinition(BuffBId, "P2A_ApplyLogB", EffectBId, 1, true, 0, BuffTriggerType.Tick, null));
            registry.Register(CreateDefinition(BuffDId, "P2A_TickAddRemoveD", EffectDId, 5, true, 1, BuffTriggerType.Tick, null));
            registry.Register(CreateDefinition(BuffEId, "P2A_QueryAfterPendingRemoveE", EffectEId, 1, true, 0, BuffTriggerType.Tick, null));
            registry.Register(CreateDefinition(BuffFId, "P2A_EventEffectProbe", EffectFId, 1, true, 0, BuffTriggerType.EventTrigger, new[] { ProbeEventId }));
        }

        private static BuffDefinition CreateDefinition(
            int configId,
            string name,
            int effectId,
            int maxStack,
            bool isForever,
            int tickIntervalFrames,
            BuffTriggerType triggerType,
            int[] eventIds)
        {
            return new BuffDefinition(
                configId,
                name,
                0,
                maxStack,
                false,
                isForever,
                isForever ? 0 : 30,
                tickIntervalFrames,
                0,
                triggerType,
                BuffInstanceType.normal,
                NormalBuffStackPolicy.AddStackOnly,
                ParallelBuffStackUpPolicy.Append,
                ParallelBuffStackDownPolicy.RemoveEarliest,
                effectId,
                eventIds);
        }

        private static void RegisterEffects(
            BuffEffectRegistry registry,
            BuffSystemCore buffSystem,
            ValidationState state,
            Entity target,
            Entity source)
        {
            registry.Register(EffectAId, new ApplyAddsBuffEffect(buffSystem, state, target, source));
            registry.Register(EffectBId, new ApplyLogEffect(state));
            registry.Register(EffectDId, new TickAddRemoveEffect(buffSystem, state, target, source));
            registry.Register(EffectEId, new QueryPendingRemoveEffect(buffSystem, state, target, source));
            registry.Register(EffectFId, new EventProbeEffect(state));
        }

        private static void RunApplyNonRecursiveTest(
            BuffSystemCore buffSystem,
            World world,
            ValidationState state,
            Entity target,
            Entity source)
        {
            buffSystem.AddBuff(new AddBuffCommand(target, BuffAId, source));
            Tick(buffSystem, world, 1);

            state.Assert(!state.HasLog("B.OnApply", 1), "[F1] Assert B.OnApply not executed in same Flush");

            Tick(buffSystem, world, 2);
            state.Assert(state.HasLog("B.OnApply", 2), "[F2] Assert B.OnApply executed on next Tick");
        }

        private static void RunTickCommandNonRecursiveTest(
            BuffSystemCore buffSystem,
            World world,
            ValidationState state,
            Entity target,
            Entity source)
        {
            buffSystem.AddBuff(new AddBuffCommand(target, BuffDId, source, 3));
            Tick(buffSystem, world, 3);
            Tick(buffSystem, world, 4);

            state.Assert(!state.HasLog("E.OnApply", 4), "[F4] Assert E.OnApply not executed in same Flush");
            state.Assert(!state.HasLog("D.OnRemove", 4), "[F4] Assert D.OnRemove not executed in same Flush");

            Tick(buffSystem, world, 5);

            state.Assert(state.PendingRemoveTryGetD == false && state.PendingRemoveGetBuffsContainsD == false, "[F5] Assert pending remove hidden from queries");
            state.Assert(state.RemoveStackSnapshot == 3, "[F5] Assert OnRemove pre-remove snapshot stack=3");
            state.Assert(state.StackChangedOrder >= 0 && state.RemoveOrder > state.StackChangedOrder, "[F5] Assert StackChanged before Remove");
        }

        private static void RunEventBypassTest(
            BuffSystemCore buffSystem,
            World world,
            ValidationState state,
            Entity target,
            Entity source)
        {
            buffSystem.AddBuff(new AddBuffCommand(target, BuffFId, source));
            Tick(buffSystem, world, 6);

            SimulationContext context = new SimulationContext(6, FixedTickLength, false);
            Phase2AProbeEvent probeEvent = new Phase2AProbeEvent(6, ProbeEventId);
            buffSystem.Raise(world, context, in probeEvent);

            state.Assert(state.EventProbeTriggered, "[F6] Assert event effect hot path still works");
        }

        private static void Tick(BuffSystemCore buffSystem, World world, int frameNumber)
        {
            SimulationContext context = new SimulationContext(frameNumber, FixedTickLength, false);
            buffSystem.Tick(world, context);
        }

        private sealed class ApplyAddsBuffEffect : BuffEffectExecutorBase
        {
            private readonly BuffSystemCore _buffSystem;
            private readonly ValidationState _state;
            private readonly Entity _target;
            private readonly Entity _source;

            public ApplyAddsBuffEffect(BuffSystemCore buffSystem, ValidationState state, Entity target, Entity source)
            {
                _buffSystem = buffSystem;
                _state = state;
                _target = target;
                _source = source;
            }

            public override void OnApply(in BuffEffectContext context)
            {
                _state.LogFrame(context.SimulationContext.frameNumber, "A.OnApply -> Queue Add B");
                _buffSystem.AddBuff(new AddBuffCommand(_target, BuffBId, _source));
            }
        }

        private sealed class ApplyLogEffect : BuffEffectExecutorBase
        {
            private readonly ValidationState _state;

            public ApplyLogEffect(ValidationState state)
            {
                _state = state;
            }

            public override void OnApply(in BuffEffectContext context)
            {
                _state.LogFrame(context.SimulationContext.frameNumber, "B.OnApply");
            }
        }

        private sealed class TickAddRemoveEffect : BuffEffectExecutorBase
        {
            private readonly BuffSystemCore _buffSystem;
            private readonly ValidationState _state;
            private readonly Entity _target;
            private readonly Entity _source;

            public TickAddRemoveEffect(BuffSystemCore buffSystem, ValidationState state, Entity target, Entity source)
            {
                _buffSystem = buffSystem;
                _state = state;
                _target = target;
                _source = source;
            }

            public override void OnApply(in BuffEffectContext context)
            {
                _state.LogFrame(context.SimulationContext.frameNumber, $"D.OnApply stack={context.Runtime.stack}");
            }

            public override void OnTick(in BuffEffectContext context)
            {
                _state.LogFrame(context.SimulationContext.frameNumber, "D.OnTick -> Queue Add E, Queue Remove D");
                _buffSystem.AddBuff(new AddBuffCommand(_target, BuffEId, _source));
                _buffSystem.RemoveBuff(new RemoveBuffCommand(_target, BuffDId, _source, 1, false, true));
            }

            public override void OnStackChanged(in BuffEffectContext context, int delta)
            {
                _state.StackChangedOrder = _state.CurrentOrder;
                _state.LogFrame(context.SimulationContext.frameNumber, $"D.OnStackChanged delta={delta} stackSnapshot={context.Runtime.stack}");
            }

            public override void OnRemove(in BuffEffectContext context)
            {
                _state.RemoveOrder = _state.CurrentOrder;
                _state.RemoveStackSnapshot = context.Runtime.stack;
                _state.LogFrame(context.SimulationContext.frameNumber, $"D.OnRemove stackSnapshot={context.Runtime.stack}");
            }
        }

        private sealed class QueryPendingRemoveEffect : BuffEffectExecutorBase
        {
            private readonly BuffSystemCore _buffSystem;
            private readonly ValidationState _state;
            private readonly Entity _target;
            private readonly Entity _source;

            public QueryPendingRemoveEffect(BuffSystemCore buffSystem, ValidationState state, Entity target, Entity source)
            {
                _buffSystem = buffSystem;
                _state = state;
                _target = target;
                _source = source;
            }

            public override void OnApply(in BuffEffectContext context)
            {
                bool tryGetD = _buffSystem.TryGetBuff(_target, BuffDId, _source, out BuffViewData _);
                bool getBuffsContainsD = false;
                IReadOnlyList<BuffViewData> buffs = _buffSystem.GetBuffs(_target);

                for (int i = 0; i < buffs.Count; i++)
                {
                    if (buffs[i].ConfigId == BuffDId)
                    {
                        getBuffsContainsD = true;
                        break;
                    }
                }

                _state.PendingRemoveTryGetD = tryGetD;
                _state.PendingRemoveGetBuffsContainsD = getBuffsContainsD;
                _state.LogFrame(context.SimulationContext.frameNumber, $"E.OnApply -> TryGetBuff(D)={tryGetD}, GetBuffsContainsD={getBuffsContainsD}");
            }
        }

        private sealed class EventProbeEffect : BuffEffectExecutorBase, IBuffEventEffectExecutor<Phase2AProbeEvent>
        {
            private readonly ValidationState _state;

            public EventProbeEffect(ValidationState state)
            {
                _state = state;
            }

            public bool ShouldTrigger(in BuffEffectContext context, in Phase2AProbeEvent gameEvent)
            {
                return gameEvent.EventId == ProbeEventId;
            }

            public void OnEvent(in BuffEffectContext context, in Phase2AProbeEvent gameEvent)
            {
                _state.EventProbeTriggered = true;
                _state.LogFrame(gameEvent.FrameNumber, $"EventProbe.OnEvent eventId={gameEvent.EventId}");
            }
        }

        private readonly struct Phase2AProbeEvent : IGameEvent
        {
            public int FrameNumber { get; }
            public int EventId { get; }

            public Phase2AProbeEvent(int frameNumber, int eventId)
            {
                FrameNumber = frameNumber;
                EventId = eventId;
            }
        }

        private sealed class ValidationState
        {
            private readonly StringBuilder _builder = new StringBuilder();
            private readonly List<FrameLog> _frameLogs = new List<FrameLog>();
            private int _order;

            public bool HasFailure { get; private set; }
            public bool? PendingRemoveTryGetD { get; set; }
            public bool? PendingRemoveGetBuffsContainsD { get; set; }
            public int RemoveStackSnapshot { get; set; } = -1;
            public int StackChangedOrder { get; set; } = -1;
            public int RemoveOrder { get; set; } = -1;
            public bool EventProbeTriggered { get; set; }
            public int CurrentOrder => _order;

            public void Log(string message)
            {
                _builder.AppendLine(message);
            }

            public void LogFrame(int frameNumber, string message)
            {
                _order++;
                _frameLogs.Add(new FrameLog(frameNumber, message));
                _builder.Append("[F").Append(frameNumber).Append("] ").AppendLine(message);
            }

            public bool HasLog(string contains, int frameNumber)
            {
                for (int i = 0; i < _frameLogs.Count; i++)
                {
                    FrameLog log = _frameLogs[i];

                    if (log.FrameNumber == frameNumber && log.Message.Contains(contains))
                        return true;
                }

                return false;
            }

            public void Assert(bool condition, string message)
            {
                if (condition)
                {
                    _builder.AppendLine(message + ": PASS");
                    return;
                }

                HasFailure = true;
                _builder.AppendLine(message + ": FAIL");
            }

            public string BuildOutput()
            {
                return _builder.ToString();
            }
        }

        private readonly struct FrameLog
        {
            public readonly int FrameNumber;
            public readonly string Message;

            public FrameLog(int frameNumber, string message)
            {
                FrameNumber = frameNumber;
                Message = message;
            }
        }
    }
}
