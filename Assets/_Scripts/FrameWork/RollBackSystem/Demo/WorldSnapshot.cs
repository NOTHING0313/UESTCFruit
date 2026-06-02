/*
 * [DEPRECATED] 已被 Ecs 层的 EcsWorldSnapshot 替代。
 *
 * EcsWorldSnapshot 位于 Ecs/EcsWorldSnapshot.cs，实现了 ISnapshot 接口，
 * 通过 EcsComponentStoreSnapshot（dense 数组）、EcsEntityManagerSnapshot、
 * EcsSingletonSnapshot 等结构化数据捕获完整 ECS 状态。
 *
 * Capture/Restore 逻辑已迁移至 EcsWorldSnapshot.Capture() / Restore()，
 * 反射恢复逻辑已内联到 EcsWorldSnapshot.RestoreComponentViaReflection()。
 *
 * 保留此文件仅用于参考对比，不可再被编译引用。
 *

using ECSFrameWork;
using Simulation.Contracts;
using System.Collections.Generic;

namespace FrameWork.RollBackSystem
{
    public sealed class WorldSnapshot
        : ISnapshot
    {
        public int Frame { get; }

        private readonly List<EntitySnapshotData>
            _entities;

        private WorldSnapshot(
            int frame,
            List<EntitySnapshotData> entities)
        {
            Frame = frame;
            _entities = entities;
        }

        public static WorldSnapshot Capture(
            World world,
            int frame)
        {
            var entities =
                new List<Entity>();

            world.FillAliveEntities(
                entities);

            var snapshotEntities =
                new List<EntitySnapshotData>();

            for (int i = 0;
                 i < entities.Count;
                 i++)
            {
                snapshotEntities.Add(
                    EntitySnapshotData
                        .Capture(
                            world,
                            entities[i]));
            }

            return new WorldSnapshot(
                frame,
                snapshotEntities);
        }

        public static void Restore(
            World world,
            WorldSnapshot snapshot)
        {
            var alive =
                new List<Entity>();

            world.FillAliveEntities(
                alive);

            for (int i = 0;
                 i < alive.Count;
                 i++)
            {
                world.DestroyEntity(
                    alive[i]);
            }

            for (int i = 0;
                 i < snapshot._entities.Count;
                 i++)
            {
                snapshot._entities[i]
                    .Restore(world);
            }
        }

        public void Release()
        {
            _entities.Clear();
        }
    }
}
*/
