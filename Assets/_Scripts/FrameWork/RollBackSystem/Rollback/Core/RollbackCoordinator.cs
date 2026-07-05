/*
 * 文件说明：RollbackCoordinator 负责管理输入预测、快照保存、状态回滚与历史帧重模拟。
 * 设计约束：
 * 1. 所有逻辑推进必须严格基于逻辑帧编号。
 * 2. 回滚后必须通过历史输入重新模拟所有后续帧。
 * 3. Snapshot 与 Checksum 必须与逻辑帧保持一致。
 * 4. Coordinator 本身不保存游戏状态，只协调 Buffer 与 World。
 * 5. Simulate() 只写输入和帧命令，World.Tick() 由外部调用。
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

        /// <summary>推进一个新的逻辑帧。只写输入和帧命令，不 Tick。</summary>
        public void Step(TInput input)
        {
            int nextFrame = CurrentFrame + 1;

            _inputBuffer.Save(nextFrame, input);

            _world.Simulate(
                input,
                new SimulationContext(nextFrame, TickLength, false));

            CurrentFrame = nextFrame;
        }

        /// <summary>
        /// 加速追帧：在一个调用中执行多次逻辑 Tick。
        /// 用于客户端落后服务端时快速追上。
        /// </summary>
        /// <param name="count">要执行的 Tick 数</param>
        /// <param name="inputProvider">为每帧提供输入的函数</param>
        public void TickMultiple(int count, Func<int, TInput> inputProvider)
        {
            int actualCount = Math.Min(count, MaxTicksPerUnityFrame);

            for (int i = 0; i < actualCount; i++)
            {
                int nextFrame = CurrentFrame + 1;
                TInput input = inputProvider(nextFrame);
                Step(input);
            }
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
            SaveChecksum();
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

            // 保存回滚前的目标帧号（ReceiveAuthoritativeInput 期间外部不能再调 Step）
            int preRollbackFrame = CurrentFrame;

            bool rollbackSuccess = RollbackTo(frame - 1);

            if (!rollbackSuccess)
            {
                UnityEngine.Debug.LogError(
                    $"[RollbackCoordinator] ReceiveAuthoritativeInput: rollback to frame {frame - 1} failed, skipping resimulate.");
                return;
            }

            _inputBuffer.Save(frame, input);

            ResimulateTo(preRollbackFrame);
        }

        //--------------------------------
        // Rollback
        //--------------------------------

        /// <summary>回滚到指定帧之前的状态（回到该帧执行前）。</summary>
        public bool RollbackTo(int frame)
        {
            bool found =
                _snapshotBuffer.TryGetNearestSnapshot(
                    frame,
                    out var snapshot);

            if (!found)
            {
                UnityEngine.Debug.LogWarning(
                    $"[RollbackCoordinator] RollbackTo failed: no snapshot found for target frame {frame}. " +
                    $"Snapshot range: [{_snapshotBuffer.MinFrame}, {_snapshotBuffer.MaxFrame}].");
                return false;
            }

            _world.Restore(snapshot);

            CurrentFrame = snapshot.Frame;

            return true;
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
        }

        //--------------------------------
        // Resimulate
        //--------------------------------

        /// <summary>使用历史输入重新模拟。每帧写输入，Tick 由外部 onEachFrame 负责。</summary>
        public void ResimulateTo(int targetFrame, Action<TSnapshot> onEachFrame = null)
        {
            ResimulateInternal(targetFrame, onEachFrame);
        }

        void IRollbackSimulation<TInput>.ResimulateTo(int targetFrame)
        {
            ResimulateInternal(targetFrame, null);
        }

        private void ResimulateInternal(int targetFrame, Action<TSnapshot> onEachFrame)
        {
            while (CurrentFrame < targetFrame)
            {
                int nextFrame = CurrentFrame + 1;

                bool found =
                    _inputBuffer.TryGet(nextFrame, out var input);

                if (!found)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[RollbackCoordinator] Resimulate: input missing at frame {nextFrame}, using default input to keep frame alignment.");
                    input = default;
                }

                var context = new SimulationContext(nextFrame, TickLength, true);

                _world.Simulate(input, context);

                _world.Tick(context);

                TSnapshot snapshot =
                    (TSnapshot)_world.Capture(nextFrame);

                if (snapshot != null)
                    _snapshotBuffer.Save(snapshot);

                SaveChecksum();

                CurrentFrame = nextFrame;

                onEachFrame?.Invoke(snapshot);
            }
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
            uint checksum = _world.CalculateChecksum();

            _checksumBuffer.Save(
                new FrameChecksum(CurrentFrame, checksum));
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
