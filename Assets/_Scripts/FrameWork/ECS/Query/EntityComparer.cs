using System.Collections.Generic;

namespace ECSFrameWork
{

/// <summary>
/// Entity 稳定排序器，先按 ID，再按 Version。
/// </summary>
internal sealed class EntityComparer : IComparer<Entity>
{
    public static readonly EntityComparer Instance = new EntityComparer();
    private EntityComparer()
    {

    }

    public int Compare(Entity x, Entity y)
    {
        int idCompare = x.ID.CompareTo(y.ID);

        if (idCompare != 0)
            return idCompare;

        return x.Version.CompareTo(y.Version);
    }
}

}
