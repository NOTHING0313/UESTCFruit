/*
 * 文件说明：WorldRollbackAdapter 负责把 ECS World 接入 Rollback 系统。
 * 设计约束：
 * 1. Adapter 不保存游戏逻辑状态，只负责桥接 World、Runner 与输入应用。
 * 2. Snapshot 与 Restore 必须完整恢复 ECS 世界状态。
 * 3. Simulate 必须保证相同输入与相同快照下得到一致结果。
 *
 * 联通说明：
 * 使用 Ecs 层的 EcsWorldSnapshot（实现 ISnapshot）替代旧的 Demo 层 WorldSnapshot，
 * EcsWorldSnapshot 直接通过 EcsComponentStoreSnapshot / EcsEntityManagerSnapshot 等
 * 结构化数据捕获完整 ECS 状态，Capture/Restore 逻辑内聚在 EcsWorldSnapshot 自身。
 */

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
        /// <summary>
        /// ECS World。
        /// </summary>
        private readonly World _world;

        /// <summary>
        /// 固定帧推进器。
        /// </summary>
        private readonly SimulateRunner _runner;

        /// <summary>
        /// 输入应用器，用于把输入写入 ECS。
        /// </summary>
        private readonly IWorldInputApplier<TInput>
            _inputApplier;

        /// <summary>
        /// 帧命令回放源。非必需：不提供时帧命令回放被跳过。
        /// </summary>
        private readonly IFrameCommandSource
            _frameCommandSource;

        /// <summary>
        /// 创建 ECS 回滚适配器。
        /// </summary>
        public WorldRollbackAdapter(
            World world,
            SimulateRunner runner,
            IWorldInputApplier<TInput> inputApplier,
            IFrameCommandSource frameCommandSource = null)
        {
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
            //--------------------------------
            // Replay / Apply Frame Commands
            //--------------------------------

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

            //--------------------------------
            // Apply Input
            //--------------------------------

            _inputApplier.Apply(
                _world,
                input);

            //--------------------------------
            // Tick World
            //--------------------------------

            _runner.TickFrame(
                context.frameNumber,
                context.isRollback);
        }

        /// <summary>
        /// 捕获当前 ECS World 快照。
        /// 使用 Ecs 层的 EcsWorldSnapshot。
        /// </summary>
        public ISnapshot Capture(int frame)
        {
            return EcsWorldSnapshot.Capture(
                _world,
                frame);
        }

        /// <summary>
        /// 从历史快照恢复 ECS World。
        /// 使用 Ecs 层的 EcsWorldSnapshot。
        /// </summary>
        public void Restore(ISnapshot snapshot)
        {
            EcsWorldSnapshot.Restore(
                _world,
                (EcsWorldSnapshot)snapshot);

            _runner.SetFrameCount(
                snapshot.Frame);
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
