namespace ECSFrameWork
{
/// <summary>
/// 标记不属于 Rollback 权威状态的瞬时组件。
/// 该类组件保留类型注册 ID，但不进入 Rollback Snapshot 与逻辑 Checksum。
/// </summary>
public interface IRollbackTransientComponent : ILogicChecksumIgnoredComponent
{
}
}
