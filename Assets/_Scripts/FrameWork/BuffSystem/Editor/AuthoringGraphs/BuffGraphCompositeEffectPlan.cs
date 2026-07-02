using System.Collections.Generic;
using System.Text;

namespace BuffSystem.Editor.AuthoringGraphs
{
    /// <summary>
    /// CompositeEffect 的 Editor-only 生成计划。
    /// 该计划只描述从多个 EffectNode 合成一个普通 Effect executor 的草稿文本，不修改 runtime、registry、whitelist 或资源文件。
    /// </summary>
    internal sealed class BuffGraphCompositeEffectPlan
    {
        internal int CompositeEffectId;
        internal string CompositeEffectName = string.Empty;
        internal string CompositeEffectClassName = string.Empty;
        internal string Namespace = string.Empty;
        internal string TargetFolder = string.Empty;
        internal string TargetFilePath = string.Empty;

        internal int BuffConfigId;
        internal string BuffName = string.Empty;
        internal string BuffConfigAssetPath = string.Empty;

        internal readonly List<BuffGraphCompositeEffectPartPlan> Parts = new List<BuffGraphCompositeEffectPartPlan>();
        internal readonly List<BuffGraphCompositeLifecyclePlan> LifecyclePlans = new List<BuffGraphCompositeLifecyclePlan>();
        internal readonly List<BuffGraphEffectFieldPlan> FieldPlans = new List<BuffGraphEffectFieldPlan>();

        internal bool HasMultipleEffectNodes;
        internal string EffectOrderMode = string.Empty;
        internal int EffectNodeCount;
        internal int ExpectedActionCallCount;
        internal int GeneratedActionFieldCount;
        internal int GeneratedActionExecuteCallCount;

        internal readonly List<string> Errors = new List<string>();
        internal readonly List<string> Warnings = new List<string>();
        internal readonly List<string> Infos = new List<string>();

        internal bool HasErrors => Errors.Count > 0;

        internal string BuildActionPreview()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("CompositeEffect 调用链预览");
            for (int i = 0; i < LifecyclePlans.Count; i++)
            {
                BuffGraphCompositeLifecyclePlan lifecycle = LifecyclePlans[i];
                builder.Append(lifecycle.LifecycleName).Append(": ");
                if (lifecycle.Actions.Count == 0)
                {
                    builder.AppendLine("<none>");
                    continue;
                }

                for (int j = 0; j < lifecycle.Actions.Count; j++)
                {
                    if (j > 0)
                        builder.Append(", ");

                    BuffGraphEffectActionCallPlan action = lifecycle.Actions[j];
                    builder.Append(action.ExecutionOrder).Append(' ').Append(action.ActionDisplayName);
                }

                builder.AppendLine();
            }

            builder.Append("ExpectedActionCallCount: ").AppendLine(ExpectedActionCallCount.ToString());
            builder.Append("GeneratedActionFieldCount: ").AppendLine(GeneratedActionFieldCount.ToString());
            builder.Append("GeneratedActionExecuteCallCount: ").Append(GeneratedActionExecuteCallCount);
            return builder.ToString();
        }

        internal BuffGraphCompositeLifecyclePlan FindLifecycle(string lifecycleName)
        {
            for (int i = 0; i < LifecyclePlans.Count; i++)
            {
                if (LifecyclePlans[i].LifecycleName == lifecycleName)
                    return LifecyclePlans[i];
            }

            return null;
        }
    }

    internal sealed class BuffGraphCompositeEffectPartPlan
    {
        internal string EffectNodeName = string.Empty;
        internal int EffectNodeExecutionOrder;
        internal int SourceEffectId;
        internal string SourceEffectName = string.Empty;
        internal string SourceEffectClassName = string.Empty;

        internal readonly List<BuffGraphCompositeLifecyclePlan> LifecyclePlans = new List<BuffGraphCompositeLifecyclePlan>();
    }

    internal sealed class BuffGraphCompositeLifecyclePlan
    {
        internal string LifecycleName = string.Empty;
        internal bool IsStackChanged;
        internal readonly List<BuffGraphEffectActionCallPlan> Actions = new List<BuffGraphEffectActionCallPlan>();
        internal readonly List<string> Todos = new List<string>();
    }
}
