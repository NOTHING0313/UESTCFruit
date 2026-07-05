using System;

namespace BuffSystem.EditorTesting
{
    [Serializable]
    internal sealed class BuffSystemEffectTestCaseResult
    {
        public string CaseName;
        public string Category;
        public string Status;
        public string Expected;
        public string Actual;
        public int EffectId;
        public int ApplyCount;
        public int TickCount;
        public int RemoveCount;
        public int RefreshCount;
        public int StackChangedCount;
        public int EventCount;
        public string ExecutionOrderTrace;
        public string ContextSnapshot;
        public int InvariantChecks;
        public string FailureReason;
        public string ExceptionType;
        public string ExceptionStack;
        public double DurationMs;

        public bool IsPassed => Status == BuffSystemEffectTestStatus.Passed;
        public bool IsFailed => Status == BuffSystemEffectTestStatus.Failed;
        public bool IsSkipped => Status == BuffSystemEffectTestStatus.Skipped;
        public bool IsNotSupported => Status == BuffSystemEffectTestStatus.NotSupported;

        public static BuffSystemEffectTestCaseResult FromOutcome(
            string category,
            string caseName,
            string status,
            string expected,
            EffectCaseOutcome outcome,
            double durationMs)
        {
            return new BuffSystemEffectTestCaseResult
            {
                Category = category,
                CaseName = caseName,
                Status = status,
                Expected = expected,
                Actual = outcome.Actual,
                EffectId = outcome.EffectId,
                ApplyCount = outcome.ApplyCount,
                TickCount = outcome.TickCount,
                RemoveCount = outcome.RemoveCount,
                RefreshCount = outcome.RefreshCount,
                StackChangedCount = outcome.StackChangedCount,
                EventCount = outcome.EventCount,
                ExecutionOrderTrace = outcome.ExecutionOrderTrace,
                ContextSnapshot = outcome.ContextSnapshot,
                InvariantChecks = outcome.InvariantChecks,
                FailureReason = outcome.FailureReason,
                DurationMs = durationMs
            };
        }

        public static BuffSystemEffectTestCaseResult Failed(
            string category,
            string caseName,
            string expected,
            string actual,
            int invariantChecks,
            double durationMs,
            Exception exception)
        {
            return new BuffSystemEffectTestCaseResult
            {
                Category = category,
                CaseName = caseName,
                Status = BuffSystemEffectTestStatus.Failed,
                Expected = expected,
                Actual = actual,
                InvariantChecks = invariantChecks,
                DurationMs = durationMs,
                FailureReason = exception != null ? exception.Message : string.Empty,
                ExceptionType = exception != null ? exception.GetType().FullName : string.Empty,
                ExceptionStack = exception != null ? exception.ToString() : string.Empty
            };
        }
    }

    internal static class BuffSystemEffectTestStatus
    {
        internal const string Passed = "PASS";
        internal const string Failed = "FAIL";
        internal const string Skipped = "SKIP";
        internal const string NotSupported = "NOT_SUPPORTED";
    }

    internal sealed class EffectCaseOutcome
    {
        public string Actual = string.Empty;
        public int EffectId;
        public int ApplyCount;
        public int TickCount;
        public int RemoveCount;
        public int RefreshCount;
        public int StackChangedCount;
        public int EventCount;
        public string ExecutionOrderTrace = string.Empty;
        public string ContextSnapshot = string.Empty;
        public int InvariantChecks;
        public string FailureReason = string.Empty;

        public static EffectCaseOutcome Pass(string actual, int invariantChecks, int effectId = 0, IEffectTestCounters counters = null)
        {
            EffectCaseOutcome outcome = new EffectCaseOutcome
            {
                Actual = actual,
                InvariantChecks = invariantChecks,
                EffectId = effectId
            };

            outcome.CopyCounters(counters);
            return outcome;
        }

        public static EffectCaseOutcome NotSupported(string reason, int effectId = 0)
        {
            return new EffectCaseOutcome
            {
                Actual = reason,
                FailureReason = reason,
                EffectId = effectId
            };
        }

        private void CopyCounters(IEffectTestCounters counters)
        {
            if (counters == null)
                return;

            ApplyCount = counters.ApplyCount;
            TickCount = counters.TickCount;
            RemoveCount = counters.RemoveCount;
            RefreshCount = counters.RefreshCount;
            StackChangedCount = counters.StackChangedCount;
            EventCount = counters.EventCount;
            ExecutionOrderTrace = counters.ExecutionOrderTrace;
            ContextSnapshot = counters.ContextSnapshot;
        }
    }

    internal interface IEffectTestCounters
    {
        int ApplyCount { get; }
        int TickCount { get; }
        int RemoveCount { get; }
        int RefreshCount { get; }
        int StackChangedCount { get; }
        int EventCount { get; }
        string ExecutionOrderTrace { get; }
        string ContextSnapshot { get; }
    }
}
