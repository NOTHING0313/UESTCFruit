/*
 * 文件说明：RollbackCoordinator 负责管理输入预测、快照保存、状态回滚与历史帧重模拟。
 * 设计约束：
 * 1. 所有逻辑推进必须严格基于逻辑帧编号。
 * 2. 回滚后必须通过历史输入重新模拟所有后续帧。
 * 3. Snapshot 与 Checksum 必须与逻辑帧保持一致。
 * 4. Coordinator 本身不保存游戏状态，只协调 Buffer 与 World。
 * 5. 正常 TryStep 只写输入，World.Tick() 由 SimulateRunner 调用；重模拟路径才显式 Tick。
 */

using FrameWork.RollBackSystem.Interfaces;
using Simulation.Contracts;
using ECSFrameWork;
using System;

namespace FrameWork.RollBackSystem
{
    public sealed class RollbackCoordinator<TInput, TSnapshot>
        : IRollbackSimulation<TInput>
        where TSnapshot : ISnapshot
    {
        public int CurrentFrame
        {
            get;
            private set;
        }

        public float TickLength { get; set; } = 1f / 60f;

        //--------------------------------
        // Catch-up
        //--------------------------------

        /// <summary>
        /// 每个 Unity 帧最多执行的逻辑 Tick 数，用于加速追帧。
        /// 默认 3，可根据网络延迟动态调整。
        /// </summary>
        public int MaxTicksPerUnityFrame { get; set; } = 3;

        //--------------------------------
        // Buffers
        //--------------------------------

        private readonly IInputBuffer<TInput>
            _inputBuffer;

        private readonly AuthoritativeInputBuffer<TInput>
            _authoritativeInputBuffer;

        private readonly SnapshotRingBuffer<TSnapshot>
            _snapshotBuffer;

        //--------------------------------
        // Runtime
        //--------------------------------

        private readonly IRollbackableWorld<TInput>
            _world;

        //--------------------------------
        // Validation
        //--------------------------------

        private readonly IInputComparer<TInput>
            _inputComparer;

        private readonly ChecksumBuffer
            _checksumBuffer;

        private readonly AuthoritativeChecksumBuffer
            _authoritativeChecksumBuffer;

        //--------------------------------
        // Confirmed Frame
        //--------------------------------

        private int _confirmedFrame = -1;

        /// <summary>服务端已确认的最高帧号，此帧前的预测数据可安全释放。</summary>
        public int ConfirmedFrame => _confirmedFrame;

        //--------------------------------
        // ctor
        //--------------------------------

        public RollbackCoordinator(
            IInputBuffer<TInput> inputBuffer,
            AuthoritativeInputBuffer<TInput> authoritativeInputBuffer,
            SnapshotRingBuffer<TSnapshot> snapshotBuffer,
            IRollbackableWorld<TInput> world,
            IInputComparer<TInput> inputComparer,
            ChecksumBuffer checksumBuffer,
            AuthoritativeChecksumBuffer authoritativeChecksumBuffer)
        {
            _inputBuffer = inputBuffer;
            _authoritativeInputBuffer = authoritativeInputBuffer;
            _snapshotBuffer = snapshotBuffer;
            _world = world;
            _inputComparer = inputComparer;
            _checksumBuffer = checksumBuffer;
            _authoritativeChecksumBuffer = authoritativeChecksumBuffer;
        }

        //--------------------------------
        // Step
        //--------------------------------

        /// <summary>推进一个新的逻辑帧。只写输入，不 Tick。</summary>
        public void Step(TInput input)
        {
            var result = TryStep(CurrentFrame + 1, input);
            if (result.Succeeded)
                return;

            UnityEngine.Debug.LogError(
                $"[RollbackCoordinator] Step failed at frame {result.RequestedFrame}: {result.FailureKind}, {result.Message}");

            throw new InvalidOperationException(
                $"Rollback Step failed at frame {result.RequestedFrame}: {result.FailureKind}. {result.Message}");
        }

        /// <summary>按指定帧号执行正常帧输入准备。成功后 Runner 继续执行 World.Tick。</summary>
        public RollbackStepResult TryStep(int frame, TInput input)
        {
            int previousFrame = CurrentFrame;
            int expectedFrame = previousFrame + 1;

            if (frame != expectedFrame)
            {
                return RollbackStepResult.Failure(
                    frame,
                    previousFrame,
                    CurrentFrame,
                    RollbackStepFailureKind.FrameMismatch,
                    $"Requested frame {frame} does not match expected frame {expectedFrame}.");
            }

            TInput inputToApply = input;
            if (_authoritativeInputBuffer.TryGet(frame, out var authoritativeInput))
            {
                if (!_inputComparer.IsEqual(authoritativeInput, input))
                {
                    UnityEngine.Debug.LogWarning(
                        $"[RollbackCoordinator] TryStep: authoritative input pre-arrived for frame {frame}; using authoritative input before Tick.");
                }

                inputToApply = authoritativeInput;
            }

            try
            {
                _inputBuffer.Save(frame, inputToApply);

                _world.Simulate(
                    inputToApply,
                    new SimulationContext(frame, TickLength, false));

                CurrentFrame = frame;

                return RollbackStepResult.Success(
                    frame,
                    previousFrame,
                    CurrentFrame);
            }
            catch (SingleInputAppliedToMultiplePlayersException ex)
            {
                return RollbackStepResult.Failure(
                    frame,
                    previousFrame,
                    CurrentFrame,
                    RollbackStepFailureKind.SingleInputAppliedToMultiplePlayersBlocked,
                    ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return RollbackStepResult.Failure(
                    frame,
                    previousFrame,
                    CurrentFrame,
                    RollbackStepFailureKind.WorldSimulateFailed,
                    ex.Message);
            }
            catch (Exception ex)
            {
                return RollbackStepResult.Failure(
                    frame,
                    previousFrame,
                    CurrentFrame,
                    RollbackStepFailureKind.Exception,
                    ex.Message);
            }
        }

        /// <summary>
        /// 加速追帧：在一个调用中执行多次逻辑 Tick。
        /// 用于客户端落后服务端时快速追上。
        /// </summary>
        /// <param name="count">要执行的 Tick 数</param>
        /// <param name="inputProvider">为每帧提供输入的函数</param>
        public void TickMultiple(int count, Func<int, TInput> inputProvider)
        {
            UnityEngine.Debug.LogWarning(
                "[RollbackCoordinator] TickMultiple is disabled for production logic-only closure. " +
                "Runner must remain the unique normal-frame driver; catch-up requires a later owner API.");
        }

        //--------------------------------
        // Snapshot
        //--------------------------------

        public void SaveSnapshot()
        {
            TSnapshot snapshot =
                (TSnapshot)_world.Capture(CurrentFrame);

            if (snapshot == null)
                return;

            _snapshotBuffer.Save(snapshot);
            SaveChecksum(CurrentFrame);
        }

        //--------------------------------
        // Receive Authoritative Input
        //--------------------------------

        public void ReceiveAuthoritativeInput(
            int frame,
            TInput input)
        {
            //--------------------------------
            // 输入时序校验
            //--------------------------------

            // 过时帧丢弃：权威输入帧号已过确认帧
            if (frame <= _confirmedFrame)
            {
                UnityEngine.Debug.LogWarning(
                    $"[RollbackCoordinator] ReceiveAuthoritativeInput: stale frame {frame} discarded (confirmed={_confirmedFrame}).");
                return;
            }

            // 重复帧检测：同一帧收到多次权威输入
            if (_authoritativeInputBuffer.TryGet(frame, out var existingInput))
            {
                if (!_inputComparer.IsEqual(existingInput, input))
                {
                    UnityEngine.Debug.LogWarning(
                        $"[RollbackCoordinator] ReceiveAuthoritativeInput: duplicate authoritative input for frame {frame} with different content (possible network duplicate packet).");
                }
                else
                {
                    UnityEngine.Debug.Log(
                        $"[RollbackCoordinator] ReceiveAuthoritativeInput: duplicate authoritative input for frame {frame} (identical, ignoring).");
                    return;
                }
            }

            _authoritativeInputBuffer.Save(frame, input);

            bool hasPredicted =
                _inputBuffer.TryGet(frame, out var predictedInput);

            if (!hasPredicted)
                return;

            bool isDifferent =
                !_inputComparer.IsEqual(predictedInput, input);

            if (!isDifferent)
                return;

            if (frame > CurrentFrame)
            {
                UnityEngine.Debug.LogWarning(
                    $"[RollbackCoordinator] ReceiveAuthoritativeInput: mismatch for future frame {frame} stored; TryStep will resolve before Tick.");
                return;
            }

            // 保存回滚前的目标帧号（ReceiveAuthoritativeInput 期间外部不能再调 Step）
            int preRollbackFrame = CurrentFrame;

            RollbackRestoreResult rollbackResult = TryRollbackTo(frame - 1);

            if (!rollbackResult.Succeeded)
            {
                UnityEngine.Debug.LogError(
                    $"[RollbackCoordinator] ReceiveAuthoritativeInput: rollback to frame {frame - 1} failed ({rollbackResult.FailureKind}), skipping resimulate. {rollbackResult.Message}");
                return;
            }

            _inputBuffer.Save(frame, input);

            RollbackResimulateResult resimulateResult = TryResimulateTo(preRollbackFrame);
            if (!resimulateResult.Succeeded)
            {
                UnityEngine.Debug.LogError(
                    $"[RollbackCoordinator] ReceiveAuthoritativeInput: resimulate to frame {preRollbackFrame} failed ({resimulateResult.FailureKind}). {resimulateResult.Message}");
            }
        }

        //--------------------------------
        // Rollback
        //--------------------------------

        /// <summary>回滚到指定帧之前的状态（回到该帧执行前）。</summary>
        public bool RollbackTo(int frame)
        {
            RollbackRestoreResult result = TryRollbackTo(frame);
            if (!result.Succeeded)
            {
                UnityEngine.Debug.LogWarning(
                    $"[RollbackCoordinator] RollbackTo failed for frame {frame}: {result.FailureKind}, {result.Message}");
            }

            return result.Succeeded;
        }

        public RollbackRestoreResult TryRollbackTo(int frame)
        {
            bool found =
                _snapshotBuffer.TryGetNearestSnapshot(
                    frame,
                    out var snapshot);

            if (!found)
            {
                return RollbackRestoreResult.Failure(
                    frame,
                    CurrentFrame,
                    RollbackRestoreFailureKind.MissingSnapshot,
                    $"No snapshot found for target frame {frame}. Snapshot range: [{_snapshotBuffer.MinFrame}, {_snapshotBuffer.MaxFrame}].");
            }

            RollbackRestoreResult restoreResult = _world.TryRestore(snapshot);
            if (!restoreResult.Succeeded)
            {
                return RollbackRestoreResult.Failure(
                    frame,
                    restoreResult.RestoredFrame,
                    restoreResult.FailureKind,
                    restoreResult.Message);
            }

            CurrentFrame = restoreResult.RestoredFrame;

            return RollbackRestoreResult.Success(
                frame,
                CurrentFrame);
        }

        //--------------------------------
        // Confirm Frame
        //--------------------------------

        /// <summary>
        /// 标记某帧已被服务端确认。释放该帧前所有缓存的输入、快照、Checksum。
        /// 应在收到服务端确认消息时调用。
        /// </summary>
        public void ConfirmFrame(int frame)
        {
            if (frame <= _confirmedFrame)
            {
                UnityEngine.Debug.LogWarning(
                    $"[RollbackCoordinator] ConfirmFrame ignored: frame {frame} <= already confirmed frame {_confirmedFrame}.");
                return;
            }

            UnityEngine.Debug.Log(
                $"[RollbackCoordinator] ConfirmFrame: advancing confirmed frame from {_confirmedFrame} to {frame}.");

            _confirmedFrame = frame;

            // 清理各缓冲区中指定帧之前的数据
            _inputBuffer.ClearBefore(frame);
            _authoritativeInputBuffer.ClearBefore(frame);
            _checksumBuffer.ClearBefore(frame);
            _authoritativeChecksumBuffer.ClearBefore(frame);
            _snapshotBuffer.ClearBefore(frame);

            if (!TryCleanupFrameCommandHistory(frame, out string message))
            {
                UnityEngine.Debug.LogWarning(
                    $"[RollbackCoordinator] ConfirmFrame frame command cleanup failed: {message}");
            }
        }

        //--------------------------------
        // Resimulate
        //--------------------------------

        /// <summary>使用历史输入重新模拟。兼容旧调用方，失败时 fail-fast 记录。</summary>
        public void ResimulateTo(int targetFrame, Action<TSnapshot> onEachFrame = null)
        {
            RollbackResimulateResult result = ResimulateInternal(targetFrame, onEachFrame);
            if (result.Succeeded)
                return;

            UnityEngine.Debug.LogError(
                $"[RollbackCoordinator] ResimulateTo failed at target {targetFrame}: {result.FailureKind}, {result.Message}");
        }

        void IRollbackSimulation<TInput>.ResimulateTo(int targetFrame)
        {
            RollbackResimulateResult result = ResimulateInternal(targetFrame, null);
            if (!result.Succeeded)
            {
                UnityEngine.Debug.LogError(
                    $"[RollbackCoordinator] IRollbackSimulation.ResimulateTo failed at target {targetFrame}: {result.FailureKind}, {result.Message}");
            }
        }

        public RollbackResimulateResult TryResimulateTo(int targetFrame)
        {
            return ResimulateInternal(targetFrame, null);
        }

        private RollbackResimulateResult ResimulateInternal(int targetFrame, Action<TSnapshot> onEachFrame)
        {
            int startFrame = CurrentFrame;

            if (targetFrame < CurrentFrame)
            {
                return RollbackResimulateResult.Failure(
                    targetFrame,
                    startFrame,
                    CurrentFrame,
                    RollbackResimulateFailureKind.TargetBeforeCurrentFrame,
                    $"Target frame {targetFrame} is before current frame {CurrentFrame}.");
            }

            while (CurrentFrame < targetFrame)
            {
                int nextFrame = CurrentFrame + 1;

                bool foundAuthoritative =
                    _authoritativeInputBuffer.TryGet(nextFrame, out var authoritativeInput);

                bool foundPredicted =
                    _inputBuffer.TryGet(nextFrame, out var predictedInput);

                if (!foundAuthoritative && !foundPredicted)
                {
                    return RollbackResimulateResult.Failure(
                        targetFrame,
                        startFrame,
                        CurrentFrame,
                        RollbackResimulateFailureKind.MissingPredictedInput,
                        $"Input missing at frame {nextFrame}; resimulation stopped instead of using default input.");
                }

                TInput input = foundAuthoritative ? authoritativeInput : predictedInput;

                var context = new SimulationContext(nextFrame, TickLength, true);

                try
                {
                    _world.Simulate(input, context);
                }
                catch (Exception ex)
                {
                    return RollbackResimulateResult.Failure(
                        targetFrame,
                        startFrame,
                        CurrentFrame,
                        RollbackResimulateFailureKind.WorldSimulateFailed,
                        ex.Message);
                }

                if (!TryReplayFrameCommands(
                    context,
                    SimulationFrameCommandTiming.BeforeTick,
                    targetFrame,
                    startFrame,
                    out RollbackResimulateResult beforeTickReplayFailure))
                {
                    return beforeTickReplayFailure;
                }

                try
                {
                    _world.Tick(context);
                }
                catch (Exception ex)
                {
                    return RollbackResimulateResult.Failure(
                        targetFrame,
                        startFrame,
                        CurrentFrame,
                        RollbackResimulateFailureKind.WorldTickFailed,
                        ex.Message);
                }

                if (!TryReplayFrameCommands(
                    context,
                    SimulationFrameCommandTiming.AfterTick,
                    targetFrame,
                    startFrame,
                    out RollbackResimulateResult afterTickReplayFailure))
                {
                    return afterTickReplayFailure;
                }

                TSnapshot snapshot =
                    (TSnapshot)_world.Capture(nextFrame);

                if (snapshot == null)
                {
                    return RollbackResimulateResult.Failure(
                        targetFrame,
                        startFrame,
                        CurrentFrame,
                        RollbackResimulateFailureKind.SnapshotCaptureFailed,
                        $"Snapshot capture failed at frame {nextFrame}.");
                }

                _snapshotBuffer.Save(snapshot);

                try
                {
                    SaveChecksum(nextFrame);
                }
                catch (Exception ex)
                {
                    return RollbackResimulateResult.Failure(
                        targetFrame,
                        startFrame,
                        CurrentFrame,
                        RollbackResimulateFailureKind.ChecksumSaveFailed,
                        ex.Message);
                }

                CurrentFrame = nextFrame;

                onEachFrame?.Invoke(snapshot);
            }

            if (_world is IRollbackWorldRestoreNotifier notifier)
                notifier.NotifyRollbackResimulated(CurrentFrame);

            return RollbackResimulateResult.Success(
                targetFrame,
                startFrame,
                CurrentFrame);
        }

        //--------------------------------
        // Checksum
        //--------------------------------

        /// <summary>
        /// Checksum 漂移检测事件。当本地 Checksum 与权威 Checksum 不匹配时触发。
        /// 参数：frame, localChecksum, authoritativeChecksum
        /// </summary>
        public event Action<int, uint, uint> OnChecksumDrift;

        public uint CalculateChecksum()
        {
            return _world.CalculateChecksum();
        }

        private void SaveChecksum()
        {
            SaveChecksum(CurrentFrame);
        }

        private void SaveChecksum(int frame)
        {
            uint checksum = _world.CalculateChecksum();

            _checksumBuffer.Save(
                new FrameChecksum(frame, checksum));
        }

        private bool TryReplayFrameCommands(
            SimulationContext context,
            SimulationFrameCommandTiming timing,
            int targetFrame,
            int startFrame,
            out RollbackResimulateResult failureResult)
        {
            failureResult = default(RollbackResimulateResult);

            if (!(_world is IRollbackFrameCommandReplay replay))
            {
                failureResult = RollbackResimulateResult.Failure(
                    targetFrame,
                    startFrame,
                    CurrentFrame,
                    RollbackResimulateFailureKind.FrameCommandReplayUnavailable,
                    "World does not expose frame command replay boundary.");
                return false;
            }

            if (!replay.HasFrameCommandSource)
            {
                failureResult = RollbackResimulateResult.Failure(
                    targetFrame,
                    startFrame,
                    CurrentFrame,
                    RollbackResimulateFailureKind.FrameCommandReplayUnavailable,
                    "FrameCommand replay binding is unavailable.");
                return false;
            }

            if (replay.TryReplayFrameCommands(context, timing, out string message))
                return true;

            failureResult = RollbackResimulateResult.Failure(
                targetFrame,
                startFrame,
                CurrentFrame,
                RollbackResimulateFailureKind.FrameCommandReplayFailed,
                string.IsNullOrEmpty(message)
                    ? $"FrameCommand replay failed at frame {context.frameNumber}, timing {timing}."
                    : message);
            return false;
        }

        private bool TryCleanupFrameCommandHistory(int frame, out string message)
        {
            if (_world is IRollbackFrameCommandHistoryCleaner cleaner)
                return cleaner.TryRemoveFrameCommandsBefore(frame, out message);

            message = "World does not expose frame command history cleanup boundary.";
            return false;
        }

        //--------------------------------
        // Authoritative Checksum
        //--------------------------------

        public void ReceiveAuthoritativeChecksum(
            int frame,
            uint checksum)
        {
            _authoritativeChecksumBuffer.Save(
                new FrameChecksum(frame, checksum));
        }

        public ChecksumComparisonResult
            VerifyChecksum(int frame)
        {
            bool hasLocal =
                _checksumBuffer.TryGet(frame, out var localChecksum);

            if (!hasLocal)
            {
                return new ChecksumComparisonResult(false, frame, 0, 0);
            }

            bool hasAuthoritative =
                _authoritativeChecksumBuffer.TryGet(
                    frame,
                    out var authoritativeChecksum);

            if (!hasAuthoritative)
            {
                return new ChecksumComparisonResult(
                    false, frame, localChecksum.Value, 0);
            }

            bool isMatch =
                localChecksum.Value == authoritativeChecksum.Value;

            if (!isMatch)
            {
                UnityEngine.Debug.LogWarning(
                    $"[RollbackCoordinator] Checksum drift detected at frame {frame}: " +
                    $"local=0x{localChecksum.Value:X8}, authoritative=0x{authoritativeChecksum.Value:X8}.");

                OnChecksumDrift?.Invoke(
                    frame, localChecksum.Value, authoritativeChecksum.Value);
            }

            return new ChecksumComparisonResult(
                isMatch, frame, localChecksum.Value, authoritativeChecksum.Value);
        }
    }
}
