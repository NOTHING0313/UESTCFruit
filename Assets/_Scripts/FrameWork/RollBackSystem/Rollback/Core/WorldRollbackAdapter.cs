/*
 * 文件说明：WorldRollbackAdapter 负责把 ECS World 接入 Rollback 系统。
 * 设计约束：
 * 1. Adapter 不保存游戏逻辑状态，只负责桥接 World、输入应用和帧命令源。
 * 2. Snapshot 与 Restore 通过 IEcsWorldSnapshotProvider（World 实现）完成。
 * 3. Simulate 只写输入和帧命令，World.Tick() 由外部 Runner 驱动。
 */

using Contracts;
using ECSFrameWork;
using FrameWork.RollBackSystem.Interfaces;
using Simulation.Contracts;

namespace FrameWork.RollBackSystem
{
    /// ECS World 回滚适配器。
    public sealed class WorldRollbackAdapter<TInput>
        : IRollbackableWorld<TInput>
    {
        private readonly IEcsWorldSnapshotProvider _snapshotProvider;
        private readonly World _world;
        private readonly IWorldInputApplier<TInput> _inputApplier;
        private readonly IFrameCommandSource _frameCommandSource;

        /// <summary>创建 ECS 回滚适配器。</summary>
        public WorldRollbackAdapter(
            IEcsWorldSnapshotProvider snapshotProvider,
            World world,
            IWorldInputApplier<TInput> inputApplier,
            IFrameCommandSource frameCommandSource = null)
        {
            _snapshotProvider = snapshotProvider;
            _world = world;
            _inputApplier = inputApplier;
            _frameCommandSource = frameCommandSource;
        }

        /// <summary>写入帧命令和输入到 ECS World。</summary>
        public void Simulate(
            TInput input,
            SimulationContext context)
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
        }

        /// <summary>直接执行 World.Tick。</summary>
        public void Tick(SimulationContext context)
        {
            _world.Tick(context);
        }

        /// <summary>
        /// 捕获当前 ECS World 快照。
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
        /// </summary>
        public void Restore(ISnapshot snapshot)
        {
            if (snapshot == null)
                return;

            var ecsSnapshot = (EcsWorldSnapshot)snapshot;

            _snapshotProvider.TryRestoreSnapshot(
                ecsSnapshot,
                out _);
        }

        /// <summary>计算状态校验值。</summary>
        public uint CalculateChecksum()
        {
            return WorldChecksumCalculator
                .Calculate(_world);
        }
    }
}
