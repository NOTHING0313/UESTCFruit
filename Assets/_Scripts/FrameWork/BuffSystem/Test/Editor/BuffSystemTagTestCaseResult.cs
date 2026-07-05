using System;

namespace BuffSystem.EditorTesting
{
    [Serializable]
    internal sealed class BuffSystemTagTestCaseResult
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
        public string TagApiAvailability;

        public bool IsPassed => Status == BuffSystemTagTestStatus.Passed;
        public bool IsFailed => Status == BuffSystemTagTestStatus.Failed;
        public bool IsSkipped => Status == BuffSystemTagTestStatus.Skipped;
        public bool IsNotSupported => Status == BuffSystemTagTestStatus.NotSupported;

        public static BuffSystemTagTestCaseResult Passed(
            string category,
            string caseName,
            string expected,
            string actual,
            int invariantChecks,
            double durationMs,
            string tagApiAvailability)
        {
            return new BuffSystemTagTestCaseResult
            {
                Category = category,
                CaseName = caseName,
                Status = BuffSystemTagTestStatus.Passed,
                Expected = expected,
                Actual = actual,
                InvariantChecks = invariantChecks,
                DurationMs = durationMs,
                TagApiAvailability = tagApiAvailability
            };
        }

        public static BuffSystemTagTestCaseResult Failed(
            string category,
            string caseName,
            string expected,
            string actual,
            int invariantChecks,
            double durationMs,
            Exception exception,
            string tagApiAvailability)
        {
            return new BuffSystemTagTestCaseResult
            {
                Category = category,
                CaseName = caseName,
                Status = BuffSystemTagTestStatus.Failed,
                Expected = expected,
                Actual = actual,
                InvariantChecks = invariantChecks,
                DurationMs = durationMs,
                FailureReason = exception != null ? exception.Message : string.Empty,
                ExceptionType = exception != null ? exception.GetType().FullName : string.Empty,
                ExceptionStack = exception != null ? exception.ToString() : string.Empty,
                TagApiAvailability = tagApiAvailability
            };
        }

        public static BuffSystemTagTestCaseResult Skipped(
            string category,
            string caseName,
            string expected,
            string actual,
            string reason,
            string tagApiAvailability)
        {
            return new BuffSystemTagTestCaseResult
            {
                Category = category,
                CaseName = caseName,
                Status = BuffSystemTagTestStatus.Skipped,
                Expected = expected,
                Actual = actual,
                FailureReason = reason,
                TagApiAvailability = tagApiAvailability
            };
        }

        public static BuffSystemTagTestCaseResult NotSupported(
            string category,
            string caseName,
            string expected,
            string actual,
            string reason,
            string tagApiAvailability)
        {
            return new BuffSystemTagTestCaseResult
            {
                Category = category,
                CaseName = caseName,
                Status = BuffSystemTagTestStatus.NotSupported,
                Expected = expected,
                Actual = actual,
                FailureReason = reason,
                TagApiAvailability = tagApiAvailability
            };
        }
    }

    internal static class BuffSystemTagTestStatus
    {
        internal const string Passed = "PASS";
        internal const string Failed = "FAIL";
        internal const string Skipped = "SKIP";
        internal const string NotSupported = "NOT_SUPPORTED";
    }
}
