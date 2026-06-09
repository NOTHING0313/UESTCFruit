using System;
using System.Collections.Generic;
using System.Text;

namespace BuffSystem.Editor.AuthoringGraphs
{
    /// <summary>
    /// BuffCandidateGraph 与 Authoring Hub 的 Editor-only 桥接层。
    /// Graph 只作为候选设计和审查输入；该桥接层不会创建 asset、不会注册 Effect、不会修改 whitelist 或 runtime。
    /// </summary>
    internal static class BuffCandidateGraphBridge
    {
        internal static bool TryBuildSummary(BuffCandidateGraph graph, out BuffCandidateGraphSummary summary)
        {
            summary = new BuffCandidateGraphSummary();

            if (graph == null)
            {
                summary.Diagnosis = "未选择候选图。";
                return false;
            }

            BuffCandidateStartNode start = graph.FindSingleNode<BuffCandidateStartNode>();
            BuffShapeNode shape = graph.FindSingleNode<BuffShapeNode>();
            EffectBindingNode effect = graph.FindSingleNode<EffectBindingNode>();
            CompressedEligibilityNode eligibility = graph.FindSingleNode<CompressedEligibilityNode>();
            RuntimeDependencyRiskNode risk = graph.FindSingleNode<RuntimeDependencyRiskNode>();
            CandidateDecisionNode decision = graph.FindSingleNode<CandidateDecisionNode>();
            BuffCandidateGraphEvaluationResult evaluation = BuffCandidateGraphEvaluation.Evaluate(graph);

            summary.Graph = graph;
            summary.GraphVersion = graph.GraphVersion;
            summary.GraphDescription = graph.Description ?? string.Empty;
            summary.ConfigId = start != null ? start.ConfigId : 0;
            summary.BuffName = start != null ? start.BuffName : string.Empty;
            summary.DesignPurpose = start != null ? start.DesignPurpose : string.Empty;
            summary.Owner = start != null ? start.Owner : string.Empty;
            summary.EffectId = effect != null ? effect.EffectId : 0;
            summary.EffectClassName = effect != null ? effect.EffectClassName : string.Empty;
            summary.EffectRegistered = effect != null && effect.EffectRegistered;
            summary.Eligibility = eligibility != null && eligibility.Eligible;
            summary.RiskLevel = risk != null ? risk.RiskLevel : string.Empty;
            summary.IsComplete = evaluation.IsComplete;
            summary.CanSubmitForReview = decision != null ? decision.CanSubmitForReview && !decision.ShouldReject && evaluation.IsComplete : evaluation.CanSubmitForReview;
            summary.RejectReasons = CombineLines(evaluation.RejectReasons, eligibility != null ? eligibility.RejectReasons : null, risk != null ? risk.RejectReasons : null, decision != null ? decision.RejectReasons : null);
            summary.Warnings = CombineLines(evaluation.Warnings, eligibility != null ? eligibility.Warnings : null, risk != null ? risk.Warnings : null, decision != null ? decision.Warnings : null);
            summary.NextActions = decision != null ? decision.NextActions : string.Empty;
            summary.Diagnosis = BuildDiagnosis(summary, start, shape, effect, eligibility, decision);
            return true;
        }

        internal static bool TryBuildCreateBuffDraft(BuffCandidateGraph graph, out BuffCandidateCreateBuffDraft draft, out string warning)
        {
            draft = new BuffCandidateCreateBuffDraft();
            warning = string.Empty;

            if (graph == null)
            {
                warning = "未选择候选图，无法导入 Create Buff 字段。";
                return false;
            }

            BuffCandidateStartNode start = graph.FindSingleNode<BuffCandidateStartNode>();
            BuffShapeNode shape = graph.FindSingleNode<BuffShapeNode>();
            EffectBindingNode effect = graph.FindSingleNode<EffectBindingNode>();
            TryBuildSummary(graph, out BuffCandidateGraphSummary summary);

            if (start == null && shape == null && effect == null)
            {
                warning = "候选图缺少可导入节点。";
                return false;
            }

            draft.BuffType = BuffInstanceType.parallel;
            draft.TriggerType = BuffTriggerType.Tick;
            draft.ParallelStorageMode = ParallelBuffStorageMode.EntityPerStack;
            draft.MaxStack = 1;
            draft.Duration = 1f;
            draft.TickTime = 1f;
            draft.StackUpPolicy = ParallelBuffStackUpPolicy.Append;
            draft.StackDownPolicy = ParallelBuffStackDownPolicy.RemoveEarliest;

            if (start != null)
            {
                draft.ConfigId = start.ConfigId;
                draft.BuffName = start.BuffName;
                draft.Description = BuildDescription(start.DesignPurpose, start.Notes);
            }

            if (shape != null)
            {
                TryParseEnum(shape.BuffType, BuffInstanceType.parallel, out draft.BuffType);
                TryParseEnum(shape.TriggerType, BuffTriggerType.Tick, out draft.TriggerType);
                TryParseEnum(shape.ParallelStorageMode, ParallelBuffStorageMode.EntityPerStack, out draft.ParallelStorageMode);
                TryParseEnum(shape.StackUpPolicy, ParallelBuffStackUpPolicy.Append, out draft.StackUpPolicy);
                TryParseEnum(shape.StackDownPolicy, ParallelBuffStackDownPolicy.RemoveEarliest, out draft.StackDownPolicy);
                draft.Unlimited = shape.Unlimited;
                draft.MaxStack = shape.MaxStack;
                draft.Duration = Math.Max(0, shape.DurationFrames);
                draft.TickTime = Math.Max(0, shape.TickIntervalFrames);
            }

            if (effect != null)
                draft.EffectId = effect.EffectId;

            draft.HasAnyValue = true;

            if (!summary.IsComplete)
                warning = "候选图不完整，已导入可用字段；请检查缺失节点、拒绝原因和警告。";

            return true;
        }

