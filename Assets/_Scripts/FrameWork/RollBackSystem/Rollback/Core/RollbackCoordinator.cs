/*
 * 文件说明：RollbackCoordinator 负责管理输入预测、快照保存、状态回滚与历史帧重模拟。
 * 设计约束：
 * 1. 所有逻辑推进必须严格基于逻辑帧编号。
 * 2. 回滚后必须通过历史输入重新模拟所有后续帧。
 * 3. Snapshot 与 Checksum 必须与逻辑帧保持一致。
 * 4. Coordinator 本身不保存游戏状态，只协调 Buffer、World 与 Runner。
 * 5. 帧命令回放由 WorldRollbackAdapter.Simulate() 在 Tick 前根据 context.isRollback
 *    分发 IFrameCommandSource.ReplayCommandsToWorld / ApplyCommandsToWorld，
 *    RollbackCoordinator 不直接持有 World，因此不在此处调用帧命令。
 */

using FrameWork.RollBackSystem.Interfaces;
using Simulation.Contracts;

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

        private readonly RollbackRunnerAdapter
            _runner;

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
            RollbackRunnerAdapter runner,
            IInputComparer<TInput> inputComparer,
            ChecksumBuffer checksumBuffer,
            AuthoritativeChecksumBuffer authoritativeChecksumBuffer)
        {
            _inputBuffer =
                inputBuffer;

            _authoritativeInputBuffer =
                authoritativeInputBuffer;

            _snapshotBuffer =
                snapshotBuffer;

            _world =
                world;

            _runner =
                runner;

            _inputComparer =
                inputComparer;

            _checksumBuffer =
                checksumBuffer;

            _authoritativeChecksumBuffer =
                authoritativeChecksumBuffer;
        }

        //--------------------------------
        // Step
        //--------------------------------

        /// <summary>
        /// 推进一个新的逻辑帧。
        /// 帧命令的 Apply 由 WorldRollbackAdapter.Simulate() 在 Tick 前完成。
        /// </summary>
        public void Step(TInput input)
        {
            int nextFrame =
                CurrentFrame + 1;

            //--------------------------------
            // Save Input
            //--------------------------------

            _inputBuffer.Save(
                nextFrame,
                input);

            //--------------------------------
            // Tick
            //--------------------------------

            _runner.TickFrame(
                nextFrame,
                false);

            //--------------------------------
            // Update Frame
            //--------------------------------

            CurrentFrame =
                nextFrame;
        }

        //--------------------------------
        // Snapshot
        //--------------------------------

        public void SaveSnapshot()
        {
            TSnapshot snapshot =
                (TSnapshot)_world
                    .Capture(CurrentFrame);

            _snapshotBuffer.Save(
                snapshot);

            SaveChecksum();
        }

        //--------------------------------
        // Receive Authoritative Input
        //--------------------------------

        public void ReceiveAuthoritativeInput(
            int frame,
            in TInput input)
        {
            _authoritativeInputBuffer
                .Save(frame, input);

            bool hasPredicted =
                _inputBuffer.TryGet(
                    frame,
                    out var predictedInput);

            if (!hasPredicted)
                return;

            bool isDifferent =
                !_inputComparer.IsEqual(
                    predictedInput,
                    input);

            if (!isDifferent)
                return;

            int targetFrame =
                CurrentFrame;

            bool rollbackSuccess =
                RollbackTo(frame);

            if (!rollbackSuccess)
                return;

            _inputBuffer.Save(
                frame,
                input);

            ResimulateTo(
                targetFrame);
        }

        //--------------------------------
        // Rollback
        //--------------------------------

        public bool RollbackTo(int frame)
        {
            bool found =
                _snapshotBuffer
                    .TryGetNearestSnapshot(
                        frame,
                        out var snapshot);

            if (!found)
                return false;

            _world.Restore(
                snapshot);

            _runner.SetFrame(
                snapshot.Frame);

            CurrentFrame =
                snapshot.Frame;

            return true;
        }

        //--------------------------------
        // Resimulate
        //--------------------------------

        /// <summary>
        /// 使用历史输入重新模拟后续逻辑帧。
        /// 帧命令的 Replay 由 WorldRollbackAdapter.Simulate() 在 Tick 前完成。
        /// </summary>
        public void ResimulateTo(
            int targetFrame)
        {
            while (CurrentFrame < targetFrame)
            {
                int nextFrame =
                    CurrentFrame + 1;

                //--------------------------------
                // Get Input
                //--------------------------------

                bool found =
                    _inputBuffer.TryGet(
                        nextFrame,
                        out var input);

                if (!found)
                    break;

                //--------------------------------
                // Tick Rollback Frame
                //--------------------------------

                _runner.TickFrame(
                    nextFrame,
                    true);

                //--------------------------------
                // Save Snapshot
                //--------------------------------

                TSnapshot snapshot =
                    (TSnapshot)_world
                        .Capture(nextFrame);

                _snapshotBuffer.Save(
                    snapshot);

                //--------------------------------
                // Save Checksum
                //--------------------------------

                SaveChecksum();

                //--------------------------------
                // Update Frame
                //--------------------------------

                CurrentFrame =
                    nextFrame;
            }
        }

        //--------------------------------
        // Checksum
        //--------------------------------

        public uint CalculateChecksum()
        {
            return _world
                .CalculateChecksum();
        }

        private void SaveChecksum()
        {
            uint checksum =
                _world.CalculateChecksum();

            FrameChecksum frameChecksum =
                new FrameChecksum(
                    CurrentFrame,
                    checksum);

            _checksumBuffer.Save(
                frameChecksum);
        }

        //--------------------------------
        // Authoritative Checksum
        //--------------------------------

        public void ReceiveAuthoritativeChecksum(
            int frame,
            uint checksum)
        {
            FrameChecksum frameChecksum =
                new FrameChecksum(
                    frame,
                    checksum);

            _authoritativeChecksumBuffer
                .Save(frameChecksum);
        }

        public ChecksumComparisonResult
            VerifyChecksum(
                int frame)
        {
            bool hasLocal =
                _checksumBuffer.TryGet(
                    frame,
                    out var localChecksum);

            if (!hasLocal)
            {
                return new ChecksumComparisonResult(
                    false,
                    frame,
                    0,
                    0);
            }

            bool hasAuthoritative =
                _authoritativeChecksumBuffer
                    .TryGet(
                        frame,
                        out var authoritativeChecksum);

            if (!hasAuthoritative)
            {
                return new ChecksumComparisonResult(
                    false,
                    frame,
                    localChecksum.Value,
                    0);
            }

            bool isMatch =
                localChecksum.Value ==
                authoritativeChecksum.Value;

            return new ChecksumComparisonResult(
                isMatch,
                frame,
                localChecksum.Value,
                authoritativeChecksum.Value);
        }
    }
}
