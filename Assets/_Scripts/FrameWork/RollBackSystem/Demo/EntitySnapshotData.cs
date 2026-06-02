/*
 * [DEPRECATED] 已被 Ecs 层的 EcsComponentStoreSnapshot + EcsComponentSnapshot 替代。
 *
 * 旧方案：每个 Entity 保存其组件列表（Entity → Component[]），
 * 按 Entity 维度组织快照数据。
 *
 * 新方案：EcsComponentStoreSnapshot 按 ComponentType 组织 dense 数组，
 * 每个 ComponentStore 持有 DenseComponents: List<EcsComponentSnapshot>，
 * 索引位置与 Entity 一一对应，更贴近 ECS 内部存储结构，恢复效率更高。
 *
 * 保留此文件仅用于参考对比，不可再被编译引用。
 *

using System;
using System.Collections.Generic;

namespace FrameWork.RollBackSystem
{
    [Serializable]
    public sealed class EntitySnapshotData
    {
        public Entity Entity;

        public readonly List<ComponentSnapshotData>
            Components =
                new List<ComponentSnapshotData>();

        public static EntitySnapshotData Capture(
            World world,
            Entity entity)
        {
            var snapshot =
                new EntitySnapshotData();

            snapshot.Entity = entity;

            var componentTypes =
                new List<Type>();

            world.FillEntityComponentTypes(
                entity,
                componentTypes);

            for (int i = 0;
                 i < componentTypes.Count;
                 i++)
            {
                Type componentType =
                    componentTypes[i];

                bool success =
                    world.TryGetComponentDebugValue(
                        entity,
                        componentType,
                        out object component);

                if (!success)
                    continue;

                snapshot.Components.Add(
                    new ComponentSnapshotData(
                        componentType,
                        component));
            }

            return snapshot;
        }

        public void Restore(World world)
        {
            Entity entity =
                world.CreateEntity();

            for (int i = 0;
                 i < Components.Count;
                 i++)
            {
                var component =
                    Components[i];

                ReflectionComponentRestore
                    .RestoreComponent(
                        world,
                        entity,
                        component.ComponentType,
                        component.ComponentValue);
            }
        }
    }
}
*/
