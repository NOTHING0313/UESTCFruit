/*
 * [DEPRECATED] 已被 Ecs 层的 EcsComponentSnapshot 替代。
 *
 * EcsComponentSnapshot 位于 Ecs/EcsComponentSnapshot.cs，字段完全等价：
 *   - Entity entity
 *   - object ComponentValue
 *
 * 保留此文件仅用于参考对比，不可再被编译引用。
 *

using System;

namespace FrameWork.RollBackSystem
{
    [Serializable]
    public sealed class ComponentSnapshotData
    {
        public Type ComponentType;
        public object ComponentValue;

        public ComponentSnapshotData(
            Type componentType,
            object componentValue)
        {
            ComponentType = componentType;
            ComponentValue = componentValue;
        }
    }
}
*/
