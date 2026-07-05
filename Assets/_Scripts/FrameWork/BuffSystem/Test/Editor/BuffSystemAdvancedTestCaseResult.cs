using System;

namespace BuffSystem.EditorTesting
{
    [Serializable]
    internal sealed class BuffSystemAdvancedTestCaseResult
    {
        public string Type;
        public string CaseName;
        public int SampleCount;
        public int TickFrames;
        public int EntityCount;
        public int BuffCount;
        public int OperationCount;
        public int ExpectedOperations;
        public int ActualOperations;
        public int InvariantChecks;
        public int InvariantFailures;
        public double ElapsedMs;
        public double SetupElapsedMs;
        public double MeasuredElapsedMs;
        public long GcAllocBytes;
        public long SetupGCAllocBytes;
        public long MeasuredGCAllocBytes;
        public string GCMethod;
        public bool GCZeroObserved;
        public string GCMeasurementWindow;
        public string OperationCountMeaning;
        public string Status;
        public string Note;
        public string FailureReason;
        public string ExceptionType;
        public string ExceptionStack;
        public int RandomSeed;
        public int FailureIteration;
        public string ReproParameters;
        public string LastOperations;
        public string ProfileParameters;
        public string ExpectedCounts;
        public string ActualCounts;

        public bool IsFailed => Status == BuffSystemAdvancedTestStatus.Failed;
        public bool IsSkipped => Status == BuffSystemAdvancedTestStatus.Skipped;
        public bool IsManualRequired => Status == BuffSystemAdvancedTestStatus.ManualRequired;

        public static BuffSystemAdvancedTestCaseResult Passed(
            string type,
            string caseName,
            int sampleCount,
            int tickFrames,
            int entityCount,
            int buffCount,
            int operationCount,
            int expectedOperations,
            int actualOperations,
            int invariantChecks,
            int invariantFailures,
            double setupElapsedMs,
            double measuredElapsedMs,
            long setupGCAllocBytes,
            long measuredGCAllocBytes,
            string gcMethod,
            string gcMeasurementWindow,
            string operationCountMeaning,
            string note,
            int randomSeed,
            string reproParameters,
            string lastOperations,
            string profileParameters,
            string expectedCounts,
            string actualCounts)
        {
            return new BuffSystemAdvancedTestCaseResult
            {
                Type = type,
                CaseName = caseName,
                SampleCount = sampleCount,
                TickFrames = tickFrames,
                EntityCount = entityCount,
                BuffCount = buffCount,
                OperationCount = operationCount,
                ExpectedOperations = expectedOperations,
                ActualOperations = actualOperations,
                InvariantChecks = invariantChecks,
                InvariantFailures = invariantFailures,
                SetupElapsedMs = setupElapsedMs,
                MeasuredElapsedMs = measuredElapsedMs,
                ElapsedMs = setupElapsedMs + measuredElapsedMs,
                SetupGCAllocBytes = setupGCAllocBytes,
                MeasuredGCAllocBytes = measuredGCAllocBytes,
                GcAllocBytes = setupGCAllocBytes + measuredGCAllocBytes,
                GCMethod = gcMethod,
                GCZeroObserved = setupGCAllocBytes == 0 && measuredGCAllocBytes == 0,
                GCMeasurementWindow = gcMeasurementWindow,
                OperationCountMeaning = operationCountMeaning,
                Status = BuffSystemAdvancedTestStatus.Passed,
                Note = note,
                RandomSeed = randomSeed,
                FailureIteration = -1,
                ReproParameters = reproParameters,
                LastOperations = lastOperations,
                ProfileParameters = profileParameters,
                ExpectedCounts = expectedCounts,
                ActualCounts = actualCounts
            };
        }

        public static BuffSystemAdvancedTestCaseResult Failed(
            string type,
            string caseName,
            int sampleCount,
            int tickFrames,
            int entityCount,
            int buffCount,
            int operationCount,
            int expectedOperations,
            int actualOperations,
            int invariantChecks,
            int invariantFailures,
            double setupElapsedMs,
            double measuredElapsedMs,
            long setupGCAllocBytes,
            long measuredGCAllocBytes,
            string gcMethod,
            string gcMeasurementWindow,
            string operationCountMeaning,
            Exception exception,
            string note,
            int randomSeed,
            int failureIteration,
            string reproParameters,
            string lastOperations,
            string profileParameters,
            string expectedCounts,
            string actualCounts)
        {
            return new BuffSystemAdvancedTestCaseResult
            {
                Type = type,
                CaseName = caseName,
                SampleCount = sampleCount,
                TickFrames = tickFrames,
                EntityCount = entityCount,
                BuffCount = buffCount,
                OperationCount = operationCount,
                ExpectedOperations = expectedOperations,
                ActualOperations = actualOperations,
                InvariantChecks = invariantChecks,
                InvariantFailures = invariantFailures,
                SetupElapsedMs = setupElapsedMs,
                MeasuredElapsedMs = measuredElapsedMs,
                ElapsedMs = setupElapsedMs + measuredElapsedMs,
                SetupGCAllocBytes = setupGCAllocBytes,
                MeasuredGCAllocBytes = measuredGCAllocBytes,
                GcAllocBytes = setupGCAllocBytes + measuredGCAllocBytes,
                GCMethod = gcMethod,
                GCZeroObserved = setupGCAllocBytes == 0 && measuredGCAllocBytes == 0,
                GCMeasurementWindow = gcMeasurementWindow,
                OperationCountMeaning = operationCountMeaning,
                Status = BuffSystemAdvancedTestStatus.Failed,
                Note = note,
                FailureReason = exception != null ? exception.Message : string.Empty,
                ExceptionType = exception != null ? exception.GetType().FullName : string.Empty,
                ExceptionStack = exception != null ? exception.ToString() : string.Empty,
                RandomSeed = randomSeed,
                FailureIteration = failureIteration,
                ReproParameters = reproParameters,
                LastOperations = lastOperations,
                ProfileParameters = profileParameters,
                ExpectedCounts = expectedCounts,
                ActualCounts = actualCounts
            };
        }

        public static BuffSystemAdvancedTestCaseResult Skipped(string type, string caseName, string note)
        {
            return new BuffSystemAdvancedTestCaseResult
            {
                Type = type,
                CaseName = caseName,
                Status = BuffSystemAdvancedTestStatus.Skipped,
                Note = note,
                FailureIteration = -1
            };
        }

        public static BuffSystemAdvancedTestCaseResult ManualRequired(string type, string caseName, string note)
        {
            return new BuffSystemAdvancedTestCaseResult
            {
                Type = type,
                CaseName = caseName,
                Status = BuffSystemAdvancedTestStatus.ManualRequired,
                Note = note,
                FailureIteration = -1
            };
        }
    }

    internal static class BuffSystemAdvancedTestStatus
    {
        internal const string Passed = "PASS";
        internal const string Failed = "FAIL";
        internal const string Skipped = "SKIP";
        internal const string ManualRequired = "MANUAL_REQUIRED";
    }
}
