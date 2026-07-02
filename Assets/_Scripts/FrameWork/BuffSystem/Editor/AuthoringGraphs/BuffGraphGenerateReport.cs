using System.Collections.Generic;
using System.Text;

namespace BuffSystem.Editor.AuthoringGraphs
{
    /// <summary>
    /// 图形化生成流程的 Editor-only 结果报告。
    /// 仅用于 Hub 展示，不修改 runtime、registry 或 whitelist。
    /// </summary>
    internal sealed class BuffGraphGenerateReport
    {
        internal bool EffectCreated;
        internal bool CompositeEffectCreated;
        internal bool BuffCreated;
        internal int CompositeEffectId;
        internal int BuffConfigId;
        internal int BuffEffectId;
        internal string CompositeEffectClassName = string.Empty;
        internal string CompositeEffectPath = string.Empty;
        internal string BuffName = string.Empty;
        internal string EffectPath = string.Empty;
        internal string BuffAssetPath = string.Empty;
        internal string RegistryMessage = string.Empty;
        internal string ManualRegistrySnippet = string.Empty;
        internal readonly List<string> Errors = new List<string>();
        internal readonly List<string> Warnings = new List<string>();
        internal readonly List<string> Infos = new List<string>();

        internal bool HasError => Errors.Count > 0;

        internal string ToDisplayText()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(HasError ? "Graph Generate: FAIL" : "Graph Generate: PASS");

            if (EffectCreated)
                builder.AppendLine("Effect 草稿生成成功：" + EffectPath);

            if (CompositeEffectCreated)
            {
                builder.AppendLine("CompositeEffect 草稿生成成功：" + CompositeEffectPath);
                builder.AppendLine("CompositeEffectId: " + CompositeEffectId);
                builder.AppendLine("CompositeEffectClassName: " + CompositeEffectClassName);
            }

            if (BuffCreated)
            {
                builder.AppendLine("Buff 草稿创建成功：" + BuffAssetPath);
                builder.AppendLine("Buff ConfigId: " + BuffConfigId);
                builder.AppendLine("BuffName: " + BuffName);
                builder.AppendLine("BuffConfigData.EffectId: " + BuffEffectId);
            }

            AppendSection(builder, "Errors", Errors);
            AppendSection(builder, "Warnings", Warnings);
            AppendSection(builder, "Infos", Infos);

            if (!string.IsNullOrWhiteSpace(RegistryMessage))
            {
                builder.AppendLine("Registry:");
                builder.AppendLine(RegistryMessage);
            }

            if (!string.IsNullOrWhiteSpace(ManualRegistrySnippet))
            {
                builder.AppendLine("Manual Registry Snippet:");
                builder.AppendLine(ManualRegistrySnippet);
            }

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
