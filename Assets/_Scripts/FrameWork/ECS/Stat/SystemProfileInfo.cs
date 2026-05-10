/// <summary>
/// 单个 System 的 Tick 耗时统计信息。
/// 该对象只用于 Debug、性能观测和测试，不参与 ECS 逻辑结果。
/// </summary>
public sealed class SystemProfileInfo
{
    private double _totalMilliseconds;

    /// <summary>System 类型名称。</summary>
    public readonly string systemName;

    /// <summary>最近一次 Tick 的真实执行耗时，单位毫秒。</summary>
    public double lastMilliseconds { get; private set; }

    /// <summary>历史 Tick 中的最大真实执行耗时，单位毫秒。</summary>
    public double maxMilliseconds { get; private set; }

    /// <summary>历史 Tick 的平均真实执行耗时，单位毫秒。</summary>
    public double averageMilliseconds => tickCount <= 0 ? 0d : _totalMilliseconds / tickCount;

    /// <summary>已经记录过的 Tick 次数。</summary>
    public int tickCount { get; private set; }

    /// <summary>创建 System 性能统计信息。</summary>
    public SystemProfileInfo(string systemName)
    {
        this.systemName = string.IsNullOrEmpty(systemName) ? "UnknownSystem" : systemName;
    }

    /// <summary>记录一次 System Tick 的耗时。</summary>
    internal void RecordTick(double milliseconds)
    {
        if (milliseconds < 0d)
            milliseconds = 0d;

        lastMilliseconds = milliseconds;
        _totalMilliseconds += milliseconds;
        tickCount++;

        if (milliseconds > maxMilliseconds)
            maxMilliseconds = milliseconds;
    }

    /// <summary>重置统计数据，但保留该 Profile 对象。</summary>
    public void Reset()
    {
        lastMilliseconds = 0d;
        maxMilliseconds = 0d;
        _totalMilliseconds = 0d;
        tickCount = 0;
    }

    /// <summary>返回便于 Debug.Log 查看的一行统计文本。</summary>
    public override string ToString()
    {
        return $"SystemProfileInfo(Name={systemName}, Last={lastMilliseconds:F4}ms, Avg={averageMilliseconds:F4}ms, Max={maxMilliseconds:F4}ms, TickCount={tickCount})";
    }
}
