using System.Collections.Generic;
using System.Text;

namespace BuffSystem.Editor.AuthoringGraphs
{
    /// <summary>
    /// 候选图生成 Effect 草稿前的 Editor-only 调用链计划。
    /// 该计划只描述派生代码，不修改 runtime、registry、whitelist 或 BuffConfigData。
    /// </summary>
    internal sealed class BuffGraphEffectCodegenPlan
    {
        internal int EffectId;
        internal string EffectName = string.Empty;
        internal string EffectClassName = string.Empty;
        internal string Namespace = string.Empty;
        internal string TargetFolder = string.Empty;
        internal string TargetFilePath = string.Empty;
        internal string SelectedEffectNodeSummary = string.Empty;
        internal bool HasMultipleEffectNodes;
        internal bool UsesLegacyEffectBindingNode;
        internal readonly List<BuffGraphEffectFieldPlan> FieldPlans = new List<BuffGraphEffectFieldPlan>();
        internal readonly List<BuffGraphEffectLifecyclePlan> LifecyclePlans = new List<BuffGraphEffectLifecyclePlan>();
        internal readonly List<string> Warnings = new List<string>();
        internal readonly List<string> Errors = new List<string>();

        internal int ExpectedActionCallCount;
        internal int GeneratedActionFieldCount;
        internal int GeneratedActionExecuteCallCount;

        internal bool HasErrors => Errors.Count > 0;

        internal bool HasActions
        {
            get
            {
                for (int i = 0; i < LifecyclePlans.Count; i++)
                {
                    if (LifecyclePlans[i].Actions.Count > 0)
                        return true;
                }

                return false;
            }
        }

        internal string BuildSummary()
        {
            if (LifecyclePlans.Count == 0)
                return UsesLegacyEffectBindingNode ? "使用旧 EffectBindingNode，仅导入字段，不生成生命周期调用链。" : "无生命周期调用链。";

            List<string> lines = new List<string>();
            for (int i = 0; i < LifecyclePlans.Count; i++)
            {
                BuffGraphEffectLifecyclePlan lifecycle = LifecyclePlans[i];
                lines.Add($"{lifecycle.LifecycleName}: Actions={lifecycle.Actions.Count}");
            }

            return string.Join("\n", lines);
        }

        internal string BuildActionPreview()
        {
            string[] lifecycleNames =
            {
                "OnApply",
                "OnTick",
                "OnRemove",
                "OnRefresh",
                "OnStackChanged"
            };

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("调用链预览");
            for (int i = 0; i < lifecycleNames.Length; i++)
            {
                BuffGraphEffectLifecyclePlan lifecycle = FindLifecycle(lifecycleNames[i]);
                builder.Append(lifecycleNames[i]).Append(": ");
                if (lifecycle == null || lifecycle.Actions.Count == 0)
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

        internal BuffGraphEffectLifecyclePlan FindLifecycle(string lifecycleName)
        {
            for (int i = 0; i < LifecyclePlans.Count; i++)
            {
                if (LifecyclePlans[i].LifecycleName == lifecycleName)
                    return LifecyclePlans[i];
            }

            return null;
        }
    }

    internal sealed class BuffGraphEffectLifecyclePlan
    {
        internal string LifecycleName = string.Empty;
        internal bool IncludeOverride;
        internal bool IsStackChanged;
        internal readonly List<BuffGraphEffectActionCallPlan> Actions = new List<BuffGraphEffectActionCallPlan>();
        internal readonly List<string> Todos = new List<string>();
    }

    internal sealed class BuffGraphEffectFieldPlan
    {
        internal string ActionTypeName = string.Empty;
        internal string ActionVariableName = string.Empty;
    }

    internal sealed class BuffGraphEffectActionCallPlan
    {
        internal string ActionTypeName = string.Empty;
        internal string ActionVariableName = string.Empty;
        internal string ActionDisplayName = string.Empty;
        internal string SourceNodeName = string.Empty;
        internal int ExecutionOrder;
    }

    internal sealed class BuffGraphEffectCodegenRequest
    {
        internal int EffectId;
        internal string EffectClassName = string.Empty;
        internal string Namespace = string.Empty;
        internal string TargetFolder = string.Empty;
        internal string TargetFilePath = string.Empty;
        internal bool OnApply;
        internal bool OnTick;
        internal bool OnRemove;
        internal bool OnRefresh;
        internal bool OnStackChanged;
    }
}
