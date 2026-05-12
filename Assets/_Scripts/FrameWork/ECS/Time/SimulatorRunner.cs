/*
 * 文件说明：SimulateRunner 负责把真实时间转换为固定逻辑帧，并在每帧前后派发 BeforeTick / AfterTick。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using System;

namespace ECSFrameWork
{

/// <summary>
/// 固定逻辑帧推进器，负责累计时间并驱动 World.Tick。
/// </summary>
public class SimulateRunner
{
    public event Action<SimulationContext> BeforeTick;
    public event Action<SimulationContext> AfterTick;

    private readonly World _world;
    private readonly float _tickLength;
    private float _tickCounter;
    private int _frameCount;
    private int _executingFrameNumber;
    private bool _isTicking;
    private readonly int _maxCompensationTickCount;

    public World World => _world;
    public int FrameCount => _frameCount;
    public int CurrentFrameNumber => _isTicking ? _executingFrameNumber : _frameCount;
    public int NextFrameNumber => (_isTicking ? _executingFrameNumber : _frameCount) + 1;
    public bool IsTicking => _isTicking;
    public float TickLength => _tickLength;
    public float TickCounter => _tickCounter;

    /// <summary>创建逻辑帧推进器，并设置逻辑帧步长与最大补偿帧数。</summary>
    public SimulateRunner(World world, float tickLength, int maxCompensationTickCount)
    {
        _world = world;
        _tickLength = tickLength > 0 ? tickLength : 0.02f;
        _maxCompensationTickCount = maxCompensationTickCount > 0 ? maxCompensationTickCount : 1;
    }

    /// <summary>累计真实时间并按固定步长推进 World.Tick。</summary>
    public bool Update(float time)
    {
        if (_world == null || time <= 0)
            return false;

        int additionalFrameCount = 0;
        _tickCounter += time;

        while (_tickCounter >= _tickLength)
        {
            _tickCounter -= _tickLength;
            additionalFrameCount++;

            if (additionalFrameCount >= _maxCompensationTickCount)
            {
                _tickCounter = 0;
                break;
            }
        }

        for (int i = 0; i < additionalFrameCount; i++)
            StepNextFrame(false);

        return additionalFrameCount > 0;
    }

    /// <summary>向前推进一帧；可用于测试、手动推进或回滚重放。</summary>
    public bool StepNextFrame(bool isRollback = false)
    {
        if (_world == null)
            return false;

        return TickFrame(_frameCount + 1, isRollback);
    }

    /// <summary>执行指定 frameNumber 的逻辑帧；回滚重放时可直接按历史帧号重新模拟。</summary>
    public bool TickFrame(int frameNumber, bool isRollback = false)
    {
        if (_world == null || frameNumber <= 0 || _isTicking)
            return false;

        _isTicking = true;
        _executingFrameNumber = frameNumber;

        try
        {
            if (frameNumber > _frameCount)
                _frameCount = frameNumber;

            SimulationContext context = new SimulationContext(frameNumber, _tickLength, isRollback);

            BeforeTick?.Invoke(context);
            _world.Tick(in context);
            AfterTick?.Invoke(context);

            return true;
        }
        finally
        {
            _executingFrameNumber = 0;
            _isTicking = false;
        }
    }

    /// <summary>设置当前帧号；恢复 WorldSnapshot 后可用它对齐 Runner 帧号。</summary>
    public void SetFrameCount(int frameCount)
    {
        if (_isTicking)
            return;

        _frameCount = frameCount < 0 ? 0 : frameCount;
        _executingFrameNumber = 0;
        _tickCounter = 0f;
    }
}

/// <summary>
/// 单次逻辑帧执行上下文。
/// </summary>
public readonly struct SimulationContext
{
    public readonly int frameNumber;
    public readonly float tickLength;
    public readonly bool isRollback;

    /// <summary>创建一次逻辑帧执行所需的上下文数据。</summary>
    public SimulationContext(int frameNumber = 0, float tickLength = 0, bool isRollback = false)
    {
        this.frameNumber = frameNumber;
        this.tickLength = tickLength;
        this.isRollback = isRollback;
    }
}

}
