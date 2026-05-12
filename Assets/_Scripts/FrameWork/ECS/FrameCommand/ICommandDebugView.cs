namespace ECSFrameWork
{
/// <summary>
/// 为 EditorWindow 和命令历史提供轻量调试摘要；该接口不参与指令执行逻辑。
/// </summary>
public interface ICommandDebugView
{
    /// <summary>命令显示名称；为空时使用运行时类型名。</summary>
    string DebugName { get; }

    /// <summary>命令主要作用目标；没有明确目标时返回 Entity.Invalid。</summary>
    Entity DebugTargetEntity { get; }

    /// <summary>返回一行可读摘要，避免 EditorWindow 直接保存或展开完整命令对象。</summary>
    string GetDebugSummary();
}
}
