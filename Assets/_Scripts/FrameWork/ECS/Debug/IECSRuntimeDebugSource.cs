using UnityEngine;

namespace ECSFrameWork
{
/// <summary>
/// ECS Runtime Inspector 的运行时数据源接口。
/// 任何持有 World / SimulateRunner 的 MonoBehaviour 都可以实现该接口，让 Editor 工具读取调试数据。
/// </summary>
public interface IECSRuntimeDebugSource
{
    /// <summary>当前可调试的 World；没有初始化时返回 null。</summary>
    World DebugWorld { get; }

    /// <summary>当前驱动 World 的 SimulateRunner；没有使用固定帧 Runner 时可以返回 null。</summary>
    SimulateRunner DebugRunner { get; }

    /// <summary>调试源显示名称。</summary>
    string DebugSourceName { get; }
}

/// <summary>
/// 可选的帧命令调试源接口；实现它后 EditorWindow 可以显示 DebugCommand 与 FrameCommand 历史。
/// </summary>
public interface IECSFrameCommandDebugSource
{
    /// <summary>当前帧命令缓冲；用于显示已记录的帧命令历史。</summary>
    SimulationFrameCommandBuffer DebugFrameCommandBuffer { get; }

    /// <summary>当前帧命令应用器；用于显示命令实际执行历史。</summary>
    SimulationFrameCommandApplier DebugFrameCommandApplier { get; }
}

/// <summary>
/// 通用 ECS 调试目标。
/// 如果项目中不是由 TimeSimulator 持有 World，可以把该组件挂到场景对象上，并在启动代码中 Bind 当前 World / Runner。
/// </summary>
public sealed class ECSRuntimeDebugTarget : MonoBehaviour, IECSRuntimeDebugSource, IECSFrameCommandDebugSource
{
    private World _world;
    private SimulateRunner _runner;
    private SimulationFrameCommandBuffer _commandBuffer;
    private SimulationFrameCommandApplier _commandApplier;

    /// <summary>当前可调试的 World。</summary>
    public World DebugWorld => _world;

    /// <summary>当前驱动 World 的 Runner。</summary>
    public SimulateRunner DebugRunner => _runner;

    /// <summary>调试源显示名称。</summary>
    public string DebugSourceName => name;

    /// <summary>当前帧命令缓冲。</summary>
    public SimulationFrameCommandBuffer DebugFrameCommandBuffer => _commandBuffer;

    /// <summary>当前帧命令应用器。</summary>
    public SimulationFrameCommandApplier DebugFrameCommandApplier => _commandApplier;

    /// <summary>绑定当前运行中的 World、Runner 与可选命令管线，供 Runtime Inspector / World Debugger 读取。</summary>
    public void Bind(World world, SimulateRunner runner = null, SimulationFrameCommandBuffer commandBuffer = null, SimulationFrameCommandApplier commandApplier = null)
    {
        _world = world;
        _runner = runner;
        _commandBuffer = commandBuffer;
        _commandApplier = commandApplier;
    }

    /// <summary>解除当前调试绑定。</summary>
    public void Unbind()
    {
        _world = null;
        _runner = null;
        _commandBuffer = null;
        _commandApplier = null;
    }
}
}
