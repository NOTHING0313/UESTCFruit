/*
 * 文件说明：WorldRollbackAdapter 负责把 ECS World 接入 Rollback 系统。
 * 设计约束：
 * 1. Adapter 不保存游戏逻辑状态，只负责桥接 World、输入应用和可选帧命令源。
 * 2. Snapshot 与 Restore 通过 IEcsWorldSnapshotProvider（World 实现）完成。
 * 3. Simulate 只写输入，World.Tick() 由 Runner 或 Coordinator 重模拟路径驱动。
 */

using Contracts;
using ECSFrameWork;
using FrameWork.RollBackSystem.Interfaces;
using Simulation.Contracts;
using System;
using System.Collections.Generic;

namespace FrameWork.RollBackSystem
{
    /// ECS World 回滚适配器。
    public sealed class WorldRollbackAdapter<TInput>
        : IRollbackableWorld<TInput>,
          IRollbackFrameCommandReplay,
          IRollbackWorldRestoreNotifier
    {
        private readonly IEcsWorldSnapshotProvider _snapshotProvider;
        private readonly World _world;
        private readonly IWorldInputApplier<TInput> _inputApplier;
        private readonly IFrameCommandSource _frameCommandSource;
        private readonly List<IRollbackRestoreListener> _restoreListeners = new List<IRollbackRestoreListener>();

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

        bool IRollbackFrameCommandReplay.HasFrameCommandSource => _frameCommandSource != null;

        /// <summary>写入输入到 ECS World；不执行帧命令、不 Tick。</summary>
        public void Simulate(
            TInput input,
            SimulationContext context)
        {
            _inputApplier.Apply(
                _world,
                input);
        }

        /// <summary>执行 World.Tick；帧命令重放由 Coordinator 的重模拟流程显式调用。</summary>
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
            RollbackRestoreResult result = TryRestore(snapshot);
            if (!result.Succeeded)
            {
                UnityEngine.Debug.LogError(
                    $"[WorldRollbackAdapter] Restore failed: {result.FailureKind}, {result.Message}");
            }
        }

        public RollbackRestoreResult TryRestore(ISnapshot snapshot)
        {
            if (snapshot == null)
            {
                return RollbackRestoreResult.Failure(
                    -1,
                    -1,
                    RollbackRestoreFailureKind.NullSnapshot,
                    "Snapshot is null.");
            }

            int requestedFrame = snapshot.Frame;

            if (!(snapshot is EcsWorldSnapshot ecsSnapshot))
            {
                return RollbackRestoreResult.Failure(
                    requestedFrame,
                    -1,
                    RollbackRestoreFailureKind.UnsupportedSnapshotType,
                    $"Unsupported snapshot type: {snapshot.GetType().FullName}.");
            }

            try
            {
                bool restored = _snapshotProvider.TryRestoreSnapshot(
                    ecsSnapshot,
                    out EcsWorldSnapshotRestoreResult result);

                if (!restored)
                {
                    return RollbackRestoreResult.Failure(
                        requestedFrame,
                        -1,
                        RollbackRestoreFailureKind.WorldRestoreFailed,
                        result != null ? result.ErrorMessage : "World restore failed without result.");
                }

                NotifyWorldRestored(ecsSnapshot.Frame);

                return RollbackRestoreResult.Success(
                    requestedFrame,
                    ecsSnapshot.Frame);
            }
            catch (Exception ex)
            {
                return RollbackRestoreResult.Failure(
                    requestedFrame,
                    -1,
                    RollbackRestoreFailureKind.Exception,
                    ex.Message);
            }
        }

        /// <summary>计算状态校验值。</summary>
        public uint CalculateChecksum()
        {
            return WorldChecksumCalculator
                .Calculate(_world);
        }

        internal void AddRollbackRestoreListener(IRollbackRestoreListener listener)
        {
            if (listener == null || _restoreListeners.Contains(listener))
                return;

            _restoreListeners.Add(listener);
        }

        internal bool RemoveRollbackRestoreListener(IRollbackRestoreListener listener)
        {
            return listener != null && _restoreListeners.Remove(listener);
        }

        bool IRollbackFrameCommandReplay.TryReplayFrameCommands(
            SimulationContext context,
            SimulationFrameCommandTiming timing,
            out string message)
        {
            if (_frameCommandSource == null)
            {
                message = "FrameCommand source is null; replay skipped.";
                return false;
            }

            _frameCommandSource.ApplyCommandsAtTiming(
                _world,
                context.frameNumber,
                timing,
                true);

            message = string.Empty;
            return true;
        }

        void IRollbackWorldRestoreNotifier.NotifyRollbackResimulated(int currentFrame)
        {
            for (int i = 0; i < _restoreListeners.Count; i++)
            {
                _restoreListeners[i].OnRollbackResimulated(
                    _world,
                    currentFrame);
            }
        }

        private void NotifyWorldRestored(int restoredFrame)
        {
            for (int i = 0; i < _restoreListeners.Count; i++)
            {
                _restoreListeners[i].OnRollbackWorldRestored(
                    _world,
                    restoredFrame);
            }
        }
    }
}
