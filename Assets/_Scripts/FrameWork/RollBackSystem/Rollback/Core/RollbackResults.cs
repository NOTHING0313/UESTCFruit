/*
 * 文件说明：RollbackSystem 诊断结果契约。
 * 设计约束：结果只描述单次回滚调用，不进入 ECS Snapshot，也不承担持久化职责。
 */

namespace FrameWork.RollBackSystem
{
    public enum RollbackStepFailureKind
    {
        None = 0,
        FrameMismatch = 1,
        MissingInput = 2,
        AuthoritativeMismatchBeforeTick = 3,
        WorldSimulateFailed = 4,
        SingleInputAppliedToMultiplePlayersBlocked = 5,
        Exception = 100
    }

    public readonly struct RollbackStepResult
    {
        public readonly bool Succeeded;
        public readonly int RequestedFrame;
        public readonly int PreviousFrame;
        public readonly int CurrentFrame;
        public readonly RollbackStepFailureKind FailureKind;
        public readonly string Message;

        private RollbackStepResult(
            bool succeeded,
            int requestedFrame,
            int previousFrame,
            int currentFrame,
            RollbackStepFailureKind failureKind,
            string message)
        {
            Succeeded = succeeded;
            RequestedFrame = requestedFrame;
            PreviousFrame = previousFrame;
            CurrentFrame = currentFrame;
            FailureKind = failureKind;
            Message = message ?? string.Empty;
        }

        public static RollbackStepResult Success(int requestedFrame, int previousFrame, int currentFrame)
        {
            return new RollbackStepResult(true, requestedFrame, previousFrame, currentFrame, RollbackStepFailureKind.None, string.Empty);
        }

        public static RollbackStepResult Failure(
            int requestedFrame,
            int previousFrame,
            int currentFrame,
            RollbackStepFailureKind failureKind,
            string message)
        {
            return new RollbackStepResult(false, requestedFrame, previousFrame, currentFrame, failureKind, message);
        }
    }

    public enum RollbackRestoreFailureKind
    {
        None = 0,
        MissingSnapshot = 1,
        NullSnapshot = 2,
        UnsupportedSnapshotType = 3,
        WorldRestoreFailed = 4,
        Exception = 100
    }

    public readonly struct RollbackRestoreResult
    {
        public readonly bool Succeeded;
        public readonly int RequestedFrame;
        public readonly int RestoredFrame;
        public readonly RollbackRestoreFailureKind FailureKind;
        public readonly string Message;

        private RollbackRestoreResult(
            bool succeeded,
            int requestedFrame,
            int restoredFrame,
            RollbackRestoreFailureKind failureKind,
            string message)
        {
            Succeeded = succeeded;
            RequestedFrame = requestedFrame;
            RestoredFrame = restoredFrame;
            FailureKind = failureKind;
            Message = message ?? string.Empty;
        }

        public static RollbackRestoreResult Success(int requestedFrame, int restoredFrame)
        {
            return new RollbackRestoreResult(true, requestedFrame, restoredFrame, RollbackRestoreFailureKind.None, string.Empty);
        }

        public static RollbackRestoreResult Failure(
            int requestedFrame,
            int restoredFrame,
            RollbackRestoreFailureKind failureKind,
            string message)
        {
            return new RollbackRestoreResult(false, requestedFrame, restoredFrame, failureKind, message);
        }
    }

    public enum RollbackResimulateFailureKind
    {
        None = 0,
        TargetBeforeCurrentFrame = 1,
        MissingPredictedInput = 2,
        WorldSimulateFailed = 3,
        WorldTickFailed = 4,
        SnapshotCaptureFailed = 5,
        ChecksumSaveFailed = 6,
        FrameCommandReplayUnavailable = 7,
        FrameCommandReplayFailed = 8,
        FrameCommandHistoryCleanupFailed = 9,
        Exception = 100
    }

    public readonly struct RollbackResimulateResult
    {
        public readonly bool Succeeded;
        public readonly int TargetFrame;
        public readonly int StartFrame;
        public readonly int CurrentFrame;
        public readonly RollbackResimulateFailureKind FailureKind;
        public readonly string Message;

        private RollbackResimulateResult(
            bool succeeded,
            int targetFrame,
            int startFrame,
            int currentFrame,
            RollbackResimulateFailureKind failureKind,
            string message)
        {
            Succeeded = succeeded;
            TargetFrame = targetFrame;
            StartFrame = startFrame;
            CurrentFrame = currentFrame;
            FailureKind = failureKind;
            Message = message ?? string.Empty;
        }

        public static RollbackResimulateResult Success(int targetFrame, int startFrame, int currentFrame)
        {
            return new RollbackResimulateResult(true, targetFrame, startFrame, currentFrame, RollbackResimulateFailureKind.None, string.Empty);
        }

        public static RollbackResimulateResult Failure(
            int targetFrame,
            int startFrame,
            int currentFrame,
            RollbackResimulateFailureKind failureKind,
            string message)
        {
            return new RollbackResimulateResult(false, targetFrame, startFrame, currentFrame, failureKind, message);
        }
    }
}
