using System;

namespace BuffSystem.EditorTesting
{
    [Serializable]
    internal sealed class BuffSystemFunctionalCoverageCaseResult
    {
        public string Category;
        public string CaseName;
        public string Status;
        public string Expected;
        public string Actual;
        public int InvariantChecks;
        public string FailureReason;
        public string ExceptionType;
        public string ExceptionStack;
        public double DurationMs;

        public bool IsPassed => Status == BuffSystemFunctionalCoverageStatus.Passed;
        public bool IsFailed => Status == BuffSystemFunctionalCoverageStatus.Failed;
        public bool IsSkipped => Status == BuffSystemFunctionalCoverageStatus.Skipped;

        public static BuffSystemFunctionalCoverageCaseResult Passed(string category, string caseName, string expected, string actual, int invariantChecks, double durationMs)
        {
            return new BuffSystemFunctionalCoverageCaseResult
            {
                Category = category,
                CaseName = caseName,
                Status = BuffSystemFunctionalCoverageStatus.Passed,
                Expected = expected,
                Actual = actual,
                InvariantChecks = invariantChecks,
                DurationMs = durationMs
            };
        }

        public static BuffSystemFunctionalCoverageCaseResult Failed(string category, string caseName, string expected, string actual, int invariantChecks, double durationMs, Exception exception)
        {
            return new BuffSystemFunctionalCoverageCaseResult
            {
                Category = category,
                CaseName = caseName,
                Status = BuffSystemFunctionalCoverageStatus.Failed,
                Expected = expected,
                Actual = actual,
                InvariantChecks = invariantChecks,
                DurationMs = durationMs,
                FailureReason = exception != null ? exception.Message : string.Empty,
                ExceptionType = exception != null ? exception.GetType().FullName : string.Empty,
                ExceptionStack = exception != null ? exception.ToString() : string.Empty
            };
        }

        public static BuffSystemFunctionalCoverageCaseResult Skipped(string category, string caseName, string expected, string actual, string reason)
        {
            return new BuffSystemFunctionalCoverageCaseResult
            {
                Category = category,
                CaseName = caseName,
                Status = BuffSystemFunctionalCoverageStatus.Skipped,
                Expected = expected,
                Actual = actual,
                FailureReason = reason
            };
        }
    }

    internal static class BuffSystemFunctionalCoverageStatus
    {
        internal const string Passed = "PASS";
        internal const string Failed = "FAIL";
        internal const string Skipped = "SKIP";
    }
}
