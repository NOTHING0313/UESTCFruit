using System;

namespace BuffSystem.EditorTesting
{
    /// <summary>
    /// BuffSystem Editor 测试的单条结果；只用于测试报告，不参与运行时状态。
    /// </summary>
    [Serializable]
    public sealed class BuffSystemTestCaseResult
    {
        public string Name;
        public string Category;
        public string Status;
        public string Message;
        public string Exception;
        public string CoveredArea;
        public string ManualAction;
        public double DurationMs;

        public static BuffSystemTestCaseResult Passed(string category, string name, string message, string coveredArea, double durationMs)
        {
            return new BuffSystemTestCaseResult
            {
                Category = category,
                Name = name,
                Status = BuffSystemTestStatus.Passed,
                Message = message,
                CoveredArea = coveredArea,
                DurationMs = durationMs
            };
        }

        public static BuffSystemTestCaseResult Failed(string category, string name, string message, Exception exception, string coveredArea, double durationMs)
        {
            return new BuffSystemTestCaseResult
            {
                Category = category,
                Name = name,
                Status = BuffSystemTestStatus.Failed,
                Message = message,
                Exception = exception != null ? exception.ToString() : string.Empty,
                CoveredArea = coveredArea,
                DurationMs = durationMs
            };
        }

        public static BuffSystemTestCaseResult Skipped(string category, string name, string message, string coveredArea, string manualAction = "")
        {
            return new BuffSystemTestCaseResult
            {
                Category = category,
                Name = name,
                Status = BuffSystemTestStatus.Skipped,
                Message = message,
                CoveredArea = coveredArea,
                ManualAction = manualAction,
                DurationMs = 0d
            };
        }
    }

    internal static class BuffSystemTestStatus
    {
        internal const string Passed = "Passed";
        internal const string Failed = "Failed";
        internal const string Skipped = "Skipped";
        internal const string NotCovered = "NotCovered";
    }
}
