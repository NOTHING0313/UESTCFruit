/*
 * 文件说明：WorldRollbackAdapter 负责把 ECS World 接入 Rollback 系统。
 * 设计约束：
 * 1. Adapter 不保存游戏逻辑状态，只负责桥接 World、Runner 与输入应用。
 * 2. Snapshot 与 Restore 通过 IEcsWorldSnapshotProvider（World 实现）完成。
 * 3. Simulate 必须保证相同输入与相同快照下得到一致结果。
 *
 * 联通说明：
 * - IEcsWorldSnapshotProvider 提供 TryCaptureSnapshot / TryRestoreSnapshot，
 *   ECS Core 已验证 Entity ID/Version/ComponentStore/Singleton 恢复。
 * - 通过接口而非具体 World 类型对接快照，将依赖面限制在 Snapshot 能力上。
 * - World 类型仍保留用于 Tick、ApplyComponent 等非快照操作。
 */

using Contracts;
using ECSFrameWork;
using FrameWork.RollBackSystem.Interfaces;
using Simulation.Contracts;

namespace FrameWork.RollBackSystem
{
    /// <summary>
    /// ECS World 回滚适配器。
    /// </summary>
    public sealed class WorldRollbackAdapter<TInput>
        : IRollbackableWorld<TInput>
    {
        private readonly IEcsWorldSnapshotProvider _snapshotProvider;
        private readonly World _world;
        private readonly SimulateRunner _runner;
        private readonly IWorldInputApplier<TInput> _inputApplier;
        private readonly IFrameCommandSource _frameCommandSource;

        /// <summary>
        /// 创建 ECS 回滚适配器。
        /// </summary>
        /// <param name="snapshotProvider">ECS 快照提供者，通常就是 World 实例</param>
        /// <param name="world">ECS World，用于 Tick、ApplyComponent 等</param>
        public WorldRollbackAdapter(
            IEcsWorldSnapshotProvider snapshotProvider,
            World world,
            SimulateRunner runner,
            IWorldInputApplier<TInput> inputApplier,
            IFrameCommandSource frameCommandSource = null)
        {
            _snapshotProvider = snapshotProvider;
            _world = world;
            _runner = runner;
            _inputApplier = inputApplier;
            _frameCommandSource = frameCommandSource;
        }

        /// <summary>
        /// 执行一次逻辑帧模拟。
        /// </summary>
        public void Simulate(
            TInput input,
            in SimulationContext context)
        {
            if (context.isRollback)
            {
                _frameCommandSource?.ReplayCommandsToWorld(
                    _world,
                    context.frameNumber);
            }
            else
            {
                _frameCommandSource?.ApplyCommandsToWorld(
                    _world,
                    context.frameNumber);
            }

            _inputApplier.Apply(
                _world,
                input);

            _runner.TickFrame(
                context.frameNumber,
                context.isRollback);
        }

        /// <summary>
        /// 捕获当前 ECS World 快照。
        /// 通过 IEcsWorldSnapshotProvider 调用，将依赖面限制在 Snapshot 能力上。
        /// </summary>
        public ISnapshot Capture(int frame)
        {
            if (!_snapshotProvider.TryCaptureSnapshot(
                    frame,
                    out EcsWorldSnapshot snapshot,
                    out EcsWorldSnapshotCaptureResult result))
            {
                return null;
            }

            return snapshot;
        }

        /// <summary>
        /// 从历史快照恢复 ECS World。
        /// Restore 成功后 ECS 会清空 WorldEventBuffer。
        /// </summary>
        public void Restore(ISnapshot snapshot)
        {
            if (snapshot == null)
                return;

            var ecsSnapshot = (EcsWorldSnapshot)snapshot;

            if (!_snapshotProvider.TryRestoreSnapshot(
                    ecsSnapshot,
                    out EcsWorldSnapshotRestoreResult result))
            {
                return;
            }

            _runner.SetFrameCount(
                ecsSnapshot.FrameNumber);
        }

        /// <summary>
        /// 计算当前 ECS World 状态校验值。
        /// </summary>
        public uint CalculateChecksum()
        {
            return WorldChecksumCalculator
                .Calculate(_world);
        }
    }
}
