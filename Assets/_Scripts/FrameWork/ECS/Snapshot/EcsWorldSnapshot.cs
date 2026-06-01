using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ECSFrameWork
{

/// <summary>
/// ECS World 的可恢复快照根数据。
/// </summary>
public sealed class EcsWorldSnapshot
{
    public int FrameNumber { get; }
    public IReadOnlyList<Type> RegisteredComponentTypes { get; }
    public EcsEntityManagerSnapshot EntityManager { get; }
    public IReadOnlyList<EcsComponentStoreSnapshot> ComponentStores { get; }
    public IReadOnlyList<EcsSingletonSnapshot> Singletons { get; }

    public EcsWorldSnapshot(int frameNumber, IEnumerable<Type> registeredComponentTypes, EcsEntityManagerSnapshot entityManager, IEnumerable<EcsComponentStoreSnapshot> componentStores, IEnumerable<EcsSingletonSnapshot> singletons)
    {
        FrameNumber = frameNumber;
        RegisteredComponentTypes = CopyAsReadOnly(registeredComponentTypes);
        EntityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
        ComponentStores = CopyAsReadOnly(componentStores);
        Singletons = CopyAsReadOnly(singletons);
    }

    private static ReadOnlyCollection<T> CopyAsReadOnly<T>(IEnumerable<T> source)
    {
        return Array.AsReadOnly(source != null ? source.ToArray() : Array.Empty<T>());
    }
}

}
