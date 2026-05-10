/// <summary>
/// World 逻辑事件标记接口。
/// 事件用于描述某一逻辑帧中发生的一次性结果，不应长期保存为状态。
/// </summary>
public interface IWorldEvent
{
    /// <summary>事件发生的逻辑帧编号。</summary>
    int frameNumber { get; }
}
