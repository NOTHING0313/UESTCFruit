using System.Collections.Generic;
using UnityEngine;
using XNode;

namespace BuffSystem.Editor.AuthoringGraphs
{
    /// <summary>
    /// Buff 候选审查图。
    /// 该图只服务 Editor authoring / review，不进入 Resources，不参与 runtime 加载，也不是 production 配置源。
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewBuffCandidateGraph",
        menuName = "BuffSystem/Buff Candidate Graph",
        order = 5100)]
    public sealed class BuffCandidateGraph : NodeGraph
    {
        [Tooltip("审查图契约版本。第一版只做节点存在性校验。")]
        public int GraphVersion = 1;

        [TextArea(3, 8)]
        [Tooltip("候选图说明。请记录该图用于哪个真实 gameplay Buff 候选。")]
        public string Description;

        [TextArea(3, 8)]
        [Tooltip("最近一次 Evaluate 的摘要。该字段只是 Editor 诊断结果，不是 runtime 状态。")]
        public string LastEvaluationSummary;

        /// <summary>
        /// 执行最小候选图校验。
        /// Phase 3I-10C 只检查节点数量完整性；端口连接路径校验留到后续阶段实现。
        /// </summary>
        public BuffCandidateGraphEvaluationResult Evaluate()
        {
            BuffCandidateGraphEvaluationResult result = BuffCandidateGraphEvaluation.Evaluate(this);
            LastEvaluationSummary = result.ToSummaryText();
            return result;
        }

        /// <summary>
        /// 查找图中第一个指定类型节点。
        /// </summary>
        public T FindSingleNode<T>() where T : Node
        {
            List<T> found = FindNodes<T>();
            return found.Count > 0 ? found[0] : null;
        }

        /// <summary>
        /// 查找图中全部指定类型节点。
        /// </summary>
        public List<T> FindNodes<T>() where T : Node
        {
            List<T> found = new List<T>();

            if (nodes == null)
                return found;

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is T typedNode)
                    found.Add(typedNode);
            }

            return found;
        }
    }
}
