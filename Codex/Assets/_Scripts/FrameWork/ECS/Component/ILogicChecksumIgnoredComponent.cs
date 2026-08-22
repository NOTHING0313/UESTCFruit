namespace ECSFrameWork
{
/// <summary>
/// 标记不参与逻辑 Checksum 的组件。
/// 组件仍可进入 Rollback Snapshot；用于保存恢复表现所需的稳定数据描述，不包含 Unity 对象引用。
/// </summary>
public interface ILogicChecksumIgnoredComponent
{
}
}
