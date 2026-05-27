/*
 * 文件说明：
 * WorldSnapshot 用于保存 ECS World 完整运行状态。
 *
 * 设计目标：
 * 1. 捕获当前所有存活 Entity。
 * 2. 保存 Entity 的全部组件状态。
 * 3. 支持完整 ECS 世界恢复。
 * 4. 为 Rollback 提供世界状态回退能力。
 *
 * 设计说明：
 * 当前实现属于完整状态快照方案：
 * - Snapshot 保存整个 World
 * - Restore 时重建全部 Entity
 *
 * 优点：
 * - 实现简单
 * - 状态绝对一致
 * - 易调试
 *
 * 缺点：
 * - Snapshot 成本较高
 * - Entity 数量大时内存开销明显
 *
 * 后续可升级：
 * - 增量 Snapshot
 * - Chunk Snapshot
 * - Component Diff Snapshot
 * - Object Pool Snapshot
 *
 * 使用场景：
 * - RollbackCoordinator
 * - ECS 回滚恢复
 * - 历史状态保存
 */

using ECSFrameWork;
using Simulation.Contracts;
using System.Collections.Generic;

namespace FrameWork.RollBackSystem
{
    public sealed class WorldSnapshot
        : ISnapshot
    {
        /// <summary>
        /// Snapshot 所属逻辑帧。
        /// </summary>
        public int Frame { get; }

        /// <summary>
        /// 保存的 Entity 快照列表。
        /// </summary>
        private readonly List<EntitySnapshotData>
            _entities;

        private WorldSnapshot(
            int frame,
            List<EntitySnapshotData> entities)
        {
            Frame = frame;

            _entities = entities;
        }

        /// <summary>
        /// 捕获当前 ECS 世界状态。
        /// </summary>
        public static WorldSnapshot Capture(
            World world,
            int frame)
        {
            //--------------------------------
            // Collect Alive Entities
            //--------------------------------

            var entities =
                new List<Entity>();

            world.FillAliveEntities(
                entities);

            //--------------------------------
            // Capture Entity Snapshots
            //--------------------------------

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

            //--------------------------------
            // Create Snapshot
            //--------------------------------

            return new WorldSnapshot(
                frame,
                snapshotEntities);
        }

        /// <summary>
        /// 从 Snapshot 恢复 ECS 世界状态。
        /// </summary>
        public static void Restore(
            World world,
            WorldSnapshot snapshot)
        {
            //--------------------------------
            // Destroy Current Entities
            //--------------------------------

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

            //--------------------------------
            // Restore Snapshot Entities
            //--------------------------------

            for (int i = 0;
                 i < snapshot._entities.Count;
                 i++)
            {
                snapshot._entities[i]
                    .Restore(world);
            }
        }

        /// <summary>
        /// 释放 Snapshot 数据。
        /// </summary>
        public void Release()
        {
            _entities.Clear();
        }
    }
}