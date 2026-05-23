using FrameWork.RollBackSystem.Interfaces;

namespace FrameWork.RollBackSystem
{
    public sealed class RollbackCoordinator<TInput, TSnapshot>
        : IRollbackSimulation<TInput>
        where TSnapshot : ISnapshot
    {
        public int CurrentFrame { get; private set; }

        private readonly IInputBuffer<TInput> _inputBuffer;

        private readonly AuthoritativeInputBuffer<TInput>
            _authoritativeInputBuffer;

        private readonly SnapshotRingBuffer<TSnapshot>
            _snapshotBuffer;

        private readonly IRollbackableWorld<TInput>
            _world;

        private readonly IInputComparer<TInput>
            _inputComparer;
        private readonly ChecksumBuffer _checksumBuffer;

        private readonly AuthoritativeChecksumBuffer _authoritativeChecksumBuffer;

        private readonly SimulationContext
    _simulationContext;
        public RollbackCoordinator(
            IInputBuffer<TInput> inputBuffer,
            AuthoritativeInputBuffer<TInput> authoritativeInputBuffer,
            SnapshotRingBuffer<TSnapshot> snapshotBuffer,
            IRollbackableWorld<TInput> world,
            IInputComparer<TInput> inputComparer,
            ChecksumBuffer checksumBuffer,
            AuthoritativeChecksumBuffer authoritativeChecksumBuffer,
            SimulationContext simulationContext)
        {
            _inputBuffer = inputBuffer;

            _authoritativeInputBuffer =
                authoritativeInputBuffer;

            _snapshotBuffer = snapshotBuffer;

            _world = world;

            _inputComparer = inputComparer;
            _checksumBuffer = checksumBuffer;
            _authoritativeChecksumBuffer = authoritativeChecksumBuffer;
            _simulationContext = simulationContext;
        }

        public void Step(in TInput input)
        {
            _simulationContext.SetFrame(
                CurrentFrame);

            _simulationContext.SetRollback(false);

            _inputBuffer.Save(CurrentFrame, input);

            _world.Simulate(input);

            CurrentFrame++;
        }
        public void SaveSnapshot()
        {
            var snapshot =
                (TSnapshot)_world
                    .Capture(CurrentFrame);

            _snapshotBuffer.Save(snapshot);

            SaveChecksum();
        }

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

            int targetFrame = CurrentFrame;

            bool rollbackSuccess =
                RollbackTo(frame);
            ResimulateTo(frame);

            if (!rollbackSuccess)
            {
                return;
            }

            _inputBuffer.Save(frame, input);

            ResimulateTo(targetFrame);
        }

        public bool RollbackTo(int frame)
        {
            bool found = _snapshotBuffer.TryGetNearestSnapshot(
    frame,
    out var snapshot);

            if (!found)
            {
                return false;
            }

            _world.Restore(snapshot);

            CurrentFrame = snapshot.Frame;

            return true;
        }

        public void ResimulateTo(int targetFrame)
        {
            while (CurrentFrame < targetFrame)
            {
                _simulationContext.SetFrame(
    CurrentFrame);

                _simulationContext.SetRollback(true);
                bool found =
                    _inputBuffer.TryGet(
                        CurrentFrame,
                        out var input);

                if (!found)
                {
                    break;
                }

                _world.Simulate(input);

                var snapshot =
                    (TSnapshot)_world
                        .Capture(CurrentFrame);

                _snapshotBuffer.Save(snapshot);
                SaveChecksum();
                CurrentFrame++;
            }
        }

        public uint CalculateChecksum()
        {
            return _world.CalculateChecksum();
        }
        private void SaveChecksum()
        {
            uint checksum =
                _world.CalculateChecksum();

            var frameChecksum =
                new FrameChecksum(
                    CurrentFrame,
                    checksum);

            _checksumBuffer.Save(frameChecksum);
        }
        public void ReceiveAuthoritativeChecksum(
    int frame,
    uint checksum)
        {
            var authoritativeChecksum =
                new FrameChecksum(
                    frame,
                    checksum);

            _authoritativeChecksumBuffer
                .Save(authoritativeChecksum);
        }
        public ChecksumComparisonResult
    VerifyChecksum(int frame)
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
                _authoritativeChecksumBuffer.TryGet(
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