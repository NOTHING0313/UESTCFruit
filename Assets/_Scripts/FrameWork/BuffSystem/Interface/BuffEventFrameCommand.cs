using ECSFrameWork;

namespace BuffSystem
{
    /// <summary>
    /// 按帧重放 Buff 逻辑事件的命令；事件数据保持泛型 struct，不保存表现层对象。
    /// </summary>
    public sealed class RaiseBuffEventFrameCommand<TEvent> : ISimulationFrameCommand, IRebuildableSimulationFrameCommand, ICommandDebugView
        where TEvent : struct, IGameEvent
    {
        private const float FallbackTickLength = 0.02f;

        private readonly IBuffSystem _buffSystem;
        private readonly TEvent _gameEvent;

        public int FrameNumber { get; }
        public string DebugName => typeof(RaiseBuffEventFrameCommand<TEvent>).Name;
        public Entity DebugTargetEntity => Entity.Invalid;

        public RaiseBuffEventFrameCommand(IBuffSystem buffSystem, in TEvent gameEvent)
            : this(gameEvent.FrameNumber, buffSystem, in gameEvent)
        {
        }

        public RaiseBuffEventFrameCommand(int frameNumber, IBuffSystem buffSystem, in TEvent gameEvent)
        {
            FrameNumber = gameEvent.FrameNumber;
            _buffSystem = buffSystem;
            _gameEvent = gameEvent;
        }

        public ISimulationFrameCommand Rebuild(int frameNumber)
        {
            if (frameNumber == _gameEvent.FrameNumber)
                return this;

            if (_gameEvent is IReframeableGameEvent<TEvent> reframeable)
            {
                TEvent reframedEvent = reframeable.WithFrame(frameNumber);
                return new RaiseBuffEventFrameCommand<TEvent>(_buffSystem, in reframedEvent);
            }

            // 不支持重设帧号的事件不能被 Rebuild 改帧，只能沿用事件自身帧号，避免命令帧号与事件帧号不一致。
            return this;
        }

        /// <summary>返回事件命令调试摘要，避免调试窗口展开完整泛型事件。</summary>
        public string GetDebugSummary()
        {
            return $"Raise Buff Event, Frame={FrameNumber}, EventId={_gameEvent.EventId}, EventType={typeof(TEvent).Name}";
        }

        public void Execute(World world)
        {
            if (world == null || _buffSystem == null || FrameNumber <= 0)
                return;

            // ISimulationFrameCommand.Execute(World) 无法获得真实 tickLength 和 isRollback。
            // 这里的上下文只用于兼容帧命令入口，不是完整 Tick 上下文；事件 Effect 不应依赖该 tickLength。
            // 需要真实上下文时，请在固定帧 System 内直接调用 IBuffSystem.Raise(world, context, in gameEvent)。
            SimulationContext context = new SimulationContext(FrameNumber, FallbackTickLength, false);
            _buffSystem.Raise(world, context, in _gameEvent);
        }
    }

    /// <summary>
    /// Buff 事件帧命令扩展；调用方必须显式传入当前使用的 IBuffSystem，避免隐藏单例依赖。
    /// </summary>
    public static class BuffEventFrameCommandExtensions
    {
        public static void RaiseBuffEventAtFrame<TEvent>(
            this SimulationFrameCommandBuffer buffer,
            IBuffSystem buffSystem,
            in TEvent gameEvent)
            where TEvent : struct, IGameEvent
        {
            buffer.RaiseBuffEventAtFrame(buffSystem, in gameEvent, SimulationFrameCommandTiming.BeforeTick);
        }

        public static void RaiseBuffEventAtFrame<TEvent>(
            this SimulationFrameCommandBuffer buffer,
            IBuffSystem buffSystem,
            in TEvent gameEvent,
            SimulationFrameCommandTiming timing)
            where TEvent : struct, IGameEvent
        {
            if (buffer == null || buffSystem == null || gameEvent.FrameNumber <= 0)
                return;

            buffer.AddCommand(new RaiseBuffEventFrameCommand<TEvent>(buffSystem, in gameEvent), timing);
        }
    }
}
