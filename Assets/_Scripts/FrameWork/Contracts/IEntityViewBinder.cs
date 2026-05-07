using UnityEngine;
using ECS;   // EntityHandle

namespace Contracts
{
    /// <summary>
    /// 逻辑实体 - 视图绑定接口（4号提供，自身使用）。
    /// 维护 EntityHandle 与 GameObject 的双向映射，供 ViewBridge 查询。
    /// </summary>
    public interface IEntityViewBinder
    {
        int Bind(EntityHandle entity, GameObject view);
        void Unbind(EntityHandle entity);
        bool TryGetView(EntityHandle entity, out GameObject view);
        bool TryGetEntity(int viewId, out EntityHandle entity);
    }
}