using System;

namespace BuffSystem.EditorTesting
{
    [Serializable]
    internal sealed class BuffSystemTriggerTestCaseResult
    {
        public string Category;
        public string CaseName;
        public string Status;
        public string Expected;
        public string Actual;
        public int InvariantChecks;
        public string TriggerApiAvailability;
        public string TriggerType;
        public string EventIdOrTriggerId;
        public int ApplyCount;
        public int TickCount;
        public int EventCount;
        public int RemoveCount;
        public int RefreshCount;
        public int StackChangedCount;
        public string FailureReason;
        public string ExceptionType;
        public string ExceptionStack;
        public double DurationMs;
        public string ManualRequiredReason;

        public bool IsPassed => Status == BuffSystemTriggerTestStatus.Passed;
        public bool IsFailed => Status == BuffSystemTriggerTestStatus.Failed;
        public bool IsSkipped => Status == BuffSystemTriggerTestStatus.Skipped;
        public bool IsNotSupported => Status == BuffSystemTriggerTestStatus.NotSupported;
        public bool IsManualRequired => Status == BuffSystemTriggerTestStatus.ManualRequired;

        public static BuffSystemTriggerTestCaseResult FromOutcome(
            string category,
            string caseName,
            string status,
            string expected,
            TriggerCaseOutcome outcome,
            double durationMs,
            string triggerApiAvailability)
        {
            return new BuffSystemTriggerTestCaseResult
            {
                Category = category,
                CaseName = caseName,
                Status = status,
                Expected = expected,
                Actual = outcome.Actual,
                InvariantChecks = outcome.InvariantChecks,
                TriggerApiAvailability = triggerApiAvailability,
                TriggerType = outcome.TriggerType,
                EventIdOrTriggerId = outcome.EventIdOrTriggerId,
                ApplyCount = outcome.ApplyCount,
                TickCount = outcome.TickCount,
                EventCount = outcome.EventCount,
                RemoveCount = outcome.RemoveCount,
                RefreshCount = outcome.RefreshCount,
                StackChangedCount = outcome.StackChangedCount,
                FailureReason = outcome.FailureReason,
                DurationMs = durationMs,
                ManualRequiredReason = status == BuffSystemTriggerTestStatus.ManualRequired ? outcome.FailureReason : string.Empty
            };
        }

        public static BuffSystemTriggerTestCaseResult Failed(
            string category,
            string caseName,
            string expected,
            string actual,
            int invariantChecks,
            double durationMs,
            Exception exception,
            string triggerApiAvailability)
        {
            return new BuffSystemTriggerTestCaseResult
            {
                Category = category,
                CaseName = caseName,
                Status = BuffSystemTriggerTestStatus.Failed,
                Expected = expected,
                Actual = actual,
                InvariantChecks = invariantChecks,
                DurationMs = durationMs,
                FailureReason = exception != null ? exception.Message : string.Empty,
                ExceptionType = exception != null ? exception.GetType().FullName : string.Empty,
                ExceptionStack = exception != null ? exception.ToString() : string.Empty,
                TriggerApiAvailability = triggerApiAvailability
            };
        }
    }

    internal static class BuffSystemTriggerTestStatus
    {
        internal const string Passed = "PASS";
        internal const string Failed = "FAIL";
        internal const string Skipped = "SKIP";
        internal const string NotSupported = "NOT_SUPPORTED";
        internal const string ManualRequired = "MANUAL_REQUIRED";
    }

    internal sealed class TriggerCaseOutcome
    {
        public string Actual = string.Empty;
        public int InvariantChecks;
        public string TriggerType = string.Empty;
        public string EventIdOrTriggerId = string.Empty;
        public int ApplyCount;
        public int TickCount;
        public int EventCount;
        public int RemoveCount;
        public int RefreshCount;
        public int StackChangedCount;
        public string FailureReason = string.Empty;

        public static TriggerCaseOutcome Pass(string actual, int invariantChecks, CountingTriggerEffect effect = null, BuffTriggerType? triggerType = null, int eventId = 0)
        {
            TriggerCaseOutcome outcome = new TriggerCaseOutcome
            {
                Actual = actual,
                InvariantChecks = invariantChecks,
                TriggerType = triggerType.HasValue ? triggerType.Value.ToString() : string.Empty,
                EventIdOrTriggerId = eventId > 0 ? eventId.ToString() : string.Empty
            };

            outcome.CopyEffect(effect);
            return outcome;
        }

        public static TriggerCaseOutcome NotSupported(string reason, BuffTriggerType? triggerType = null, int eventId = 0)
        {
            return new TriggerCaseOutcome
            {
                Actual = reason,
                FailureReason = reason,
                TriggerType = triggerType.HasValue ? triggerType.Value.ToString() : string.Empty,
                EventIdOrTriggerId = eventId > 0 ? eventId.ToString() : string.Empty
            };
        }

        public static TriggerCaseOutcome ManualRequired(string reason, BuffTriggerType? triggerType = null, int eventId = 0)
        {
            return NotSupported(reason, triggerType, eventId);
        }

        private void CopyEffect(CountingTriggerEffect effect)
        {
            if (effect == null)
                return;

            ApplyCount = effect.ApplyCount;
            TickCount = effect.TickCount;
            EventCount = effect.EventCount;
            RemoveCount = effect.RemoveCount;
            RefreshCount = effect.RefreshCount;
            StackChangedCount = effect.StackChangedCount;
        }
    }
}
