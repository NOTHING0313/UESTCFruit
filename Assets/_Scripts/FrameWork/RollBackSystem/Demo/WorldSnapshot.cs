/*
 * [DEPRECATED] 已被 ECS Core World 的 TryCaptureSnapshot / TryRestoreSnapshot 替代。
 *
 * ECS Core 的 World 已实现 IEcsWorldSnapshotProvider，提供完整的 Snapshot 能力：
 *   World.TryCaptureSnapshot(frame, out snapshot, out result)
 *   World.TryRestoreSnapshot(snapshot, out result)
 *
 * 且已验证 Entity ID/Version/Alive 恢复、ComponentStore dense 顺序恢复、
 * Query/ArcheType 重建、Singleton 映射恢复等（见 ECS_WorldSnapshot_Interface_For_RollBackSystem_Reviewed.md）。
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
