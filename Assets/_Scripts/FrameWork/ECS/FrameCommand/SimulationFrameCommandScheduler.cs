namespace ECSFrameWork
{
/*
 * 文件说明：SimulationFrameCommandScheduler 基于 SimulateRunner 当前帧号提供相对帧调度 API，避免外部调用者手动计算 frameNumber。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

/// <summary>
/// 为 Tick 外部调用者提供基于当前模拟帧的帧指令调度入口，避免业务层手动计算 frameNumber。
/// </summary>
public sealed class SimulationFrameCommandScheduler
{
    private readonly SimulateRunner _runner;
    private readonly SimulationFrameCommandBuffer _commandBuffer;

    public int DefaultDelayFrames { get; set; }

    /// <summary>创建帧指令调度器。</summary>
    public SimulationFrameCommandScheduler(SimulateRunner runner, SimulationFrameCommandBuffer commandBuffer, int defaultDelayFrames = 0)
    {
        _runner = runner;
        _commandBuffer = commandBuffer;
        DefaultDelayFrames = defaultDelayFrames < 0 ? 0 : defaultDelayFrames;
    }

    /// <summary>添加一条下一逻辑帧开始前执行的指令。</summary>
    public void AddNextFrameStart(ISimulationFrameCommand command)
    {
        AddAfterFrames(0, SimulationFrameCommandTiming.BeforeTick, command);
    }

    /// <summary>添加一条下一逻辑帧末尾执行的指令。</summary>
    public void AddNextFrameEnd(ISimulationFrameCommand command)
    {
        AddAfterFrames(0, SimulationFrameCommandTiming.AfterTick, command);
    }

    /// <summary>添加一条当前 Tick 末尾执行的指令；若当前没有 Tick 正在执行，则退化为下一逻辑帧末尾执行。</summary>
    public void AddCurrentFrameEndOrNextFrameEnd(ISimulationFrameCommand command)
    {
        if (_runner == null || _commandBuffer == null || command == null)
            return;

        int targetFrame = _runner.IsTicking ? _runner.CurrentFrameNumber : _runner.NextFrameNumber;
        ISimulationFrameCommand targetCommand = RebuildCommand(command, targetFrame);
        _commandBuffer.AddCommand(targetCommand, SimulationFrameCommandTiming.AfterTick);
    }

    /// <summary>按默认输入延迟添加一条逻辑帧开始前执行的指令，适合帧同步模式下的本地外部指令。</summary>
    public void AddWithDefaultDelay(ISimulationFrameCommand command)
    {
        AddAfterFrames(DefaultDelayFrames, SimulationFrameCommandTiming.BeforeTick, command);
    }

    /// <summary>添加一条延迟若干逻辑帧后执行的指令；delayFrames 为 0 表示下一逻辑帧。</summary>
    public void AddAfterFrames(int delayFrames, SimulationFrameCommandTiming timing, ISimulationFrameCommand command)
    {
        if (_runner == null || _commandBuffer == null || command == null)
            return;

        int delay = delayFrames < 0 ? 0 : delayFrames;
        int targetFrame = _runner.NextFrameNumber + delay;
        ISimulationFrameCommand targetCommand = RebuildCommand(command, targetFrame);
        _commandBuffer.AddCommand(targetCommand, timing);
    }

    /// <summary>添加一条指定逻辑帧执行的指令，通常给网络同步、回滚重放或测试使用。</summary>
    public void AddAtFrame(int frameNumber, SimulationFrameCommandTiming timing, ISimulationFrameCommand command)
    {
        if (_commandBuffer == null || command == null || frameNumber <= 0)
            return;

        ISimulationFrameCommand targetCommand = RebuildCommand(command, frameNumber);
        _commandBuffer.AddCommand(targetCommand, timing);
    }

    private ISimulationFrameCommand RebuildCommand(ISimulationFrameCommand command, int frameNumber)
    {
        if (command is IRebuildableSimulationFrameCommand rebuildable)
            return rebuildable.Rebuild(frameNumber);

        return new ScheduledSimulationFrameCommand(frameNumber, command);
    }

    private sealed class ScheduledSimulationFrameCommand : ISimulationFrameCommand, ICommandDebugView
    {
        private readonly ISimulationFrameCommand _innerCommand;

        public int FrameNumber { get; }
        public string DebugName => _innerCommand is ICommandDebugView debugView && !string.IsNullOrEmpty(debugView.DebugName) ? debugView.DebugName : "ScheduledSimulationFrameCommand";
        public Entity DebugTargetEntity => _innerCommand is ICommandDebugView debugView ? debugView.DebugTargetEntity : Entity.Invalid;

        public ScheduledSimulationFrameCommand(int frameNumber, ISimulationFrameCommand innerCommand)
        {
            FrameNumber = frameNumber;
            _innerCommand = innerCommand;
        }

        /// <summary>返回被调度命令的调试摘要。</summary>
        public string GetDebugSummary()
        {
            if (_innerCommand is ICommandDebugView debugView)
                return $"Scheduled Frame={FrameNumber}, Inner={debugView.GetDebugSummary()}";

            return _innerCommand != null ? $"Scheduled Frame={FrameNumber}, Inner={_innerCommand.GetType().Name}" : $"Scheduled Frame={FrameNumber}, Inner=null";
        }

        public void Execute(World world)
        {
            _innerCommand?.Execute(world);
        }
    }
}

/// <summary>
/// 支持由调度器重建目标帧号的帧指令。
/// </summary>
public interface IRebuildableSimulationFrameCommand : ISimulationFrameCommand
{
    ISimulationFrameCommand Rebuild(int frameNumber);
}

}
