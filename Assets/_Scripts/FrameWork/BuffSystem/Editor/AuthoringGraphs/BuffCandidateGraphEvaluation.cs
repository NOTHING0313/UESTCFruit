using System.Collections.Generic;

namespace BuffSystem.Editor.AuthoringGraphs
{
    /// <summary>
    /// BuffCandidateGraph 的最小校验器。
    /// 当前只检查节点数量与 EffectNode 轻量连接统计；不访问 runtime / registry / whitelist。
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

            int rootCount = graph.FindNodes<BuffRootNode>().Count;
            int effectRootCount = graph.FindNodes<EffectCompositionRootNode>().Count;
            int startCount = graph.FindNodes<BuffCandidateStartNode>().Count;
            int effectNodeCount = graph.FindNodes<EffectNode>().Count;
            int legacyEffectCount = graph.FindNodes<EffectBindingNode>().Count;
            int scriptActionCount = graph.FindNodes<ScriptActionNode>().Count;

            if (startCount == 0)
                result.RejectReasons.Add("缺少必需节点：BuffCandidateStartNode。");
            else if (startCount > 1)
                result.RejectReasons.Add($"BuffCandidateStartNode 必须有且只能有一个，当前数量：{startCount}。");

            if (rootCount > 0)
                result.Warnings.Add("检测到旧 BuffRootNode；该节点仅为兼容旧图保留，新图请使用 EffectCompositionRootNode。");

            if (rootCount > 1)
                result.RejectReasons.Add($"旧 BuffRootNode 不应超过一个，当前数量：{rootCount}。");

            if (effectRootCount == 0)
                result.Warnings.Add("未找到 EffectCompositionRootNode；Graph Generate 会 fallback 到 EffectNode / 旧 EffectBinding，但新图建议补充组合根。");
            else if (effectRootCount > 1)
                result.RejectReasons.Add($"EffectCompositionRootNode 必须有且只能有一个，当前数量：{effectRootCount}。");

            RequireSingleNode<BuffShapeNode>(graph, "BuffShapeNode", result);
            RequireEffectInfo(graph, effectRootCount, effectNodeCount, legacyEffectCount, result);
            WarnMissingOptionalNode<CompressedEligibilityNode>(graph, "CompressedEligibilityNode", result);
            WarnMissingOptionalNode<RuntimeDependencyRiskNode>(graph, "RuntimeDependencyRiskNode", result);
            RequireSingleNode<CandidateDecisionNode>(graph, "CandidateDecisionNode", result);
            EvaluateEffectNodes(graph, result);
            EvaluateScriptActionNodes(graph, result);

            result.Warnings.Add(scriptActionCount > 0
                ? "已识别 ScriptActionNode；它仍是 Editor-only 设计节点，只会在 Effect Template 手动生成草稿时参与调用链。"
                : "Phase 3I-11I 未发现 ScriptActionNode；当前图仍可只表达 Buff / Effect 结构。");
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

        private static void WarnMissingOptionalNode<T>(BuffCandidateGraph graph, string nodeName, BuffCandidateGraphEvaluationResult result)
            where T : XNode.Node
        {
            int count = graph.FindNodes<T>().Count;

            if (count == 1)
                return;

            if (count == 0)
                result.Warnings.Add($"缺少候选审查维度节点：{nodeName}。");
            else
                result.RejectReasons.Add($"{nodeName} 不应重复，当前数量：{count}。");
        }

        private static void RequireEffectInfo(BuffCandidateGraph graph, int effectRootCount, int effectNodeCount, int legacyEffectCount, BuffCandidateGraphEvaluationResult result)
        {
            if (effectNodeCount > 0)
            {
                if (legacyEffectCount > 0)
                    result.Warnings.Add("当前图同时存在 EffectCompositionRootNode / EffectNode 和旧 EffectBindingNode；新组合结构会优先生效，旧节点仅作为无新结构时的 fallback。");

                if (effectRootCount > 0)
                    return;

                result.Warnings.Add("EffectNode 已存在，但缺少 EffectCompositionRootNode；Graph Generate 可 fallback 到 EffectNode，但新图建议补充组合根。");
                return;
            }

            if (legacyEffectCount > 0)
            {
                result.Warnings.Add("当前图仅使用旧 EffectBindingNode；该路径只作为无 EffectCompositionRootNode / EffectNode 时的 legacy fallback，新图建议迁移。");
                return;
            }

            result.RejectReasons.Add("缺少 Effect 信息：至少需要 EffectCompositionRootNode + EffectNode，或旧 EffectBindingNode fallback。");
        }

        private static void EvaluateEffectNodes(BuffCandidateGraph graph, BuffCandidateGraphEvaluationResult result)
        {
            List<EffectNode> effects = graph.FindNodes<EffectNode>();
            if (effects.Count == 0)
                return;

            for (int i = 0; i < effects.Count; i++)
            {
                EffectNode effect = effects[i];

                if (effect.EffectId <= 0)
                    result.Warnings.Add($"EffectNode[{i}] EffectId 小于等于 0，后续可由自动 ID 分配或 Effect Template 修复。");

                if (string.IsNullOrWhiteSpace(effect.EffectClassName))
                    result.Warnings.Add($"EffectNode[{i}] EffectClassName 为空。");

                if (CountLifecycleConnections(effect) == 0)
                    result.Warnings.Add($"EffectNode[{i}] 没有任何生命周期端口连接。");

                EvaluateLifecycleConnections(effect, i, result);
            }

            BuffGraphEffectOrderUtility.OrderResult orderResult = BuffGraphEffectOrderUtility.Build(graph);
            AppendRange(result.RejectReasons, orderResult.Errors);
            AppendRange(result.Warnings, orderResult.Warnings);
            if (orderResult.Mode == BuffGraphEffectOrderUtility.OrderMode.NextChain)
                result.Warnings.Add("EffectNode 使用 Next 链定义显式顺序；ExecutionOrder 已确认一致。");

            BuffRootNode root = graph.FindSingleNode<BuffRootNode>();
            if (root != null && CountPortConnections(root, "Effects") == 0)
                result.Warnings.Add("旧 BuffRootNode Effects 端口尚未连接 EffectNode。");

            EffectCompositionRootNode effectRoot = graph.FindSingleNode<EffectCompositionRootNode>();
            if (effectRoot != null && CountPortConnections(effectRoot, "Effects") == 0)
                result.Warnings.Add("EffectCompositionRootNode Effects 端口尚未连接 EffectNode。");
        }

