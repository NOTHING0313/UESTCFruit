using System;

namespace ECSFrameWork
{

/// <summary>
/// World 层 Singleton 组件索引的快照。
/// </summary>
public sealed class EcsSingletonSnapshot
{
    public Type ComponentType { get; }
    public Entity Entity { get; }

    public EcsSingletonSnapshot(Type componentType, Entity entity)
    {
        ComponentType = componentType ?? throw new ArgumentNullException(nameof(componentType));
        Entity = entity;
    }
}

}
