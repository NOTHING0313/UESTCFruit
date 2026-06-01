using UnityEngine;
using ECSFrameWork;   // Entity

namespace Contracts
{
    /// <summary>
    /// 逻辑实体 - 视图绑定接口（4号提供，自身使用）。
    /// 维护 Entity 与 viewID 的双向映射，供 ViewBridge 查询。
    /// </summary>
    public interface IEntityViewBinder
    {
        /// <summary>绑定实体与 viewID，若已有绑定则先解绑。</summary>
        int Bind(Entity entity, int viewId);

        /// <summary>解除实体与视图的绑定。</summary>
        void Unbind(Entity entity);

        /// <summary>根据实体获取对应的 GameObject。</summary>
        bool TryGetView(Entity entity, out GameObject view);

        /// <summary>根据 viewID 获取对应的实体。</summary>
        bool TryGetEntity(int viewId, out Entity entity);
    }
}