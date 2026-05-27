/*
 * 文件说明：
 * ISnapshot 定义回滚系统中的世界快照协议。
 *
 * 设计目标：
 * 1. 抽象所有可用于回滚恢复的快照对象。
 * 2. 统一 SnapshotRingBuffer 的存储结构。
 * 3. 支持不同类型世界快照。
 * 4. 为未来对象池复用预留 Release 生命周期。
 *
 * 使用场景：
 * - WorldSnapshot
 * - BuffSnapshot
 * - SnapshotRingBuffer
 * - RollbackCoordinator
 */

namespace Simulation.Contracts
{
    public interface ISnapshot
    {
        /// <summary>
        /// 快照对应的逻辑帧号。
        /// </summary>
        int Frame { get; }

        /// <summary>
        /// 释放快照资源。
        /// </summary>
        void Release();
    }
}