using System.Collections.Generic;
using System.Text;
using XNode;

namespace BuffSystem.Editor.AuthoringGraphs
{
    /// <summary>
    /// EffectNode 顺序解析工具。只服务 Editor 图语义，不进入 runtime。
    /// </summary>
    internal static class BuffGraphEffectOrderUtility
    {
        internal enum OrderMode
        {
            None,
            ExecutionOrder,
            NextChain,
            InvalidNextChain
        }

        internal sealed class OrderResult
        {
            internal OrderMode Mode = OrderMode.None;
            internal readonly List<EffectNode> OrderedEffects = new List<EffectNode>();
            internal readonly List<string> Errors = new List<string>();
            internal readonly List<string> Warnings = new List<string>();

            internal string ModeLabel
            {
                get
                {
                    if (Mode == OrderMode.NextChain)
                        return "NextChain";

                    if (Mode == OrderMode.InvalidNextChain)
                        return "InvalidNextChain";

                    if (Mode == OrderMode.ExecutionOrder)
                        return "ExecutionOrder";

                    return "None";
                }
            }
        }

        internal static OrderResult Build(BuffCandidateGraph graph)
        {
            OrderResult result = new OrderResult();
            if (graph == null)
                return result;

            List<EffectNode> effects = graph.FindNodes<EffectNode>();
            if (effects.Count == 0)
                return result;

            Dictionary<EffectNode, List<EffectNode>> outgoing = new Dictionary<EffectNode, List<EffectNode>>();
            Dictionary<EffectNode, int> incomingCounts = new Dictionary<EffectNode, int>();
            for (int i = 0; i < effects.Count; i++)
            {
                outgoing.Add(effects[i], new List<EffectNode>());
                incomingCounts.Add(effects[i], 0);
            }

            bool hasNextEdge = false;
            for (int i = 0; i < effects.Count; i++)
            {
                EffectNode effect = effects[i];
                NodePort port = effect.GetPort("Next");
                if (port == null || port.ConnectionCount == 0)
                    continue;

                List<NodePort> connections = port.GetConnections();
                for (int connectionIndex = 0; connectionIndex < connections.Count; connectionIndex++)
                {
                    Node connectedNode = connections[connectionIndex] != null ? connections[connectionIndex].node : null;
                    if (connectedNode is EffectNode nextEffect && incomingCounts.ContainsKey(nextEffect))
                    {
                        hasNextEdge = true;
                        outgoing[effect].Add(nextEffect);
                        incomingCounts[nextEffect]++;
                        continue;
                    }

                    result.Warnings.Add($"EffectNode[{i}].Next 连接到非 EffectNode，顺序解析会忽略该连接。");
                }
            }

            if (!hasNextEdge)
            {
                result.Mode = OrderMode.ExecutionOrder;
                AddExecutionOrderSorted(effects, result.OrderedEffects);
                AddDuplicateExecutionOrderErrors(effects, result.Errors);
                return result;
            }

            result.Mode = OrderMode.InvalidNextChain;
            ValidateNextDegrees(outgoing, incomingCounts, result.Errors);
            List<EffectNode> starts = FindNextStarts(effects, incomingCounts);
            if (starts.Count != 1)
                result.Errors.Add($"EffectNode.Next 链必须有且只能有一个起点，当前起点数量：{starts.Count}。");

            if (result.Errors.Count == 0)
            {
                List<EffectNode> chain = WalkChain(starts[0], outgoing, result.Errors);
                if (chain.Count != effects.Count)
                    result.Errors.Add($"EffectNode.Next 链未覆盖全部 EffectNode，链长度={chain.Count}，节点数={effects.Count}。");

                if (result.Errors.Count == 0)
                {
                    List<EffectNode> executionOrder = new List<EffectNode>();
                    AddExecutionOrderSorted(effects, executionOrder);
                    AddDuplicateExecutionOrderErrors(effects, result.Errors);
                    if (!SameOrder(chain, executionOrder))
                        result.Errors.Add("EffectNode.Next 链与 ExecutionOrder 顺序冲突；请统一二者后再生成。");

                    if (result.Errors.Count == 0)
                    {
                        result.Mode = OrderMode.NextChain;
                        result.OrderedEffects.AddRange(chain);
                        return result;
                    }
                }
            }

            AddExecutionOrderSorted(effects, result.OrderedEffects);
            return result;
        }

