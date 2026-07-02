using System.Collections.Generic;
using System.Text;

namespace BuffSystem
{
    /// <summary>
    /// Effect Bootstrap 自动注册写入报告；仅用于 Editor 工具反馈，不进入 runtime。
    /// </summary>
    internal sealed class BuffEffectBootstrapAutoRegistryReport
    {
        internal readonly List<string> Errors = new List<string>();
        internal readonly List<string> Warnings = new List<string>();
        internal readonly List<string> Infos = new List<string>();

        internal string BootstrapPath;
        internal bool Succeeded;
        internal bool WroteFile;

        internal bool HasError => Errors.Count > 0;

        internal string ToDisplayText()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("BuffEffectRegistryBootstrap auto 注册结果：");
            builder.AppendLine("Path: " + BootstrapPath);
            builder.AppendLine("Succeeded: " + Succeeded);
            builder.AppendLine("WroteFile: " + WroteFile);

            AppendSection(builder, "Errors", Errors);
            AppendSection(builder, "Warnings", Warnings);
            AppendSection(builder, "Infos", Infos);
            return builder.ToString();
        }

        private static void AppendSection(StringBuilder builder, string title, List<string> lines)
        {
            builder.AppendLine(title + ":");
            if (lines.Count == 0)
            {
                builder.AppendLine("- None");
                return;
            }

            for (int i = 0; i < lines.Count; i++)
                builder.AppendLine("- " + lines[i]);
        }
    }
}
