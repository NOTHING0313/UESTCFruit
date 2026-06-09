using System.Collections.Generic;

namespace BuffSystem.Editor.AuthoringGraphs
{
    /// <summary>
    /// BuffCandidateGraph 的最小校验器。
    /// 第一版只检查节点数量完整性；不遍历端口连接，不访问 runtime / registry / whitelist。
    /// </summary>
    internal static class BuffCandidateGraphEvaluation
    {
        internal static BuffCandidateGraphEvaluationResult Evaluate(BuffCandidateGraph graph)
        {
            BuffCandidateGraphEvaluationResult result = new BuffCandidateGraphEvaluationResult();

            if (graph == null)
            {
                result.RejectReasons.Add("Graph 为空，无法审查。");
                result.UpdateFlags();
                return result;
            }

            RequireSingleNode<BuffCandidateStartNode>(graph, "BuffCandidateStartNode", result);
            RequireSingleNode<BuffShapeNode>(graph, "BuffShapeNode", result);
            RequireAtLeastOneNode<EffectBindingNode>(graph, "EffectBindingNode", result);
            RequireSingleNode<CompressedEligibilityNode>(graph, "CompressedEligibilityNode", result);
            RequireAtLeastOneNode<RuntimeDependencyRiskNode>(graph, "RuntimeDependencyRiskNode", result);
            RequireSingleNode<CandidateDecisionNode>(graph, "CandidateDecisionNode", result);

            result.Warnings.Add("Phase 3I-10C 只做节点存在性校验；Start -> Decision 连接路径校验后续实现。");
            result.UpdateFlags();
            return result;
        }

        private static void RequireSingleNode<T>(BuffCandidateGraph graph, string nodeName, BuffCandidateGraphEvaluationResult result)
            where T : XNode.Node
        {
            int count = graph.FindNodes<T>().Count;

            if (count == 1)
                return;

            if (count == 0)
                result.RejectReasons.Add($"缺少必需节点：{nodeName}。");
            else
                result.RejectReasons.Add($"{nodeName} 必须有且只能有一个，当前数量：{count}。");
        }

        private static void RequireAtLeastOneNode<T>(BuffCandidateGraph graph, string nodeName, BuffCandidateGraphEvaluationResult result)
            where T : XNode.Node
        {
            int count = graph.FindNodes<T>().Count;

            if (count > 0)
                return;

            result.RejectReasons.Add($"缺少必需节点：{nodeName}。");
        }
    }

    /// <summary>
    /// BuffCandidateGraph 最小校验结果。
    /// 该结果不参与 Unity 序列化，只用于 Editor 诊断和 Inspector 摘要。
    /// </summary>
    public sealed class BuffCandidateGraphEvaluationResult
    {
        public bool IsComplete;
        public bool CanSubmitForReview;
        public readonly List<string> RejectReasons = new List<string>();
        public readonly List<string> Warnings = new List<string>();

        internal void UpdateFlags()
        {
            IsComplete = RejectReasons.Count == 0;
            CanSubmitForReview = IsComplete;
        }

        public string ToSummaryText()
        {
            if (IsComplete)
                return "节点数量完整。注意：Phase 3I-10C 尚未校验连接路径，也不代表 whitelist 通过。";

            return "节点数量不完整：\n" + string.Join("\n", RejectReasons);
        }
    }
}
