/*
 * 文件说明：RollbackCoordinator 负责管理输入预测、快照保存、状态回滚与历史帧重模拟。
 * 设计约束：
 * 1. 所有逻辑推进必须严格基于逻辑帧编号。
 * 2. 回滚后必须通过历史输入重新模拟所有后续帧。
 * 3. Snapshot 与 Checksum 必须与逻辑帧保持一致。
 * 4. Coordinator 本身不保存游戏状态，只协调 Buffer、World 与 Runner。
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

        /// <summary>
        /// 本地预测输入缓存。
        /// </summary>
        private readonly IInputBuffer<TInput>
            _inputBuffer;

        /// <summary>
        /// 权威输入缓存（服务器输入）。
        /// </summary>
        private readonly AuthoritativeInputBuffer<TInput>
            _authoritativeInputBuffer;

        /// <summary>
        /// 历史快照缓存。
        /// </summary>
        private readonly SnapshotRingBuffer<TSnapshot>
            _snapshotBuffer;

        //--------------------------------
        // Runtime
        //--------------------------------

        /// <summary>
        /// 支持回滚的 ECS World。
        /// </summary>
        private readonly IRollbackableWorld<TInput>
            _world;

        /// <summary>
        /// 用于推进逻辑帧的 Runner 适配器。
        /// </summary>
        private readonly RollbackRunnerAdapter
            _runner;

        //--------------------------------
        // Validation
        //--------------------------------

        /// <summary>
        /// 输入比较器，用于检测预测输入与权威输入差异。
        /// </summary>
        private readonly IInputComparer<TInput>
            _inputComparer;

        /// <summary>
        /// 本地 Checksum 缓存。
        /// </summary>
        private readonly ChecksumBuffer
            _checksumBuffer;

        /// <summary>
        /// 权威 Checksum 缓存。
        /// </summary>
        private readonly AuthoritativeChecksumBuffer
            _authoritativeChecksumBuffer;

        //--------------------------------
        // ctor
        //--------------------------------

        /// <summary>
        /// 创建回滚协调器。
        /// </summary>
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

        /// <summary>
        /// 保存当前逻辑帧的世界快照与 Checksum。
        /// </summary>
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

        /// <summary>
        /// 接收服务器权威输入，并在预测错误时触发回滚。
        /// </summary>
        public void ReceiveAuthoritativeInput(
            int frame,
            in TInput input)
        {
            //--------------------------------
            // Save authoritative
            //--------------------------------

            _authoritativeInputBuffer
                .Save(frame, input);

            //--------------------------------
            // Compare prediction
            //--------------------------------

            bool hasPredicted =
                _inputBuffer.TryGet(
                    frame,
                    out var predictedInput);

            if (!hasPredicted)
            {
                return;
            }

            bool isDifferent =
                !_inputComparer.IsEqual(
                    predictedInput,
                    input);

            if (!isDifferent)
            {
                return;
            }

            //--------------------------------
            // Rollback
            //--------------------------------

            int targetFrame =
                CurrentFrame;

            bool rollbackSuccess =
                RollbackTo(frame);

            if (!rollbackSuccess)
            {
                return;
            }

            //--------------------------------
            // Replace Input
            //--------------------------------

            _inputBuffer.Save(
                frame,
                input);

            //--------------------------------
            // Resimulate
            //--------------------------------

            ResimulateTo(
                targetFrame);
        }

        //--------------------------------
        // Rollback
        //--------------------------------

        /// <summary>
        /// 回滚到指定逻辑帧最近的历史快照。
        /// </summary>
        public bool RollbackTo(int frame)
        {
            bool found =
                _snapshotBuffer
                    .TryGetNearestSnapshot(
                        frame,
                        out var snapshot);

            if (!found)
            {
                return false;
            }

            //--------------------------------
            // Restore Snapshot
            //--------------------------------

            _world.Restore(
                snapshot);

            //--------------------------------
            // Sync Runner
            //--------------------------------

            _runner.SetFrame(
                snapshot.Frame);

            //--------------------------------
            // Update Frame
            //--------------------------------

            CurrentFrame =
                snapshot.Frame;

            return true;
        }

        //--------------------------------
        // Resimulate
        //--------------------------------

        /// <summary>
        /// 使用历史输入重新模拟后续逻辑帧。
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
                {
                    break;
                }

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

        /// <summary>
        /// 计算当前 World 的逻辑状态校验值。
        /// </summary>
        public uint CalculateChecksum()
        {
            return _world
                .CalculateChecksum();
        }

        /// <summary>
        /// 保存当前逻辑帧对应的 Checksum。
        /// </summary>
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

        /// <summary>
        /// 接收服务器权威 Checksum。
        /// </summary>
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

        /// <summary>
        /// 校验指定逻辑帧的本地与权威 Checksum 是否一致。
        /// </summary>
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