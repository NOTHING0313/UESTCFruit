using Contracts;
using ECS;
using UnityEngine;

namespace View
{
    /// <summary>
    /// 实体 - 视图绑定器空壳（4号实现，后续填充）。
    /// 维护 EntityHandle 与 GameObject 的双向映射。
    /// </summary>
    public sealed class EntityViewBinder : IEntityViewBinder
    {
        public int Bind(EntityHandle entity, GameObject view) => entity.ID;
        public void Unbind(EntityHandle entity) { }
        public bool TryGetView(EntityHandle entity, out GameObject view)
        {
            view = null;
            return false;
        }
        public bool TryGetEntity(int viewId, out EntityHandle entity)
        {
            entity = default;
            return false;
        }
    }
}