/*
 * 文件说明：
 * EntitySnapshotData 用于保存单个 Entity 的完整组件状态。
 *
 * 设计目标：
 * 1. 保存 Entity 当前所有组件。
 * 2. 支持完整 Entity 状态恢复。
 * 3. 用于 WorldSnapshot 捕获 ECS 世界。
 *
 * 使用场景：
 * - WorldSnapshot
 * - Rollback Restore
 * - ECS 状态回滚
 */

using ECSFrameWork;
using System;
using System.Collections.Generic;

namespace FrameWork.RollBackSystem
{
    [Serializable]
    public sealed class EntitySnapshotData
    {
        /// <summary>
        /// 快照对应实体。
        /// </summary>
        public Entity Entity;

        /// <summary>
        /// 实体组件快照列表。
        /// </summary>
        public readonly List<ComponentSnapshotData>
            Components =
                new List<ComponentSnapshotData>();

        /// <summary>
        /// 捕获 Entity 当前组件状态。
        /// </summary>
        public static EntitySnapshotData Capture(
            World world,
            Entity entity)
        {
            var snapshot =
                new EntitySnapshotData();

            snapshot.Entity = entity;

            //--------------------------------
            // Get Component Types
            //--------------------------------

            var componentTypes =
                new List<Type>();

            world.FillEntityComponentTypes(
                entity,
                componentTypes);

            //--------------------------------
            // Capture Components
            //--------------------------------

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

        /// <summary>
        /// 恢复 Entity 与组件状态。
        /// </summary>
        public void Restore(World world)
        {
            //--------------------------------
            // Recreate Entity
            //--------------------------------

            Entity entity =
                world.CreateEntity();

            //--------------------------------
            // Restore Components
            //--------------------------------

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