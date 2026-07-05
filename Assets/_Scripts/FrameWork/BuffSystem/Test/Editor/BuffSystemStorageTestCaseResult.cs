using System;

namespace BuffSystem.EditorTesting
{
    [Serializable]
    internal sealed class BuffSystemStorageTestCaseResult
    {
        public string Category;
        public string CaseName;
        public string Status;
        public string Expected;
        public string Actual;
        public string StorageMode;
        public string ExpectedCounts;
        public string ActualCounts;
        public int InvariantChecks;
        public string FailureReason;
        public string ExceptionType;
        public string ExceptionStack;
        public double DurationMs;
        public string ManualRequiredReason;
        public string Classification;
        public string EntitySnapshot;
        public string CompressedSnapshot;
        public string Timeline;
        public string ReproResult;
        public string KeyEvidence;

        public bool IsPassed => Status == BuffSystemStorageTestStatus.Passed;
        public bool IsFailed => Status == BuffSystemStorageTestStatus.Failed;
        public bool IsSkipped => Status == BuffSystemStorageTestStatus.Skipped;
        public bool IsManualRequired => Status == BuffSystemStorageTestStatus.ManualRequired;

        public static BuffSystemStorageTestCaseResult Passed(
            string category,
            string caseName,
            string expected,
            string actual,
            string storageMode,
            string expectedCounts,
            string actualCounts,
            int invariantChecks,
            double durationMs)
        {
            return new BuffSystemStorageTestCaseResult
            {
                Category = category,
                CaseName = caseName,
                Status = BuffSystemStorageTestStatus.Passed,
                Expected = expected,
                Actual = actual,
                StorageMode = storageMode,
                ExpectedCounts = expectedCounts,
                ActualCounts = actualCounts,
                InvariantChecks = invariantChecks,
                DurationMs = durationMs
            };
        }

        public static BuffSystemStorageTestCaseResult Failed(
            string category,
            string caseName,
            string expected,
            string actual,
            string storageMode,
            string expectedCounts,
            string actualCounts,
            int invariantChecks,
            double durationMs,
            Exception exception)
        {
            return new BuffSystemStorageTestCaseResult
            {
                Category = category,
                CaseName = caseName,
                Status = BuffSystemStorageTestStatus.Failed,
                Expected = expected,
                Actual = actual,
                StorageMode = storageMode,
                ExpectedCounts = expectedCounts,
                ActualCounts = actualCounts,
                InvariantChecks = invariantChecks,
                DurationMs = durationMs,
                FailureReason = exception != null ? exception.Message : string.Empty,
                ExceptionType = exception != null ? exception.GetType().FullName : string.Empty,
                ExceptionStack = exception != null ? exception.ToString() : string.Empty
            };
        }

        public static BuffSystemStorageTestCaseResult Skipped(string category, string caseName, string expected, string actual, string reason)
        {
            return new BuffSystemStorageTestCaseResult
            {
                Category = category,
                CaseName = caseName,
                Status = BuffSystemStorageTestStatus.Skipped,
                Expected = expected,
                Actual = actual,
                FailureReason = reason
            };
        }

        public static BuffSystemStorageTestCaseResult ManualRequired(string category, string caseName, string expected, string actual, string reason)
        {
            return new BuffSystemStorageTestCaseResult
            {
                Category = category,
                CaseName = caseName,
                Status = BuffSystemStorageTestStatus.ManualRequired,
                Expected = expected,
                Actual = actual,
                FailureReason = reason,
                ManualRequiredReason = reason
            };
        }

        public BuffSystemStorageTestCaseResult WithDiagnostics(
            string classification,
            string entitySnapshot,
            string compressedSnapshot,
            string timeline,
            string reproResult,
            string keyEvidence)
        {
            Classification = classification ?? string.Empty;
            EntitySnapshot = entitySnapshot ?? string.Empty;
            CompressedSnapshot = compressedSnapshot ?? string.Empty;
            Timeline = timeline ?? string.Empty;
            ReproResult = reproResult ?? string.Empty;
            KeyEvidence = keyEvidence ?? string.Empty;
            return this;
        }
    }

    internal static class BuffSystemStorageTestStatus
    {
        internal const string Passed = "PASS";
        internal const string Failed = "FAIL";
        internal const string Skipped = "SKIP";
        internal const string ManualRequired = "MANUAL_REQUIRED";
    }
}
