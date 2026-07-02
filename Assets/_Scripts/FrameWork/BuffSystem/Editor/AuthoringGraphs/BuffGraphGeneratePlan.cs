using System.Collections.Generic;
using System.Text;

namespace BuffSystem.Editor.AuthoringGraphs
{
    /// <summary>
    /// 从 BuffCandidateGraph 创建 Buff / 主 Effect 草稿前的 Editor-only 计划。
    /// 计划只描述将要创建的草稿，不代表 production Buff，也不会自动注册 Effect 或加入 whitelist。
    /// </summary>
    internal sealed class BuffGraphGeneratePlan
    {
        internal BuffCandidateGraph Graph;
        internal string GraphAssetPath = string.Empty;
        internal string GraphGuid = string.Empty;

        internal int BuffConfigId;
        internal string BuffName = string.Empty;
        internal string BuffDescription = string.Empty;
        internal string BuffConfigAssetPath = string.Empty;
        internal BuffCandidateCreateBuffDraft BuffDraft;

        internal int EffectId;
        internal string EffectName = string.Empty;
        internal string EffectClassName = string.Empty;
        internal string EffectScriptPath = string.Empty;
        internal string EffectNamespace = string.Empty;
        internal string EffectTargetFolder = string.Empty;

        internal string SelectedEffectNodeSummary = string.Empty;
        internal bool HasMultipleEffectNodes;
        internal bool WillGenerateEffect;
        internal bool WillCreateBuff;
        internal BuffGraphEffectCodegenPlan EffectCodegenPlan;

        internal readonly List<string> Errors = new List<string>();
        internal readonly List<string> Warnings = new List<string>();
        internal readonly List<string> Infos = new List<string>();

        internal bool HasError => Errors.Count > 0;

        internal string ToDisplayText()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Graph Generate Plan");
            builder.AppendLine("Graph: " + (string.IsNullOrWhiteSpace(GraphAssetPath) ? "<unsaved-or-none>" : GraphAssetPath));
            builder.AppendLine("Buff: " + BuffConfigId + " / " + BuffName);
            builder.AppendLine("Buff Asset: " + BuffConfigAssetPath);
            builder.AppendLine("Effect: " + EffectId + " / " + EffectClassName);
            builder.AppendLine("Effect Script: " + EffectScriptPath);
            builder.AppendLine("Primary Effect: " + (string.IsNullOrWhiteSpace(SelectedEffectNodeSummary) ? "<none>" : SelectedEffectNodeSummary));
            builder.AppendLine("Multiple EffectNode: " + HasMultipleEffectNodes);
            builder.AppendLine("WillGenerateEffect: " + WillGenerateEffect);
            builder.AppendLine("WillCreateBuff: " + WillCreateBuff);

            if (EffectCodegenPlan != null)
            {
                builder.AppendLine("Call Chain:");
                builder.AppendLine(EffectCodegenPlan.BuildActionPreview());
            }

            AppendSection(builder, "Errors", Errors);
            AppendSection(builder, "Warnings", Warnings);
            AppendSection(builder, "Infos", Infos);
            return builder.ToString().TrimEnd();
        }

        private static void AppendSection(StringBuilder builder, string title, List<string> lines)
        {
            if (lines == null || lines.Count == 0)
                return;

            builder.AppendLine(title + ":");
            for (int i = 0; i < lines.Count; i++)
                builder.AppendLine("- " + lines[i]);
        }
    }
}
