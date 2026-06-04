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
                new SimulationContext(nextFrame, 0f, false));

            CurrentFrame = nextFrame;
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
                return;

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
                return false;

            _world.Restore(snapshot);

            CurrentFrame = snapshot.Frame;

            return true;
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
                    break;

                _world.Simulate(
                    input,
                    new SimulationContext(nextFrame, 0f, true));

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

            return new ChecksumComparisonResult(
                isMatch, frame, localChecksum.Value, authoritativeChecksum.Value);
        }
    }
}
