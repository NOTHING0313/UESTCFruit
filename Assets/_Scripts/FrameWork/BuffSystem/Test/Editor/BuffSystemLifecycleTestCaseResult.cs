using System;
using System.Collections.Generic;

namespace BuffSystem.EditorTesting
{
    [Serializable]
    internal sealed class BuffSystemLifecycleTestCaseResult
    {
        public string Category;
        public string CaseName;
        public string Status;
        public string Expected;
        public string Actual;
        public int ApplyCount;
        public int TickCount;
        public int RemoveCount;
        public int RefreshCount;
        public int StackChangedCount;
        public int LastStackDelta;
        public int InvariantChecks;
        public string FailureReason;
        public string ExceptionType;
        public string ExceptionStack;
        public double DurationMs;
        public readonly List<string> Events = new List<string>();

        public bool IsPassed => Status == BuffSystemLifecycleTestStatus.Passed;
        public bool IsFailed => Status == BuffSystemLifecycleTestStatus.Failed;
        public bool IsSkipped => Status == BuffSystemLifecycleTestStatus.Skipped;

        public static BuffSystemLifecycleTestCaseResult FromContext(
            string category,
            string caseName,
            string status,
            string expected,
            string actual,
            int invariantChecks,
            double durationMs,
            LifecycleEffectSnapshot snapshot,
            Exception exception)
        {
            BuffSystemLifecycleTestCaseResult result = new BuffSystemLifecycleTestCaseResult
            {
                Category = category,
                CaseName = caseName,
                Status = status,
                Expected = expected,
                Actual = actual,
                InvariantChecks = invariantChecks,
                DurationMs = durationMs,
                ApplyCount = snapshot.ApplyCount,
                TickCount = snapshot.TickCount,
                RemoveCount = snapshot.RemoveCount,
                RefreshCount = snapshot.RefreshCount,
                StackChangedCount = snapshot.StackChangedCount,
                LastStackDelta = snapshot.LastStackDelta,
                FailureReason = exception != null ? exception.Message : string.Empty,
                ExceptionType = exception != null ? exception.GetType().FullName : string.Empty,
                ExceptionStack = exception != null ? exception.ToString() : string.Empty
            };

            for (int i = 0; i < snapshot.Events.Count; i++)
                result.Events.Add(snapshot.Events[i]);

            return result;
        }
    }

    internal readonly struct LifecycleEffectSnapshot
    {
        public readonly int ApplyCount;
        public readonly int TickCount;
        public readonly int RemoveCount;
        public readonly int RefreshCount;
        public readonly int StackChangedCount;
        public readonly int LastStackDelta;
        public readonly IReadOnlyList<string> Events;

        public LifecycleEffectSnapshot(
            int applyCount,
            int tickCount,
            int removeCount,
            int refreshCount,
            int stackChangedCount,
            int lastStackDelta,
            IReadOnlyList<string> events)
        {
            ApplyCount = applyCount;
            TickCount = tickCount;
            RemoveCount = removeCount;
            RefreshCount = refreshCount;
            StackChangedCount = stackChangedCount;
            LastStackDelta = lastStackDelta;
            Events = events ?? Array.Empty<string>();
        }

        public static LifecycleEffectSnapshot Empty =>
            new LifecycleEffectSnapshot(0, 0, 0, 0, 0, 0, Array.Empty<string>());
    }

    internal static class BuffSystemLifecycleTestStatus
    {
        internal const string Passed = "PASS";
        internal const string Failed = "FAIL";
        internal const string Skipped = "SKIP";
    }
}