        internal static string BuildSummary(OrderResult result)
        {
            if (result == null || result.OrderedEffects.Count == 0)
                return "无 EffectNode";

            StringBuilder builder = new StringBuilder();
            builder.Append(result.ModeLabel).Append(": ");
            for (int i = 0; i < result.OrderedEffects.Count; i++)
            {
                EffectNode effect = result.OrderedEffects[i];
                if (i > 0)
                    builder.Append(" -> ");

                string name = !string.IsNullOrWhiteSpace(effect.EffectClassName)
                    ? effect.EffectClassName
                    : (!string.IsNullOrWhiteSpace(effect.EffectName) ? effect.EffectName : "<unnamed>");
                builder.Append(effect.ExecutionOrder).Append(' ').Append(name);
            }

            return builder.ToString();
        }

        internal static int CompareEffectNodes(EffectNode left, EffectNode right)
        {
            int order = left.ExecutionOrder.CompareTo(right.ExecutionOrder);
            if (order != 0)
                return order;

            int id = left.EffectId.CompareTo(right.EffectId);
            if (id != 0)
                return id;

            return string.Compare(left.EffectClassName, right.EffectClassName, System.StringComparison.Ordinal);
        }

        private static void AddExecutionOrderSorted(List<EffectNode> source, List<EffectNode> target)
        {
            List<EffectNode> copy = new List<EffectNode>(source);
            copy.Sort(CompareEffectNodes);
            target.AddRange(copy);
        }

        private static void AddDuplicateExecutionOrderErrors(List<EffectNode> effects, List<string> errors)
        {
            Dictionary<int, int> orderCounts = new Dictionary<int, int>();
            for (int i = 0; i < effects.Count; i++)
            {
                int order = effects[i].ExecutionOrder;
                if (!orderCounts.ContainsKey(order))
                    orderCounts.Add(order, 0);

                orderCounts[order]++;
            }

            foreach (KeyValuePair<int, int> pair in orderCounts)
            {
                if (pair.Value > 1)
                    errors.Add($"EffectNode ExecutionOrder 重复：{pair.Key}，数量={pair.Value}。");
            }
        }

        private static void ValidateNextDegrees(
            Dictionary<EffectNode, List<EffectNode>> outgoing,
            Dictionary<EffectNode, int> incomingCounts,
            List<string> errors)
        {
            foreach (KeyValuePair<EffectNode, List<EffectNode>> pair in outgoing)
            {
                if (pair.Value.Count > 1)
                    errors.Add($"EffectNode.Next 不允许分叉：{BuildEffectName(pair.Key)} 连接数量={pair.Value.Count}。");
            }

            foreach (KeyValuePair<EffectNode, int> pair in incomingCounts)
            {
                if (pair.Value > 1)
                    errors.Add($"EffectNode.Next 不允许多个前驱指向同一节点：{BuildEffectName(pair.Key)} 前驱数量={pair.Value}。");
            }
        }

        private static List<EffectNode> FindNextStarts(List<EffectNode> effects, Dictionary<EffectNode, int> incomingCounts)
        {
            List<EffectNode> starts = new List<EffectNode>();
            for (int i = 0; i < effects.Count; i++)
            {
                if (incomingCounts[effects[i]] == 0)
                    starts.Add(effects[i]);
            }

            return starts;
        }

        private static List<EffectNode> WalkChain(
            EffectNode start,
            Dictionary<EffectNode, List<EffectNode>> outgoing,
            List<string> errors)
        {
            List<EffectNode> chain = new List<EffectNode>();
            HashSet<EffectNode> visited = new HashSet<EffectNode>();
            EffectNode current = start;
            while (current != null)
            {
                if (!visited.Add(current))
                {
                    errors.Add("EffectNode.Next 链存在循环。");
                    break;
                }

                chain.Add(current);
                List<EffectNode> nextList = outgoing[current];
                current = nextList.Count == 1 ? nextList[0] : null;
            }

            return chain;
        }

        private static bool SameOrder(List<EffectNode> left, List<EffectNode> right)
        {
            if (left.Count != right.Count)
                return false;

            for (int i = 0; i < left.Count; i++)
            {
                if (!ReferenceEquals(left[i], right[i]))
                    return false;
            }

            return true;
        }

        private static string BuildEffectName(EffectNode effect)
        {
            if (effect == null)
                return "<null>";

            if (!string.IsNullOrWhiteSpace(effect.EffectClassName))
                return effect.EffectClassName;

            if (!string.IsNullOrWhiteSpace(effect.EffectName))
                return effect.EffectName;

            return effect.name;
        }
    }
}
