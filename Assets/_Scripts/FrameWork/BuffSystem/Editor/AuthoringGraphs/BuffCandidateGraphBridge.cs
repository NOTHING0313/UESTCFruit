using System;
using System.Collections.Generic;
using System.Text;
using XNode;

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

            BuffRootNode root = graph.FindSingleNode<BuffRootNode>();
            EffectCompositionRootNode effectRoot = graph.FindSingleNode<EffectCompositionRootNode>();
            BuffCandidateStartNode start = graph.FindSingleNode<BuffCandidateStartNode>();
            BuffShapeNode shape = graph.FindSingleNode<BuffShapeNode>();
            EffectBindingNode effect = graph.FindSingleNode<EffectBindingNode>();
            BuffGraphEffectOrderUtility.OrderResult effectOrder = BuffGraphEffectOrderUtility.Build(graph);
            List<EffectNode> effectNodes = effectOrder.OrderedEffects;
            List<ScriptActionNode> scriptActions = GetSortedScriptActionNodes(graph);
            CompressedEligibilityNode eligibility = graph.FindSingleNode<CompressedEligibilityNode>();
            RuntimeDependencyRiskNode risk = graph.FindSingleNode<RuntimeDependencyRiskNode>();
            CandidateDecisionNode decision = graph.FindSingleNode<CandidateDecisionNode>();
            BuffCandidateGraphEvaluationResult evaluation = BuffCandidateGraphEvaluation.Evaluate(graph);
            EffectNode primaryEffect = effectNodes.Count > 0 ? effectNodes[0] : null;

            summary.Graph = graph;
            summary.GraphVersion = graph.GraphVersion;
            summary.GraphDescription = graph.Description ?? string.Empty;
            summary.ConfigId = start != null ? start.ConfigId : (root != null ? root.ConfigId : 0);
            summary.BuffName = start != null ? start.BuffName : (root != null ? root.BuffName : string.Empty);
            summary.DesignPurpose = start != null ? start.DesignPurpose : (root != null ? root.Description : string.Empty);
            summary.Owner = start != null ? start.Owner : (root != null ? root.Owner : string.Empty);
            summary.EffectId = effectRoot != null && effectRoot.FinalEffectId > 0 ? effectRoot.FinalEffectId : (primaryEffect != null ? primaryEffect.EffectId : (effect != null ? effect.EffectId : 0));
            summary.EffectClassName = effectRoot != null && !string.IsNullOrWhiteSpace(effectRoot.FinalEffectClassName) ? effectRoot.FinalEffectClassName : (primaryEffect != null ? primaryEffect.EffectClassName : (effect != null ? effect.EffectClassName : string.Empty));
            summary.EffectRegistered = effectNodes.Count == 0 && effect != null && effect.EffectRegistered;
            summary.EffectCompositionRootExists = effectRoot != null;
            summary.EffectNodeCount = effectNodes.Count;
            summary.EffectOrderMode = effectOrder.ModeLabel;
            summary.EffectOrderSummary = BuffGraphEffectOrderUtility.BuildSummary(effectOrder);
            summary.LifecycleSummary = BuildLifecycleSummary(effectNodes);
            summary.UsesLegacyEffectBindingNode = effect != null;
            summary.UsesLegacyBuffRoot = root != null;
            summary.HasMultipleEffectNodes = effectNodes.Count > 1;
            summary.DeprecatedPlaceholderCount = graph.FindNodes<EmptyActionPlaceholderNode>().Count;
            summary.ScriptActionNodeCount = scriptActions.Count;
            summary.ValidScriptActionNodeCount = CountValidScriptActions(scriptActions);
            summary.InvalidScriptActionNodeCount = summary.ScriptActionNodeCount - summary.ValidScriptActionNodeCount;
            summary.ScriptActionWarningCount = CountScriptActionWarnings(scriptActions);
            summary.ScriptActionSummary = BuildScriptActionSummary(scriptActions);
            summary.ScriptActionWarnings = BuildScriptActionWarnings(scriptActions);
            summary.Eligibility = eligibility != null && eligibility.Eligible;
            summary.RiskLevel = risk != null ? risk.RiskLevel : string.Empty;
            summary.IsComplete = evaluation.IsComplete;
            summary.CanSubmitForReview = decision != null ? decision.CanSubmitForReview && !decision.ShouldReject && evaluation.IsComplete : evaluation.CanSubmitForReview;
            summary.RejectReasons = CombineLines(evaluation.RejectReasons, eligibility != null ? eligibility.RejectReasons : null, risk != null ? risk.RejectReasons : null, decision != null ? decision.RejectReasons : null);
            summary.Warnings = CombineLines(evaluation.Warnings, eligibility != null ? eligibility.Warnings : null, risk != null ? risk.Warnings : null, decision != null ? decision.Warnings : null);
            summary.NextActions = decision != null ? decision.NextActions : string.Empty;
            summary.Diagnosis = BuildDiagnosis(summary, root, effectRoot, start, shape, effect, effectNodes, eligibility, decision);
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

            BuffRootNode root = graph.FindSingleNode<BuffRootNode>();
            EffectCompositionRootNode effectRoot = graph.FindSingleNode<EffectCompositionRootNode>();
            BuffCandidateStartNode start = graph.FindSingleNode<BuffCandidateStartNode>();
            BuffShapeNode shape = graph.FindSingleNode<BuffShapeNode>();
            EffectBindingNode effect = graph.FindSingleNode<EffectBindingNode>();
            List<EffectNode> effectNodes = GetOrderedEffectNodes(graph);
            TryBuildSummary(graph, out BuffCandidateGraphSummary summary);

            if (root == null && effectRoot == null && start == null && shape == null && effect == null && effectNodes.Count == 0)
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
            else if (root != null)
            {
                draft.ConfigId = root.ConfigId;
                draft.BuffName = root.BuffName;
                draft.Description = BuildDescription(root.Description, root.Notes);
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

            if (effectRoot != null && effectRoot.FinalEffectId > 0)
                draft.EffectId = effectRoot.FinalEffectId;
            else if (effectNodes.Count == 1)
                draft.EffectId = effectNodes[0].EffectId;
            else if (effectNodes.Count > 1)
                warning = "当前图包含多个 EffectNode 且缺少 EffectCompositionRoot.FinalEffectId；Create Buff 仅导入 Buff 基础字段。";
            else if (effect != null)
                draft.EffectId = effect.EffectId;

            draft.HasAnyValue = true;

            if (!summary.IsComplete && string.IsNullOrWhiteSpace(warning))
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

            EffectCompositionRootNode effectRoot = graph.FindSingleNode<EffectCompositionRootNode>();
            List<EffectNode> effectNodes = GetOrderedEffectNodes(graph);
            EffectBindingNode effect = graph.FindSingleNode<EffectBindingNode>();
            TryBuildSummary(graph, out BuffCandidateGraphSummary summary);

            if (effectRoot != null)
            {
                draft.EffectId = effectRoot.FinalEffectId;
                draft.EffectClassName = effectRoot.FinalEffectClassName;
                draft.Note = BuildDescription(effectRoot.Description, effectRoot.Notes);
                draft.HasAnyValue = true;

                if (effectNodes.Count > 1)
                    warning = "已从 EffectCompositionRoot 导入最终 Effect 字段；EffectNode 连接表示组合成员，顺序由 Next 链或 ExecutionOrder 决定。";
            }
            else if (effectNodes.Count > 0)
            {
                EffectNode primary = effectNodes[0];
                draft.EffectId = primary.EffectId;
                draft.EffectClassName = primary.EffectClassName;
                draft.Note = BuildDescription(primary.Description, primary.Notes);
                draft.HasAnyValue = true;

                if (effectNodes.Count > 1)
                    warning = "当前图包含多个 EffectNode 但缺少 EffectCompositionRoot；已按顺序导入第一个 EffectNode。";
            }
            else if (effect != null)
            {
                draft.EffectId = effect.EffectId;
                draft.EffectClassName = effect.EffectClassName;
                draft.Note = effect.EffectRiskNotes;
                draft.HasAnyValue = true;
            }
            else
            {
                warning = "候选图缺少 EffectCompositionRootNode / EffectNode / 旧 EffectBindingNode，无法导入 Effect 字段。";
                return false;
            }

            if (!summary.IsComplete && string.IsNullOrWhiteSpace(warning))
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

        private static List<EffectNode> GetOrderedEffectNodes(BuffCandidateGraph graph)
        {
            return BuffGraphEffectOrderUtility.Build(graph).OrderedEffects;
        }

        private static List<ScriptActionNode> GetSortedScriptActionNodes(BuffCandidateGraph graph)
        {
            List<ScriptActionNode> actions = graph.FindNodes<ScriptActionNode>();
            actions.Sort(CompareScriptActionNodes);
            return actions;
        }

        private static int CompareEffectNodes(EffectNode left, EffectNode right)
        {
            return BuffGraphEffectOrderUtility.CompareEffectNodes(left, right);
        }

        private static int CompareScriptActionNodes(ScriptActionNode left, ScriptActionNode right)
        {
            int order = left.ExecutionOrder.CompareTo(right.ExecutionOrder);
            if (order != 0)
                return order;

            return string.Compare(GetScriptActionDisplayName(left), GetScriptActionDisplayName(right), StringComparison.Ordinal);
        }

        private static string BuildLifecycleSummary(List<EffectNode> effects)
        {
            int onApply = 0;
            int onTick = 0;
            int onRemove = 0;
            int onRefresh = 0;
            int onStackChanged = 0;

            if (effects != null)
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    onApply += CountPortConnections(effects[i], "OnApply");
                    onTick += CountPortConnections(effects[i], "OnTick");
                    onRemove += CountPortConnections(effects[i], "OnRemove");
                    onRefresh += CountPortConnections(effects[i], "OnRefresh");
                    onStackChanged += CountPortConnections(effects[i], "OnStackChanged");
                }
            }

            return $"OnApply={onApply}, OnTick={onTick}, OnRemove={onRemove}, OnRefresh={onRefresh}, OnStackChanged={onStackChanged}";
        }

        private static int CountValidScriptActions(List<ScriptActionNode> actions)
        {
            int count = 0;
            for (int i = 0; i < actions.Count; i++)
            {
                BuffScriptActionValidationResult validation = BuffScriptActionNodeValidator.RefreshFromScript(actions[i]);
                if (validation.IsValid)
                    count++;
            }

            return count;
        }

        private static int CountScriptActionWarnings(List<ScriptActionNode> actions)
        {
            int count = 0;
            for (int i = 0; i < actions.Count; i++)
            {
                BuffScriptActionValidationResult validation = BuffScriptActionNodeValidator.RefreshFromScript(actions[i]);
                count += validation.Warnings.Count;
            }

            return count;
        }

        private static string BuildScriptActionSummary(List<ScriptActionNode> actions)
        {
            if (actions == null || actions.Count == 0)
                return "无 ScriptActionNode";

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < actions.Count; i++)
            {
                ScriptActionNode action = actions[i];
                BuffScriptActionValidationResult validation = BuffScriptActionNodeValidator.RefreshFromScript(action);
                if (i > 0)
                    builder.Append(", ");

                builder.Append(action.ExecutionOrder)
                    .Append(' ')
                    .Append(validation.IsValid ? GetScriptActionDisplayName(action) : "<invalid>");
            }

            return builder.ToString();
        }

        private static string BuildScriptActionWarnings(List<ScriptActionNode> actions)
        {
            if (actions == null || actions.Count == 0)
                return "无";

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < actions.Count; i++)
            {
                BuffScriptActionValidationResult validation = BuffScriptActionNodeValidator.RefreshFromScript(actions[i]);
                string name = GetScriptActionDisplayName(actions[i]);

                for (int errorIndex = 0; errorIndex < validation.Errors.Count; errorIndex++)
                    builder.AppendLine($"{name}: Error: {validation.Errors[errorIndex]}");

                for (int warningIndex = 0; warningIndex < validation.Warnings.Count; warningIndex++)
                    builder.AppendLine($"{name}: Warning: {validation.Warnings[warningIndex]}");
            }

            return builder.Length == 0 ? "无" : builder.ToString().TrimEnd();
        }

        private static string GetScriptActionDisplayName(ScriptActionNode action)
        {
            if (action == null)
                return "<null>";

            if (!string.IsNullOrWhiteSpace(action.ActionDisplayName))
                return action.ActionDisplayName;

            if (!string.IsNullOrWhiteSpace(action.ActionName))
                return action.ActionName;

            if (!string.IsNullOrWhiteSpace(action.ActionTypeName))
                return action.ActionTypeName;

            return "<unnamed>";
        }

        private static int CountPortConnections(XNode.Node node, string portName)
        {
            NodePort port = node.GetPort(portName);
            return port != null ? port.ConnectionCount : 0;
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
            BuffRootNode root,
            EffectCompositionRootNode effectRoot,
            BuffCandidateStartNode start,
            BuffShapeNode shape,
            EffectBindingNode effect,
            List<EffectNode> effectNodes,
            CompressedEligibilityNode eligibility,
            CandidateDecisionNode decision)
        {
            bool hasRoot = start != null || root != null;
            bool hasEffect = effectNodes.Count > 0 || effect != null;
            if (!hasRoot || shape == null || !hasEffect || decision == null)
                return "候选图仍缺少必需节点；可先导入已有字段，但不能视为审查完成。";

            if (eligibility == null)
                return "候选图缺少压缩资格审查维度；可读取结构，但不建议提交 whitelist 候选审查。";

            if (root != null && effectRoot == null)
                return "候选图使用旧 BuffRootNode 兼容路径；建议迁移到 EffectCompositionRootNode。";

            if (summary.HasMultipleEffectNodes && effectRoot == null)
                return "候选图包含多个 EffectNode；请补充 EffectCompositionRootNode 明确最终 EffectId / EffectClassName。";

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
        public bool EffectCompositionRootExists;
        public int EffectNodeCount;
        public string EffectOrderMode;
        public string EffectOrderSummary;
        public string LifecycleSummary;
        public bool UsesLegacyEffectBindingNode;
        public bool UsesLegacyBuffRoot;
        public bool HasMultipleEffectNodes;
        public int DeprecatedPlaceholderCount;
        public int ScriptActionNodeCount;
        public int ValidScriptActionNodeCount;
        public int InvalidScriptActionNodeCount;
        public int ScriptActionWarningCount;
        public string ScriptActionSummary;
        public string ScriptActionWarnings;
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