        internal static bool TryBuildEffectTemplateDraft(BuffCandidateGraph graph, out BuffCandidateEffectTemplateDraft draft, out string warning)
        {
            draft = new BuffCandidateEffectTemplateDraft();
            warning = string.Empty;

            if (graph == null)
            {
                warning = "未选择候选图，无法导入 Effect 字段。";
                return false;
            }

            EffectBindingNode effect = graph.FindSingleNode<EffectBindingNode>();
            TryBuildSummary(graph, out BuffCandidateGraphSummary summary);

            if (effect == null)
            {
                warning = "候选图缺少 EffectBindingNode，无法导入 Effect 字段。";
                return false;
            }

            draft.EffectId = effect.EffectId;
            draft.EffectClassName = effect.EffectClassName;
            draft.Note = effect.EffectRiskNotes;
            draft.HasAnyValue = true;

            if (!summary.IsComplete)
                warning = "候选图不完整，已导入 Effect 字段；请检查缺失节点、拒绝原因和警告。";

            return true;
        }

        internal static bool RealBuffConfigExists(int configId)
        {
            if (configId <= 0)
                return false;

            List<BuffAssetSummary> summaries = BuffAuthoringValidationUtility.ScanBuffAssets();
            for (int i = 0; i < summaries.Count; i++)
            {
                if (summaries[i].ConfigId == configId)
                    return true;
            }

            return false;
        }

        private static void TryParseEnum<T>(string value, T fallback, out T result)
            where T : struct
        {
            if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse(value.Trim(), true, out result))
                return;

            result = fallback;
        }

        private static string BuildDescription(string designPurpose, string notes)
        {
            StringBuilder builder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(designPurpose))
                builder.AppendLine(designPurpose.Trim());

            if (!string.IsNullOrWhiteSpace(notes))
            {
                if (builder.Length > 0)
                    builder.AppendLine();

                builder.AppendLine(notes.Trim());
            }

            return builder.ToString().TrimEnd();
        }

        private static string CombineLines(IList<string> first, params string[] others)
        {
            StringBuilder builder = new StringBuilder();

            if (first != null)
            {
                for (int i = 0; i < first.Count; i++)
                    AppendLine(builder, first[i]);
            }

            if (others != null)
            {
                for (int i = 0; i < others.Length; i++)
                    AppendLine(builder, others[i]);
            }

            return builder.Length == 0 ? "无" : builder.ToString().TrimEnd();
        }

        private static void AppendLine(StringBuilder builder, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            string[] lines = value.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                    builder.AppendLine(lines[i].Trim());
            }
        }

        private static string BuildDiagnosis(
            BuffCandidateGraphSummary summary,
            BuffCandidateStartNode start,
            BuffShapeNode shape,
            EffectBindingNode effect,
            CompressedEligibilityNode eligibility,
            CandidateDecisionNode decision)
        {
            if (start == null || shape == null || effect == null || eligibility == null || decision == null)
                return "候选图仍缺少必需节点；可先导入已有字段，但不能视为审查完成。";

            if (!summary.IsComplete)
                return "候选图节点数量不完整；请根据拒绝原因补齐。";

            if (!summary.CanSubmitForReview)
                return "候选图已可读取，但当前结论尚不建议提交 whitelist 审查。";

            return "候选图节点完整，可作为 Authoring Hub 表单导入来源；仍需 Validator、Runner、场景验证和人工审批。";
        }
    }

    internal sealed class BuffCandidateGraphSummary
    {
        public BuffCandidateGraph Graph;
        public int GraphVersion;
        public string GraphDescription;
        public int ConfigId;
        public string BuffName;
        public string DesignPurpose;
        public string Owner;
        public int EffectId;
        public string EffectClassName;
        public bool EffectRegistered;
        public bool Eligibility;
        public string RiskLevel;
        public bool IsComplete;
        public bool CanSubmitForReview;
        public string RejectReasons;
        public string Warnings;
        public string NextActions;
        public string Diagnosis;
    }

    internal struct BuffCandidateCreateBuffDraft
    {
        public bool HasAnyValue;
        public int ConfigId;
        public string BuffName;
        public string Description;
        public BuffInstanceType BuffType;
        public BuffTriggerType TriggerType;
        public ParallelBuffStorageMode ParallelStorageMode;
        public bool Unlimited;
        public int MaxStack;
        public float Duration;
        public float TickTime;
        public ParallelBuffStackUpPolicy StackUpPolicy;
        public ParallelBuffStackDownPolicy StackDownPolicy;
        public int EffectId;
    }

    internal struct BuffCandidateEffectTemplateDraft
    {
        public bool HasAnyValue;
        public int EffectId;
        public string EffectClassName;
        public string Note;
    }
}
