namespace ECSFrameWork
{
/*
 * 文件说明：Entity 句柄、版本号和实体运行时数据。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

/// <summary>
/// Entity 运行时元数据，保存存活状态、版本号和组件 Mask。
/// </summary>
internal class EntityData
{
    private bool _isAlive;
    private int _version;
    private ComponentMask256 _archeType;

    public bool isAlive => _isAlive;
    public int Version => _version;
    public ComponentMask256 ArcheType => _archeType;

    /// <summary>
    /// 刷新实体版本号，用于区分复用后的实体 ID。
    /// </summary>
    public void RefreshVersion()
    {
        _version++;
    }

    /// <summary>
    /// 设置实体底层数据是否处于存活状态。
    /// </summary>
    public void SetAlive(bool isAlive)
    {
        _isAlive = isAlive;
    }

    /// <summary>
    /// 把指定组件类型加入实体当前 ArcheType Mask。
    /// </summary>
    public void SetMask(int componentTypeId)
    {
        _archeType.Set(componentTypeId);
    }

    /// <summary>
    /// 从实体当前 ArcheType Mask 中移除指定组件类型。
    /// </summary>
    public void RemoveMask(int componentTypeId)
    {
        _archeType.Clear(componentTypeId);
    }

    /// <summary>
    /// 清空实体当前 ArcheType Mask。
    /// </summary>
    public void ClearMask()
    {
        _archeType.Clear();
    }
}

}
