/*
 * 文件说明：SystemManager 负责 System 生命周期、执行顺序维护以及 SystemChangeBuffer 播放。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using System.Collections.Generic;
using System.Diagnostics;

namespace ECSFrameWork
{

/// <summary>
/// System 管理器，负责 System 生命周期、执行顺序和延迟增删。
/// </summary>
internal class SystemManager
{
    private readonly List<IFixedStepSystem> _systems;
    private readonly Dictionary<IFixedStepSystem, SystemProfileInfo> _profiles;
    private readonly World _world;
    private readonly SystemChangeBuffer _changeBuffer;
    private bool _isChangingSystems;

    /// <summary>当前注册的 System 数量。</summary>
    public int SystemCount => _systems.Count;

    /// <summary>当前等待播放的 System 变更命令数量。</summary>
    public int PendingSystemCommandCount => _changeBuffer.Count;

    /// <summary>是否启用 System Tick 耗时统计；关闭后 System 仍正常执行，但不会更新 Profile。</summary>
    public bool EnableSystemProfile { get; set; } = true;

    /// <summary>当前持有的 SystemProfileInfo 数量，通常与已注册 System 数量一致。</summary>
    public int ProfileCount => _profiles.Count;

    /// <summary>
    /// 创建 SystemManager，并绑定所属 World。
    /// </summary>
    public SystemManager(World world)
    {
        _world = world;
        _systems = new List<IFixedStepSystem>();
        _profiles = new Dictionary<IFixedStepSystem, SystemProfileInfo>();
        _changeBuffer = new SystemChangeBuffer();
    }

    /// <summary>
    /// 按当前排序依次执行所有 IFixedStepSystem 的 Tick。
    /// 启用 EnableSystemProfile 时，会使用 Stopwatch 记录每个 System 的真实执行耗时。
    /// </summary>
    public void Tick(in SimulationContext context)
    {
        for (int i = 0; i < _systems.Count; i++)
        {
            if (_world == null || _world.IsDisposing())
                break;

            IFixedStepSystem system = _systems[i];

            if (system == null)
                continue;

            TickSystem(system, in context);

            if (_world.IsDisposing())
                break;
        }
    }

    /// <summary>
    /// 执行单个 System，并在启用性能统计时记录真实耗时。
    /// Stopwatch 只用于调试观测，不参与逻辑时间和 ECS 状态。
    /// </summary>
    private void TickSystem(IFixedStepSystem system, in SimulationContext context)
    {
        if (!EnableSystemProfile)
        {
            system.Tick(in context);
            return;
        }

        SystemProfileInfo profile = GetOrCreateProfile(system);
        long startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            system.Tick(in context);
        }
        finally
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            double milliseconds = elapsedTicks * 1000d / Stopwatch.Frequency;
            profile.RecordTick(milliseconds);
        }
    }

    /// <summary>
    /// 在 SystemOperating 阶段播放延迟的 System 增删命令。
    /// </summary>
    public void PlaybackSystemChanges()
    {
        if (_world == null || _world.CurrentState != WorldStates.SystemOperating)
            return;

        _changeBuffer.Playback(this);
    }

    /// <summary>
    /// 添加 System；当前阶段不允许立即修改时写入 SystemChangeBuffer。
    /// </summary>
    public void AddSystem(IFixedStepSystem system)
    {
        if (system == null || _world == null || _world.IsDisposing())
            return;

        if (_isChangingSystems || !_world.CanExcuteSystemImmediately(ExcuteType.Add))
        {
            _changeBuffer.AddSystem(system);
            return;
        }

        AddSystemImmediate(system);
    }

    /// <summary>
    /// 移除 System；当前阶段不允许立即修改时写入 SystemChangeBuffer。
    /// </summary>
    public bool RemoveSystem(IFixedStepSystem system)
    {
        if (system == null || _world == null || _world.IsDisposing())
            return false;

        if (_isChangingSystems || !_world.CanExcuteSystemImmediately(ExcuteType.Remove))
        {
            _changeBuffer.RemoveSystem(system);
            return true;
        }

        return RemoveSystemImmediate(system);
    }

    /// <summary>
    /// 清空所有 System；当前阶段不允许立即修改时写入 SystemChangeBuffer。
    /// </summary>
    public void ClearSystem()
    {
        if (_world == null || _world.IsDisposing())
            return;

        if (_isChangingSystems || !_world.CanExcuteSystemImmediately(ExcuteType.Remove))
        {
            _changeBuffer.ClearSystem();
            return;
        }

        ClearSystemImmediately();
    }

    /// <summary>
    /// 立即添加 System，按 sequence 排序并调用 OnCreate。
    /// </summary>
    internal void AddSystemImmediate(IFixedStepSystem system)
    {
        if (system == null || _world == null || _world.IsDisposing())
            return;

        if (_systems.Contains(system))
            return;

        RearrangeNewSystems(system);
        GetOrCreateProfile(system);

        bool oldChangingState = _isChangingSystems;
        _isChangingSystems = true;

        try
        {
            system.OnCreate(_world);
        }
        finally
        {
            _isChangingSystems = oldChangingState;
        }
    }

    /// <summary>
    /// 立即移除 System，并调用 OnDestroy。
    /// </summary>
    internal bool RemoveSystemImmediate(IFixedStepSystem system)
    {
        if (system == null)
            return false;

        if (!_systems.Contains(system))
            return false;

        bool oldChangingState = _isChangingSystems;
        _isChangingSystems = true;

        try
        {
            system.OnDestroy(_world);
        }
        finally
        {
            _isChangingSystems = oldChangingState;
        }

        _systems.Remove(system);
        _profiles.Remove(system);
        return true;
    }

    /// <summary>
    /// 立即销毁并清空全部 System。
    /// </summary>
    internal void ClearSystemImmediately()
    {
        bool oldChangingState = _isChangingSystems;
        _isChangingSystems = true;

        try
        {
            for (int i = 0; i < _systems.Count; i++)
            {
                _systems[i]?.OnDestroy(_world);
            }
        }
        finally
        {
            _isChangingSystems = oldChangingState;
        }

        _systems.Clear();
        _profiles.Clear();
    }


    /// <summary>
    /// 尝试获取指定 System 的性能统计信息。
    /// </summary>
    public bool TryGetSystemProfile(IFixedStepSystem system, out SystemProfileInfo profile)
    {
        if (system == null)
        {
            profile = null;
            return false;
        }

        return _profiles.TryGetValue(system, out profile);
    }

    /// <summary>
    /// 获取当前所有 System 的性能统计快照列表。
    /// 返回的是新的 List，避免外部修改 SystemManager 内部字典。
    /// </summary>
    public List<SystemProfileInfo> GetSystemProfiles()
    {
        return new List<SystemProfileInfo>(_profiles.Values);
    }

    /// <summary>
    /// 重置所有 System 的性能统计数据，但不移除 Profile 对象。
    /// </summary>
    public void ResetSystemProfiles()
    {
        foreach (SystemProfileInfo profile in _profiles.Values)
            profile?.Reset();
    }

    /// <summary>
    /// 获取或创建指定 System 的性能统计对象。
    /// </summary>
    private SystemProfileInfo GetOrCreateProfile(IFixedStepSystem system)
    {
        if (_profiles.TryGetValue(system, out SystemProfileInfo profile))
            return profile;

        string systemName = system != null ? system.GetType().Name : "UnknownSystem";
        profile = new SystemProfileInfo(systemName);
        _profiles[system] = profile;
        return profile;
    }

    /// <summary>
    /// 清空尚未播放的 SystemChangeBuffer 命令。
    /// </summary>
    internal void ClearPendingSystemChanges()
    {
        _changeBuffer.Clear();
    }

    /// <summary>
    /// 根据 SystemTickSequence 把新 System 插入到正确执行顺序。
    /// </summary>
    private void RearrangeNewSystems(IFixedStepSystem system)
    {
        int index = _systems.FindIndex(s => s.sequence > system.sequence);

        if (index < 0)
        {
            _systems.Add(system);
        }
        else
        {
            _systems.Insert(index, system);
        }
    }
}

}
