using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ECSFrameWork
{

/// <summary>
/// 单个 ComponentStore 的 dense 顺序组件快照。
/// </summary>
public sealed class EcsComponentStoreSnapshot
{
    public Type ComponentType { get; }
    public int RegisterID { get; }
    public IReadOnlyList<EcsComponentSnapshot> DenseComponents { get; }

    public EcsComponentStoreSnapshot(Type componentType, int registerID, IEnumerable<EcsComponentSnapshot> denseComponents)
    {
        ComponentType = componentType ?? throw new ArgumentNullException(nameof(componentType));
        RegisterID = registerID;
        DenseComponents = CopyAsReadOnly(denseComponents);
    }

    private static ReadOnlyCollection<T> CopyAsReadOnly<T>(IEnumerable<T> source)
    {
        return Array.AsReadOnly(source != null ? source.ToArray() : Array.Empty<T>());
    }
}

}