        private static void EvaluateScriptActionNodes(BuffCandidateGraph graph, BuffCandidateGraphEvaluationResult result)
        {
            List<ScriptActionNode> actions = graph.FindNodes<ScriptActionNode>();
            if (actions.Count == 0)
                return;

            Dictionary<int, int> orderCounts = new Dictionary<int, int>();
            for (int i = 0; i < actions.Count; i++)
            {
                ScriptActionNode action = actions[i];
                BuffScriptActionValidationResult validation = BuffScriptActionNodeValidator.RefreshFromScript(action);

                if (!orderCounts.ContainsKey(action.ExecutionOrder))
                    orderCounts.Add(action.ExecutionOrder, 0);

                orderCounts[action.ExecutionOrder]++;

                for (int errorIndex = 0; errorIndex < validation.Errors.Count; errorIndex++)
                    result.Warnings.Add($"ScriptActionNode[{i}] 校验错误：{validation.Errors[errorIndex]}");

                for (int warningIndex = 0; warningIndex < validation.Warnings.Count; warningIndex++)
                    result.Warnings.Add($"ScriptActionNode[{i}] 警告：{validation.Warnings[warningIndex]}");
            }

            foreach (KeyValuePair<int, int> pair in orderCounts)
            {
                if (pair.Value > 1)
                    result.RejectReasons.Add($"ScriptActionNode ExecutionOrder 重复：{pair.Key}，数量={pair.Value}。");
            }
        }

        private static void EvaluateLifecycleConnections(EffectNode node, int effectIndex, BuffCandidateGraphEvaluationResult result)
        {
            EvaluateLifecyclePort(node, effectIndex, "OnApply", result);
            EvaluateLifecyclePort(node, effectIndex, "OnTick", result);
            EvaluateLifecyclePort(node, effectIndex, "OnRemove", result);
            EvaluateLifecyclePort(node, effectIndex, "OnRefresh", result);
            EvaluateLifecyclePort(node, effectIndex, "OnStackChanged", result);
        }

        private static void EvaluateLifecyclePort(EffectNode node, int effectIndex, string portName, BuffCandidateGraphEvaluationResult result)
        {
            XNode.NodePort port = node.GetPort(portName);
            if (port == null || port.ConnectionCount == 0)
                return;

            List<XNode.NodePort> connections = port.GetConnections();
            for (int i = 0; i < connections.Count; i++)
            {
                XNode.Node connectedNode = connections[i] != null ? connections[i].node : null;
                if (connectedNode is ScriptActionNode scriptAction)
                {
                    BuffScriptActionValidationResult validation = BuffScriptActionNodeValidator.RefreshFromScript(scriptAction);
                    if (!validation.IsValid)
                        result.Warnings.Add($"EffectNode[{effectIndex}].{portName} 连接到无效 ScriptActionNode：{scriptAction.ActionDisplayName}。");

                    if (CountPortConnections(scriptAction, "Next") > 0)
                        result.Warnings.Add($"EffectNode[{effectIndex}].{portName} 连接的 ScriptActionNode 使用了 Next；当前语义要求 Next 与 ExecutionOrder 一致，生成前会继续校验冲突。");

                    continue;
                }

                if (connectedNode is EmptyActionPlaceholderNode)
                {
                    result.Warnings.Add($"EffectNode[{effectIndex}].{portName} 仍连接到 EmptyActionPlaceholderNode，占位节点不会生成 Effect 调用。");
                    continue;
                }

                result.Warnings.Add($"EffectNode[{effectIndex}].{portName} 连接到未知节点类型：{(connectedNode != null ? connectedNode.GetType().Name : "<null>")}。");
            }
        }

        private static int CountLifecycleConnections(EffectNode node)
        {
            return CountPortConnections(node, "OnApply")
                + CountPortConnections(node, "OnTick")
                + CountPortConnections(node, "OnRemove")
                + CountPortConnections(node, "OnRefresh")
                + CountPortConnections(node, "OnStackChanged");
        }

        private static int CountPortConnections(XNode.Node node, string portName)
        {
            XNode.NodePort port = node.GetPort(portName);
            return port != null ? port.ConnectionCount : 0;
        }

        private static void AppendRange(List<string> target, List<string> source)
        {
            if (target == null || source == null)
                return;

            for (int i = 0; i < source.Count; i++)
                target.Add(source[i]);
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
                return "候选图基础语义检查通过。注意：这不代表 whitelist 通过，也不证明 runtime / rollback ready。";

            return "候选图存在阻断问题：\n" + string.Join("\n", RejectReasons);
        }
    }
}
