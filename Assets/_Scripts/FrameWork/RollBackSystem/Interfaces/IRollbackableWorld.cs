/*
 * 文件说明：IRollbackableWorld 定义支持回滚模拟的世界接口，负责逻辑推进、快照恢复与校验。
 * 设计约束：World 必须保证相同输入与相同快照下可得到一致逻辑结果。
 */

using ECSFrameWork;
using FrameWork.RollBackSystem;
using Simulation.Contracts;

namespace FrameWork.RollBackSystem.Interfaces
{
    public interface IRollbackableWorld<TInput>
        : ISnapshotable<ISnapshot>,
          ISimulationChecksum
    {
        void Simulate(
            TInput input,
            SimulationContext context);

        void Tick(
            SimulationContext context);

        RollbackRestoreResult TryRestore(
            ISnapshot snapshot);
    }
}
