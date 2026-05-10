/*
 * 文件说明：TimeSimulator 是 Unity MonoBehaviour 层的时间驱动器，负责在 Unity Update 中采样输入并推进 SimulateRunner。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using UnityEngine;
using Utility;

/// <summary>
/// Unity 层时间驱动器，负责在 Update 中采样输入并推进 SimulateRunner。
/// </summary>
public class TimeSimulator : Singleton<TimeSimulator>
{
    [SerializeField] private UnityInputAdapter[] inputAdapters;

    private SimulateRunner _runner;
    private InputSnapshotBuffer _inputSnapshotBuffer;
    private WorldInputApplier _worldInputApplier;
    private SimulationFrameCommandApplier _frameCommandApplier;

    /// <summary>在 Unity 每帧 Update 中先采样输入，再推进逻辑帧。</summary>
    private void Update()
    {
        SampleInputAdapters();
        _runner?.Update(Time.deltaTime);
    }

    /// <summary>销毁时解除 Runner 事件订阅，避免场景切换后残留引用。</summary>
    private void OnDestroy()
    {
        UnbindRunnerEvents();
    }

    /// <summary>注入当前要驱动的 SimulateRunner，并把帧数据写入绑定到每个逻辑帧开始前和结束后。</summary>
    public void InitSimulator(SimulateRunner runner)
    {
        UnbindRunnerEvents();

        _runner = runner;

        if (_runner != null)
        {
            _runner.BeforeTick += OnBeforeTick;
            _runner.AfterTick += OnAfterTick;
        }
    }

    /// <summary>设置当前需要采样的输入 Adapter。</summary>
    public void SetInputAdapters(params UnityInputAdapter[] adapters)
    {
        inputAdapters = adapters;
    }

    /// <summary>设置按帧输入缓存和输入应用器；设置后输入会先进入 InputSnapshotBuffer，再按 frameNumber 写入 World。</summary>
    public void SetInputFramePipeline(InputSnapshotBuffer inputSnapshotBuffer, WorldInputApplier worldInputApplier)
    {
        _inputSnapshotBuffer = inputSnapshotBuffer;
        _worldInputApplier = worldInputApplier;
    }

    /// <summary>设置按帧外部指令应用器；每个 Tick 开始前和结束后会分别消费对应时机的指令。</summary>
    public void SetFrameCommandApplier(SimulationFrameCommandApplier frameCommandApplier)
    {
        _frameCommandApplier = frameCommandApplier;
    }

    /// <summary>采样所有输入 Adapter；该方法只读取 Unity 输入，不直接写入 ECS。</summary>
    private void SampleInputAdapters()
    {
        if (inputAdapters == null)
            return;

        for (int i = 0; i < inputAdapters.Length; i++)
        {
            if (inputAdapters[i] != null)
                inputAdapters[i].SampleInput();
        }
    }

    /// <summary>每个逻辑帧开始前，先消费 BeforeTick 外部指令，再提交当前帧输入到 World。</summary>
    private void OnBeforeTick(SimulationContext context)
    {
        ApplyFrameCommands(context.frameNumber, SimulationFrameCommandTiming.BeforeTick);
        ApplyInputFrame(context);
    }

    /// <summary>每个逻辑帧结束后，消费 AfterTick 外部指令；这类修改本帧不可见，下一帧开始可见。</summary>
    private void OnAfterTick(SimulationContext context)
    {
        ApplyFrameCommands(context.frameNumber, SimulationFrameCommandTiming.AfterTick);
    }

    /// <summary>消费指定逻辑帧、指定时机的 Entity、Component、System 外部指令。</summary>
    private void ApplyFrameCommands(int frameNumber, SimulationFrameCommandTiming timing)
    {
        _frameCommandApplier?.ApplyCommandsToWorld(frameNumber, timing);
    }

    /// <summary>把本地输入采样转换成输入快照，再按当前逻辑帧写入 World；没有配置帧输入管线时回退到旧版直接写入。</summary>
    private void ApplyInputFrame(SimulationContext context)
    {
        if (_inputSnapshotBuffer != null && _worldInputApplier != null)
        {
            CollectInputSnapshots(context.frameNumber);
            _worldInputApplier.ApplyInputToWorld(context.frameNumber);
            return;
        }

        WriteInputAdaptersToWorld(context);
    }

    /// <summary>收集所有本地 Adapter 的当前输入，并写入 InputSnapshotBuffer。</summary>
    private void CollectInputSnapshots(int frameNumber)
    {
        if (inputAdapters == null || _inputSnapshotBuffer == null)
            return;

        for (int i = 0; i < inputAdapters.Length; i++)
        {
            if (inputAdapters[i] == null)
                continue;

            PlayerInputSnapshot snapshot = inputAdapters[i].CollectSnapshot(frameNumber);
            _inputSnapshotBuffer.SetInput(in snapshot);
        }
    }

    /// <summary>旧版输入路径：在每个逻辑帧开始前把所有输入 Adapter 的缓存直接写入 ECS。</summary>
    private void WriteInputAdaptersToWorld(SimulationContext context)
    {
        if (inputAdapters == null)
            return;

        for (int i = 0; i < inputAdapters.Length; i++)
        {
            if (inputAdapters[i] != null)
                inputAdapters[i].WriteInputToWorld(context);
        }
    }

    private void UnbindRunnerEvents()
    {
        if (_runner == null)
            return;

        _runner.BeforeTick -= OnBeforeTick;
        _runner.AfterTick -= OnAfterTick;
        _runner = null;
    }
}
