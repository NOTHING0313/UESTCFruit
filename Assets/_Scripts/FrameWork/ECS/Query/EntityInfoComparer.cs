using System.Collections.Generic;

/// <summary>
/// EntityInfo 稳定排序器，先按 ID，再按 Version。
/// </summary>
public sealed class EntityInfoComparer : IComparer<EntityInfo>
{
    public static readonly EntityInfoComparer Instance = new EntityInfoComparer();
    private EntityInfoComparer()
    {

    }

    public int Compare(EntityInfo x, EntityInfo y)
    {
        int idCompare = x.ID.CompareTo(y.ID);

        if (idCompare != 0)
            return idCompare;

        return x.Version.CompareTo(y.Version);
    }
}
