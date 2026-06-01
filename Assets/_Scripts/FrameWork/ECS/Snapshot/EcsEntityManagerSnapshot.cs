using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ECSFrameWork
{

/// <summary>
/// EntityManager 的实体槽位与 ID 复用状态快照。
/// </summary>
public sealed class EcsEntityManagerSnapshot
{
    public int DataCount { get; }
    public IReadOnlyList<EcsEntitySlotSnapshot> Slots { get; }
    public IReadOnlyList<int> FreeIdsInPopOrder { get; }

    public EcsEntityManagerSnapshot(int dataCount, IEnumerable<EcsEntitySlotSnapshot> slots, IEnumerable<int> freeIdsInPopOrder)
    {
        DataCount = dataCount;
        Slots = CopyAsReadOnly(slots);
        FreeIdsInPopOrder = CopyAsReadOnly(freeIdsInPopOrder);
    }

    private static ReadOnlyCollection<T> CopyAsReadOnly<T>(IEnumerable<T> source)
    {
        return Array.AsReadOnly(source != null ? source.ToArray() : Array.Empty<T>());
    }
}

}
