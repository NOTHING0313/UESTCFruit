/*
 * 文件说明：
 * ISnapshotable 定义对象支持快照捕获与恢复能力。
 *
 * 设计目标：
 * 1. 统一世界状态保存接口。
 * 2. 解耦 RollbackCoordinator 与具体 ECS 实现。
 * 3. 支持任意 Snapshot 类型。
 *
 * 使用场景：
 * - WorldRollbackAdapter
 * - ECS World 回滚恢复
 * - Snapshot 捕获系统
 */

namespace Simulation.Contracts
{
    public interface ISnapshotable<TSnapshot>
    {
        /// <summary>
        /// 捕获当前运行状态快照。
        /// </summary>
        TSnapshot Capture(int frame);

        /// <summary>
        /// 从快照恢复运行状态。
        /// </summary>
        void Restore(TSnapshot snapshot);
    }
}