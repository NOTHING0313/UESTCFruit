namespace ECSFrameWork
{

/// <summary>
/// 单个组件值及其所属 Entity 的快照。
/// </summary>
public sealed class EcsComponentSnapshot
{
    public Entity Entity { get; }
    public object ComponentValue { get; }

    public EcsComponentSnapshot(Entity entity, object componentValue)
    {
        Entity = entity;
        ComponentValue = componentValue;
    }
}

}
