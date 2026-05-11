using UnityEngine;
using ECSFrameWork;   // Entity

namespace Contracts
{
    /// <summary>
    /// 逻辑实体 - 视图绑定接口（4号提供，自身使用）。
    /// 维护 Entity 与 GameObject 的双向映射，供 ViewBridge 查询。
    /// </summary>
    public interface IEntityViewBinder
    {
        int Bind(Entity entity, GameObject view);
        void Unbind(Entity entity);
        bool TryGetView(Entity entity, out GameObject view);
        bool TryGetEntity(int viewId, out Entity entity);
    }
}